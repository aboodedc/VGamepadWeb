using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using QRCoder;
using VGamepadWeb.Core;
using VGamepadWeb.WinForm.UI;

namespace VGamepadWeb.WinForm
{
    public partial class Form1 : Form
    {
        /// <summary>槽位固定 4 个，不增不减，与 Core 的 _availableIndexes 长度一致。</summary>
        private const int SlotCount = AppSettings.SlotCount;

        // toast 等级由界面层决定观感，这里只传稳定键。
        private const string ToastSuccess = "success";
        private const string ToastWarn = "warn";
        private const string ToastError = "error";

        // 槽位卡的设备类型稳定键；显示文案由 Strings.TypeLabel 按语言出。
        private const string DeviceXbox = "xbox";
        private const string DeviceDualShock4 = "ds4";

        /// <summary>踢出原因固定值，手机端按它显示提示。</summary>
        private const string KickReason = "kicked_by_admin";

        private readonly ServerRunner _server = new ServerRunner();
        private readonly AppSettings _settings = AppSettings.Load();

        private ServerPhase _phase = ServerPhase.Stopped;
        private DateTimeOffset? _startedAt;

        // 500ms 兜底刷新：事件漏了也能自愈，界面永远读最新快照。
        private System.Windows.Forms.Timer _refreshPump;

        // 二维码按「地址字符串」缓存；地址变了才重建，旧图交接完再 Dispose。
        private Image _qrImage;
        private string _qrAddress;

        // 托盘：点 × 只是隐藏到这里，只有托盘菜单「退出」才真的关。
        private readonly TrayController _tray;
        private bool _allowExit;
        private bool _exiting;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ServerPhase CurrentState
        {
            get => _phase;
            set
            {
                if (value != _phase)
                {
                    _phase = value;
                    RefreshUi();
                }
            }
        }

