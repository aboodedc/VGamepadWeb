using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System.Collections.Concurrent;

namespace VGamepadWeb.Core
{
    public enum TypeController
    {
        Xbox360,
        DualShock4
    }

    public class GamepadSettings
    {
        public GamepadSettings(bool enableVibration, int joystickSensitivity, TypeController controllerType, IVirtualGamepad gamepad, int controllerIndex)
        {
            EnableVibration = enableVibration;
            JoystickSensitivity = joystickSensitivity;
            ControllerType = controllerType;
            Gamepad = gamepad;
            ControllerIndex = controllerIndex;
        }

        public bool EnableVibration { get; set; }
        public int JoystickSensitivity { get; set; }
        public TypeController ControllerType { get; set; }
        public IVirtualGamepad Gamepad { get; set; }
        public int ControllerIndex { get; set; }
    }

    /// <summary>落盘的槽位配置（启动时由桌面端灌进来）：类型 / 名称 / 锁定。</summary>
    public sealed record SlotConfig(int Index, TypeController Type, string Name, bool Locked);

    /// <summary>推给手机端的槽位投影。Slot 是 0-based，展示号 = Slot + 1 由客户端负责。</summary>
    public sealed record SlotInfo(int Slot, bool Occupied, bool Locked, string Name);

    /// <summary>
    /// GetSnapshot() 的只读投影。Slot 是 0-based 的 ControllerIndex（等于 XInput 槽位），
    /// 展示号 = Slot + 1 由 WinForm 负责。ControllerType 恒为槽位配置的类型，空槽也有值。
    /// </summary>
    public sealed record GamepadSnapshot(
        int Slot, string ControllerType, bool Occupied, string? ConnectionId, string Name, bool Locked);

    public class GamepadManager : IDisposable
    {
        private ViGEmClient client;
        private readonly ConcurrentDictionary<string, GamepadSettings> _controllers = new();

        // 设备槽位常驻：设备在首次认领时创建，断开只清 Owner，Dispose 才销毁。
        private readonly GamepadSlot[] _slots = { new GamepadSlot(), new GamepadSlot(), new GamepadSlot(), new GamepadSlot() };
        private readonly object _indexLock = new object();

        private sealed class GamepadSlot
        {
            public IVirtualGamepad? Device;
            public TypeController Type = TypeController.Xbox360;
            public string Name = string.Empty;
            public bool Locked;
            public string? OwnerConnectionId;

            // 「上一次是谁坐在这个槽位上」。断开不清，重连才能认回同一台设备。
            // 只在本次进程里记（每次运行单独记录），服务重启就没了。
            // ClientId 是手机端 localStorage 里持久化的设备码，比 IP 稳；IP 是拿不到设备码时的退路。
            public string? ClientId;
            public string? RemoteIp;
        }

        public event Action<string, byte, byte> OnVibrationReceived;

        /// <summary>槽位占用 / 锁定 / 名称 / 类型变化后触发，宿主据此广播 SlotChanged。在锁外触发。</summary>
        public event Action? OnSlotsChanged;

        // حدث إعلام السيرفر برقم اليد الذي حصل عليه اللاعب فوراً
        public event Action<string, int> OnControllerIdAssigned;

        // قاموس إكس بوكس
        private readonly Dictionary<string, Xbox360Button> _buttonMapXbox = new(StringComparer.OrdinalIgnoreCase)
        {
            {"A", Xbox360Button.A}, {"B", Xbox360Button.B}, {"X", Xbox360Button.X}, {"Y", Xbox360Button.Y},
            {"Up", Xbox360Button.Up}, {"Down", Xbox360Button.Down}, {"Left", Xbox360Button.Left}, {"Right", Xbox360Button.Right},
            {"Start", Xbox360Button.Start}, {"Back", Xbox360Button.Back}, {"LB", Xbox360Button.LeftShoulder}, {"RB", Xbox360Button.RightShoulder},
            {"LS", Xbox360Button.LeftThumb}, {"RS", Xbox360Button.RightThumb},
            {"Xbox", Xbox360Button.Guide}
        };

