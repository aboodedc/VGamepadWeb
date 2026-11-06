using SIPSorcery.Net;
using System.Collections.Concurrent;
using System.Text;

namespace VGamepadWeb.Core
{
    public class WebRTCSessionManager
    {
        private readonly GamepadManager _gamepadManager;
        private readonly ConcurrentDictionary<string, RTCPeerConnection> _peerConnections = new();
        private readonly ConcurrentDictionary<string, RTCDataChannel> _dataChannels = new();

        public event Action<string, string> OnIceCandidateReady;
        public event Action<string, string> OnAnswerReady;
        public event Action<string> OnPlayerDisconnected; // حدث الـ WinForm

        public WebRTCSessionManager(GamepadManager gamepadManager)
        {
            _gamepadManager = gamepadManager;
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

        /// <summary>
        /// 只做协商。槽位已在 GamepadHub 里占好，这里不再建手柄、不再决定类型。
        /// </summary>
        public async Task StartConnectionAsync(string connectionId, string sdpOffer)
        {
            var pc = new RTCPeerConnection();
            _peerConnections.TryAdd(connectionId, pc);

            pc.ondatachannel += (dc) =>
            {
                ServerLog.Info($"数据通道已建立（连接 {connectionId}）");
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

            if (parts[0] == "B") // الأزرار (بما فيها ضغطة الـ Touchpad كزر عادي)
            {
                _gamepadManager.OnButtonReceive(connectionId, parts[1], parts[2] == "1");
            }
            else if (parts[0] == "J") // عصا التحكم
            {
                _gamepadManager.OnJoystickMove(connectionId, parts[1], short.Parse(parts[2]), short.Parse(parts[3]));
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