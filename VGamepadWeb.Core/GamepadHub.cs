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
        /// 否则现网缓存的老页面会直接绑定失败。`controllerType` 现在不参与任何判断（类型归槽位），
        /// `slot` 放在末尾且可空：老客户端不传 → 按「最小空闲号」顺序分配。
        /// </summary>
        public async Task SendOffer(
            string sdpOffer,
            int controllerType,
            bool enableVib,
            int sensitivity,
            string clientPassword = "",
            string clientId = "",
            int? slot = null)
        {
            // 校验密码：与服务器设置不一致直接踢掉，不进入任何协商
            if (_serverPassword != "" && clientPassword != _serverPassword)
            {
                Console.WriteLine($"[Auth Warning] Unauthorized connection attempt blocked for: {Context.ConnectionId}");
                await Clients.Caller.SendAsync("AuthFailed", "Invalid server password.");
                Context.Abort();
                return;
            }

            // 身份只用于「优先认领自己用过的槽位」：新客户端传 clientId，旧客户端退回远程 IP。
            string? identity;
            if (!string.IsNullOrEmpty(clientId))
            {
                identity = "id:" + clientId;
            }
            else
            {
                var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress;
                identity = remoteIp == null ? null : "ip:" + remoteIp;
            }

            // 占位必须在 WebRTC 协商之前：占不到就回稳定码并停手，绝不建立 DataChannel。
            if (!_gamepadManager.TryClaimSlot(
                    Context.ConnectionId, slot, identity, enableVib, sensitivity,
                    out int slotIndex, out string reason))
            {
                Console.WriteLine($"[Slot] Refused {Context.ConnectionId}: {reason}");
                await Clients.Caller.SendAsync("SlotUnavailable", reason);
                return;
            }

            Console.WriteLine($"[Slot] {Context.ConnectionId} -> slot {slotIndex}");
            await _rtcManager.StartConnectionAsync(Context.ConnectionId, sdpOffer);
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
            Console.WriteLine($"[Settings] {settingName} -> {value} for Player: {Context.ConnectionId}");
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