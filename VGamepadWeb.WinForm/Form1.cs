using System.ComponentModel;
using System.Drawing;
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
        private const string ToastInfo = "info";
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
            InitializeComponent();

            _server.OnControllerIdAssigned += HandlePlayerConnected;
            _server.OnPlayerDisconnected += HandlePlayerDisconnected;

            // 界面 → 业务：一一对应，没有中间协议层。
            console.ToggleServer += OnToggleServer;
            console.CopyAddress += CopyAddress;
            console.CommitCredentials += OnCommitCredentials;
            console.LanguageChanged += OnLanguageChanged;
            console.DisconnectSlot += OnDisconnectSlot;

            _tray = new TrayController(this, ShowFromTray, ExitApplication, _settings.Language);

            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _refreshPump = new System.Windows.Forms.Timer { Interval = 500 };
            _refreshPump.Tick += (_, _) => RefreshUi();
            _refreshPump.Start();

            FormClosed += (_, _) =>
            {
                _refreshPump.Stop();
                _refreshPump.Dispose();

                _qrImage?.Dispose();
                _qrImage = null;

                _tray.Dispose();
            };

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

            UiModel model = BuildModel();

            // 二维码按地址缓存：只有地址变了才重建，避免 2Hz 反复造 GDI 位图。
            string address = BuildAddress();
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

        private UiModel BuildModel()
        {
            bool running = CurrentState == ServerPhase.Running;
            List<LocalAddress> allIps = NetworkHelper.GetAllLocalIPv4Addresses();

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
                Address = running && ipv4.Length > 0 ? $"{ipv4}:{_settings.Port}" : string.Empty,
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
            // 名称 / 锁定整套槽位配置已取消：槽位由服务端自动分配，设备类型由客户端自己选。
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                int index = slot - 1;
                bool hasLive = bySlot.TryGetValue(index, out GamepadSnapshot liveSlot);
                bool occupied = hasLive && liveSlot.Occupied;

                model.Slots.Add(new SlotItem
                {
                    Slot = slot,
                    Occupied = occupied,
                    Type = occupied ? MapCoreType(liveSlot.ControllerType) : string.Empty,
                    ControllerId = occupied ? liveSlot.Slot : -1,
                    LatencyMs = null
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

                if (health.LanWarning)
                {
                    // 服务能用，但局域网不通：照样允许运行，只提示。
                    ServerLog.Warn(health.Message);
                    ShowToast("toast.lanUnreachable", ToastWarn, health.Message);
                }
                else
                {
                    ServerLog.Info("启动自检通过：端口在监听，首页可访问");
                }

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
        /// 局域网地址单独探一遍 —— 本机通、局域网不通，通常是防火墙而不是服务坏了。
        /// </summary>
        private async Task<ServerHealthCheck.Result> RunHealthCheckAsync()
        {
            List<string> lanAddresses = NetworkHelper.GetAllLocalIPv4Addresses()
                .Select(item => item.Ip)
                .ToList();

            ServerHealthCheck.Result health = ServerHealthCheck.Check(_settings.Port, lanAddresses);

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

            WebRTCSessionManager rtcManager = _server.WebRTCSessionManager;
            if (rtcManager != null)
            {
                // EndConnection 关 PC、断手柄并触发 OnPlayerDisconnected，刷新由事件带回。
                rtcManager.EndConnection(target.ConnectionId);
            }
            else
            {
                gamepadManager.DisconnectGamepad(target.ConnectionId);
                RefreshUi();
            }
        }

        private GamepadSnapshot FindSnapshot(int index)
        {
            GamepadManager gamepadManager = _server.GamepadManager;
            if (gamepadManager == null)
            {
                return null;
            }

            foreach (GamepadSnapshot item in gamepadManager.GetSnapshot())
            {
                if (item.Slot == index)
                {
                    return item;
                }
            }

            return null;
        }

        // ==========================================
        // 🪟 托盘与关闭行为：点 × 收进托盘，托盘「退出」才真退
        // ==========================================
        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
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

            // 真退出：先停服再关窗，避免端口占用和虚拟手柄靠进程消亡回收。
            // 用 Task.Run 把停顿移出 UI 线程，否则 await 会把续体排回被阻塞的 UI 线程（死锁）。
            try
            {
                Task.Run(() => _server.StopServerAsync()).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // 退出路径上不再冒泡：进程马上结束。
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
