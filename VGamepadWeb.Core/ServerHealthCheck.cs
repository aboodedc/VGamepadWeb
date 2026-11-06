using System.Net.Sockets;

namespace VGamepadWeb.Core
{
    /// <summary>
    /// 启动后的自检：确认服务真的能被人访问。
    ///
    /// 「假启动」有两种：
    /// 1. 端口根本没监听（启动异常被吞了）——靠 TCP 连接探测发现；
    /// 2. 端口监听了但网页打不开（wwwroot 缺失 / 静态文件没构建）——靠 HTTP 取首页发现。
    /// 只看界面上的「运行中」是不够的，界面只反映状态机，不反映服务实际可用。
    /// </summary>
    public static class ServerHealthCheck
    {
        /// <summary>自检结果。Ok 为 true 时 Message 仅作说明，为 false 时是失败原因。</summary>
        public sealed record Result(bool Ok, string Message, int StatusCode);

        /// <summary>
        /// 探测本机服务是否真的可用：端口有没有在监听。
        ///
        /// 只探 127.0.0.1。以前还挨个探局域网 IP，但同一台机器连自己的局域网地址走的是本机栈，
        /// 根本不经过 Windows 防火墙的入站规则 —— 防火墙把端口拦了，那个探测照样通过，
        /// 等于给「假启动」又盖了个「自检通过」的章。测不出来的东西就别测。
        /// </summary>
        public static Result Check(int port, int timeoutMs = 4000)
        {
            Result loopback = Probe("127.0.0.1", port, timeoutMs);

            if (!loopback.Ok)
            {
                return new Result(false, "本机端口未响应：" + loopback.Message, 0);
            }

            return loopback;
        }

        private static Result Probe(string host, int port, int timeoutMs)
        {
            try
            {
                using var client = new TcpClient();
                Task connect = client.ConnectAsync(host, port);
                if (!connect.Wait(timeoutMs))
                {
                    return new Result(false, $"连接 {host}:{port} 超时", 0);
                }

                if (!client.Connected)
                {
                    return new Result(false, $"无法连接 {host}:{port}", 0);
                }

                return new Result(true, "端口已监听", 0);
            }
            catch (Exception ex)
            {
                return new Result(false, ex.Message, 0);
            }
        }

        /// <summary>
        /// 取一次首页，确认网页真的能打开（静态资源在不在、路由通不通）。
        /// </summary>
        public static async Task<Result> CheckHomepageAsync(int port, string host, int timeoutMs = 4000)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };

                HttpResponseMessage response = await client.GetAsync($"http://{host}:{port}/");
                int status = (int)response.StatusCode;

                if (!response.IsSuccessStatusCode)
                {
                    return new Result(false, $"首页返回 HTTP {status}", status);
                }

                string body = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(body))
                {
                    return new Result(false, "首页内容为空", status);
                }

                if (!body.Contains("<div id=\"root\">", StringComparison.OrdinalIgnoreCase)
                    && !body.Contains("id=\"root\"", StringComparison.OrdinalIgnoreCase))
                {
                    return new Result(false, "首页不是预期的网页（缺少 #root 节点）", status);
                }

                return new Result(true, "首页可访问", status);
            }
            catch (Exception ex)
            {
                return new Result(false, ex.Message, 0);
            }
        }
    }
}
