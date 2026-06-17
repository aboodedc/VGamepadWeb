using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace VGamepadWeb.Core;

public class CemuHookDSUServer : IDisposable
{
    private const int DsuDefaultPort = 26760;
    private const int SlotCount = 4;
    private const float GyroScale = 16.0f;
    private const float AccelScale = 800.0f;

    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private int _port;
    private bool _running;

    private readonly DsuSlotState[] _slots = new DsuSlotState[SlotCount];
    private readonly object _slotLock = new();

    private static readonly byte[][] SlotMacs = Enumerable.Range(0, SlotCount)
        .Select(i => new byte[] { 0x00, 0x1A, 0x7D, 0xDA, 0x71, (byte)i })
        .ToArray();

    public int Port => _port;
    public bool IsRunning => _running;

    public event Action<int, int, int>? OnLog;

    public void Start(int? port = null)
    {
        if (_running) return;
        _port = port ?? DsuDefaultPort;

        for (int i = 0; i < SlotCount; i++)
        {
            _slots[i] = DsuSlotState.Default(i);
        }

        try
        {
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, _port));
            _cts = new CancellationTokenSource();
            _running = true;
            Log(0, 0, $"[DSU] Listening on UDP port {_port}");
            _ = ReceiveLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            Log(0, 0, $"[DSU] Failed to start: {ex.Message}");
            _udp?.Close();
            _udp = null;
            _running = false;
        }
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _cts?.Cancel();
        _udp?.Close();
        _udp = null;
        _cts?.Dispose();
        _cts = null;
        Log(0, 0, "[DSU] Stopped");
    }

    public void UpdateSlotState(int slot, DsuSlotState state)
    {
        if (slot < 0 || slot >= SlotCount) return;
        lock (_slotLock)
        {
            _slots[slot] = state;
        }
    }

    public void UpdateMotion(int slot, float ax, float ay, float az, float gx, float gy, float gz)
    {
        if (slot < 0 || slot >= SlotCount) return;
        lock (_slotLock)
        {
            _slots[slot].AccelX = (short)(az * AccelScale);
            _slots[slot].AccelY = (short)(ax * AccelScale);
            _slots[slot].AccelZ = (short)(ay * AccelScale);
            _slots[slot].GyroX = (short)(gz * GyroScale);
            _slots[slot].GyroY = (short)(gx * GyroScale);
            _slots[slot].GyroZ = (short)(gy * GyroScale);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udp!.ReceiveAsync(ct);
                ProcessRequest(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                Log(0, 0, $"[DSU] Receive error: {ex.Message}");
            }
        }
    }

    private void ProcessRequest(byte[] data, IPEndPoint client)
    {
        if (data.Length < DSU.HeaderSize) return;

        if (!DSU.ReadHeader(data, out var length, out var clientId, out var packetType))
            return;

        if (packetType == DSU.ListPorts)
        {
            var response = DSU.BuildListResponse(clientId, _slots.AsSpan());
            _udp?.Send(response, response.Length, client);
            Log(0, 0, $"[DSU] ListPorts → {client}");
        }
        else if (packetType == DSU.DataRequest)
        {
            int slot = 0;
            if (data.Length >= DSU.HeaderSize + 8)
            {
                slot = data[DSU.HeaderSize + 4];
                if (slot < 0 || slot >= SlotCount) slot = 0;
            }

            DsuSlotState slotState;
            lock (_slotLock)
            {
                slotState = _slots[slot];
                if (slotState.State != 2)
                {
                    var connected = _slots.FirstOrDefault(s => s.State == 2);
                    slotState = connected.PadId != 0 || connected.State == 2 ? connected : _slots[0];
                    slot = (int)slotState.PadId;
                }
            }

            var response = DSU.BuildDataResponse(clientId, slotState);
            _udp?.Send(response, response.Length, client);
        }
    }

    public void UpdateGamepadState(int slot, uint buttons, byte ps, byte touch, byte l2, byte r2, byte lx, byte ly, byte rx, byte ry, byte dpad)
    {
        if (slot < 0 || slot >= SlotCount) return;
        lock (_slotLock)
        {
            _slots[slot].Buttons = buttons;
            _slots[slot].Ps = ps;
            _slots[slot].Touch = touch;
            _slots[slot].L2 = l2;
            _slots[slot].R2 = r2;
            _slots[slot].LX = lx;
            _slots[slot].LY = ly;
            _slots[slot].RX = rx;
            _slots[slot].RY = ry;
            _slots[slot].Dpad = dpad;
            _slots[slot].State = 2;
            _slots[slot].ConnectionType = 2;
            _slots[slot].Mac = SlotMacs[slot];
        }
    }

    private void Log(int slot, int level, string msg)
    {
        Console.WriteLine(msg);
        OnLog?.Invoke(slot, level, msg);
    }

    public void Dispose()
    {
        Stop();
    }
}
