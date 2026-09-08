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

[assembly: AssemblyTitle("OMirror")]
[assembly: AssemblyProduct("OMirror")]
[assembly: AssemblyVersion("1.16.0.0")]
[assembly: AssemblyFileVersion("1.16.0.0")]

namespace OMirror
{
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
        public const string DisplayFontFamily = "Segoe UI Variable Display Semib";
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

        public static int Scale(Control control, int value)
        {
            using (Graphics graphics = control.CreateGraphics())
                return (int)Math.Round(value * graphics.DpiX / 96F);
        }

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
            p.SurfaceRaised = Color.FromArgb(248, 248, 250);
            p.SurfaceMuted = Color.FromArgb(238, 238, 241);
            p.SurfaceHover = Color.FromArgb(232, 232, 236);
            p.SurfacePressed = Color.FromArgb(218, 218, 223);
            p.Border = Color.FromArgb(229, 229, 233);
            p.BorderStrong = Color.FromArgb(190, 190, 196);
            p.Text = Color.FromArgb(32, 33, 36);
            p.TextMuted = Color.FromArgb(104, 104, 109);
            p.TextDim = Color.FromArgb(113, 113, 120);
            p.Accent = Color.FromArgb(0, 113, 227);
            p.AccentHover = Color.FromArgb(0, 113, 227);
            p.AccentPressed = Color.FromArgb(0, 97, 204);
            p.Success = Color.FromArgb(31, 122, 54);
            p.Offline = Color.FromArgb(113, 113, 120);
            p.Danger = Color.FromArgb(215, 0, 21);
            return p;
        }

        private static ThemePalette CreateDark()
        {
            ThemePalette p = new ThemePalette();
            p.IsDark = true;
            p.Background = Color.FromArgb(32, 32, 35);
            p.Surface = Color.FromArgb(44, 44, 47);
            p.SurfaceRaised = Color.FromArgb(48, 48, 52);
            p.SurfaceMuted = Color.FromArgb(59, 59, 64);
            p.SurfaceHover = Color.FromArgb(82, 82, 85);
            p.SurfacePressed = Color.FromArgb(99, 99, 102);
            p.Border = Color.FromArgb(65, 65, 70);
            p.BorderStrong = Color.FromArgb(112, 112, 117);
            p.Text = Color.FromArgb(245, 245, 247);
            p.TextMuted = Color.FromArgb(177, 177, 186);
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
                int rounded = 2;
                int caption = ColorTranslator.ToWin32(UiTheme.Background);
                int captionText = ColorTranslator.ToWin32(UiTheme.Text);
                DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));
                DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
                DwmSetWindowAttribute(handle, 36, ref captionText, sizeof(int));
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
        ChevronUp,
        Phone, Monitor, ArrowLeft, ArrowRight, FolderPlus,
        Moon, Pin, Keyboard, Appearance, File
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
                else
                {
                    GraphicsState state = graphics.Save();
                    graphics.TranslateTransform(left, top);
                    graphics.ScaleTransform(scale, scale);
                    using (Pen line = new Pen(color, 1.65F))
                    {
                        line.StartCap = line.EndCap = LineCap.Round;
                        line.LineJoin = LineJoin.Round;
                        if (icon == UiIcon.Phone)
                        {
                            graphics.DrawRoundedRectangle(line, new RectangleF(6.5F, 2, 11, 20), 3);
                            graphics.DrawLine(line, 10, 5, 14, 5);
                            graphics.DrawLine(line, 11, 19, 13, 19);
                        }
                        else if (icon == UiIcon.Monitor)
                        {
                            graphics.DrawRoundedRectangle(line, new RectangleF(2.5F, 3.5F, 19, 13), 2);
                            graphics.DrawLine(line, 8, 21, 16, 21);
                            graphics.DrawLine(line, 12, 17, 12, 21);
                        }
                        else if (icon == UiIcon.ArrowLeft || icon == UiIcon.ArrowRight)
                        {
                            graphics.DrawLine(line, 4, 12, 20, 12);
                            graphics.DrawLines(line, icon == UiIcon.ArrowRight
                                ? new Point[] { new Point(14, 6), new Point(20, 12), new Point(14, 18) }
                                : new Point[] { new Point(10, 6), new Point(4, 12), new Point(10, 18) });
                        }
                        else if (icon == UiIcon.Moon)
                        {
                            using (GraphicsPath moon = new GraphicsPath())
                            {
                                moon.AddBezier(10, 3, -3, 7, 2, 24, 15, 21);
                                moon.AddBezier(15, 21, 18, 20, 21, 17, 21, 14);
                                moon.AddBezier(21, 14, 11, 18, 7, 10, 10, 3);
                                graphics.DrawPath(line, moon);
                            }
                        }
                        else if (icon == UiIcon.Pin)
                        {
                            graphics.DrawLines(line, new Point[] { new Point(8, 3), new Point(16, 3),
                                new Point(15, 10), new Point(18, 13), new Point(18, 15),
                                new Point(6, 15), new Point(6, 13), new Point(9, 10), new Point(8, 3) });
                            graphics.DrawLine(line, 12, 15, 12, 22);
                        }
                        else if (icon == UiIcon.Keyboard)
                        {
                            graphics.DrawRoundedRectangle(line, new RectangleF(2, 5, 20, 14), 2);
                            for (int y = 9; y <= 12; y += 3)
                                for (int x = 6; x <= 18; x += 4)
                                    graphics.FillEllipse(brush, x - .65F, y - .65F, 1.3F, 1.3F);
                            graphics.DrawLine(line, 7, 16, 17, 16);
                        }
                        else if (icon == UiIcon.Appearance)
                        {
                            graphics.DrawEllipse(line, 3, 3, 18, 18);
                            graphics.FillPie(brush, 3, 3, 18, 18, -90, 180);
                        }
                        else if (icon == UiIcon.File)
                        {
                            graphics.DrawLines(line, new Point[] { new Point(6, 2), new Point(14, 2),
                                new Point(19, 7), new Point(19, 22), new Point(5, 22), new Point(5, 2), new Point(6, 2) });
                            graphics.DrawLines(line, new Point[] { new Point(14, 2), new Point(14, 8), new Point(19, 8) });
                            graphics.DrawLine(line, 8, 13, 16, 13);
                            graphics.DrawLine(line, 8, 17, 14, 17);
                        }
                        else if (icon == UiIcon.FolderPlus)
                        {
                            graphics.DrawLines(line, new Point[] { new Point(3, 5), new Point(10, 5),
                                new Point(12, 8), new Point(21, 8), new Point(21, 20), new Point(3, 20), new Point(3, 5) });
                            graphics.DrawLine(line, 9, 14, 15, 14);
                            graphics.DrawLine(line, 12, 11, 12, 17);
                        }
                    }
                    graphics.Restore(state);
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
        public bool ShowBorder;
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
            float scale = e.Graphics.DpiX / 96F;
            Func<int, int> px = delegate(int value) { return (int)Math.Round(value * scale); };
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill;
            Color content;
            if (!Enabled) { fill = UiTheme.SurfaceMuted; content = UiTheme.TextDim; }
            else if (Kind == UiButtonKind.Primary)
            {
                fill = pressed ? UiTheme.AccentPressed : hovered ? UiTheme.AccentHover : UiTheme.Accent;
                content = Color.White;
            }
            else if (Kind == UiButtonKind.Danger)
            {
                fill = pressed ? Color.FromArgb(190, UiTheme.Danger) : hovered ? Color.FromArgb(224, UiTheme.Danger) : UiTheme.Danger;
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
            using (GraphicsPath path = RoundedPanel.CreateRoundedRect(bounds, px(CornerRadius)))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                e.Graphics.FillPath(brush, path);
                if (ShowBorder)
                    using (Pen pen = new Pen(UiTheme.Border, scale)) e.Graphics.DrawPath(pen, path);
            }

            bool hasText = !IconOnly && !string.IsNullOrEmpty(Text);
            int iconSize = Icon == UiIcon.None ? 0 : Math.Min(px(IconSize), Math.Min(Width - px(8), Height - px(8)));
            int gap = iconSize > 0 && hasText ? px(8) : 0;
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            int textWidth = hasText ? Math.Min(TextRenderer.MeasureText(e.Graphics, Text, Font,
                new Size(int.MaxValue, Height), flags).Width, Math.Max(0, Width - px(20) - iconSize - gap)) : 0;
            int left = (Width - iconSize - gap - textWidth) / 2;
            if (iconSize > 0)
            {
                Rectangle iconBounds = new Rectangle(left, (Height - iconSize) / 2, iconSize, iconSize);
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
                if (iconState != null) e.Graphics.Restore(iconState);
            }
            if (hasText)
                TextRenderer.DrawText(e.Graphics, Text, Font,
                    new Rectangle(left + iconSize + gap, 0, textWidth, Height), content, flags);
            if (Focused && ShowFocusCues)
            {
                using (GraphicsPath path = RoundedPanel.CreateRoundedRect(Rectangle.Inflate(bounds, -px(2), -px(2)), px(Math.Max(4, CornerRadius - 2))))
                using (Pen pen = new Pen(UiTheme.Accent, px(2))) e.Graphics.DrawPath(pen, path);
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
        public UiIcon Icon = UiIcon.None;
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
            Font = new Font(UiTheme.FontFamily, 9.75F, FontStyle.Regular);
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
            float scale = e.Graphics.DpiX / 96F;
            Func<int, int> px = delegate(int value) { return (int)Math.Round(value * scale); };
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (hovered)
                using (GraphicsPath path = RoundedPanel.CreateRoundedRect(bounds, px(8)))
                using (SolidBrush brush = new SolidBrush(UiTheme.SurfaceRaised)) e.Graphics.FillPath(brush, path);
            Rectangle track = new Rectangle(Width - px(38), (Height - px(23)) / 2, px(38), px(23));
            using (GraphicsPath path = RoundedPanel.CreateRoundedRect(track, px(11)))
            using (SolidBrush brush = new SolidBrush(Checked ? UiTheme.Accent :
                UiTheme.IsDark ? Color.FromArgb(100, 100, 108) : Color.FromArgb(213, 213, 218)))
                e.Graphics.FillPath(brush, path);
            int thumbX = Checked ? track.Right - px(21) : track.Left + px(2);
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(22, 0, 0, 0)))
                e.Graphics.FillEllipse(shadow, thumbX, track.Top + px(3), px(19), px(19));
            using (SolidBrush brush = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(brush, thumbX, track.Top + px(2), px(19), px(19));
            int textLeft = Icon == UiIcon.None ? 0 : px(28);
            UiIconRenderer.Draw(e.Graphics, Icon, new Rectangle(0, (Height - px(18)) / 2, px(18), px(18)), UiTheme.TextMuted);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(textLeft, 0,
                Math.Max(0, Width - textLeft - px(54)), Height), Enabled ? UiTheme.Text : UiTheme.TextDim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            if (Focused && ShowFocusCues)
                using (GraphicsPath path = RoundedPanel.CreateRoundedRect(Rectangle.Inflate(bounds, -px(2), -px(2)), px(7)))
                using (Pen pen = new Pen(UiTheme.Accent, px(2))) e.Graphics.DrawPath(pen, path);
        }
    }

    internal sealed class SettingLabel : Control
    {
        public UiIcon Icon;
        public SettingLabel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = new Font(UiTheme.FontFamily, 9.75F);
            BackColor = UiTheme.Surface;
            TabStop = false;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            float scale = e.Graphics.DpiX / 96F;
            int iconSize = (int)Math.Round(18 * scale), left = (int)Math.Round(28 * scale);
            UiIconRenderer.Draw(e.Graphics, Icon, new Rectangle(0, (Height - iconSize) / 2, iconSize, iconSize), UiTheme.TextMuted);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(left, 0, Width - left, Height), UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
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
            Font = new Font(UiTheme.FontFamily, 8.25F, FontStyle.Regular);
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
            float scale = e.Graphics.DpiX / 96F;
            Func<int, int> px = delegate(int value) { return (int)Math.Round(value * scale); };
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath outer = RoundedPanel.CreateRoundedRect(bounds, px(8)))
            using (SolidBrush brush = new SolidBrush(UiTheme.SurfaceMuted)) e.Graphics.FillPath(brush, outer);
            for (int index = 0; index < ItemCount; index++)
            {
                int left = index * Width / ItemCount + px(3);
                int right = (index + 1) * Width / ItemCount - px(3);
                Rectangle item = new Rectangle(left, px(3), Math.Max(1, right - left), Height - px(6));
                if (index == selectedIndex || index == hoveredIndex)
                {
                    if (index == selectedIndex)
                    {
                        Rectangle shadowBounds = item; shadowBounds.Offset(0, px(1));
                        using (GraphicsPath shadow = RoundedPanel.CreateRoundedRect(shadowBounds, px(6)))
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(18, 0, 0, 0))) e.Graphics.FillPath(brush, shadow);
                    }
                    Color fill = index == selectedIndex ? (UiTheme.IsDark ? Color.FromArgb(97, 97, 103) : UiTheme.Surface) : UiTheme.SurfaceHover;
                    using (GraphicsPath path = RoundedPanel.CreateRoundedRect(item, px(6)))
                    using (SolidBrush brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
                }
                TextRenderer.DrawText(e.Graphics, ItemText(index), Font, item,
                    index == selectedIndex ? UiTheme.Text : UiTheme.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            if (Focused && ShowFocusCues)
                using (GraphicsPath path = RoundedPanel.CreateRoundedRect(Rectangle.Inflate(bounds, -px(2), -px(2)), px(6)))
                using (Pen pen = new Pen(UiTheme.Accent, px(2))) e.Graphics.DrawPath(pen, path);
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
            input.Font = new Font(UiTheme.FontFamily, 8.25F);
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
                input.SetBounds(UiTheme.Scale(this, 32), Math.Max(1, (Height - inputHeight) / 2),
                    Math.Max(1, Width - UiTheme.Scale(this, 43)), inputHeight);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            input.Focus();
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = e.Graphics.DpiX / 96F;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedPanel.CreateRoundedRect(bounds, (int)Math.Round(7 * scale)))
            using (SolidBrush fill = new SolidBrush(UiTheme.SurfaceRaised))
            using (Pen pen = new Pen(input.Focused ? UiTheme.Accent : UiTheme.Border, (input.Focused ? 2 : 1) * scale))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
            int size = (int)Math.Round(15 * scale);
            UiIconRenderer.Draw(e.Graphics, UiIcon.Folder, new Rectangle((int)Math.Round(10 * scale),
                (Height - size) / 2, size, size), UiTheme.TextMuted);
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
        private bool actionHandled;

        public event EventHandler DeviceChosen;
        public event EventHandler ConnectRequested;
        public event EventHandler MoreRequested;
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
                device.IsPlaceholder ? "未选择设备" :
                device.AdbState == DeviceAdbState.Unauthorized ? "等待 USB 调试授权" :
                device.AdbState == DeviceAdbState.Offline ? "ADB 设备离线" :
                !device.IsSaved && device.IsOnline ? "新设备，可以连接" :
                device.IsOnline ? "USB 已连接" : "未连接";
            AccessibleDescription = device.Model + "，" + state;
            if (!ShowsChevron)
            {
                if (device.IsSaved)
                    AccessibleDescription += "，按 Delete 打开删除菜单";
                else if (device.IsOnline)
                    AccessibleDescription += "，按 Enter 连接";
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
            if (actionHandled)
            {
                actionHandled = false;
                base.OnClick(e);
                return;
            }
            Focus();
            EventHandler handler = DeviceChosen;
            if (handler != null)
                handler(this, EventArgs.Empty);
            base.OnClick(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !ShowsChevron && device != null)
            {
                if (!device.IsSaved && ConnectButtonBounds.Contains(e.Location))
                {
                    actionHandled = true;
                    if (device.IsOnline && ConnectRequested != null)
                        ConnectRequested(this, EventArgs.Empty);
                    return;
                }
                if (device.IsSaved && MoreButtonBounds.Contains(e.Location))
                {
                    actionHandled = true;
                    if (MoreRequested != null)
                        MoreRequested(this, EventArgs.Empty);
                    return;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if ((e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) &&
                !ShowsChevron && device != null && !device.IsSaved)
            {
                if (device.IsOnline && ConnectRequested != null)
                    ConnectRequested(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Delete && !ShowsChevron &&
                device != null && device.IsSaved)
            {
                if (MoreRequested != null)
                    MoreRequested(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space ||
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
            if (ShowsChevron)
            {
                PaintActiveDevice(e);
                return;
            }
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

            int textRightPadding = ShowsChevron ? 130 : 175;
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
                device.IsPlaceholder ? "未选择" :
                device.AdbState == DeviceAdbState.Unauthorized ? "等待授权" :
                device.AdbState == DeviceAdbState.Offline ? "设备离线" :
                !device.IsSaved && device.IsOnline ? "新设备" :
                device.IsOnline ? "已连接" : "未连接";
            Color statusColor = device.MirrorActive || device.MirrorStarting
                ? UiTheme.Accent
                : device.IsOnline ? UiTheme.Success : UiTheme.TextDim;
            int statusRight = ShowsChevron ? Width - 46 : Width - 88;
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
            else if (device.IsSaved)
            {
                if (Selected)
                {
                    UiIconRenderer.Draw(e.Graphics, UiIcon.Check,
                        new Rectangle(Width - 70, (Height - 18) / 2, 18, 18), UiTheme.Accent);
                }
                UiIconRenderer.Draw(e.Graphics, UiIcon.More,
                    new Rectangle(Width - 38, (Height - 22) / 2, 22, 22), UiTheme.TextMuted);
            }
            else
            {
                Rectangle button = ConnectButtonBounds;
                Color buttonFill = device.IsOnline ? UiTheme.Accent : UiTheme.SurfaceMuted;
                Color buttonText = device.IsOnline ? Color.White : UiTheme.TextDim;
                using (GraphicsPath buttonPath = RoundedPanel.CreateRoundedRect(button, 9))
                using (SolidBrush buttonBrush = new SolidBrush(buttonFill))
                    e.Graphics.FillPath(buttonBrush, buttonPath);
                TextRenderer.DrawText(e.Graphics, "连接", Font, button, buttonText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
            }

            if (Focused && ShowFocusCues)
            {
                using (GraphicsPath focusPath = RoundedPanel.CreateRoundedRect(Rectangle.Inflate(bounds, -2, -2), 10))
                using (Pen focusPen = new Pen(UiTheme.Accent, 2))
                    e.Graphics.DrawPath(focusPen, focusPath);
            }
        }

        private void PaintActiveDevice(PaintEventArgs e)
        {
            if (device == null) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = e.Graphics.DpiX / 96F;
            Func<int, int> px = delegate(int value) { return (int)Math.Round(value * scale); };
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (hovered)
                using (GraphicsPath path = RoundedPanel.CreateRoundedRect(bounds, px(10)))
                using (SolidBrush brush = new SolidBrush(UiTheme.SurfaceRaised)) e.Graphics.FillPath(brush, path);
            Rectangle well = new Rectangle(0, px(5), px(48), px(60));
            using (GraphicsPath path = RoundedPanel.CreateRoundedRect(well, px(14)))
            using (LinearGradientBrush brush = new LinearGradientBrush(well,
                UiTheme.IsDark ? Color.FromArgb(37, 59, 89) : Color.FromArgb(239, 247, 255),
                UiTheme.IsDark ? Color.FromArgb(37, 67, 101) : Color.FromArgb(226, 237, 254), 65F))
                e.Graphics.FillPath(brush, path);
            Color blue = UiTheme.IsDark ? Color.FromArgb(127, 189, 255) : UiTheme.Accent;
            UiIconRenderer.Draw(e.Graphics, UiIcon.Phone, new Rectangle(px(8), px(19), px(32), px(32)),
                device.IsOnline ? blue : UiTheme.TextMuted);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            int textWidth = Math.Max(px(80), Width - px(94));
            using (Font nameFont = new Font(UiTheme.FontFamily, 13.5F, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, device.Name, nameFont, new Rectangle(px(62), 0, textWidth, px(26)), UiTheme.Text, flags);
            using (Font detailFont = new Font(UiTheme.FontFamily, 9F))
                TextRenderer.DrawText(e.Graphics, device.Model, detailFont, new Rectangle(px(62), px(28), textWidth, px(18)), UiTheme.TextMuted, flags);
            string status = device.MirrorStarting ? "连接中" : device.MirrorStopping ? "停止中" :
                device.MirrorActive ? "正在投屏" : device.IsPlaceholder ? "未选择设备" :
                device.AdbState == DeviceAdbState.Unauthorized ? "等待授权" :
                device.AdbState == DeviceAdbState.Offline ? "设备离线" :
                !device.IsSaved && device.IsOnline ? "新设备" : device.IsOnline ? "已连接" : "未连接";
            if (device.IsOnline) status += "  ·  USB";
            Color statusColor = device.MirrorActive || device.MirrorStarting ? blue : device.IsOnline ? UiTheme.Success : UiTheme.TextMuted;
            using (Font statusFont = new Font(UiTheme.FontFamily, 8.25F))
            {
                int width = Math.Min(textWidth, TextRenderer.MeasureText(e.Graphics, status, statusFont,
                    new Size(int.MaxValue, px(24)), flags).Width + px(29));
                Rectangle pill = new Rectangle(px(62), px(53), width, px(24));
                using (GraphicsPath path = RoundedPanel.CreateRoundedRect(pill, px(12)))
                using (SolidBrush brush = new SolidBrush(device.IsOnline
                    ? UiTheme.IsDark ? Color.FromArgb(32, 61, 96) : Color.FromArgb(232, 242, 255)
                    : UiTheme.SurfaceMuted)) e.Graphics.FillPath(brush, path);
                using (SolidBrush brush = new SolidBrush(statusColor))
                    e.Graphics.FillEllipse(brush, pill.Left + px(10), pill.Top + px(9), px(6), px(6));
                TextRenderer.DrawText(e.Graphics, status, statusFont,
                    new Rectangle(pill.Left + px(22), pill.Top, pill.Width - px(27), pill.Height), statusColor, flags);
            }
            UiIconRenderer.Draw(e.Graphics, UiIcon.ChevronDown,
                new Rectangle(Width - px(22), px(6), px(18), px(18)), UiTheme.TextMuted);
            if (Focused && ShowFocusCues)
                using (GraphicsPath path = RoundedPanel.CreateRoundedRect(Rectangle.Inflate(bounds, -px(2), -px(2)), px(10)))
                using (Pen pen = new Pen(UiTheme.Accent, px(2))) e.Graphics.DrawPath(pen, path);
        }

        private Rectangle ConnectButtonBounds
        {
            get { return new Rectangle(Width - 76, (Height - 32) / 2, 62, 32); }
        }

        private Rectangle MoreButtonBounds
        {
            get { return new Rectangle(Width - 48, 4, 44, Height - 8); }
        }
    }

    internal sealed class DeviceCard
    {
        public string Name;
        public string Model;
        public string Serial;
        public string AndroidVersion;
        public bool IsSaved;
        public bool IsDiscovered;
        public bool IsPlaceholder;
        public DeviceAdbState AdbState;
        public bool IsOnline;
        public bool MirrorActive;
        public int WindowX;
        public int LastWindowX;
        public int LastWindowY;
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
                using (GraphicsPath shadowPath = CreateRoundedRect(shadowBounds, (int)Math.Round(CornerRadius * e.Graphics.DpiX / 96F)))
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(20, 0, 0, 0)))
                    e.Graphics.FillPath(shadowBrush, shadowPath);
            }

            using (GraphicsPath path = CreateRoundedRect(r, (int)Math.Round(CornerRadius * e.Graphics.DpiX / 96F)))
            using (SolidBrush fill = new SolidBrush(BackColor))
            using (Pen pen = new Pen(BorderColor, 1))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        internal static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
        {
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class WordmarkLabel : Label
    {
        private const int WmLButtonDoubleClick = 0x0203;

        public event EventHandler WordmarkDoubleClick;

        protected override void WndProc(ref Message message)
        {
            bool doubleClicked = message.Msg == WmLButtonDoubleClick;
            base.WndProc(ref message);
            if (doubleClicked && WordmarkDoubleClick != null)
                WordmarkDoubleClick(this, EventArgs.Empty);
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
                // Keep this as a normal owned tool window. WS_EX_NOACTIVATE made
                // the picker fail to surface on some WinForms/DWM combinations.
                parameters.ExStyle |= 0x00000080;
                return parameters;
            }
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
        private readonly List<DeviceCard> devices = new List<DeviceCard>();
        private readonly Dictionary<string, DeviceCard> devicesBySerial =
            new Dictionary<string, DeviceCard>(StringComparer.OrdinalIgnoreCase);
        private readonly List<DeviceSelectRow> deviceRows = new List<DeviceSelectRow>();
        private readonly DeviceCard emptyDevice;
        private DeviceCard activeDevice;
        private readonly Timer refreshTimer;
        private readonly Timer restartTimer;
        private readonly Label footerStatus;
        private readonly ModeSelector keyboardModeSelector;
        private readonly ModeSelector themeModeSelector;
        private readonly ToggleSwitch screenOffToggle;
        private readonly ToggleSwitch alwaysOnTopToggle;
        private readonly DeviceSelectRow activeDeviceRow;
        private readonly DevicePickerForm devicePicker;
        private readonly PickerDismissFilter pickerDismissFilter;
        private readonly RoundedPanel settingsPanel;
        private readonly ModernButton launchButton;
        private readonly ModernButton transferButton;
        private readonly ModernButton refreshButton;
        private readonly ToolTip toolTip;
        private readonly string settingsPath;
        private readonly string screenOffSettingsPath;
        private readonly string alwaysOnTopSettingsPath;
        private readonly string selectedDeviceSettingsPath;
        private readonly string themeSettingsPath;
        private readonly string deviceStorePath;
        private readonly List<DeviceCard> pendingRestart = new List<DeviceCard>();
        private readonly HashSet<DeviceCard> pendingWakeBeforeRestart = new HashSet<DeviceCard>();
        private readonly KeyboardCapture keyboardCapture;
        private int keyboardMode;
        private AppThemeMode themeMode;
        private int refreshInProgress;
        private string lastAdbProbeDetail = string.Empty;
        private static readonly object screenPowerInputGate = new object();

        public MainForm()
        {
            SuspendLayout();
            appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            adbPath = Path.Combine(appDirectory, "scrcpy", "adb.exe");
            scrcpyPath = Path.Combine(appDirectory, "scrcpy", "scrcpy.exe");
            settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OMirror",
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
            deviceStorePath = Path.Combine(
                Path.GetDirectoryName(settingsPath),
                "devices.json");
            keyboardMode = LoadKeyboardMode();
            themeMode = LoadThemeMode();
            UiTheme.SetDark(ThemeShouldBeDark());
            toolTip = new ToolTip();
            toolTip.AutoPopDelay = 5000;
            toolTip.InitialDelay = 450;
            toolTip.ReshowDelay = 100;

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                // The embedded executable icon remains available to the shell.
            }

            Text = "OMirror";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(480, 606);
            BackColor = background;
            ForeColor = foreground;
            Font = new Font(UiTheme.FontFamily, 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            KeyPreview = true;

            WordmarkLabel title = new WordmarkLabel();
            title.Text = "OMirror";
            title.Font = new Font(UiTheme.DisplayFontFamily, 21F, FontStyle.Regular);
            title.ForeColor = foreground;
            title.AutoSize = true;
            title.Location = new Point(26, 23);
            title.WordmarkDoubleClick += delegate { CenterActiveMirrorWindow(); };
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "你的手机，触手可及。";
            subtitle.Font = new Font(UiTheme.FontFamily, 9F);
            subtitle.ForeColor = muted;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(26, 65);
            Controls.Add(subtitle);

            refreshButton = MakeIconButton(UiIcon.Refresh, "刷新设备", UiButtonKind.Quiet);
            refreshButton.Location = new Point(420, 36);
            refreshButton.Size = new Size(34, 34);
            refreshButton.CornerRadius = 17;
            refreshButton.ShowBorder = true;
            refreshButton.IconSize = 17;
            refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refreshButton.Click += delegate { RefreshDevices(); };
            Controls.Add(refreshButton);
            toolTip.SetToolTip(refreshButton, "刷新设备");

            emptyDevice = CreateEmptyDevice();
            LoadSavedDevices();
            activeDevice = FindSavedDevice(LoadSelectedDeviceSerial()) ??
                FirstSavedDevice() ?? emptyDevice;

            RoundedPanel devicePanel = new RoundedPanel();
            devicePanel.Name = "activeDevicePanel";
            devicePanel.BackColor = card;
            devicePanel.BorderColor = UiTheme.Border;
            devicePanel.Shadow = false;
            devicePanel.Location = new Point(26, 106);
            devicePanel.CornerRadius = 16;
            devicePanel.Size = new Size(428, 178);
            Controls.Add(devicePanel);

            activeDeviceRow = new DeviceSelectRow();
            activeDeviceRow.Name = "activeDeviceSelector";
            activeDeviceRow.Device = activeDevice;
            activeDeviceRow.ShowsChevron = true;
            activeDeviceRow.Location = new Point(20, 20);
            activeDeviceRow.Size = new Size(388, 80);
            activeDeviceRow.DeviceChosen += delegate { ToggleDeviceList(); };
            devicePanel.Controls.Add(activeDeviceRow);
            toolTip.SetToolTip(activeDeviceRow, "选择设备");

            launchButton = MakeActionButton(UiIcon.Mirror, "投屏", UiButtonKind.Primary);
            launchButton.Location = new Point(20, 119);
            launchButton.Size = new Size(189, 38);
            launchButton.CornerRadius = 9;
            launchButton.Enabled = false;
            launchButton.Click += delegate
            {
                CloseDeviceList();
                ToggleMirror(activeDevice);
            };
            devicePanel.Controls.Add(launchButton);
            toolTip.SetToolTip(launchButton, "启动有线投屏");

            transferButton = MakeActionButton(UiIcon.Transfer, "文件互传", UiButtonKind.Primary);
            transferButton.Location = new Point(219, 119);
            transferButton.Size = new Size(189, 38);
            transferButton.CornerRadius = 9;
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
            info.Location = new Point(26, 334);
            info.CornerRadius = 16;
            info.Size = new Size(428, 222);
            info.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            Label infoTitle = new Label();
            infoTitle.Text = "偏好设置";
            infoTitle.ForeColor = muted;
            infoTitle.Font = new Font(UiTheme.FontFamily, 9F, FontStyle.Regular);
            infoTitle.AutoSize = true;
            infoTitle.Location = new Point(30, 307);
            Controls.Add(infoTitle);

            screenOffToggle = new ToggleSwitch();
            screenOffToggle.Text = "仅熄手机屏幕";
            screenOffToggle.Location = new Point(16, 1);
            screenOffToggle.Icon = UiIcon.Moon;
            screenOffToggle.Size = new Size(396, 54);
            screenOffToggle.Checked = LoadScreenOffSetting();
            screenOffToggle.CheckedChanged += ScreenOffSettingChanged;
            info.Controls.Add(screenOffToggle);
            toolTip.SetToolTip(screenOffToggle, "关闭手机实体屏幕，投屏保持运行");

            alwaysOnTopToggle = new ToggleSwitch();
            alwaysOnTopToggle.Text = "投屏窗口置顶";
            alwaysOnTopToggle.Location = new Point(16, 56);
            alwaysOnTopToggle.Icon = UiIcon.Pin;
            alwaysOnTopToggle.Size = new Size(396, 54);
            alwaysOnTopToggle.Checked = LoadAlwaysOnTopSetting();
            alwaysOnTopToggle.CheckedChanged += AlwaysOnTopSettingChanged;
            info.Controls.Add(alwaysOnTopToggle);
            toolTip.SetToolTip(alwaysOnTopToggle, "让投屏窗口保持在其他窗口上方");

            for (int dividerIndex = 0; dividerIndex < 3; dividerIndex++)
            {
                Panel divider = new Panel();
                divider.BackColor = UiTheme.Border;
                divider.Location = new Point(16, 55 + dividerIndex * 55);
                divider.Size = new Size(396, 1);
                info.Controls.Add(divider);
            }

            SettingLabel keyboardLabel = new SettingLabel();
            keyboardLabel.Icon = UiIcon.Keyboard;
            keyboardLabel.Text = "键盘模式";
            keyboardLabel.ForeColor = foreground;
            keyboardLabel.Size = new Size(170, 54);
            keyboardLabel.Location = new Point(16, 111);
            info.Controls.Add(keyboardLabel);

            keyboardModeSelector = new ModeSelector();
            keyboardModeSelector.FirstText = "短语";
            keyboardModeSelector.SecondText = "数字选词";
            keyboardModeSelector.Location = new Point(231, 122);
            keyboardModeSelector.Size = new Size(181, 31);
            keyboardModeSelector.SelectedIndex = keyboardMode;
            keyboardModeSelector.SelectedIndexChanged += KeyboardModeChanged;
            info.Controls.Add(keyboardModeSelector);
            toolTip.SetToolTip(keyboardModeSelector, "短语：Shift 切换中英；数字选词：Shift+Space 切换中英");

            SettingLabel themeLabel = new SettingLabel();
            themeLabel.Icon = UiIcon.Appearance;
            themeLabel.Text = "外观";
            themeLabel.ForeColor = foreground;
            themeLabel.Size = new Size(170, 54);
            themeLabel.Location = new Point(16, 166);
            info.Controls.Add(themeLabel);

            themeModeSelector = new ModeSelector();
            themeModeSelector.AccessibleName = "外观主题";
            themeModeSelector.FirstText = "自动";
            themeModeSelector.SecondText = "浅色";
            themeModeSelector.ThirdText = "深色";
            themeModeSelector.Location = new Point(231, 177);
            themeModeSelector.Size = new Size(181, 31);
            themeModeSelector.SelectedIndex = (int)themeMode;
            themeModeSelector.SelectedIndexChanged += ThemeModeChanged;
            info.Controls.Add(themeModeSelector);
            Controls.Add(info);

            devicePicker = new DevicePickerForm();
            devicePicker.Size = new Size(532, 72);
            devicePicker.AutoScroll = true;
            pickerDismissFilter = new PickerDismissFilter(this);
            Application.AddMessageFilter(pickerDismissFilter);
            RebuildDevicePicker();

            footerStatus = new Label();
            footerStatus.Text = "正在检查设备…";
            footerStatus.ForeColor = muted;
            footerStatus.AutoSize = false;
            footerStatus.AutoEllipsis = true;
            footerStatus.Size = new Size(344, 23);
            footerStatus.Font = new Font(UiTheme.FontFamily, 8.25F);
            footerStatus.TextAlign = ContentAlignment.MiddleLeft;
            footerStatus.TextChanged += delegate { toolTip.SetToolTip(footerStatus, footerStatus.Text); };
            footerStatus.Location = new Point(26, 572);
            footerStatus.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            Controls.Add(footerStatus);

            Label version = new Label();
            version.Text = "v1.16.0";
            version.ForeColor = muted;
            version.AutoSize = false;
            version.Location = new Point(378, 572);
            version.Font = new Font(UiTheme.FontFamily, 8.25F);
            version.Size = new Size(76, 23);
            version.TextAlign = ContentAlignment.MiddleRight;
            version.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(version);

            refreshTimer = new Timer();
            refreshTimer.Interval = 2500;
            refreshTimer.Tick += delegate { RefreshDevices(false); };

            restartTimer = new Timer();
            restartTimer.Interval = 700;
            restartTimer.Tick += RestartMirrors;

            keyboardCapture = new KeyboardCapture(this);
            FormClosed += delegate
            {
                foreach (DeviceCard device in DevicesSnapshot())
                    StopScreenControl(device, true);
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
                    CycleSavedDevice(e.KeyCode == Keys.Down ? 1 : -1);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            UiTheme.ThemeChanged += ApplyTheme;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemThemeChanged;
            ResumeLayout(true);
        }

        internal bool IsRuntimeReady
        {
            get { return File.Exists(adbPath) && File.Exists(scrcpyPath); }
        }

        private void LoadSavedDevices()
        {
            try
            {
                foreach (SavedDeviceRecord record in DeviceRegistry.Load(deviceStorePath))
                {
                    DeviceCard device = CreateDevice(record);
                    devices.Add(device);
                    devicesBySerial[device.Serial] = device;
                }
            }
            catch
            {
                devices.Clear();
                devicesBySerial.Clear();
            }
        }

        private DeviceCard CreateEmptyDevice()
        {
            return new DeviceCard
            {
                Name = "未选择设备",
                Model = "连接手机后点击刷新",
                Serial = string.Empty,
                IsPlaceholder = true,
                AdbState = DeviceAdbState.Disconnected,
                WindowX = 80,
                LastWindowX = 80,
                LastWindowY = 80
            };
        }

        private DeviceCard CreateDevice(SavedDeviceRecord record)
        {
            DeviceCard device = new DeviceCard();
            device.Name = record.Name;
            device.Model = record.Model;
            device.Serial = record.Serial;
            device.AndroidVersion = record.AndroidVersion;
            device.IsSaved = true;
            device.AdbState = DeviceAdbState.Disconnected;
            device.WindowX = record.WindowX;
            device.LastWindowX = record.WindowX;
            device.LastWindowY = record.WindowY;
            return device;
        }

        private DeviceCard FindSavedDevice(string serial)
        {
            DeviceCard device;
            if (string.IsNullOrWhiteSpace(serial) ||
                !devicesBySerial.TryGetValue(serial, out device) || !device.IsSaved)
                return null;
            return device;
        }

        private DeviceCard FirstSavedDevice()
        {
            foreach (DeviceCard device in devices)
            {
                if (device.IsSaved)
                    return device;
            }
            return null;
        }

        private DeviceCard[] DevicesSnapshot()
        {
            return devices.ToArray();
        }

        private List<DeviceCard> SavedDevices()
        {
            List<DeviceCard> result = new List<DeviceCard>();
            foreach (DeviceCard device in devices)
            {
                if (device.IsSaved)
                    result.Add(device);
            }
            return result;
        }

        private void PersistSavedDevices()
        {
            List<SavedDeviceRecord> records = new List<SavedDeviceRecord>();
            foreach (DeviceCard device in devices)
            {
                if (!device.IsSaved)
                    continue;
                records.Add(new SavedDeviceRecord
                {
                    Serial = device.Serial,
                    Name = device.Name,
                    Model = device.Model,
                    AndroidVersion = device.AndroidVersion,
                    WindowX = device.LastWindowX,
                    WindowY = device.LastWindowY
                });
            }
            try
            {
                DeviceRegistry.Save(deviceStorePath, records);
            }
            catch
            {
                if (footerStatus != null)
                    footerStatus.Text = "设备记录保存失败；本次连接仍可使用";
            }
        }

        internal bool StressScreenOffSetting { get { return screenOffToggle.Checked; } }
        internal bool StressTopMostSetting { get { return alwaysOnTopToggle.Checked; } }
        internal int StressKeyboardSetting { get { return keyboardMode; } }
        internal int StressActiveDeviceIndex
        {
            get
            {
                List<DeviceCard> saved = SavedDevices();
                int index = saved.IndexOf(activeDevice);
                return index < 0 ? 0 : index;
            }
        }

        internal bool StressPrepare(int deviceIndex)
        {
            List<DeviceCard> saved = SavedDevices();
            if (deviceIndex < 0 || deviceIndex >= saved.Count)
                return false;
            DeviceCard requested = saved[deviceIndex];
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
            Diagnostics.Trace("stress", "prepare", "saved=" + saved.Count + " online=" + online + " detail=" + lastAdbProbeDetail);
            return online;
        }

        internal bool StressMirrorIsRunning()
        {
            if (activeDevice == null || activeDevice.IsPlaceholder)
                return false;
            return activeDevice.MirrorActive || FindMirrorProcesses(activeDevice).Count > 0;
        }

        internal int StressMirrorProcessCount()
        {
            if (activeDevice == null || activeDevice.IsPlaceholder)
                return 0;
            return FindMirrorProcesses(activeDevice).Count;
        }

        internal void StressStartOrStopMirror()
        {
            if (activeDevice != null && !activeDevice.IsPlaceholder)
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
            if (target == null || target.IsPlaceholder)
                return;
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
            List<DeviceCard> saved = SavedDevices();
            if (deviceIndex >= 0 && deviceIndex < saved.Count)
                SelectDevice(saved[deviceIndex]);
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
            button.Font = new Font(UiTheme.FontFamily, 9.75F, FontStyle.Regular);
            button.IconSize = 17;
            return button;
        }

        private void ToggleDeviceList()
        {
            if (devicePicker.Visible)
            {
                CloseDeviceList();
                return;
            }
            RebuildDevicePicker();
            activeDeviceRow.AccessibleDescription = activeDevice.Model + "，设备列表已展开";
            devicePicker.ShowFor(activeDeviceRow, this);
        }

        private void CloseDeviceList()
        {
            if (!devicePicker.Visible)
                return;
            devicePicker.Hide();
            activeDeviceRow.AccessibleDescription = activeDevice.Model + "，设备列表已折叠";
        }

        private void SelectDevice(DeviceCard device)
        {
            if (device == null || !device.IsSaved)
                return;
            activeDevice = device;
            activeDeviceRow.Device = device;
            foreach (DeviceSelectRow row in deviceRows)
            {
                row.Selected = row.Device == device;
                row.Invalidate();
            }
            UpdateMirrorAction(device);
            transferButton.Enabled = device.IsOnline;
            SaveSelectedDeviceSerial(device.Serial);
            devicePicker.Hide();
            footerStatus.Text = DeviceStatusText(device);
            activeDeviceRow.Focus();
        }

        private void RebuildDevicePicker()
        {
            List<DeviceCard> ordered = OrderedDevices();
            bool sameRows = ordered.Count == deviceRows.Count;
            if (sameRows)
            {
                for (int i = 0; i < ordered.Count; i++)
                {
                    if (deviceRows[i].Device != ordered[i])
                    {
                        sameRows = false;
                        break;
                    }
                }
            }
            if (sameRows && (ordered.Count > 0 || devicePicker.Controls.Count > 0))
            {
                foreach (DeviceSelectRow row in deviceRows)
                {
                    row.Selected = row.Device == activeDevice;
                    row.UpdateStatus();
                    toolTip.SetToolTip(row, row.Device.IsSaved
                        ? "选择设备；更多菜单可删除记录"
                        : row.Device.IsOnline ? "连接并启动投屏" : "设备暂不可连接");
                }
                return;
            }

            bool wasVisible = devicePicker.Visible;
            if (wasVisible)
                devicePicker.Hide();
            devicePicker.SuspendLayout();
            deviceRows.Clear();
            while (devicePicker.Controls.Count > 0)
            {
                Control oldControl = devicePicker.Controls[0];
                devicePicker.Controls.RemoveAt(0);
                oldControl.Dispose();
            }

            if (ordered.Count == 0)
            {
                Label empty = new Label();
                empty.Text = "未发现设备\r\n连接手机并允许 USB 调试后点击刷新";
                empty.ForeColor = UiTheme.TextMuted;
                empty.Font = new Font(UiTheme.FontFamily, 9F, FontStyle.Regular);
                empty.TextAlign = ContentAlignment.MiddleCenter;
                empty.Location = new Point(12, 8);
                empty.Size = new Size(508, 52);
                devicePicker.Controls.Add(empty);
                devicePicker.ClientSize = new Size(532, 68);
                devicePicker.AutoScrollMinSize = Size.Empty;
            }
            else
            {
                for (int i = 0; i < ordered.Count; i++)
                {
                    DeviceCard target = ordered[i];
                    DeviceSelectRow row = new DeviceSelectRow();
                    row.Device = target;
                    row.Selected = target == activeDevice;
                    row.Location = new Point(8, 7 + i * 59);
                    row.Size = new Size(500, 55);
                    row.DeviceChosen += delegate { SelectDevice(target); };
                    row.ConnectRequested += delegate { ConnectDevice(target); };
                    row.MoreRequested += delegate { ShowDeviceMenu(row, target); };
                    toolTip.SetToolTip(row, target.IsSaved
                        ? "选择设备；更多菜单可删除记录"
                        : target.IsOnline ? "连接并启动投屏" : "设备暂不可连接");
                    deviceRows.Add(row);
                    devicePicker.Controls.Add(row);
                }
                int visibleRows = Math.Min(4, ordered.Count);
                devicePicker.ClientSize = new Size(532, 14 + visibleRows * 59);
                devicePicker.AutoScrollMinSize = new Size(0, 14 + ordered.Count * 59);
            }
            devicePicker.ResumeLayout(true);
            if (wasVisible)
                devicePicker.ShowFor(activeDeviceRow, this);
        }

        private List<DeviceCard> OrderedDevices()
        {
            List<DeviceCard> result = new List<DeviceCard>();
            if (activeDevice != null && !activeDevice.IsPlaceholder && activeDevice.IsSaved)
                result.Add(activeDevice);
            foreach (DeviceCard device in devices)
            {
                if (device != activeDevice && device.IsSaved && device.IsOnline)
                    result.Add(device);
            }
            foreach (DeviceCard device in devices)
            {
                if (device != activeDevice && device.IsSaved && !device.IsOnline)
                    result.Add(device);
            }
            foreach (DeviceCard device in devices)
            {
                if (!device.IsSaved && device.IsOnline)
                    result.Add(device);
            }
            foreach (DeviceCard device in devices)
            {
                if (!device.IsSaved && !device.IsOnline)
                    result.Add(device);
            }
            return result;
        }

        private void CycleSavedDevice(int direction)
        {
            List<DeviceCard> saved = SavedDevices();
            if (saved.Count == 0)
                return;
            int current = saved.IndexOf(activeDevice);
            if (current < 0)
                current = 0;
            else
                current = (current + direction + saved.Count) % saved.Count;
            SelectDevice(saved[current]);
        }

        private void ConnectDevice(DeviceCard device)
        {
            if (device == null || device.IsSaved || !device.IsOnline)
                return;
            device.IsSaved = true;
            if (device.LastWindowX == 0)
                device.LastWindowX = 80 + (SavedDevices().Count - 1) * 40;
            PersistSavedDevices();
            RebuildDevicePicker();
            SelectDevice(device);
            LaunchDevice(device);
        }

        private void ShowDeviceMenu(DeviceSelectRow row, DeviceCard device)
        {
            if (device == null || !device.IsSaved)
                return;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.BackColor = UiTheme.Surface;
            menu.ForeColor = UiTheme.Text;
            ToolStripMenuItem delete = new ToolStripMenuItem("删除此设备");
            delete.ForeColor = UiTheme.Danger;
            delete.Click += delegate { DeleteSavedDevice(device); };
            menu.Items.Add(delete);
            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(row, new Point(row.Width - 142, row.Height - 2));
        }

        private void DeleteSavedDevice(DeviceCard device)
        {
            if (device == null || !device.IsSaved)
                return;
            DialogResult confirmation = MessageBox.Show(
                "从 OMirror 中删除“" + device.Name + "”？\n\n" +
                "只会清除这台电脑上的设备记录，不会删除手机中的数据。",
                "删除设备",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.OK)
                return;

            // Invalidate every delayed lifecycle callback before stopping/removing
            // the record, and ensure a queued hot restart cannot relaunch it.
            device.StopRequested = true;
            device.SessionGeneration++;
            device.RecoveryRequestId++;
            device.ScreenPowerRequestId++;
            device.TopMostRequestId++;
            pendingRestart.Remove(device);
            pendingWakeBeforeRestart.Remove(device);
            List<Process> running = FindMirrorProcesses(device);
            if (running.Count > 0)
            {
                device.SessionState = MirrorSessionState.Stopping;
                StopScreenControl(device, true);
                StopMirrorProcesses(running, true);
            }
            device.MirrorProcess = null;
            device.MirrorProcessId = 0;
            device.MirrorActive = false;
            device.MirrorStarting = false;
            device.MirrorStopping = false;
            device.SessionState = MirrorSessionState.Stopped;
            device.StopRequested = false;
            device.IsSaved = false;
            try { File.Delete(DeviceModePath(device.Serial)); }
            catch { }

            if (!device.IsDiscovered)
            {
                devices.Remove(device);
                devicesBySerial.Remove(device.Serial);
            }
            if (activeDevice == device)
                activeDevice = FirstSavedDevice() ?? emptyDevice;
            activeDeviceRow.Device = activeDevice;
            SaveSelectedDeviceSerial(activeDevice.IsPlaceholder ? string.Empty : activeDevice.Serial);
            PersistSavedDevices();
            RebuildDevicePicker();
            UpdateMirrorAction(activeDevice);
            transferButton.Enabled = activeDevice.IsOnline && activeDevice.IsSaved;
            footerStatus.Text = device.IsDiscovered
                ? "已删除设备记录；手机仍连接，可作为新设备重新添加"
                : "已删除设备记录";
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

        private string LoadSelectedDeviceSerial()
        {
            try
            {
                return File.ReadAllText(selectedDeviceSettingsPath, Encoding.UTF8).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private void SaveSelectedDeviceSerial(string serial)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(selectedDeviceSettingsPath));
                File.WriteAllText(selectedDeviceSettingsPath, serial ?? string.Empty, Encoding.UTF8);
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
            RefreshDevices(true);
        }

        private void RefreshDevices(bool announceProgress)
        {
            if (!File.Exists(adbPath) || !File.Exists(scrcpyPath))
            {
                footerStatus.Text = "安装不完整：缺少 scrcpy 或 ADB";
                foreach (DeviceCard device in devices)
                    SetDeviceState(device, false);
                return;
            }

            if (System.Threading.Interlocked.CompareExchange(ref refreshInProgress, 1, 0) != 0)
                return;
            refreshButton.Enabled = false;
            refreshButton.AccessibleDescription = "正在扫描 USB 设备";
            if (announceProgress)
                footerStatus.Text = "正在扫描 USB 设备…";

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                DeviceDiscoveryResult result = DeviceRegistry.DiscoverUsb(adbPath, 10000);

                if (IsDisposed)
                {
                    System.Threading.Interlocked.Exchange(ref refreshInProgress, 0);
                    return;
                }

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        ApplyDiscoveryResult(result);
                        refreshButton.Enabled = true;
                        refreshButton.AccessibleDescription = "刷新 USB 设备";
                        if (!result.Success)
                            footerStatus.Text = "设备扫描失败：" + result.Error;
                        else if (result.Devices.Count == 0)
                            footerStatus.Text = "未发现 USB 调试设备";
                        else
                            footerStatus.Text = string.Format("已发现 {0} 台 USB 设备", result.Devices.Count);

                        System.Threading.Interlocked.Exchange(ref refreshInProgress, 0);
                    });
                }
                catch
                {
                    System.Threading.Interlocked.Exchange(ref refreshInProgress, 0);
                }
            });
        }

        private void ApplyDiscoveryResult(DeviceDiscoveryResult result)
        {
            if (!result.Success)
                return;

            foreach (DeviceCard device in devices)
            {
                device.IsDiscovered = false;
                device.IsOnline = false;
                device.AdbState = DeviceAdbState.Disconnected;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool savedMetadataChanged = false;
            foreach (DiscoveredDevice discovered in result.Devices)
            {
                seen.Add(discovered.Serial);
                DeviceCard device;
                if (!devicesBySerial.TryGetValue(discovered.Serial, out device))
                {
                    device = new DeviceCard
                    {
                        Serial = discovered.Serial,
                        Name = discovered.Name,
                        Model = discovered.Model,
                        AndroidVersion = discovered.AndroidVersion,
                        IsSaved = false,
                        WindowX = 80 + devices.Count * 40,
                        LastWindowX = 80 + devices.Count * 40,
                        LastWindowY = 80
                    };
                    devices.Add(device);
                    devicesBySerial[device.Serial] = device;
                }
                device.IsDiscovered = true;
                device.AdbState = discovered.State;
                device.IsOnline = discovered.State == DeviceAdbState.Online;
                if (!string.IsNullOrWhiteSpace(discovered.Name) && device.Name != discovered.Name)
                {
                    device.Name = discovered.Name;
                    savedMetadataChanged = savedMetadataChanged || device.IsSaved;
                }
                if (!string.IsNullOrWhiteSpace(discovered.Model) && device.Model != discovered.Model)
                {
                    device.Model = discovered.Model;
                    savedMetadataChanged = savedMetadataChanged || device.IsSaved;
                }
                if (!string.IsNullOrWhiteSpace(discovered.AndroidVersion) &&
                    device.AndroidVersion != discovered.AndroidVersion)
                {
                    device.AndroidVersion = discovered.AndroidVersion;
                    savedMetadataChanged = savedMetadataChanged || device.IsSaved;
                }
            }

            for (int i = devices.Count - 1; i >= 0; i--)
            {
                DeviceCard device = devices[i];
                if (!device.IsSaved && !seen.Contains(device.Serial))
                {
                    devices.RemoveAt(i);
                    devicesBySerial.Remove(device.Serial);
                }
            }
            if (savedMetadataChanged)
                PersistSavedDevices();
            if (activeDevice == null || (!activeDevice.IsSaved && !activeDevice.IsPlaceholder))
                activeDevice = FirstSavedDevice() ?? emptyDevice;
            activeDeviceRow.Device = activeDevice;
            UpdateMirrorAction(activeDevice);
            transferButton.Enabled = activeDevice.IsSaved && activeDevice.IsOnline;
            RebuildDevicePicker();
        }

        private bool IsUsbDeviceOnline(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
                return false;

            try
            {
                AdbResult result = new AdbClient(adbPath, serial).RunDevice("get-state", 5000);
                string output = (result.Output ?? string.Empty).Trim();
                lastAdbProbeDetail = "exit=" + result.ExitCode + " state=" + output;
                return result.Success && output == "device";
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
            device.AdbState = connected ? DeviceAdbState.Online : DeviceAdbState.Disconnected;
            UpdateDeviceRows(device);
            if (device == activeDevice)
            {
                UpdateMirrorAction(device);
                transferButton.Enabled = connected && device.IsSaved;
            }
        }

        private string DeviceStatusText(DeviceCard device)
        {
            if (device == null || device.IsPlaceholder) return "连接手机后点击刷新";
            if (device.MirrorStarting) return device.Name + " 正在启动投屏…";
            if (device.MirrorStopping) return device.Name + " 正在停止投屏…";
            if (device.MirrorActive) return device.Name + " 正在投屏";
            if (!device.IsSaved && device.IsOnline) return device.Name + " 是新设备，可以连接";
            if (device.AdbState == DeviceAdbState.Unauthorized) return device.Name + " 等待 USB 调试授权";
            if (device.AdbState == DeviceAdbState.Offline) return device.Name + " ADB 离线";
            return device.Name + (device.IsOnline ? " 已就绪" : " 未连接");
        }

        private void UpdateDeviceRows(DeviceCard device)
        {
            foreach (DeviceSelectRow row in deviceRows)
            {
                if (row.Device == device)
                    row.UpdateStatus();
            }
            if (device == activeDevice)
                activeDeviceRow.UpdateStatus();
        }

        private void UpdateMirrorAction(DeviceCard device)
        {
            if (device != activeDevice)
                return;

            if (device == null || device.IsPlaceholder || !device.IsSaved)
            {
                launchButton.Text = "投屏";
                launchButton.Icon = UiIcon.Mirror;
                launchButton.Kind = UiButtonKind.Primary;
                launchButton.Enabled = false;
                launchButton.AnimateIcon = false;
                launchButton.AccessibleName = "请先连接设备";
                launchButton.BackColor = UiTheme.Accent;
                launchButton.ForeColor = Color.White;
                launchButton.Invalidate();
                return;
            }

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
                launchButton.Text = "停止投屏";
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
                launchButton.Enabled = device.IsOnline && device.IsSaved;
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
            if (device == null || device.IsPlaceholder || !device.IsSaved)
                return;
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
            if (alreadyRunning && screenOffToggle.Checked)
                StartScreenControl(device);
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
                        if (screenOffToggle.Checked)
                            StartScreenControl(device);
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
            if (!device.IsSaved || device.RecoveryAttempts >= 2)
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
                            device.SessionGeneration != exitedGeneration || device.StopRequested ||
                            !device.IsSaved)
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
            DeviceCard[] snapshot = SavedDevices().ToArray();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                foreach (DeviceCard device in snapshot)
                    AdoptExistingMirror(device);
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
            if (device == null || device.IsPlaceholder || !device.IsSaved)
                return;
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
                    "--serial={0} --window-title=\"{1} USB Low Latency\" --video-codec=h264 --max-fps=60 --video-bit-rate=16M --video-buffer=0 --no-audio --shortcut-mod=lalt --window-x={2} --window-y={5} --window-width=450 --window-height=900 -V {4}{3}",
                    device.Serial, device.Name, device.LastWindowX, keyboardArg,
                    Diagnostics.DebugEnabled ? "debug" : "info", device.LastWindowY);

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
                foreach (DeviceCard device in SavedDevices())
                    ApplyScreenSettingToRunning(device, true);
                footerStatus.Text = "已开启“仅熄手机屏幕”；正在运行的投屏将热生效";
            }
            else
            {
                bool restartNeeded = false;
                foreach (DeviceCard device in SavedDevices())
                    restartNeeded = QueueScreenWakeRestart(device) || restartNeeded;
                if (restartNeeded)
                {
                    restartTimer.Stop();
                    restartTimer.Start();
                    footerStatus.Text = "正在释放实体屏幕并恢复投屏…";
                }
                else
                {
                    footerStatus.Text = "已关闭“仅熄手机屏幕”；手机实体屏幕正在恢复";
                }
            }
        }

        private bool QueueScreenWakeRestart(DeviceCard device)
        {
            device.ScreenDesiredOff = false;
            System.Threading.Interlocked.Increment(ref device.ScreenPowerRequestId);
            List<Process> running = FindMirrorProcesses(device);
            if (running.Count == 0)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(delegate { SendAndroidWake(device); });
                return false;
            }

            CaptureMirrorWindowPosition(device, running[0]);
            if (!pendingRestart.Contains(device))
                pendingRestart.Add(device);
            pendingWakeBeforeRestart.Add(device);
            device.StopRequested = true;
            device.SessionState = MirrorSessionState.Stopping;
            device.MirrorStopping = true;
            device.MirrorStarting = false;
            Diagnostics.Trace(device.Name, "mirror-restart-requested", "screen-wake");
            System.Threading.Interlocked.Increment(ref device.TopMostRequestId);
            StopMirrorProcesses(running, false);
            return true;
        }

        private void ApplyScreenSettingToRunning(DeviceCard device, bool turnOff)
        {
            // Retain the setting for a later launch without letting an idle device
            // overwrite the status message for the phone that is actually mirroring.
            device.ScreenDesiredOff = turnOff;
            if (IsProcessRunning(device.MirrorProcess))
                QueueScreenPower(device);
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
            foreach (DeviceCard device in DevicesSnapshot())
                ApplyTopMostToRunning(device);
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

        private void CenterActiveMirrorWindow()
        {
            DeviceCard device = activeDevice;
            if (device == null || device.IsPlaceholder)
                return;
            List<Process> running = FindMirrorProcesses(device);
            if (running.Count == 0)
                return;

            try
            {
                Process process = running[0];
                process.Refresh();
                IntPtr window = process.MainWindowHandle;
                NativeRect bounds;
                if (window == IntPtr.Zero || !GetWindowRect(window, out bounds))
                    return;

                int width = bounds.Right - bounds.Left;
                int height = bounds.Bottom - bounds.Top;
                if (width <= 0 || height <= 0)
                    return;

                Rectangle workingArea = Screen.FromHandle(Handle).WorkingArea;
                int x = workingArea.Left + Math.Max(0, (workingArea.Width - width) / 2);
                int y = workingArea.Top + Math.Max(0, (workingArea.Height - height) / 2);
                IntPtr insertAfter = alwaysOnTopToggle.Checked ? HwndTopMost : HwndNoTopMost;
                if (!SetWindowPos(window, insertAfter, x, y, 0, 0,
                    SwpNoSize | SwpNoActivate))
                    return;

                device.LastWindowX = x;
                device.LastWindowY = y;
                if (device.IsSaved)
                    PersistSavedDevices();
                Diagnostics.Trace(device.Name, "window-centered", "x=" + x + " y=" + y);
            }
            catch
            {
                // The mirror may exit between the double-click and the move.
            }
        }

        private void StartScreenControl(DeviceCard device)
        {
            device.ScreenDesiredOff = true;
            if (IsProcessRunning(device.MirrorProcess))
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
            if (IsProcessRunning(device.MirrorProcess))
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
                bool sent = desiredOff
                    ? SendMirrorScreenPower(device, false)
                    : SendAndroidWake(device);
                if (!sent && requestId == device.ScreenPowerRequestId)
                {
                    System.Threading.Thread.Sleep(150);
                    if (!IsDisposed && requestId == device.ScreenPowerRequestId)
                    {
                        sent = desiredOff
                            ? SendMirrorScreenPower(device, false)
                            : SendAndroidWake(device);
                    }
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

        private bool SendAndroidWake(DeviceCard device)
        {
            AdbResult result = new AdbClient(adbPath, device.Serial)
                .Shell("input keyevent 224", 5000);
            return result.Success;
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
                    IntPtr window = IntPtr.Zero;
                    for (int attempt = 0; attempt < 30; attempt++)
                    {
                        process.Refresh();
                        window = process.MainWindowHandle;
                        if (window != IntPtr.Zero)
                            break;
                        System.Threading.Thread.Sleep(100);
                    }
                    if (window == IntPtr.Zero)
                        return false;
                    IntPtr previousWindow = GetForegroundWindow();
                    if (!TrySetForegroundWindow(window))
                        return false;
                    try
                    {
                        System.Threading.Thread.Sleep(100);
                        bool sent = SendScreenPowerShortcut(turnOn);
                        // SendInput queues the keystrokes. Keep scrcpy focused
                        // briefly so SDL consumes the shortcut before focus is restored.
                        System.Threading.Thread.Sleep(150);
                        return sent;
                    }
                    finally
                    {
                        if (previousWindow != IntPtr.Zero && previousWindow != window)
                            TrySetForegroundWindow(previousWindow);
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

        private static bool TrySetForegroundWindow(IntPtr window)
        {
            if (window == IntPtr.Zero)
                return false;

            uint ignoredProcessId;
            uint targetThread = GetWindowThreadProcessId(window, out ignoredProcessId);
            IntPtr currentForeground = GetForegroundWindow();
            uint foregroundThread = currentForeground == IntPtr.Zero
                ? 0
                : GetWindowThreadProcessId(currentForeground, out ignoredProcessId);
            uint currentThread = GetCurrentThreadId();
            bool attachedForeground = false;
            bool attachedTarget = false;
            try
            {
                if (foregroundThread != 0 && foregroundThread != currentThread)
                    attachedForeground = AttachThreadInput(currentThread, foregroundThread, true);
                if (targetThread != 0 && targetThread != currentThread && targetThread != foregroundThread)
                    attachedTarget = AttachThreadInput(currentThread, targetThread, true);
                BringWindowToTop(window);
                SetForegroundWindow(window);
                SetFocus(window);
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    if (GetForegroundWindow() == window)
                        return true;
                    System.Threading.Thread.Sleep(20);
                }
                return false;
            }
            finally
            {
                if (attachedTarget)
                    AttachThreadInput(currentThread, targetThread, false);
                if (attachedForeground)
                    AttachThreadInput(currentThread, foregroundThread, false);
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
            if (device == null || device.IsPlaceholder || !device.IsSaved)
                return;
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
            foreach (DeviceCard device in DevicesSnapshot())
            {
                if (processId == device.MirrorProcessId)
                    return device;
            }
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

            foreach (DeviceCard device in DevicesSnapshot())
                QueueRestartIfRunning(device);

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

            CaptureMirrorWindowPosition(device, running[0]);

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

        private void CaptureMirrorWindowPosition(DeviceCard device, Process process)
        {
            try
            {
                process.Refresh();
                IntPtr window = process.MainWindowHandle;
                NativeRect bounds;
                if (window != IntPtr.Zero && GetWindowRect(window, out bounds) &&
                    bounds.Right > bounds.Left && bounds.Bottom > bounds.Top)
                {
                    device.LastWindowX = bounds.Left;
                    device.LastWindowY = bounds.Top;
                    if (device.IsSaved)
                        PersistSavedDevices();
                    Diagnostics.Trace(device.Name, "window-position-captured",
                        "x=" + bounds.Left + " y=" + bounds.Top);
                }
            }
            catch { }
        }

        private void RestartMirrors(object sender, EventArgs e)
        {
            restartTimer.Stop();
            DeviceCard[] devices = pendingRestart.ToArray();
            pendingRestart.Clear();
            bool restoringScreen = false;

            foreach (DeviceCard device in devices)
            {
                bool wakeBeforeLaunch = pendingWakeBeforeRestart.Remove(device);
                StopMirrorProcesses(FindMirrorProcesses(device), true);
                if (!device.IsSaved)
                    continue;
                if (!wakeBeforeLaunch)
                {
                    if (IsUsbDeviceOnline(device.Serial))
                        LaunchDevice(device);
                    continue;
                }

                restoringScreen = true;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    bool woke = SendAndroidWake(device);
                    if (IsDisposed)
                        return;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            if (IsUsbDeviceOnline(device.Serial))
                                LaunchDevice(device);
                            footerStatus.Text = woke
                                ? device.Name + "：实体屏幕已恢复，可解锁"
                                : device.Name + "：投屏已重建，请确认实体屏幕已亮起";
                        });
                    }
                    catch { }
                });
            }

            if (devices.Length > 0)
            {
                footerStatus.Text = restoringScreen
                    ? "正在唤醒实体屏幕并重建投屏…"
                    : "已热切换为“" + KeyboardModeName() + "”";
            }
        }

        private List<Process> FindMirrorProcesses(DeviceCard device)
        {
            List<Process> processes = new List<Process>();
            HashSet<int> processIds = new HashSet<int>();
            if (device == null || string.IsNullOrWhiteSpace(device.Serial))
                return processes;

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

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(
            uint threadIdAttach,
            uint threadIdAttachTo,
            bool attach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, Input[] inputs, int size);

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint Type;
            public InputUnion Data;
        }

        // Win32 INPUT contains a union whose largest member is MOUSEINPUT.
        // Declaring every native union member keeps sizeof(INPUT) at 40 bytes
        // on x64 and 28 bytes on x86. A keyboard-only union is too small, so
        // SendInput fails with ERROR_INVALID_PARAMETER.
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KeyboardInput Keyboard;

            [FieldOffset(0)]
            public MouseInput Mouse;

            [FieldOffset(0)]
            public HardwareInput Hardware;
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
        private struct MouseInput
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HardwareInput
        {
            public uint Message;
            public ushort ParameterLow;
            public ushort ParameterHigh;
        }

        internal static bool IsScreenPowerInputInteropValid
        {
            get
            {
                int expectedSize = IntPtr.Size == 8 ? 40 : 28;
                return Marshal.SizeOf(typeof(Input)) == expectedSize;
            }
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
                if (!MainForm.IsScreenPowerInputInteropValid)
                    return 3;
                string directory = AppDomain.CurrentDomain.BaseDirectory;
                string runtime = Path.Combine(directory, "scrcpy");
                return DeviceRegistry.RunSelfTest(
                    Path.Combine(runtime, "adb.exe"),
                    Path.Combine(runtime, "scrcpy.exe")) ? 0 : 2;
            }

            Application.Run(new MainForm());
            return 0;
        }
    }
}
