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

        public event Action<string, string>? OnIceCandidateReady;
        public event Action<string, string>? OnAnswerReady;
        public event Action<string>? OnPlayerDisconnected; // حدث الـ WinForm

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

            // 连接可能在我们登记之前就断了：EndConnection 先删条目，随后这条 TryAdd 又把它塞回去。
            // 槽位已经没了（认领被释放）= 这条 pc 没人认领，登记完立刻收掉。
            if (_gamepadManager.GetControllerId(connectionId) == -1)
            {
                _peerConnections.TryRemove(connectionId, out _);
                pc.Close("Session gone");
                return;
            }

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
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            string[] parts = message.Split(':');

            // 报文边界在入口收口：少一段的坏包直接丢。
            // 不检查的话 "Ping" 会 IndexOutOfRange、"J:Left:1:abc" 会 FormatException，
            // 异常从 SCTP 回调里冒出去，整条数据通道静默失效，手机还显示「已连接」。
            switch (parts[0])
            {
                case "Ping":
                    if (parts.Length >= 2)
                    {
                        dc.send($"Pong:{parts[1]}");
                    }

                    return;

                case "B": // الأزرار (بما فيها ضغطة الـ Touchpad كزر عادي)
                    if (parts.Length >= 3)
                    {
                        _gamepadManager.OnButtonReceive(connectionId, parts[1], parts[2] == "1");
                    }

                    return;

                case "J": // عصا التحكم
                    if (parts.Length >= 4
                        && short.TryParse(parts[2], out short x)
                        && short.TryParse(parts[3], out short y))
                    {
                        _gamepadManager.OnJoystickMove(connectionId, parts[1], x, y);
                    }

                    return;
            }
        }

        /// <summary>
        /// 停服前把还活着的会话全部关掉。
        /// 不关的话：容器 Dispose 会把 GamepadManager 连同 ViGEm 设备一起拆掉，
        /// 而 SIPSorcery 的接收线程还在跑，后续输入会打在已释放的设备上。
        /// </summary>
        public void CloseAll()
        {
            foreach (string connectionId in _peerConnections.Keys.ToArray())
            {
                EndConnection(connectionId);
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