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
[assembly: AssemblyVersion("1.9.0.0")]
[assembly: AssemblyFileVersion("1.9.0.0")]

namespace OPhoneMirror
{
    internal sealed class DeviceDefinition
    {
        public string Name;
        public string Model;
        public string Serial;
        public int WindowX;
    }

    internal static class UiTheme
    {
        public static readonly Color Background = Color.FromArgb(15, 23, 42);
        public static readonly Color Surface = Color.FromArgb(27, 35, 54);
        public static readonly Color SurfaceRaised = Color.FromArgb(32, 42, 63);
        public static readonly Color SurfaceMuted = Color.FromArgb(39, 48, 69);
        public static readonly Color Border = Color.FromArgb(63, 76, 101);
        public static readonly Color BorderStrong = Color.FromArgb(78, 94, 124);
        public static readonly Color Text = Color.FromArgb(248, 250, 252);
        public static readonly Color TextMuted = Color.FromArgb(174, 186, 204);
        public static readonly Color TextDim = Color.FromArgb(137, 151, 173);
        public static readonly Color Accent = Color.FromArgb(59, 111, 216);
        public static readonly Color AccentHover = Color.FromArgb(63, 112, 213);
        public static readonly Color AccentPressed = Color.FromArgb(53, 99, 194);
        public static readonly Color Success = Color.FromArgb(55, 211, 154);
        public static readonly Color Offline = Color.FromArgb(126, 140, 164);
        public static readonly Color Danger = Color.FromArgb(248, 113, 113);
    }

    internal enum UiButtonKind
    {
        Primary,
        Secondary,
        Quiet
    }

    internal sealed class ModernButton : Button
    {
        private bool hovered;
        private bool pressed;

        public UiButtonKind Kind = UiButtonKind.Secondary;
        public int CornerRadius = 8;

        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            pressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill;
            Color stroke;
            Color content;

            if (!Enabled)
            {
                fill = UiTheme.SurfaceMuted;
                stroke = UiTheme.Border;
                content = UiTheme.TextDim;
            }
            else if (Kind == UiButtonKind.Primary)
            {
                fill = pressed ? UiTheme.AccentPressed : hovered ? UiTheme.AccentHover : UiTheme.Accent;
                stroke = fill;
                content = Color.White;
            }
            else if (Kind == UiButtonKind.Quiet)
            {
                fill = pressed ? UiTheme.SurfaceMuted : hovered ? UiTheme.SurfaceRaised : BackColor;
                stroke = fill;
                content = ForeColor;
            }
            else
            {
                fill = pressed ? UiTheme.SurfaceMuted : hovered ? UiTheme.SurfaceRaised : UiTheme.Surface;
                stroke = hovered ? UiTheme.BorderStrong : UiTheme.Border;
                content = UiTheme.Text;
            }

