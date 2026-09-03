using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("OPhoneMirror")]
[assembly: AssemblyProduct("OPhoneMirror")]
[assembly: AssemblyVersion("1.8.9.0")]
[assembly: AssemblyFileVersion("1.8.9.0")]

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
        public int ScreenPowerRequestId;
        public bool ScreenOffApplied;
        public int PinOverlayRequestId;
        public PinOverlayForm PinOverlay;
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

    internal sealed class PinOverlayForm : Form
    {
        private const int GwlpHwndParent = -8;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;
        private const int SmCxSize = 30;
        private const int SmCySize = 31;
        private const int SwHide = 0;
        private const int SwShowNoActivate = 4;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private static readonly IntPtr HwndTopMost = new IntPtr(-1);
        private static readonly IntPtr HwndNoTopMost = new IntPtr(-2);

        private readonly IntPtr targetWindow;
        private readonly Action<bool> pinChanged;
        private readonly Timer trackingTimer;
        private readonly ToolTip toolTip;
        private bool pinned;
        private bool hovering;
        private bool closing;

        public PinOverlayForm(IntPtr targetWindow, Action<bool> pinChanged)
        {
            this.targetWindow = targetWindow;
            this.pinChanged = pinChanged;
            pinned = true;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Size = new Size(30, 26);
            BackColor = Color.FromArgb(32, 32, 32);
            Cursor = Cursors.Hand;
            AccessibleName = "保持在最顶层";
            DoubleBuffered = true;

            toolTip = new ToolTip();
            UpdateToolTip();

            MouseEnter += delegate
            {
                hovering = true;
                Invalidate();
            };
            MouseLeave += delegate
            {
                hovering = false;
                Invalidate();
            };
            MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left)
                    return;
                TogglePinned();
            };

            trackingTimer = new Timer();
            trackingTimer.Interval = 120;
            trackingTimer.Tick += delegate { TrackTarget(); };
            Shown += delegate
            {
                // WinForms assigns its own temporary owner while Show() runs, so attach to
                // the external scrcpy window only after the overlay is fully visible.
                SetWindowOwner(Handle, targetWindow);
                TrackTarget();
                trackingTimer.Start();
            };
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
                return parameters;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetWindowOwner(Handle, targetWindow);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(hovering ? Color.FromArgb(64, 64, 64) : Color.FromArgb(40, 40, 40));

            Color iconColor = pinned ? Color.FromArgb(82, 135, 255) : Color.FromArgb(190, 194, 204);
            using (SolidBrush brush = new SolidBrush(iconColor))
            using (Pen pen = new Pen(iconColor, 1.8F))
            {
                e.Graphics.TranslateTransform(Width / 2F, Height / 2F);
                e.Graphics.RotateTransform(35F);
                e.Graphics.FillRectangle(brush, -5F, -8F, 10F, 5F);
                e.Graphics.FillPolygon(brush, new PointF[]
                {
                    new PointF(-6F, -3F),
                    new PointF(6F, -3F),
                    new PointF(3F, 2F),
                    new PointF(-3F, 2F)
                });
                e.Graphics.DrawLine(pen, 0F, 1F, 0F, 9F);
                e.Graphics.ResetTransform();
            }

            if (!pinned)
            {
                using (Pen slash = new Pen(Color.FromArgb(235, 104, 104), 2F))
                    e.Graphics.DrawLine(slash, 7, 20, 23, 5);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            closing = true;
            trackingTimer.Stop();
            trackingTimer.Dispose();
            toolTip.Dispose();
            base.OnFormClosed(e);
        }

        private void TogglePinned()
        {
            bool next = !pinned;
            IntPtr insertAfter = next ? HwndTopMost : HwndNoTopMost;
            if (!SetWindowPos(targetWindow, insertAfter, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpNoActivate))
                return;

            pinned = next;
            UpdateToolTip();
            Invalidate();
            SetForegroundWindow(targetWindow);
            if (pinChanged != null)
                pinChanged(pinned);
        }

        private void UpdateToolTip()
        {
            toolTip.SetToolTip(this, pinned
                ? "保持在最顶层：已开启（点击取消）"
                : "保持在最顶层：已关闭（点击开启）");
        }

        private void TrackTarget()
        {
            if (closing)
                return;

            if (!IsWindow(targetWindow))
            {
                closing = true;
                Close();
                return;
            }

            if (!IsWindowVisible(targetWindow) || IsIconic(targetWindow))
            {
                ShowWindow(Handle, SwHide);
                return;
            }

            NativeRect bounds;
            if (!GetWindowRect(targetWindow, out bounds))
                return;

            int captionButtonWidth = Math.Max(GetSystemMetrics(SmCxSize), 36);
            int captionHeight = Math.Max(GetSystemMetrics(SmCySize), Height);
            int x = bounds.Right - (captionButtonWidth * 3) - Width - 4;
            int y = bounds.Top + Math.Max(0, (captionHeight - Height) / 2);
            SetWindowPos(Handle, IntPtr.Zero, x, y, Width, Height,
                SwpNoZOrder | SwpNoActivate);
            ShowWindow(Handle, SwShowNoActivate);
        }

        private static void SetWindowOwner(IntPtr window, IntPtr owner)
        {
            if (IntPtr.Size == 8)
                SetWindowLongPtr64(window, GwlpHwndParent, owner);
            else
                SetWindowLong32(window, GwlpHwndParent, owner.ToInt32());
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr window, int index, int value);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);
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
        private readonly CheckBox screenOffToggle;
        private readonly string settingsPath;
        private readonly string screenOffSettingsPath;
        private readonly List<DeviceCard> pendingRestart = new List<DeviceCard>();
        private readonly KeyboardCapture keyboardCapture;
        private int keyboardMode;
        private int refreshInProgress;

        public MainForm()
        {
            appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            adbPath = Path.Combine(appDirectory, "scrcpy", "adb.exe");
            scrcpyPath = Path.Combine(appDirectory, "scrcpy", "scrcpy.exe");
            settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OPhoneMirror",
                "keyboard-mode.txt");
            screenOffSettingsPath = Path.Combine(
                Path.GetDirectoryName(settingsPath),
                "screen-off-enabled.txt");
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

            screenOffToggle = new CheckBox();
            screenOffToggle.Text = "仅熄手机屏幕";
            screenOffToggle.ForeColor = foreground;
            screenOffToggle.AutoSize = true;
            screenOffToggle.Location = new Point(135, 12);
            screenOffToggle.Checked = LoadScreenOffSetting();
            screenOffToggle.CheckedChanged += ScreenOffSettingChanged;
            info.Controls.Add(screenOffToggle);

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
            version.Text = "OPhoneMirror 1.8.9 · scrcpy 4.1";
            version.ForeColor = muted;
            version.AutoSize = false;
            version.Location = new Point(426, 421);
            version.Size = new Size(260, 24);
            version.TextAlign = ContentAlignment.MiddleRight;
            version.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(version);

            refreshTimer = new Timer();
            refreshTimer.Interval = 2500;
            refreshTimer.Tick += delegate { RefreshDevices(); };

            restartTimer = new Timer();
            restartTimer.Interval = 700;
            restartTimer.Tick += RestartMirrors;

            keyboardCapture = new KeyboardCapture(this);
            FormClosed += delegate
            {
                ClosePinOverlay(device1);
                ClosePinOverlay(device2);
                StopScreenControl(device1, true);
                StopScreenControl(device2, true);
                keyboardCapture.Dispose();
            };
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

            if (System.Threading.Interlocked.CompareExchange(ref refreshInProgress, 1, 0) != 0)
                return;

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                bool device1Online = IsUsbDeviceOnline(device1.Serial);
                bool device2Online = IsUsbDeviceOnline(device2.Serial);

                if (IsDisposed)
                {
                    System.Threading.Interlocked.Exchange(ref refreshInProgress, 0);
                    return;
                }

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        SetDeviceState(device1, device1Online);
                        SetDeviceState(device2, device2Online);

                        int connectedCount = (device1Online ? 1 : 0) + (device2Online ? 1 : 0);
                        if (connectedCount == 0)
                            footerStatus.Text = "未发现已授权的目标 USB 设备";
                        else
                            footerStatus.Text = string.Format("已连接 {0} 台目标设备", connectedCount);

                        System.Threading.Interlocked.Exchange(ref refreshInProgress, 0);
                    });
                }
                catch
                {
                    System.Threading.Interlocked.Exchange(ref refreshInProgress, 0);
                }
            });
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
                ClosePinOverlay(device);
                int previousDeviceMode = LoadDeviceMode(device.Serial);
                bool normalizeShortPhrase = keyboardMode == 0 && previousDeviceMode == 1;
                string keyboardArg = keyboardMode == 1 ? " --keyboard=uhid" : string.Empty;
                string args = string.Format(
                    "--serial={0} --window-title=\"{1} USB Low Latency\" --video-codec=h264 --max-fps=60 --video-bit-rate=16M --video-buffer=0 --no-audio --always-on-top --shortcut-mod=lalt --window-x={2} --window-y=80 --window-width=450 --window-height=900{3}",
                    device.Serial, device.Name, device.WindowX, keyboardArg);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = scrcpyPath;
                psi.Arguments = args;
                psi.WorkingDirectory = Path.GetDirectoryName(scrcpyPath);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                device.MirrorProcess = Process.Start(psi);
                device.MirrorProcessId = device.MirrorProcess.Id;
                StartPinOverlay(device, device.MirrorProcess);
                SaveDeviceMode(device.Serial, keyboardMode);
                if (screenOffToggle.Checked)
                    StartScreenControl(device);
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

        private bool LoadScreenOffSetting()
        {
            try
            {
                return File.ReadAllText(screenOffSettingsPath).Trim() == "1";
            }
            catch
            {
                return false;
            }
        }

        private void SaveScreenOffSetting()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(screenOffSettingsPath));
                File.WriteAllText(
                    screenOffSettingsPath,
                    screenOffToggle.Checked ? "1" : "0",
                    Encoding.UTF8);
            }
            catch
            {
                // A settings write failure must not prevent mirroring.
            }
        }

        private void ScreenOffSettingChanged(object sender, EventArgs e)
        {
            SaveScreenOffSetting();
            if (screenOffToggle.Checked)
            {
                StartScreenControl(device1);
                StartScreenControl(device2);
                footerStatus.Text = "已开启“仅熄手机屏幕”；正在运行的投屏将热生效";
            }
            else
            {
                StopScreenControl(device1, true);
                StopScreenControl(device2, true);
                footerStatus.Text = "已关闭“仅熄手机屏幕”；手机实体屏幕正在恢复";
            }
        }

        private void StartScreenControl(DeviceCard device)
        {
            if (!screenOffToggle.Checked || !IsProcessRunning(device.MirrorProcess))
                return;

            int requestId = System.Threading.Interlocked.Increment(ref device.ScreenPowerRequestId);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                System.Threading.Thread.Sleep(900);
                if (IsDisposed || requestId != device.ScreenPowerRequestId)
                    return;

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (requestId != device.ScreenPowerRequestId || !screenOffToggle.Checked ||
                            !IsProcessRunning(device.MirrorProcess))
                            return;

                        bool sent = SendMirrorScreenPower(device, false);
                        device.ScreenOffApplied = sent;
                        footerStatus.Text = sent
                            ? device.Name + "：已关闭手机实体屏幕，投屏继续运行"
                            : device.Name + "：未能切换实体屏幕状态，请重新启动投屏后再试";
                    });
                }
                catch
                {
                    // The main form may close while the delayed command is pending.
                }
            });
        }

        private void StopScreenControl(DeviceCard device, bool wakeDevice)
        {
            System.Threading.Interlocked.Increment(ref device.ScreenPowerRequestId);
            bool shouldWake = wakeDevice && device.ScreenOffApplied;
            device.ScreenOffApplied = false;
            if (!shouldWake || string.IsNullOrWhiteSpace(device.Serial))
                return;

            bool sent = SendMirrorScreenPower(device, true);
            if (sent)
            {
                footerStatus.Text = device.Name + "：手机实体屏幕已恢复";
                return;
            }

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                AdbResult wakeResult = WakeDevice(device.Serial);
                SetWakeResultStatus(device, wakeResult.Success);
            });
        }

        private bool SendMirrorScreenPower(DeviceCard device, bool turnOn)
        {
            try
            {
                Process process = device.MirrorProcess;
                if (!IsProcessRunning(process))
                    return false;

                process.Refresh();
                IntPtr window = process.MainWindowHandle;
                if (window == IntPtr.Zero)
                    return false;

                IntPtr previousWindow = GetForegroundWindow();
                if (!SetForegroundWindow(window))
                    return false;

                // scrcpy documents MOD+O for display-off. When the physical display is off,
                // a right-click inside the mirror explicitly turns it back on. The real mouse
                // event is required because SDL ignores a posted background mouse message.
                System.Threading.Thread.Sleep(60);
                if (turnOn)
                {
                    NativeRect bounds;
                    if (!GetWindowRect(window, out bounds))
                        return false;

                    Point previousCursor = Cursor.Position;
                    try
                    {
                        Cursor.Position = new Point(
                            bounds.Left + ((bounds.Right - bounds.Left) / 2),
                            bounds.Top + ((bounds.Bottom - bounds.Top) / 2));
                        MouseEvent(MouseRightDown, 0, 0, 0, UIntPtr.Zero);
                        MouseEvent(MouseRightUp, 0, 0, 0, UIntPtr.Zero);
                        System.Threading.Thread.Sleep(80);
                    }
                    finally
                    {
                        Cursor.Position = previousCursor;
                    }
                }
                else
                {
                    SendKeys.SendWait("%o");
                }
                System.Threading.Thread.Sleep(40);
                if (previousWindow != IntPtr.Zero && previousWindow != window)
                    SetForegroundWindow(previousWindow);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private AdbResult WakeDevice(string serial)
        {
            return new AdbClient(adbPath, serial).Shell("input keyevent 224", 2500);
        }

        private void SetWakeResultStatus(DeviceCard device, bool success)
        {
            if (IsDisposed || Disposing)
                return;

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!screenOffToggle.Checked)
                    {
                        footerStatus.Text = success
                            ? device.Name + "：手机实体屏幕已恢复"
                            : device.Name + "：未能自动点亮，请按一下手机电源键";
                    }
                });
            }
            catch
            {
                // The main form may close while the wake command is completing.
            }
        }

        private static bool IsProcessRunning(Process process)
        {
            if (process == null)
                return false;
            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
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

            ClosePinOverlay(device);
            StopMirrorProcesses(running, false);
        }

        private void StartPinOverlay(DeviceCard device, Process mirrorProcess)
        {
            int requestId = System.Threading.Interlocked.Increment(ref device.PinOverlayRequestId);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                IntPtr mirrorWindow = IntPtr.Zero;
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    if (IsDisposed || requestId != device.PinOverlayRequestId)
                        return;

                    try
                    {
                        if (mirrorProcess.HasExited)
                            return;
                        mirrorProcess.Refresh();
                        mirrorWindow = mirrorProcess.MainWindowHandle;
                        if (mirrorWindow != IntPtr.Zero)
                            break;
                    }
                    catch
                    {
                        return;
                    }

                    System.Threading.Thread.Sleep(100);
                }

                if (mirrorWindow == IntPtr.Zero || IsDisposed)
                    return;

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (requestId != device.PinOverlayRequestId ||
                            !IsProcessRunning(device.MirrorProcess) ||
                            device.MirrorProcessId != mirrorProcess.Id)
                            return;

                        ClosePinOverlayCore(device, false);
                        PinOverlayForm overlay = new PinOverlayForm(mirrorWindow, delegate(bool pinned)
                        {
                            footerStatus.Text = device.Name + (pinned
                                ? "：已保持在最顶层"
                                : "：已允许其他窗口覆盖");
                        });
                        device.PinOverlay = overlay;
                        overlay.FormClosed += delegate
                        {
                            if (device.PinOverlay == overlay)
                                device.PinOverlay = null;
                        };
                        overlay.Show();
                    });
                }
                catch
                {
                    // The main form may close while scrcpy is creating its window.
                }
            });
        }

        private void ClosePinOverlay(DeviceCard device)
        {
            ClosePinOverlayCore(device, true);
        }

        private void ClosePinOverlayCore(DeviceCard device, bool cancelPending)
        {
            if (cancelPending)
                System.Threading.Interlocked.Increment(ref device.PinOverlayRequestId);

            PinOverlayForm overlay = device.PinOverlay;
            device.PinOverlay = null;
            if (overlay == null || overlay.IsDisposed)
                return;

            overlay.Close();
            overlay.Dispose();
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
                        if (commandLine.IndexOf("--no-video", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            commandLine.IndexOf("--no-audio", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            commandLine.IndexOf("--turn-screen-off", StringComparison.OrdinalIgnoreCase) >= 0)
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

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const uint MouseRightDown = 0x0008;
        private const uint MouseRightUp = 0x0010;

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

        [DllImport("user32.dll", EntryPoint = "mouse_event")]
        private static extern void MouseEvent(
            uint flags,
            uint dx,
            uint dy,
            uint data,
            UIntPtr extraInfo);

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
