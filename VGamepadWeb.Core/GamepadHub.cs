using Microsoft.AspNetCore.SignalR;

namespace VGamepadWeb.Core
{
    public class GamepadHub : Hub
    {
        private readonly WebRTCSessionManager _rtcManager;
        private readonly GamepadManager _gamepadManager;

        // 🔥 استخدام static لضمان بقاء كلمة المرور ثابتة في الذاكرة عبر جميع الطلبات والمكالمات
        private static string _serverPassword = "";

        public GamepadHub(WebRTCSessionManager rtcManager, GamepadManager gamepadManager)
        {
            _rtcManager = rtcManager;
            _gamepadManager = gamepadManager;
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _rtcManager.EndConnection(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        /// <summary>新连接先拿一份全量槽位，手机端据此渲染选槽界面。</summary>
        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("SlotChanged", _gamepadManager.GetSlotInfos());
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// 占槽 + 建立 WebRTC。
        /// 参数顺序是历史包袱：前五个位置必须与老客户端一致（sdp, controllerType, enableVib, sensitivity, password），
        /// 否则现网缓存的老页面会直接绑定失败。`controllerType` 现在是客户端自选的手柄类型（0 = Xbox 360，1 = DualShock 4），
        /// 服务端照办不干涉；`slot` 放在末尾且可空：老客户端不传 → 按「最小空闲号」顺序分配。
        /// </summary>
        public async Task SendOffer(
            string sdpOffer,
            int controllerType,
            bool enableVib,
            int sensitivity,
            string clientPassword = "",
            string clientId = "",
            int? slot = null,
            string deviceName = "")
        {
            // 校验密码：与服务器设置不一致直接踢掉，不进入任何协商
            if (_serverPassword != "" && clientPassword != _serverPassword)
            {
                ServerLog.Warn($"密码校验失败，已拒绝连接 {Context.ConnectionId}");
                await Clients.Caller.SendAsync("AuthFailed", "Invalid server password.");
                Context.Abort();
                return;
            }

            // 类型由客户端自己选：0 = Xbox 360，其余一律回落 Xbox 360。
            TypeController type = controllerType == (int)TypeController.DualShock4
                ? TypeController.DualShock4
                : TypeController.Xbox360;

            // 身份只用于「优先认领自己上次的槽位」：设备码（clientId）优先，拿不到就退回远程 IP。
            string? remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();

            // 占位必须在 WebRTC 协商之前：占不到就回稳定码并停手，绝不建立 DataChannel。
            if (!_gamepadManager.TryClaimSlot(
                    Context.ConnectionId, slot, clientId, remoteIp, deviceName, type, enableVib, sensitivity,
                    out int slotIndex, out string reason))
            {
                ServerLog.Warn($"占位失败：{reason}（连接 {Context.ConnectionId}）");
                await Clients.Caller.SendAsync("SlotUnavailable", reason);
                return;
            }

            ServerLog.Info($"连接 {Context.ConnectionId} 占用槽位 {slotIndex}");

            try
            {
                await _rtcManager.StartConnectionAsync(Context.ConnectionId, sdpOffer);
            }
            catch (Exception ex)
            {
                // 协商失败（offer 解析不了、客户端中途取消）：槽位和 pc 都收干净，
                // 否则界面上会留一台不存在的手机「占用中」，而那台手机只拿到一个裸异常。
                ServerLog.Error($"WebRTC 协商失败（连接 {Context.ConnectionId}）", ex);
                _rtcManager.EndConnection(Context.ConnectionId);
                await Clients.Caller.SendAsync("SlotUnavailable", "negotiation_failed");
            }
        }

        /// <summary>
        /// 这个连接上次坐的是哪个槽（0-based）。手机端拿它把上次的槽位标出来，一键接回去。
        /// 只读查询，不含认证信息：设备码是客户端自己声明的，本来就只用于「默认建议槽号」。
        /// </summary>
        public int? RecommendSlot(string clientId)
        {
            string? remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
            return _gamepadManager.GetRecommendedSlot(clientId, remoteIp);
        }

        /// <summary>页面关闭时前端尽力而为地调一次；服务端 OnDisconnectedAsync 仍会兜底。</summary>
        public void Release()
        {
            _rtcManager.EndConnection(Context.ConnectionId);
        }

        public void SendIceCandidate(string iceCandidateJson)
        {
            _rtcManager.AddIceCandidate(Context.ConnectionId, iceCandidateJson);
        }

        public void UpdateSetting(string settingName, string value)
        {
            _gamepadManager.UpdateSettingLive(Context.ConnectionId, settingName, value);
            ServerLog.Info($"设置更新 {settingName} -> {value}（连接 {Context.ConnectionId}）");
        }

        // ==========================================
        // 🔥 الدالة الجديدة المخصصة لتعيين كلمة المرور
        // ==========================================
        public static void SetPassword(string newPassword)
        {
            _serverPassword = newPassword ?? "";
        }
    }
}