            using (GraphicsPath path = RoundedPanel.CreateRoundedRect(bounds, CornerRadius))
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(stroke, 1))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                bounds,
                content,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
            {
                Rectangle focus = Rectangle.Inflate(bounds, -3, -3);
                ControlPaint.DrawFocusRectangle(e.Graphics, focus, Color.White, fill);
            }
        }
    }

    internal sealed class ToggleSwitch : Control
    {
        private bool hovered;
        private bool isChecked;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value)
                    return;
                isChecked = value;
                Invalidate();
                EventHandler handler = CheckedChanged;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Cursor = Cursors.Hand;
            ForeColor = UiTheme.Text;
            BackColor = UiTheme.Surface;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            AccessibleRole = AccessibleRole.CheckButton;
            TabStop = true;
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new ToggleSwitchAccessibleObject(this);
        }

        private sealed class ToggleSwitchAccessibleObject : ControlAccessibleObject
        {
            private readonly ToggleSwitch owner;

            public ToggleSwitchAccessibleObject(ToggleSwitch ownerControl)
                : base(ownerControl)
            {
                owner = ownerControl;
            }

            public override AccessibleStates State
            {
                get
                {
                    AccessibleStates state = base.State;
                    return owner.Checked ? state | AccessibleStates.Checked : state;
                }
            }

            public override string DefaultAction
            {
                get { return "切换"; }
            }

            public override void DoDefaultAction()
            {
                owner.Checked = !owner.Checked;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            AccessibleName = Text;
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle controlBounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (hovered)
            {
                using (GraphicsPath hoverPath = RoundedPanel.CreateRoundedRect(controlBounds, 8))
                using (SolidBrush hoverBrush = new SolidBrush(UiTheme.SurfaceRaised))
                    e.Graphics.FillPath(hoverBrush, hoverPath);
            }

            Rectangle track = new Rectangle(Width - 45, (Height - 22) / 2, 40, 22);
            Color trackColor = Checked ? UiTheme.Accent : UiTheme.SurfaceMuted;
            using (GraphicsPath trackPath = RoundedPanel.CreateRoundedRect(track, 11))
            using (SolidBrush trackBrush = new SolidBrush(trackColor))
                e.Graphics.FillPath(trackBrush, trackPath);

            int thumbX = Checked ? track.Right - 19 : track.Left + 3;
            using (SolidBrush thumbBrush = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(thumbBrush, thumbX, track.Top + 3, 16, 16);

            Rectangle textBounds = new Rectangle(6, 0, Math.Max(0, Width - 57), Height);
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textBounds,
                Enabled ? UiTheme.Text : UiTheme.TextDim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(controlBounds, -2, -2));
        }
    }

    internal sealed class ModeSelector : Control
    {
        private int selectedIndex;
        private int hoveredIndex = -1;

        public string FirstText = string.Empty;
        public string SecondText = string.Empty;
        public event EventHandler SelectedIndexChanged;

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                int normalized = value == 1 ? 1 : 0;
                AccessibleDescription = normalized == 0 ? FirstText : SecondText;
                if (selectedIndex == normalized)
                    return;
                selectedIndex = normalized;
                Invalidate();
                EventHandler handler = SelectedIndexChanged;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        public ModeSelector()
        {
            SetStyle(ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Surface;
            ForeColor = UiTheme.Text;
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.ComboBox;
            AccessibleName = "键盘输入模式";
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int next = e.X < Width / 2 ? 0 : 1;
            if (next != hoveredIndex)
            {
                hoveredIndex = next;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoveredIndex = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            SelectedIndex = e.X < Width / 2 ? 0 : 1;
            Focus();
            base.OnMouseClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up)
            {
                SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down)
            {
                SelectedIndex = 1;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                SelectedIndex = SelectedIndex == 0 ? 1 : 0;
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath outer = RoundedPanel.CreateRoundedRect(bounds, 8))
            using (SolidBrush backgroundBrush = new SolidBrush(UiTheme.SurfaceMuted))
            using (Pen borderPen = new Pen(UiTheme.Border, 1))
            {
                e.Graphics.FillPath(backgroundBrush, outer);
                e.Graphics.DrawPath(borderPen, outer);
            }

            int half = Width / 2;
            for (int index = 0; index < 2; index++)
            {
                Rectangle item = index == 0
                    ? new Rectangle(3, 3, half - 5, Height - 7)
                    : new Rectangle(half + 2, 3, Width - half - 5, Height - 7);
                Color fill = index == selectedIndex
                    ? UiTheme.Accent
                    : index == hoveredIndex ? UiTheme.SurfaceRaised : UiTheme.SurfaceMuted;
                using (GraphicsPath itemPath = RoundedPanel.CreateRoundedRect(item, 6))
                using (SolidBrush itemBrush = new SolidBrush(fill))
                    e.Graphics.FillPath(itemBrush, itemPath);

                TextRenderer.DrawText(
                    e.Graphics,
                    index == 0 ? FirstText : SecondText,
                    Font,
                    item,
                    index == selectedIndex ? Color.White : UiTheme.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -2, -2));
        }
    }

    internal sealed class PhoneGlyph : Control
    {
        public PhoneGlyph()
        {
            SetStyle(ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(UiTheme.Accent, 2.2F))
            using (SolidBrush dot = new SolidBrush(UiTheme.Accent))
            {
                Rectangle phone = new Rectangle(9, 4, Width - 19, Height - 9);
                using (GraphicsPath path = RoundedPanel.CreateRoundedRect(phone, 6))
                    e.Graphics.DrawPath(pen, path);
                e.Graphics.DrawLine(pen, phone.Left + 8, phone.Top + 6, phone.Right - 8, phone.Top + 6);
                e.Graphics.FillEllipse(dot, phone.Left + (phone.Width / 2) - 2, phone.Bottom - 6, 4, 4);
            }
        }
    }

    internal sealed class StatusDot : Control
    {
        public Color DotColor = UiTheme.Offline;

        public StatusDot()
        {
            SetStyle(ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(DotColor))
                e.Graphics.FillEllipse(brush, 1, 1, Width - 2, Height - 2);
        }
    }

    internal sealed class DeviceCard
    {
        public string Name;
        public string Model;
        public string Serial;
        public Label StatusLabel;
        public StatusDot StatusDot;
        public RoundedPanel StatusBadge;
        public Button LaunchButton;
        public Button TransferButton;
        public int WindowX;
        public Process MirrorProcess;
        public int MirrorProcessId;
        public int ScreenPowerRequestId;
        public bool ScreenOffApplied;
        public int TopMostRequestId;
    }

    internal sealed class RoundedPanel : Panel
    {
        public Color BorderColor = UiTheme.Border;
        public int CornerRadius = 14;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (Width > 1 && Height > 1)
            {
                using (GraphicsPath path = CreateRoundedRect(new Rectangle(0, 0, Width, Height), CornerRadius))
                    Region = new Region(path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = CreateRoundedRect(r, CornerRadius))
            using (Pen pen = new Pen(BorderColor, 1))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        internal static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
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
        private readonly Color background = UiTheme.Background;
        private readonly Color card = UiTheme.Surface;
        private readonly Color foreground = UiTheme.Text;
        private readonly Color muted = UiTheme.TextMuted;
        private readonly Color accent = UiTheme.Accent;
        private readonly Color online = UiTheme.Success;
        private readonly Color offline = UiTheme.Offline;

        private readonly string appDirectory;
        private readonly string adbPath;
        private readonly string scrcpyPath;
        private readonly DeviceCard device1;
        private readonly DeviceCard device2;
        private readonly Timer refreshTimer;
        private readonly Timer restartTimer;
        private readonly Label footerStatus;
        private readonly ModeSelector keyboardModeSelector;
        private readonly ToggleSwitch screenOffToggle;
        private readonly ToggleSwitch alwaysOnTopToggle;
        private readonly string settingsPath;
        private readonly string screenOffSettingsPath;
        private readonly string alwaysOnTopSettingsPath;
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
            alwaysOnTopSettingsPath = Path.Combine(
                Path.GetDirectoryName(settingsPath),
                "always-on-top-enabled.txt");
            keyboardMode = LoadKeyboardMode();

            Text = "OPhoneMirror · 手机有线投屏";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(800, 552);
            MinimumSize = new Size(816, 591);
            BackColor = background;
            ForeColor = foreground;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            Label title = new Label();
            title.Text = "OPhoneMirror";
            title.Font = new Font("Microsoft YaHei UI", 24F, FontStyle.Bold);
            title.ForeColor = foreground;
            title.AutoSize = true;
            title.Location = new Point(32, 24);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "USB 低延迟投屏、键鼠控制与文件互传";
            subtitle.Font = new Font("Microsoft YaHei UI", 10F);
            subtitle.ForeColor = muted;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(35, 72);
            Controls.Add(subtitle);

            Button refresh = MakeSecondaryButton("刷新设备");
            refresh.Location = new Point(660, 32);
            refresh.Size = new Size(108, 38);
            refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refresh.Click += delegate { RefreshDevices(); };
            Controls.Add(refresh);

            List<DeviceDefinition> definitions = LoadDeviceDefinitions();
            device1 = CreateDeviceCard(definitions[0], 32, 116);
            device2 = CreateDeviceCard(definitions[1], 412, 116);

            RoundedPanel info = new RoundedPanel();
            info.Name = "optionsPanel";
            info.BackColor = card;
            info.Location = new Point(32, 344);
            info.Size = new Size(736, 134);
            info.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            Label infoTitle = new Label();
            infoTitle.Text = "投屏选项";
            infoTitle.ForeColor = foreground;
            infoTitle.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            infoTitle.AutoSize = true;
            infoTitle.Location = new Point(20, 16);
            info.Controls.Add(infoTitle);

            Label infoCaption = new Label();
            infoCaption.Text = "修改后会立即应用到正在运行的投屏";
            infoCaption.ForeColor = muted;
            infoCaption.AutoSize = true;
            infoCaption.Location = new Point(20, 43);
            info.Controls.Add(infoCaption);

            screenOffToggle = new ToggleSwitch();
            screenOffToggle.Text = "仅熄手机屏幕";
            screenOffToggle.Location = new Point(14, 76);
            screenOffToggle.Size = new Size(176, 40);
            screenOffToggle.Checked = LoadScreenOffSetting();
            screenOffToggle.CheckedChanged += ScreenOffSettingChanged;
            info.Controls.Add(screenOffToggle);

            alwaysOnTopToggle = new ToggleSwitch();
            alwaysOnTopToggle.Text = "保持在最顶层";
            alwaysOnTopToggle.Location = new Point(198, 76);
            alwaysOnTopToggle.Size = new Size(176, 40);
            alwaysOnTopToggle.Checked = LoadAlwaysOnTopSetting();
            alwaysOnTopToggle.CheckedChanged += AlwaysOnTopSettingChanged;
            info.Controls.Add(alwaysOnTopToggle);

            Label keyboardLabel = new Label();
            keyboardLabel.Text = "键盘输入模式";
            keyboardLabel.ForeColor = muted;
            keyboardLabel.AutoSize = true;
            keyboardLabel.Location = new Point(398, 58);
            info.Controls.Add(keyboardLabel);

            keyboardModeSelector = new ModeSelector();
            keyboardModeSelector.FirstText = "搜狗短语 · Shift 中英";
            keyboardModeSelector.SecondText = "数字选词 · Shift+Space";
            keyboardModeSelector.Location = new Point(398, 82);
            keyboardModeSelector.Size = new Size(316, 32);
            keyboardModeSelector.SelectedIndex = keyboardMode;
            keyboardModeSelector.SelectedIndexChanged += KeyboardModeChanged;
            info.Controls.Add(keyboardModeSelector);
            Controls.Add(info);

            footerStatus = new Label();
            footerStatus.Text = "正在检查设备…";
            footerStatus.ForeColor = muted;
            footerStatus.AutoSize = true;
            footerStatus.Location = new Point(36, 510);
            footerStatus.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            Controls.Add(footerStatus);

            Label version = new Label();
            version.Text = "OPhoneMirror 1.9.0 · scrcpy 4.1";
            version.ForeColor = muted;
            version.AutoSize = false;
            version.Location = new Point(500, 505);
            version.Size = new Size(268, 24);
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
            panel.Size = new Size(356, 204);
            panel.BorderColor = UiTheme.Border;

            PhoneGlyph phoneIcon = new PhoneGlyph();
            phoneIcon.Location = new Point(18, 18);
            phoneIcon.Size = new Size(46, 52);
            panel.Controls.Add(phoneIcon);

            Label nameLabel = new Label();
            nameLabel.Text = device.Name;
            nameLabel.Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold);
            nameLabel.ForeColor = foreground;
            nameLabel.AutoSize = true;
            nameLabel.Location = new Point(78, 18);
            panel.Controls.Add(nameLabel);

            Label modelLabel = new Label();
            modelLabel.Text = device.Model;
            modelLabel.ForeColor = muted;
            modelLabel.AutoSize = true;
            modelLabel.Location = new Point(80, 55);
            panel.Controls.Add(modelLabel);

            device.StatusBadge = new RoundedPanel();
            device.StatusBadge.BackColor = UiTheme.SurfaceMuted;
            device.StatusBadge.BorderColor = UiTheme.Border;
            device.StatusBadge.CornerRadius = 14;
            device.StatusBadge.Location = new Point(20, 88);
            device.StatusBadge.Size = new Size(116, 30);
            panel.Controls.Add(device.StatusBadge);

            device.StatusDot = new StatusDot();
            device.StatusDot.DotColor = offline;
            device.StatusDot.Location = new Point(12, 10);
            device.StatusDot.Size = new Size(10, 10);
            device.StatusBadge.Controls.Add(device.StatusDot);

            device.StatusLabel = new Label();
            device.StatusLabel.Text = "未连接";
            device.StatusLabel.ForeColor = muted;
            device.StatusLabel.AutoSize = true;
            device.StatusLabel.Location = new Point(29, 6);
            device.StatusBadge.Controls.Add(device.StatusLabel);

            device.LaunchButton = MakePrimaryButton("启动有线投屏");
            device.LaunchButton.Location = new Point(20, 140);
            device.LaunchButton.Size = new Size(202, 44);
            device.LaunchButton.Enabled = false;
            device.LaunchButton.Click += delegate { LaunchDevice(device); };
            panel.Controls.Add(device.LaunchButton);

            device.TransferButton = MakeSecondaryButton("文件互传");
            device.TransferButton.Location = new Point(230, 140);
            device.TransferButton.Size = new Size(106, 44);
            device.TransferButton.Enabled = false;
            device.TransferButton.Click += delegate { OpenTransfer(device); };
            panel.Controls.Add(device.TransferButton);

            Controls.Add(panel);
            return device;
        }

        private Button MakePrimaryButton(string text)
        {
            ModernButton b = new ModernButton();
            b.Text = text;
            b.Kind = UiButtonKind.Primary;
            b.BackColor = accent;
            b.ForeColor = Color.White;
            b.Cursor = Cursors.Hand;
            b.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            return b;
        }

        private Button MakeSecondaryButton(string text)
        {
            ModernButton b = new ModernButton();
            b.Text = text;
            b.Kind = UiButtonKind.Secondary;
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
            device.StatusDot.DotColor = connected ? online : offline;
            device.StatusDot.Invalidate();
            device.StatusBadge.BackColor = connected ? Color.FromArgb(25, 65, 58) : UiTheme.SurfaceMuted;
            device.StatusBadge.BorderColor = connected ? Color.FromArgb(45, 128, 101) : UiTheme.Border;
            device.StatusBadge.Invalidate();
            device.StatusLabel.Text = connected ? "USB 已连接" : "未连接";
            device.StatusLabel.ForeColor = connected ? online : muted;
            device.LaunchButton.Enabled = connected;
            device.LaunchButton.BackColor = connected ? accent : UiTheme.SurfaceMuted;
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
                    "--serial={0} --window-title=\"{1} USB Low Latency\" --video-codec=h264 --max-fps=60 --video-bit-rate=16M --video-buffer=0 --no-audio --shortcut-mod=lalt --window-x={2} --window-y=80 --window-width=450 --window-height=900{3}",
                    device.Serial, device.Name, device.WindowX, keyboardArg);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = scrcpyPath;
                psi.Arguments = args;
                psi.WorkingDirectory = Path.GetDirectoryName(scrcpyPath);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                device.MirrorProcess = Process.Start(psi);
                device.MirrorProcessId = device.MirrorProcess.Id;
                ApplyTopMostWhenReady(device, device.MirrorProcess);
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

        private bool LoadAlwaysOnTopSetting()
        {
            try
            {
                return File.ReadAllText(alwaysOnTopSettingsPath).Trim() != "0";
            }
            catch
            {
                return true;
            }
        }

        private void SaveAlwaysOnTopSetting()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(alwaysOnTopSettingsPath));
                File.WriteAllText(
                    alwaysOnTopSettingsPath,
                    alwaysOnTopToggle.Checked ? "1" : "0",
                    Encoding.UTF8);
            }
            catch
            {
                // A settings write failure must not prevent mirroring.
            }
        }

        private void AlwaysOnTopSettingChanged(object sender, EventArgs e)
        {
            SaveAlwaysOnTopSetting();
            ApplyTopMostToRunning(device1);
            ApplyTopMostToRunning(device2);
            footerStatus.Text = alwaysOnTopToggle.Checked
                ? "已让正在运行的投屏保持在最顶层"
                : "已允许其他窗口覆盖正在运行的投屏";
        }

        private void ApplyTopMostToRunning(DeviceCard device)
        {
            Process process = device.MirrorProcess;
            if (!IsProcessRunning(process))
                return;

            try
            {
                process.Refresh();
                IntPtr window = process.MainWindowHandle;
                if (window != IntPtr.Zero)
                {
                    SetMirrorTopMost(window, alwaysOnTopToggle.Checked);
                    return;
                }
            }
            catch
            {
                return;
            }

            ApplyTopMostWhenReady(device, process);
        }

        private void ApplyTopMostWhenReady(DeviceCard device, Process mirrorProcess)
        {
            int requestId = System.Threading.Interlocked.Increment(ref device.TopMostRequestId);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                IntPtr mirrorWindow = IntPtr.Zero;
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    if (IsDisposed || requestId != device.TopMostRequestId)
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
                        if (requestId != device.TopMostRequestId ||
                            !IsProcessRunning(device.MirrorProcess) ||
                            device.MirrorProcessId != mirrorProcess.Id)
                            return;

                        SetMirrorTopMost(mirrorWindow, alwaysOnTopToggle.Checked);
                    });
                }
                catch
                {
                    // The main form may close while scrcpy is creating its window.
                }
            });
        }

        private static bool SetMirrorTopMost(IntPtr window, bool enabled)
        {
            IntPtr insertAfter = enabled ? HwndTopMost : HwndNoTopMost;
            return SetWindowPos(
                window,
                insertAfter,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoActivate);
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

            System.Threading.Interlocked.Increment(ref device.TopMostRequestId);
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
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;
        private static readonly IntPtr HwndTopMost = new IntPtr(-1);
        private static readonly IntPtr HwndNoTopMost = new IntPtr(-2);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

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
