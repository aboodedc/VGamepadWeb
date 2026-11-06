using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;

namespace VGamepadWeb.WinForm.UI
{
    /// <summary>
    /// 浅色版控制台：整窗 GDI+ 自绘，只读 <see cref="UiModel"/> 渲染，交互一律以事件交回宿主。
    ///
    /// 设计基准 980×580（两行式：第一行服务面板「地址 | 端口密码 | 启停」，
    /// 第二行「配置列 + 手柄槽位网格」），按 fit-scale = min(客户宽/980, 客户高/580)
    /// 等比缩放后整页居中；所有矩形/文字原点/画笔位置都取整到像素，
    /// 配合整数像素字号保证 ClearType 锐利。
    ///
    /// 尺寸纪律：外边距 16、面板内边距 20、组间距 16、标签到控件 8；
    /// 输入框 / 下拉 / 次按钮一律 36px 高，主按钮 40px，圆角一律 4px。
    /// </summary>
    public sealed class ConsoleView : Control
    {
        private const float BaseWidth = 980f;
        private const float BaseHeight = 580f;

        // 与宿主约定的 Toast 等级（不是文案）。
        private const string ToastInfo = "info";
        private const string ToastSuccess = "success";
        private const string ToastWarn = "warn";
        private const string ToastError = "error";

        private const float ToastLifeMilliseconds = 2200f;

        // WS_CLIPCHILDREN：父控件重绘时裁掉子控件区域，真 TextBox / ComboBox 才不会跟着闪。
        private const int WsClipChildren = 0x02000000;

        // 排版用的固定符号；中文文案一律走 Strings.T。
        private const string Dash = "—";
        private const string MiddleDot = " · ";
        private const string IdPrefix = "ID ";

        // ===================== 状态 =====================
        private UiModel _model = new UiModel();
        private Image _qrImage;

        private readonly TextBox _portBox;
        private readonly TextBox _passBox;
        private readonly Button _pwdToggle;
        private readonly ContextMenuStrip _langMenu;

        private Font _portFont;
        private Font _passFont;
        private Font _pwdFont;
        private float _inputFontScale = -1f;

        private readonly System.Windows.Forms.Timer _toastTimer;
        private string _toastText;
        private string _toastLevel = ToastInfo;

        private string _hoverId;
        private bool _passwordVisible;
        private bool _portInvalid;

        /// <summary>当前语言是否从右往左读（阿拉伯语）：布局镜像 + 文字 RTL 排版。</summary>
        private bool _rtl;

        private Frame _frame;
        private bool _frameDirty = true;

        // 上次真正画出来的内容签名：和这次一样就跳过重绘。
        private string _paintedSignature;
        private Bitmap _measureBitmap;
        private Graphics _measureGraphics;

        // ===================== 公开契约 =====================
        public event Action ToggleServer;
        public event Action CopyAddress;
        public event Action<int, string> CommitCredentials;
        public event Action<string> LanguageChanged;
        public event Action<int> DisconnectSlot;
        public event Action<int> RemoveDevice;

        public ConsoleView()
        {
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable,
                true);

            BackColor = Theme.Page;
            ForeColor = Theme.Text;
            TabStop = false;

            _portBox = CreateInputBox();
            _portBox.KeyPress += PortKeyPress;
            _portBox.TextChanged += PortTextChanged;
            _portBox.Enter += InputFocusChanged;
            _portBox.Leave += InputLeave;
            _portBox.KeyDown += CredentialKeyDown;

            _passBox = CreateInputBox();
            _passBox.UseSystemPasswordChar = true;
            _passBox.Enter += InputFocusChanged;
            _passBox.Leave += InputLeave;
            _passBox.KeyDown += CredentialKeyDown;
            _passBox.TextChanged += PassTextChanged;

            // 显隐用真的 Button，不往 TextBox 里画字：Text 由语言和状态驱动。
            _pwdToggle = CreateLinkButton();
            _pwdToggle.Click += PasswordToggleClick;

            _langMenu = CreateLanguageMenu();

            Controls.Add(_portBox);
            Controls.Add(_passBox);
            Controls.Add(_pwdToggle);

            _toastTimer = new System.Windows.Forms.Timer { Interval = (int)ToastLifeMilliseconds };
            _toastTimer.Tick += ToastTimerTick;

            SyncInputText();
            SyncPasswordToggle();
        }

        /// <summary>数据入口（UI 线程调用）：只保存引用 + 重画；qrImage 可为 null，图片由宿主负责释放。</summary>
        public void Apply(UiModel model, Image qrImage)
        {
            if (IsDisposed)
            {
                return;
            }

            _model = model ?? new UiModel();
            _qrImage = qrImage;

            // 阿语是从右往左读的：布局镜像 + 文字 RTL 排版，两件事一起切。
            _rtl = Strings.IsRtl(Lang);

            // 宿主每 500ms 递一份快照上来；画面内容没变就别重绘，否则整窗跟着抖。
            string signature = BuildSignature();
            if (!_frameDirty && string.Equals(signature, _paintedSignature, StringComparison.Ordinal))
            {
                return;
            }

            _paintedSignature = signature;

            // 这里只存引用；输入框文本/语言选择由 OnPaint → SyncInputs 统一补。
            InvalidateFrame();
            Invalidate();
        }

