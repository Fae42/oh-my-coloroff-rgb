// RGB Auto Off daemon (shipped core).
// Compile daemon (from repo root):   csc -nologo -platform:x64 -target:winexe -win32icon:assets\app.ico -out:bin\RgbAuto.exe <refs below> src\RgbAuto.cs
// Compile tests (from repo root):    csc -nologo -platform:x64 -target:winexe -out:bin\RgbAuto.Tests.exe -r:bin\RgbAuto.exe <refs below> src\RgbAuto.Tests.cs
// refs: "-r:C:\Program Files\iGC.Lite\iGameAPI.Contracts.dll" "-r:C:\Program Files\iGC.Lite\iGC.Lite.Service.dll"
//       "-r:C:\Program Files\iGC.Lite\iGameCenter.ConfigManager.dll" "-r:C:\Program Files\iGC.Lite\Castle.Core.dll"
//       "-r:C:\Program Files\iGC.Lite\iGameCenter.Hardware.dll" "-r:C:\Program Files\iGC.Lite\iGameCenter.Contracts.dll"
//       -r:System.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using iGameAPI.Contracts.LED;
using iGameAPI.Contracts.LED.iGameLEDDevices;

namespace RgbAuto
{
    // Windows console display state (GUID_CONSOLE_DISPLAY_STATE data value)
    public enum DisplayState { Off = 0, On = 1, Dim = 2 }

    public static class Native
    {
        public const int WM_POWERBROADCAST = 0x0218;
        public const int PBT_POWERSETTINGCHANGE = 0x8013;
        public const int DEVICE_NOTIFY_WINDOW_HANDLE = 0;
        public static readonly Guid GUID_CONSOLE_DISPLAY_STATE = new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid powerSettingGuid, int flags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterPowerSettingNotification(IntPtr handle);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr handle);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct POWERBROADCAST_SETTING
        {
            public Guid PowerSetting;
            public int DataLength;
            public byte Data;
        }

