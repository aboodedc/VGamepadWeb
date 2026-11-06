using System.Text.Json;
using VGamepadWeb.Core;
using VGamepadWeb.WinForm.UI;

namespace VGamepadWeb.WinForm
{
    /// <summary>
    /// 桌面端设置的唯一模型，落盘到 %APPDATA%\VGamepadWeb\settings.json。
    /// 内容是端口 / 密码 / 语言；槽位配置整体取消 —— 设备类型由客户端自己选，
    /// 槽位也由服务端自动分配，桌面端不再有可配的槽位项。
    /// 读写全部走「临时文件 + Move 覆盖」：写一半崩掉不会留下半个 JSON。
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>槽位上限 4（XInput 只有 0–3），与 Core 的槽位数一致。</summary>
        public const int SlotCount = 4;

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public int Port { get; set; } = 5000;

        public string Password { get; set; } = string.Empty;

        /// <summary>界面语言，取值来自 UI.Languages。</summary>
        public string Language { get; set; } = Languages.Chinese;

        public static string SettingsDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VGamepadWeb");

        public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

        /// <summary>
        /// 启动时读取。文件不存在 → 默认；内容损坏 → 默认。
        /// 旧版本多出来的成员（autoStart / minimizeToTray / slots 等）反序列化时直接忽略，不做兼容分支。
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return new AppSettings();
                }

                string json = File.ReadAllText(SettingsPath);
                AppSettings loaded = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);

                return Normalize(loaded);
            }
            catch (Exception ex)
            {
                // JsonException / IOException / UnauthorizedAccessException：一律按「没有可用设置」处理。
                // 但要留痕：不然用户只会遇到「它自己把端口和密码忘了」，还查不出原因。
                ServerLog.Warn($"设置读取失败，本次使用默认值：{ex.Message}");
                return new AppSettings();
            }
        }

        /// <summary>原子写。返回 false 表示落盘失败，调用方负责提示。</summary>
        public bool Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);

                string tempPath = SettingsPath + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(this, SerializerOptions));
                File.Move(tempPath, SettingsPath, overwrite: true);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static AppSettings Normalize(AppSettings settings)
        {
            if (settings == null)
            {
                return new AppSettings();
            }

            settings.Password ??= string.Empty;

            // 手改坏了的端口不能让程序永远起不来：超出范围就回默认。
            if (settings.Port < 1 || settings.Port > 65535)
            {
                settings.Port = 5000;
            }

            if (string.IsNullOrEmpty(settings.Language) || !Languages.All.Contains(settings.Language))
            {
                settings.Language = Languages.Chinese;
            }

            return settings;
        }
    }
}
