using System;
using System.Collections.Generic;

namespace VGamepadWeb.WinForm.UI
{
    /// <summary>
    /// 界面文案的唯一出处（doc/01：文案集中取，不许散落字面量）。
    /// 中文/英文一一对应；缺键时回退到键名，方便一眼看出漏了哪条。
    /// </summary>
    public static class Strings
    {
        private static readonly Dictionary<string, (string Zh, string En)> Map =
            new Dictionary<string, (string, string)>(StringComparer.Ordinal)
            {
                // 电源与状态
                ["power.start"] = ("启动服务", "Start"),
                ["power.stop"] = ("停止服务", "Stop"),
                ["power.starting"] = ("启动中…", "Starting…"),
                ["power.stopping"] = ("停止中…", "Stopping…"),
                ["phase.running"] = ("RUNNING", "RUNNING"),
                ["phase.stopped"] = ("SERVICE STOPPED", "SERVICE STOPPED"),
                ["phase.starting"] = ("STARTING", "STARTING"),
                ["phase.stopping"] = ("STOPPING", "STOPPING"),
                ["state.running"] = ("服务运行中", "Service running"),
                ["state.stopped"] = ("服务已停止", "Service stopped"),
                ["state.starting"] = ("服务启动中", "Service starting"),
                ["state.stopping"] = ("服务停止中", "Service stopping"),
                ["state.stoppedHint"] = ("启动后手机即可连接", "Start to let phones connect"),
                ["uptime"] = ("已运行 {0}", "up {0}"),

                // 地址与设置
                ["addr.title"] = ("手机访问地址 · 在手机浏览器中输入", "Phone address · open in mobile browser"),
                ["addr.hint"] = ("手机与电脑需连接同一 Wi-Fi / 局域网，首次连接时输入访问密码。", "Phone and PC must share the same Wi-Fi / LAN. Enter the password on first connect."),
                ["addr.copy"] = ("复制地址", "Copy"),
                ["addr.port"] = ("服务器端口", "Port"),
                ["addr.password"] = ("访问密码", "Password"),
                ["addr.optional"] = ("可选", "optional"),
                ["addr.portHint"] = ("改后需重启服务", "restart required"),
                ["addr.locked"] = ("服务运行中，端口与密码不可修改 — 先停止服务即可编辑，修改即时生效。", "Port and password are locked while running — stop the service to edit them."),
                ["addr.lockedShort"] = ("只读", "read-only"),
                ["addr.unlocked"] = ("修改后即时生效，下次启动服务使用新端口。", "Changes apply immediately and take effect on the next start."),
                ["addr.localIp"] = ("本地 IP", "Local IP"),
                ["addr.show"] = ("显示", "Show"),
                ["addr.hide"] = ("隐藏", "Hide"),
                ["config.title"] = ("配置", "Settings"),
                ["connect.title"] = ("连接指南", "Connection guide"),
                ["lang.label"] = ("界面语言", "Language"),
                ["status.online"] = ("{0} 台设备在线 · 局域网可达", "{0} devices online · reachable on LAN"),

                // 槽位
                ["slot.section"] = ("手柄槽位", "Controller slots"),
                ["slot.player"] = ("玩家 {0}", "Player {0}"),
                ["ip.current"] = ("当前使用", "Current"),
                ["slot.online"] = ("{0} / {1} 已连接", "{0} / {1} connected"),
                ["slot.onlineSuffix"] = ("已连接", "connected"),
                ["slot.maxCount"] = ("最多 {0} 个槽位", "max {0} slots"),
                ["slot.add"] = ("新增槽位", "Add slot"),
                ["slot.type"] = ("类型", "Type"),
                ["slot.emptyNote"] = ("暂无设备，用手机打开上方地址即可占用", "No device yet — open the address above"),
                ["slot.waiting"] = ("等待手机连接", "Waiting for phone"),
                ["slot.connected"] = ("已连接", "Connected"),
                ["slot.idle"] = ("空闲", "Idle"),
                ["slot.latency"] = ("当前延迟", "Latency"),
                ["slot.config"] = ("手柄设置", "Settings"),
                ["slot.disconnect"] = ("断开连接", "Disconnect"),
                ["slot.delete"] = ("删除", "Remove"),
                ["slot.assign"] = ("等待连接…", "Waiting…"),
                ["slot.full"] = ("槽位已满", "Slots full"),
                ["type.xbox"] = ("Xbox 360 手柄", "Xbox 360 controller"),
                ["type.ds4"] = ("DualShock 4 手柄", "DualShock 4 controller"),
                ["type.auto"] = ("跟随手机端", "Follow the phone"),
                ["layout.xbox"] = ("XInput", "XInput"),
                ["layout.ds4"] = ("PS4 布局", "PS4 layout"),
                ["layout.auto"] = ("由手机端决定", "Chosen by the phone"),

                // 开关与语言
                ["setting.autoStart"] = ("开机自动启动", "Start with Windows"),
                ["setting.minimizeToTray"] = ("最小化到托盘", "Minimize to tray"),
                ["lang.zh"] = ("中文", "中文"),
                ["lang.en"] = ("EN", "EN"),

                // 二维码与页脚
                ["qr.caption"] = ("扫码连接", "Scan to connect"),
                ["qr.hint.run"] = ("手机扫码直接打开", "Scan to open on the phone"),
                ["qr.hint.stop"] = ("启动服务后可扫码", "Available once running"),
                ["qr.stopped"] = ("服务未启动", "Service off"),
                ["qr.empty"] = ("暂无地址", "No address"),
                ["footer.github"] = ("GitHub", "GitHub"),
                ["tray.show"] = ("显示窗口", "Show window"),
                ["tray.exit"] = ("退出", "Exit"),
                ["tray.minimized.title"] = ("VGamepadWeb 仍在运行", "VGamepadWeb is still running"),
                ["tray.minimized.text"] = ("已最小化到通知区域，服务保持运行。点托盘图标即可恢复窗口。", "Minimized to the notification area — the server keeps running. Click the tray icon to reopen."),

                // 反馈（toast）
                ["toast.copied"] = ("已复制 {0}", "Copied {0}"),
                ["toast.noIp"] = ("本机没有可用的 IPv4 地址", "No IPv4 address available"),
                ["toast.copyFailed"] = ("复制失败：{0}", "Copy failed: {0}"),
                ["toast.portInvalid"] = ("端口必须在 1-65535 之间", "Port must be between 1 and 65535"),
                ["toast.startFailed"] = ("启动失败：{0}", "Start failed: {0}"),
                ["toast.stopFailed"] = ("停止失败：{0}", "Stop failed: {0}"),
                ["toast.settingsSaved"] = ("设置已保存", "Settings saved"),
                ["toast.settingsLocked"] = ("服务运行中不能修改端口或密码，请先停止服务", "Stop the service before changing port or password"),
                ["toast.slotLimit"] = ("已达到槽位上限（{0}）", "Slot limit reached ({0})"),
                ["toast.slotOnlyLast"] = ("只能删除最后一个槽位", "Only the last slot can be removed"),
                ["toast.slotBusy"] = ("槽位 {0} 正在使用，先断开设备再删除", "Slot {0} is in use — disconnect first"),
                ["toast.slotKeepOne"] = ("至少保留一个槽位", "Keep at least one slot"),
                ["toast.slotInvalid"] = ("无效的槽位", "Invalid slot"),
                ["toast.unknownType"] = ("未知的槽位类型：{0}", "Unknown slot type: {0}"),
                ["toast.notRunning"] = ("服务未运行", "Service is not running"),
                ["toast.noDevice"] = ("槽位 {0} 没有已连接的设备", "Slot {0} has no device"),
                ["toast.configComing"] = ("槽位 {0} 的设置面板将在后续版本提供", "Slot {0} settings arrive in a later build"),
                ["toast.slotFreeHint"] = ("槽位 {0} 空闲：请在手机端选择该槽位发起连接", "Slot {0} is free — pick it on the phone"),
                ["toast.saveFailed"] = ("设置保存失败：无法写入 {0}", "Could not write {0}"),
                ["toast.clipboardOk"] = ("已复制", "Copied")
            };

