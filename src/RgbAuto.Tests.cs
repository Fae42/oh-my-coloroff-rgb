// RGB Auto Off - diagnostic/test scaffolding (NOT part of the shipped daemon).
// Compile (from repo root): csc -nologo -platform:x64 -target:winexe -out:bin\RgbAuto.Tests.exe -r:bin\RgbAuto.exe <same refs as RgbAuto.cs> src\RgbAuto.Tests.cs
// Usage:
//   RgbAuto.Tests.exe selftest              - pure-logic tests for the debounce state machine (no hardware)
//   RgbAuto.Tests.exe listen                - log display power events without touching LEDs
//   RgbAuto.Tests.exe test svc sleep 45     - push Sleep effect for 45s, then restore
//   RgbAuto.Tests.exe test svc rainbow 30   - push Rainbow for 30s, then restore
//   RgbAuto.Tests.exe test probe            - LedDriver probe dump
//   RgbAuto.Tests.exe test nprobe 20        - raw iGameMBoard.dll direct test
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using iGameAPI.Contracts.LED;
using iGameAPI.Contracts.LED.iGameLEDDevices;

namespace RgbAuto
{
    internal class LedDriver : IDisposable
    {
        ILEDBase led;
        iGameLEDDevice[] devs;

        public bool Init()
        {
            for (int attempt = 1; attempt <= 6; attempt++)
            {
                try
                {
                    Log.W("init attempt " + attempt);
                    led = LEDFactory.CreateLEDDevice(iGameLEDDeviceType.MBoard);
                    led.Init();
                    int n = led.GetDeviceCount();
                    Log.W("device count=" + n);
                    if (n > 0)
                    {
                        devs = led.GetDeviceList();
                        foreach (var d in devs)
                            Log.W("dev idx=" + d.DeviceIndex + " name=" + d.Name + " ch=" + d.ChannelCount);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Log.W("init error: " + ex.GetType().Name + " " + ex.Message);
                }
                Thread.Sleep(3000);
            }
            return false;
        }

        iGameEasyCalc_LEDParameter BuildParam(iGameEasyCalc_LEDType t)
        {
            if (t == iGameEasyCalc_LEDType.Sleep) return LedStack.SleepParam();
            var p = LedStack.RainbowParam(); // sane defaults, overlaid with the Lite config below
            p.LEDType = t;
            try
            {
                string cfg = Path.Combine(Program.LiteDir, "CCData", "Configs", "GlobalLightingConfig.json");
                if (File.Exists(cfg))
                {
                    string json = File.ReadAllText(cfg);
                    int bri = GetInt(json, "Bri"); if (bri >= 0) p.Brightness = bri;
                    int spd = GetInt(json, "Speed"); if (spd >= 0) p.Speed = spd;
                    int cnt = GetInt(json, "LEDCount"); if (cnt > 0) p.LEDCount = cnt;
                    int fps = GetInt(json, "FPS"); if (fps > 0) p.FPS = fps;
                    int dir = GetInt(json, "Dir"); if (dir >= 0) p.Direction = dir;
                    int sen = GetInt(json, "Sensitivity"); if (sen >= 0) p.Sensitivity = sen;
                    int r = -1, g = -1, b = -1;
                    int si = json.IndexOf("\"Static\"");
                    if (si >= 0)
                    {
                        string seg = json.Substring(si, Math.Min(200, json.Length - si));
                        r = GetInt(seg, "R"); g = GetInt(seg, "G"); b = GetInt(seg, "B");
                    }
                    if (r >= 0 && g >= 0 && b >= 0)
                    {
                        var c = new RGB();
                        c.R = (byte)r; c.G = (byte)g; c.B = (byte)b;
                        p.Color = c;
                    }
                    Log.W("config applied from Lite");
                }
            }
            catch (Exception ex) { Log.W("cfg read: " + ex.Message); }
            return p;
        }

        static int GetInt(string json, string key)
        {
            string pat = "\"" + key + "\":";
            int i = json.IndexOf(pat, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return -1;
            i += pat.Length;
            int j = i;
            while (j < json.Length && (char.IsDigit(json[j]) || json[j] == '-')) j++;
            int v;
            return int.TryParse(json.Substring(i, j - i), out v) ? v : -1;
        }

        public void Apply(iGameEasyCalc_LEDType t)
        {
            if (led == null || devs == null) return;
            var p = BuildParam(t);
            foreach (var d in devs)
            {
                try
                {
                    bool ok = led.Set_DeviceCalc(d, p);
                    Log.W("Set_DeviceCalc " + t + " on " + d.Name + " -> " + ok);
                    try { led.Set_EasyCalc_Effects(d); }
                    catch (Exception ex2) { Log.W("Set_EasyCalc_Effects: " + ex2.Message); }
                }
                catch (Exception ex)
                {
                    Log.W("apply error: " + ex.GetType().Name + " " + ex.Message);
                }
            }
        }

        public void Dispose()
        {
            try { if (led != null) led.SetFPS(0); } catch { }
        }

        public void Probe()
        {
            if (led == null || devs == null) { Log.W("probe: not inited"); return; }
            foreach (var d in devs)
            {
                Log.W("probe dev " + d.Name + " LEDCalcMode=" + d.LEDCalcMode + " PMode=" + d.PMode + " Win11=" + d.Win11Support);
            }
            var p = BuildParam(iGameEasyCalc_LEDType.Rainbow);
            try { bool r1 = led.Set_DeviceCalc(p); Log.W("global Set_DeviceCalc -> " + r1); }
            catch (Exception ex) { Log.W("global Set_DeviceCalc EX: " + ex.GetType().Name + " " + ex.Message); }
            try { bool r2 = led.Set_EasyCalc_Effects(); Log.W("global Set_EasyCalc_Effects -> " + r2); }
            catch (Exception ex) { Log.W("global Set_EasyCalc_Effects EX: " + ex.GetType().Name + " " + ex.Message); }
            foreach (var d in devs)
            {
                try { bool r3 = led.Set_DeviceCalc(d, p); Log.W("perdev Set_DeviceCalc after global -> " + r3); }
                catch (Exception ex) { Log.W("perdev EX: " + ex.Message); }
            }
            Log.W("probe sleeping 20s so effect is observable");
            Thread.Sleep(20000);
        }
    }

    internal class NativeMboard
    {
        const string DLL = "iGameMBoard.dll";

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void iGameMBoard_Init();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void iGameMBoard_UnInit();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int iGameMBoard_Get_Device_Count();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void iGameMBoard_Get_Device_Info(int index, IntPtr info);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool iGameMBoard_Set_EasyCalc(IntPtr dev);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool iGameMBoard_Set_DeviceCalc(IntPtr dev, ref EasyParam param);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void iGameMBoard_Set_FPS(int fps);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct EasyParam
        {
            public int LEDType;
            public byte R;
            public byte G;
            public byte B;
            public byte Pad;
            public float Brightness;
            public int Sensitivity;
            public int Speed;
            public int LEDCount;
            public int FPS;
            public int Direction;
        }

        public static int Run(int secs)
        {
            try
            {
                Log.W("nprobe: init");
                iGameMBoard_Init();
                int n = iGameMBoard_Get_Device_Count();
                Log.W("nprobe: device count=" + n);
                if (n <= 0) return 1;
                IntPtr buf = Marshal.AllocHGlobal(8192);
                try
                {
                    for (int i = 0; i < 8192; i++) Marshal.WriteByte(buf, i, 0);
                    iGameMBoard_Get_Device_Info(0, buf);
                    Log.W("nprobe: got device info");

                    var p = new EasyParam();
                    p.LEDType = 4; // Rainbow
                    p.Brightness = 255; p.Speed = 2; p.LEDCount = 100; p.FPS = 30; p.Direction = 0; p.Sensitivity = 10;
                    iGameMBoard_Set_FPS(30);

                    bool r1 = iGameMBoard_Set_EasyCalc(buf);
                    Log.W("nprobe: Set_EasyCalc -> " + r1);
                    bool r2 = iGameMBoard_Set_DeviceCalc(buf, ref p);
                    Log.W("nprobe: Set_DeviceCalc(Rainbow) -> " + r2);
                    Log.W("nprobe: streaming rainbow " + secs + "s, watch the fans");
                    Thread.Sleep(secs * 1000);

                    p.LEDType = 0; // Sleep
                    p.Brightness = 0;
                    bool r3 = iGameMBoard_Set_DeviceCalc(buf, ref p);
                    Log.W("nprobe: Set_DeviceCalc(Sleep) -> " + r3);
                    Log.W("nprobe: streaming sleep " + secs + "s, watch the fans");
                    Thread.Sleep(secs * 1000);
                    Log.W("nprobe: done");
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                    try { iGameMBoard_UnInit(); } catch { }
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log.W("nprobe EX: " + ex.GetType().Name + " " + ex.Message);
                return 1;
            }
        }
    }

    // Single-shot service test: push an effect for N seconds, then restore.
    // Note: returns UnknowError(1) as a false negative - effects still apply.
    internal class SvcMode
    {
        public static int Run(string[] args)
        {
            try
            {
                int deviceCount;
                var ids = new System.Collections.Generic.List<string>();
                var svc = LedStack.Create(out deviceCount, ids);
                Log.W("device infos count=" + deviceCount);

                try { svc.InitLightingEffect(); Log.W("InitLightingEffect done"); }
                catch (Exception ex) { Log.W("InitLightingEffect: " + ex.Message); }

                string which = args.Length > 2 ? args[2] : "sleep";
                int secs = TestMode.ParseSecs(args, 3, 20);
                if (which == "sleep" || which == "rainbow")
                {
                    var p = which == "sleep" ? LedStack.SleepParam() : LedStack.RainbowParam();
                    foreach (var devId in ids)
                    {
                        var rr = svc.SetLightingEffect(devId, p);
                        Log.W("SetLightingEffect(" + which + ") on " + devId + " -> " + rr + " (" + (int)rr + ")");
                    }
                    Log.W("watching " + secs + "s");
                    Thread.Sleep(secs * 1000);
                    // Experiment: release the LED stack instead of restoring, then exit.
                    // If the board reverts to its hardware default effect, the fans keep
                    // flowing after this process exits (that's the tray-exit ideal).
                    try { svc.StopLEDService(); Log.W("StopLEDService done"); }
                    catch (Exception ex) { Log.W("StopLEDService: " + ex.Message); }
                    try { var u = svc.Uninit(); Log.W("Uninit -> " + u + " (" + (int)u + ")"); }
                    catch (Exception ex) { Log.W("Uninit: " + ex.Message); }
                    Log.W("exiting; watch whether the fans keep flowing or freeze");
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log.W("svc EX: " + ex.GetType().Name + " " + ex.Message);
                Log.W(ex.StackTrace);
                return 1;
            }
        }
    }

    // Event-only listener: registers GUID_CONSOLE_DISPLAY_STATE and logs, no LED access.
    internal class ListenerForm : Form
    {
        IntPtr regHandle;

        public ListenerForm()
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Load += delegate
            {
                Guid g = Native.GUID_CONSOLE_DISPLAY_STATE;
                regHandle = Native.RegisterPowerSettingNotification(Handle, ref g, Native.DEVICE_NOTIFY_WINDOW_HANDLE);
                Log.W("[listen] registered, no LED init");
            };
        }

        protected override void WndProc(ref Message m)
        {
            DisplayState state;
            if (Native.TryDecodeDisplayState(ref m, out state))
                Log.W("display state -> " + state + " [listen]");
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (regHandle != IntPtr.Zero) Native.UnregisterPowerSettingNotification(regHandle);
            base.OnFormClosing(e);
        }
    }

    internal class TestMode
    {
        // first int argument at or after startIndex wins
        internal static int ParseSecs(string[] args, int startIndex, int fallback)
        {
            for (int i = startIndex; i < args.Length; i++)
            {
                int v;
                if (int.TryParse(args[i], out v)) return v;
            }
            return fallback;
        }

        public static int Run(string[] args)
        {
            string which = args.Length > 1 ? args[1] : "probe";
            int secs = ParseSecs(args, 2, 20);
            if (which == "svc")
                return SvcMode.Run(args);
            var d = new LedDriver();
            try
            {
                if (!d.Init()) return 1;
                if (which == "nprobe")
                    return NativeMboard.Run(secs);
                if (which == "probe")
                {
                    d.Probe();
                    return 0;
                }
                d.Apply(which == "sleep" ? iGameEasyCalc_LEDType.Sleep : iGameEasyCalc_LEDType.Rainbow);
                Log.W("test mode: " + which + " for " + secs + "s");
                Thread.Sleep(secs * 1000);
                Log.W("test done");
                return 0;
            }
            finally { d.Dispose(); }
        }
    }

    // Pure-logic self tests for the daemon's debounce/echo-guard state machine.
    // No hardware, no message loop; run: RgbAuto.Tests.exe selftest
    internal static class SelfTest
    {
        static int failures;
        static void Check(bool cond, string name)
        {
            Console.WriteLine((cond ? "PASS  " : "FAIL  ") + name);
            if (!cond) failures++;
        }

        [DllImport("kernel32.dll")] static extern bool AttachConsole(int dwProcessId);

        public static int Run()
        {
            AttachConsole(-1); // winexe has no console; attach to the caller's
            var t0 = new DateTime(2026, 1, 1);
            Debounce d;

            // --- normal screen-off path ---
            d = new Debounce(t0);
            Check(d.OnEvent(DisplayState.Off, t0.AddHours(1)) == EventResult.Accepted, "off event accepted");
            Check(d.Tick(t0.AddHours(1).AddSeconds(1.4)) == LedAction.None, "no write before off is stable 1.5s");
            Check(d.Tick(t0.AddHours(1).AddSeconds(1.6)) == LedAction.Sleep, "sleep once off is stable 1.5s");
            d.Commit(LedAction.Sleep, t0.AddHours(1).AddSeconds(1.6));
            Check(d.Tick(t0.AddHours(1).AddSeconds(30)) == LedAction.None, "no duplicate sleep after commit");

            // --- normal wake path ---
            d = new Debounce(t0);
            d.OnEvent(DisplayState.Off, t0.AddHours(1));
            d.Commit(LedAction.Sleep, t0.AddHours(1).AddSeconds(2));
            Check(d.OnEvent(DisplayState.On, t0.AddHours(1).AddSeconds(6)) == EventResult.Accepted, "wake accepted past refractory");
            Check(d.Tick(t0.AddHours(1).AddSeconds(6.9)) == LedAction.None, "no restore before on is stable 1.0s");
            Check(d.Tick(t0.AddHours(1).AddSeconds(7.1)) == LedAction.Restore, "restore once on is stable 1.0s");

            // --- echo guard: on-direction swallowed within 3s of a lights-off write ---
            d = new Debounce(t0);
            d.OnEvent(DisplayState.Off, t0.AddHours(1));
            d.Commit(LedAction.Sleep, t0.AddHours(1).AddSeconds(2));
            Check(d.OnEvent(DisplayState.On, t0.AddHours(1).AddSeconds(3.2)) == EventResult.Swallowed, "on event inside refractory swallowed");
            Check(d.RawState == DisplayState.Off, "raw state stays off after a swallowed wake");
            Check(d.Tick(t0.AddHours(1).AddSeconds(30)) == LedAction.None, "no spurious restore after swallowed wake");

            // --- regression: manual tray restore sticks after a swallowed wake ---
            d.Manual(DisplayState.On, t0.AddHours(1).AddSeconds(40));
            Check(d.AppliedState == DisplayState.On && d.RawState == DisplayState.On, "manual restore syncs believed state");
            Check(d.Tick(t0.AddHours(1).AddSeconds(50)) == LedAction.None, "timer does not undo a manual restore");

            // --- off-direction events are never swallowed ---
            d = new Debounce(t0);
            d.Commit(LedAction.Restore, t0.AddHours(1));
            Check(d.OnEvent(DisplayState.Off, t0.AddHours(1).AddSeconds(0.5)) == EventResult.Accepted, "off event accepted even inside refractory");

            // --- refractory blocks tick writes after any commit ---
            d.OnEvent(DisplayState.Off, t0.AddHours(1).AddSeconds(0.6));
            Check(d.Tick(t0.AddHours(1).AddSeconds(2.2)) == LedAction.None, "tick blocked by refractory after a write");
            Check(d.Tick(t0.AddHours(1).AddSeconds(3.2)) == LedAction.Sleep, "tick allowed again after refractory");

            // --- manual lights-off sticks while the screen is on ---
            d = new Debounce(t0);
            d.Manual(DisplayState.Off, t0.AddHours(1));
            Check(d.Tick(t0.AddHours(1).AddSeconds(30)) == LedAction.None, "timer does not undo a manual lights-off");

            // --- failed write (no commit) retries on the next tick ---
            d = new Debounce(t0);
            d.OnEvent(DisplayState.Off, t0.AddHours(1));
            Check(d.Tick(t0.AddHours(1).AddSeconds(2)) == LedAction.Sleep, "sleep suggested");
            Check(d.Tick(t0.AddHours(1).AddSeconds(3)) == LedAction.Sleep, "uncommitted write retried next tick");

            // --- dim counts as on-direction ---
            d = new Debounce(t0);
            d.OnEvent(DisplayState.Off, t0.AddHours(1));
            d.Commit(LedAction.Sleep, t0.AddHours(1).AddSeconds(2));
            Check(d.OnEvent(DisplayState.Dim, t0.AddHours(1).AddSeconds(6)) == EventResult.Accepted, "dim accepted past refractory");
            Check(d.Tick(t0.AddHours(1).AddSeconds(7.1)) == LedAction.Restore, "dim restores like on");

            // --- same-state event is a no-op ---
            Check(new Debounce(t0).OnEvent(DisplayState.On, t0.AddHours(1)) == EventResult.NoChange, "same-state event is no-change");

            Console.WriteLine(failures == 0 ? "\r\nALL PASS" : "\r\n" + failures + " FAILURES");
            Console.Out.Flush();
            return failures == 0 ? 0 : 1;
        }
    }

    internal class TestProgram
    {
        [STAThread]
        static int Main(string[] args)
        {
            Program.Bootstrap();

            if (args.Length > 0 && args[0] == "listen")
            {
                Log.W("=== listen mode (no LED access) ===");
                Application.Run(new ListenerForm());
                return 0;
            }
            if (args.Length > 0 && args[0] == "selftest")
                return SelfTest.Run();
            if (args.Length == 0 || args[0] != "test")
            {
                Console.WriteLine("usage: RgbAuto.Tests.exe selftest | listen | test svc sleep 45 | test svc rainbow 30 | test probe | test nprobe 20 | test sleep 30 | test rainbow 30");
                return 2;
            }
            return TestMode.Run(args);
        }
    }
}
