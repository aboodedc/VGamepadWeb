using Microsoft.AspNetCore.SignalR;

namespace VGamepadWeb.Core
{
    public class GamepadHub : Hub
    {
        private readonly WebRTCSessionManager _rtcManager;
        private readonly GamepadManager _gamepadManager;
        private readonly CemuHookDSUServer _dsuServer;

        // 🔥 استخدام static لضمان بقاء كلمة المرور ثابتة في الذاكرة عبر جميع الطلبات والمكالمات
        private static string _serverPassword = "";

        public GamepadHub(WebRTCSessionManager rtcManager, GamepadManager gamepadManager, CemuHookDSUServer dsuServer)
        {
            _rtcManager = rtcManager;
            _gamepadManager = gamepadManager;
            _dsuServer = dsuServer;
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _rtcManager.EndConnection(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        // تحديث مهم: التحقق من كلمة المرور الاستاتيكية المركزية قبل بدء اتصال الـ WebRTC
        public async Task SendOffer(string sdpOffer, int controllerType, bool enableVib, int sensitivity, string clientPassword = "", bool enableGyro = true, string motionOrientation = "Horizontal")
        {
            // التحقق من تطابق كلمة المرور القادمة من الهاتف مع كلمة مرور السيرفر
            if (_serverPassword == "" || clientPassword == _serverPassword)
            {
                await _rtcManager.StartConnectionAsync(Context.ConnectionId, sdpOffer, (TypeController)controllerType, enableVib, sensitivity, enableGyro, motionOrientation);
            }
            else
            {
                Console.WriteLine($"[Auth Warning] Unauthorized connection attempt blocked for: {Context.ConnectionId}");

                // 1. إعلام الهاتف أولاً بأن كلمة المرور خاطئة
                await Clients.Caller.SendAsync("AuthFailed", "Invalid server password.");

                // 2. 🔥 السطر السحري: طرد العميل وإغلاق اتصال الـ SignalR الخاص به فوراً
                Context.Abort();
            }
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