        /// <summary>取一条文案；lang 不是 en 时一律按中文处理。</summary>
        public static string T(string lang, string key)
        {
            if (!Map.TryGetValue(key, out var pair))
            {
                return key;
            }

            return string.Equals(lang, Languages.English, StringComparison.OrdinalIgnoreCase) ? pair.En : pair.Zh;
        }

        public static string T(string lang, string key, params object[] args)
        {
            string format = T(lang, key);
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        public static string TypeLabel(string lang, string type)
        {
            return T(lang, "type." + NormalizeType(type));
        }

        public static string TypeLayout(string lang, string type)
        {
            return T(lang, "layout." + NormalizeType(type));
        }

        /// <summary>语言自身名字不随界面语言变化，下拉菜单直接用这个。</summary>
        public static string LanguageName(string code)
        {
            return string.Equals(code, Languages.English, StringComparison.OrdinalIgnoreCase) ? "English" : "中文";
        }

        private static string NormalizeType(string type)
        {
            return string.Equals(type, "ds4", StringComparison.OrdinalIgnoreCase) ? "ds4" : "xbox";
        }

        public static string IpLabel(string lang, string kind)
        {
            bool en = string.Equals(lang, Languages.English, StringComparison.OrdinalIgnoreCase);
            switch (kind)
            {
                case "wifi":
                    return "Wi-Fi";
                case "ethernet":
                    return en ? "Ethernet" : "以太网";
                default:
                    return en ? "Other" : "其它";
            }
        }
    }
}
