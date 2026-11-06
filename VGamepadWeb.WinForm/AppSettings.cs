using System.Text.Json;
using VGamepadWeb.WinForm.UI;

namespace VGamepadWeb.WinForm
{
    /// <summary>槽位类型稳定键：落盘用这个，和 Core 的 TypeController 一一对应。</summary>
    public static class SlotTypes
    {
        public const string Xbox = "xbox";
        public const string DualShock4 = "ds4";
    }

    /// <summary>单个槽位的落盘配置。Index 是 0-based 槽位号，展示号 = Index + 1。</summary>
    public sealed class SlotSetting
    {
        public int Index { get; set; }

        /// <summary>SlotTypes.Xbox / SlotTypes.DualShock4。</summary>
        public string Type { get; set; } = SlotTypes.Xbox;

        public string Name { get; set; } = string.Empty;

        public bool Locked { get; set; }
    }

    /// <summary>
    /// 桌面端设置的唯一模型，落盘到 %APPDATA%\VGamepadWeb\settings.json。
    /// 内容是端口 / 密码 / 语言 + 4 个槽位的类型 / 名称 / 锁定；槽位固定 4 条，不增不减。
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

        /// <summary>恒为 4 条，顺序即槽位号 0..3；Load 的 Normalize 会补齐并纠正非法值。</summary>
        public List<SlotSetting> Slots { get; set; } = CreateDefaultSlots();

        public static string SettingsDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VGamepadWeb");

        public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

        /// <summary>
        /// 启动时读取。文件不存在 → 默认；内容损坏 → 默认。
        /// 旧版本多出来的成员（autoStart / minimizeToTray 等）反序列化时直接忽略，不做兼容分支；
        /// 旧文件没有 slots 时由 Normalize 补成 4 条默认槽位。
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
            catch (Exception)
            {
                // JsonException / IOException / UnauthorizedAccessException：一律按「没有可用设置」处理。
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

            if (string.IsNullOrEmpty(settings.Language) || !Languages.All.Contains(settings.Language))
            {
                settings.Language = Languages.Chinese;
            }

            settings.Slots = NormalizeSlots(settings.Slots);

            return settings;
        }

        private static List<SlotSetting> CreateDefaultSlots()
        {
            var slots = new List<SlotSetting>(SlotCount);
            for (int index = 0; index < SlotCount; index++)
            {
                slots.Add(new SlotSetting { Index = index });
            }

            return slots;
        }

        /// <summary>
        /// 补齐 / 纠正成 4 条：缺的补默认（xbox / 空名 / 未锁），Type 非法回落 xbox，
        /// Index 一律按 0..3 重排，重复与越界的条目直接丢弃。
        /// </summary>
        private static List<SlotSetting> NormalizeSlots(List<SlotSetting> source)
        {
            var slots = new List<SlotSetting>(SlotCount);
            for (int index = 0; index < SlotCount; index++)
            {
                SlotSetting existing = null;
                if (source != null)
                {
                    foreach (SlotSetting item in source)
                    {
                        if (item != null && item.Index == index)
                        {
                            existing = item;
                            break;
                        }
                    }
                }

                var slot = new SlotSetting { Index = index };
                if (existing != null)
                {
                    slot.Type = string.Equals(existing.Type, SlotTypes.DualShock4, StringComparison.OrdinalIgnoreCase)
                        ? SlotTypes.DualShock4
                        : SlotTypes.Xbox;
                    slot.Name = existing.Name ?? string.Empty;
                    slot.Locked = existing.Locked;
                }

                slots.Add(slot);
            }

            return slots;
        }
    }
}
