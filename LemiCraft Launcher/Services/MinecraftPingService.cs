using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace LemiCraft_Launcher.Services
{
    public class ServerStatus
    {
        public bool Success { get; set; }
        public int OnlinePlayers { get; set; }
        public int MaxPlayers { get; set; }
        public string? Motd { get; set; }
        public string? Version { get; set; }
        public int LatencyMs { get; set; }
    }

    public static class MineStatClient
    {
        public static async Task<ServerStatus?> PingAsync(string host, int port = 25565, int timeoutMs = 5000)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, port).WaitAsync(cts.Token);

                using var stream = client.GetStream();
                stream.ReadTimeout = timeoutMs;
                stream.WriteTimeout = timeoutMs;

                var handshakePayload = new List<byte>();
                handshakePayload.AddRange(WriteVarInt(0));
                handshakePayload.AddRange(WriteVarInt(754));
                var hostBytes = Encoding.UTF8.GetBytes(host);
                handshakePayload.AddRange(WriteVarInt(hostBytes.Length));
                handshakePayload.AddRange(hostBytes);
                handshakePayload.Add((byte)(port >> 8));
                handshakePayload.Add((byte)(port & 0xFF));
                handshakePayload.AddRange(WriteVarInt(1));

                await WritePacketAsync(stream, handshakePayload.ToArray(), cts.Token);
                await WritePacketAsync(stream, new byte[] { 0x00 }, cts.Token);

                var sw = Stopwatch.StartNew();

                int packetLength = await ReadVarIntAsync(stream, cts.Token);
                if (packetLength <= 0) return null;

                int packetId = await ReadVarIntAsync(stream, cts.Token);

                int jsonLen = await ReadVarIntAsync(stream, cts.Token);
                if (jsonLen <= 0) return null;

                var jsonBytes = await ReadExactlyAsync(stream, jsonLen, cts.Token);
                sw.Stop();
                var json = Encoding.UTF8.GetString(jsonBytes);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var result = new ServerStatus { Success = true, LatencyMs = (int)sw.ElapsedMilliseconds };

                if (root.TryGetProperty("players", out var players))
                {
                    if (players.TryGetProperty("online", out var on)) result.OnlinePlayers = on.GetInt32();
                    if (players.TryGetProperty("max", out var mx)) result.MaxPlayers = mx.GetInt32();
                }

                if (root.TryGetProperty("description", out var desc))
                    result.Motd = desc.ValueKind == JsonValueKind.String ? desc.GetString() : desc.ToString();

                if (root.TryGetProperty("version", out var ver))
                {
                    if (ver.TryGetProperty("name", out var name)) result.Version = name.GetString();
                    else result.Version = ver.ToString();
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        private static byte[] WriteVarInt(int value)
        {
            var buffer = new List<byte>();
            uint v = (uint)value;
            while (true)
            {
                if ((v & ~0x7Fu) == 0)
                {
                    buffer.Add((byte)v);
                    break;
                }

                buffer.Add((byte)((v & 0x7Fu) | 0x80u));
                v >>= 7;
            }
            return buffer.ToArray();
        }

        private static async Task WritePacketAsync(NetworkStream stream, byte[] data, CancellationToken ct)
        {
            var lengthBytes = WriteVarInt(data.Length);
            await stream.WriteAsync(lengthBytes, ct);
            await stream.WriteAsync(data, ct);
            await stream.FlushAsync(ct);
        }

        private static async Task<int> ReadVarIntAsync(NetworkStream stream, CancellationToken ct)
        {
            int numRead = 0;
            int result = 0;
            while (true)
            {
                int read = await stream.ReadByteAsync(ct);
                if (read == -1) throw new EndOfStreamException();
                byte b = (byte)read;
                int value = b & 0x7F;
                result |= (value << (7 * numRead));
                numRead++;
                if (numRead > 5) throw new FormatException("VarInt too big");
                if ((b & 0x80) == 0) break;
            }
            return result;
        }

        private static async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int count, CancellationToken ct)
        {
            var buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer, offset, count - offset, ct);
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
            return buffer;
        }

        private static async Task<int> ReadByteAsync(this NetworkStream stream, CancellationToken ct)
        {
            var buf = new byte[1];
            var t = stream.ReadAsync(buf, 0, 1, ct);
            var read = await t;
            if (read == 0) return -1;
            return buf[0];
        }
    }
}
