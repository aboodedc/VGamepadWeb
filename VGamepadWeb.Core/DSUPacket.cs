using System.Net;
using System.Runtime.InteropServices;

namespace VGamepadWeb.Core;

internal static class DSU
{
    public const string Magic = "DSUS";
    public const ushort Version = 1;
    public const uint ListPorts = 0x10000001;
    public const uint DataRequest = 0x10000002;
    public const int HeaderSize = 20;

    public const int DataBodySize = 80;
    public const int DataPacketSize = HeaderSize + DataBodySize;

    public static readonly byte[] MagicBytes = "DSUS"u8.ToArray();

    public static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFF;
    }

    private static readonly uint[] _crcTable = (() =>
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int j = 0; j < 8; j++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
            table[i] = c;
        }
        return table;
    })();

    public static void WriteHeader(Span<byte> buf, ushort length, uint clientId, uint packetType)
    {
        MagicBytes.CopyTo(buf);
        Write16(buf[4..], Version);
        Write16(buf[6..], length);
        Write32(buf[12..], clientId);
        Write32(buf[16..], packetType);

        uint crc = Crc32(buf[8..length]);
        Write32(buf[8..], crc);
    }

    public static bool ReadHeader(ReadOnlySpan<byte> buf, out ushort length, out uint clientId, out uint packetType)
    {
        length = 0; clientId = 0; packetType = 0;
        if (buf.Length < HeaderSize) return false;
        if (!buf[..4].SequenceEqual(MagicBytes)) return false;

        length = Read16(buf[6..]);
        clientId = Read32(buf[12..]);
        packetType = Read32(buf[16..]);

        uint expectedCrc = Crc32(buf[8..length]);
        uint actualCrc = Read32(buf[8..]);
        return actualCrc == expectedCrc;
    }

    public static ushort Read16(ReadOnlySpan<byte> buf) => (ushort)(buf[0] | (buf[1] << 8));
    public static uint Read32(ReadOnlySpan<byte> buf) => (uint)(buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24));
    public static short Read16S(ReadOnlySpan<byte> buf) => (short)(buf[0] | (buf[1] << 8));

    public static void Write16(Span<byte> buf, ushort v) { buf[0] = (byte)v; buf[1] = (byte)(v >> 8); }
    public static void Write32(Span<byte> buf, uint v) { buf[0] = (byte)v; buf[1] = (byte)(v >> 8); buf[2] = (byte)(v >> 16); buf[3] = (byte)(v >> 24); }
    public static void Write16S(Span<byte> buf, short v) { buf[0] = (byte)v; buf[1] = (byte)(v >> 8); }

    public static byte[] BuildDataResponse(uint clientId, in DsuSlotState slot)
    {
        var buf = new byte[DataPacketSize];
        int off = HeaderSize;

        Write32(buf[off..], slot.Buttons); off += 4;
        buf[off] = slot.Ps; off += 1;
        buf[off] = slot.Touch; off += 1;
        off += 2;
        buf[off] = slot.L2; off += 1;
        buf[off] = slot.R2; off += 1;
        buf[off] = slot.LX; off += 1;
        buf[off] = slot.LY; off += 1;
        buf[off] = slot.RX; off += 1;
        buf[off] = slot.RY; off += 1;
        buf[off] = slot.Dpad; off += 1;
        off += 13;
        Write16S(buf[off..], slot.GyroX); off += 2;
        Write16S(buf[off..], slot.GyroY); off += 2;
        Write16S(buf[off..], slot.GyroZ); off += 2;
        Write16S(buf[off..], slot.AccelX); off += 2;
        Write16S(buf[off..], slot.AccelY); off += 2;
        Write16S(buf[off..], slot.AccelZ); off += 2;
        Write16(buf[off..], slot.Touch1X); off += 2;
        Write16(buf[off..], slot.Touch1Y); off += 2;
        Write16(buf[off..], slot.Touch2X); off += 2;
        Write16(buf[off..], slot.Touch2Y); off += 2;
        buf[off] = slot.Battery; off += 1;
        off += 11;
        Write32(buf[off..], slot.ConnectionType); off += 4;
        slot.Mac.CopyTo(buf[off..]); off += 6;
        off += 2;
        Write32(buf[off..], slot.PadId);

        WriteHeader(buf, (ushort)buf.Length, clientId, DataRequest);
        return buf;
    }

    public static byte[] BuildListResponse(uint clientId, ReadOnlySpan<DsuSlotState> slots)
    {
        int count = 4;
        int bodySize = 4 + count * 32;
        var buf = new byte[HeaderSize + bodySize];
        int off = HeaderSize;

        Write32(buf[off..], (uint)count); off += 4;

        for (int i = 0; i < count; i++)
        {
            var slot = slots[i];
            slot.Mac.CopyTo(buf[off..]); off += 6;
            Write32(buf[off..], slot.Model); off += 4;
            Write32(buf[off..], slot.ConnectionType); off += 4;
            Write32(buf[off..], (uint)i); off += 4;
            Write32(buf[off..], slot.State); off += 4;
            off += 10;
        }

        WriteHeader(buf, (ushort)buf.Length, clientId, ListPorts);
        return buf;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct DsuSlotState
{
    public uint Buttons;
    public byte Ps;
    public byte Touch;
    public byte L2;
    public byte R2;
    public byte LX;
    public byte LY;
    public byte RX;
    public byte RY;
    public byte Dpad;
    public short GyroX;
    public short GyroY;
    public short GyroZ;
    public short AccelX;
    public short AccelY;
    public short AccelZ;
    public ushort Touch1X;
    public ushort Touch1Y;
    public ushort Touch2X;
    public ushort Touch2Y;
    public byte Battery;
    public uint ConnectionType;
    public byte[] Mac;
    public uint PadId;
    public uint Model;
    public uint State;

    public static DsuSlotState Default(int slot)
    {
        var mac = new byte[6];
        mac[5] = (byte)slot;
        return new DsuSlotState
        {
            Mac = mac,
            Model = 1,
            ConnectionType = 0,
            PadId = (uint)slot,
            State = 0,
            Battery = 255,
            Dpad = 8,
        };
    }

    public static DsuSlotState Connected(int slot, byte[] mac)
    {
        var state = Default(slot);
        state.Mac = mac;
        state.ConnectionType = 2;
        state.State = 2;
        return state;
    }
}
