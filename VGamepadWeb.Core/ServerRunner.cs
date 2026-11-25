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

            // Set the web root during initialization so its file provider uses the bundled UI.
            string wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                WebRootPath = Directory.Exists(wwwrootPath) ? wwwrootPath : null
            });

            builder.Services.AddSignalR();
            builder.Services.AddSingleton<GamepadManager>();
            builder.Services.AddSingleton<WebRTCSessionManager>();

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
