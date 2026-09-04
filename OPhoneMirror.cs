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
[assembly: AssemblyVersion("1.13.0.0")]
[assembly: AssemblyFileVersion("1.13.0.0")]

namespace OPhoneMirror
{
    internal sealed class DeviceDefinition
    {
        public string Name;
        public string Model;
        public string Serial;
        public int WindowX;
    }

    internal enum AppThemeMode
    {
        Auto,
        Light,
        Dark
    }

    internal enum MirrorSessionState
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Recovering
    }

    internal sealed class ThemePalette
    {
        public bool IsDark;
        public Color Background;
        public Color Surface;
        public Color SurfaceRaised;
        public Color SurfaceMuted;
        public Color SurfaceHover;
        public Color SurfacePressed;
        public Color Border;
        public Color BorderStrong;
        public Color Text;
        public Color TextMuted;
        public Color TextDim;
        public Color Accent;
        public Color AccentHover;
        public Color AccentPressed;
        public Color Success;
        public Color Offline;
        public Color Danger;
    }

    internal static class UiTheme
    {
        public const string FontFamily = "Microsoft YaHei UI";
        public const string DisplayFontFamily = "Segoe UI";
        private static ThemePalette current = CreateLight();

        public static event Action<ThemePalette> ThemeChanged;
        public static bool IsDark { get { return current.IsDark; } }
        public static Color Background { get { return current.Background; } }
        public static Color Surface { get { return current.Surface; } }
        public static Color SurfaceRaised { get { return current.SurfaceRaised; } }
        public static Color SurfaceMuted { get { return current.SurfaceMuted; } }
        public static Color SurfaceHover { get { return current.SurfaceHover; } }
        public static Color SurfacePressed { get { return current.SurfacePressed; } }
        public static Color Border { get { return current.Border; } }
        public static Color BorderStrong { get { return current.BorderStrong; } }
        public static Color Text { get { return current.Text; } }
        public static Color TextMuted { get { return current.TextMuted; } }
        public static Color TextDim { get { return current.TextDim; } }
        public static Color Accent { get { return current.Accent; } }
        public static Color AccentHover { get { return current.AccentHover; } }
        public static Color AccentPressed { get { return current.AccentPressed; } }
        public static Color Success { get { return current.Success; } }
        public static Color Offline { get { return current.Offline; } }
        public static Color Danger { get { return current.Danger; } }

        public static void SetDark(bool dark)
        {
            if (current.IsDark == dark)
                return;
            ThemePalette previous = current;
            current = dark ? CreateDark() : CreateLight();
            Action<ThemePalette> handler = ThemeChanged;
            if (handler != null)
                handler(previous);
        }

        public static Color Map(Color value, ThemePalette previous)
        {
            if (value == previous.Background) return Background;
            if (value == previous.Surface) return Surface;
            if (value == previous.SurfaceRaised) return SurfaceRaised;
            if (value == previous.SurfaceMuted) return SurfaceMuted;
            if (value == previous.SurfaceHover) return SurfaceHover;
            if (value == previous.SurfacePressed) return SurfacePressed;
            if (value == previous.Border) return Border;
            if (value == previous.BorderStrong) return BorderStrong;
            if (value == previous.Text) return Text;
            if (value == previous.TextMuted) return TextMuted;
            if (value == previous.TextDim) return TextDim;
            if (value == previous.Accent) return Accent;
            if (value == previous.AccentHover) return AccentHover;
            if (value == previous.AccentPressed) return AccentPressed;
            if (value == previous.Success) return Success;
            if (value == previous.Offline) return Offline;
            if (value == previous.Danger) return Danger;
            return value;
        }

        public static void ApplyControlTree(Control root, ThemePalette previous)
        {
            root.BackColor = Map(root.BackColor, previous);
            root.ForeColor = Map(root.ForeColor, previous);
            Label label = root as Label;
            if (label != null && label.BackColor == Color.Transparent && label.Parent != null)
                label.BackColor = label.Parent.BackColor;

            RoundedPanel rounded = root as RoundedPanel;
            if (rounded != null)
                rounded.BorderColor = Map(rounded.BorderColor, previous);

            StatusDot status = root as StatusDot;
            if (status != null)
                status.DotColor = Map(status.DotColor, previous);

            ModernButton button = root as ModernButton;
            if (button != null)
            {
                button.BackColor = button.Kind == UiButtonKind.Primary ? Accent :
                    button.Kind == UiButtonKind.Danger ? Danger : Surface;
                button.ForeColor = button.Kind == UiButtonKind.Primary || button.Kind == UiButtonKind.Danger
                    ? Color.White : Text;
            }

            DataGridView grid = root as DataGridView;
            if (grid != null)
            {
                grid.BackgroundColor = Surface;
                grid.GridColor = Border;
                grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceRaised;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceRaised;
                grid.DefaultCellStyle.BackColor = Surface;
                grid.DefaultCellStyle.ForeColor = Text;
                grid.DefaultCellStyle.SelectionBackColor = IsDark
                    ? Color.FromArgb(38, 72, 112)
                    : Color.FromArgb(222, 237, 255);
                grid.DefaultCellStyle.SelectionForeColor = Text;
            }

            foreach (Control child in root.Controls)
                ApplyControlTree(child, previous);
            root.Invalidate();
        }

        public static bool SystemUsesDarkTheme()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static ThemePalette CreateLight()
        {
            ThemePalette p = new ThemePalette();
            p.IsDark = false;
            p.Background = Color.FromArgb(245, 245, 247);
            p.Surface = Color.White;
            p.SurfaceRaised = Color.FromArgb(250, 250, 252);
            p.SurfaceMuted = Color.FromArgb(238, 238, 241);
            p.SurfaceHover = Color.FromArgb(232, 232, 236);
            p.SurfacePressed = Color.FromArgb(218, 218, 223);
            p.Border = Color.FromArgb(222, 222, 226);
            p.BorderStrong = Color.FromArgb(190, 190, 196);
            p.Text = Color.FromArgb(29, 29, 31);
            p.TextMuted = Color.FromArgb(88, 88, 94);
            p.TextDim = Color.FromArgb(134, 134, 139);
            p.Accent = Color.FromArgb(0, 122, 255);
            p.AccentHover = Color.FromArgb(0, 113, 227);
            p.AccentPressed = Color.FromArgb(0, 97, 204);
            p.Success = Color.FromArgb(31, 122, 54);
            p.Offline = Color.FromArgb(134, 134, 139);
            p.Danger = Color.FromArgb(215, 0, 21);
            return p;
        }

        private static ThemePalette CreateDark()
        {
            ThemePalette p = new ThemePalette();
            p.IsDark = true;
            p.Background = Color.FromArgb(28, 28, 30);
            p.Surface = Color.FromArgb(44, 44, 46);
            p.SurfaceRaised = Color.FromArgb(58, 58, 60);
            p.SurfaceMuted = Color.FromArgb(72, 72, 74);
            p.SurfaceHover = Color.FromArgb(82, 82, 85);
            p.SurfacePressed = Color.FromArgb(99, 99, 102);
            p.Border = Color.FromArgb(72, 72, 74);
            p.BorderStrong = Color.FromArgb(112, 112, 117);
            p.Text = Color.FromArgb(245, 245, 247);
            p.TextMuted = Color.FromArgb(199, 199, 204);
            p.TextDim = Color.FromArgb(142, 142, 147);
            p.Accent = Color.FromArgb(10, 132, 255);
            p.AccentHover = Color.FromArgb(64, 156, 255);
            p.AccentPressed = Color.FromArgb(0, 102, 204);
            p.Success = Color.FromArgb(48, 209, 88);
            p.Offline = Color.FromArgb(142, 142, 147);
            p.Danger = Color.FromArgb(255, 69, 58);
            return p;
        }
    }

    internal static class WindowTheme
    {
        public static void Apply(Form form)
        {
            try
            {
                int enabled = UiTheme.IsDark ? 1 : 0;
                IntPtr handle = form.Handle;
                if (DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)) != 0)
                    DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
            }
            catch
            {
                // Older Windows versions may not support dark title bars.
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }

    internal enum UiButtonKind
    {
        Primary,
        Secondary,
        Quiet,
        Danger
    }

    internal enum UiIcon
    {
        None,
        Refresh,
        Mirror,
        Transfer,
        ChevronDown,
        Check,
        Folder,
        ArrowUp,
        More,
        Trash,
        Stop,
        ChevronUp
    }

    internal static class UiIconRenderer
    {
        public static void Draw(Graphics graphics, UiIcon icon, Rectangle bounds, Color color)
        {
            if (icon == UiIcon.None)
                return;

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = Math.Max(0.75F, Math.Min(bounds.Width, bounds.Height) / 24F);
            float left = bounds.Left;
            float top = bounds.Top;
            using (Pen pen = new Pen(color, 1.8F * scale))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                if (icon == UiIcon.Refresh)
                {
                    RectangleF arc = new RectangleF(left + 4 * scale, top + 4 * scale, 16 * scale, 16 * scale);
                    graphics.DrawArc(pen, arc, -42, 285);
                    PointF tip = new PointF(left + 20.2F * scale, top + 5.3F * scale);
                    graphics.DrawLines(pen, new PointF[]
                    {
                        new PointF(left + 15.8F * scale, top + 4.4F * scale),
                        tip,
                        new PointF(left + 19.2F * scale, top + 9.6F * scale)
                    });
                }
                else if (icon == UiIcon.Mirror)
                {
                    RectangleF screen = new RectangleF(left + 3 * scale, top + 4 * scale, 18 * scale, 13 * scale);
                    graphics.DrawRoundedRectangle(pen, screen, 2.5F * scale);
                    graphics.DrawLine(pen, left + 9 * scale, top + 21 * scale, left + 15 * scale, top + 21 * scale);
                    graphics.DrawLine(pen, left + 12 * scale, top + 17 * scale, left + 12 * scale, top + 21 * scale);
                    PointF[] play = new PointF[]
                    {
                        new PointF(left + 10 * scale, top + 8 * scale),
                        new PointF(left + 10 * scale, top + 14 * scale),
                        new PointF(left + 15 * scale, top + 11 * scale)
                    };
                    graphics.FillPolygon(brush, play);
                }
                else if (icon == UiIcon.Transfer)
                {
                    graphics.DrawLine(pen, left + 4 * scale, top + 8 * scale, left + 17 * scale, top + 8 * scale);
                    graphics.DrawLines(pen, new PointF[]
                    {
                        new PointF(left + 14 * scale, top + 5 * scale),
                        new PointF(left + 18 * scale, top + 8 * scale),
                        new PointF(left + 14 * scale, top + 11 * scale)
                    });
                    graphics.DrawLine(pen, left + 20 * scale, top + 16 * scale, left + 7 * scale, top + 16 * scale);
                    graphics.DrawLines(pen, new PointF[]
                    {
                        new PointF(left + 10 * scale, top + 13 * scale),
                        new PointF(left + 6 * scale, top + 16 * scale),
                        new PointF(left + 10 * scale, top + 19 * scale)
                    });
                }
                else if (icon == UiIcon.ChevronDown)
                {
                    graphics.DrawLines(pen, new PointF[]
                    {
                        new PointF(left + 6 * scale, top + 9 * scale),
                        new PointF(left + 12 * scale, top + 15 * scale),
                        new PointF(left + 18 * scale, top + 9 * scale)
                    });
                }
                else if (icon == UiIcon.ChevronUp)
                {
                    graphics.DrawLines(pen, new PointF[]
                    {
                        new PointF(left + 6 * scale, top + 15 * scale),
                        new PointF(left + 12 * scale, top + 9 * scale),
                        new PointF(left + 18 * scale, top + 15 * scale)
                    });
                }
                else if (icon == UiIcon.Check)
                {
                    graphics.DrawLines(pen, new PointF[]
                    {
                        new PointF(left + 5 * scale, top + 12 * scale),
                        new PointF(left + 10 * scale, top + 17 * scale),
                        new PointF(left + 19 * scale, top + 7 * scale)
                    });
                }
                else if (icon == UiIcon.Folder)
                {
                    using (GraphicsPath folder = new GraphicsPath())
                    {
                        folder.AddLines(new PointF[]
                        {
                            new PointF(left + 3 * scale, top + 7 * scale),
                            new PointF(left + 9 * scale, top + 7 * scale),
                            new PointF(left + 11 * scale, top + 9 * scale),
                            new PointF(left + 21 * scale, top + 9 * scale),
                            new PointF(left + 20 * scale, top + 19 * scale),
                            new PointF(left + 4 * scale, top + 19 * scale)
                        });
                        folder.CloseFigure();
                        graphics.DrawPath(pen, folder);
                    }
                }
                else if (icon == UiIcon.ArrowUp)
                {
                    graphics.DrawLine(pen, left + 12 * scale, top + 19 * scale, left + 12 * scale, top + 5 * scale);
                    graphics.DrawLines(pen, new PointF[]
                    {
                        new PointF(left + 6 * scale, top + 11 * scale),
                        new PointF(left + 12 * scale, top + 5 * scale),
                        new PointF(left + 18 * scale, top + 11 * scale)
                    });
                }
                else if (icon == UiIcon.More)
                {
                    graphics.FillEllipse(brush, left + 4 * scale, top + 10 * scale, 3 * scale, 3 * scale);
                    graphics.FillEllipse(brush, left + 10.5F * scale, top + 10 * scale, 3 * scale, 3 * scale);
                    graphics.FillEllipse(brush, left + 17 * scale, top + 10 * scale, 3 * scale, 3 * scale);
                }
                else if (icon == UiIcon.Trash)
                {
                    graphics.DrawLine(pen, left + 6 * scale, top + 7 * scale, left + 18 * scale, top + 7 * scale);
                    graphics.DrawLine(pen, left + 9 * scale, top + 4 * scale, left + 15 * scale, top + 4 * scale);
                    graphics.DrawRoundedRectangle(pen, new RectangleF(left + 7 * scale, top + 8 * scale, 10 * scale, 12 * scale), 1.5F * scale);
                    graphics.DrawLine(pen, left + 10 * scale, top + 11 * scale, left + 10 * scale, top + 17 * scale);
                    graphics.DrawLine(pen, left + 14 * scale, top + 11 * scale, left + 14 * scale, top + 17 * scale);
                }
                else if (icon == UiIcon.Stop)
                {
                    graphics.FillRectangle(brush, left + 7 * scale, top + 7 * scale, 10 * scale, 10 * scale);
                }
            }
        }

        private static void DrawRoundedRectangle(this Graphics graphics, Pen pen, RectangleF bounds, float radius)
        {
            float diameter = radius * 2;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                graphics.DrawPath(pen, path);
            }
        }
    }

    internal sealed class ModernButton : Control
    {
        private bool hovered;
        private bool pressed;
        private bool keyboardPressed;
        private int cornerRadius = 10;
        private readonly Timer iconAnimationTimer;
        private bool animateIcon;

        public UiButtonKind Kind = UiButtonKind.Secondary;
        public UiIcon Icon = UiIcon.None;
        public bool IconOnly;
        public int IconSize = 20;
        public bool AnimateIcon
        {
            get { return animateIcon; }
            set
            {
                animateIcon = value;
                iconAnimationTimer.Enabled = value;
                Invalidate();
            }
        }
        public int CornerRadius
        {
            get { return cornerRadius; }
            set
            {
                cornerRadius = Math.Max(2, value);
                Invalidate();
            }
        }

        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.StandardClick, true);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            iconAnimationTimer = new Timer();
            iconAnimationTimer.Interval = 40;
            iconAnimationTimer.Tick += delegate { Invalidate(); };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                iconAnimationTimer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
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
            if (Enabled && mevent.Button == MouseButtons.Left)
            {
                Focus();
                pressed = true;
            }
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Space || key == Keys.Enter || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                keyboardPressed = true;
                pressed = true;
                Invalidate();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (keyboardPressed && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                keyboardPressed = false;
                pressed = false;
                Invalidate();
                PerformClick();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyUp(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            keyboardPressed = false;
            pressed = false;
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        public void PerformClick()
        {
            if (Enabled)
                OnClick(EventArgs.Empty);
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
            Color content;

            if (!Enabled)
            {
                fill = UiTheme.SurfaceMuted;
                content = UiTheme.TextDim;
            }
            else if (Kind == UiButtonKind.Primary)
            {
                fill = pressed ? UiTheme.AccentPressed : hovered ? UiTheme.AccentHover : UiTheme.Accent;
                content = Color.White;
            }
            else if (Kind == UiButtonKind.Danger)
            {
                fill = pressed ? Color.FromArgb(190, UiTheme.Danger) :
                    hovered ? Color.FromArgb(224, UiTheme.Danger) : UiTheme.Danger;
                content = Color.White;
            }
            else if (Kind == UiButtonKind.Quiet)
            {
                fill = pressed ? UiTheme.SurfacePressed : hovered ? UiTheme.SurfaceHover : BackColor;
                content = ForeColor;
            }
            else
            {
                fill = pressed ? UiTheme.SurfacePressed : hovered ? UiTheme.SurfaceHover : UiTheme.SurfaceMuted;
                content = UiTheme.Text;
            }

            using (GraphicsPath path = RoundedPanel.CreateRoundedRect(bounds, CornerRadius))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                e.Graphics.FillPath(brush, path);
            }

            if (Icon != UiIcon.None)
            {
                int size = Math.Min(IconSize, Math.Min(Width - 8, Height - 8));
                Rectangle iconBounds;
                if (IconOnly || string.IsNullOrEmpty(Text))
                {
                    iconBounds = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
                }
                else
                {
                    iconBounds = new Rectangle(13, (Height - size) / 2, size, size);
                }
                GraphicsState iconState = null;
                if (AnimateIcon)
                {
                    iconState = e.Graphics.Save();
                    float centerX = iconBounds.Left + iconBounds.Width / 2F;
                    float centerY = iconBounds.Top + iconBounds.Height / 2F;
                    e.Graphics.TranslateTransform(centerX, centerY);
                    e.Graphics.RotateTransform((Environment.TickCount / 3F) % 360F);
                    e.Graphics.TranslateTransform(-centerX, -centerY);
                }
                UiIconRenderer.Draw(e.Graphics, Icon, iconBounds, content);
                if (iconState != null)
                    e.Graphics.Restore(iconState);
            }

            if (!IconOnly && !string.IsNullOrEmpty(Text))
            {
                Rectangle textBounds = Icon == UiIcon.None
                    ? bounds
                    : new Rectangle(41, 0, Math.Max(0, Width - 50), Height);
                TextRenderer.DrawText(
                    e.Graphics,
                    Text,
                    Font,
                    textBounds,
                    content,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }

            if (Focused && ShowFocusCues)
            {
                Rectangle focus = Rectangle.Inflate(bounds, -2, -2);
                using (GraphicsPath focusPath = RoundedPanel.CreateRoundedRect(focus, Math.Max(4, CornerRadius - 2)))
                using (Pen focusPen = new Pen(UiTheme.Accent, 2))
                    e.Graphics.DrawPath(focusPen, focusPath);
            }
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new ModernButtonAccessibleObject(this);
        }

        private sealed class ModernButtonAccessibleObject : ControlAccessibleObject
        {
            private readonly ModernButton ownerButton;

            public ModernButtonAccessibleObject(ModernButton owner)
                : base(owner)
            {
                ownerButton = owner;
            }

            public override AccessibleRole Role
            {
                get { return AccessibleRole.PushButton; }
            }

            public override string DefaultAction
            {
                get { return "按下"; }
            }

            public override void DoDefaultAction()
            {
                ownerButton.PerformClick();
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
            Font = new Font(UiTheme.FontFamily, 9F, FontStyle.Regular);
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
            using (Pen trackPen = new Pen(Checked ? UiTheme.Accent : UiTheme.BorderStrong, 1))
            {
                e.Graphics.FillPath(trackBrush, trackPath);
                e.Graphics.DrawPath(trackPen, trackPath);
            }

            int thumbX = Checked ? track.Right - 19 : track.Left + 3;
            using (SolidBrush thumbBrush = new SolidBrush(Color.White))
            using (Pen thumbPen = new Pen(Color.FromArgb(205, 205, 210), 1))
            {
                e.Graphics.FillEllipse(thumbBrush, thumbX, track.Top + 3, 16, 16);
                e.Graphics.DrawEllipse(thumbPen, thumbX, track.Top + 3, 16, 16);
            }

            Rectangle textBounds = new Rectangle(6, 0, Math.Max(0, Width - 57), Height);
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textBounds,
                Enabled ? UiTheme.Text : UiTheme.TextDim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            if (Focused && ShowFocusCues)
            {
                using (GraphicsPath focusPath = RoundedPanel.CreateRoundedRect(Rectangle.Inflate(controlBounds, -2, -2), 7))
                using (Pen focusPen = new Pen(UiTheme.Accent, 2))
                    e.Graphics.DrawPath(focusPen, focusPath);
            }
        }
    }

    internal sealed class ModeSelector : Control
    {
        private int selectedIndex;
        private int hoveredIndex = -1;

        public string FirstText = string.Empty;
        public string SecondText = string.Empty;
        public string ThirdText = string.Empty;
        public event EventHandler SelectedIndexChanged;

        private int ItemCount
        {
            get { return string.IsNullOrEmpty(ThirdText) ? 2 : 3; }
        }

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                int normalized = Math.Max(0, Math.Min(ItemCount - 1, value));
                AccessibleDescription = ItemText(normalized);
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
            Font = new Font(UiTheme.FontFamily, 8.5F, FontStyle.Regular);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.ComboBox;
            AccessibleName = "键盘输入模式";
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int next = Math.Min(ItemCount - 1, Math.Max(0, e.X * ItemCount / Math.Max(1, Width)));
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
            SelectedIndex = Math.Min(ItemCount - 1, Math.Max(0, e.X * ItemCount / Math.Max(1, Width)));
            Focus();
            base.OnMouseClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up)
            {
                SelectedIndex = Math.Max(0, SelectedIndex - 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down)
            {
                SelectedIndex = Math.Min(ItemCount - 1, SelectedIndex + 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                SelectedIndex = (SelectedIndex + 1) % ItemCount;
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

            int count = ItemCount;
            int itemWidth = Width / count;
            for (int index = 0; index < count; index++)
            {
                int itemLeft = index * itemWidth + 3;
                int itemRight = index == count - 1 ? Width - 3 : (index + 1) * itemWidth - 2;
                Rectangle item = new Rectangle(itemLeft, 3, Math.Max(1, itemRight - itemLeft), Height - 7);
                Color fill = index == selectedIndex
                    ? UiTheme.Accent
                    : index == hoveredIndex ? UiTheme.SurfaceRaised : UiTheme.SurfaceMuted;
                using (GraphicsPath itemPath = RoundedPanel.CreateRoundedRect(item, 6))
                using (SolidBrush itemBrush = new SolidBrush(fill))
                    e.Graphics.FillPath(itemBrush, itemPath);

                TextRenderer.DrawText(
                    e.Graphics,
                    ItemText(index),
                    Font,
                    item,
                    index == selectedIndex ? Color.White : UiTheme.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }

            if (Focused && ShowFocusCues)
            {
                using (GraphicsPath focusPath = RoundedPanel.CreateRoundedRect(Rectangle.Inflate(bounds, -2, -2), 6))
                using (Pen focusPen = new Pen(UiTheme.Accent, 2))
                    e.Graphics.DrawPath(focusPen, focusPath);
            }
        }

        private string ItemText(int index)
        {
            if (index == 0) return FirstText;
            if (index == 1) return SecondText;
            return ThirdText;
        }
    }

    internal sealed class RoundedTextBox : UserControl
    {
        private readonly TextBox input;

        public new event KeyEventHandler KeyDown
        {
            add { input.KeyDown += value; }
            remove { input.KeyDown -= value; }
        }

        public override string Text
        {
            get { return input == null ? base.Text : input.Text; }
            set
            {
                base.Text = value;
                if (input != null)
                    input.Text = value;
            }
        }

        public RoundedTextBox()
        {
            SetStyle(ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            TabStop = false;

            input = new TextBox();
            input.BorderStyle = BorderStyle.None;
            input.BackColor = UiTheme.SurfaceRaised;
            input.ForeColor = UiTheme.Text;
            input.Font = new Font("Consolas", 9.5F);
            input.TabStop = true;
            input.GotFocus += delegate { Invalidate(); };
            input.LostFocus += delegate { Invalidate(); };
            Controls.Add(input);
            Height = 32;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int inputHeight = input == null ? 20 : input.PreferredHeight;
            if (input != null)
                input.SetBounds(11, Math.Max(1, (Height - inputHeight) / 2), Math.Max(1, Width - 22), inputHeight);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            input.Focus();
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedPanel.CreateRoundedRect(bounds, 8))
            using (SolidBrush fill = new SolidBrush(UiTheme.SurfaceRaised))
            using (Pen pen = new Pen(input.Focused ? UiTheme.Accent : UiTheme.Border, input.Focused ? 2 : 1))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
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
            Rectangle well = new Rectangle(1, 3, Width - 3, Width - 3);
            using (SolidBrush wellBrush = new SolidBrush(Color.FromArgb(232, 242, 255)))
                e.Graphics.FillEllipse(wellBrush, well);
            using (Pen pen = new Pen(UiTheme.Accent, 2.2F))
            using (SolidBrush dot = new SolidBrush(UiTheme.Accent))
            {
                Rectangle phone = new Rectangle(17, 12, Width - 35, Height - 22);
                using (GraphicsPath path = RoundedPanel.CreateRoundedRect(phone, 6))
                    e.Graphics.DrawPath(pen, path);
                e.Graphics.DrawLine(pen, phone.Left + 6, phone.Top + 5, phone.Right - 6, phone.Top + 5);
                e.Graphics.FillEllipse(dot, phone.Left + (phone.Width / 2) - 1.5F, phone.Bottom - 5, 3, 3);
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

    internal sealed class DeviceSelectRow : Control
    {
        private DeviceCard device;
        private bool hovered;

        public event EventHandler DeviceChosen;
        public bool Selected;
        public bool ShowsChevron;

        public DeviceCard Device
        {
            get { return device; }
            set
            {
                device = value;
                UpdateAccessibility();
                Invalidate();
            }
        }

        public DeviceSelectRow()
        {
            SetStyle(ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.ListItem;
            Font = new Font(UiTheme.FontFamily, 9F, FontStyle.Regular);
        }

        public void UpdateStatus()
        {
            UpdateAccessibility();
            Invalidate();
        }

        private void UpdateAccessibility()
        {
            if (device == null)
                return;
            AccessibleName = device.Name;
            string state = device.MirrorStarting ? "正在启动投屏" :
                device.MirrorStopping ? "正在停止投屏" :
                device.MirrorActive ? "正在投屏" :
                device.IsOnline ? "USB 已连接" : "未连接";
            AccessibleDescription = device.Model + "，" + state;
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
            Focus();
            EventHandler handler = DeviceChosen;
            if (handler != null)
                handler(this, EventArgs.Empty);
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space ||
                (ShowsChevron && (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)))
            {
                OnClick(EventArgs.Empty);
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
            Color fill = Selected && !ShowsChevron
                ? (UiTheme.IsDark ? Color.FromArgb(38, 72, 112) : Color.FromArgb(238, 246, 255))
                : hovered ? UiTheme.SurfaceRaised : UiTheme.Surface;
            using (GraphicsPath path = RoundedPanel.CreateRoundedRect(bounds, 12))
            using (SolidBrush fillBrush = new SolidBrush(fill))
            {
                e.Graphics.FillPath(fillBrush, path);
                if (ShowsChevron)
                {
                    using (Pen borderPen = new Pen(UiTheme.Border, 1))
                        e.Graphics.DrawPath(borderPen, path);
                }
            }

            if (device == null)
                return;

            Rectangle phone = new Rectangle(18, (Height - 30) / 2, 18, 30);
            using (Pen phonePen = new Pen(device.IsOnline ? UiTheme.Accent : UiTheme.TextDim, 1.8F))
            using (GraphicsPath phonePath = RoundedPanel.CreateRoundedRect(phone, 5))
            {
                phonePen.StartCap = LineCap.Round;
                phonePen.EndCap = LineCap.Round;
                e.Graphics.DrawPath(phonePen, phonePath);
                e.Graphics.DrawLine(phonePen, phone.Left + 6, phone.Bottom - 5, phone.Right - 6, phone.Bottom - 5);
            }

            int textRightPadding = ShowsChevron ? 130 : 105;
            Rectangle nameBounds = new Rectangle(52, 10, Math.Max(80, Width - 52 - textRightPadding), 24);
            Rectangle modelBounds = new Rectangle(52, 34, Math.Max(80, Width - 52 - textRightPadding), 20);
            using (Font nameFont = new Font(UiTheme.FontFamily, ShowsChevron ? 11F : 9.5F, FontStyle.Bold))
            {
                TextRenderer.DrawText(e.Graphics, device.Name, nameFont, nameBounds, UiTheme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            TextRenderer.DrawText(e.Graphics, device.Model, Font, modelBounds, UiTheme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            string status = device.MirrorStarting ? "连接中" :
                device.MirrorStopping ? "停止中" :
                device.MirrorActive ? "投屏中" :
                device.IsOnline ? "已连接" : "未连接";
            Color statusColor = device.MirrorActive || device.MirrorStarting
                ? UiTheme.Accent
                : device.IsOnline ? UiTheme.Success : UiTheme.TextDim;
            int statusRight = ShowsChevron ? Width - 46 : Selected ? Width - 48 : Width - 18;
            int statusWidth = 70;
            Rectangle statusBounds = new Rectangle(statusRight - statusWidth, 0, statusWidth, Height);
            TextRenderer.DrawText(e.Graphics, status, Font, statusBounds,
                statusColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            using (SolidBrush dot = new SolidBrush(statusColor))
                e.Graphics.FillEllipse(dot, statusBounds.Left - 10, (Height - 7) / 2, 7, 7);

            if (ShowsChevron)
            {
                UiIconRenderer.Draw(e.Graphics, UiIcon.ChevronDown,
                    new Rectangle(Width - 34, (Height - 18) / 2, 18, 18), UiTheme.TextMuted);
            }
            else if (Selected)
            {
                UiIconRenderer.Draw(e.Graphics, UiIcon.Check,
                    new Rectangle(Width - 31, (Height - 18) / 2, 18, 18), UiTheme.Accent);
            }

            if (Focused && ShowFocusCues)
            {
                using (GraphicsPath focusPath = RoundedPanel.CreateRoundedRect(Rectangle.Inflate(bounds, -2, -2), 10))
                using (Pen focusPen = new Pen(UiTheme.Accent, 2))
                    e.Graphics.DrawPath(focusPen, focusPath);
            }
        }
    }

    internal sealed class DeviceCard
    {
        public string Name;
        public string Model;
        public string Serial;
        public bool IsOnline;
        public bool MirrorActive;
        public int WindowX;
        public Process MirrorProcess;
        public int MirrorProcessId;
        public bool MirrorStarting;
        public bool MirrorStopping;
        public MirrorSessionState SessionState;
        public int SessionGeneration;
        public int RecoveryRequestId;
        public int RecoveryAttempts;
        public bool StopRequested;
        public DateTime RunningSinceUtc;
        public int ScreenPowerRequestId;
        public bool ScreenDesiredOff;
        public bool ScreenOffApplied;
        public int TopMostRequestId;
    }

    internal sealed class RoundedPanel : Panel
    {
        public Color BorderColor = UiTheme.Border;
        public int CornerRadius = 18;
        public bool Shadow;

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
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Color parentColor = Parent == null ? UiTheme.Background : Parent.BackColor;
            e.Graphics.Clear(parentColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = Shadow
                ? new Rectangle(1, 1, Width - 6, Height - 7)
                : new Rectangle(0, 0, Width - 1, Height - 1);

            if (Shadow)
            {
                Rectangle shadowBounds = new Rectangle(r.X + 2, r.Y + 3, r.Width, r.Height);
                using (GraphicsPath shadowPath = CreateRoundedRect(shadowBounds, CornerRadius))
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(20, 0, 0, 0)))
                    e.Graphics.FillPath(shadowBrush, shadowPath);
            }

            using (GraphicsPath path = CreateRoundedRect(r, CornerRadius))
            using (SolidBrush fill = new SolidBrush(BackColor))
            using (Pen pen = new Pen(BorderColor, 1))
            {
                e.Graphics.FillPath(fill, path);
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

    internal sealed class DevicePickerForm : Form
    {
        public DevicePickerForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = UiTheme.Surface;
            ClientSize = new Size(532, 128);
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Hide();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ClassStyle |= 0x00020000;
                parameters.ExStyle |= 0x08000000 | 0x00000080;
                return parameters;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width < 2 || Height < 2)
                return;
            Region previous = Region;
            using (GraphicsPath path = RoundedPanel.CreateRoundedRect(
                new Rectangle(0, 0, Width, Height), 16))
                Region = new Region(path);
            if (previous != null)
                previous.Dispose();
        }

        public void ShowFor(Control anchor, IWin32Window owner)
        {
            Point target = anchor.PointToScreen(new Point(0, anchor.Height + 6));
            Rectangle working = Screen.FromControl(anchor).WorkingArea;
            int x = Math.Max(working.Left + 8, Math.Min(target.X, working.Right - Width - 8));
            int y = target.Y + Height <= working.Bottom - 8
                ? target.Y
                : anchor.PointToScreen(new Point(0, -Height - 6)).Y;
            Location = new Point(x, y);
            BackColor = UiTheme.Surface;
            WindowTheme.Apply(this);
            Show(owner);
            BringToFront();
        }
    }

    internal sealed class MainForm : Form
    {
        private Color background { get { return UiTheme.Background; } }
        private Color card { get { return UiTheme.Surface; } }
        private Color foreground { get { return UiTheme.Text; } }
        private Color muted { get { return UiTheme.TextMuted; } }
        private Color accent { get { return UiTheme.Accent; } }

        private readonly string appDirectory;
        private readonly string adbPath;
        private readonly string scrcpyPath;
        private readonly DeviceCard device1;
        private readonly DeviceCard device2;
        private DeviceCard activeDevice;
        private readonly Timer refreshTimer;
        private readonly Timer restartTimer;
        private readonly Label footerStatus;
        private readonly ModeSelector keyboardModeSelector;
        private readonly ModeSelector themeModeSelector;
        private readonly ToggleSwitch screenOffToggle;
        private readonly ToggleSwitch alwaysOnTopToggle;
        private readonly DeviceSelectRow activeDeviceRow;
        private readonly DeviceSelectRow device1Row;
        private readonly DeviceSelectRow device2Row;
        private readonly DevicePickerForm devicePicker;
        private readonly PickerDismissFilter pickerDismissFilter;
        private readonly RoundedPanel settingsPanel;
        private readonly ModernButton launchButton;
        private readonly ModernButton transferButton;
        private readonly ToolTip toolTip;
        private readonly string settingsPath;
        private readonly string screenOffSettingsPath;
        private readonly string alwaysOnTopSettingsPath;
        private readonly string selectedDeviceSettingsPath;
        private readonly string themeSettingsPath;
        private readonly List<DeviceCard> pendingRestart = new List<DeviceCard>();
        private readonly KeyboardCapture keyboardCapture;
        private int keyboardMode;
        private AppThemeMode themeMode;
        private int refreshInProgress;
        private string lastAdbProbeDetail = string.Empty;
        private static readonly object screenPowerInputGate = new object();

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
            selectedDeviceSettingsPath = Path.Combine(
                Path.GetDirectoryName(settingsPath),
                "selected-device.txt");
            themeSettingsPath = Path.Combine(
                Path.GetDirectoryName(settingsPath),
                "theme-mode.txt");
            keyboardMode = LoadKeyboardMode();
            themeMode = LoadThemeMode();
            UiTheme.SetDark(ThemeShouldBeDark());
            toolTip = new ToolTip();
            toolTip.AutoPopDelay = 5000;
            toolTip.InitialDelay = 450;
            toolTip.ReshowDelay = 100;

            Text = "OPhoneMirror · 手机有线投屏";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(620, 520);
            MinimumSize = new Size(636, 559);
            BackColor = background;
            ForeColor = foreground;
            Font = new Font(UiTheme.FontFamily, 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            KeyPreview = true;

            Label title = new Label();
            title.Text = "OPhoneMirror";
            title.Font = new Font(UiTheme.DisplayFontFamily, 21F, FontStyle.Bold);
            title.ForeColor = foreground;
            title.AutoSize = true;
            title.Location = new Point(28, 18);
            Controls.Add(title);

            ModernButton refresh = MakeIconButton(UiIcon.Refresh, "刷新设备", UiButtonKind.Secondary);
            refresh.Location = new Point(552, 18);
            refresh.Size = new Size(40, 40);
            refresh.CornerRadius = 20;
            refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refresh.Click += delegate { RefreshDevices(); };
            Controls.Add(refresh);
            toolTip.SetToolTip(refresh, "刷新设备");

            List<DeviceDefinition> definitions = LoadDeviceDefinitions();
            device1 = CreateDevice(definitions[0]);
            device2 = CreateDevice(definitions[1]);
            activeDevice = LoadSelectedDeviceIndex() == 1 ? device2 : device1;

            RoundedPanel devicePanel = new RoundedPanel();
            devicePanel.Name = "activeDevicePanel";
            devicePanel.BackColor = card;
            devicePanel.BorderColor = UiTheme.Border;
            devicePanel.Shadow = false;
            devicePanel.Location = new Point(28, 70);
            devicePanel.Size = new Size(564, 178);
            Controls.Add(devicePanel);

            activeDeviceRow = new DeviceSelectRow();
            activeDeviceRow.Name = "activeDeviceSelector";
            activeDeviceRow.Device = activeDevice;
            activeDeviceRow.ShowsChevron = true;
            activeDeviceRow.Location = new Point(16, 14);
            activeDeviceRow.Size = new Size(532, 64);
            activeDeviceRow.DeviceChosen += delegate { ToggleDeviceList(); };
            devicePanel.Controls.Add(activeDeviceRow);
            toolTip.SetToolTip(activeDeviceRow, "选择设备");

            Panel deviceDivider = new Panel();
            deviceDivider.BackColor = UiTheme.Border;
            deviceDivider.Location = new Point(24, 88);
            deviceDivider.Size = new Size(516, 1);
            devicePanel.Controls.Add(deviceDivider);

            launchButton = MakeActionButton(UiIcon.Mirror, "投屏", UiButtonKind.Primary);
            launchButton.Location = new Point(148, 105);
            launchButton.Size = new Size(126, 48);
            launchButton.CornerRadius = 14;
            launchButton.Enabled = false;
            launchButton.Click += delegate
            {
                CloseDeviceList();
                ToggleMirror(activeDevice);
            };
            devicePanel.Controls.Add(launchButton);
            toolTip.SetToolTip(launchButton, "启动有线投屏");

            transferButton = MakeActionButton(UiIcon.Transfer, "文件", UiButtonKind.Secondary);
            transferButton.Location = new Point(290, 105);
            transferButton.Size = new Size(126, 48);
            transferButton.CornerRadius = 14;
            transferButton.Enabled = false;
            transferButton.Click += delegate
            {
                CloseDeviceList();
                OpenTransfer(activeDevice);
            };
            devicePanel.Controls.Add(transferButton);
            toolTip.SetToolTip(transferButton, "文件互传");

            settingsPanel = new RoundedPanel();
            RoundedPanel info = settingsPanel;
            info.Name = "optionsPanel";
            info.BackColor = card;
            info.BorderColor = UiTheme.Border;
            info.Shadow = false;
            info.Location = new Point(28, 264);
            info.Size = new Size(564, 208);
            info.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            Label infoTitle = new Label();
            infoTitle.Text = "设置";
            infoTitle.ForeColor = foreground;
            infoTitle.Font = new Font(UiTheme.FontFamily, 11F, FontStyle.Bold);
            infoTitle.AutoSize = true;
            infoTitle.Location = new Point(22, 13);
            info.Controls.Add(infoTitle);

            screenOffToggle = new ToggleSwitch();
            screenOffToggle.Text = "仅熄手机屏幕";
            screenOffToggle.Location = new Point(18, 39);
            screenOffToggle.Size = new Size(528, 38);
            screenOffToggle.Checked = LoadScreenOffSetting();
            screenOffToggle.CheckedChanged += ScreenOffSettingChanged;
            info.Controls.Add(screenOffToggle);
            toolTip.SetToolTip(screenOffToggle, "关闭手机实体屏幕，投屏保持运行");

            alwaysOnTopToggle = new ToggleSwitch();
            alwaysOnTopToggle.Text = "保持在最顶层";
            alwaysOnTopToggle.Location = new Point(18, 79);
            alwaysOnTopToggle.Size = new Size(528, 38);
            alwaysOnTopToggle.Checked = LoadAlwaysOnTopSetting();
            alwaysOnTopToggle.CheckedChanged += AlwaysOnTopSettingChanged;
            info.Controls.Add(alwaysOnTopToggle);
            toolTip.SetToolTip(alwaysOnTopToggle, "让投屏窗口保持在其他窗口上方");

            for (int dividerIndex = 0; dividerIndex < 3; dividerIndex++)
            {
                Panel divider = new Panel();
                divider.BackColor = UiTheme.Border;
                divider.Location = new Point(22, 78 + dividerIndex * 40);
                divider.Size = new Size(520, 1);
                info.Controls.Add(divider);
            }

            Label keyboardLabel = new Label();
            keyboardLabel.Text = "键盘";
            keyboardLabel.ForeColor = muted;
            keyboardLabel.AutoSize = true;
            keyboardLabel.Location = new Point(24, 131);
            info.Controls.Add(keyboardLabel);

            keyboardModeSelector = new ModeSelector();
            keyboardModeSelector.FirstText = "短语";
            keyboardModeSelector.SecondText = "数字选词";
            keyboardModeSelector.Location = new Point(294, 120);
            keyboardModeSelector.Size = new Size(252, 34);
            keyboardModeSelector.SelectedIndex = keyboardMode;
            keyboardModeSelector.SelectedIndexChanged += KeyboardModeChanged;
            info.Controls.Add(keyboardModeSelector);
            toolTip.SetToolTip(keyboardModeSelector, "短语：Shift 切换中英；数字选词：Shift+Space 切换中英");

            Label themeLabel = new Label();
            themeLabel.Text = "外观";
            themeLabel.ForeColor = muted;
            themeLabel.AutoSize = true;
            themeLabel.Location = new Point(24, 171);
            info.Controls.Add(themeLabel);

            themeModeSelector = new ModeSelector();
            themeModeSelector.AccessibleName = "外观主题";
            themeModeSelector.FirstText = "自动";
            themeModeSelector.SecondText = "浅色";
            themeModeSelector.ThirdText = "深色";
            themeModeSelector.Location = new Point(294, 160);
            themeModeSelector.Size = new Size(252, 34);
            themeModeSelector.SelectedIndex = (int)themeMode;
            themeModeSelector.SelectedIndexChanged += ThemeModeChanged;
            info.Controls.Add(themeModeSelector);
            Controls.Add(info);

            devicePicker = new DevicePickerForm();
            devicePicker.Size = new Size(532, 128);
            pickerDismissFilter = new PickerDismissFilter(this);
            Application.AddMessageFilter(pickerDismissFilter);

            device1Row = new DeviceSelectRow();
            device1Row.Device = device1;
            device1Row.Selected = activeDevice == device1;
            device1Row.Location = new Point(8, 7);
            device1Row.Size = new Size(516, 55);
            device1Row.DeviceChosen += delegate { SelectDevice(device1); };
            devicePicker.Controls.Add(device1Row);

            device2Row = new DeviceSelectRow();
            device2Row.Device = device2;
            device2Row.Selected = activeDevice == device2;
            device2Row.Location = new Point(8, 66);
            device2Row.Size = new Size(516, 55);
            device2Row.DeviceChosen += delegate { SelectDevice(device2); };
            devicePicker.Controls.Add(device2Row);

            footerStatus = new Label();
            footerStatus.Text = "正在检查设备…";
            footerStatus.ForeColor = muted;
            footerStatus.AutoSize = true;
            footerStatus.Location = new Point(32, 493);
            footerStatus.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            Controls.Add(footerStatus);

            Label version = new Label();
            version.Text = "v1.13.0";
            version.ForeColor = muted;
            version.AutoSize = false;
            version.Location = new Point(512, 488);
            version.Size = new Size(80, 24);
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
                UiTheme.ThemeChanged -= ApplyTheme;
                Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemThemeChanged;
                devicePicker.Close();
                devicePicker.Dispose();
                Application.RemoveMessageFilter(pickerDismissFilter);
                keyboardCapture.Dispose();
                toolTip.Dispose();
            };
            Shown += delegate
            {
                WindowTheme.Apply(this);
                RefreshDevices();
                AdoptExistingMirrorsAsync();
                refreshTimer.Start();
            };
            Deactivate += delegate { CloseDeviceList(); };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape && devicePicker.Visible)
                {
                    CloseDeviceList();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (devicePicker.Visible && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
                {
                    SelectDevice(activeDevice == device1 ? device2 : device1);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            UiTheme.ThemeChanged += ApplyTheme;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemThemeChanged;
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

        private DeviceCard CreateDevice(DeviceDefinition definition)
        {
            DeviceCard device = new DeviceCard();
            device.Name = definition.Name;
            device.Model = definition.Model;
            device.Serial = definition.Serial;
            device.WindowX = definition.WindowX;
            return device;
        }

        internal bool StressScreenOffSetting { get { return screenOffToggle.Checked; } }
        internal bool StressTopMostSetting { get { return alwaysOnTopToggle.Checked; } }
        internal int StressKeyboardSetting { get { return keyboardMode; } }
        internal int StressActiveDeviceIndex { get { return activeDevice == device2 ? 1 : 0; } }

        internal bool StressPrepare(int deviceIndex)
        {
            DeviceCard requested = deviceIndex == 1 ? device2 : device1;
            SelectDevice(requested);
            RefreshDevices();
            bool online = false;
            for (int attempt = 0; attempt < 5 && !online; attempt++)
            {
                online = IsUsbDeviceOnline(requested.Serial);
                if (!online)
                    System.Threading.Thread.Sleep(500);
            }
            requested.IsOnline = online;
            Diagnostics.Trace("stress", "prepare", "configured=" + HasConfiguredDevices + " online=" + online + " detail=" + lastAdbProbeDetail);
            return online;
        }

        internal bool StressMirrorIsRunning()
        {
            return activeDevice.MirrorActive || FindMirrorProcesses(activeDevice).Count > 0;
        }

        internal int StressMirrorProcessCount()
        {
            return FindMirrorProcesses(activeDevice).Count;
        }

        internal void StressStartOrStopMirror()
        {
            ToggleMirror(activeDevice);
        }

        internal void StressToggleScreenPower()
        {
            screenOffToggle.Checked = !screenOffToggle.Checked;
        }

        internal void StressTogglePicker()
        {
            if (devicePicker.Visible)
                CloseDeviceList();
            else
                ToggleDeviceList();
        }

        internal void StressClosePicker()
        {
            CloseDeviceList();
        }

        internal void StressToggleKeyboardMode()
        {
            keyboardModeSelector.SelectedIndex = keyboardMode == 0 ? 1 : 0;
        }

        internal void StressToggleTopMost()
        {
            alwaysOnTopToggle.Checked = !alwaysOnTopToggle.Checked;
        }

        internal void StressNavigatePhone(int keyCode)
        {
            DeviceCard target = activeDevice;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                new AdbClient(adbPath, target.Serial).Shell("input keyevent " + keyCode, 5000);
            });
        }

        internal void StressRestore(bool screenOff, bool topMost, int keyboard, int deviceIndex)
        {
            screenOffToggle.Checked = screenOff;
            alwaysOnTopToggle.Checked = topMost;
            if (keyboardModeSelector.SelectedIndex != keyboard)
                keyboardModeSelector.SelectedIndex = keyboard;
            SelectDevice(deviceIndex == 1 ? device2 : device1);
        }

        private ModernButton MakeIconButton(UiIcon icon, string accessibleName, UiButtonKind kind)
        {
            ModernButton b = new ModernButton();
            b.Text = string.Empty;
            b.AccessibleName = accessibleName;
            b.Icon = icon;
            b.IconOnly = true;
            b.IconSize = 22;
            b.Kind = kind;
            b.BackColor = kind == UiButtonKind.Primary ? accent :
                kind == UiButtonKind.Danger ? UiTheme.Danger : card;
            b.ForeColor = kind == UiButtonKind.Primary || kind == UiButtonKind.Danger
                ? Color.White : foreground;
            b.Font = new Font(UiTheme.FontFamily, 9F, FontStyle.Regular);
            return b;
        }

        private ModernButton MakeActionButton(UiIcon icon, string text, UiButtonKind kind)
        {
            ModernButton button = MakeIconButton(icon, text, kind);
            button.IconOnly = false;
            button.Text = text;
            button.Font = new Font(UiTheme.FontFamily, 9.5F, FontStyle.Bold);
            return button;
        }

        private void ToggleDeviceList()
        {
            if (devicePicker.Visible)
            {
                CloseDeviceList();
                return;
            }
            activeDeviceRow.AccessibleDescription = activeDevice.Model + "，" +
                (activeDevice.IsOnline ? "USB 已连接" : "未连接") + "，" +
                "设备列表已展开";
            devicePicker.ShowFor(activeDeviceRow, this);
        }

        private void CloseDeviceList()
        {
            if (!devicePicker.Visible)
                return;
            devicePicker.Hide();
            activeDeviceRow.AccessibleDescription = activeDevice.Model + "，" +
                (activeDevice.IsOnline ? "USB 已连接" : "未连接") + "，设备列表已折叠";
        }

        private void SelectDevice(DeviceCard device)
        {
            activeDevice = device;
            activeDeviceRow.Device = device;
            device1Row.Selected = device == device1;
            device2Row.Selected = device == device2;
            device1Row.Invalidate();
            device2Row.Invalidate();
            UpdateMirrorAction(device);
            transferButton.Enabled = device.IsOnline;
            SaveSelectedDeviceIndex(device == device2 ? 1 : 0);
            devicePicker.Hide();
            footerStatus.Text = DeviceStatusText(device);
            activeDeviceRow.Focus();
        }

        // The picker deliberately never activates, so it cannot make the main panel
        // lose focus. This filter supplies the missing "click elsewhere closes it"
        // behavior without stealing focus from the owner window.
        private sealed class PickerDismissFilter : IMessageFilter
        {
            private const int WmLButtonDown = 0x0201;
            private const int WmNCLButtonDown = 0x00A1;
            private readonly MainForm owner;

            public PickerDismissFilter(MainForm owner)
            {
                this.owner = owner;
            }

            public bool PreFilterMessage(ref Message message)
            {
                if (!owner.devicePicker.Visible ||
                    (message.Msg != WmLButtonDown && message.Msg != WmNCLButtonDown))
                    return false;

                Control clicked = Control.FromHandle(message.HWnd);
                if (!IsWithin(clicked, owner.devicePicker))
                {
                    try { owner.BeginInvoke((MethodInvoker)owner.CloseDeviceList); }
                    catch { }
                }
                return false;
            }

            private static bool IsWithin(Control control, Control root)
            {
                for (Control current = control; current != null; current = current.Parent)
                {
                    if (current == root)
                        return true;
                }
                return false;
            }
        }

        private int LoadSelectedDeviceIndex()
        {
            try
            {
                return File.ReadAllText(selectedDeviceSettingsPath).Trim() == "1" ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private void SaveSelectedDeviceIndex(int index)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(selectedDeviceSettingsPath));
                File.WriteAllText(selectedDeviceSettingsPath, index == 1 ? "1" : "0", Encoding.UTF8);
            }
            catch
            {
                // The selector still works for this session if settings cannot be persisted.
            }
        }

        private AppThemeMode LoadThemeMode()
        {
            try
            {
                string value = File.ReadAllText(themeSettingsPath).Trim();
                if (value == "1") return AppThemeMode.Light;
                if (value == "2") return AppThemeMode.Dark;
            }
            catch { }
            return AppThemeMode.Auto;
        }

        private void SaveThemeMode()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(themeSettingsPath));
                File.WriteAllText(themeSettingsPath, ((int)themeMode).ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private bool ThemeShouldBeDark()
        {
            if (themeMode == AppThemeMode.Dark) return true;
            if (themeMode == AppThemeMode.Light) return false;
            return UiTheme.SystemUsesDarkTheme();
        }

        private void ThemeModeChanged(object sender, EventArgs e)
        {
            themeMode = (AppThemeMode)themeModeSelector.SelectedIndex;
            SaveThemeMode();
            UiTheme.SetDark(ThemeShouldBeDark());
            footerStatus.Text = "外观已切换为“" + themeModeSelector.AccessibleDescription + "”";
        }

        private void SystemThemeChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (themeMode != AppThemeMode.Auto || IsDisposed)
                return;
            try
            {
                BeginInvoke((MethodInvoker)delegate { UiTheme.SetDark(ThemeShouldBeDark()); });
            }
            catch { }
        }

        private void ApplyTheme(ThemePalette previous)
        {
            UiTheme.ApplyControlTree(this, previous);
            UiTheme.ApplyControlTree(devicePicker, previous);
            BackColor = UiTheme.Background;
            ForeColor = UiTheme.Text;
            settingsPanel.BorderColor = UiTheme.Border;
            devicePicker.BackColor = UiTheme.Surface;
            WindowTheme.Apply(this);
            if (devicePicker.Visible)
                WindowTheme.Apply(devicePicker);
            Invalidate(true);
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
                // The first query after an ADB-server restart can take longer than a
                // normal refresh. Do not classify an otherwise connected phone as
                // absent just because its server is warming up.
                if (!p.WaitForExit(5000))
                {
                    p.Kill();
                    return false;
                }
                string output = p.StandardOutput.ReadToEnd().Trim();
                string error = p.StandardError.ReadToEnd().Trim();
                lastAdbProbeDetail = "exit=" + p.ExitCode + " output=" + output + " error=" + error;
                return p.ExitCode == 0 && output == "device";
            }
            catch (Exception ex)
            {
                lastAdbProbeDetail = ex.GetType().Name;
                return false;
            }
        }

        private void SetDeviceState(DeviceCard device, bool connected)
        {
            device.IsOnline = connected;
            UpdateDeviceRows(device);
            if (device == activeDevice)
            {
                UpdateMirrorAction(device);
                transferButton.Enabled = connected;
            }
        }

        private string DeviceStatusText(DeviceCard device)
        {
            if (device.MirrorStarting) return device.Name + " 正在启动投屏…";
            if (device.MirrorStopping) return device.Name + " 正在停止投屏…";
            if (device.MirrorActive) return device.Name + " 正在投屏";
            return device.Name + (device.IsOnline ? " 已就绪" : " 未连接");
        }

        private void UpdateDeviceRows(DeviceCard device)
        {
            if (device == device1)
                device1Row.UpdateStatus();
            else if (device == device2)
                device2Row.UpdateStatus();
            if (device == activeDevice)
                activeDeviceRow.UpdateStatus();
        }

        private void UpdateMirrorAction(DeviceCard device)
        {
            if (device != activeDevice)
                return;

            if (device.MirrorStarting)
            {
                launchButton.Text = "连接中";
                launchButton.Icon = UiIcon.Refresh;
                launchButton.Kind = UiButtonKind.Primary;
                launchButton.Enabled = false;
                launchButton.AnimateIcon = true;
                launchButton.AccessibleName = "正在启动投屏";
            }
            else if (device.MirrorStopping)
            {
                launchButton.Text = "停止中";
                launchButton.Icon = UiIcon.Stop;
                launchButton.Kind = UiButtonKind.Secondary;
                launchButton.Enabled = false;
                launchButton.AnimateIcon = false;
                launchButton.AccessibleName = "正在停止投屏";
            }
            else if (device.MirrorActive)
            {
                launchButton.Text = "停止";
                launchButton.Icon = UiIcon.Stop;
                launchButton.Kind = UiButtonKind.Secondary;
                launchButton.Enabled = true;
                launchButton.AnimateIcon = false;
                launchButton.AccessibleName = "停止投屏";
            }
            else
            {
                launchButton.Text = "投屏";
                launchButton.Icon = UiIcon.Mirror;
                launchButton.Kind = UiButtonKind.Primary;
                launchButton.Enabled = device.IsOnline;
                launchButton.AnimateIcon = false;
                launchButton.AccessibleName = "启动有线投屏";
            }
            launchButton.BackColor = launchButton.Kind == UiButtonKind.Primary ? UiTheme.Accent : UiTheme.Surface;
            launchButton.ForeColor = launchButton.Kind == UiButtonKind.Primary ? Color.White : UiTheme.Text;
            launchButton.Invalidate();
            UpdateDeviceRows(device);
        }

        private void ToggleMirror(DeviceCard device)
        {
            if (device.MirrorStarting || device.MirrorStopping)
                return;

            List<Process> running = FindMirrorProcesses(device);
            if (running.Count > 0 && !device.MirrorActive)
            {
                Process adopted = running[0];
                for (int i = 1; i < running.Count; i++)
                    running[i].Dispose();
                AttachMirrorProcess(device, adopted, true);
                footerStatus.Text = device.Name + " 已在投屏；再次点击可停止";
                return;
            }

            if (device.MirrorActive || running.Count > 0)
            {
                StopDeviceMirror(device, running);
                return;
            }

            LaunchDevice(device);
        }

        private void StopDeviceMirror(DeviceCard device, List<Process> running)
        {
            device.MirrorStarting = false;
            device.MirrorStopping = true;
            device.SessionState = MirrorSessionState.Stopping;
            device.StopRequested = true;
            Diagnostics.Trace(device.Name, "mirror-stop-requested", "manual");
            UpdateMirrorAction(device);
            footerStatus.Text = device.Name + " 正在停止投屏…";
            StopScreenControl(device, true);
            System.Threading.Interlocked.Increment(ref device.TopMostRequestId);

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                StopMirrorProcesses(running, false);
                System.Threading.Thread.Sleep(350);
                StopMirrorProcesses(FindMirrorProcesses(device), true);
                if (IsDisposed)
                    return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        device.MirrorProcess = null;
                        device.MirrorProcessId = 0;
                        device.MirrorActive = false;
                        device.MirrorStopping = false;
                        device.SessionState = MirrorSessionState.Stopped;
                        UpdateMirrorAction(device);
                        footerStatus.Text = device.Name + " 投屏已停止";
                    });
                }
                catch { }
            });
        }

        private void AttachMirrorProcess(DeviceCard device, Process process, bool alreadyRunning)
        {
            int sessionGeneration = ++device.SessionGeneration;
            device.MirrorProcess = process;
            device.MirrorProcessId = process.Id;
            device.MirrorStarting = !alreadyRunning;
            device.MirrorStopping = false;
            device.MirrorActive = alreadyRunning;
            device.SessionState = alreadyRunning ? MirrorSessionState.Running : MirrorSessionState.Starting;
            device.StopRequested = false;
            Diagnostics.Trace(device.Name, "mirror-attached",
                "pid=" + process.Id + " generation=" + sessionGeneration +
                " adopted=" + alreadyRunning);
            try
            {
                process.EnableRaisingEvents = true;
                int processId = process.Id;
                process.Exited += delegate { MirrorProcessExited(device, processId, sessionGeneration); };
            }
            catch { }
            UpdateMirrorAction(device);
        }

        private void MirrorProcessExited(DeviceCard device, int processId, int sessionGeneration)
        {
            if (IsDisposed)
                return;
            int exitCode = -1;
            try { exitCode = device.MirrorProcess == null ? -1 : device.MirrorProcess.ExitCode; }
            catch { }
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (device.MirrorProcessId != processId || device.SessionGeneration != sessionGeneration)
                        return;
                    bool expected = device.StopRequested || device.MirrorStopping ||
                        device.SessionState == MirrorSessionState.Stopping;
                    Diagnostics.Trace(device.Name, "mirror-exited",
                        "pid=" + processId + " code=" + exitCode + " expected=" + expected);
                    device.MirrorProcess = null;
                    device.MirrorProcessId = 0;
                    device.MirrorActive = false;
                    device.MirrorStarting = false;
                    device.MirrorStopping = false;
                    device.ScreenOffApplied = false;
                    if (expected || exitCode == 0)
                    {
                        device.SessionState = MirrorSessionState.Stopped;
                        device.StopRequested = false;
                        UpdateMirrorAction(device);
                        footerStatus.Text = device.Name + " 投屏已结束";
                        return;
                    }

                    Diagnostics.FlushUnexpectedExit(device.Name, device.Serial, exitCode);
                    ScheduleRecovery(device, sessionGeneration, exitCode);
                });
            }
            catch { }
        }

        private void WatchMirrorStarted(DeviceCard device, Process process, int sessionGeneration)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                bool ready = false;
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    try
                    {
                        if (process.HasExited)
                            break;
                        process.Refresh();
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            ready = true;
                            break;
                        }
                    }
                    catch { break; }
                    System.Threading.Thread.Sleep(100);
                }
                if (!ready || IsDisposed)
                    return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (device.MirrorProcessId != process.Id ||
                            device.SessionGeneration != sessionGeneration || device.StopRequested)
                            return;
                        device.MirrorStarting = false;
                        device.MirrorActive = true;
                        device.SessionState = MirrorSessionState.Running;
                        device.RunningSinceUtc = DateTime.UtcNow;
                        UpdateMirrorAction(device);
                        footerStatus.Text = device.Name + " 正在投屏 · " + KeyboardModeName();
                        ResetRecoveryAfterStableRun(device, sessionGeneration);
                    });
                }
                catch { }
            });
        }

        private void ResetRecoveryAfterStableRun(DeviceCard device, int sessionGeneration)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                System.Threading.Thread.Sleep(30000);
                if (IsDisposed)
                    return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (device.SessionGeneration == sessionGeneration &&
                            device.SessionState == MirrorSessionState.Running)
                        {
                            device.RecoveryAttempts = 0;
                            Diagnostics.Trace(device.Name, "recovery-budget-reset", "stable-30s");
                        }
                    });
                }
                catch { }
            });
        }

        private void ScheduleRecovery(DeviceCard device, int exitedGeneration, int exitCode)
        {
            if (device.RecoveryAttempts >= 2)
            {
                device.SessionState = MirrorSessionState.Stopped;
                device.StopRequested = false;
                UpdateMirrorAction(device);
                footerStatus.Text = device.Name + " 投屏已断开；已停止自动重连";
                Diagnostics.Trace(device.Name, "recovery-exhausted", "code=" + exitCode);
                return;
            }

            int attempt = ++device.RecoveryAttempts;
            int requestId = ++device.RecoveryRequestId;
            int delay = attempt == 1 ? 750 : 2000;
            device.SessionState = MirrorSessionState.Recovering;
            device.MirrorStarting = true;
            device.MirrorStopping = false;
            UpdateMirrorAction(device);
            footerStatus.Text = device.Name + " 连接中；正在自动重连（" + attempt + "/2）";
            Diagnostics.Trace(device.Name, "recovery-scheduled",
                "code=" + exitCode + " attempt=" + attempt + " delay=" + delay);

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                System.Threading.Thread.Sleep(delay);
                if (IsDisposed)
                    return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (device.RecoveryRequestId != requestId ||
                            device.SessionGeneration != exitedGeneration || device.StopRequested)
                            return;
                        if (!IsUsbDeviceOnline(device.Serial))
                        {
                            device.MirrorStarting = false;
                            ScheduleRecovery(device, exitedGeneration, exitCode);
                            return;
                        }
                        LaunchDevice(device);
                    });
                }
                catch { }
            });
        }

        private void AdoptExistingMirrorsAsync()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                AdoptExistingMirror(device1);
                AdoptExistingMirror(device2);
            });
        }

        private void AdoptExistingMirror(DeviceCard device)
        {
            List<Process> running = FindMirrorProcesses(device);
            if (running.Count == 0 || IsDisposed)
                return;
            Process adopted = running[0];
            for (int i = 1; i < running.Count; i++)
                running[i].Dispose();
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (device.MirrorProcess == null || !IsProcessRunning(device.MirrorProcess))
                        AttachMirrorProcess(device, adopted, true);
                    else
                        adopted.Dispose();
                });
            }
            catch { adopted.Dispose(); }
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

            device.MirrorStarting = true;
            device.MirrorStopping = false;
            device.MirrorActive = false;
            UpdateMirrorAction(device);
            footerStatus.Text = device.Name + " 正在启动投屏…";
            try
            {
                int previousDeviceMode = LoadDeviceMode(device.Serial);
                bool normalizeShortPhrase = keyboardMode == 0 && previousDeviceMode == 1;
                string keyboardArg = keyboardMode == 1 ? " --keyboard=uhid" : string.Empty;
                string args = string.Format(
                    "--serial={0} --window-title=\"{1} USB Low Latency\" --video-codec=h264 --max-fps=60 --video-bit-rate=16M --video-buffer=0 --no-audio --shortcut-mod=lalt --window-x={2} --window-y=80 --window-width=450 --window-height=900 -V {4}{3}",
                    device.Serial, device.Name, device.WindowX, keyboardArg,
                    Diagnostics.DebugEnabled ? "debug" : "info");

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = scrcpyPath;
                psi.Arguments = args;
                psi.WorkingDirectory = Path.GetDirectoryName(scrcpyPath);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                Process mirrorProcess = Process.Start(psi);
                mirrorProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Diagnostics.Trace(device.Name, "scrcpy-out", e.Data);
                };
                mirrorProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Diagnostics.Trace(device.Name, "scrcpy-err", e.Data);
                };
                AttachMirrorProcess(device, mirrorProcess, false);
                mirrorProcess.BeginOutputReadLine();
                mirrorProcess.BeginErrorReadLine();
                ApplyTopMostWhenReady(device, mirrorProcess);
                WatchMirrorStarted(device, mirrorProcess, device.SessionGeneration);
                SaveDeviceMode(device.Serial, keyboardMode);
                if (screenOffToggle.Checked)
                    StartScreenControl(device);
                if (normalizeShortPhrase)
                    NormalizeShortPhraseModeAsync(device);
            }
            catch (Exception ex)
            {
                device.MirrorStarting = false;
                device.MirrorActive = false;
                UpdateMirrorAction(device);
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
            device.ScreenDesiredOff = true;
            QueueScreenPower(device);
        }

        private void StopScreenControl(DeviceCard device, bool wakeDevice)
        {
            if (!wakeDevice)
            {
                System.Threading.Interlocked.Increment(ref device.ScreenPowerRequestId);
                return;
            }
            device.ScreenDesiredOff = false;
            QueueScreenPower(device);
        }

        private void QueueScreenPower(DeviceCard device)
        {
            int requestId = System.Threading.Interlocked.Increment(ref device.ScreenPowerRequestId);
            int generation = device.SessionGeneration;
            Diagnostics.Trace(device.Name, "screen-power-request",
                "off=" + device.ScreenDesiredOff + " request=" + requestId + " generation=" + generation);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                System.Threading.Thread.Sleep(150);
                if (IsDisposed || requestId != device.ScreenPowerRequestId)
                    return;
                bool desiredOff = device.ScreenDesiredOff;
                bool sent = SendMirrorScreenPower(device, !desiredOff);
                if (!sent && requestId == device.ScreenPowerRequestId)
                {
                    System.Threading.Thread.Sleep(150);
                    if (!IsDisposed && requestId == device.ScreenPowerRequestId)
                        sent = SendMirrorScreenPower(device, !desiredOff);
                }
                if (IsDisposed || requestId != device.ScreenPowerRequestId ||
                    generation != device.SessionGeneration)
                    return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (requestId != device.ScreenPowerRequestId ||
                            generation != device.SessionGeneration)
                            return;
                        if (sent)
                        {
                            device.ScreenOffApplied = desiredOff;
                            footerStatus.Text = desiredOff
                                ? device.Name + "：已关闭手机实体屏幕，投屏继续运行"
                                : device.Name + "：手机实体屏幕已恢复";
                        }
                        else
                        {
                            footerStatus.Text = device.Name + "：未能切换实体屏幕状态，请重新启动投屏后再试";
                        }
                        Diagnostics.Trace(device.Name, "screen-power-result",
                            "off=" + desiredOff + " sent=" + sent);
                    });
                }
                catch { }
            });
        }

        private bool SendMirrorScreenPower(DeviceCard device, bool turnOn)
        {
            try
            {
                lock (screenPowerInputGate)
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
                    try
                    {
                        System.Threading.Thread.Sleep(35);
                        return SendScreenPowerShortcut(turnOn);
                    }
                    finally
                    {
                        if (previousWindow != IntPtr.Zero && previousWindow != window)
                            SetForegroundWindow(previousWindow);
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool SendScreenPowerShortcut(bool turnOn)
        {
            List<Input> inputs = new List<Input>();
            AddKey(inputs, VirtualKeyLMenu, false);
            if (turnOn)
                AddKey(inputs, VirtualKeyLShift, false);
            AddKey(inputs, VirtualKeyO, false);
            AddKey(inputs, VirtualKeyO, true);
            if (turnOn)
                AddKey(inputs, VirtualKeyLShift, true);
            AddKey(inputs, VirtualKeyLMenu, true);
            try
            {
                return SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(Input))) == inputs.Count;
            }
            finally
            {
                // Always release modifiers. This makes a rapid toggle unable to leave Alt held.
                Input[] release = new Input[2];
                release[0] = CreateKeyInput(VirtualKeyLShift, true);
                release[1] = CreateKeyInput(VirtualKeyLMenu, true);
                SendInput((uint)release.Length, release, Marshal.SizeOf(typeof(Input)));
            }
        }

        private static void AddKey(List<Input> inputs, ushort key, bool keyUp)
        {
            inputs.Add(CreateKeyInput(key, keyUp));
        }

        private static Input CreateKeyInput(ushort key, bool keyUp)
        {
            Input input = new Input();
            input.Type = 1;
            input.Data.Keyboard.VirtualKey = key;
            input.Data.Keyboard.Flags = keyUp ? 0x0002U : 0U;
            return input;
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

            device.StopRequested = true;
            device.SessionState = MirrorSessionState.Stopping;
            device.MirrorStopping = true;
            device.MirrorStarting = false;
            Diagnostics.Trace(device.Name, "mirror-restart-requested", "keyboard-mode");
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, Input[] inputs, int size);

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint Type;
            public InputUnion Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KeyboardInput Keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const ushort VirtualKeyLMenu = 0xA4;
        private const ushort VirtualKeyLShift = 0xA0;
        private const ushort VirtualKeyO = 0x4F;
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

    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--diagnostics=debug", StringComparison.OrdinalIgnoreCase))
                    Diagnostics.Configure(true);
            }

            if (args.Length >= 1 && args[0] == "--stress-model")
            {
                int seed = args.Length >= 2 ? int.Parse(args[1]) : 20260905;
                return StressModel.Run(seed, 100000);
            }

            if (args.Length >= 1 && args[0] == "--stress-live")
            {
                int deviceIndex = args.Length >= 2 ? int.Parse(args[1]) : 0;
                int durationMinutes = args.Length >= 3 ? int.Parse(args[2]) : 10;
                int seed = args.Length >= 4 ? int.Parse(args[3]) : 20260905;
                using (MainForm form = new MainForm())
                {
                    form.Shown += delegate { StressRunner.Start(form, deviceIndex, durationMinutes, seed); };
                    Application.Run(form);
                }
                return StressRunner.LastExitCode;
            }

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