        // قاموس بلايستيشن الموحد
        private readonly Dictionary<string, DualShock4Button> _buttonMapDualShock4 = new(StringComparer.OrdinalIgnoreCase)
        {
            {"A", DualShock4Button.Cross}, {"B", DualShock4Button.Circle}, {"X", DualShock4Button.Square}, {"Y", DualShock4Button.Triangle},
            {"Start", DualShock4Button.Options}, {"Back", DualShock4Button.Share}, {"LB", DualShock4Button.ShoulderLeft}, {"RB", DualShock4Button.ShoulderRight},
            {"LS", DualShock4Button.ThumbLeft}, {"RS", DualShock4Button.ThumbRight},
            {"Xbox", DualShock4SpecialButton.Ps},
            {"Touchpad", DualShock4SpecialButton.Touchpad}
        };

        public GamepadManager()
        {
            client = new ViGEmClient();
        }

        /// <summary>
        /// 首次认领槽位时按客户端类型创建设备；设备从此常驻，直到 Dispose。
        /// 振动回调不捕获 connectionId：设备会被多个连接先后复用，必须按槽位当前 Owner 转发。
        /// </summary>
        private IVirtualGamepad CreateGamepad(int slotIndex, TypeController type)
        {
            if (type == TypeController.DualShock4)
            {
                var ds4 = client.CreateDualShock4Controller();
                ds4.FeedbackReceived += (sender, args) => OnVibrationForSlot(slotIndex, args.LargeMotor, args.SmallMotor);
                ds4.Connect();
                return ds4;
            }

            var x360 = client.CreateXbox360Controller();
            x360.FeedbackReceived += (sender, args) => OnVibrationForSlot(slotIndex, args.LargeMotor, args.SmallMotor);
            x360.Connect();
            return x360;
        }

        private void OnVibrationForSlot(int slotIndex, byte largeMotor, byte smallMotor)
        {
            string? owner;
            lock (_indexLock)
            {
                owner = _slots[slotIndex].OwnerConnectionId;
            }

            if (owner == null) return;

            if (_controllers.TryGetValue(owner, out var settings) && settings.EnableVibration)
            {
                OnVibrationReceived?.Invoke(owner, largeMotor, smallMotor);
            }
        }

        /// <summary>
        /// 把常驻设备的中性状态重置一遍：设备断开后仍然活着，必须清掉上一位玩家留下的按键/摇杆状态，
        /// 否则游戏里会一直卡着最后按下的那一下。异常一律吞掉（设备可能正被 Dispose）。
        /// </summary>
        private void ResetNeutral(int slotIndex)
        {
            try
            {
                IVirtualGamepad? device;
                lock (_indexLock)
                {
                    device = _slots[slotIndex].Device;
                }

                if (device == null) return;

                device.ResetReport();
                device.SubmitReport();
            }
            catch
            {
                // 重置失败不能影响断开/接管流程。
            }
        }

        private void RaiseSlotsChanged()
        {
            OnSlotsChanged?.Invoke();
        }

