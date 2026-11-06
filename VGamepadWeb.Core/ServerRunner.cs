using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace VGamepadWeb.Core
{
    public class ServerRunner
    {
        private WebApplication? _app;

        // الأحداث المركزية للـ WinForm لمنع الـ Null Reference
        public event Action<string, int>? OnControllerIdAssigned;
        public event Action<string>? OnPlayerDisconnected;

        public GamepadManager? GamepadManager => _app?.Services.GetRequiredService<GamepadManager>();
        public WebRTCSessionManager? WebRTCSessionManager => _app?.Services.GetRequiredService<WebRTCSessionManager>();

        // 🔥 تم تحديث الدالة لتقبل البورت وكلمة المرور وتشغيل الـ React على نفس السيرفر
        public async Task StartServerAsync(int port = 5000, string password = "")
        {
            // تعيين كلمة المرور في الـ Hub فوراً قبل بدء تشغيل السيرفر لتأمين القنوات مباشرة
            GamepadHub.SetPassword(password);

            var builder = WebApplication.CreateBuilder();

            builder.Services.AddSignalR();
            builder.Services.AddSingleton<GamepadManager>();
            builder.Services.AddSingleton<WebRTCSessionManager>();

            // تفعيل الكشف الديناميكي عن مجلد wwwroot المرفق مع الـ WinForm
            string wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            if (Directory.Exists(wwwrootPath))
            {
                builder.Environment.WebRootPath = wwwrootPath;
            }

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyHeader()
                          .AllowAnyMethod()
                          .SetIsOriginAllowed(_ => true)
                          .AllowCredentials();
                });
            });

            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

            _app = builder.Build();

            _app.UseCors();

            // 🔥 1. البحث التلقائي عن الملفات الافتراضية مثل index.html عند الدخول للرابط الأساسي
            _app.UseDefaultFiles();

            // 🔥 2. تخديم ملفات الـ React (JS, CSS, Images) المتواجدة في wwwroot
            _app.UseStaticFiles();

            // خريطة الـ Hub الخاص بالـ SignalR
            _app.MapHub<GamepadHub>("/gamepadhub");

            // 🔥 3. حماية مسارات الـ React لضمان إعادة توجيه أي مسار عشوائي إلى صفحة الـ index.html لتجنب خطأ 404
            _app.MapFallbackToFile("index.html");

            var hubContext = _app.Services.GetRequiredService<IHubContext<GamepadHub>>();
            var rtcManager = _app.Services.GetRequiredService<WebRTCSessionManager>();
            var gamepadManager = _app.Services.GetRequiredService<GamepadManager>();

            // ربط الأحداث داخلياً فور بناء الـ App بنجاح
            gamepadManager.OnControllerIdAssigned += (connId, id) => OnControllerIdAssigned?.Invoke(connId, id);
            rtcManager.OnPlayerDisconnected += (connId) => OnPlayerDisconnected?.Invoke(connId);

            // 槽位任何变化（占用 / 释放 / 锁定 / 改名 / 改类型）都广播给所有手机端，选槽界面靠这个刷新。
            gamepadManager.OnSlotsChanged += () => BroadcastSlots(hubContext, gamepadManager.GetSlotInfos());

            rtcManager.OnAnswerReady += async (connectionId, sdpAnswer) =>
            {
                await hubContext.Clients.Client(connectionId).SendAsync("ReceiveAnswer", sdpAnswer);
            };

            rtcManager.OnIceCandidateReady += async (connectionId, iceCandidate) =>
            {
                await hubContext.Clients.Client(connectionId).SendAsync("ReceiveIceCandidate", iceCandidate);
            };

            await _app.StartAsync();
        }

        public void UpdateServerPassword(string newPassword)
        {
            GamepadHub.SetPassword(newPassword);
        }

        /// <summary>启动时把桌面端落盘的槽位配置（类型 / 名称 / 锁定）灌进管理器。</summary>
        public void ApplySlotConfig(IReadOnlyList<SlotConfig> configs)
        {
            GamepadManager?.ApplySlotConfig(configs);
        }

        /// <summary>
        /// 踢出：先把手稳定码发给手机端，再断开 WebRTC 并释放槽位。
        /// SignalR 连接不动，客户端收到 Kicked 自己收尾（否则它只会看到「掉线」）。
        /// </summary>
        public bool Kick(string connectionId, string reason)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                return false;
            }

            WebRTCSessionManager? rtc = WebRTCSessionManager;
            if (rtc == null)
            {
                return false;
            }

            IHubContext<GamepadHub>? hubContext = _app?.Services.GetService<IHubContext<GamepadHub>>();
            if (hubContext != null)
            {
                SendKicked(hubContext, connectionId, reason);
            }

            rtc.EndConnection(connectionId);
            return true;
        }

        private static async void SendKicked(IHubContext<GamepadHub> hubContext, string connectionId, string reason)
        {
            try
            {
                await hubContext.Clients.Client(connectionId).SendAsync("Kicked", reason);
            }
            catch
            {
                // 客户端已经走了：断开本身仍然有效。
            }
        }

        private static async void BroadcastSlots(IHubContext<GamepadHub> hubContext, IReadOnlyList<SlotInfo> slots)
        {
            try
            {
                await hubContext.Clients.All.SendAsync("SlotChanged", slots);
            }
            catch
            {
                // 广播失败不影响槽位状态本身。
            }
        }

        public async Task StopServerAsync()
        {
            if (_app != null)
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
                _app = null;
            }
        }
    }
}