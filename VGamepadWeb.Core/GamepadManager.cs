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

    /// <summary>
    /// GetSnapshot() 的只读投影。Slot 是 0-based 的 ControllerIndex（等于 XInput 槽位），
    /// 展示号 = Slot + 1 由 WinForm 负责。
    /// </summary>
    public sealed record GamepadSnapshot(int Slot, string ControllerType, bool Occupied, string? ConnectionId);

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
            public TypeController Type;
            public string? OwnerConnectionId;
            // 谁占用过这个槽位（"id:xxx" 或 "ip:x.x.x.x"）。断开时保留，重连才能认回同一台设备。
            public string? Identity;
        }

        public event Action<string, byte, byte> OnVibrationReceived;

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
                IVirtualGamepad? device = _slots[slotIndex].Device;
                if (device == null) return;

                device.ResetReport();
                device.SubmitReport();
            }
            catch
            {
                // 重置失败不能影响断开/接管流程。
            }
        }

        public void ConnectNewGamepad(string connectionId, TypeController type = TypeController.Xbox360, bool enableVib = true, int sensitivity = 100, string? identity = null)
        {
            if (!_controllers.ContainsKey(connectionId))
            {
                int assignedIndex = -1;
                IVirtualGamepad? gamepad = null;
                TypeController slotType = type;
                bool reusedDevice = false;

                lock (_indexLock)
                {
                    // 第一级：按身份找回自己用过的槽位（空闲且 Identity 相同）；身份为空或没命中再往下走。
                    if (!string.IsNullOrEmpty(identity))
                    {
                        for (int i = 0; i < _slots.Length; i++)
                        {
                            if (_slots[i].OwnerConnectionId == null && _slots[i].Identity == identity)
                            {
                                assignedIndex = i;
                                break;
                            }
                        }
                    }

                    // 第二级：给新人一个从未被认领过的干净槽位，不去抢别人留下的占位。
                    if (assignedIndex == -1)
                    {
                        for (int i = 0; i < _slots.Length; i++)
                        {
                            if (_slots[i].OwnerConnectionId == null && string.IsNullOrEmpty(_slots[i].Identity))
                            {
                                assignedIndex = i;
                                break;
                            }
                        }
                    }

                    // 第三级：兜底取最小空闲槽位（4 格都曾被认领过时才会到这里）。
                    if (assignedIndex == -1)
                    {
                        for (int i = 0; i < _slots.Length; i++)
                        {
                            if (_slots[i].OwnerConnectionId == null)
                            {
                                assignedIndex = i;
                                break;
                            }
                        }
                    }

                    if (assignedIndex != -1)
                    {
                        GamepadSlot slot = _slots[assignedIndex];

                        // 槽位首次被认领时建设备并锁定类型；已有设备则直接复用，忽略这次客户端传来的类型。
                        if (slot.Device == null)
                        {
                            slot.Device = CreateGamepad(assignedIndex, type);
                            slot.Type = type;
                        }
                        else
                        {
                            reusedDevice = true;
                        }

                        slot.OwnerConnectionId = connectionId;
                        if (!string.IsNullOrEmpty(identity))
                        {
                            slot.Identity = identity;
                        }

                        gamepad = slot.Device;
                        slotType = slot.Type;
                    }
                }

                // 四个槽位都被占用：不新建设备，连接不获得手柄。
                if (assignedIndex == -1 || gamepad == null)
                {
                    Console.WriteLine($"[Manager] No free slot: {connectionId} refused.");
                    return;
                }

                // 复用别人用过的设备：先清掉上一位玩家留下的按键/摇杆状态，新玩家接手时必须是干净的。
                if (reusedDevice)
                {
                    ResetNeutral(assignedIndex);
                }

                _controllers.TryAdd(connectionId, new GamepadSettings(enableVib, sensitivity, slotType, gamepad, assignedIndex));

                OnControllerIdAssigned?.Invoke(connectionId, assignedIndex);
                Console.WriteLine($"[Manager] Assigned Controller ID: {assignedIndex} to Player: {connectionId}");
            }
        }

        public void DisconnectGamepad(string connectionId)
        {
            if (_controllers.TryRemove(connectionId, out var settings))
            {
                // 设备留在槽位上（不 Disconnect、不 Dispose），只把 Owner 置空：
                // 重连的人只要拿回这个空槽，就还是同一台设备、同一个编号。
                int slotIndex = -1;
                lock (_indexLock)
                {
                    int index = settings.ControllerIndex;
                    if (index >= 0 && index < _slots.Length && _slots[index].OwnerConnectionId == connectionId)
                    {
                        _slots[index].OwnerConnectionId = null;
                        slotIndex = index;
                    }
                }

                if (slotIndex != -1)
                {
                    ResetNeutral(slotIndex);
                    Console.WriteLine($"[Manager] Controller ID: ({slotIndex}) is now FREE.");
                }
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
                if (settings.ControllerType == TypeController.Xbox360)
                {
                    var x360 = (IXbox360Controller)settings.Gamepad;
                    if (stickName == "Left")
                    {
                        x360.SetAxisValue(Xbox360Axis.LeftThumbX, xValue);
                        x360.SetAxisValue(Xbox360Axis.LeftThumbY, yValue);
                    }
                    else if (stickName == "Right")
                    {
                        x360.SetAxisValue(Xbox360Axis.RightThumbX, xValue);
                        x360.SetAxisValue(Xbox360Axis.RightThumbY, yValue);
                    }
                    x360.SubmitReport();
                }
                else if (settings.ControllerType == TypeController.DualShock4)
                {
                    var ds4 = (IDualShock4Controller)settings.Gamepad;

                    byte ps4X = (byte)((xValue + 32768) / 256);
                    byte ps4Y = (byte)(255 - ((yValue + 32768) / 256));

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
        /// Occupied = 当前有 Owner；ControllerType = 槽位锁定的类型，空槽为 "Unknown"。
        /// </summary>
        public IReadOnlyList<GamepadSnapshot> GetSnapshot()
        {
            var snapshot = new List<GamepadSnapshot>(_slots.Length);
            lock (_indexLock)
            {
                for (int slot = 0; slot < _slots.Length; slot++)
                {
                    string? owner = _slots[slot].OwnerConnectionId;
                    snapshot.Add(owner != null
                        ? new GamepadSnapshot(slot, _slots[slot].Type.ToString(), true, owner)
                        : new GamepadSnapshot(slot, "Unknown", false, null));
                }
            }

            return snapshot;
        }

        public void UpdateSettingLive(string connectionId, string settingName, string value)
        {
            if (_controllers.TryGetValue(connectionId, out var settings))
            {
                if (settingName == "VibrationEnabled")
                {
                    settings.EnableVibration = bool.Parse(value);
                }
                else if (settingName == "JoystickSensitivity")
                {
                    settings.JoystickSensitivity = int.Parse(value);
                }
            }
        }

        public void Dispose()
        {
            // 设备是常驻的，只有进程收尾时在这里真正销毁。
            foreach (var slot in _slots)
            {
                if (slot.Device == null) continue;

                slot.Device.Disconnect();
                if (slot.Device is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                slot.Device = null;
            }
            client?.Dispose();
        }
    }
}