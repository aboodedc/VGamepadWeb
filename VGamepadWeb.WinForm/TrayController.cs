using VGamepadWeb.WinForm.UI;

namespace VGamepadWeb.WinForm
{
    /// <summary>
    /// 托盘只做两件事：显示窗口 / 退出。刻意不做启动/停止服务，也不做开机自启。
    ///
    /// 图标取窗口自身的 Icon（Form1.resx 的 $this.Icon）并克隆一份：
    /// NotifyIcon.Dispose 会销毁它拿到的 Icon，直接用 form.Icon 会让窗口图标失效。
    ///
    /// 文案走 UI/Strings（tray.show / tray.exit）；语言切换时调 <see cref="ApplyLanguage"/>
    /// 就地改这两项的 Text，不重建 NotifyIcon。
    /// </summary>
    public sealed class TrayController : IDisposable    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Icon _ownedIcon;
        private readonly bool _ownsIcon;
        private readonly ToolStripMenuItem _showItem;
        private readonly ToolStripMenuItem _exitItem;

        public TrayController(Form form, Action showWindow, Action exit, string language)
        {
            if (form.Icon != null)
            {
                _ownedIcon = (Icon)form.Icon.Clone();
                _ownsIcon = true;
            }
            else
            {
                _ownedIcon = SystemIcons.Application;
                _ownsIcon = false;
            }

            _showItem = new ToolStripMenuItem(Strings.T(language, "tray.show"), null, (_, _) => showWindow());
            _exitItem = new ToolStripMenuItem(Strings.T(language, "tray.exit"), null, (_, _) => exit());

            // 托盘菜单没有图标，关掉左侧图标边距：那一列默认是浅色渐变，暗色主题下会留一条白带。
            var menu = new ContextMenuStrip { ShowImageMargin = false, Renderer = new ThemeMenuRenderer() };
            menu.Items.Add(_showItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_exitItem);

            // 阿拉伯语下菜单从右往左展开，文字也跟着贴右。
            menu.RightToLeft = Strings.IsRtl(language) ? RightToLeft.Yes : RightToLeft.No;

            _notifyIcon = new NotifyIcon
            {
                Icon = _ownedIcon,
                Text = "VGamepadWeb",
                ContextMenuStrip = menu,
                Visible = true
            };

            _notifyIcon.DoubleClick += (_, _) => showWindow();
        }

        /// <summary>只改两项菜单文案，不动 NotifyIcon 本身。</summary>
        public void ApplyLanguage(string language)
        {
            _showItem.Text = Strings.T(language, "tray.show");
            _exitItem.Text = Strings.T(language, "tray.exit");

            // 语言换了方向，菜单也要跟着翻。
            if (_notifyIcon.ContextMenuStrip != null)
            {
                _notifyIcon.ContextMenuStrip.RightToLeft = Strings.IsRtl(language) ? RightToLeft.Yes : RightToLeft.No;
            }
        }

        /// <summary>
        /// 关闭窗口后的原生提示：走 Shell_NotifyIcon 的气泡通知，
        /// Win10/11 上由系统直接渲染成通知中心里的那条，不需要额外依赖。
        ///
        /// 气泡的排版方向由 Windows 自己的语言设置决定，应用没法直接指定 ——
        /// 所以阿语文案要套上 Unicode 方向标记（U+202B/U+202C），
        /// 系统才会按从右往左排。标记不占位、不画笔画。
        /// </summary>
        public void ShowMinimizedHint(string title, string message, string language)
        {
            _notifyIcon.ShowBalloonTip(
                4000,
                Strings.WithDirectionMarks(title, language),
                Strings.WithDirectionMarks(message, language),
                ToolTipIcon.Info);
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();

            if (_ownsIcon)
            {
                _ownedIcon.Dispose();
            }
        }
    }
}
