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
        public GamepadSettings(bool enableVibration, int joystickSensitivity, TypeController controllerType, IVirtualGamepad gamepad, int controllerIndex, bool enableGyro = true, string motionOrientation = "Horizontal")
        {
            EnableVibration = enableVibration;
            JoystickSensitivity = joystickSensitivity;
            ControllerType = controllerType;
            Gamepad = gamepad;
            ControllerIndex = controllerIndex;
            EnableGyro = enableGyro;
            MotionOrientation = motionOrientation;
        }

        public bool EnableVibration { get; set; }
        public int JoystickSensitivity { get; set; }
        public TypeController ControllerType { get; set; }
        public IVirtualGamepad Gamepad { get; set; }
        public int ControllerIndex { get; set; }
        public bool EnableGyro { get; set; }
        public string MotionOrientation { get; set; }
    }

    public class GamepadManager : IDisposable
    {
        private ViGEmClient client;
        private readonly ConcurrentDictionary<string, GamepadSettings> _controllers = new();

        // نظام ذكي لإدارة الأرقام المتاحة ديناميكياً من 0 إلى 255
        private readonly HashSet<int> _usedIndexes = new();
        private readonly object _indexLock = new object();

        public event Action<string, byte, byte> OnVibrationReceived;

        // حدث إعلام السيرفر برقم اليد الذي حصل عليه اللاعب فوراً
        public event Action<string, int> OnControllerIdAssigned;

        // Motion data per controller (ax, ay, az, gx, gy, gz)
        private readonly ConcurrentDictionary<string, (float, float, float, float, float, float)> _motionData = new();
        public event Action<string, float, float, float, float, float, float>? OnMotionReceivedEvent;
        public event Action<string, string, bool>? OnButtonReceivedEvent;
        public event Action<string, string, short, short>? OnJoystickReceivedEvent;

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

        public void ConnectNewGamepad(string connectionId, TypeController type = TypeController.Xbox360, bool enableVib = true, int sensitivity = 100, bool enableGyro = true, string motionOrientation = "Horizontal")
        {
            if (!_controllers.ContainsKey(connectionId))
            {
                int assignedIndex = -1;
                lock (_indexLock)
                {
                    for (int i = 0; i < 256; i++)
                    {
                        if (!_usedIndexes.Contains(i))
                        {
                            _usedIndexes.Add(i);
                            assignedIndex = i; // حجز الرقم ديناميكياً
                            break;
                        }
                    }
                }

                if (assignedIndex == -1)
                {
                    assignedIndex = _controllers.Count;
                }

                IVirtualGamepad gamepad;

                if (type == TypeController.DualShock4)
                {
                    var ds4 = client.CreateDualShock4Controller();
                    ds4.FeedbackReceived += (sender, args) =>
                    {
                        if (enableVib) OnVibrationReceived?.Invoke(connectionId, args.LargeMotor, args.SmallMotor);
                    };
                    ds4.Connect();
                    gamepad = ds4;
                }
                else
                {
                    var x360 = client.CreateXbox360Controller();
                    x360.FeedbackReceived += (sender, args) =>
                    {
                        if (enableVib) OnVibrationReceived?.Invoke(connectionId, args.LargeMotor, args.SmallMotor);
                    };
                    x360.Connect();
                    gamepad = x360;
                }

                _controllers.TryAdd(connectionId, new GamepadSettings(enableVib, sensitivity, type, gamepad, assignedIndex, enableGyro, motionOrientation));

                OnControllerIdAssigned?.Invoke(connectionId, assignedIndex);
                Console.WriteLine($"[Manager] Assigned Controller ID: {assignedIndex} to Player: {connectionId}");
            }
        }

        public void DisconnectGamepad(string connectionId)
        {
            if (_controllers.TryRemove(connectionId, out var settings))
            {
                settings.Gamepad.Disconnect();

                lock (_indexLock)
                {
                    _usedIndexes.Remove(settings.ControllerIndex);
                    Console.WriteLine($"[Manager] Controller ID: ({settings.ControllerIndex}) is now FREE.");
                }
            }
        }

        public void OnButtonReceive(string connectionId, string buttonName, bool isPressed)
        {
            OnButtonReceivedEvent?.Invoke(connectionId, buttonName, isPressed);
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
            OnJoystickReceivedEvent?.Invoke(connectionId, stickName, xValue, yValue);
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

        public void OnMotionReceived(string connectionId, float ax, float ay, float az, float gx, float gy, float gz)
        {
            _motionData[connectionId] = (ax, ay, az, gx, gy, gz);
            OnMotionReceivedEvent?.Invoke(connectionId, ax, ay, az, gx, gy, gz);

            if (_controllers.TryGetValue(connectionId, out var settings))
            {
                int slot = settings.ControllerIndex;
                if (slot >= 0 && slot < 4)
                {
                    var ds4 = settings.Gamepad as IDualShock4Controller;
                    if (ds4 != null)
                    {
                        // Try to avoid calling extra SubmitReport for every motion frame.
                        // ViGEmBus DS4 gyro/accel is set via the same SubmitReport mechanism,
                        // but the v1.21 API doesn't expose direct SetGyroX methods.
                        // Gyro/accel are forwarded via the CemuHook DSU UDP server instead.
                    }
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
                else if (settingName == "GyroEnabled")
                {
                    settings.EnableGyro = bool.Parse(value);
                }
                else if (settingName == "MotionOrientation")
                {
                    settings.MotionOrientation = value;
                }
            }
        }

        public bool IsGyroActiveForSlot(int slotIndex)
        {
            foreach (var controller in _controllers.Values)
            {
                if (controller.ControllerIndex == slotIndex)
                {
                    return controller.EnableGyro;
                }
            }
            return false;
        }

        public string GetMotionOrientation(string connectionId)
        {
            if (_controllers.TryGetValue(connectionId, out var settings))
            {
                return settings.MotionOrientation;
            }
            return "Horizontal";
        }

        public void Dispose()
        {
            foreach (var settings in _controllers.Values)
            {
                settings.Gamepad.Disconnect();
            }
            client?.Dispose();
        }
    }
}