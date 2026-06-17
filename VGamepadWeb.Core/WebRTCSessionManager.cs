using SIPSorcery.Net;
using System.Collections.Concurrent;
using System.Text;

namespace VGamepadWeb.Core
{
    public class WebRTCSessionManager
    {
        private readonly GamepadManager _gamepadManager;
        private readonly MotionServer _motionServer;
        private readonly ConcurrentDictionary<string, RTCPeerConnection> _peerConnections = new();
        private readonly ConcurrentDictionary<string, RTCDataChannel> _dataChannels = new();

        public event Action<string, string> OnIceCandidateReady;
        public event Action<string, string> OnAnswerReady;
        public event Action<string> OnPlayerDisconnected; // حدث الـ WinForm

        public WebRTCSessionManager(GamepadManager gamepadManager, MotionServer motionServer)
        {
            _gamepadManager = gamepadManager;
            _motionServer = motionServer;
            _gamepadManager.OnVibrationReceived += HandleVibrationFromGame;
        }

        private void HandleVibrationFromGame(string connectionId, byte largeMotor, byte smallMotor)
        {
            byte maxVibration = Math.Max(largeMotor, smallMotor);
            if (_dataChannels.TryGetValue(connectionId, out var dc) && dc.readyState == RTCDataChannelState.open)
            {
                dc.send($"V:{maxVibration}");
            }
        }

        public async Task StartConnectionAsync(string connectionId, string sdpOffer, TypeController type, bool enableVib, int sensitivity, bool enableGyro = true, string motionOrientation = "Horizontal")
        {
            var pc = new RTCPeerConnection();
            _peerConnections.TryAdd(connectionId, pc);

            pc.ondatachannel += (dc) =>
            {
                Console.WriteLine($"[WebRTC] Data Channel Opened for: {connectionId}");
                _dataChannels.TryAdd(connectionId, dc);

                int controllerId = _gamepadManager.GetControllerId(connectionId);
                if (controllerId != -1 && dc.readyState == RTCDataChannelState.open)
                {
                    dc.send($"ID:{controllerId}");
                }

                dc.onmessage += (channel, protocol, data) =>
                {
                    string message = Encoding.UTF8.GetString(data);
                    ProcessFastMessage(connectionId, message, dc);
                };
            };

            pc.onicecandidate += (candidate) => { OnIceCandidateReady?.Invoke(connectionId, candidate.toJSON()); };

            pc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdpOffer });
            var answer = pc.createAnswer(null);
            await pc.setLocalDescription(answer);

            OnAnswerReady?.Invoke(connectionId, answer.sdp);
            _gamepadManager.ConnectNewGamepad(connectionId, type, enableVib, sensitivity, enableGyro, motionOrientation);
        }

        public void AddIceCandidate(string connectionId, string iceCandidateJson)
        {
            if (_peerConnections.TryGetValue(connectionId, out var pc) && RTCIceCandidateInit.TryParse(iceCandidateJson, out var candidateInit))
            {
                pc.addIceCandidate(candidateInit);
            }
        }

        private void ProcessFastMessage(string connectionId, string message, RTCDataChannel dc)
        {
            var parts = message.Split(':');
            if (parts.Length == 0) return;

            if (parts[0] == "Ping")
            {
                dc.send($"Pong:{parts[1]}");
                return;
            }

            if (parts[0] == "B") // الأزرار
            {
                _gamepadManager.OnButtonReceive(connectionId, parts[1], parts[2] == "1");
            }
            else if (parts[0] == "J") // عصا التحكم
            {
                _gamepadManager.OnJoystickMove(connectionId, parts[1], short.Parse(parts[2]), short.Parse(parts[3]));
            }
            else if (parts[0] == "M") // 🔥 الحركة (Gyro & Accel)
            {
                int slot = _gamepadManager.GetControllerId(connectionId);
                if (slot != -1 && parts.Length >= 7)
                {
                    // استخدام InvariantCulture لتجنب أخطاء تحويل الفواصل العشرية (بين . و ,) باختلاف لغة النظام
                    float web_alpha = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture); // yaw / Z
                    float web_beta = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);  // pitch / X
                    float web_gamma = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture); // roll / Y
                    float web_ax = float.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture);
                    float web_ay = float.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture);
                    float web_az = float.Parse(parts[6], System.Globalization.CultureInfo.InvariantCulture);

                    string orientation = _gamepadManager.GetMotionOrientation(connectionId);

                    float ax = 0f;
                    float ay = 0f;
                    float az = 0f;
                    float gx = 0f;
                    float gy = 0f;
                    float gz = 0f;

                    if (orientation == "Vertical") // Joy-Con / Portrait style
                    {
                        // DSU X = Web X, DSU Y = -Web Z, DSU Z = Web Y
                        ax = web_ax / 9.80665f;
                        ay = -web_az / 9.80665f;
                        az = web_ay / 9.80665f;

                        // Gyroscope axes mapping:
                        // DSU Pitch = Web beta (gy), DSU Yaw = Web gamma (gz), DSU Roll = Web alpha (gx)
                        gx = web_beta;
                        gy = web_gamma;
                        gz = web_alpha;
                    }
                    else // Horizontal / Landscape style (default)
                    {
                        // DSU X = -Web Y, DSU Y = -Web Z, DSU Z = Web X
                        ax = -web_ay / 9.80665f;
                        ay = -web_az / 9.80665f;
                        az = web_ax / 9.80665f;

                        // Gyroscope axes mapping:
                        // DSU Pitch = Web gamma, DSU Yaw = -Web beta, DSU Roll = -Web alpha
                        gx = web_gamma;
                        gy = -web_beta;
                        gz = -web_alpha;
                    }

                    _motionServer.UpdateMotion(slot, gx, gy, gz, ax, ay, az);
                }
            }
        }

        public void EndConnection(string connectionId)
        {
            if (_peerConnections.TryRemove(connectionId, out var pc)) pc.Close("User Disconnected");
            _dataChannels.TryRemove(connectionId, out _);
            _gamepadManager.DisconnectGamepad(connectionId);
            OnPlayerDisconnected?.Invoke(connectionId); // إعلام الـ WinForm بحذفه فوراً
        }
    }
}