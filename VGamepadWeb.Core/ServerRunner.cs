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
            ServerLog.Info($"正在启动服务：端口 {port}{(string.IsNullOrEmpty(password) ? "" : "，已设置密码")}");

            try
            {
                // تعيين كلمة المرور في الـ Hub فوراً قبل بدء تشغيل السيرفر لتأمين القنوات مباشرة
                GamepadHub.SetPassword(password);

                // تفعيل الكشف الديناميكي عن مجلد wwwroot المرفق مع الـ WinForm
                string wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
                bool hasWwwroot = Directory.Exists(wwwrootPath);

                // 内容根固定为程序所在目录：wwwroot 就在它下面，
                // 不能让内容根落到 dotnet run 的当前目录（那会让静态资源找错地方）。
                var options = new WebApplicationOptions
                {
                    ContentRootPath = AppContext.BaseDirectory,
                    WebRootPath = hasWwwroot ? wwwrootPath : null
                };

                var builder = WebApplication.CreateBuilder(options);

                builder.Services.AddSignalR();
                builder.Services.AddSingleton<GamepadManager>();
                builder.Services.AddSingleton<WebRTCSessionManager>();

                if (hasWwwroot)
                {
                    // WebRootPath 必须在 WebApplicationOptions 里给：静态文件中间件用的
                    // WebRootFileProvider 是按内容根算出来的，事后改 Environment.WebRootPath
                    // 或调用 builder.WebHost.UseWebRoot 都只是改字符串，不会重建 provider，
                    // 首页照样 404（builder 构造后再 UseWebRoot 甚至会直接抛异常）。
                    ServerLog.Info($"静态资源目录：{wwwrootPath}");
                }
                else
                {
                    // 没有 wwwroot 时页面会是 404 —— 这正是「假启动」最常见的一种。
                    ServerLog.Warn($"找不到静态资源目录：{wwwrootPath}（网页将无法打开，需先执行 npm run build）");
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

                // 槽位任何变化（占用 / 释放 / 设备类型）都广播给所有手机端，选槽界面靠这个刷新。
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

                ServerLog.Info($"服务已启动，正在监听 http://0.0.0.0:{port}");
            }
            catch (Exception ex)
            {
                ServerLog.Error($"服务启动失败：{ex.Message}", ex);
                throw;
            }
        }

        public void UpdateServerPassword(string newPassword)
        {
            GamepadHub.SetPassword(newPassword);
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
            if (_app == null)
            {
                ServerLog.Info("停止服务：当前没有运行中的服务");
                return;
            }

            try
            {
                ServerLog.Info("正在停止服务…");
                await _app.StopAsync();
                await _app.DisposeAsync();
                _app = null;
                ServerLog.Info("服务已停止");
            }
            catch (Exception ex)
            {
                ServerLog.Error("停止服务时出错", ex);
                throw;
            }
        }
    }
}