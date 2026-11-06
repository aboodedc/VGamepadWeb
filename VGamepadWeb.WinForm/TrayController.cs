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

            var menu = new ContextMenuStrip();
            menu.Items.Add(_showItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_exitItem);

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
        }

        /// <summary>
        /// 关闭窗口后的原生提示：走 Shell_NotifyIcon 的气泡通知，
        /// Win10/11 上由系统直接渲染成通知中心里的那条，不需要额外依赖。
        /// </summary>
        public void ShowMinimizedHint(string title, string message)
        {
            _notifyIcon.ShowBalloonTip(4000, title, message, ToolTipIcon.Info);
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