        /// <summary>底部浮层：2.2 秒自动消失，新消息直接覆盖旧的。</summary>
        public void ShowToast(string text, string level)
        {
            if (IsDisposed || _toastTimer == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            _toastText = text;
            _toastLevel = string.IsNullOrEmpty(level) ? ToastInfo : level;
            _toastTimer.Stop();
            _toastTimer.Start();
            Invalidate();
        }

        /// <summary>父控件重绘时裁掉子控件区域，避免每次全窗重绘都把真 TextBox / ComboBox 盖一遍。</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= WsClipChildren;
                return cp;
            }
        }

        /// <summary>把「画面上会变的一切」压成一个签名；和上次一致就不重绘。</summary>
        private string BuildSignature()
        {
            StringBuilder sb = new StringBuilder(192);
            string lang = Lang;

            sb.Append((int)_model.Phase).Append('|');
            sb.Append(StatusMetaText(lang)).Append('|');
            sb.Append(_model.Port).Append('|');
            sb.Append(lang).Append('|');
            sb.Append(_model.Version).Append('|');
            sb.Append(_model.Ipv4).Append('|');
            sb.Append(LocalIpAddress()).Append('|');
            sb.Append(LocalIpSuffix(lang)).Append('|');
            sb.Append(_qrImage != null ? '1' : '0').Append('|');

            List<IpItem> ips = _model.Ips;
            if (ips != null)
            {
                for (int i = 0; i < ips.Count; i++)
                {
                    IpItem ip = ips[i];
                    if (ip == null)
                    {
                        continue;
                    }

                    sb.Append(ip.Ip).Append(':').Append(ip.Kind).Append(ip.Current ? '*' : '-').Append(';');
                }
            }

            List<SlotItem> slots = _model.Slots;
            if (slots != null)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    SlotItem slot = slots[i];
                    if (slot == null)
                    {
                        continue;
                    }

                    sb.Append(slot.Slot).Append(':').Append(slot.Occupied ? '1' : '0').Append(slot.Type).Append(':')
                      .Append(slot.ControllerId).Append(':')
                      .Append(slot.DeviceName).Append(':')
                      .Append(slot.RemoteIp)                      .Append(';');
                }
            }

            return sb.ToString();
        }

        // ===================== 基础属性 =====================
        private string Lang
        {
            get { return string.IsNullOrEmpty(_model.Language) ? Languages.Chinese : _model.Language; }
        }

        private bool InputLocked
        {
            get { return _model.IsRunning || _model.IsBusy; }
        }

        private string PowerText(string lang)
        {
            switch (_model.Phase)
            {
                case ServerPhase.Starting:
                    return Strings.T(lang, "power.starting");
                case ServerPhase.Stopping:
                    return Strings.T(lang, "power.stopping");
                case ServerPhase.Running:
                    return Strings.T(lang, "power.stop");
                default:
                    return Strings.T(lang, "power.start");
            }
        }

        private string StateKey()
        {
            switch (_model.Phase)
            {
                case ServerPhase.Starting:
                    return "state.starting";
                case ServerPhase.Stopping:
                    return "state.stopping";
                case ServerPhase.Running:
                    return "state.running";
                default:
                    return "state.stopped";
            }
        }

        private static string FormatUptime(DateTimeOffset startedAt)
        {
            TimeSpan span = DateTimeOffset.Now - startedAt;
            if (span.Ticks < 0)
            {
                span = TimeSpan.Zero;
            }

            return string.Format("{0:00}:{1:00}:{2:00}", (long)span.TotalHours, span.Minutes, span.Seconds);
        }

        /// <summary>本机地址里优先「当前使用」的那条；没有就用第一条。</summary>
        private IpItem PreferredIp()
        {
            List<IpItem> ips = _model.Ips;
            if (ips == null)
            {
                return null;
            }

            for (int i = 0; i < ips.Count; i++)
            {
                if (ips[i] != null && ips[i].Current)
                {
                    return ips[i];
                }
            }

            for (int i = 0; i < ips.Count; i++)
            {
                if (ips[i] != null)
                {
                    return ips[i];
                }
            }

            return null;
        }

        private string LocalIpAddress()
        {
            if (!string.IsNullOrEmpty(_model.Ipv4))
            {
                return _model.Ipv4;
            }

            IpItem preferred = PreferredIp();
            return preferred != null ? (preferred.Ip ?? string.Empty) : string.Empty;
        }

        private string LocalIpSuffix(string lang)
        {
            IpItem preferred = PreferredIp();
            string kind = preferred != null ? Strings.IpLabel(lang, preferred.Kind) : string.Empty;
            return string.IsNullOrEmpty(kind) ? string.Empty : " (" + kind + ")";
        }

        private string StatusMetaText(string lang)
        {
            if (_model.IsRunning && _model.StartedAt.HasValue)
            {
                return Strings.T(lang, "uptime", FormatUptime(_model.StartedAt.Value))
                    + MiddleDot
                    + Strings.T(lang, "status.online", _model.Online);
            }

            if (_model.IsBusy)
            {
                return string.Empty;
            }

            return Strings.T(lang, "state.stoppedHint");
        }

        // ===================== 输入框 / 下拉 =====================
        private static TextBox CreateInputBox()
        {
            return new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Panel,
                ForeColor = Theme.Text,
                ShortcutsEnabled = true,
                TextAlign = HorizontalAlignment.Left,

                // 输入框一律不进 Tab 序列：窗体激活时系统会把初始焦点给第一个可 Tab 的
                // 子控件，那样端口框会莫名其妙地亮起光标。输入框靠点击获得焦点。
                TabStop = false
            };
        }

        /// <summary>无边框文字按钮：白底蓝字，FlatStyle 关掉系统边框，只留文字。</summary>
        private static Button CreateLinkButton()
        {
            Button button = new Button
            {
                AutoSize = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Panel,
                ForeColor = Theme.Accent,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 6, 0),
                TabStop = false,
                UseVisualStyleBackColor = false
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Theme.ButtonHover;
            button.FlatAppearance.MouseDownBackColor = Theme.ButtonHover;
            return button;
        }

        /// <summary>语言下拉用自绘字段 + ContextMenuStrip，避开系统 ComboBox 的灰方块箭头。</summary>
        private ContextMenuStrip CreateLanguageMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };

            for (int i = 0; i < Languages.All.Length; i++)
            {
                string code = Languages.All[i];
                ToolStripMenuItem item = new ToolStripMenuItem(Strings.LanguageName(code)) { Tag = code };
                item.Click += LanguageItemClick;
                menu.Items.Add(item);
            }

            menu.Closed += (_, _) => Invalidate();
            return menu;
        }
        private void LanguageItemClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            string code = item != null ? item.Tag as string : null;
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            Action<string> handler = LanguageChanged;
            if (handler != null)
            {
                handler(code);
            }

            Invalidate();
        }

        private void ShowLanguageMenu()
        {
            Frame frame = CurrentFrame();
            string current = Lang;

            // 阿拉伯语下菜单本身也要从右往左展开，否则语言名和勾选标记会挤在错误的一侧。
            _langMenu.RightToLeft = _rtl ? RightToLeft.Yes : RightToLeft.No;

            for (int i = 0; i < _langMenu.Items.Count; i++)
            {
                ToolStripMenuItem item = _langMenu.Items[i] as ToolStripMenuItem;
                if (item != null)
                {
                    item.Checked = string.Equals(item.Tag as string, current, StringComparison.OrdinalIgnoreCase);
                }
            }

            // 菜单从字段的哪一侧弹出也跟着翻：RTL 下从左边弹。
            int x = (int)Math.Round(frame.LangField.Left);

            _langMenu.Show(this, new Point(x, (int)Math.Round(frame.LangField.Bottom) + 2));
        }

        private void PortKeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void PortTextChanged(object sender, EventArgs e)
        {
            bool invalid = !TryParsePort(_portBox.Text, out _);
            if (invalid != _portInvalid)
            {
                _portInvalid = invalid;
                Invalidate();
            }
        }

        private void InputFocusChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        private void InputLeave(object sender, EventArgs e)
        {
            Invalidate();
            CommitFromInputs();
        }

        private void CredentialKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                CommitFromInputs();
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                SyncInputText();
                Invalidate();
            }
        }

        private void PassTextChanged(object sender, EventArgs e)
        {
            SyncPasswordToggle();
            Invalidate();
        }

        /// <summary>显隐按钮：文案跟随语言与可见状态，密码框空了就置灰（没东西可显）。</summary>
        private void SyncPasswordToggle()
        {
            if (_pwdToggle == null)
            {
                return;
            }

            string text = Strings.T(Lang, _passwordVisible ? "addr.hide" : "addr.show");
            if (!string.Equals(_pwdToggle.Text, text, StringComparison.Ordinal))
            {
                _pwdToggle.Text = text;
            }

            // 按钮整体挪到了框的另一端，文字也要跟着换边，否则会空掉一大片。
            ContentAlignment toggleAlign = _rtl ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
            if (_pwdToggle.TextAlign != toggleAlign)
            {
                _pwdToggle.TextAlign = toggleAlign;
            }

            _pwdToggle.Padding = _rtl ? new Padding(6, 0, 0, 0) : new Padding(0, 0, 6, 0);

            // 运行中整套凭据锁定，显隐按钮跟着消失，免得在白框上留一块灰。
            bool visible = !InputLocked;
            if (_pwdToggle.Visible != visible)
            {
                _pwdToggle.Visible = visible;
            }

            bool enabled = _passBox != null && _passBox.TextLength > 0;
            if (_pwdToggle.Enabled != enabled)
            {
                _pwdToggle.Enabled = enabled;
            }

            if (_pwdFont != null && !ReferenceEquals(_pwdToggle.Font, _pwdFont))
            {
                _pwdToggle.Font = _pwdFont;
            }
        }

        private static bool TryParsePort(string text, out int port)
        {
            port = 0;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.Length == 0 || trimmed.Length > 5)
            {
                return false;
            }

            return int.TryParse(trimmed, out port) && port >= 1 && port <= 65535;
        }

        /// <summary>失焦 / 回车提交；端口非法时就地红字，不发事件。</summary>
        private void CommitFromInputs()
        {
            if (IsDisposed || _model == null || InputLocked)
            {
                return;
            }

            int port;
            if (!TryParsePort(_portBox.Text, out port))
            {
                if (!_portInvalid)
                {
                    _portInvalid = true;
                    Invalidate();
                }

                return;
            }

            _portInvalid = false;

            string password = _passBox.Text ?? string.Empty;
            string currentPassword = _model.Password ?? string.Empty;
            if (port == _model.Port && string.Equals(password, currentPassword, StringComparison.Ordinal))
            {
                // 没改就不打扰宿主，免得每次失焦都弹一条「设置已保存」。
                return;
            }

            Action<int, string> handler = CommitCredentials;
            if (handler != null)
            {
                handler(port, password);
            }

            Invalidate();
        }

        private void SyncInputText()
        {
            if (_portBox != null && !_portBox.Focused)
            {
                SetBoxText(_portBox, _model.Port.ToString());
            }

            if (_passBox != null)
            {
                if (!_passBox.Focused)
                {
                    SetBoxText(_passBox, _model.Password ?? string.Empty);
                }

                bool wantPasswordChar = !_passwordVisible;
                if (_passBox.UseSystemPasswordChar != wantPasswordChar)
                {
                    _passBox.UseSystemPasswordChar = wantPasswordChar;
                }
            }
        }

        private static void SetBoxText(TextBox box, string text)
        {
            if (box == null || string.Equals(box.Text, text, StringComparison.Ordinal))
            {
                return;
            }

            box.Text = text;
            box.SelectionStart = box.TextLength;
        }

        private void PasswordToggleClick(object sender, EventArgs e)
        {
            _passwordVisible = !_passwordVisible;
            if (_passBox != null)
            {
                bool wantPasswordChar = !_passwordVisible;
                if (_passBox.UseSystemPasswordChar != wantPasswordChar)
                {
                    _passBox.UseSystemPasswordChar = wantPasswordChar;
                }
            }

            SyncPasswordToggle();
            Invalidate();
        }

        private void EnsureInputFonts(float scale)
        {
            if (Math.Abs(_inputFontScale - scale) < 0.001f
                && _portFont != null
                && _passFont != null
                && _pwdFont != null)
            {
                return;
            }

            if (_portFont != null)
            {
                _portFont.Dispose();
            }

            if (_passFont != null)
            {
                _passFont.Dispose();
            }

            if (_pwdFont != null)
            {
                _pwdFont.Dispose();
            }

            _portFont = Theme.Mono(13f * scale);
            _passFont = Theme.Sans(13f * scale);
            _pwdFont = Theme.Sans(12f * scale);
            _inputFontScale = scale;
        }

        private void SyncInputs(Frame frame)
        {
            if (frame == null || _portBox == null || _passBox == null || _pwdToggle == null)
            {
                return;
            }

            // 文本同步放在绘制路径里：Apply 可能早于句柄创建，早调也不会留空框。
            SyncInputText();
            EnsureInputFonts(frame.Scale);

            bool locked = InputLocked;

            // 绘制路径里反复写同一个 Font / 只读 / 配色会让原生控件不停重绘。
            StyleInputBox(_portBox, _portFont, locked);
            StyleInputBox(_passBox, _passFont, locked);

            // 输入框里的内容（端口数字、密码）始终是 LTR 的：TextBox 的 RightToLeft 只决定
            // 贴哪一边，不会重排字符顺序。但值要和它上方的标签同侧，
            // 否则阿语界面里标签贴右、值贴左，两者隔得老远。
            HorizontalAlignment valueAlign = frame.Rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            if (_portBox.TextAlign != valueAlign) _portBox.TextAlign = valueAlign;
            if (_passBox.TextAlign != valueAlign) _passBox.TextAlign = valueAlign;

            // 显隐按钮跟着布局走：RTL 下它贴在框的左端内侧，所以左右内边距要交换。
            LayoutInput(_portBox, frame.PortInput, 10f * frame.Scale, 10f * frame.Scale);
            LayoutInput(_passBox, frame.PassInput, frame.Rtl ? 56f * frame.Scale : 10f * frame.Scale,
                frame.Rtl ? 10f * frame.Scale : 56f * frame.Scale);

            SyncPasswordToggle();
            LayoutPasswordToggle(frame);
        }

        /// <summary>显隐按钮贴在密码框靠内的一端：文字右对齐，距框沿 6px。</summary>
        private void LayoutPasswordToggle(Frame frame)
        {
            float s = frame.Scale;
            float width = 48f * s;
            float height = 28f * s;
            // RTL：密码框整体在右边，显隐按钮挪到左端内侧。
            float x = frame.Rtl
                ? frame.PassInput.Left + 6f * s
                : frame.PassInput.Right - width - 6f * s;
            float y = frame.PassInput.Top + (frame.PassInput.Height - height) / 2f;

            ApplyBounds(
                _pwdToggle,
                (int)Math.Round(x),
                (int)Math.Round(y),
                (int)Math.Round(width),
                (int)Math.Round(height));
        }

        private static void LayoutInput(TextBox box, RectangleF input, float padLeft, float padRight)
        {
            int height = Math.Max(1, box.PreferredHeight);
            float width = Math.Max(1f, input.Width - padLeft - padRight);
            float x = input.Left + padLeft;
            float y = input.Top + (input.Height - height) / 2f;
            ApplyBounds(
                box,
                (int)Math.Round(x),
                (int)Math.Round(y),
                (int)Math.Round(width),
                height);
        }

        /// <summary>位置没变就不碰窗口：子控件 SetBounds 会走 SetWindowPos，重复调用会把父级卷进重绘循环。</summary>
        private static void ApplyBounds(Control control, int x, int y, int width, int height)
        {
            Rectangle current = control.Bounds;
            if (current.X == x && current.Y == y && current.Width == width && current.Height == height)
            {
                return;
            }

            control.SetBounds(x, y, width, height);
        }

        /// <summary>
        /// 字体、可编辑性、配色只在值真的变了才写，避免原生 TextBox 持续重排。
        /// 锁定态直接禁用控件：只读（ReadOnly）仍然能选中文本、能拖出高亮，看着像还能改。
        /// </summary>
        private static void StyleInputBox(TextBox box, Font font, bool locked)
        {
            if (!ReferenceEquals(box.Font, font))
            {
                box.Font = font;
            }

            // 禁用后由系统画灰底，这里把配色对齐到 SystemColors.Control，和自绘外壳无缝。
            if (box.Enabled == locked)
            {
                box.Enabled = !locked;
            }

            // TabStop 恒为 false：输入框只靠点击获得焦点，不参与 Tab 键遍历。
            if (box.TabStop)
            {
                box.TabStop = false;
            }

            Color back = locked ? SystemColors.Control : Theme.Panel;
            if (box.BackColor != back)
            {
                box.BackColor = back;
            }

            Color fore = locked ? Theme.DisabledText : Theme.Text;
            if (box.ForeColor != fore)
            {
                box.ForeColor = fore;
            }
        }

        // ===================== 取整工具 =====================
        private static float Snap(float value)
        {
            return (float)Math.Round(value);
        }

        private static RectangleF Snap(RectangleF rect)
        {
            return new RectangleF(
                Snap(rect.X),
                Snap(rect.Y),
                Math.Max(0f, Snap(rect.Width)),
                Math.Max(0f, Snap(rect.Height)));
        }

        private static float PenWidth(float width)
        {
            return Math.Max(1f, Snap(width));
        }

        // ===================== 布局 =====================
        /// <summary>整页等比适配：设计 900×600，取宽/高两个方向里更小的比例。</summary>
        private float FitScale()
        {
            float width = ClientSize.Width;
            float height = ClientSize.Height;
            if (width <= 0f || height <= 0f)
            {
                return 1f;
            }

            float scale = Math.Min(width / BaseWidth, height / BaseHeight);
            return scale > 0f ? scale : 1f;
        }

        /// <summary>设计坐标 → 客户坐标：先乘缩放，再加居中偏移，最后取整。</summary>
        private static RectangleF R(Frame f, float x, float y, float width, float height)
        {
            // RTL：整块布局水平镜像（x 轴翻转）。
            // 文字的对齐方向由 DrawTextRtl 单独处理，这里只管矩形位置。
            if (f.Rtl)
            {
                x = BaseWidth - x - width;
            }

            return Snap(new RectangleF(
                f.OffsetX + x * f.Scale,
                f.OffsetY + y * f.Scale,
                width * f.Scale,
                height * f.Scale));
        }

        private Frame CurrentFrame()
        {
            if (_frame == null || _frameDirty)
            {
                _frame = BuildFrame();
                _frameDirty = false;
            }

            return _frame;
        }

        private void InvalidateFrame()
        {
            _frameDirty = true;
            _frame = null;
        }

        private Frame BuildFrame()
        {
            Frame f = new Frame();
            float s = FitScale();
            f.Scale = s;

            // 等比缩放后整页居中：窗口比例与设计比例不一致时均匀留白，绝不把控件横向拉稀。
            f.OffsetX = Snap(Math.Max(0f, (ClientSize.Width - BaseWidth * s) / 2f));
            f.OffsetY = Snap(Math.Max(0f, (ClientSize.Height - BaseHeight * s) / 2f));

            string lang = Lang;
            f.Rtl = _rtl;
            List<SlotItem> slots = _model.Slots ?? new List<SlotItem>();

            // ---------- 第一行：服务面板（地址 | 端口密码 | 启停），只用 24px 间距分区 ----------
            // 两条硬基线：标题行统一 36，控件行统一 64（36px 控件），40px 主按钮底边对齐到 100。
            const float pad = 16f;        // 外边距
            const float inset = 20f;      // 面板内边距
            const float titleY = 36f;
            const float fieldY = 64f;
            const float fieldBottom = fieldY + 36f;   // 100
            const float rowBottom = 146f;
            const float gap = 24f;        // 三段间距，不画竖线

            float right = BaseWidth - pad;            // 964
            float contentLeft = pad + inset;          // 36
            float contentRight = right - inset;       // 944

            float actionX = contentRight - 170f;      // 774
            float connX = actionX - gap - 270f;       // 480
            float addrRight = connX - gap;            // 456

            f.ServicePanel = R(f, pad, pad, right - pad, rowBottom - pad);

            // 地址段：27px 等宽地址 + 紧贴其右端的复制按钮。
            // 按钮位置按地址实际宽度算：绘制时用同一套「放不下就缩字号」的规则，量出来多少就贴多近。
            string address = LocalIpAddress() + ":" + _model.Port.ToString();
            float addressWidth;
            using (Font addrFont = CreateFittedFont(true, 27f, s, FontStyle.Bold, address, 300f * s, 16f))
            {
                addressWidth = Snap(Measure(address, addrFont) / s);
            }

            addressWidth = Math.Min(Math.Max(addressWidth, 60f), 300f);

            f.AddrTitle = R(f, contentLeft, titleY, addrRight - contentLeft, 18f);
            f.AddrText = R(f, contentLeft, fieldY - 2f, addressWidth + 2f, 40f);
            f.CopyButton = R(f, contentLeft + addressWidth + 12f, fieldY, 92f, 36f);
            AddHit(f, f.CopyButton, "copy", HitAction.Copy, true);
            f.AddrNote = R(f, contentLeft, 110f, addrRight - contentLeft, 16f);

            // 连接设置段：端口 110 + 间距 12 + 密码 148，合计 270。
            f.PortLabel = R(f, connX, titleY, 110f, 18f);
            f.PortInput = R(f, connX, fieldY, 110f, 36f);
            AddHit(f, f.PortInput, "focus:port", HitAction.FocusPort, true);

            f.PassLabel = R(f, connX + 122f, titleY, 148f, 18f);
            f.PassInput = R(f, connX + 122f, fieldY, 148f, 36f);
            AddHit(f, f.PassInput, "focus:pass", HitAction.FocusPass, true);

            f.FieldHint = R(f, connX, 110f, 270f, 16f);

            // 启停段：圆点与标题同基线，40px 主按钮与输入框底边齐平。
            f.StatusDot = R(f, actionX, titleY + 5f, 8f, 8f);
            f.StatusText = R(f, actionX + 15f, titleY, 155f, 18f);
            f.PowerButton = R(f, actionX, fieldBottom - 40f, 170f, 40f);
            AddHit(f, f.PowerButton, "power", HitAction.Power, !_model.IsBusy);

            // ---------- 第二行：配置列（272）+ 组间距 16 + 槽位（660） ----------
            const float mainTop = 162f;
            const float mainBottom = 532f;

            f.ConfigPanel = R(f, pad, mainTop, 272f, mainBottom - mainTop);
            float slotsX = pad + 272f + 16f;
            f.SlotsPanel = R(f, slotsX, mainTop, right - slotsX, mainBottom - mainTop);

            f.ConfigTitle = R(f, 36f, 182f, 232f, 18f);
            f.LangLabel = R(f, 36f, 212f, 232f, 16f);
            f.LangField = R(f, 36f, 236f, 232f, 36f);
            AddHit(f, f.LangField, "lang", HitAction.OpenLanguage, true);

            // 二维码框是正方形：停服占位和运行态用同一块区域，切换时不跳版。
            f.QrBox = R(f, 44f, 288f, 216f, 216f);

            f.SlotsTitle = R(f, 324f, 182f, 320f, 18f);
            f.SlotCount = R(f, 744f, 182f, 200f, 18f);

            f.FooterRow = R(f, contentLeft, 548f, 400f, 16f);

            const float cardWidth = 302f;
            const float cardHeight = 140f;
            const float cardGapX = 16f;
            const float cardGapY = 16f;
            const float cardPad = 14f;
            const float cardInner = cardWidth - cardPad * 2f;   // 274

            for (int i = 0; i < 4; i++)
            {
                SlotItem item = i < slots.Count ? slots[i] : null;
                bool occupied = item != null && item.Occupied;

                // Type 非空 ⇔ 这个槽位上常驻着设备（空槽给空串）：空闲但设备还在的槽，要能删掉它。
                bool hasDevice = item != null && !string.IsNullOrEmpty(item.Type);
                int slotNumber = item != null && item.Slot > 0 ? item.Slot : i + 1;

                float cardX = 324f + (i % 2) * (cardWidth + cardGapX);
                float cardY = 216f + (i / 2) * (cardHeight + cardGapY);

                CardInfo card = new CardInfo
                {
                    Bounds = R(f, cardX, cardY, cardWidth, cardHeight),
                    Item = item,
                    Occupied = occupied,
                    SlotNumber = slotNumber,
                    Index = i,
                    HasDevice = hasDevice,
                    Brand = hasDevice ? item.Type : "xbox"
                };

                card.NumberRect = R(f, cardX + cardPad, cardY + 12f, 22f, 18f);
                card.TitleRect = R(f, cardX + 42f, cardY + 12f, 150f, 18f);

                float stateWidth;
                using (Font stateFont = Theme.Sans(11.5f))
                {
                    // 徽标宽度按实测文字 + 圆点 + 间距 + 2px 余量，绝不让文字顶到边缘。
                    stateWidth = Snap(8f + 6f + Measure(Strings.T(lang, occupied ? "slot.connected" : "slot.idle"), stateFont) + 2f);
                }

                card.StateRect = R(f, cardX + cardWidth - cardPad - stateWidth, cardY + 12f, stateWidth, 18f);
                card.TypeRect = R(f, cardX + cardPad, cardY + 36f, cardInner, 20f);
                card.MetaRect = R(f, cardX + cardPad, cardY + 60f, cardInner, 16f);
                card.FooterLine = R(f, cardX + cardPad, cardY + 84f, cardInner, 1f);
                // 空闲态：内容在「顶行以下」这块区域里真正垂直居中（30..140 的中线是 85）。
                card.GlyphRect = R(f, cardX + cardPad, cardY + 69f, 32f, 32f);
                card.HintRect = R(f, cardX + cardPad + 40f, cardY + 69f, cardInner - 40f, 32f);

                // 右下角是「谁连着」：上行设备名、下行 IP。断开按钮占左侧 84px，剩下这段归它。
                card.DeviceRect = R(f, cardX + 108f, cardY + 88f, 166f, 18f);
                card.AddressRect = R(f, cardX + 108f, cardY + 106f, 166f, 16f);

                if (occupied)
                {
                    // 只剩「断开连接」一个操作：槽位设置整套取消，卡片不再可点配置。
                    card.PrimaryButton = R(f, cardX + cardPad, cardY + 88f, 84f, 36f);

                    Hit disconnectHit = AddHit(f, card.PrimaryButton, "slot" + i + ":disconnect", HitAction.Disconnect, true);
                    disconnectHit.Slot = slotNumber;
                }
                else
                {
                    // 空闲卡：手柄线稿 + 一句提示横排，不再居中留一大片空白。
                    card.PrimaryButton = RectangleF.Empty;

                    // 设备还常驻着才给「卸载」：卸载的是这台虚拟手柄，不是槽位本身。
                    // 提示文字让出这段宽度，按钮贴在右下角（RTL 下由 R() 镜像到左下）。
                    if (hasDevice)
                    {
                        const float deleteWidth = 72f;
                        card.HintRect = R(
                            f,
                            cardX + cardPad + 40f,
                            cardY + 69f,
                            cardInner - 40f - deleteWidth - 10f,
                            32f);
                        card.DeleteButton = R(
                            f,
                            cardX + cardWidth - cardPad - deleteWidth,
                            cardY + 71f,
                            deleteWidth,
                            28f);

                        Hit removeHit = AddHit(f, card.DeleteButton, "slot" + i + ":remove", HitAction.RemoveDevice, true);
                        removeHit.Slot = slotNumber;
                    }
                }

                f.Cards.Add(card);
            }

            return f;
        }

        private static Hit AddHit(Frame frame, RectangleF rect, string id, HitAction action, bool enabled)
        {
            Hit hit = new Hit
            {
                Rect = rect,
                Id = id,
                Action = action,
                Enabled = enabled
            };
            frame.Hits.Add(hit);
            return hit;
        }

        private static Hit HitAt(Frame frame, Point location)
        {
            if (frame == null)
            {
                return null;
            }

            for (int i = frame.Hits.Count - 1; i >= 0; i--)
            {
                Hit hit = frame.Hits[i];
                if (hit.Rect.Contains(location.X, location.Y))
                {
                    return hit;
                }
            }

            return null;
        }

        // ===================== 绘制 =====================
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.Page);

            Frame frame = CurrentFrame();
            SyncInputs(frame);

            DrawServiceRow(g, frame);
            DrawConfigPanel(g, frame);
            DrawSlots(g, frame);
            DrawFooter(g, frame);
            DrawToast(g, frame);
        }

        /// <summary>第一行：一块白面板里三段——地址 / 端口密码 / 启停，只用留白分区。</summary>
        private void DrawServiceRow(Graphics g, Frame f)
        {
            float s = f.Scale;
            string lang = Lang;
            bool running = _model.IsRunning;

            DrawPanel(g, f, f.ServicePanel);

            DrawAddress(g, f);
            DrawCredentials(g, f);

            // 启停段：状态圆点 + 一句话，颜色只表达「活着 / 停了 / 切换中」。
            Color statusColor = _model.IsBusy ? Theme.Amber : (running ? Theme.Green : Theme.Red);
            using (SolidBrush dotBrush = new SolidBrush(statusColor))
            {
                g.FillEllipse(dotBrush, f.StatusDot);
            }

            using (Font stateFont = Theme.Sans(14f * s, FontStyle.Bold))
            {
                DrawText(g, Strings.T(lang, StateKey()), stateFont, Theme.Text, f.StatusText, StringAlignment.Near, StringAlignment.Center, true);
            }

            DrawPowerButton(g, f);
        }

        /// <summary>地址段：小标题 + 27px 等宽地址 + 浅蓝底复制按钮。</summary>
        private void DrawAddress(Graphics g, Frame f)
        {
            float s = f.Scale;
            string lang = Lang;

            using (Font titleFont = Theme.Sans(12f * s))
            {
                DrawText(g, Strings.T(lang, "addr.title"), titleFont, Theme.Muted, f.AddrTitle, StringAlignment.Near, StringAlignment.Center, true);
            }

            DrawAddressValue(g, f);

            using (Font noteFont = Theme.Sans(12f * s))
            {
                DrawText(g, Strings.T(lang, "addr.hint"), noteFont, Theme.Muted, f.AddrNote, StringAlignment.Near, StringAlignment.Center, true);
            }

            DrawButton(g, f, f.CopyButton, Strings.T(lang, "addr.copy"), 13f, ButtonStyle.Link, "copy", true);
        }

        /// <summary>
        /// 大号地址：IP 用主文字色，端口单独染蓝，一眼能读。
        ///
        /// 地址整体是一个 LTR 单元（"192.168.1.5:5000"），哪怕界面是阿语，
        /// 它也必须按原样从左往右读。所以这里自己算两段的位置，不交给 RTL 自动摆。
        /// </summary>
        private void DrawAddressValue(Graphics g, Frame f)
        {
            float s = f.Scale;
            string ip = LocalIpAddress();
            string port = ":" + _model.Port.ToString();

            using (Font addrFont = CreateFittedFont(true, 27f, s, FontStyle.Bold, ip + port, f.AddrText.Width, 16f))
            {
                if (string.IsNullOrEmpty(ip))
                {
                    DrawText(g, Dash, addrFont, Theme.Faint, f.AddrText, StringAlignment.Near, StringAlignment.Center, true);
                    return;
                }

                float ipWidth = Measure(ip, addrFont);
                float portWidth = Measure(port, addrFont);

                // RTL：地址单元贴到矩形的右缘（阿语的行首在右），但单元内部仍按 IP:端口 左→右。
                // 两段各自贴着自己在单元里的位置，所以这里必须手动定位，不能拿矩形 Left 推算。
                float ipLeft;
                float portLeft;

                if (f.Rtl)
                {
                    float total = ipWidth + portWidth;
                    ipLeft = f.AddrText.Right - total;
                    portLeft = ipLeft + ipWidth;
                }
                else
                {
                    ipLeft = f.AddrText.Left;
                    portLeft = f.AddrText.Left + ipWidth;
                }

                // IP 与端口都是 LTR 单元：用 DrawTextLtr 强制贴各自矩形的左缘，
                // 否则 RTL 的 Near 会变成贴右缘，文字会整体偏移。
                DrawTextLtr(
                    g,
                    ip,
                    addrFont,
                    Theme.Text,
                    Snap(new RectangleF(ipLeft, f.AddrText.Top, ipWidth + Snap(2f * s), f.AddrText.Height)),
                    StringAlignment.Near,
                    StringAlignment.Center);

                float available = Math.Max(0f, f.AddrText.Right - portLeft);
                if (available > 0f)
                {
                    DrawTextLtr(
                        g,
                        port,
                        addrFont,
                        Theme.Accent,
                        Snap(new RectangleF(portLeft, f.AddrText.Top, available, f.AddrText.Height)),
                        StringAlignment.Near,
                        StringAlignment.Center);
                }
            }
        }

        /// <summary>端口与密码：两个真 TextBox 的外壳与标签；提示行只在端口非法时出现。</summary>
        private void DrawCredentials(Graphics g, Frame f)
        {
            float s = f.Scale;
            string lang = Lang;

            using (Font labelFont = Theme.Sans(12f * s))
            {
                DrawText(g, Strings.T(lang, "addr.port"), labelFont, Theme.Muted, f.PortLabel, StringAlignment.Near, StringAlignment.Center, false);
                DrawText(g, Strings.T(lang, "addr.password"), labelFont, Theme.Muted, f.PassLabel, StringAlignment.Near, StringAlignment.Center, false);
            }

            DrawInputBox(g, f, f.PortInput, _portBox, _portInvalid);
            DrawInputBox(g, f, f.PassInput, _passBox, false);

            if (_portInvalid)
            {
                using (Font hintFont = Theme.Sans(12f * s))
                {
                    DrawText(g, Strings.T(lang, "toast.portInvalid"), hintFont, Theme.Red, f.FieldHint, StringAlignment.Near, StringAlignment.Center, true);
                }
            }
        }

        /// <summary>启停按钮：启动态整块闷绿白字，运行态白底红字，切换中置灰。</summary>
        private void DrawPowerButton(Graphics g, Frame f)
        {
            RectangleF rect = f.PowerButton;
            if (rect.Width <= 0f || rect.Height <= 0f)
            {
                return;
            }

            float s = f.Scale;
            bool enabled = !_model.IsBusy;
            bool running = _model.IsRunning;
            bool hover = enabled && IsHover("power");

            Color back;
            Color fore;
            Color border;

            if (!enabled)
            {
                back = Theme.DisabledBack;
                fore = Theme.DisabledText;
                border = Theme.Line;
            }
            else if (running)
            {
                back = hover ? Theme.RedSoft : Theme.Panel;
                fore = Theme.Red;
                border = Theme.LineStrong;
            }
            else
            {
                back = hover ? Theme.GreenHover : Theme.Green;
                fore = Color.White;
                border = back;
            }

            Theme.FillRoundRect(g, rect, Theme.RadiusCard * s, back);
            Theme.DrawRoundRect(g, rect, Theme.RadiusCard * s, border, PenWidth(s));

            using (Font font = Theme.Sans(14f * s, FontStyle.Bold))
            {
                DrawText(g, PowerText(Lang), font, fore, rect, StringAlignment.Center, StringAlignment.Center, true);
            }
        }

        /// <summary>配置列：分区标题、界面语言、扫码区。二维码提示只保留框内那一处。</summary>
        private void DrawConfigPanel(Graphics g, Frame f)
        {
            float s = f.Scale;
            string lang = Lang;

            DrawPanel(g, f, f.ConfigPanel);

            using (Font titleFont = Theme.Sans(13f * s, FontStyle.Bold))
            {
                DrawText(g, Strings.T(lang, "config.title"), titleFont, Theme.Text, f.ConfigTitle, StringAlignment.Near, StringAlignment.Center, false);
            }

            using (Font labelFont = Theme.Sans(12f * s))
            {
                DrawText(g, Strings.T(lang, "lang.label"), labelFont, Theme.Muted, f.LangLabel, StringAlignment.Near, StringAlignment.Center, false);
            }

            DrawLanguageField(g, f);
            DrawQrBox(g, f);
        }

        /// <summary>自绘语言字段：36px 白底 + 1px 边 + 右侧两条斜线箭头，不用系统 ComboBox 的灰方块。</summary>
        private void DrawLanguageField(Graphics g, Frame f)
        {
            float s = f.Scale;
            RectangleF rect = f.LangField;
            bool hover = IsHover("lang");

            Theme.FillRoundRect(g, rect, Theme.RadiusCard * s, hover ? Theme.ButtonHover : Theme.Panel);
            Theme.DrawRoundRect(g, rect, Theme.RadiusCard * s, Theme.Line, PenWidth(s));

            using (Font font = Theme.Sans(13f * s))
            {
                // RTL：语言名贴右，箭头挪到左端 —— 两者跟着读的方向一起翻。
                RectangleF textRect = f.Rtl
                    ? Snap(new RectangleF(rect.Left + Snap(30f * s), rect.Top, rect.Width - Snap(40f * s), rect.Height))
                    : Snap(new RectangleF(rect.Left + Snap(10f * s), rect.Top, rect.Width - Snap(40f * s), rect.Height));

                DrawText(
                    g,
                    Strings.LanguageName(Lang),
                    font,
                    Theme.Text,
                    textRect,
                    StringAlignment.Near,
                    StringAlignment.Center,
                    true);
            }

            float cx = Snap(f.Rtl ? rect.Left + Snap(17f * s) : rect.Right - Snap(17f * s));
            float cy = Snap(rect.Top + rect.Height / 2f);
            float w = Snap(4f * s);
            using (Pen pen = new Pen(Theme.Muted, PenWidth(1.4f * s)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                // RTL 下箭头也翻个向：原本朝右下，翻成朝左下。
                if (f.Rtl)
                {
                    g.DrawLine(pen, Snap(cx + w), Snap(cy - w / 2f), cx, Snap(cy + w / 2f));
                    g.DrawLine(pen, cx, Snap(cy + w / 2f), Snap(cx - w), Snap(cy - w / 2f));
                }
                else
                {
                    g.DrawLine(pen, Snap(cx - w), Snap(cy - w / 2f), cx, Snap(cy + w / 2f));
                    g.DrawLine(pen, cx, Snap(cy + w / 2f), Snap(cx + w), Snap(cy - w / 2f));
                }
            }
        }

        /// <summary>二维码框：运行态画真码；停止态整块浅灰占位 + 图标 + 一句提示，不在码上压字。</summary>
        private void DrawQrBox(Graphics g, Frame f)
        {
            float s = f.Scale;
            bool running = _model.IsRunning;
            RectangleF box = f.QrBox;
            if (box.Width <= 0f || box.Height <= 0f)
            {
                return;
            }

            if (running && _qrImage != null)
            {
                Theme.FillRoundRect(g, box, Theme.RadiusCard * s, Theme.QrBack);

                // 图片铺满容器：二维码图片自带 quiet zone（白边），不需要再留内边距。
                // 只内缩 1px，让 1px 描边完整地压在图片外沿上。
                RectangleF target = Snap(RectangleF.Inflate(box, -Snap(s), -Snap(s)));

                InterpolationMode previous = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(_qrImage, target);
                g.InterpolationMode = previous;

                Theme.DrawRoundRect(g, box, Theme.RadiusCard * s, Theme.Line, PenWidth(s));
                return;
            }

            // 停服 / 无码：浅灰占位，图标 + 文案作为一整块在框里真正居中（水平与垂直都居中）。
            Theme.FillRoundRect(g, box, Theme.RadiusCard * s, Theme.PanelSoft);
            Theme.DrawRoundRect(g, box, Theme.RadiusCard * s, Theme.Line, PenWidth(s));

            float icon = Snap(30f * s);
            float textHeight = Snap(16f * s);
            float gapY = Snap(10f * s);
            float blockTop = Snap(box.Top + (box.Height - icon - gapY - textHeight) / 2f);

            DrawQrGlyph(
                g,
                Snap(new RectangleF(box.Left + (box.Width - icon) / 2f, blockTop, icon, icon)),
                Theme.Muted,
                s);

            using (Font font = Theme.Sans(12f * s))
            {
                DrawText(
                    g,
                    Strings.T(Lang, "qr.hint.stop"),
                    font,
                    Theme.Muted,
                    Snap(new RectangleF(
                        box.Left + Snap(8f * s),
                        Snap(blockTop + icon + gapY),
                        box.Width - Snap(16f * s),
                        textHeight)),
                    StringAlignment.Center,
                    StringAlignment.Center,
                    true);
            }
        }

        /// <summary>二维码图标：三个定位角 + 右下几个点，纯线稿。</summary>
        private static void DrawQrGlyph(Graphics g, RectangleF rect, Color color, float scale)
        {
            rect = Snap(rect);
            if (rect.Width <= 10f || rect.Height <= 10f)
            {
                return;
            }

            float u = rect.Width / 10f;
            float stroke = PenWidth(1.5f * scale);

            using (Pen pen = new Pen(color, stroke))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pen.LineJoin = LineJoin.Miter;

                DrawQrFinder(g, pen, Snap(rect.Left), Snap(rect.Top), Snap(u * 3f));
                DrawQrFinder(g, pen, Snap(rect.Left + u * 7f), Snap(rect.Top), Snap(u * 3f));
                DrawQrFinder(g, pen, Snap(rect.Left), Snap(rect.Top + u * 7f), Snap(u * 3f));

                float dot = Math.Max(1f, Snap(u));
                g.FillRectangle(brush, Snap(rect.Left + u * 6f), Snap(rect.Top + u * 6f), dot, dot);
                g.FillRectangle(brush, Snap(rect.Left + u * 8f), Snap(rect.Top + u * 7f), dot, dot);
                g.FillRectangle(brush, Snap(rect.Left + u * 7f), Snap(rect.Top + u * 8f), dot, dot);
            }
        }

        private static void DrawQrFinder(Graphics g, Pen pen, float x, float y, float size)
        {
            float inner = size / 3f;
            g.DrawRectangle(pen, x, y, size, size);
            using (SolidBrush brush = new SolidBrush(pen.Color))
            {
                g.FillRectangle(brush, Snap(x + inner), Snap(y + inner), Snap(inner), Snap(inner));
            }
        }

        /// <summary>输入框外壳：白底 + 1px 边，聚焦描蓝，非法描红。</summary>
        private void DrawInputBox(Graphics g, Frame f, RectangleF rect, TextBox box, bool invalid)
        {
            float s = f.Scale;
            bool locked = InputLocked;
            bool focused = box != null && box.Focused;

            // 禁用态的背景由系统绘制，这里必须用同一个色（SystemColors.Control）才对得齐。
            Color back = locked ? SystemColors.Control : Theme.Panel;
            Color border = invalid
                ? Theme.Red
                : (focused ? Theme.Accent : Theme.Line);

            Theme.FillRoundRect(g, rect, Theme.RadiusInput * s, back);
            Theme.DrawRoundRect(g, rect, Theme.RadiusInput * s, border, focused ? PenWidth(1.6f * s) : PenWidth(s));
        }

        /// <summary>面板底板：白底 + 1px 边框 + 4px 圆角，无阴影无渐变。</summary>
        private static void DrawPanel(Graphics g, Frame f, RectangleF rect)
        {
            if (g == null || rect.Width <= 0f || rect.Height <= 0f)
            {
                return;
            }

            Theme.FillRoundRect(g, rect, Theme.RadiusCard * f.Scale, Theme.Panel);
            Theme.DrawRoundRect(g, rect, Theme.RadiusCard * f.Scale, Theme.Line, PenWidth(f.Scale));
        }

        private void DrawSlots(Graphics g, Frame f)
        {
            float s = f.Scale;
            string lang = Lang;
            int total = _model.Slots != null && _model.Slots.Count > 0 ? _model.Slots.Count : 4;

            DrawPanel(g, f, f.SlotsPanel);

            using (Font sectionFont = Theme.Sans(13f * s, FontStyle.Bold))
            {
                DrawText(g, Strings.T(lang, "slot.section"), sectionFont, Theme.Text, f.SlotsTitle, StringAlignment.Near, StringAlignment.Center, false);
            }

            // 计数：数字用运行绿 + 等宽，后面接一句灰字，右对齐成一列。
            // RTL 下这一列要整体贴到左边：数字在左、说明在右（阿语的行首在右）。
            string suffix = Strings.T(lang, "slot.onlineSuffix");
            using (Font suffixFont = Theme.Sans(13f * s))
            using (Font countFont = Theme.Mono(13f * s, FontStyle.Bold))
            {
                float suffixWidth = Measure(suffix, suffixFont);
                string countText = _model.Online + "/" + total;
                float countWidth = Measure(countText, countFont);

                float suffixLeft;
                float countLeft;

                if (f.Rtl)
                {
                    // 贴着矩形左缘排：先数字，后说明。
                    countLeft = f.SlotCount.Left;
                    suffixLeft = countLeft + countWidth + Snap(8f * s);
                }
                else
                {
                    suffixLeft = Snap(f.SlotCount.Right - suffixWidth);
                    countLeft = Snap(suffixLeft - Snap(8f * s) - countWidth);
                }

                DrawText(
                    g,
                    suffix,
                    suffixFont,
                    Theme.Muted,
                    Snap(new RectangleF(suffixLeft, f.SlotCount.Top, suffixWidth + Snap(2f * s), f.SlotCount.Height)),
                    StringAlignment.Near,
                    StringAlignment.Center,
                    false);

                // 计数是 "2/4"，LTR 单元：强制贴左缘。
                DrawTextLtr(
                    g,
                    countText,
                    countFont,
                    Theme.Green,
                    Snap(new RectangleF(countLeft, f.SlotCount.Top, countWidth + Snap(2f * s), f.SlotCount.Height)),
                    StringAlignment.Near,
                    StringAlignment.Center);
            }

            for (int i = 0; i < f.Cards.Count; i++)
            {
                DrawCard(g, f, f.Cards[i]);
            }
        }

        private void DrawCard(Graphics g, Frame f, CardInfo card)
        {
            float s = f.Scale;
            string lang = Lang;
            RectangleF bounds = card.Bounds;

            if (bounds.Width <= 0f || bounds.Height <= 0f)
            {
                return;
            }

            bool xbox = IsXboxBrand(card.Brand);

            // 已连接 = 按手柄品牌铺浅底；空闲 = 略深于面板的灰底，跟外层白面板分得开。
            bool cardHover = false;
            Color back = card.Occupied
                ? (xbox ? Theme.XboxBg : Theme.PsBg)
                : (cardHover ? Theme.ButtonHover : Theme.CardIdle);
            Color border = card.Occupied ? (xbox ? Theme.XboxLine : Theme.PsLine) : Theme.Line;

            Theme.FillRoundRect(g, bounds, Theme.RadiusCard * s, back);
            Theme.DrawRoundRect(g, bounds, Theme.RadiusCard * s, border, PenWidth(s));

            using (Font numberFont = Theme.Mono(12f * s, FontStyle.Bold))
            {
                // 槽位编号是纯数字（LTR 单元），阿语下也要贴框的左缘。
                // 走 DrawText 的话 RTL 的 Near 会变成贴右缘，22px 宽的框根本放不下两个数字。
                DrawTextLtr(
                    g,
                    card.SlotNumber.ToString("00"),
                    numberFont,
                    card.Occupied ? (xbox ? Theme.Xbox : Theme.Ps) : Theme.Faint,
                    card.NumberRect,
                    StringAlignment.Near,
                    StringAlignment.Center);
            }

            // 标题固定「手柄 N」：槽位名称与锁定整套配置已取消。
            string title = Strings.T(lang, "slot.player", card.SlotNumber);

            using (Font titleFont = CreateFittedFont(false, 13f, s, FontStyle.Bold, title, card.TitleRect.Width, 11f))
            {
                float titleWidth = Math.Min(Measure(title, titleFont), card.TitleRect.Width);

                if (f.Rtl)
                {
                    // RTL：标题贴右。
                    float titleRight = card.TitleRect.Right;
                    DrawText(
                        g,
                        title,
                        titleFont,
                        Theme.Text,
                        Snap(new RectangleF(titleRight - titleWidth, card.TitleRect.Top, titleWidth + Snap(2f * s), card.TitleRect.Height)),
                        StringAlignment.Far,
                        StringAlignment.Center,
                        true);
                }
                else
                {
                    DrawText(
                        g,
                        title,
                        titleFont,
                        Theme.Text,
                        Snap(new RectangleF(card.TitleRect.Left, card.TitleRect.Top, titleWidth + Snap(2f * s), card.TitleRect.Height)),
                        StringAlignment.Near,
                        StringAlignment.Center,
                        true);
                }
            }

            DrawCardState(g, f, card);

            if (card.Occupied)
            {
                DrawCardBody(g, f, card);
            }
            else
            {
                // 空闲但设备还常驻：先把类型标出来，管理员才知道要删掉的是哪台。
                if (card.HasDevice)
                {
                    using (Font typeFont = Theme.Sans(12f * s, FontStyle.Bold))
                    {
                        DrawText(
                            g,
                            Strings.TypeLabel(lang, card.Brand),
                            typeFont,
                            Theme.Muted,
                            card.TypeRect,
                            StringAlignment.Near,
                            StringAlignment.Center,
                            true);
                    }
                }

                // 空闲位：手柄线稿在左、提示在右，整行在顶行以下垂直居中。
                DrawGamepadGlyph(g, card.GlyphRect, Theme.Muted, s);

                // 有卸载键时提示只剩 152px 宽，阿语/英语原样排会顶出去，按实际宽度缩字号。
                string hint = Strings.T(lang, card.HasDevice ? "slot.waiting" : "slot.waitingDevice");
                using (Font hintFont = CreateFittedFont(false, 12.5f, s, FontStyle.Regular, hint, card.HintRect.Width, 10f))
                {
                    DrawText(g, hint, hintFont, Theme.Muted, card.HintRect, StringAlignment.Near, StringAlignment.Center, true);
                }

                if (card.HasDevice)
                {
                    DrawButton(g, f, card.DeleteButton, Strings.T(lang, "slot.delete"), 11.5f, ButtonStyle.Danger, "slot" + card.Index + ":remove", true);
                }
            }
        }

        /// <summary>状态徽标：8px 圆点 + 「已连接 / 空闲」。RTL 下圆点在右、文字在左。</summary>
        private void DrawCardState(Graphics g, Frame f, CardInfo card)
        {
            float s = f.Scale;
            float dot = Snap(8f * s);
            float gap = Snap(6f * s);

            Color color = card.Occupied ? Theme.Green : Theme.DotIdle;
            Color textColor = card.Occupied ? Theme.Green : Theme.Muted;

            // RTL：圆点贴着矩形的右缘，文字在圆点左边。
            float dotLeft = f.Rtl
                ? Snap(card.StateRect.Right - dot)
                : card.StateRect.Left;

            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, dotLeft, Snap(card.StateRect.Top + (card.StateRect.Height - dot) / 2f), dot, dot);
            }

            using (Font font = Theme.Sans(11.5f * s))
            {
                float textLeft = f.Rtl
                    ? card.StateRect.Left
                    : Snap(card.StateRect.Left + dot + gap);
                float textWidth = f.Rtl
                    ? Math.Max(0f, dotLeft - gap - card.StateRect.Left)
                    : Math.Max(0f, card.StateRect.Width - dot - gap);

                DrawText(
                    g,
                    Strings.T(Lang, card.Occupied ? "slot.connected" : "slot.idle"),
                    font,
                    textColor,
                    Snap(new RectangleF(textLeft, card.StateRect.Top, textWidth, card.StateRect.Height)),
                    StringAlignment.Near,
                    StringAlignment.Center,
                    false);
            }
        }

        /// <summary>已连接卡的主体：手柄品牌行 + 布局/编号 + 分割线 + 两个按钮 + 右对齐延迟。</summary>
        private void DrawCardBody(Graphics g, Frame f, CardInfo card)
        {
            float s = f.Scale;
            string lang = Lang;
            bool xbox = IsXboxBrand(card.Brand);
            Color brand = xbox ? Theme.Xbox : Theme.Ps;

            string typeLabel = Strings.TypeLabel(lang, card.Brand);
            string meta = Strings.TypeLayout(lang, card.Brand) + MiddleDot + IdPrefix + (card.Item != null ? card.Item.ControllerId : -1);

            using (Font typeFont = CreateFittedFont(false, 14f, s, FontStyle.Bold, typeLabel, card.TypeRect.Width - Snap(26f * s), 11f))
            {
                float iconSize = Snap(18f * s);

                // RTL：图标贴在文字右侧（阿语行首在右），文字跟在图标左边。
                float iconLeft = f.Rtl
                    ? Snap(card.TypeRect.Right - iconSize)
                    : card.TypeRect.Left;

                DrawGamepadGlyph(
                    g,
                    Snap(new RectangleF(iconLeft, card.TypeRect.Top + (card.TypeRect.Height - iconSize) / 2f, iconSize, iconSize)),
                    brand,
                    s);

                if (f.Rtl)
                {
                    // 文字右对齐、紧贴图标左边。
                    DrawText(
                        g,
                        typeLabel,
                        typeFont,
                        xbox ? Theme.XboxText : Theme.PsText,
                        Snap(new RectangleF(card.TypeRect.Left, card.TypeRect.Top, Math.Max(0f, iconLeft - Snap(8f * s) - card.TypeRect.Left), card.TypeRect.Height)),
                        StringAlignment.Far,
                        StringAlignment.Center,
                        true);
                }
                else
                {
                    float textLeft = Snap(card.TypeRect.Left + iconSize + Snap(8f * s));
                    DrawText(
                        g,
                        typeLabel,
                        typeFont,
                        xbox ? Theme.XboxText : Theme.PsText,
                        Snap(new RectangleF(textLeft, card.TypeRect.Top, Math.Max(0f, card.TypeRect.Right - textLeft), card.TypeRect.Height)),
                        StringAlignment.Near,
                        StringAlignment.Center,
                        true);
                }
            }

            using (Font metaFont = Theme.Sans(11.5f * s))
            {
                DrawText(g, meta, metaFont, Theme.Muted, card.MetaRect, StringAlignment.Near, StringAlignment.Center, true);
            }

            using (SolidBrush brush = new SolidBrush(xbox ? Theme.XboxLine : Theme.PsLine))
            {
                g.FillRectangle(brush, card.FooterLine);
            }

            DrawButton(g, f, card.PrimaryButton, Strings.T(lang, "slot.disconnect"), 12.5f, ButtonStyle.Danger, "slot" + card.Index + ":disconnect", true);

            // 谁连着：上行设备名、下行 IP。都没有就显示破折号（老客户端不报设备名）。
            string deviceName = card.Item != null ? card.Item.DeviceName : string.Empty;
            string address = card.Item != null ? card.Item.RemoteIp : string.Empty;

            using (Font deviceFont = Theme.Sans(11.5f * s, FontStyle.Bold))
            using (Font addressFont = Theme.Mono(10.5f * s))
            {
                DrawText(
                    g,
                    string.IsNullOrEmpty(deviceName) ? Dash : deviceName,
                    deviceFont,
                    Theme.Text,
                    card.DeviceRect,
                    StringAlignment.Far,
                    StringAlignment.Center,
                    true);

                DrawText(
                    g,
                    string.IsNullOrEmpty(address) ? Dash : address,
                    addressFont,
                    Theme.Muted,
                    card.AddressRect,
                    StringAlignment.Far,
                    StringAlignment.Center,
                    false);
            }
        }

        private static bool IsXboxBrand(string brand)
        {
            return !string.Equals(brand, "ds4", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>手柄线稿：一段闭合贝塞尔轮廓 + 十字键 + 两颗按键，空闲位用它代替大片空白。</summary>
        private static void DrawGamepadGlyph(Graphics g, RectangleF rect, Color color, float scale)
        {
            rect = Snap(rect);
            if (rect.Width <= 4f || rect.Height <= 4f)
            {
                return;
            }

            float x = rect.Left;
            float y = rect.Top;
            float w = rect.Width;
            float h = rect.Height;

            using (Pen pen = new Pen(color, PenWidth(1.4f * scale)))
            using (GraphicsPath outline = new GraphicsPath())
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                outline.AddBezier(x + 0.20f * w, y + 0.14f * h, x + 0.08f * w, y + 0.16f * h, x + 0.03f * w, y + 0.34f * h, x + 0.02f * w, y + 0.52f * h);
                outline.AddBezier(x + 0.02f * w, y + 0.52f * h, x + 0.01f * w, y + 0.70f * h, x + 0.06f * w, y + 0.92f * h, x + 0.16f * w, y + 0.92f * h);
                outline.AddBezier(x + 0.16f * w, y + 0.92f * h, x + 0.26f * w, y + 0.92f * h, x + 0.30f * w, y + 0.70f * h, x + 0.38f * w, y + 0.62f * h);
                outline.AddBezier(x + 0.38f * w, y + 0.62f * h, x + 0.44f * w, y + 0.56f * h, x + 0.56f * w, y + 0.56f * h, x + 0.62f * w, y + 0.62f * h);
                outline.AddBezier(x + 0.62f * w, y + 0.62f * h, x + 0.70f * w, y + 0.70f * h, x + 0.74f * w, y + 0.92f * h, x + 0.84f * w, y + 0.92f * h);
                outline.AddBezier(x + 0.84f * w, y + 0.92f * h, x + 0.94f * w, y + 0.92f * h, x + 0.99f * w, y + 0.70f * h, x + 0.98f * w, y + 0.52f * h);
                outline.AddBezier(x + 0.98f * w, y + 0.52f * h, x + 0.97f * w, y + 0.34f * h, x + 0.92f * w, y + 0.16f * h, x + 0.80f * w, y + 0.14f * h);
                outline.AddBezier(x + 0.80f * w, y + 0.14f * h, x + 0.60f * w, y + 0.09f * h, x + 0.40f * w, y + 0.09f * h, x + 0.20f * w, y + 0.14f * h);

                g.DrawPath(pen, outline);

                float dpadX = x + 0.25f * w;
                float dpadY = y + 0.42f * h;
                float dpadR = 0.09f * w;
                g.DrawLine(pen, Snap(dpadX - dpadR), Snap(dpadY), Snap(dpadX + dpadR), Snap(dpadY));
                g.DrawLine(pen, Snap(dpadX), Snap(dpadY - dpadR), Snap(dpadX), Snap(dpadY + dpadR));

                float dotSize = Math.Max(2f, Snap(0.06f * w));
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, Snap(x + 0.66f * w), Snap(y + 0.32f * h), dotSize, dotSize);
                    g.FillEllipse(brush, Snap(x + 0.78f * w), Snap(y + 0.46f * h), dotSize, dotSize);
                }
            }
        }

        /// <summary>标准按钮：白底 1px 边。Link 是复制按钮的浅蓝底，Danger 用于断开连接。</summary>
        private void DrawButton(
            Graphics g,
            Frame f,
            RectangleF rect,
            string text,
            float fontSize,
            ButtonStyle style,
            string hoverId,
            bool enabled)
        {
            if (rect.Width <= 0f || rect.Height <= 0f)
            {
                return;
            }

            rect = Snap(rect);
            float s = f.Scale;
            bool hover = enabled && IsHover(hoverId);

            Color back;
            Color border;
            Color fore;

            switch (style)
            {
                case ButtonStyle.Link:
                    back = hover ? Theme.AccentHover : Theme.AccentBg;
                    border = Theme.AccentLine;
                    fore = Theme.Accent;
                    break;

                case ButtonStyle.Danger:
                    back = hover ? Theme.RedSoft : Theme.Panel;
                    border = Theme.LineStrong;
                    fore = Theme.Red;
                    break;

                default:
                    back = hover ? Theme.ButtonHover : Theme.Button;
                    border = Theme.LineStrong;
                    fore = Theme.Text;
                    break;
            }

            if (!enabled)
            {
                back = Theme.DisabledBack;
                border = Theme.Line;
                fore = Theme.DisabledText;
            }

            Theme.FillRoundRect(g, rect, Theme.RadiusSm * s, back);
            Theme.DrawRoundRect(g, rect, Theme.RadiusSm * s, border, PenWidth(s));

            using (Font font = Theme.Sans(fontSize * s))
            {
                DrawText(
                    g,
                    text,
                    font,
                    fore,
                    Snap(new RectangleF(rect.Left + Snap(6f * s), rect.Top, Math.Max(0f, rect.Width - Snap(12f * s)), rect.Height)),
                    StringAlignment.Center,
                    StringAlignment.Center,
                    true);
            }
        }

        /// <summary>页脚：版本号角标，不加分割线。版本号是 LTR 内容，阿语下也照样从左往右读。</summary>
        private void DrawFooter(Graphics g, Frame f)
        {
            float s = f.Scale;

            using (Font font = Theme.Mono(11.5f * s))
            {
                // 版号本身是 ASCII，RTL 标志会让它贴到右边去；这里显式用 LTR 绘制并贴左缘。
                float width = Measure("VGamepadWeb v" + (_model.Version ?? "0.0.0"), font);
                RectangleF rect = f.Rtl
                    ? Snap(new RectangleF(f.FooterRow.Right - width, f.FooterRow.Top, width + Snap(2f * s), f.FooterRow.Height))
                    : f.FooterRow;

                using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
                {
                    format.Alignment = StringAlignment.Near;
                    format.LineAlignment = StringAlignment.Center;
                    format.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;

                    using (SolidBrush brush = new SolidBrush(Theme.Faint))
                    {
                        g.DrawString("VGamepadWeb v" + (_model.Version ?? "0.0.0"), font, brush, rect, format);
                    }
                }
            }
        }

        private void DrawToast(Graphics g, Frame f)
        {
            if (string.IsNullOrEmpty(_toastText))
            {
                return;
            }

            float s = f.Scale;
            using (Font font = Theme.Sans(14f * s))
            {
                // 浮层挂在内容区底部中央：整页居中后跟着内容走，不会飘到留白里。
                float contentWidth = BaseWidth * s;
                float contentHeight = BaseHeight * s;
                float textWidth = Measure(_toastText, font);
                float iconWidth = Snap(16f * s);
                float gap = Snap(8f * s);
                float padding = Snap(16f * s);
                float boxWidth = Math.Min(Math.Max(0f, contentWidth - Snap(32f * s)), textWidth + iconWidth + gap + padding * 2f);
                float boxHeight = Snap(38f * s);
                float left = Snap(f.OffsetX + Math.Max(0f, (contentWidth - boxWidth) / 2f));
                float top = Snap(f.OffsetY + Math.Max(0f, contentHeight - Snap(28f * s) - boxHeight));
                RectangleF box = Snap(new RectangleF(left, top, boxWidth, boxHeight));

                Theme.FillRoundRect(g, box, Theme.RadiusSm * s, Theme.ToastBack);
                Theme.DrawRoundRect(g, box, Theme.RadiusSm * s, Theme.ToastBorder, PenWidth(s));

                float centerY = Snap(box.Top + box.Height / 2f);
                DrawCheckIcon(
                    g,
                    Snap(new RectangleF(box.Left + padding, centerY - Snap(7f * s), Snap(14f * s), Snap(14f * s))),
                    ToastAccent(_toastLevel),
                    s);

                float textLeft = Snap(box.Left + padding + iconWidth + gap);
                DrawText(
                    g,
                    _toastText,
                    font,
                    Theme.ToastText,
                    Snap(new RectangleF(textLeft, box.Top, Math.Max(0f, box.Right - padding - textLeft), box.Height)),
                    StringAlignment.Near,
                    StringAlignment.Center,
                    false);
            }
        }

        /// <summary>Toast 勾的颜色：info/success 绿、warn 琥珀、error 红（深色底上取亮一档）。</summary>
        private static Color ToastAccent(string level)
        {
            if (string.Equals(level, ToastError, StringComparison.OrdinalIgnoreCase))
            {
                return Color.FromArgb(0xFF, 0x8A, 0x80);
            }

            if (string.Equals(level, ToastWarn, StringComparison.OrdinalIgnoreCase))
            {
                return Color.FromArgb(0xE8, 0xB3, 0x4C);
            }

            return Theme.ToastOk;
        }

        private static void DrawCheckIcon(Graphics g, RectangleF rect, Color color, float scale)
        {
            rect = Snap(rect);
            using (Pen pen = new Pen(color, PenWidth(2f * scale)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLines(
                    pen,
                    new[]
                    {
                        new PointF(Snap(rect.Left + rect.Width * 0.15f), Snap(rect.Top + rect.Height * 0.55f)),
                        new PointF(Snap(rect.Left + rect.Width * 0.42f), Snap(rect.Top + rect.Height * 0.82f)),
                        new PointF(Snap(rect.Left + rect.Width * 0.9f), Snap(rect.Top + rect.Height * 0.2f))
                    });
            }
        }

        // ===================== 文本工具 =====================
        /// <summary>按可用宽度逐级降字号，返回能单行放下的 Font（调用方负责 Dispose）。</summary>
        private Font CreateFittedFont(bool mono, float baseSize, float scale, FontStyle style, string text, float maxWidth, float minSize)
        {
            float size = baseSize;
            while (true)
            {
                Font font = mono ? Theme.Mono(size * scale, style) : Theme.Sans(size * scale, style);
                if (size <= minSize || Measure(text, font) <= maxWidth)
                {
                    return font;
                }

                font.Dispose();
                size -= 1f;
            }
        }

        private float Measure(string text, Font font)
        {
            if (string.IsNullOrEmpty(text) || font == null)
            {
                return 0f;
            }

            EnsureMeasureGraphics();
            return _measureGraphics.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic).Width;
        }

        private void EnsureMeasureGraphics()
        {
            if (_measureGraphics != null)
            {
                return;
            }

            _measureBitmap = new Bitmap(1, 1);
            _measureGraphics = Graphics.FromImage(_measureBitmap);
        }

        /// <summary>
        /// 画一段文案。当前语言是从右往左读（阿拉伯语）时自动切到 RTL 排版。
        ///
        /// GDI+ 的 DirectionRightToLeft 实测行为（临时工程验证过）：
        /// 1. 文字内部的数字 / ASCII 片段仍保持左到右（Unicode Bidi 的标准行为），
        ///    "19" 不会变成 "91"，IP 也不会被倒过来；
        /// 2. 阿拉伯字形整形 GDI+ 会自己做（与 TextRenderer 的连字形态一致）；
        /// 3. 对齐方向自动跟着方向翻转：Near 变成「贴矩形右缘」，Far 变成「贴左缘」。
        ///
        /// 所以这里不需要手工对调 Near / Far —— 交给 GDI+ 处理，
        /// 否则会对调两次，等于没翻转。
        /// </summary>
        private void DrawText(
            Graphics g,
            string text,
            Font font,
            Color color,
            RectangleF rect,
            StringAlignment horizontal,
            StringAlignment vertical,
            bool ellipsis = false)
        {
            if (g == null || font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            rect = Snap(rect);
            if (rect.Width <= 0f || rect.Height <= 0f)
            {
                return;
            }

            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.Alignment = horizontal;
                format.LineAlignment = vertical;
                // NoClip：字宽差半个像素时别把最后一个字裁掉；NoWrap：所有文案单行。
                // 超长文案靠 Trimming 出省略号（NoClip 下照样生效）。
                format.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                if (_rtl)
                {
                    format.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
                }

                format.Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None;

                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.DrawString(text, font, brush, rect, format);
                }
            }
        }

        /// <summary>
        /// 强制按左到右绘制一段纯 ASCII 内容（数字 / IP / 版本号）。
        ///
        /// 这些是 LTR 单元：阿语界面里它们也必须从左往右读、贴框左缘。
        /// 走 <see cref="DrawText"/> 的话 RTL 标志会把 Near 变成贴右缘，
        /// 窄框里放不下就会被挤出去。
        /// </summary>
        private void DrawTextLtr(
            Graphics g,
            string text,
            Font font,
            Color color,
            RectangleF rect,
            StringAlignment horizontal,
            StringAlignment vertical,
            bool ellipsis = false)
        {
            if (g == null || font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            rect = Snap(rect);
            if (rect.Width <= 0f || rect.Height <= 0f)
            {
                return;
            }

            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.Alignment = horizontal;
                format.LineAlignment = vertical;
                format.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                format.Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None;

                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.DrawString(text, font, brush, rect, format);
                }
            }
        }

        // ===================== 交互 =====================
        private bool IsHover(string id)
        {
            return id != null && string.Equals(_hoverId, id, StringComparison.Ordinal);
        }

        private void HandleHit(Hit hit)
        {
            if (hit == null || !hit.Enabled)
            {
                return;
            }

            switch (hit.Action)
            {
                case HitAction.Power:
                    {
                        Action handler = ToggleServer;
                        if (handler != null)
                        {
                            handler();
                        }

                        break;
                    }

                case HitAction.Copy:
                    {
                        Action handler = CopyAddress;
                        if (handler != null)
                        {
                            handler();
                        }

                        break;
                    }

                case HitAction.OpenLanguage:
                    ShowLanguageMenu();
                    break;

                case HitAction.FocusPort:
                    if (_portBox != null)
                    {
                        _portBox.Focus();
                    }

                    break;

                case HitAction.FocusPass:
                    if (_passBox != null)
                    {
                        _passBox.Focus();
                    }

                    break;

                case HitAction.Disconnect:
                    {
                        Action<int> handler = DisconnectSlot;
                        if (handler != null)
                        {
                            handler(hit.Slot);
                        }

                        break;
                    }

                case HitAction.RemoveDevice:
                    {
                        Action<int> handler = RemoveDevice;
                        if (handler != null)
                        {
                            handler(hit.Slot);
                        }

                        break;
                    }
            }

            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            // 点空白/按钮时把焦点收回来：子输入框的 Leave 才会触发「失焦即提交」。
            Focus();

            Hit hit = HitAt(CurrentFrame(), e.Location);
            if (hit != null && hit.Enabled)
            {
                HandleHit(hit);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            Hit hit = HitAt(CurrentFrame(), e.Location);
            string hoverId = hit != null && hit.Enabled ? hit.Id : null;
            if (!string.Equals(hoverId, _hoverId, StringComparison.Ordinal))
            {
                _hoverId = hoverId;
                Invalidate();
            }

            Cursor desired;
            if (hoverId == null)
            {
                desired = Cursors.Default;
            }
            else if (hoverId.StartsWith("focus:", StringComparison.Ordinal))
            {
                desired = Cursors.IBeam;
            }
            else
            {
                desired = Cursors.Hand;
            }

            if (Cursor != desired)
            {
                Cursor = desired;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_hoverId != null)
            {
                _hoverId = null;
                Invalidate();
            }

            Cursor = Cursors.Default;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            InvalidateFrame();
            Invalidate();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);

            // 先把子控件换成新比例的字体重建，再释放旧字体。
            float scale = FitScale();
            Font newPortFont = Theme.Mono(13f * scale);
            Font newPassFont = Theme.Sans(13f * scale);
            Font newPwdFont = Theme.Sans(12f * scale);
            Font oldPortFont = _portFont;
            Font oldPassFont = _passFont;
            Font oldPwdFont = _pwdFont;

            _portFont = newPortFont;
            _passFont = newPassFont;
            _pwdFont = newPwdFont;
            _inputFontScale = scale;

            if (_portBox != null)
            {
                _portBox.Font = newPortFont;
            }

            if (_passBox != null)
            {
                _passBox.Font = newPassFont;
            }

            if (_pwdToggle != null)
            {
                _pwdToggle.Font = newPwdFont;
            }

            if (oldPortFont != null)
            {
                oldPortFont.Dispose();
            }

            if (oldPassFont != null)
            {
                oldPassFont.Dispose();
            }

            if (oldPwdFont != null)
            {
                oldPwdFont.Dispose();
            }

            InvalidateFrame();
            Invalidate();
        }

        private void ToastTimerTick(object sender, EventArgs e)
        {
            _toastTimer.Stop();
            _toastText = null;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_toastTimer != null)
                {
                    _toastTimer.Stop();
                    _toastTimer.Tick -= ToastTimerTick;
                    _toastTimer.Dispose();
                }

                if (_portFont != null)
                {
                    _portFont.Dispose();
                    _portFont = null;
                }

                if (_passFont != null)
                {
                    _passFont.Dispose();
                    _passFont = null;
                }

                if (_pwdFont != null)
                {
                    _pwdFont.Dispose();
                    _pwdFont = null;
                }

                if (_langMenu != null)
                {
                    _langMenu.Dispose();
                }

                if (_measureGraphics != null)
                {
                    _measureGraphics.Dispose();
                    _measureGraphics = null;
                }

                if (_measureBitmap != null)
                {
                    _measureBitmap.Dispose();
                    _measureBitmap = null;
                }

                // 二维码图片由宿主拥有，这里绝不 Dispose。
            }

            base.Dispose(disposing);
        }

        // ===================== 内部类型 =====================
        private enum HitAction
        {
            None,
            Power,
            Copy,
            OpenLanguage,
            FocusPort,
            FocusPass,
            Disconnect,
            RemoveDevice
        }

        private enum ButtonStyle
        {
            Plain,
            Link,
            Danger
        }

        private sealed class Hit
        {
            public RectangleF Rect;
            public string Id;
            public HitAction Action;
            public int Slot;
            public bool Enabled = true;
        }

        private sealed class CardInfo
        {
            public RectangleF Bounds;
            public SlotItem Item;
            public bool Occupied;
            public int SlotNumber;
            public int Index;

            /// <summary>"xbox" / "ds4"，决定槽位浅色底与图标颜色。</summary>
            public string Brand = "xbox";

            public RectangleF NumberRect;
            public RectangleF TitleRect;
            public RectangleF StateRect;
            public RectangleF TypeRect;
            public RectangleF MetaRect;
            public RectangleF FooterLine;
            public RectangleF GlyphRect;
            public RectangleF HintRect;
            public RectangleF DeviceRect;
            public RectangleF AddressRect;
            public RectangleF PrimaryButton;
            public RectangleF DeleteButton;

            /// <summary>这个槽位上是否常驻着虚拟设备（空闲也可能有）。</summary>
            public bool HasDevice;
        }

        private sealed class Frame
        {
            public float Scale = 1f;

            /// <summary>true = 从右往左读（阿拉伯语）：布局镜像，文字对齐方向对调。</summary>
            public bool Rtl;

            /// <summary>等比缩放后的居中偏移：布局用设计坐标，绘制前统一换算。</summary>
            public float OffsetX;
            public float OffsetY;

            // 第一行：服务面板（地址 / 端口密码 / 启停）
            public RectangleF ServicePanel;
            public RectangleF AddrTitle;
            public RectangleF AddrText;
            public RectangleF CopyButton;
            public RectangleF AddrNote;
            public RectangleF PortLabel;
            public RectangleF PortInput;
            public RectangleF PassLabel;
            public RectangleF PassInput;
            public RectangleF FieldHint;
            public RectangleF StatusDot;
            public RectangleF StatusText;
            public RectangleF PowerButton;

            // 第二行：配置列 + 槽位网格
            public RectangleF ConfigPanel;
            public RectangleF ConfigTitle;
            public RectangleF LangLabel;
            public RectangleF LangField;
            public RectangleF QrBox;
            public RectangleF SlotsPanel;
            public RectangleF SlotsTitle;
            public RectangleF SlotCount;
            public RectangleF FooterRow;

            public List<CardInfo> Cards = new List<CardInfo>();
            public List<Hit> Hits = new List<Hit>();
        }
    }
}
