using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VGamepadWeb.WinForm.UI
{
    /// <summary>
    /// 手绘界面的唯一视觉出处：色板、字体、圆角路径、DPI 与灰阶工具。
    /// 色值取自参考图 C:\Users\lanze\Downloads\VGamepadWeb_样式升级 (1).html 的 :root（浅色主题）。
    /// 色板有两套，运行时用 <see cref="UseTheme"/> 切换：界面代码只读 Theme.X，不关心当前是哪套。
    /// </summary>
    public static class Theme
    {
        // ===================== 色板（浅色 / 暗色，全部平色） =====================
        // 浅色色值取自参考图 VGamepadWeb_样式升级 (1).html 的 :root；只做平色填充，不做渐变与阴影。
        // 暗色是同一套语义的另一组值：底色压深、文字提亮、强调色提饱和，字段名一个都不变。
        public static Color Page;
        public static Color Window;
        public static Color Panel;
        public static Color PanelSoft;
        public static Color Button;
        public static Color ButtonHover;
        public static Color Line;
        public static Color LineStrong;
        public static Color Text;

        // 次要文字统一下限 #6B7280（暗色 #A3A9B2）；比它还浅的灰只留给纯装饰（占位图标、破折号）。
        public static Color Muted;
        public static Color Faint;

        // 彩色只落在三处：启动按钮的闷绿、停止与端口/链接的蓝红、槽位底纹。
        // OnGreen 是「绿底上的字」：浅色主题用白，暗色主题的绿调亮了，白字会糊，得反过来用深字。
        public static Color Green;
        public static Color GreenHover;
        public static Color OnGreen;
        public static Color Red;
        public static Color RedSoft;
        public static Color Amber;
        public static Color Accent;
        public static Color AccentBg;
        public static Color AccentLine;
        public static Color AccentHover;

        // 手柄品牌色：Xbox 绿、PS 蓝，只用于槽位图标、编号和浅底。
        public static Color Xbox;
        public static Color XboxText;
        public static Color XboxBg;
        public static Color XboxLine;
        public static Color Ps;
        public static Color PsText;
        public static Color PsBg;
        public static Color PsLine;

        public static Color DotIdle;
        public static Color CardIdle;
        public static Color DisabledBack;
        public static Color DisabledText;

        // 页脚主题开关的胶囊底：比页面深一档，白色页面上才看得出是一颗「按钮」。
        public static Color ToggleBack;

        // 二维码永远白底黑模块：暗色主题下也保持白，否则手机扫不出来。
        public static Color QrBack;

        // Toast 本身就是深色浮层：浅色主题沿用参考图的 #323232，暗色主题略微提亮一档和页面分开。
        public static Color ToastBack;
        public static Color ToastBorder;
        public static Color ToastText;
        public static Color ToastOk;

        /// <summary>当前是不是暗色主题。只读，切换统一走 <see cref="UseTheme"/>。</summary>
        public static bool Dark { get; private set; }

        /// <summary>
        /// 系统是不是偏好暗色：读 HKCU 的 AppsUseLightTheme（0 = 暗色）。
        /// 键不存在（老系统）或读不到（受限账户）一律当浅色，启动不能靠它决定成败。
        /// </summary>
        public static bool SystemPrefersDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key != null ? key.GetValue("AppsUseLightTheme") : null;
                    if (value is int light)
                    {
                        return light == 0;
                    }
                }
            }
            catch (Exception)
            {
                // 读注册表失败：按浅色处理，界面照常。
            }

            return false;
        }

        static Theme()
        {
            UseTheme(false);
        }

        /// <summary>
        /// 切到暗色 / 浅色。这里只改配色值，重画由调用方负责（WinForm 里是 ConsoleView.ApplyTheme）。
        /// </summary>
        public static void UseTheme(bool dark)
        {
            Dark = dark;

            if (dark)
            {
                Page = Hex(0x14161A);
                Window = Hex(0x1A1D21);
                Panel = Hex(0x1A1D21);
                PanelSoft = Hex(0x23262B);
                Button = Hex(0x262A30);
                ButtonHover = Hex(0x31363D);
                Line = Hex(0x33383F);
                LineStrong = Hex(0x3E444C);
                Text = Hex(0xE6E8EB);
                Muted = Hex(0xA3A9B2);
                Faint = Hex(0x767C85);
                Green = Hex(0x53BA77);
                GreenHover = Hex(0x63C98A);
                OnGreen = Hex(0x0E1A12);
                Red = Hex(0xE2695F);
                RedSoft = Hex(0x2E2020);
                Amber = Hex(0xE0B45C);
                Accent = Hex(0x7FA9E8);
                AccentBg = Hex(0x22303F);
                AccentLine = Hex(0x36485E);
                AccentHover = Hex(0x2C3D51);
                Xbox = Hex(0x4FB854);
                XboxText = Hex(0xA8DCA9);
                XboxBg = Hex(0x1E2A1F);
                XboxLine = Hex(0x2F4230);
                Ps = Hex(0x7CA5E0);
                PsText = Hex(0xB7CDF0);
                PsBg = Hex(0x1E2633);
                PsLine = Hex(0x2C3A4E);
                DotIdle = Hex(0x4B515A);
                CardIdle = Hex(0x20242A);
                DisabledBack = Hex(0x1F2227);
                DisabledText = Hex(0x6E747D);
                ToggleBack = Hex(0x23262B);
                QrBack = Hex(0xFFFFFF);
                ToastBack = Hex(0x2E3136);
                ToastBorder = Hex(0x4A4F57);
                ToastText = Hex(0xF0F2F5);
                ToastOk = Hex(0x8FD48F);
                return;
            }

            Page = Hex(0xF2F3F5);
            Window = Hex(0xFFFFFF);
            Panel = Hex(0xFFFFFF);
            PanelSoft = Hex(0xF3F4F6);
            Button = Hex(0xFFFFFF);
            ButtonHover = Hex(0xF0F1F3);
            Line = Hex(0xD1D5DB);
            LineStrong = Hex(0xD1D5DB);
            Text = Hex(0x1C1F24);
            Muted = Hex(0x6B7280);
            Faint = Hex(0x9CA3AF);
            Green = Hex(0x2F7D4F);
            GreenHover = Hex(0x276A43);
            OnGreen = Hex(0xFFFFFF);
            Red = Hex(0xC0453E);
            RedSoft = Hex(0xFBF1F0);
            Amber = Hex(0x9A6B14);
            Accent = Hex(0x2B5EA7);
            AccentBg = Hex(0xEDF2F8);
            AccentLine = Hex(0xC7D3E6);
            AccentHover = Hex(0xE2EBF5);
            Xbox = Hex(0x107C10);
            XboxText = Hex(0x14532D);
            XboxBg = Hex(0xEEF4EE);
            XboxLine = Hex(0xC8DCC8);
            Ps = Hex(0x2F5496);
            PsText = Hex(0x1E3A5F);
            PsBg = Hex(0xEDF1F7);
            PsLine = Hex(0xC5D2E6);
            DotIdle = Hex(0xC3C8CF);
            CardIdle = Hex(0xFAFAFA);
            DisabledBack = Hex(0xF4F5F6);
            DisabledText = Hex(0x9AA0A8);
            ToggleBack = Hex(0xE9EAEC);
            QrBack = Hex(0xFFFFFF);
            ToastBack = Hex(0x323232);
            ToastBorder = Hex(0x1C1C1C);
            ToastText = Hex(0xF5F5F5);
            ToastOk = Hex(0x8FD48F);
        }

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

        private static Color Hex(int rgb)
        {
            return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }
    }
}
