// RGB Auto Off - diagnostic/test scaffolding (NOT part of the shipped daemon).
// Compile: csc -nologo -platform:x64 -target:winexe -out:RgbAuto.Tests.exe -r:RgbAuto.exe <same refs as RgbAuto.cs> RgbAuto.Tests.cs
// Usage:
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
            var p = new iGameEasyCalc_LEDParameter();
            if (t == iGameEasyCalc_LEDType.Sleep)
            {
                p.LEDType = iGameEasyCalc_LEDType.Sleep;
                p.Brightness = 0;
                return p;
            }
            p.LEDType = t;
            p.Brightness = 255; p.Speed = 2; p.LEDCount = 100; p.FPS = 30; p.Direction = 0; p.Sensitivity = 10;
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
            string cfgDir = Path.Combine(Program.LiteDir, "CCData", "Configs");
            try
            {
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
                Log.W("svc constructed");
                var r = svc.Init();
                Log.W("svc.Init -> " + r + " (" + (int)r + ")");

                var infos = svc.GetDeviceInfos();
                Log.W("device infos count=" + (infos == null ? -1 : infos.Count));
                var ids = new System.Collections.Generic.List<string>();
                if (infos != null)
                {
                    foreach (var di in infos)
                    {
                        Log.W("  dev type=" + di.DeviceType + " name=" + di.Name + " id=" + di.ID + " idx=" + di.DeviceIndex);
                        if (!string.IsNullOrEmpty(di.ID)) ids.Add(di.ID);
                    }
                }

                try { svc.InitLightingEffect(); Log.W("InitLightingEffect done"); }
                catch (Exception ex) { Log.W("InitLightingEffect: " + ex.Message); }

                string which = args.Length > 2 ? args[2] : "sleep";
                int secs = 20;
                for (int i = 3; i < args.Length; i++)
                {
                    int v;
                    if (int.TryParse(args[i], out v)) { secs = v; break; }
                }
                if (which == "sleep" || which == "rainbow")
                {
                    var p = new iGameAPI.Contracts.LED.iGameEasyCalc_LEDParameter();
                    if (which == "sleep") { p.LEDType = iGameAPI.Contracts.LED.iGameEasyCalc_LEDType.Sleep; p.Brightness = 0; }
                    else { p.LEDType = iGameAPI.Contracts.LED.iGameEasyCalc_LEDType.Rainbow; p.Brightness = 255; p.Speed = 2; p.LEDCount = 100; p.FPS = 30; p.Direction = 0; p.Sensitivity = 10; }
                    foreach (var devId in ids)
                    {
                        var rr = svc.SetLightingEffect(devId, p);
                        Log.W("SetLightingEffect(" + which + ") on " + devId + " -> " + rr + " (" + (int)rr + ")");
                    }
                    Log.W("watching " + secs + "s");
                    Thread.Sleep(secs * 1000);
                    try { svc.InitLightingEffect(); Log.W("restored saved effect"); }
                    catch (Exception ex) { Log.W("restore: " + ex.Message); }
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
            if (m.Msg == Native.WM_POWERBROADCAST && m.WParam.ToInt32() == Native.PBT_POWERSETTINGCHANGE)
            {
                var st = (Native.POWERBROADCAST_SETTING)Marshal.PtrToStructure(m.LParam, typeof(Native.POWERBROADCAST_SETTING));
                if (st.PowerSetting == Native.GUID_CONSOLE_DISPLAY_STATE)
                {
                    var state = (DisplayState)BitConverter.ToInt32(new byte[] { st.Data, 0, 0, 0 }, 0);
                    Log.W("display state -> " + state + " [listen]");
                }
            }
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
        public static int Run(string[] args)
        {
            string which = args.Length > 1 ? args[1] : "probe";
            int secs = 20;
            for (int i = 2; i < args.Length; i++)
            {
                int v;
                if (int.TryParse(args[i], out v)) { secs = v; break; }
            }
            if (which == "svc")
                return SvcMode.Run(args);
            var d = new LedDriver();
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
    }

    internal class TestProgram
    {
        [STAThread]
        static int Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, a) =>
            {
                string p = Path.Combine(Program.LiteDir, new AssemblyName(a.Name).Name + ".dll");
                return File.Exists(p) ? Assembly.LoadFrom(p) : null;
            };
            Assembly.LoadFrom(Path.Combine(Program.LiteDir, "iGameAPI.Contracts.dll"));

            Directory.SetCurrentDirectory(Program.LiteDir);
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            Environment.SetEnvironmentVariable("PATH", Path.Combine(Program.LiteDir, "iGameAPI") + ";" + pathEnv);

            if (args.Length > 0 && args[0] == "listen")
            {
                Log.W("=== listen mode (no LED access) ===");
                Application.Run(new ListenerForm());
                return 0;
            }
            if (args.Length == 0 || args[0] != "test")
            {
                Console.WriteLine("usage: RgbAuto.Tests.exe listen | test svc sleep 45 | test svc rainbow 30 | test probe | test nprobe 20 | test sleep 30 | test rainbow 30");
                return 2;
            }
            return TestMode.Run(args);
        }
    }
}
