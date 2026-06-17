using Microsoft.AspNetCore.SignalR;

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
        // إتاحة الوصول لكائن الحركة إذا احتجته في الـ WinForm مستقبلاً
        public MotionServer? MotionServer => _app?.Services.GetRequiredService<MotionServer>();

        // 🔥 تم تحديث الدالة لتقبل البورت وكلمة المرور وتشغيل الـ React على نفس السيرفر
        public async Task StartServerAsync(int port = 5000, string password = "")
        {
            // تعيين كلمة المرور في الـ Hub فوراً قبل بدء تشغيل السيرفر لتأمين القنوات مباشرة
            GamepadHub.SetPassword(password);

            var builder = WebApplication.CreateBuilder();

            builder.Services.AddSignalR();
            builder.Services.AddSingleton<GamepadManager>();
            builder.Services.AddSingleton<MotionServer>(); // سطر الإضافة الخاص بك ممتاز!
            builder.Services.AddSingleton<WebRTCSessionManager>();
            builder.Services.AddSingleton<CemuHookDSUServer>();

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
            var dsuServer = _app.Services.GetRequiredService<CemuHookDSUServer>();

            // 🔥 خطوة أمان أساسية: استدعاء كائن MotionServer صراحة من الحاوية لضمان تفعيل مَشيد الكلاس (Constructor)
            // وبدء خادم الـ UDP بالاستماع فوراً عند إقلاع السيرفر.
            var motionServer = _app.Services.GetRequiredService<MotionServer>();

            // ربط الأحداث داخلياً فور بناء الـ App بنجاح
            gamepadManager.OnControllerIdAssigned += (connId, id) =>
            {
                OnControllerIdAssigned?.Invoke(connId, id);
                dsuServer.UpdateGamepadState(id, 0, 0, 0, 0, 0, 128, 128, 128, 128, 8);
            };
            rtcManager.OnPlayerDisconnected += (connId) =>
            {
                OnPlayerDisconnected?.Invoke(connId);
            };

            // ربط بيانات الحركة من اللاعب إلى خادم CemuHook DSU
            gamepadManager.OnMotionReceivedEvent += (connId, ax, ay, az, gx, gy, gz) =>
            {
                int slot = gamepadManager.GetControllerId(connId);
                if (slot >= 0 && slot < 4)
                {
                    dsuServer.UpdateMotion(slot, ax, ay, az, gx, gy, gz);
                }
            };

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
                // 🔥 خطوة أمان أساسية: تحرير المنفذ 26760 وإغلاق الـ UDP قبل تدمير السيرفر 
                // لكي لا يحدث كراش عند إعادة التشغيل مرة أخرى من الـ WinForm
                try
                {
                    var motionServer = _app.Services.GetService<MotionServer>();
                    motionServer?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ServerRunner] Error disposing MotionServer: {ex.Message}");
                }

                await _app.StopAsync();
                await _app.DisposeAsync();
                _app = null;
            }
        }
    }
}