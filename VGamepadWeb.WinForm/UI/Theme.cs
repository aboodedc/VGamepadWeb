using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace VGamepadWeb.WinForm.UI
{
    /// <summary>
    /// 手绘界面的唯一视觉出处：色板、字体、圆角路径、DPI 与灰阶工具。
    /// 色值取自参考图 C:\Users\lanze\Downloads\VGamepadWeb_样式升级 (1).html 的 :root（浅色主题）。
    /// </summary>
    public static class Theme
    {
        // ===================== 色板（浅色，全部平色） =====================
        // 色值取自参考图 VGamepadWeb_样式升级 (1).html 的 :root；只做平色填充，不做渐变与阴影。
        public static readonly Color Page = Hex(0xF2F3F5);
        public static readonly Color Window = Hex(0xFFFFFF);
        public static readonly Color Panel = Hex(0xFFFFFF);
        public static readonly Color PanelSoft = Hex(0xF3F4F6);
        public static readonly Color Button = Hex(0xFFFFFF);
        public static readonly Color ButtonHover = Hex(0xF0F1F3);
        public static readonly Color Line = Hex(0xD1D5DB);
        public static readonly Color LineStrong = Hex(0xD1D5DB);
        public static readonly Color Text = Hex(0x1C1F24);

        // 次要文字统一下限 #6B7280；比它还浅的灰只留给纯装饰（占位图标、破折号）。
        public static readonly Color Muted = Hex(0x6B7280);
        public static readonly Color Faint = Hex(0x9CA3AF);

        // 彩色只落在三处：启动按钮的闷绿、停止与端口/链接的蓝红、槽位底纹。
        public static readonly Color Green = Hex(0x2F7D4F);
        public static readonly Color GreenHover = Hex(0x276A43);
        public static readonly Color Red = Hex(0xC0453E);
        public static readonly Color RedSoft = Hex(0xFBF1F0);
        public static readonly Color Amber = Hex(0x9A6B14);
        public static readonly Color Accent = Hex(0x2B5EA7);
        public static readonly Color AccentBg = Hex(0xEDF2F8);
        public static readonly Color AccentLine = Hex(0xC7D3E6);
        public static readonly Color AccentHover = Hex(0xE2EBF5);

        // 手柄品牌色：Xbox 绿、PS 蓝，只用于槽位图标、编号和浅底。
        public static readonly Color Xbox = Hex(0x107C10);
        public static readonly Color XboxText = Hex(0x14532D);
        public static readonly Color XboxBg = Hex(0xEEF4EE);
        public static readonly Color XboxLine = Hex(0xC8DCC8);
        public static readonly Color Ps = Hex(0x2F5496);
        public static readonly Color PsText = Hex(0x1E3A5F);
        public static readonly Color PsBg = Hex(0xEDF1F7);
        public static readonly Color PsLine = Hex(0xC5D2E6);

        public static readonly Color DotIdle = Hex(0xC3C8CF);
        public static readonly Color CardIdle = Hex(0xFAFAFA);
        public static readonly Color DisabledBack = Hex(0xF4F5F6);
        public static readonly Color DisabledText = Hex(0x9AA0A8);
        public static readonly Color QrBack = Hex(0xFFFFFF);

        // Toast 沿用参考图的深色浮层。
        public static readonly Color ToastBack = Hex(0x323232);
        public static readonly Color ToastBorder = Hex(0x1C1C1C);
        public static readonly Color ToastText = Hex(0xF5F5F5);
        public static readonly Color ToastOk = Hex(0x8FD48F);

        // 圆角统一 4px，不再大小不一。
        public const float RadiusCard = 4f;
        public const float RadiusSm = 4f;
        public const float RadiusInput = 4f;

        // ===================== 字体 =====================
        private static readonly object FontGate = new object();
        private static string _monoFamily;
        private static string _sansFamily;

        public static string MonoFamily
        {
            get
            {
                EnsureFontFamilies();
                return _monoFamily;
            }
        }

        public static string SansFamily
        {
            get
            {
                EnsureFontFamilies();
                return _sansFamily;
            }
        }

        /// <summary>等宽（地址、编号、端口、延迟）。字号按像素给；取整后再建字体，ClearType 才锐利。</summary>
        public static Font Mono(float pixelSize, FontStyle style = FontStyle.Regular)
        {
            return new Font(MonoFamily, NormalizeSize(pixelSize), style, GraphicsUnit.Pixel);
        }

        /// <summary>正文：Segoe UI / Microsoft YaHei 回退，保证中文字形。</summary>
        public static Font Sans(float pixelSize, FontStyle style = FontStyle.Regular)
        {
            return new Font(SansFamily, NormalizeSize(pixelSize), style, GraphicsUnit.Pixel);
        }

        /// <summary>字号取整到像素：半像素字号会让 GDI+ 把字栅格化到半格上，看着就是“糊”。</summary>
        private static float NormalizeSize(float pixelSize)
        {
            float size = (float)Math.Round(pixelSize);
            return size < 1f ? 1f : size;
        }

        private static void EnsureFontFamilies()
        {
            if (_monoFamily != null && _sansFamily != null)
            {
                return;
            }

            lock (FontGate)
            {
                if (_monoFamily == null)
                {
                    _monoFamily = PickFamily(
                        new[] { "Consolas", "Cascadia Mono", "Cascadia Code", "Courier New" },
                        "Consolas");
                }

                if (_sansFamily == null)
                {
                    _sansFamily = PickFamily(
                        new[] { "Segoe UI", "Microsoft YaHei UI", "Microsoft YaHei", "Tahoma" },
                        "Segoe UI");
                }
            }
        }

        private static string PickFamily(string[] candidates, string fallback)
        {
            try
            {
                using (InstalledFontCollection installed = new InstalledFontCollection())
                {
                    FontFamily[] families = installed.Families;
                    foreach (string candidate in candidates)
                    {
                        for (int i = 0; i < families.Length; i++)
                        {
                            if (string.Equals(families[i].Name, candidate, StringComparison.OrdinalIgnoreCase))
                            {
                                return families[i].Name;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 枚举字体失败（受限环境）时交给 GDI+ 默认回退，界面不能因此崩。
            }

            return candidates.Length > 0 ? candidates[0] : fallback;
        }

        // ===================== 几何 =====================
        /// <summary>圆角矩形路径；宽高非正时返回空路径（Fill/Draw 自然变成空操作）。</summary>
        public static GraphicsPath RoundRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (rect.Width <= 0f || rect.Height <= 0f)
            {
                return path;
            }

            float diameter = Math.Min(Math.Min(radius * 2f, rect.Width), rect.Height);
            if (diameter <= 1f)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180f, 90f);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270f, 90f);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0f, 90f);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        public static void FillRoundRect(Graphics graphics, RectangleF rect, float radius, Color fill)
        {
            if (graphics == null || rect.Width <= 0f || rect.Height <= 0f)
            {
                return;
            }

            using (GraphicsPath path = RoundRect(rect, radius))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                graphics.FillPath(brush, path);
            }
        }

        public static void DrawRoundRect(Graphics graphics, RectangleF rect, float radius, Color stroke, float width)
        {
            if (graphics == null || rect.Width <= 0f || rect.Height <= 0f)
            {
                return;
            }

            using (GraphicsPath path = RoundRect(rect, radius))
            using (Pen pen = new Pen(stroke, width > 0f ? width : 1f))
            {
                graphics.DrawPath(pen, path);
            }
        }

        // ===================== DPI =====================
        /// <summary>控件当前的缩放比例：DeviceDpi / 96。布局只算一次，所有设计像素统一乘它。</summary>
        public static float Scale(Control control)
        {
            if (control == null)
            {
                return 1f;
            }

            int dpi = control.DeviceDpi;
            return dpi > 0 ? dpi / 96f : 1f;
        }

        public static float Px(float designPixels, float scale)
        {
            return designPixels * scale;
        }

        // ===================== 灰阶 / 混色 =====================
        public static Color Alpha(Color color, int alpha)
        {
            return Color.FromArgb(Channel(alpha), color.R, color.G, color.B);
        }

        public static Color Blend(Color from, Color to, float amount)
        {
            if (amount <= 0f)
            {
                return from;
            }

            if (amount >= 1f)
            {
                return to;
            }

            return Color.FromArgb(
                Mix(from.A, to.A, amount),
                Mix(from.R, to.R, amount),
                Mix(from.G, to.G, amount),
                Mix(from.B, to.B, amount));
        }

        /// <summary>factor &lt; 1 压暗，&gt; 1 提亮；alpha 原样保留。</summary>
        public static Color Shade(Color color, float factor)
        {
            if (factor < 0f)
            {
                factor = 0f;
            }

            return Color.FromArgb(
                color.A,
                Channel(color.R * (double)factor),
                Channel(color.G * (double)factor),
                Channel(color.B * (double)factor));
        }

        public static Color Disabled(Color color)
        {
            return Blend(color, Faint, 0.55f);
        }

        private static int Mix(int from, int to, float amount)
        {
            return Channel(from + (to - from) * (double)amount);
        }

        private static int Channel(double value)
        {
            if (value <= 0d)
            {
                return 0;
            }

            if (value >= 255d)
            {
                return 255;
            }

            return (int)Math.Round(value);
        }

        private static Color Hex(int rgb)
        {
            return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }
    }
}