        /// <summary>
        /// 占用槽位。必须在 WebRTC 协商之前调用：占不到就直接拒，别让客户端进入协商。
        /// 指定槽位：必须空闲且未锁定（用户自己选的，优先尊重）；
        /// 不指定：设备码命中 → IP 命中 → 从未被分配过的干净槽 → 最小空闲，全部跳过锁定槽。
        /// 失败时 reason 是稳定码，客户端负责翻译：slot_invalid / slot_taken / slot_locked / no_free_slot。
        /// </summary>
        public bool TryClaimSlot(
            string connectionId,
            int? requestedSlot,
            string? clientId,
            string? remoteIp,
            bool enableVib,
            int sensitivity,
            out int slotIndex,
            out string reason)
        {
            slotIndex = -1;
            reason = string.Empty;

            bool claimed = false;

            lock (_indexLock)
            {
                // 同一连接重复 SendOffer：沿用已经拿到的槽位，不占第二个。
                if (_controllers.TryGetValue(connectionId, out GamepadSettings? existing))
                {
                    slotIndex = existing.ControllerIndex;
                    return true;
                }

                int assigned = -1;

                if (requestedSlot.HasValue)
                {
                    int wanted = requestedSlot.Value;
                    if (wanted < 0 || wanted >= _slots.Length)
                    {
                        reason = "slot_invalid";
                        return false;
                    }

                    GamepadSlot target = _slots[wanted];
                    if (target.OwnerConnectionId != null)
                    {
                        reason = "slot_taken";
                        return false;
                    }

                    if (target.Locked)
                    {
                        reason = "slot_locked";
                        return false;
                    }

                    assigned = wanted;
                }
                else
                {
                    // 第一、二级：认回「上次就是我」的空闲槽（设备码优先，IP 兜底）。
                    assigned = FindOwnedFreeSlot(clientId, remoteIp);

                    // 第三级：给新用户一个从未被分配过的干净槽位，不去占别人留下的位置。
                    if (assigned == -1)
                    {
                        for (int i = 0; i < _slots.Length; i++)
                        {
                            if (IsFree(i) && string.IsNullOrEmpty(_slots[i].ClientId) && string.IsNullOrEmpty(_slots[i].RemoteIp))
                            {
                                assigned = i;
                                break;
                            }
                        }
                    }

                    // 第四级：所有槽位都被人用过，才去占曾用过但已空闲的位。
                    if (assigned == -1)
                    {
                        for (int i = 0; i < _slots.Length; i++)
                        {
                            if (IsFree(i))
                            {
                                assigned = i;
                                break;
                            }
                        }
                    }

                    if (assigned == -1)
                    {
                        reason = "no_free_slot";
                        return false;
                    }
                }

                GamepadSlot slot = _slots[assigned];

                // 设备常驻：首次认领时按「槽位配置的类型」建设备（客户端不再决定类型），之后复用。
                if (slot.Device == null)
                {
                    slot.Device = CreateGamepad(assigned, slot.Type);
                }

                slot.OwnerConnectionId = connectionId;

                // 记下这次是谁坐的：设备码优先，IP 一起记，老客户端没设备码时靠 IP 认人。
                if (!string.IsNullOrEmpty(clientId))
                {
                    slot.ClientId = clientId;
                }

                if (!string.IsNullOrEmpty(remoteIp))
                {
                    slot.RemoteIp = remoteIp;
                }

                _controllers[connectionId] = new GamepadSettings(
                    enableVib, sensitivity, slot.Type, slot.Device, assigned);

                slotIndex = assigned;
                claimed = true;
            }

            if (!claimed)
            {
                return false;
            }

            // 无论新建还是复用，接手前都必须是干净状态。
            ResetNeutral(slotIndex);

            OnControllerIdAssigned?.Invoke(connectionId, slotIndex);
            Console.WriteLine($"[Manager] Assigned Controller ID: {slotIndex} to Player: {connectionId}");
            RaiseSlotsChanged();
            return true;
        }

        /// <summary>空闲 = 没人占用且没被锁定。</summary>
        private bool IsFree(int slot)
        {
            return _slots[slot].OwnerConnectionId == null && !_slots[slot].Locked;
        }

