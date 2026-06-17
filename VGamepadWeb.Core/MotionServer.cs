using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace VGamepadWeb.Core
{
    public class MotionServer : IDisposable
    {
        private UdpClient _server;
        private readonly ConcurrentDictionary<IPEndPoint, DateTime> _subscribers = new();
        private uint _packetCount = 0;
        private readonly object _packetCountLock = new object();
        private readonly byte[] _macAddress = { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };
        private System.Threading.Timer _heartbeatTimer;
        private readonly GamepadManager _gamepadManager;

        private static readonly uint[] CrcTable = InitializeCrcTable();
        private const float GRAVITY = 1.0f;

        public MotionServer(GamepadManager gamepadManager)
        {
            _gamepadManager = gamepadManager;
            try
            {
                _server = new UdpClient(new IPEndPoint(IPAddress.Any, 26760));

                // Disable WSAECONNRESET on Windows to avoid SocketException when sending/receiving UDP
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    const int SIO_UDP_CONNRESET = -1744830452;
                    try
                    {
                        _server.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MotionServer] Warning: Could not disable UDP connection reset: {ex.Message}");
                    }
                }

                StartListening();

                // 10ms (100Hz) heartbeat to satisfy the emulator's continuous stream requirement
                _heartbeatTimer = new System.Threading.Timer(SendHeartbeat, null, 10, 10);

                Console.WriteLine("[MotionServer] DSU Protocol is listening securely on port 26760...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MotionServer] Error starting on port 26760: {ex.Message}");
            }
        }

        private void StartListening()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var result = await _server.ReceiveAsync();
                        byte[] data = result.Buffer;

                        if (data != null && data.Length >= 20 &&
                            data[0] == 'D' && data[1] == 'S' && data[2] == 'U' && (data[3] == 'S' || data[3] == 'C'))
                        {
                            uint messageType = BitConverter.ToUInt32(data, 16);

                            // 0x100000: Request Version
                            if (messageType == 0x100000)
                            {
                                ReplyWithVersion(result.RemoteEndPoint);
                            }
                            // 0x100001: Request Controller Info
                            else if (messageType == 0x100001)
                            {
                                ReplyWithControllerInfo(result.RemoteEndPoint, data);
                            }
                            // 0x100002: Request Data Subscription
                            else if (messageType == 0x100002)
                            {
                                // Add or update subscription time
                                _subscribers[result.RemoteEndPoint] = DateTime.UtcNow;

                                // Immediately feed it an initial motion packet for any active slots
                                for (int slot = 0; slot < 256; slot++)
                                {
                                    if (_gamepadManager.IsGyroActiveForSlot(slot) || slot == 0)
                                    {
                                        byte[] packet = BuildCemuHookPacket(slot, 0f, 0f, 0f, 0f, GRAVITY, 0f);
                                        try { _server.Send(packet, packet.Length, result.RemoteEndPoint); } catch { }
                                    }
                                }
                            }
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (_server == null || _server.Client == null || !_server.Client.IsBound)
                        {
                            break;
                        }
                        Console.WriteLine($"[MotionServer] StartListening packet error: {ex.Message}");
                    }
                }
            });
        }

        private void PruneExpiredSubscribers()
        {
            var now = DateTime.UtcNow;
            foreach (var sub in _subscribers.Keys)
            {
                if ((now - _subscribers[sub]).TotalSeconds > 5.0)
                {
                    _subscribers.TryRemove(sub, out _);
                }
            }
        }

        private void SendHeartbeat(object state)
        {
            PruneExpiredSubscribers();

            if (_subscribers.IsEmpty) return;

            foreach (var sub in _subscribers.Keys)
            {
                ReplyWithControllerInfo(sub);

                // Send default motion packets to keep connection alive in emulator
                for (int slot = 0; slot < 256; slot++)
                {
                    bool reportAsConnected = (slot == 0) || _gamepadManager.IsGyroActiveForSlot(slot);
                    bool isCurrentlyActive = _gamepadManager.IsGyroActiveForSlot(slot);

                    if (reportAsConnected && !isCurrentlyActive)
                    {
                        byte[] packet = BuildCemuHookPacket(slot, 0f, 0f, 0f, 0f, GRAVITY, 0f);
                        try
                        {
                            _server.Send(packet, packet.Length, sub);
                        }
                        catch
                        {
                            // Ignore
                        }
                    }
                }
            }
        }

        private void ReplyWithVersion(IPEndPoint targetEndPoint)
        {
            if (targetEndPoint == null) return;

            byte[] response = new byte[22];

            response[0] = (byte)'D'; response[1] = (byte)'S';
            response[2] = (byte)'U'; response[3] = (byte)'S';
            response[4] = 0xE9; response[5] = 0x03; // Version 1001

            ushort dataLength = (ushort)(response.Length - 16);
            BitConverter.GetBytes(dataLength).CopyTo(response, 6);

            // Server ID (0x12345678)
            BitConverter.GetBytes(0x12345678).CopyTo(response, 12);

            // Message Type (0x100000) - Version Request response
            response[16] = 0x00; response[17] = 0x00;
            response[18] = 0x10; response[19] = 0x00;

            // Payload: Max version supported (1001) -> 2 bytes
            response[20] = 0xE9; response[21] = 0x03;

            // CRC32 Calculation
            uint crc = ComputeCrc32(response, response.Length);
            response[8] = (byte)(crc & 0xFF);
            response[9] = (byte)((crc >> 8) & 0xFF);
            response[10] = (byte)((crc >> 16) & 0xFF);
            response[11] = (byte)((crc >> 24) & 0xFF);

            try
            {
                _server.Send(response, response.Length, targetEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MotionServer] Error sending version reply: {ex.Message}");
            }
        }

        private void ReplyWithControllerInfo(IPEndPoint targetEndPoint, byte[]? requestData = null)
        {
            if (targetEndPoint == null) return;

            int portCount = 0;
            if (requestData != null && requestData.Length >= 24)
            {
                portCount = BitConverter.ToInt32(requestData, 20);
            }

            // If portCount is 0 or invalid, we default to sending info for slot 0
            if (portCount <= 0 || portCount > 4 || requestData == null || requestData.Length < 24 + portCount)
            {
                SendSingleControllerInfo(targetEndPoint, 0);
                return;
            }

            for (int i = 0; i < portCount; i++)
            {
                byte slot = requestData[24 + i];
                SendSingleControllerInfo(targetEndPoint, slot);
            }
        }

        private void SendSingleControllerInfo(IPEndPoint targetEndPoint, byte slot)
        {
            byte[] response = new byte[32];

            response[0] = (byte)'D'; response[1] = (byte)'S';
            response[2] = (byte)'U'; response[3] = (byte)'S';
            response[4] = 0xE9; response[5] = 0x03;

            ushort dataLength = (ushort)(response.Length - 16);
            BitConverter.GetBytes(dataLength).CopyTo(response, 6);

            // Server ID (0x12345678)
            BitConverter.GetBytes(0x12345678).CopyTo(response, 12);

            // Message Type (0x100001) - Controller Info
            response[16] = 0x01; response[17] = 0x00;
            response[18] = 0x10; response[19] = 0x00;

            response[20] = slot;

            bool isSlotActive = _gamepadManager.IsGyroActiveForSlot(slot);
            bool shouldReportConnected = isSlotActive || (slot == 0);

            if (shouldReportConnected)
            {
                response[21] = 0x02; // State: Connected
                response[22] = 0x02; // Model: Full Gyro
                response[23] = 0x02; // Connection Type: Bluetooth

                // Make MAC address unique per slot!
                byte[] mac = new byte[6];
                Array.Copy(_macAddress, mac, 6);
                mac[5] = (byte)((mac[5] + slot) & 0xFF);
                Array.Copy(mac, 0, response, 24, 6);

                response[30] = 0x05; // Battery: Full
                response[31] = 0x01; // Mark the controller as Active
            }
            else
            {
                response[21] = 0x00; // State: Disconnected
                response[22] = 0x00; // Model: N/A
                response[23] = 0x00; // Connection Type: N/A
                response[30] = 0x00; // Battery: N/A
                response[31] = 0x00; // Inactive
            }

            // تصفير الخانة لضمان الحساب النظيف
            response[8] = 0; response[9] = 0; response[10] = 0; response[11] = 0;

            // الحساب والتقسيم اليدوي المضمون
            uint crc = ComputeCrc32(response, response.Length);
            response[8] = (byte)(crc & 0xFF);
            response[9] = (byte)((crc >> 8) & 0xFF);
            response[10] = (byte)((crc >> 16) & 0xFF);
            response[11] = (byte)((crc >> 24) & 0xFF);

            try
            {
                _server.Send(response, response.Length, targetEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MotionServer] Error sending info reply for slot {slot}: {ex.Message}");
            }
        }
        public void UpdateMotion(int slotIndex, float gx, float gy, float gz, float ax, float ay, float az)
        {
            if (slotIndex < 0 || slotIndex > 255) return;

            if (!_gamepadManager.IsGyroActiveForSlot(slotIndex)) return;

            PruneExpiredSubscribers();

            if (_subscribers.IsEmpty) return;

            byte[] packet = BuildCemuHookPacket(slotIndex, gx, gy, gz, ax, ay, az);

            foreach (var sub in _subscribers.Keys)
            {
                try
                {
                    _server.Send(packet, packet.Length, sub);
                }
                catch
                {
                    // Ignore transient network errors
                }
            }
        }

        private byte[] BuildCemuHookPacket(int slot, float gx, float gy, float gz, float ax, float ay, float az)
        {
            byte[] packet = new byte[100]; // Standard DSU packet size is 100 bytes (20 bytes header + 80 bytes payload)

            // Header magic: DSUS
            packet[0] = (byte)'D'; packet[1] = (byte)'S';
            packet[2] = (byte)'U'; packet[3] = (byte)'S';
            packet[4] = 0xE9; packet[5] = 0x03; // Version 1001

            // Payload length (excluding the 16-byte header): 84 bytes (from offset 16 to 99)
            ushort dataLength = (ushort)(packet.Length - 16);
            BitConverter.GetBytes(dataLength).CopyTo(packet, 6);

            // Server ID (0x12345678)
            BitConverter.GetBytes(0x12345678).CopyTo(packet, 12);

            // Message Type (0x100002) - Controller Data
            packet[16] = 0x02; packet[17] = 0x00;
            packet[18] = 0x10; packet[19] = 0x00;

            // Shared beginning of payload
            packet[20] = (byte)slot;
            packet[21] = 0x02; // State: Connected
            packet[22] = 0x02; // Model: Full Gyro
            packet[23] = 0x02; // Connection Type: Bluetooth

            // Make MAC address unique per slot!
            byte[] mac = new byte[6];
            Array.Copy(_macAddress, mac, 6);
            mac[5] = (byte)((mac[5] + slot) & 0xFF);
            Array.Copy(mac, 0, packet, 24, 6);
            packet[30] = 0x05; // Battery: Full
            packet[31] = 0x01; // is_active: Connected

            // Packet Count (offset 32)
            uint count;
            lock (_packetCountLock) { count = _packetCount++; }
            BitConverter.GetBytes(count).CopyTo(packet, 32);

            // Digital Buttons (offset 36 - 39)
            packet[36] = 0x00;
            packet[37] = 0x00;
            packet[38] = 0x00; // PS Button
            packet[39] = 0x00; // Touch Button

            // Sticks: Left X, Left Y, Right X, Right Y (offset 40 - 43, center is 128)
            packet[40] = 128;
            packet[41] = 128;
            packet[42] = 128;
            packet[43] = 128;

            // Analog buttons (offset 44 - 55) - 12 bytes
            for (int i = 44; i <= 55; i++) packet[i] = 0x00;

            // Touch pad states (offset 56 - 67) - 12 bytes
            for (int i = 56; i <= 67; i++) packet[i] = 0x00;

            // Timestamp: uint64 (8 bytes, offset 68)
            ulong timestampUs = (ulong)(DateTime.UtcNow.Ticks / 10);
            BitConverter.GetBytes(timestampUs).CopyTo(packet, 68);

            // Accelerometer (offset 76)
            BitConverter.GetBytes(ax).CopyTo(packet, 76);
            BitConverter.GetBytes(ay).CopyTo(packet, 80);
            BitConverter.GetBytes(az).CopyTo(packet, 84);

            // Gyroscope (offset 88)
            BitConverter.GetBytes(gx).CopyTo(packet, 88);
            BitConverter.GetBytes(gy).CopyTo(packet, 92);
            BitConverter.GetBytes(gz).CopyTo(packet, 96);

            // Clear CRC32 slot before calculation
            packet[8] = 0; packet[9] = 0; packet[10] = 0; packet[11] = 0;

            // Calculate CRC32 of full packet
            uint crc = ComputeCrc32(packet, packet.Length);

            // write CRC in little-endian format (DSU standard)
            packet[8] = (byte)(crc & 0xFF);
            packet[9] = (byte)((crc >> 8) & 0xFF);
            packet[10] = (byte)((crc >> 16) & 0xFF);
            packet[11] = (byte)((crc >> 24) & 0xFF);

            return packet;
        }

        private static uint[] InitializeCrcTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 8; j > 0; j--)
                {
                    if ((crc & 1) == 1) crc = (crc >> 1) ^ 0xEDB88320;
                    else crc >>= 1;
                }
                table[i] = crc;
            }
            return table;
        }

        private uint ComputeCrc32(byte[] data, int length)
        {
            // إنشاء نسخة حقيقية وتصفير الخانات 8-11 لضمان سلامة العملية الحسابية للـ Checksum
            byte[] buf = new byte[length];
            Array.Copy(data, buf, length);
            buf[8] = 0; buf[9] = 0; buf[10] = 0; buf[11] = 0;

            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < length; i++)
            {
                byte index = (byte)((crc & 0xFF) ^ buf[i]);
                crc = (crc >> 8) ^ CrcTable[index];
            }
            return ~crc;
        }

        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
            _server?.Close();
            _server?.Dispose();
        }
    }
}