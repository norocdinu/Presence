// Presence - a low-profile keep-awake / keep-active tray utility for Windows.
//
// Design notes (why this is hard to flag by "checking systems"):
//  * Primary keep-awake uses SetThreadExecutionState - the OS power API. This
//    emits NO synthetic input at all; it is the same mechanism media players
//    use. There is nothing for an input-hook / jiggler-detector to see.
//  * Optional "keep active" (resets the OS idle timer so presence stays green)
//    uses an invisible zero-pixel mouse move, fired at RANDOMIZED intervals and
//    ONLY while you are actually idle - so there is no fixed periodic pattern
//    and no interference with real input.
//  * No admin rights, no drivers, no network. Single user-mode process.
//  * The exe/process name has no bearing on behaviour - rename it to anything.
//
// Build: see build.cmd (csc /target:winexe).

using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// Version / publisher metadata. A binary with real version info and a product
// name looks like normal software to AV heuristics; a blank one scores higher.
[assembly: AssemblyTitle("Presence")]
[assembly: AssemblyDescription("Keep-awake / keep-active tray utility")]
[assembly: AssemblyProduct("Presence")]
[assembly: AssemblyCompany("Presence")]
[assembly: AssemblyCopyright("Copyright (c) 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Presence
{
    static class Native
    {
        [FlagsAttribute]
        public enum EXECUTION_STATE : uint
        {
            ES_CONTINUOUS = 0x80000000,
            ES_SYSTEM_REQUIRED = 0x00000001,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_AWAYMODE_REQUIRED = 0x00000040,
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [StructLayout(LayoutKind.Sequential)]
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        public static extern uint GetTickCount();

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi; // only the mouse variant is used
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public const uint INPUT_MOUSE = 0;
        public const uint MOUSEEVENTF_MOVE = 0x0001;

        public static uint IdleSeconds()
        {
            LASTINPUTINFO lii = new LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref lii)) return 0;
            return (GetTickCount() - lii.dwTime) / 1000u;
        }

        // Invisible zero-pixel relative mouse move. Resets the OS idle timer
        // (GetLastInputInfo) without moving the cursor a single pixel.
        public static void NudgeInput()
        {
            INPUT[] inp = new INPUT[1];
            inp[0].type = INPUT_MOUSE;
            inp[0].mi.dx = 0;
            inp[0].mi.dy = 0;
            inp[0].mi.dwFlags = MOUSEEVENTF_MOVE;
            inp[0].mi.dwExtraInfo = IntPtr.Zero;
            SendInput(1, inp, Marshal.SizeOf(typeof(INPUT)));
        }
    }

    class Config
    {
        public bool KeepAwake = true;       // hold the machine awake (power API)
        public bool KeepDisplayOn = true;   // also keep the monitor on
        public bool KeepActive = true;      // nudge idle timer so presence stays "active"
        public int IdleThreshold = 50;      // only nudge after this many seconds of real idle
        public int MinInterval = 40;        // jitter window (seconds) between nudges
        public int MaxInterval = 110;
        public bool StopEnabled = false;    // auto-stop at a time of day
        public string StopTime = "18:00";   // HH:mm, 24h
        public bool StartAtLogin = false;

        static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Presence"); } }
        static string PathFile { get { return Path.Combine(Dir, "config.ini"); } }

        public static Config Load()
        {
            Config c = new Config();
            try
            {
                if (File.Exists(PathFile))
                {
                    foreach (string raw in File.ReadAllLines(PathFile))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#")) continue;
                        int eq = line.IndexOf('=');
                        if (eq < 1) continue;
                        string k = line.Substring(0, eq).Trim();
                        string v = line.Substring(eq + 1).Trim();
                        switch (k)
                        {
                            case "KeepAwake": c.KeepAwake = ParseBool(v, c.KeepAwake); break;
                            case "KeepDisplayOn": c.KeepDisplayOn = ParseBool(v, c.KeepDisplayOn); break;
                            case "KeepActive": c.KeepActive = ParseBool(v, c.KeepActive); break;
                            case "IdleThreshold": int.TryParse(v, out c.IdleThreshold); break;
                            case "MinInterval": int.TryParse(v, out c.MinInterval); break;
                            case "MaxInterval": int.TryParse(v, out c.MaxInterval); break;
                            case "StopEnabled": c.StopEnabled = ParseBool(v, c.StopEnabled); break;
                            case "StopTime": c.StopTime = v; break;
                            case "StartAtLogin": c.StartAtLogin = ParseBool(v, c.StartAtLogin); break;
                        }
                    }
                }
            }
            catch { }
            if (c.MinInterval < 5) c.MinInterval = 5;
            if (c.MaxInterval < c.MinInterval) c.MaxInterval = c.MinInterval + 1;
            return c;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string[] lines = new string[]
                {
                    "# Presence config - edit values or use the tray menu",
                    "KeepAwake=" + KeepAwake,
                    "KeepDisplayOn=" + KeepDisplayOn,
                    "KeepActive=" + KeepActive,
                    "IdleThreshold=" + IdleThreshold,
                    "MinInterval=" + MinInterval,
                    "MaxInterval=" + MaxInterval,
                    "StopEnabled=" + StopEnabled,
                    "StopTime=" + StopTime,
                    "StartAtLogin=" + StartAtLogin,
                };
                File.WriteAllLines(PathFile, lines);
            }
            catch { }
        }

        static bool ParseBool(string v, bool dflt)
        {
            bool b;
            if (bool.TryParse(v, out b)) return b;
            if (v == "1") return true;
            if (v == "0") return false;
            return dflt;
        }
    }

    class TrayApp : ApplicationContext
    {
        readonly NotifyIcon _tray;
        readonly System.Windows.Forms.Timer _timer;
        readonly Config _cfg;
        readonly Random _rng = new Random();
        readonly string _exePath;

        bool _paused = false;          // manual pause
        DateTime _nextNudge = DateTime.MinValue;
        Icon _iconActive, _iconIdle;

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "Presence";

        public TrayApp()
        {
            _cfg = Config.Load();
            _exePath = Application.ExecutablePath;

            // Keep the registry Run value in sync with the saved preference.
            ApplyStartup(_cfg.StartAtLogin);

            _iconActive = MakeIcon(Color.FromArgb(0x2E, 0xCC, 0x71)); // green
            _iconIdle = MakeIcon(Color.FromArgb(0x95, 0xA5, 0xA6));   // gray

            _tray = new NotifyIcon();
            _tray.Icon = _iconActive;
            _tray.Visible = true;
            _tray.Text = "Presence";
            _tray.DoubleClick += (s, e) => TogglePause();
            BuildMenu();

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 4000;
            _timer.Tick += (s, e) => Tick();
            _timer.Start();

            ScheduleNextNudge();
            Tick();
        }

        bool InActiveWindow()
        {
            if (!_cfg.StopEnabled) return true;
            DateTime stop;
            if (!TryParseStop(_cfg.StopTime, out stop)) return true;
            return DateTime.Now < stop;
        }

        static bool TryParseStop(string hhmm, out DateTime today)
        {
            today = DateTime.MinValue;
            DateTime t;
            if (DateTime.TryParseExact(hhmm, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t) ||
                DateTime.TryParse(hhmm, out t))
            {
                DateTime now = DateTime.Now;
                today = new DateTime(now.Year, now.Month, now.Day, t.Hour, t.Minute, 0);
                return true;
            }
            return false;
        }

        bool Working { get { return !_paused && InActiveWindow(); } }

        void Tick()
        {
            if (Working)
            {
                if (_cfg.KeepAwake)
                {
                    Native.EXECUTION_STATE flags = Native.EXECUTION_STATE.ES_CONTINUOUS | Native.EXECUTION_STATE.ES_SYSTEM_REQUIRED;
                    if (_cfg.KeepDisplayOn) flags |= Native.EXECUTION_STATE.ES_DISPLAY_REQUIRED;
                    Native.SetThreadExecutionState(flags);
                }
                else
                {
                    Native.SetThreadExecutionState(Native.EXECUTION_STATE.ES_CONTINUOUS);
                }

                if (_cfg.KeepActive && DateTime.Now >= _nextNudge)
                {
                    if (Native.IdleSeconds() >= (uint)_cfg.IdleThreshold)
                    {
                        Native.NudgeInput();
                        ScheduleNextNudge();
                    }
                    // if the user is active we leave the timer and re-check next tick
                }
            }
            else
            {
                // Release the power request so the machine behaves normally.
                Native.SetThreadExecutionState(Native.EXECUTION_STATE.ES_CONTINUOUS);
            }

            UpdateUi();
        }

        void ScheduleNextNudge()
        {
            int secs = _rng.Next(_cfg.MinInterval, _cfg.MaxInterval + 1);
            _nextNudge = DateTime.Now.AddSeconds(secs);
        }

        // ---- UI ----

        ToolStripMenuItem _miStatus, _miPause, _miAwake, _miDisplay, _miActive, _miStartup, _miStopHdr;

        void BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            _miStatus = new ToolStripMenuItem("Presence");
            _miStatus.Enabled = false;
            menu.Items.Add(_miStatus);
            menu.Items.Add(new ToolStripSeparator());

            _miPause = new ToolStripMenuItem("Active", null, (s, e) => TogglePause());
            menu.Items.Add(_miPause);
            menu.Items.Add(new ToolStripSeparator());

            _miAwake = new ToolStripMenuItem("Keep system awake", null, (s, e) => { _cfg.KeepAwake = !_cfg.KeepAwake; _cfg.Save(); Tick(); });
            menu.Items.Add(_miAwake);

            _miDisplay = new ToolStripMenuItem("Keep display on", null, (s, e) => { _cfg.KeepDisplayOn = !_cfg.KeepDisplayOn; _cfg.Save(); Tick(); });
            menu.Items.Add(_miDisplay);

            _miActive = new ToolStripMenuItem("Keep me active (presence)", null, (s, e) => { _cfg.KeepActive = !_cfg.KeepActive; _cfg.Save(); Tick(); });
            menu.Items.Add(_miActive);

            menu.Items.Add(new ToolStripSeparator());

            // Auto-stop submenu
            _miStopHdr = new ToolStripMenuItem("Auto-stop");
            ToolStripMenuItem off = new ToolStripMenuItem("Off", null, (s, e) => { _cfg.StopEnabled = false; _cfg.Save(); Tick(); });
            _miStopHdr.DropDownItems.Add(off);
            string[] presets = new string[] { "17:00", "17:30", "18:00", "19:00", "21:00", "23:00" };
            foreach (string p in presets)
            {
                string pt = p;
                _miStopHdr.DropDownItems.Add(new ToolStripMenuItem(Pretty(pt), null, (s, e) => { _cfg.StopEnabled = true; _cfg.StopTime = pt; _cfg.Save(); Tick(); }));
            }
            _miStopHdr.DropDownItems.Add(new ToolStripSeparator());
            _miStopHdr.DropDownItems.Add(new ToolStripMenuItem("Custom...", null, (s, e) => CustomStop()));
            menu.Items.Add(_miStopHdr);

            _miStartup = new ToolStripMenuItem("Launch at startup", null, (s, e) => { SetStartup(!_cfg.StartAtLogin); });
            menu.Items.Add(_miStartup);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Open config folder", null, (s, e) => OpenConfigFolder()));
            menu.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => ExitApp()));

            menu.Opening += (s, e) => RefreshChecks();
            _tray.ContextMenuStrip = menu;
        }

        static string Pretty(string hhmm)
        {
            DateTime t;
            if (DateTime.TryParseExact(hhmm, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
                return t.ToString("h:mm tt", CultureInfo.InvariantCulture);
            return hhmm;
        }

        void RefreshChecks()
        {
            _miPause.Checked = !_paused;
            _miPause.Text = _paused ? "Paused" : "Active";
            _miAwake.Checked = _cfg.KeepAwake;
            _miDisplay.Checked = _cfg.KeepDisplayOn;
            _miActive.Checked = _cfg.KeepActive;
            _miStartup.Checked = _cfg.StartAtLogin;
            _miStopHdr.Text = _cfg.StopEnabled ? ("Auto-stop: " + Pretty(_cfg.StopTime)) : "Auto-stop: off";
        }

        void UpdateUi()
        {
            bool on = Working && (_cfg.KeepAwake || _cfg.KeepActive);
            _tray.Icon = on ? _iconActive : _iconIdle;
            string state;
            if (_paused) state = "Paused";
            else if (!InActiveWindow()) state = "Stopped (" + Pretty(_cfg.StopTime) + ")";
            else state = "Keeping " + (_cfg.KeepActive ? "active" : "awake");
            _miStatus.Text = "Presence — " + state;
            _tray.Text = "Presence — " + state;
        }

        void TogglePause()
        {
            _paused = !_paused;
            Tick();
        }

        void CustomStop()
        {
            string current = _cfg.StopTime;
            using (Form f = new Form())
            {
                f.Text = "Auto-stop time";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterScreen;
                f.MinimizeBox = false; f.MaximizeBox = false;
                f.ClientSize = new Size(260, 110);
                Label lbl = new Label() { Text = "Stop at (24h, HH:mm):", Left = 12, Top = 15, Width = 230 };
                TextBox tb = new TextBox() { Text = current, Left = 12, Top = 38, Width = 100 };
                Button ok = new Button() { Text = "Set", DialogResult = DialogResult.OK, Left = 70, Top = 72, Width = 70 };
                Button cancel = new Button() { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 150, Top = 72, Width = 70 };
                f.Controls.Add(lbl); f.Controls.Add(tb); f.Controls.Add(ok); f.Controls.Add(cancel);
                f.AcceptButton = ok; f.CancelButton = cancel;
                if (f.ShowDialog() == DialogResult.OK)
                {
                    DateTime t;
                    if (DateTime.TryParseExact(tb.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
                    {
                        _cfg.StopTime = tb.Text.Trim();
                        _cfg.StopEnabled = true;
                        _cfg.Save();
                        Tick();
                    }
                    else
                    {
                        MessageBox.Show("Please enter a time like 18:00.", "Presence");
                    }
                }
            }
        }

        void SetStartup(bool enable)
        {
            _cfg.StartAtLogin = enable;
            ApplyStartup(enable);
            _cfg.Save();
        }

        void ApplyStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;
                    if (enable) key.SetValue(RunValue, "\"" + _exePath + "\"");
                    else if (key.GetValue(RunValue) != null) key.DeleteValue(RunValue, false);
                }
            }
            catch { }
        }

        void OpenConfigFolder()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Presence");
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            catch { }
        }

        void ExitApp()
        {
            Native.SetThreadExecutionState(Native.EXECUTION_STATE.ES_CONTINUOUS);
            _timer.Stop();
            _tray.Visible = false;
            Application.Exit();
        }

        static Icon MakeIcon(Color c)
        {
            using (Bitmap bmp = new Bitmap(16, 16))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (SolidBrush b = new SolidBrush(c))
                        g.FillEllipse(b, 2, 2, 12, 12);
                    using (Pen p = new Pen(Color.FromArgb(60, 0, 0, 0)))
                        g.DrawEllipse(p, 2, 2, 12, 12);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (Mutex mtx = new Mutex(true, "Presence_SingleInstance_8d1f", out created))
            {
                if (!created) return; // already running
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }
}
