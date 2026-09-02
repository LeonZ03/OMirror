using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace OPhoneMirror
{
    internal sealed class DeviceDefinition
    {
        public string Name;
        public string Model;
        public string Serial;
        public int WindowX;
    }

       internal sealed class DeviceCard
    {
        public string Name;
        public string Model;
        public string Serial;
        public Label StatusLabel;
        public Panel StatusDot;
        public Button LaunchButton;
        public Button TransferButton;
        public int WindowX;
        public Process MirrorProcess;
        public int MirrorProcessId;
    }

    internal sealed class RoundedPanel : Panel
    {
        public Color BorderColor = Color.FromArgb(53, 61, 78);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedRect(r, 14))
            using (Pen pen = new Pen(BorderColor, 1))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly Color background = Color.FromArgb(18, 21, 29);
        private readonly Color card = Color.FromArgb(28, 33, 45);
        private readonly Color foreground = Color.FromArgb(238, 241, 247);
        private readonly Color muted = Color.FromArgb(151, 160, 180);
        private readonly Color accent = Color.FromArgb(82, 135, 255);
        private readonly Color online = Color.FromArgb(64, 211, 147);
        private readonly Color offline = Color.FromArgb(113, 122, 143);

        private readonly string appDirectory;
        private readonly string adbPath;
        private readonly string scrcpyPath;
        private readonly DeviceCard device1;
        private readonly DeviceCard device2;
        private readonly Timer refreshTimer;
        private readonly Timer restartTimer;
        private readonly Label footerStatus;
        private readonly ComboBox keyboardModeSelector;
        private readonly string settingsPath;
        private readonly List<DeviceCard> pendingRestart = new List<DeviceCard>();
        private readonly KeyboardCapture keyboardCapture;
        private int keyboardMode;

        public MainForm()
        {
            appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            adbPath = Path.Combine(appDirectory, "scrcpy", "adb.exe");
            scrcpyPath = Path.Combine(appDirectory, "scrcpy", "scrcpy.exe");
            settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OPhoneMirror",
                "keyboard-mode.txt");
            keyboardMode = LoadKeyboardMode();

            Text = "OPhoneMirror · 手机有线投屏";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 470);
            MinimumSize = new Size(736, 509);
            BackColor = background;
            ForeColor = foreground;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;

            Label title = new Label();
            title.Text = "OPhoneMirror";
            title.Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold);
            title.ForeColor = foreground;
            title.AutoSize = true;
            title.Location = new Point(32, 24);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "选择已通过 USB 调试授权的手机，启动低延迟键鼠控制";
            subtitle.Font = new Font("Microsoft YaHei UI", 10F);
            subtitle.ForeColor = muted;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(35, 70);
            Controls.Add(subtitle);

            Button refresh = MakeSecondaryButton("刷新设备");
            refresh.Location = new Point(590, 32);
            refresh.Size = new Size(96, 36);
            refresh.Click += delegate { RefreshDevices(); };
            Controls.Add(refresh);

            List<DeviceDefinition> definitions = LoadDeviceDefinitions();
            device1 = CreateDeviceCard(definitions[0], 32, 112);
            device2 = CreateDeviceCard(definitions[1], 370, 112);

            RoundedPanel info = new RoundedPanel();
            info.BackColor = Color.FromArgb(23, 28, 39);
            info.Location = new Point(32, 330);
            info.Size = new Size(654, 72);
            info.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            Label infoTitle = new Label();
            infoTitle.Text = "低延迟预设";
            infoTitle.ForeColor = foreground;
            infoTitle.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            infoTitle.AutoSize = true;
            infoTitle.Location = new Point(18, 12);
            info.Controls.Add(infoTitle);

            Label infoText = new Label();
            infoText.Text = "USB · 60 fps · 双向剪贴板 · 文件互传 · 聚焦投屏时 Alt+Tab→最近任务、Win→桌面";
            infoText.ForeColor = muted;
            infoText.AutoSize = true;
            infoText.Location = new Point(18, 40);
            info.Controls.Add(infoText);

            keyboardModeSelector = new ComboBox();
            keyboardModeSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            keyboardModeSelector.BackColor = Color.FromArgb(39, 45, 59);
            keyboardModeSelector.ForeColor = foreground;
            keyboardModeSelector.FlatStyle = FlatStyle.Flat;
            keyboardModeSelector.Font = new Font("Microsoft YaHei UI", 9F);
            keyboardModeSelector.Location = new Point(381, 12);
            keyboardModeSelector.Size = new Size(253, 28);
            keyboardModeSelector.Items.Add("搜狗短语（Shift 中英）");
            keyboardModeSelector.Items.Add("数字选词（Shift+Space 中英）");
            keyboardModeSelector.SelectedIndex = keyboardMode;
            keyboardModeSelector.SelectedIndexChanged += KeyboardModeChanged;
            info.Controls.Add(keyboardModeSelector);
            Controls.Add(info);

            footerStatus = new Label();
            footerStatus.Text = "正在检查设备…";
            footerStatus.ForeColor = muted;
            footerStatus.AutoSize = true;
            footerStatus.Location = new Point(35, 425);
            footerStatus.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            Controls.Add(footerStatus);

            Label version = new Label();
            version.Text = "OPhoneMirror 1.8.2 · scrcpy 4.1";
            version.ForeColor = muted;
            version.AutoSize = true;
            version.Location = new Point(614, 425);
            version.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(version);

            refreshTimer = new Timer();
            refreshTimer.Interval = 2500;
            refreshTimer.Tick += delegate { RefreshDevices(); };

            restartTimer = new Timer();
            restartTimer.Interval = 700;
            restartTimer.Tick += RestartMirrors;

            keyboardCapture = new KeyboardCapture(this);
            FormClosed += delegate { keyboardCapture.Dispose(); };
            Shown += delegate
            {
                RefreshDevices();
                refreshTimer.Start();
            };
        }

        internal bool HasConfiguredDevices
        {
            get
            {
                return !string.IsNullOrWhiteSpace(device1.Serial) &&
                    !string.IsNullOrWhiteSpace(device2.Serial) &&
                    File.Exists(adbPath) &&
                    File.Exists(scrcpyPath);
            }
        }

        private List<DeviceDefinition> LoadDeviceDefinitions()
        {
            List<DeviceDefinition> definitions = new List<DeviceDefinition>();
            string path = Path.Combine(appDirectory, "devices.local.txt");

            try
            {
                if (File.Exists(path))
                {
                    foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
                    {
                        string line = rawLine.Trim();
                        if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                            continue;

                        string[] parts = line.Split('|');
                        int windowX;
                        if (parts.Length != 4 ||
                            parts[0].Trim().Length == 0 ||
                            parts[2].Trim().Length == 0 ||
                            !int.TryParse(parts[3].Trim(), out windowX))
                            continue;

                        definitions.Add(new DeviceDefinition
                        {
                            Name = parts[0].Trim(),
                            Model = parts[1].Trim(),
                            Serial = parts[2].Trim(),
                            WindowX = windowX
                        });
                        if (definitions.Count == 2)
                            break;
                    }
                }
            }
            catch
            {
                definitions.Clear();
            }

            while (definitions.Count < 2)
            {
                int index = definitions.Count + 1;
                definitions.Add(new DeviceDefinition
                {
                    Name = "未配置设备 " + index,
                    Model = "请配置 devices.local.txt",
                    Serial = string.Empty,
                    WindowX = index == 1 ? 80 : 560
                });
            }

            return definitions;
        }

        private DeviceCard CreateDeviceCard(DeviceDefinition definition, int x, int y)
        {
            DeviceCard device = new DeviceCard();
            device.Name = definition.Name;
            device.Model = definition.Model;
            device.Serial = definition.Serial;
            device.WindowX = definition.WindowX;

            RoundedPanel panel = new RoundedPanel();
            panel.BackColor = card;
            panel.Location = new Point(x, y);
            panel.Size = new Size(316, 190);

            Label nameLabel = new Label();
            nameLabel.Text = device.Name;
            nameLabel.Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold);
            nameLabel.ForeColor = foreground;
            nameLabel.AutoSize = true;
            nameLabel.Location = new Point(20, 18);
            panel.Controls.Add(nameLabel);

            Label modelLabel = new Label();
            modelLabel.Text = device.Model;
            modelLabel.ForeColor = muted;
            modelLabel.AutoSize = true;
            modelLabel.Location = new Point(22, 55);
            panel.Controls.Add(modelLabel);

            device.StatusDot = new Panel();
            device.StatusDot.BackColor = offline;
            device.StatusDot.Location = new Point(22, 87);
            device.StatusDot.Size = new Size(10, 10);
            panel.Controls.Add(device.StatusDot);

            device.StatusLabel = new Label();
            device.StatusLabel.Text = "未连接";
            device.StatusLabel.ForeColor = muted;
            device.StatusLabel.AutoSize = true;
            device.StatusLabel.Location = new Point(40, 83);
            panel.Controls.Add(device.StatusLabel);

            device.LaunchButton = MakePrimaryButton("启动有线投屏");
            device.LaunchButton.Location = new Point(20, 126);
            device.LaunchButton.Size = new Size(174, 44);
            device.LaunchButton.Enabled = false;
            device.LaunchButton.Click += delegate { LaunchDevice(device); };
            panel.Controls.Add(device.LaunchButton);

            device.TransferButton = MakeSecondaryButton("文件互传");
            device.TransferButton.Location = new Point(202, 126);
            device.TransferButton.Size = new Size(94, 44);
            device.TransferButton.Enabled = false;
            device.TransferButton.Click += delegate { OpenTransfer(device); };
            panel.Controls.Add(device.TransferButton);

            Controls.Add(panel);
            return device;
        }

        private Button MakePrimaryButton(string text)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = accent;
            b.ForeColor = Color.White;
            b.Cursor = Cursors.Hand;
            b.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            return b;
        }

        private Button MakeSecondaryButton(string text)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(65, 73, 92);
            b.FlatAppearance.BorderSize = 1;
            b.BackColor = card;
            b.ForeColor = foreground;
            b.Cursor = Cursors.Hand;
            return b;
        }

        private void RefreshDevices()
        {
            if (!File.Exists(adbPath) || !File.Exists(scrcpyPath))
            {
                footerStatus.Text = "安装不完整：缺少 scrcpy 或 ADB";
                SetDeviceState(device1, false);
                SetDeviceState(device2, false);
                return;
            }

            bool device1Online = IsUsbDeviceOnline(device1.Serial);
            bool device2Online = IsUsbDeviceOnline(device2.Serial);
            SetDeviceState(device1, device1Online);
            SetDeviceState(device2, device2Online);

            int count = (device1Online ? 1 : 0) + (device2Online ? 1 : 0);
            if (count == 0)
                footerStatus.Text = "未发现已授权的目标 USB 设备";
            else
                footerStatus.Text = string.Format("已连接 {0} 台目标设备", count);
        }

        private bool IsUsbDeviceOnline(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
                return false;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = adbPath;
                psi.Arguments = "-s " + serial + " get-state";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                Process p = Process.Start(psi);
                if (!p.WaitForExit(1200))
                {
                    p.Kill();
                    return false;
                }
                string output = p.StandardOutput.ReadToEnd().Trim();
                return p.ExitCode == 0 && output == "device";
            }
            catch
            {
                return false;
            }
        }

        private void SetDeviceState(DeviceCard device, bool connected)
        {
            device.StatusDot.BackColor = connected ? online : offline;
            device.StatusLabel.Text = connected ? "USB 已连接" : "未连接";
            device.StatusLabel.ForeColor = connected ? online : muted;
            device.LaunchButton.Enabled = connected;
            device.LaunchButton.BackColor = connected ? accent : Color.FromArgb(61, 68, 84);
            device.TransferButton.Enabled = connected;
            device.TransferButton.ForeColor = connected ? foreground : muted;
        }

        private void LaunchDevice(DeviceCard device)
        {
            if (!IsUsbDeviceOnline(device.Serial))
            {
                MessageBox.Show(
                    device.Name + " 当前未通过 USB ADB 连接。\n\n请检查数据线、USB 调试和电脑授权。",
                    "设备未连接",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                RefreshDevices();
                return;
            }

            try
            {
                int previousDeviceMode = LoadDeviceMode(device.Serial);
                bool normalizeShortPhrase = keyboardMode == 0 && previousDeviceMode == 1;
                string keyboardArg = keyboardMode == 1 ? " --keyboard=uhid" : string.Empty;
                string args = string.Format(
                    "--serial={0} --window-title=\"{1} USB Low Latency\" --video-codec=h264 --max-fps=60 --video-bit-rate=16M --video-buffer=0 --no-audio --always-on-top --window-x={2} --window-y=80 --window-width=450 --window-height=900{3}",
                    device.Serial, device.Name, device.WindowX, keyboardArg);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = scrcpyPath;
                psi.Arguments = args;
                psi.WorkingDirectory = Path.GetDirectoryName(scrcpyPath);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                device.MirrorProcess = Process.Start(psi);
                device.MirrorProcessId = device.MirrorProcess.Id;
                SaveDeviceMode(device.Serial, keyboardMode);
                if (normalizeShortPhrase)
                    NormalizeShortPhraseModeAsync(device);
                footerStatus.Text = "已启动 " + device.Name + " · " + KeyboardModeName();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "无法启动 scrcpy：\n\n" + ex.Message,
                    "启动失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private int LoadKeyboardMode()
        {
            try
            {
                string value = File.ReadAllText(settingsPath).Trim();
                return value == "1" ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private void SaveKeyboardMode()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(settingsPath, keyboardMode.ToString(), Encoding.UTF8);
            }
            catch
            {
                // A settings write failure must not prevent mirroring.
            }
        }

        private string DeviceModePath(string serial)
        {
            return Path.Combine(Path.GetDirectoryName(settingsPath), "last-mode-" + serial + ".txt");
        }

        private int LoadDeviceMode(string serial)
        {
            try
            {
                string value = File.ReadAllText(DeviceModePath(serial)).Trim();
                if (value == "0")
                    return 0;
                if (value == "1")
                    return 1;
            }
            catch { }
            return -1;
        }

        private void SaveDeviceMode(string serial, int mode)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(DeviceModePath(serial), mode.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private void NormalizeShortPhraseModeAsync(DeviceCard device)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                System.Threading.Thread.Sleep(900);
                AdbResult result = new AdbClient(adbPath, device.Serial).Shell("input keyevent 59", 5000);
                if (!result.Success || IsDisposed)
                    return;
                BeginInvoke((MethodInvoker)delegate
                {
                    footerStatus.Text = device.Name + " 已进入搜狗短语模式";
                });
            });
        }

        private void OpenTransfer(DeviceCard device)
        {
            if (!IsUsbDeviceOnline(device.Serial))
            {
                MessageBox.Show(
                    device.Name + " 当前未通过 USB ADB 连接。",
                    "设备未连接",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                RefreshDevices();
                return;
            }

            TransferForm transfer = new TransferForm(device.Name, device.Serial, adbPath);
            transfer.Show(this);
        }

        internal DeviceCard GetFocusedMirrorDevice()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return null;

            uint processId;
            GetWindowThreadProcessId(foregroundWindow, out processId);
            if (processId == device1.MirrorProcessId)
                return device1;
            if (processId == device2.MirrorProcessId)
                return device2;
            return null;
        }

        internal void SendAndroidKeyAsync(DeviceCard device, int keyCode)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                new AdbClient(adbPath, device.Serial).Shell("input keyevent " + keyCode, 5000);
            });
        }

        private string KeyboardModeName()
        {
            return keyboardMode == 1 ? "数字选词" : "搜狗短语";
        }

        private void KeyboardModeChanged(object sender, EventArgs e)
        {
            int selected = keyboardModeSelector.SelectedIndex;
            if (selected < 0 || selected == keyboardMode)
                return;

            keyboardMode = selected;
            SaveKeyboardMode();

            QueueRestartIfRunning(device1);
            QueueRestartIfRunning(device2);

            if (pendingRestart.Count == 0)
            {
                footerStatus.Text = "已切换为“" + KeyboardModeName() + "”，下次投屏生效";
                return;
            }

            restartTimer.Stop();
            restartTimer.Start();
            footerStatus.Text = "正在切换为“" + KeyboardModeName() + "”…";
        }

        private void QueueRestartIfRunning(DeviceCard device)
        {
            List<Process> running = FindMirrorProcesses(device);
            if (running.Count == 0)
                return;

            if (!pendingRestart.Contains(device))
                pendingRestart.Add(device);

            StopMirrorProcesses(running, false);
        }

        private void RestartMirrors(object sender, EventArgs e)
        {
            restartTimer.Stop();
            DeviceCard[] devices = pendingRestart.ToArray();
            pendingRestart.Clear();

            foreach (DeviceCard device in devices)
            {
                StopMirrorProcesses(FindMirrorProcesses(device), true);
                if (IsUsbDeviceOnline(device.Serial))
                    LaunchDevice(device);
            }

            if (devices.Length > 0)
                footerStatus.Text = "已热切换为“" + KeyboardModeName() + "”";
        }

        private List<Process> FindMirrorProcesses(DeviceCard device)
        {
            List<Process> processes = new List<Process>();
            HashSet<int> processIds = new HashSet<int>();

            try
            {
                if (device.MirrorProcess != null && !device.MirrorProcess.HasExited)
                {
                    processes.Add(device.MirrorProcess);
                    processIds.Add(device.MirrorProcess.Id);
                }
            }
            catch
            {
                device.MirrorProcess = null;
            }

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='scrcpy.exe'"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject item in results)
                    {
                        string commandLine = Convert.ToString(item["CommandLine"]);
                        if (commandLine.IndexOf("--serial=" + device.Serial, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        int processId = Convert.ToInt32((uint)item["ProcessId"]);
                        if (processIds.Add(processId))
                            processes.Add(Process.GetProcessById(processId));
                    }
                }
            }
            catch
            {
                // The process started by this app is still handled without WMI.
            }

            return processes;
        }

        private static void StopMirrorProcesses(List<Process> processes, bool force)
        {
            foreach (Process process in processes)
            {
                try
                {
                    if (process.HasExited)
                        continue;

                    bool closeRequested = process.CloseMainWindow();
                    if (force || !closeRequested)
                    {
                        if (!process.WaitForExit(150))
                            process.Kill();
                    }
                }
                catch
                {
                    // A process may exit between discovery and shutdown.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length == 1 && args[0] == "--self-test")
            {
                using (MainForm form = new MainForm())
                    return form.HasConfiguredDevices ? 0 : 2;
            }

            Application.Run(new MainForm());
            return 0;
        }
    }
}
