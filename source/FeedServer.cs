using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HudWall
{
    /// <summary>
    /// Minimal localhost JSON feed for wallpaper hosts that cannot read system
    /// counters themselves (Wallpaper Engine web wallpapers, Lively).
    /// Plain TcpListener so no URL ACL / admin reservation is needed.
    /// </summary>
    internal sealed class FeedServer : IDisposable
    {
        private readonly StatsCollector _stats;
        private readonly AudioCapture _audio;
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private int _port;

        public FeedServer(int port)
        {
            _port = port;
            _stats = new StatsCollector();
            _audio = new AudioCapture();
            _listener = new TcpListener(IPAddress.Loopback, port);
        }

        public int Port => _port;

        public void Start()
        {
            try
            {
                _listener.Start(64);
            }
            catch (SocketException)
            {
                // port busy: try a couple of neighbours, the page reads the real one from a file
                for (int p = _port + 1; p <= _port + 5; p++)
                {
                    try
                    {
                        var l = new TcpListener(IPAddress.Loopback, p);
                        l.Start(64);
                        _port = p;
                        StartAcceptLoop(l);
                        Program.Log("feed listening on " + _port + " (fallback)");
                        return;
                    }
                    catch (SocketException) { }
                }
                throw;
            }
            StartAcceptLoop(_listener);
            Program.Log("feed listening on 127.0.0.1:" + _port);
        }

        private void StartAcceptLoop(TcpListener listener)
        {
            Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    TcpClient client;
                    try { client = await listener.AcceptTcpClientAsync(_cts.Token); }
                    catch (OperationCanceledException) { break; }
                    catch (ObjectDisposedException) { break; }
                    catch (Exception ex) { Program.Log("accept: " + ex.Message); continue; }

                    _ = Task.Run(() => Handle(client));
                }
            });
        }

        private void Handle(TcpClient client)
        {
            try
            {
                client.NoDelay = true;
                using var stream = client.GetStream();
                stream.ReadTimeout = 2000;
                stream.WriteTimeout = 2000;

                string requestLine = ReadRequestLine(stream);
                if (string.IsNullOrEmpty(requestLine)) return;

                // drain headers (bounded) so the client does not see an RST
                DrainHeaders(stream);

                string path = "/";
                var parts = requestLine.Split(' ');
                if (parts.Length >= 2) path = parts[1];

                string body;
                int code = 200;
                if (path.StartsWith("/stats", StringComparison.OrdinalIgnoreCase))
                {
                    body = _stats.SnapshotJson();
                }
                else if (path.StartsWith("/audio", StringComparison.OrdinalIgnoreCase))
                {
                    body = _audio.SnapshotJson();
                }
                else if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
                {
                    body = "{\"ok\":true,\"port\":" + _port + "}";
                }
                else
                {
                    code = 404;
                    body = "{\"ok\":false,\"error\":\"not found\"}";
                }

                byte[] payload = Encoding.UTF8.GetBytes(body);
                var head = new StringBuilder()
                    .Append("HTTP/1.1 ").Append(code == 200 ? "200 OK" : "404 Not Found").Append("\r\n")
                    .Append("Content-Type: application/json; charset=utf-8\r\n")
                    .Append("Content-Length: ").Append(payload.Length).Append("\r\n")
                    .Append("Cache-Control: no-store\r\n")
                    // Wallpaper Engine loads the page from file://, whose Origin is "null"
                    .Append("Access-Control-Allow-Origin: *\r\n")
                    .Append("Access-Control-Allow-Headers: *\r\n")
                    .Append("Connection: close\r\n\r\n")
                    .ToString();

                byte[] header = Encoding.ASCII.GetBytes(head);
                stream.Write(header, 0, header.Length);
                stream.Write(payload, 0, payload.Length);
                stream.Flush();
            }
            catch { /* one bad client must never take the feed down */ }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        private static string ReadRequestLine(Stream s)
        {
            var buf = new StringBuilder();
            for (int i = 0; i < 2048; i++)
            {
                int b = s.ReadByte();
                if (b < 0) return null;
                if (b == '\n')
                {
                    string line = buf.ToString().TrimEnd('\r');
                    return line.Length > 0 ? line : null;
                }
                if (b != '\r') buf.Append((char)b);
            }
            return null;
        }

        private static void DrainHeaders(Stream s)
        {
            int seen = 0, run = 0;
            while (seen < 8192)
            {
                int b = s.ReadByte();
                if (b < 0) return;
                seen++;
                if (b == '\n')
                {
                    if (run == 1) return; // CRLFCRLF terminator
                    run = 1;
                }
                else if (b != '\r') run = 0;
            }
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }
            try { _audio?.Dispose(); } catch { }
            try { _stats?.Dispose(); } catch { }
        }
    }
}