        /// <summary>
        /// 找回「上次是我」的空闲槽：先按设备码，再按 IP。只认本次运行期内的记录。
        /// 调用方必须已持有 _indexLock。
        /// </summary>
        private int FindOwnedFreeSlot(string? clientId, string? remoteIp)
        {
            if (!string.IsNullOrEmpty(clientId))
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (IsFree(i) && string.Equals(_slots[i].ClientId, clientId, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
            }

            if (!string.IsNullOrEmpty(remoteIp))
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (IsFree(i) && string.Equals(_slots[i].RemoteIp, remoteIp, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// 只读查询：这个连接「上次坐的是哪个槽」。手机端用它把上次的槽位标出来，方便一键接回去。
        /// 没有记录或那个槽已被占/被锁 → null。
        /// </summary>
        public int? GetRecommendedSlot(string? clientId, string? remoteIp)
        {
            lock (_indexLock)
            {
                int slot = FindOwnedFreeSlot(clientId, remoteIp);
                return slot == -1 ? null : slot;
            }
        }

        /// <summary>
        /// 释放占用权（踢出与正常断开都走这里）：设备留在槽位上（不 Disconnect、不 Dispose），
        /// 只把 Owner 置空并归零，重连的人拿回这个空槽就还是同一台设备、同一个编号。
        /// </summary>
        public void DisconnectGamepad(string connectionId)
        {
            int slotIndex = -1;

            lock (_indexLock)
            {
                if (_controllers.TryRemove(connectionId, out GamepadSettings? settings))
                {
                    int index = settings.ControllerIndex;
                    if (index >= 0 && index < _slots.Length && _slots[index].OwnerConnectionId == connectionId)
                    {
                        _slots[index].OwnerConnectionId = null;
                        slotIndex = index;
                    }
                }
            }

            if (slotIndex == -1)
            {
                return;
            }

            ResetNeutral(slotIndex);
            Console.WriteLine($"[Manager] Controller ID: ({slotIndex}) is now FREE.");
            RaiseSlotsChanged();
        }

        /// <summary>
        /// 改槽位类型。只有空闲槽位能改：改类型要销毁并重建设备，Windows 会重新枚举、播设备接入音。
        /// 返回 false 表示槽位正被占用；槽号不变。
        /// </summary>
        public bool SetSlotType(int slot, TypeController type)
        {
            IVirtualGamepad? previous = null;

            lock (_indexLock)
            {
                if (slot < 0 || slot >= _slots.Length)
                {
                    return false;
                }

                GamepadSlot target = _slots[slot];
                if (target.OwnerConnectionId != null)
                {
                    return false;
                }

                if (target.Type == type)
                {
                    return true;
                }

                // 设备还没建过：只改配置，首次认领时按新类型建。
                if (target.Device == null)
                {
                    target.Type = type;
                }
                else
                {
                    previous = target.Device;
                    target.Device = null;
                    target.Type = type;
                    target.Device = CreateGamepad(slot, type);
                }
            }

            DisposeDevice(previous);
            RaiseSlotsChanged();
            return true;
        }

        public void SetSlotName(int slot, string name)
        {
            if (slot < 0 || slot >= _slots.Length)
            {
                return;
            }

            string normalized = (name ?? string.Empty).Trim();
            if (normalized.Length > 24)
            {
                normalized = normalized.Substring(0, 24);
            }

            lock (_indexLock)
            {
                _slots[slot].Name = normalized;
            }

            RaiseSlotsChanged();
        }

        /// <summary>锁定只影响「能不能被选中」：锁定槽对任何客户端都不可用，解锁后立即可选。</summary>
        public void SetSlotLocked(int slot, bool locked)
        {
            if (slot < 0 || slot >= _slots.Length)
            {
                return;
            }

            lock (_indexLock)
            {
                _slots[slot].Locked = locked;
            }

            RaiseSlotsChanged();
        }

        /// <summary>启动时把桌面端落盘的槽位配置灌进来；此时还没有设备，只写配置。</summary>
        public void ApplySlotConfig(IReadOnlyList<SlotConfig> configs)
        {
            if (configs == null)
            {
                return;
            }

            lock (_indexLock)
            {
                foreach (SlotConfig config in configs)
                {
                    if (config.Index < 0 || config.Index >= _slots.Length)
                    {
                        continue;
                    }

                    GamepadSlot slot = _slots[config.Index];
                    slot.Type = config.Type;
                    slot.Name = (config.Name ?? string.Empty).Trim();
                    slot.Locked = config.Locked;
                }
            }

            RaiseSlotsChanged();
        }

        /// <summary>推给手机端的槽位投影（不含 connectionId）。</summary>
        public IReadOnlyList<SlotInfo> GetSlotInfos()
        {
            lock (_indexLock)
            {
                var infos = new List<SlotInfo>(_slots.Length);
                for (int slot = 0; slot < _slots.Length; slot++)
                {
                    infos.Add(new SlotInfo(
                        slot,
                        _slots[slot].OwnerConnectionId != null,
                        _slots[slot].Locked,
                        _slots[slot].Name));
                }

                return infos;
            }
        }

        private static void DisposeDevice(IVirtualGamepad? device)
        {
            if (device == null)
            {
                return;
            }

            try
            {
                device.Disconnect();
                if (device is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch
            {
                // 旧设备回收失败不影响新设备接管。
            }
        }

        public void OnButtonReceive(string connectionId, string buttonName, bool isPressed)
        {
            if (_controllers.TryGetValue(connectionId, out var settings))
            {
                if (settings.ControllerType == TypeController.Xbox360)
                {
                    var x360 = (IXbox360Controller)settings.Gamepad;

                    if (buttonName == "LT") x360.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(isPressed ? 255 : 0));
                    else if (buttonName == "RT") x360.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(isPressed ? 255 : 0));
                    else if (_buttonMapXbox.TryGetValue(buttonName, out var button))
                    {
                        x360.SetButtonState(button, isPressed);
                    }
                    x360.SubmitReport();
                }
                else if (settings.ControllerType == TypeController.DualShock4)
                {
                    var ds4 = (IDualShock4Controller)settings.Gamepad;

                    if (buttonName == "LT") ds4.SetSliderValue(DualShock4Slider.LeftTrigger, (byte)(isPressed ? 255 : 0));
                    else if (buttonName == "RT") ds4.SetSliderValue(DualShock4Slider.RightTrigger, (byte)(isPressed ? 255 : 0));
                    else if (buttonName == "Up" || buttonName == "Down" || buttonName == "Left" || buttonName == "Right")
                    {
                        if (!isPressed) ds4.SetDPadDirection(DualShock4DPadDirection.None);
                        else if (buttonName == "Up") ds4.SetDPadDirection(DualShock4DPadDirection.North);
                        else if (buttonName == "Down") ds4.SetDPadDirection(DualShock4DPadDirection.South);
                        else if (buttonName == "Left") ds4.SetDPadDirection(DualShock4DPadDirection.West);
                        else if (buttonName == "Right") ds4.SetDPadDirection(DualShock4DPadDirection.East);
                    }
                    else if (_buttonMapDualShock4.TryGetValue(buttonName, out var button))
                    {
                        ds4.SetButtonState(button, isPressed);
                    }
                    ds4.SubmitReport();
                }
            }
        }

        public void OnJoystickMove(string connectionId, string stickName, short xValue, short yValue)
        {
            if (_controllers.TryGetValue(connectionId, out var settings))
            {
                // 灵敏度之前是个死设置：这里按 sensitivity/100 缩放（10–200%，100% 为原值），再夹回轴值域。
                short x = ScaleAxis(xValue, settings.JoystickSensitivity);
                short y = ScaleAxis(yValue, settings.JoystickSensitivity);

                if (settings.ControllerType == TypeController.Xbox360)
                {
                    var x360 = (IXbox360Controller)settings.Gamepad;
                    if (stickName == "Left")
                    {
                        x360.SetAxisValue(Xbox360Axis.LeftThumbX, x);
                        x360.SetAxisValue(Xbox360Axis.LeftThumbY, y);
                    }
                    else if (stickName == "Right")
                    {
                        x360.SetAxisValue(Xbox360Axis.RightThumbX, x);
                        x360.SetAxisValue(Xbox360Axis.RightThumbY, y);
                    }
                    x360.SubmitReport();
                }
                else if (settings.ControllerType == TypeController.DualShock4)
                {
                    var ds4 = (IDualShock4Controller)settings.Gamepad;

                    byte ps4X = (byte)((x + 32768) / 256);
                    byte ps4Y = (byte)(255 - ((y + 32768) / 256));

                    if (stickName == "Left")
                    {
                        ds4.SetAxisValue(DualShock4Axis.LeftThumbX, ps4X);
                        ds4.SetAxisValue(DualShock4Axis.LeftThumbY, ps4Y);
                    }
                    else if (stickName == "Right")
                    {
                        ds4.SetAxisValue(DualShock4Axis.RightThumbX, ps4X);
                        ds4.SetAxisValue(DualShock4Axis.RightThumbY, ps4Y);
                    }
                    ds4.SubmitReport();
                }
            }
        }

        /// <summary>Xbox 轴值域是 -32768..32767；缩放后必须夹回来，否则强转 short 会绕回反方向。</summary>
        private static short ScaleAxis(short value, int sensitivity)
        {
            int percent = sensitivity <= 0 ? 100 : sensitivity;
            if (percent == 100)
            {
                return value;
            }

            double scaled = value * (percent / 100.0);
            if (scaled > short.MaxValue)
            {
                return short.MaxValue;
            }

            if (scaled < short.MinValue)
            {
                return short.MinValue;
            }

            return (short)Math.Round(scaled);
        }



        public int GetControllerId(string connectionId)
        {
            if (_controllers.TryGetValue(connectionId, out var settings))
            {
                return settings.ControllerIndex;
            }
            return -1;
        }

        // دالة مساعدة لـ WinForms لاستخراج نوع اليد كـ String لعرضه في الكرت
        public string GetControllerTypeString(string connectionId)
        {
            if (_controllers.TryGetValue(connectionId, out var settings))
            {
                return settings.ControllerType.ToString();
            }
            return "Unknown";
        }

        /// <summary>
        /// 只读快照：固定 4 条，Slot = 槽位下标（0-based，等于 XInput 槽位）。
        /// Occupied = 当前有 Owner；ControllerType 恒为槽位配置的类型（空槽也有值），Name / Locked 供管理面用。
        /// </summary>
        public IReadOnlyList<GamepadSnapshot> GetSnapshot()
        {
            var snapshot = new List<GamepadSnapshot>(_slots.Length);
            lock (_indexLock)
            {
                for (int slot = 0; slot < _slots.Length; slot++)
                {
                    GamepadSlot item = _slots[slot];
                    snapshot.Add(new GamepadSnapshot(
                        slot,
                        item.Type.ToString(),
                        item.OwnerConnectionId != null,
                        item.OwnerConnectionId,
                        item.Name,
                        item.Locked));
                }
            }

            return snapshot;
        }

        public void UpdateSettingLive(string connectionId, string settingName, string value)
        {
            if (!_controllers.TryGetValue(connectionId, out var settings))
            {
                return;
            }

            if (settingName == "VibrationEnabled")
            {
                if (bool.TryParse(value, out bool enabled))
                {
                    settings.EnableVibration = enabled;
                }
            }
            else if (settingName == "JoystickSensitivity")
            {
                // 客户端滑块是 10–200%，边界在这里收口，别让脏值把轴值缩放到离谱。
                if (int.TryParse(value, out int sensitivity))
                {
                    settings.JoystickSensitivity = Math.Clamp(sensitivity, 10, 200);
                }
            }
        }

        public void Dispose()
        {
            // 设备是常驻的，只有进程收尾时在这里真正销毁；顺序必须是先 Disconnect 再 Dispose。
            foreach (var slot in _slots)
            {
                DisposeDevice(slot.Device);
                slot.Device = null;
            }

            client?.Dispose();
        }
    }
}