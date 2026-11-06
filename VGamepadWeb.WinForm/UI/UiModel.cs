using System;
using System.Collections.Generic;
using System.Globalization;

namespace VGamepadWeb.WinForm.UI
{
    /// <summary>服务四态。</summary>
    public enum ServerPhase
    {
        Stopped,
        Starting,
        Running,
        Stopping
    }

    /// <summary>槽位卡需要的全部事实。槽位固定 4 个，不增不减。</summary>
    public sealed class SlotItem
    {
        public int Slot { get; set; }

        public bool Occupied { get; set; }

        /// <summary>
        /// 槽位上常驻设备的实际类型："xbox" / "ds4"；没有设备就空串。
        /// 注意这表示「有没有设备」，不是「有没有人连着」—— 空闲槽也可能挂着上一台。
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>手机端自报的设备名（机型）；空槽或老客户端为空串。</summary>
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>手机端的局域网 IP；空槽或老客户端为空串。</summary>
        public string RemoteIp { get; set; } = string.Empty;

        /// <summary>0-based ControllerIndex；空槽为 -1。</summary>
        public int ControllerId { get; set; } = -1;
    }

    public sealed class IpItem
    {
        public string Ip { get; set; } = string.Empty;

        /// <summary>"wifi" / "ethernet" / "other"。</summary>
        public string Kind { get; set; } = "other";

        public bool Current { get; set; }
    }

    /// <summary>界面所需的全部状态快照。Form1 填，ConsoleView 只读。</summary>
    public sealed class UiModel
    {
        public ServerPhase Phase { get; set; } = ServerPhase.Stopped;
        public DateTimeOffset? StartedAt { get; set; }

        public int Port { get; set; } = 5000;
        public string Password { get; set; } = string.Empty;

        public string Ipv4 { get; set; } = string.Empty;

        public string Language { get; set; } = "zh";
        public string Version { get; set; } = "2.0.0";
        public int Online { get; set; }

        public List<IpItem> Ips { get; set; } = new List<IpItem>();

        /// <summary>恒为 4 条，顺序即槽位号 1..4。</summary>
        public List<SlotItem> Slots { get; set; } = new List<SlotItem>();

        public bool IsRunning => Phase == ServerPhase.Running;

        public bool IsBusy => Phase == ServerPhase.Starting || Phase == ServerPhase.Stopping;
    }

    public static class Languages
    {
        public const string Chinese = "zh";
        public const string English = "en";

        /// <summary>阿拉伯语：从右往左读，渲染时要按 RTL 处理。</summary>
        public const string Arabic = "ar";

        public static readonly string[] All = { Chinese, English, Arabic };

        /// <summary>
        /// 系统 UI 语言对应的界面语言。只认中文 / 英文 / 阿拉伯语：
        /// 法、日、德这些没有对应文案，统一回英文，而不是硬套中文。
        /// </summary>
        public static string FromSystem()
        {
            try
            {
                string code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

                if (string.Equals(code, Chinese, StringComparison.OrdinalIgnoreCase))
                {
                    return Chinese;
                }

                if (string.Equals(code, Arabic, StringComparison.OrdinalIgnoreCase))
                {
                    return Arabic;
                }
            }
            catch (Exception)
            {
                // 受限环境拿不到区域设置：回英文，不能因此起不来。
            }

            return English;
        }
    }
}