        public Form1()
        {
            // 主题必须在控件创建之前定下来：ConsoleView 构造时就按 Theme 取底色和文字色。
            Theme.UseTheme(ResolveDarkTheme());

            InitializeComponent();

            _server.OnControllerIdAssigned += HandlePlayerConnected;
            _server.OnPlayerDisconnected += HandlePlayerDisconnected;

            // 界面 → 业务：一一对应，没有中间协议层。
            console.ToggleServer += OnToggleServer;
            console.CopyAddress += CopyAddress;
            console.CommitCredentials += OnCommitCredentials;
            console.LanguageChanged += OnLanguageChanged;
            console.DisconnectSlot += OnDisconnectSlot;
            console.RemoveDevice += OnRemoveDevice;
            console.ToggleTheme += OnToggleTheme;

            _tray = new TrayController(this, ShowFromTray, ExitApplication, _settings.Language);

            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // 初始焦点落在自绘容器上，不要停在端口/密码框：服务刚起来时端口多半不用改，
            // 让光标在那儿闪只会让人以为漏改了配置。想改端口/密码用鼠标点过去即可。
            if (ActiveControl is TextBox && !console.ContainsFocus)
            {
                console.Focus();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _refreshPump = new System.Windows.Forms.Timer { Interval = 500 };
            // 收进托盘/最小化时没人看得见，不用每 500ms 建一遍模型（还顺带枚举网卡）。
            _refreshPump.Tick += (_, _) => { if (Visible) RefreshUi(); };
            _refreshPump.Start();

            FormClosed += (_, _) =>
            {
                _refreshPump.Stop();
                _refreshPump.Dispose();

                _qrImage?.Dispose();
                _qrImage = null;

                _tray.Dispose();
            };

            BackColor = Theme.Page;
            ApplyTitleBarTheme();
            RefreshUi();
        }

        // ==========================================
        // 🖼 界面刷新：BuildModel → 二维码 → console.Apply
        // ==========================================
        private void RefreshUi()
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(RefreshUi));
                }
                catch (InvalidOperationException)
                {
                    // 句柄已销毁：丢弃这次刷新。
                }
                return;
            }

            // 网卡只枚举一次：BuildModel 和下面的地址都用这一份。
            // 之前每拍枚举两遍，2Hz 跑几个小时，在电池机器上很显眼。
            List<LocalAddress> allIps = NetworkHelper.GetAllLocalIPv4Addresses();
            UiModel model = BuildModel(allIps);

            // 二维码按地址缓存：只有地址变了才重建，避免 2Hz 反复造 GDI 位图。
            string address = allIps.Count > 0 ? $"{allIps[0].Ip}:{_settings.Port}" : string.Empty;
            Image previousQr = _qrImage;
            if (!string.Equals(address, _qrAddress, StringComparison.Ordinal))
            {
                _qrImage = BuildQrImage(address);
                _qrAddress = address;
            }

            console.Apply(model, _qrImage);

            // 交接完成之后再释放旧图：ConsoleView 只持有引用、不负责释放它。
            if (!ReferenceEquals(previousQr, _qrImage))
            {
                previousQr?.Dispose();
            }
        }

        private UiModel BuildModel(List<LocalAddress> allIps)
        {
            bool running = CurrentState == ServerPhase.Running;

            // 首选地址 == GetAllLocalIPv4Addresses()[0]（NetworkHelper 内部即如此实现）；
            // 无网卡时给空串，不能把 127.0.0.1 当作可达地址。
            string ipv4 = allIps.Count > 0 ? allIps[0].Ip : string.Empty;

            IReadOnlyList<GamepadSnapshot> snapshot = GetGamepadSnapshot();

            var model = new UiModel
            {
                Phase = CurrentState,
                StartedAt = running ? _startedAt : null,
                Port = _settings.Port,
                Password = _settings.Password,
                Ipv4 = ipv4,
                Language = _settings.Language,
                Version = Application.ProductVersion.Split('+')[0],
                Online = snapshot.Count(item => item.Occupied)
            };

            foreach (LocalAddress item in allIps)
            {
                model.Ips.Add(new IpItem
                {
                    Ip = item.Ip,
                    Kind = item.Kind,
                    Current = string.Equals(item.Ip, ipv4, StringComparison.Ordinal)
                });
            }

            var bySlot = new Dictionary<int, GamepadSnapshot>();
            foreach (GamepadSnapshot item in snapshot)
            {
                bySlot[item.Slot] = item;
            }

            // 固定 4 条，顺序即槽位号 1..4；空槽 Type 空串、ControllerId -1。
            // Type 反映的是「这个槽位上有没有设备」而不是「有没有人连着」：
            // 设备常驻，空闲槽也可能挂着上一台，界面靠它决定要不要显示「卸载」。
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                int index = slot - 1;
                bool hasLive = bySlot.TryGetValue(index, out GamepadSnapshot liveSlot);
                bool occupied = hasLive && liveSlot.Occupied;

                model.Slots.Add(new SlotItem
                {
                    Slot = slot,
                    Occupied = occupied,
                    Type = hasLive && !string.IsNullOrEmpty(liveSlot.ControllerType) ? MapCoreType(liveSlot.ControllerType) : string.Empty,
                    ControllerId = occupied ? liveSlot.Slot : -1,
                    DeviceName = occupied ? liveSlot.DeviceName ?? string.Empty : string.Empty,
                    RemoteIp = occupied ? liveSlot.RemoteIp ?? string.Empty : string.Empty
                });
            }

            return model;
        }

        /// <summary>Core 的 ControllerType 是枚举 ToString：Xbox360 / DualShock4；空槽给 null。</summary>
        private static string MapCoreType(string coreType) => coreType switch
        {
            nameof(TypeController.DualShock4) => DeviceDualShock4,
            _ => DeviceXbox
        };

        // ==========================================
        // 🔳 二维码：地址变了才重建，旧图就地 Dispose
        // ==========================================
        private string BuildAddress()
        {
            List<LocalAddress> allIps = NetworkHelper.GetAllLocalIPv4Addresses();
            return allIps.Count > 0 ? $"{allIps[0].Ip}:{_settings.Port}" : string.Empty;
        }

        private static Image BuildQrImage(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            try
            {
                using var generator = new QRCodeGenerator();
                using QRCodeData data = generator.CreateQrCode("http://" + address, QRCodeGenerator.ECCLevel.Q);
                using var png = new PngByteQRCode(data);

                using var stream = new MemoryStream(png.GetGraphic(6));
                using Image decoded = Image.FromStream(stream);

                // Image.FromStream 要求源流在图片存活期间一直可读，所以复制一份再返回。
                return new Bitmap(decoded);
            }
            catch (Exception)
            {
                // 二维码只是便利功能：画不出来就返回 null，界面自己降级。
                return null;
            }
        }

        // ==========================================
        // 🎮 槽位数据
        // ==========================================
        private IReadOnlyList<GamepadSnapshot> GetGamepadSnapshot()
        {
            GamepadManager gamepadManager = _server.GamepadManager;
            return gamepadManager != null ? gamepadManager.GetSnapshot() : Array.Empty<GamepadSnapshot>();
        }

        // ==========================================
        // ⚙️ 启停
        // ==========================================
        private void OnToggleServer()
        {
            if (CurrentState == ServerPhase.Running || CurrentState == ServerPhase.Stopping)
            {
                _ = StopServerCoreAsync();
            }
            else
            {
                _ = StartServerCoreAsync();
            }
        }

        private async Task StartServerCoreAsync()
        {
            if (CurrentState != ServerPhase.Stopped)
            {
                return;
            }

            if (_settings.Port <= 0 || _settings.Port > 65535)
            {
                ShowToast("toast.portInvalid", ToastError);
                return;
            }

            try
            {
                CurrentState = ServerPhase.Starting;

                await _server.StartServerAsync(_settings.Port, _settings.Password);

                // 自检：界面上的「运行中」只说明状态机走到了这一步，不代表服务真的能用。
                // 这里真的去连一次端口、取一次首页，把「假启动」变成会报错的事。
                ServerHealthCheck.Result health = await RunHealthCheckAsync();

                if (!health.Ok)
                {
                    ServerLog.Error($"启动自检未通过：{health.Message}");

                    // 自检没过就把服务收干净：只改状态机的话，端口仍被占着，
                    // 下一次启动就会撞 address already in use。
                    await StopServerQuietlyAsync();

                    CurrentState = ServerPhase.Stopped;
                    ShowToast("toast.selfCheckFailed", ToastError, health.Message);
                    return;
                }

                ServerLog.Info("启动自检通过：端口在监听，首页可访问");

                _startedAt = DateTimeOffset.Now;
                CurrentState = ServerPhase.Running;
            }
            catch (Exception ex)
            {
                _startedAt = null;
                CurrentState = ServerPhase.Stopped;
                ServerLog.Error("启动服务失败", ex);
                ShowToast("toast.startFailed", ToastError, ex.Message);
            }
        }

        /// <summary>
        /// 启动后自检：端口是否真的在监听、首页是否真的能打开。
        /// 只探本机：局域网地址从本机连走的是本机栈，测不出防火墙，别给自己盖章。
        /// </summary>
        private async Task<ServerHealthCheck.Result> RunHealthCheckAsync()
        {
            // Check 内部是同步的 Wait(4s)：扔到线程池，别让窗口卡在「启动中」连一帧都画不出来。
            ServerHealthCheck.Result health = await Task.Run(() => ServerHealthCheck.Check(_settings.Port));

            if (!health.Ok)
            {
                return health;
            }

            ServerHealthCheck.Result homepage = await ServerHealthCheck.CheckHomepageAsync(_settings.Port, "127.0.0.1");
            if (!homepage.Ok)
            {
                return new ServerHealthCheck.Result(false, "首页无法访问：" + homepage.Message, homepage.StatusCode);
            }

            return health;
        }

        private async Task StopServerCoreAsync()
        {
            if (CurrentState != ServerPhase.Running)
            {
                return;
            }

            try
            {
                CurrentState = ServerPhase.Stopping;
                await _server.StopServerAsync();
            }
            catch (Exception ex)
            {
                ShowToast("toast.stopFailed", ToastError, ex.Message);
            }
            finally
            {
                _startedAt = null;
                CurrentState = ServerPhase.Stopped;
            }
        }

        /// <summary>
        /// 启动失败/自检失败时的收尾：把服务停掉，释放端口。
        /// 失败不再往外抛 —— 此刻已经有错误要报给用户了，再叠一个异常只会添乱。
        /// </summary>
        private async Task StopServerQuietlyAsync()
        {
            try
            {
                await _server.StopServerAsync();
            }
            catch (Exception ex)
            {
                ServerLog.Warn($"清理失败的服务时出错：{ex.Message}");
            }
        }

        // ==========================================
        // 🔥 玩家连接事件（来自 SignalR / WebRTC 线程）
        // ==========================================
        private void HandlePlayerConnected(string connectionId, int controllerId)
        {
            RunOnUi(RefreshUi);
        }

        private void HandlePlayerDisconnected(string connectionId)
        {
            RunOnUi(RefreshUi);
        }

        // ==========================================
        // 📋 地址
        // ==========================================
        private void CopyAddress()
        {
            string address = BuildAddress();
            if (address.Length == 0)
            {
                ShowToast("toast.noIp", ToastWarn);
                return;
            }

            try
            {
                Clipboard.SetText(address);
                ShowToast("toast.copied", ToastSuccess, address);
            }
            catch (Exception ex)
            {
                ShowToast("toast.copyFailed", ToastError, ex.Message);
            }
        }

        // ==========================================
        // ⚙️ 设置：端口/密码失焦提交、语言
        // ==========================================
        /// <summary>
        /// 端口/密码是「失焦即提交」：只在停止态生效（运行态输入框由界面锁成只读），
        /// 因此不做重启确认、不做 Stop→Start。运行态收到就直接拒绝并回推旧值。
        /// </summary>
        private void OnCommitCredentials(int port, string password)
        {
            if (CurrentState != ServerPhase.Stopped)
            {
                ShowToast("toast.settingsLocked", ToastWarn);
                RefreshUi();
                return;
            }

            if (port <= 0 || port > 65535)
            {
                ShowToast("toast.portInvalid", ToastError);
                RefreshUi();
                return;
            }

            _settings.Port = port;
            _settings.Password = password ?? string.Empty;
            _server.UpdateServerPassword(_settings.Password);

            PersistSettings();
            RefreshUi();
            ShowToast("toast.settingsSaved", ToastSuccess);
        }

        private void OnLanguageChanged(string language)
        {
            if (!Languages.All.Contains(language) ||
                string.Equals(language, _settings.Language, StringComparison.Ordinal))
            {
                return;
            }

            _settings.Language = language;
            _tray.ApplyLanguage(language);
            PersistSettings();
            RefreshUi();
        }

        private void PersistSettings()
        {
            if (!_settings.Save())
            {
                ShowToast("toast.saveFailed", ToastError, AppSettings.SettingsPath);
            }
        }

        /// <summary>启动主题：手动选过就用存下来的，没选过跟随系统。</summary>
        private bool ResolveDarkTheme()
        {
            if (string.Equals(_settings.ThemeMode, AppSettings.ThemeDark, StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(_settings.ThemeMode, AppSettings.ThemeLight, StringComparison.Ordinal))
            {
                return false;
            }

            return Theme.SystemPrefersDark();
        }

        /// <summary>页脚主题按钮：换配色 → 重画自绘层 → 跟上标题栏 → 落盘。</summary>
        private void OnToggleTheme()
        {
            bool dark = !Theme.Dark;

            Theme.UseTheme(dark);
            _settings.ThemeMode = dark ? AppSettings.ThemeDark : AppSettings.ThemeLight;

            BackColor = Theme.Page;
            ApplyTitleBarTheme();
            console.ApplyTheme();
            PersistSettings();
        }

        // ==========================================
        // 🎮 槽位命令
        // ==========================================
        private void OnDisconnectSlot(int slot)
        {
            if (slot < 1 || slot > SlotCount)
            {
                ShowToast("toast.slotInvalid", ToastWarn);
                return;
            }

            GamepadManager gamepadManager = _server.GamepadManager;
            if (gamepadManager == null)
            {
                ShowToast("toast.notRunning", ToastWarn);
                return;
            }

            GamepadSnapshot target = null;
            foreach (GamepadSnapshot item in gamepadManager.GetSnapshot())
            {
                if (item.Occupied && item.Slot == slot - 1)
                {
                    target = item;
                    break;
                }
            }

            if (target == null || string.IsNullOrEmpty(target.ConnectionId))
            {
                ShowToast("toast.noDevice", ToastWarn, slot);
                return;
            }

            // 走 Kick：先给手机发「你被管理员踢出」的稳定码，再关会话。
            // 直接 EndConnection 的话，手机只看到数据通道关闭，分不清是被踢还是掉线。
            if (_server.Kick(target.ConnectionId, KickReason))
            {
                ShowToast("toast.kickOk", ToastSuccess, slot);
                return;
            }

            // RTC 管理器还没起来（服务刚停）：退回到只释放槽位。
            gamepadManager.DisconnectGamepad(target.ConnectionId);
            RefreshUi();
        }

        /// <summary>
        /// 卸载槽位上的常驻设备。只在空闲时允许：手机还连着就先断开连接再删 ——
        /// 删设备等于把它从系统里拔掉，正在玩的手柄会当场消失。
        /// </summary>
        private void OnRemoveDevice(int slot)
        {
            if (slot < 1 || slot > SlotCount)
            {
                ShowToast("toast.slotInvalid", ToastWarn);
                return;
            }

            GamepadManager gamepadManager = _server.GamepadManager;
            if (gamepadManager == null)
            {
                ShowToast("toast.notRunning", ToastWarn);
                return;
            }

            if (gamepadManager.RemoveDevice(slot - 1))
            {
                ShowToast("toast.deviceRemoved", ToastSuccess, slot);
            }
            else
            {
                // 界面画完之后有人抢了这个槽：设备没删，提示先断开。
                ShowToast("toast.slotBusy", ToastWarn, slot);
            }

            RefreshUi();
        }

        // ==========================================
        // 🪟 托盘与关闭行为：点 × 收进托盘，托盘「退出」才真退
        // ==========================================
        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();

            // 托盘期间刷新泵是停着的，回来先补一拍，别让用户看到过期的槽位。
            RefreshUi();
        }

        private void ExitApplication()
        {
            _allowExit = true;
            Close();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 用户点 × ：只是收进托盘，服务继续跑。用系统原生通知告诉用户窗口去哪了。
            if (!_allowExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                _tray.ShowMinimizedHint(
                    Strings.T(_settings.Language, "tray.minimized.title"),
                    Strings.T(_settings.Language, "tray.minimized.text"),
                    _settings.Language);
                return;
            }

            // 已经走过一次关停流程：放行。
            if (_exiting)
            {
                return;
            }

            // 真退出：先停服再关窗，而且不能在 UI 线程上干等 ——
            // StopAsync 的默认排空上限是 30 秒，卡在这里 Windows 会先给个「未响应」。
            if (e.CloseReason == CloseReason.UserClosing || e.CloseReason == CloseReason.None)
            {
                e.Cancel = true;
                _ = ExitAfterShutdownAsync();
                return;
            }

            // 关机 / 任务管理器结束：不能取消，最多等两秒就走。
            try
            {
                Task.Run(() => _server.StopServerAsync()).Wait(2000);
            }
            catch (Exception)
            {
                // 关停路径上不再冒泡。
            }
        }

        private async Task ExitAfterShutdownAsync()
        {
            _exiting = true;

            try
            {
                Task stop = _server.StopServerAsync();
                // 等不到就算了：进程退出本身也会回收端口和设备。
                Task finished = await Task.WhenAny(stop, Task.Delay(5000));

                // 已经结束了就把它 await 掉：不然这里快速失败的话，异常没人接，日志里什么都没有。
                if (finished == stop)
                {
                    await stop;
                }
            }
            catch (Exception ex)
            {
                ServerLog.Warn($"退出时停止服务失败：{ex.Message}");
            }

            _allowExit = true;
            Close();
        }

        // ==========================================
        // 🌗 标题栏跟随暗色：DWM 属性，系统不认识就静默跳过
        // ==========================================
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeLegacy = 19;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>
        /// Win10 20H1+ 用 20 号属性，更早的 build 只认 19。两个都不认就留系统色 ——
        /// 为一条标题栏不值得加依赖，也不该让它把启动搞崩。
        /// </summary>
        private void ApplyTitleBarTheme()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            int value = Theme.Dark ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(Handle, DwmUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(Handle, DwmUseImmersiveDarkModeLegacy, ref value, sizeof(int));
                }
            }
            catch (Exception)
            {
                // dwmapi 不可用：标题栏保持系统配色，其余照常。
            }
        }

        // ==========================================
        // 🧵 线程工具 + toast
        // ==========================================
        private void RunOnUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(action);
                }
                catch (InvalidOperationException)
                {
                    // 句柄正在销毁：丢弃这次更新。
                }
                return;
            }

            action();
        }

        private void ShowToast(string key, string level, params object[] args)
        {
            console.ShowToast(Strings.T(_settings.Language, key, args), level);
        }
    }
}