        // Decode a GUID_CONSOLE_DISPLAY_STATE broadcast; shared by MainForm and the tests' ListenerForm.
        public static bool TryDecodeDisplayState(ref Message m, out DisplayState state)
        {
            state = DisplayState.Off;
            if (m.Msg != WM_POWERBROADCAST || m.WParam.ToInt32() != PBT_POWERSETTINGCHANGE) return false;
            var st = (POWERBROADCAST_SETTING)Marshal.PtrToStructure(m.LParam, typeof(POWERBROADCAST_SETTING));
            if (st.PowerSetting != GUID_CONSOLE_DISPLAY_STATE) return false;
            state = (DisplayState)BitConverter.ToInt32(new byte[] { st.Data, 0, 0, 0 }, 0);
            return true;
        }
    }

    public static class Log
    {
        const long MaxBytes = 1 * 1024 * 1024; // rotate to .1 when the log exceeds 1 MB
        static string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rgbrun.log");
        public static void W(string msg)
        {
            try
            {
                var fi = new FileInfo(file);
                if (fi.Exists && fi.Length > MaxBytes)
                {
                    string bak = file + ".1";
                    File.Delete(bak);
                    File.Move(file, bak);
                }
                File.AppendAllText(file, DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + "\r\n");
            }
            catch { }
        }
    }

    // Shared ConfigManager construction (reflection: non-public ctor of ConfigManager<T>).
    // Used by the daemon (ServiceLed) and by RgbAuto.Tests (SvcMode).
    public static class Cm
    {
        public static object Make(Type cfgType, Castle.Core.Logging.ILoggerFactory f, string path)
        {
            var gm = cfgType.Assembly.GetType("iGameCenter.ConfigManager.ConfigManager`1").MakeGenericType(cfgType);
            var ctor = gm.GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                null,
                new Type[] { typeof(Castle.Core.Logging.ILoggerFactory), typeof(string) },
                null);
            try
            {
                return ctor.Invoke(new object[] { f, path });
            }
            catch (TargetInvocationException tie)
            {
                Log.W("Cm.Make(" + cfgType.Name + ") inner: " + (tie.InnerException == null ? "?" : tie.InnerException.GetType().Name + " " + tie.InnerException.Message));
                throw;
            }
        }
    }

    // Construction of the iGC.Lite LED service stack, shared by the daemon (ServiceLed)
    // and RgbAuto.Tests (SvcMode). Hardware requires a resident pumper.
    public static class LedStack
    {
        public static iGC.Lite.Service.LED.LEDAPIService Create(out int deviceCount, System.Collections.Generic.List<string> idsOut)
        {
            string cfgDir = Path.Combine(Program.LiteDir, "CCData", "Configs");
            var loggerFactory = new Castle.Core.Logging.TraceLoggerFactory(Castle.Core.Logging.LoggerLevel.Error);

            object cmNormal = Cm.Make(typeof(iGameCenter.ConfigManager.NormalConfig), loggerFactory, Path.Combine(cfgDir, "NormalConfig.json"));
            object cmLight = Cm.Make(typeof(iGameCenter.ConfigManager.LiteRGBEffectConfig), loggerFactory, Path.Combine(cfgDir, "LiteRGBEffectConfig.json"));
            object cmOrder = Cm.Make(typeof(iGameCenter.ConfigManager.Configs.LEDOrderConfig), loggerFactory, Path.Combine(cfgDir, "LEDOrderConfig.json"));
            var cmf = new iGameCenter.ConfigManager.ConfigManagerFactory(loggerFactory);

            iGameCenter.Contracts.Hardware.IHardwareMonitor hw = null;
            try
            {
                hw = new iGameCenter.Hardware.HardwareMonitor(new iGameCenter.Hardware.HardwareMonitorConfig());
                Log.W("hw monitor created");
            }
            catch (Exception ex) { Log.W("hw monitor failed, using null: " + ex.Message); }

            var svc = new iGC.Lite.Service.LED.LEDAPIService(
                loggerFactory,
                (iGameCenter.ConfigManager.IConfigManager<iGameCenter.ConfigManager.NormalConfig>)cmNormal,
                (iGameCenter.ConfigManager.IConfigManager<iGameCenter.ConfigManager.LiteRGBEffectConfig>)cmLight,
                (iGameCenter.ConfigManager.IConfigManager<iGameCenter.ConfigManager.Configs.LEDOrderConfig>)cmOrder,
                cmf, hw);
            var r = svc.Init();
            Log.W("svc.Init -> " + r + " (" + (int)r + ")");

            deviceCount = 0;
            var infos = svc.GetDeviceInfos();
            if (infos != null)
            {
                deviceCount = infos.Count;
                foreach (var di in infos)
                {
                    Log.W("  dev type=" + di.DeviceType + " name=" + di.Name + " id=" + di.ID + " idx=" + di.DeviceIndex);
                    if (idsOut != null && !string.IsNullOrEmpty(di.ID)) idsOut.Add(di.ID);
                }
            }
            return svc;
        }

        // lights-out parameter set (Sleep effect, zero brightness)
        public static iGameEasyCalc_LEDParameter SleepParam()
        {
            var p = new iGameEasyCalc_LEDParameter();
            p.LEDType = iGameEasyCalc_LEDType.Sleep;
            p.Brightness = 0;
            return p;
        }

        // default Rainbow parameter set (tests use as-is; LedDriver overlays the Lite config on top)
        public static iGameEasyCalc_LEDParameter RainbowParam()
        {
            var p = new iGameEasyCalc_LEDParameter();
            p.LEDType = iGameEasyCalc_LEDType.Rainbow;
            p.Brightness = 255; p.Speed = 2; p.LEDCount = 100; p.FPS = 30; p.Direction = 0; p.Sensitivity = 10;
            return p;
        }
    }

    // LED control via the iGC.Lite native service stack (hardware requires a resident pumper).
    internal class ServiceLed
    {
        iGC.Lite.Service.LED.LEDAPIService svc;
        readonly System.Collections.Generic.List<string> ids = new System.Collections.Generic.List<string>();

        public bool Init()
        {
            int deviceCount;
            svc = LedStack.Create(out deviceCount, ids);
            return deviceCount > 0;
        }

        public void Restore()
        {
            if (svc == null) return;
            svc.InitLightingEffect();
            Log.W("restored configured effect");
        }

        public void Sleep()
        {
            if (svc == null) return;
            var p = LedStack.SleepParam();
            foreach (var devId in ids)
            {
                var r = svc.SetLightingEffect(devId, p);
                Log.W("SetLightingEffect(Sleep) on " + devId + " -> " + r + " (" + (int)r + ")");
            }
        }
    }

    internal class MainForm : Form
    {
        IntPtr regHandle;
        ServiceLed drv;
        bool ledInitialized; // true once the LED stack is live (drives the spawn-failure fallback)
        NotifyIcon trayIcon;
        Icon iconOn, iconOff; // cached; created once, destroyed on close
        // after each LED write: no further writes, and On-direction events are echo-guarded
        const double RefractorySeconds = 3.0;
        // debounce state
        DisplayState rawState = DisplayState.On;  // last reported display state
        DateTime rawSince = DateTime.Now;         // since when it has held
        DisplayState appliedState = DisplayState.On; // state we last pushed to the LEDs
        DateTime lastAction = DateTime.Now;
        System.Windows.Forms.Timer tmr;

        public MainForm()
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Load += OnLoad;
        }

        public static bool LiteRunning()
        {
            try { return System.Diagnostics.Process.GetProcessesByName("iGC.Lite").Length > 0; }
            catch { return false; }
        }

        // shared poll: used by a deferred instance (Main) and by the in-process handoff fallback
        public static void WaitForLiteExit()
        {
            while (LiteRunning()) Thread.Sleep(3000);
        }

        // Hand over to a deferred instance that waits for iGC.Lite to exit, then becomes the daemon,
        // and exit — UNLESS the spawn itself fails before the LED stack is live: then we must not
        // leave zero daemons behind, so we wait in-process (exactly what the deferred instance would
        // have done) and return, letting OnLoad continue. Callers must tolerate both postconditions.
        public void HandoffOrWaitInProcess(int code)
        {
            bool spawned = false;
            for (int attempt = 1; attempt <= 3 && !spawned; attempt++)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        Arguments = "--deferred",
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    });
                    spawned = true;
                    Log.W("spawned deferred instance; exiting (" + code + ")");
                }
                catch (Exception ex)
                {
                    Log.W("spawn deferred failed (attempt " + attempt + "/3): " + ex.Message);
                    Thread.Sleep(1000);
                }
            }
            if (!spawned && !ledInitialized)
            {
                Log.W("spawn failed before LED init; waiting in-process for iGC.Lite to exit.");
                WaitForLiteExit();
                Log.W("iGC.Lite closed; resuming in-process.");
                return; // OnLoad continues and initializes normally
            }
            if (!spawned) Log.W("spawn failed with LED stack live; exiting anyway (" + code + "). Relaunch RgbAuto.exe after closing iGC.Lite.");
            // best-effort cleanup; Environment.Exit skips OnFormClosing
            if (trayIcon != null) { try { trayIcon.Visible = false; trayIcon.Dispose(); } catch { } }
            DisposeIcons();
            Environment.Exit(code);
        }

        // cached icons were created with Icon.FromHandle (we own the handles) — destroy them
        void DisposeIcons()
        {
            foreach (var ic in new Icon[] { iconOn, iconOff })
            {
                if (ic != null) { try { Native.DestroyIcon(ic.Handle); } catch { } }
            }
            iconOn = iconOff = null;
        }

        void CreateTray()
        {
            iconOn = MakeIcon(true);
            iconOff = MakeIcon(false);
            trayIcon = new NotifyIcon();
            trayIcon.Icon = iconOn;
            trayIcon.Visible = true;
            trayIcon.Text = "RGB \u606f\u5c4f\u5173\u706f\u5b88\u62a4"; // 息屏关灯守护
            var menu = new ContextMenuStrip();
            // 恢复灯效
            var miRestore = new ToolStripMenuItem("\u6062\u590d\u706f\u6548");
            miRestore.Click += delegate { ManualSet(DisplayState.On); };
            // 立即关灯
            var miOff = new ToolStripMenuItem("\u7acb\u5373\u5173\u706f");
            miOff.Click += delegate { ManualSet(DisplayState.Off); };
            // 退出（停用看门狗后直接退出，灯保持当前状态不变；重新运行 setup_autostart.ps1 或手动启动即恢复）
            var miExit = new ToolStripMenuItem("\u9000\u51fa");
            miExit.Click += delegate
            {
                Log.W("exit requested from tray; disabling watchdog, leaving lights as-is");
                DisableWatchdog();
                Application.Exit();
            };
            menu.Items.Add(miRestore);
            menu.Items.Add(miOff);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miExit);
            trayIcon.ContextMenuStrip = menu;
            trayIcon.BalloonTipTitle = "RGB Auto";
            trayIcon.BalloonTipText = "\u5b88\u62a4\u5df2\u542f\u52a8\uff0c\u53f3\u952e\u6258\u76d8\u56fe\u6807\u53ef\u64cd\u4f5c"; // 守护已启动，右键托盘图标可操作
            trayIcon.ShowBalloonTip(3000);
        }

        // Tray exit must not be resurrected: disable the watchdog task (fails open - we still exit).
        // Only the tray path does this; yield/crash exits keep the watchdog alive on purpose.
        static void DisableWatchdog()
        {
            const string task = "RGB Auto Off Watchdog";
            try
            {
                var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Change /TN \"" + task + "\" /Disable",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                });
                if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } Log.W("disable watchdog: schtasks timed out"); return; }
                Log.W("disable watchdog: schtasks exit " + p.ExitCode + (p.ExitCode == 0 ? "" : " (watchdog will still relaunch the daemon)"));
            }
            catch (Exception ex) { Log.W("disable watchdog failed: " + ex.Message); }
        }

        void ManualSet(DisplayState target)
        {
            if (drv == null) return;
            try
            {
                if (target == DisplayState.On) { drv.Restore(); Log.W("manual restore"); }
                else { drv.Sleep(); Log.W("manual lights off"); }
                appliedState = target;
                lastAction = DateTime.Now;
                SetTrayIcon();
            }
            catch (Exception ex) { Log.W("manual: " + ex.Message); }
        }

        void SetTrayIcon()
        {
            if (trayIcon == null || iconOn == null || iconOff == null) return;
            try { trayIcon.Icon = (appliedState == DisplayState.On) ? iconOn : iconOff; } catch { }
        }

        static System.Drawing.Icon MakeIcon(bool on)
        {
            using (var bmp = new Bitmap(16, 16))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(on ? Color.Goldenrod : Color.DimGray))
                    g.FillEllipse(brush, 2, 2, 12, 12);
                using (var pen = new Pen(Color.FromArgb(60, 60, 60), 1))
                    g.DrawEllipse(pen, 2, 2, 12, 12);
                return Icon.FromHandle(bmp.GetHicon()); // caller must DestroyIcon the handle
            }
        }

        void OnLoad(object s, EventArgs e)
        {
            if (LiteRunning())
            {
                Log.W("iGC.Lite is running at daemon start; deferring.");
                HandoffOrWaitInProcess(Program.ExitYieldAtStart);
            }
            drv = new ServiceLed();
            // init watchdog: native Init can block if another app holds the hardware
            bool ok = false;
            Thread t = new Thread(delegate()
            {
                try { ok = drv.Init(); }
                catch (Exception ex) { ok = false; Log.W("init thread: " + ex.GetType().Name + ": " + ex.Message); }
            });
            t.IsBackground = true;
            t.Start();
            if (!t.Join(40000))
            {
                Log.W("init blocked >40s; another program may hold LED hardware (iGC.Lite?). exiting.");
                Environment.Exit(Program.ExitInitBlocked);
            }
            if (!ok)
            {
                Log.W("no LED device found; exiting.");
                Environment.Exit(Program.ExitNoDevice);
            }
            ledInitialized = true;
            Guid g = Native.GUID_CONSOLE_DISPLAY_STATE;
            regHandle = Native.RegisterPowerSettingNotification(Handle, ref g, Native.DEVICE_NOTIFY_WINDOW_HANDLE);
            Log.W("registered display-state notify, handle=" + regHandle);
            // initial state: display is on -> apply configured effect
            drv.Restore();
            appliedState = DisplayState.On;
            lastAction = DateTime.Now;

            // debounce timer: apply LED state only when display state is stable
            tmr = new System.Windows.Forms.Timer();
            tmr.Interval = 500;
            tmr.Tick += delegate
            {
                try
                {
                    double stable = (DateTime.Now - rawSince).TotalSeconds;
                    double sinceAction = (DateTime.Now - lastAction).TotalSeconds;
                    if (sinceAction < RefractorySeconds) return; // refractory: LED writes can themselves trigger state events
                    if (rawState == DisplayState.Off && stable >= 1.5 && appliedState != DisplayState.Off)
                    {
                        appliedState = DisplayState.Off;
                        lastAction = DateTime.Now;
                        Log.W("stable " + rawState + " for " + stable.ToString("0.0") + "s -> lights off");
                        try { drv.Sleep(); } catch (Exception ex) { Log.W("sleep: " + ex.Message); }
                        SetTrayIcon();
                    }
                    else if (rawState != DisplayState.Off && stable >= 1.0 && appliedState != DisplayState.On)
                    {
                        appliedState = DisplayState.On;
                        lastAction = DateTime.Now;
                        Log.W("stable " + rawState + " for " + stable.ToString("0.0") + "s -> lights restored");
                        try { drv.Restore(); } catch (Exception ex) { Log.W("restore: " + ex.Message); }
                        SetTrayIcon();
                    }
                }
                catch (Exception ex) { Log.W("tick: " + ex.Message); }
            };
            tmr.Start();
            CreateTray();

            // if user starts iGC.Lite, yield and exit to avoid fighting over hardware;
            // a deferred instance waits for iGC.Lite to close and then resumes control
            var watcher = new Thread(delegate()
            {
                while (true)
                {
                    Thread.Sleep(10000);
                    if (LiteRunning())
                    {
                        Log.W("iGC.Lite started; daemon yielding.");
                        HandoffOrWaitInProcess(Program.ExitYieldWhileRunning);
                    }
                }
            });
            watcher.IsBackground = true;
            watcher.Start();
        }

        protected override void WndProc(ref Message m)
        {
            DisplayState state;
            if (Native.TryDecodeDisplayState(ref m, out state))
            {
                Log.W("display state -> " + state);
                if (state != rawState)
                {
                    double sinceAction = (DateTime.Now - lastAction).TotalSeconds;
                    // One-way echo guard: a Sleep write can echo back a spurious "On" within the
                    // refractory window; answering it would resurrect the lights while the display
                    // is off and re-arm the write/echo loop, so swallow it. Off events are NEVER
                    // ignored — lights-on-with-display-off defeats the product's purpose, and a
                    // wrongly accepted Off only parks the lights in the safe direction. Costs:
                    // a genuine wake within 3s of a lights-off write is missed until the next
                    // transition (tray "恢复灯效" covers it), and a Restore write's own Off echo is
                    // accepted, parking lights off — both fail safe.
                    if (sinceAction < RefractorySeconds && state != DisplayState.Off && appliedState == DisplayState.Off)
                    {
                        Log.W("ignored (probable echo of Sleep write, " + sinceAction.ToString("0.0") + "s ago)");
                    }
                    else
                    {
                        rawState = state;
                        rawSince = DateTime.Now;
                    }
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (regHandle != IntPtr.Zero) Native.UnregisterPowerSettingNotification(regHandle);
            if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); trayIcon = null; }
            DisposeIcons();
            base.OnFormClosing(e);
        }
    }

    public class Program
    {
        public const string LiteDir = @"C:\Program Files\iGC.Lite";
        const string MutexName = @"Local\RgbAutoDaemon.SingleInstance";

        // process exit codes (visible to Task Scheduler and in logs)
        public const int ExitOk = 0;
        public const int ExitInitBlocked = 3;    // native Init blocked >40s, another app holds the hardware
        public const int ExitNoDevice = 4;       // no LED device enumerated
        public const int ExitYieldAtStart = 5;   // iGC.Lite running when the daemon started
        public const int ExitYieldWhileRunning = 6; // iGC.Lite started while the daemon was running

        // Resolve and load the vendor assemblies under LiteDir and make its native iGameAPI
        // folder reachable. Shared by the daemon and RgbAuto.Tests.
        public static void Bootstrap()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, a) =>
            {
                string p = Path.Combine(LiteDir, new AssemblyName(a.Name).Name + ".dll");
                return File.Exists(p) ? Assembly.LoadFrom(p) : null;
            };
            Assembly.LoadFrom(Path.Combine(LiteDir, "iGameAPI.Contracts.dll"));

            Directory.SetCurrentDirectory(LiteDir);
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            Environment.SetEnvironmentVariable("PATH", Path.Combine(LiteDir, "iGameAPI") + ";" + pathEnv);
        }

        [STAThread]
        static int Main(string[] args)
        {
            bool deferred = args.Length > 0 && args[0] == "--deferred";
            // Single instance (daemon or deferred) per session; exit 0 on duplicates so
            // Task Scheduler never treats it as failure. A deferred instance is spawned just
            // before the old daemon exits, so it retries briefly to cover that handoff race.
            Mutex single = new Mutex(true, MutexName);
            bool owns = false;
            int attempts = deferred ? 30 : 1;
            for (int i = 0; i < attempts && !owns; i++)
            {
                try { owns = single.WaitOne(0); }
                catch (AbandonedMutexException) { owns = true; } // previous holder died; we now own it
                if (!owns && i + 1 < attempts) Thread.Sleep(500);
            }
            if (!owns)
            {
                Log.W("another instance is already active; exiting.");
                return ExitOk;
            }
            if (deferred)
            {
                Log.W("=== deferred start: waiting for iGC.Lite to exit ===");
                MainForm.WaitForLiteExit();
                Log.W("=== iGC.Lite closed; daemon resuming ===");
            }
            Bootstrap();

            Log.W("=== daemon start ===");
            Application.Run(new MainForm());
            GC.KeepAlive(single); // hold the instance mutex for the whole process lifetime
            return ExitOk;
        }
    }
}
