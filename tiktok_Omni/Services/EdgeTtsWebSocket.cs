using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// WebSocket tối thiểu cho Edge TTS trên .NET Framework — ClientWebSocket không gửi được User-Agent (403 Forbidden).
    /// </summary>
    internal sealed class EdgeTtsWebSocket : IDisposable
    {
        private const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0";

        private readonly Stream _stream;
        private readonly TcpClient _tcpClient;
        private readonly Random _rnd = new Random();
        private bool _disposed;

        private EdgeTtsWebSocket(TcpClient tcpClient, Stream stream)
        {
            _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public static async Task<EdgeTtsWebSocket> ConnectAsync(
            Uri uri,
            IReadOnlyDictionary<string, string> extraHeaders,
            CancellationToken cancellationToken)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (!string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Chỉ hỗ trợ wss://.");
            }

            var host = uri.IdnHost;
            var port = uri.IsDefaultPort ? 443 : uri.Port;
            var tcp = new TcpClient();
            using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                connectCts.CancelAfter(TimeSpan.FromSeconds(45));
                await tcp.ConnectAsync(host, port).ConfigureAwait(false);
            }

            var netStream = tcp.GetStream();
            var ssl = new SslStream(netStream, false);
            await ssl.AuthenticateAsClientAsync(
                host,
                null,
                SslProtocols.Tls12,
                false).ConfigureAwait(false);

            var keyBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyBytes);
            }

            var secKey = Convert.ToBase64String(keyBytes);
            var pathAndQuery = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;

            var req = new StringBuilder();
            req.Append("GET ").Append(pathAndQuery).Append(" HTTP/1.1\r\n");
            req.Append("Host: ").Append(host).Append("\r\n");
            req.Append("Upgrade: websocket\r\n");
            req.Append("Connection: Upgrade\r\n");
            req.Append("Sec-WebSocket-Key: ").Append(secKey).Append("\r\n");
            req.Append("Sec-WebSocket-Version: 13\r\n");
            req.Append("User-Agent: ").Append(UserAgent).Append("\r\n");
            req.Append("Origin: chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold\r\n");
            req.Append("Pragma: no-cache\r\n");
            req.Append("Cache-Control: no-cache\r\n");
            req.Append("Accept-Language: en-US,en;q=0.9\r\n");
            if (extraHeaders != null)
            {
                foreach (var pair in extraHeaders)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    req.Append(pair.Key).Append(": ").Append(pair.Value ?? string.Empty).Append("\r\n");
                }
            }

            req.Append("\r\n");
            var reqBytes = Encoding.UTF8.GetBytes(req.ToString());
            await ssl.WriteAsync(reqBytes, 0, reqBytes.Length, cancellationToken).ConfigureAwait(false);
            await ssl.FlushAsync(cancellationToken).ConfigureAwait(false);

            var statusLine = await ReadLineAsync(ssl, cancellationToken).ConfigureAwait(false);
            if (statusLine == null || statusLine.IndexOf(" 101 ", StringComparison.Ordinal) < 0)
            {
                var headers = await ReadHeadersAsync(ssl, cancellationToken).ConfigureAwait(false);
                var body = await ReadBodyIfPresentAsync(ssl, headers, cancellationToken).ConfigureAwait(false);
                headers.TryGetValue("Date", out var serverDate);
                throw new InvalidOperationException(
                    "Edge TTS WebSocket handshake thất bại: "
                    + (statusLine ?? "(no status)")
                    + (string.IsNullOrEmpty(serverDate) ? string.Empty : " Date: " + serverDate)
                    + (string.IsNullOrEmpty(body) ? string.Empty : " — " + body));
            }

            await ReadHeadersAsync(ssl, cancellationToken).ConfigureAwait(false);
            return new EdgeTtsWebSocket(tcp, ssl);
        }

        public Task SendTextAsync(string text, CancellationToken cancellationToken) =>
            SendFrameAsync(WebSocketOpcode.Text, Encoding.UTF8.GetBytes(text ?? string.Empty), true, cancellationToken);

        public async Task<WebSocketReceiveResult> ReceiveAsync(CancellationToken cancellationToken)
        {
            using (var payload = new MemoryStream())
            {
                WebSocketOpcode? opcode = null;
                while (true)
                {
                    var frame = await ReadFrameAsync(cancellationToken).ConfigureAwait(false);
                    if (!opcode.HasValue)
                    {
                        opcode = frame.Opcode;
                    }

                    if (frame.Payload.Length > 0)
                    {
                        payload.Write(frame.Payload, 0, frame.Payload.Length);
                    }

                    if (frame.Fin)
                    {
                        break;
                    }
                }

                var op = opcode ?? WebSocketOpcode.Text;
                if (op == WebSocketOpcode.Close)
                {
                    return new WebSocketReceiveResult(WebSocketMessageType.Close, payload.ToArray(), true);
                }

                if (op == WebSocketOpcode.Binary)
                {
                    return new WebSocketReceiveResult(WebSocketMessageType.Binary, payload.ToArray(), true);
                }

                return new WebSocketReceiveResult(WebSocketMessageType.Text, payload.ToArray(), true);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _stream.Dispose();
            }
            catch
            {
                // ignored
            }

            try
            {
                _tcpClient?.Close();
            }
            catch
            {
                // ignored
            }
        }

        private async Task SendFrameAsync(
            WebSocketOpcode opcode,
            byte[] payload,
            bool fin,
            CancellationToken cancellationToken)
        {
            payload = payload ?? Array.Empty<byte>();
            using (var ms = new MemoryStream())
            {
                ms.WriteByte((byte)((fin ? 0x80 : 0x00) | (byte)opcode));
                var len = payload.Length;
                if (len <= 125)
                {
                    ms.WriteByte((byte)(0x80 | len));
                }
                else if (len <= 65535)
                {
                    ms.WriteByte((byte)(0x80 | 126));
                    ms.WriteByte((byte)((len >> 8) & 0xFF));
                    ms.WriteByte((byte)(len & 0xFF));
                }
                else
                {
                    ms.WriteByte((byte)(0x80 | 127));
                    for (var i = 7; i >= 0; i--)
                    {
                        ms.WriteByte((byte)((len >> (8 * i)) & 0xFF));
                    }
                }

                var mask = new byte[4];
                _rnd.NextBytes(mask);
                ms.Write(mask, 0, 4);
                for (var i = 0; i < payload.Length; i++)
                {
                    ms.WriteByte((byte)(payload[i] ^ mask[i % 4]));
                }

                var bytes = ms.ToArray();
                await _stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
                await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<Frame> ReadFrameAsync(CancellationToken cancellationToken)
        {
            var b0 = await ReadByteAsync(_stream, cancellationToken).ConfigureAwait(false);
            var b1 = await ReadByteAsync(_stream, cancellationToken).ConfigureAwait(false);
            var fin = (b0 & 0x80) != 0;
            var opcode = (WebSocketOpcode)(b0 & 0x0F);
            var masked = (b1 & 0x80) != 0;
            var len = b1 & 0x7F;
            if (len == 126)
            {
                len = (await ReadByteAsync(_stream, cancellationToken).ConfigureAwait(false) << 8)
                      | await ReadByteAsync(_stream, cancellationToken).ConfigureAwait(false);
            }
            else if (len == 127)
            {
                len = 0;
                for (var i = 0; i < 8; i++)
                {
                    len = (len << 8) | await ReadByteAsync(_stream, cancellationToken).ConfigureAwait(false);
                }
            }

            byte[] maskKey = null;
            if (masked)
            {
                maskKey = new byte[4];
                await ReadExactAsync(_stream, maskKey, cancellationToken).ConfigureAwait(false);
            }

            var payload = new byte[len];
            if (len > 0)
            {
                await ReadExactAsync(_stream, payload, cancellationToken).ConfigureAwait(false);
                if (masked && maskKey != null)
                {
                    for (var i = 0; i < payload.Length; i++)
                    {
                        payload[i] ^= maskKey[i % 4];
                    }
                }
            }

            return new Frame(fin, opcode, payload);
        }

        private static async Task<string> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
        {
            var ms = new MemoryStream();
            while (true)
            {
                var b = await ReadByteAsync(stream, cancellationToken).ConfigureAwait(false);
                if (b == '\n')
                {
                    break;
                }

                if (b != '\r')
                {
                    ms.WriteByte((byte)b);
                }
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static async Task<Dictionary<string, string>> ReadHeadersAsync(Stream stream, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                var line = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                var idx = line.IndexOf(':');
                if (idx <= 0)
                {
                    continue;
                }

                headers[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
            }

            return headers;
        }

        private static async Task<string> ReadBodyIfPresentAsync(
            Stream stream,
            Dictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            if (headers == null || !headers.TryGetValue("Content-Length", out var lenStr))
            {
                return string.Empty;
            }

            if (!int.TryParse(lenStr, out var len) || len <= 0)
            {
                return string.Empty;
            }

            var buf = new byte[len];
            await ReadExactAsync(stream, buf, cancellationToken).ConfigureAwait(false);
            return Encoding.UTF8.GetString(buf);
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken)
                    .ConfigureAwait(false);
                if (read <= 0)
                {
                    throw new EndOfStreamException();
                }

                offset += read;
            }
        }

        private static async Task<int> ReadByteAsync(Stream stream, CancellationToken cancellationToken)
        {
            var buf = new byte[1];
            var read = await stream.ReadAsync(buf, 0, 1, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                throw new EndOfStreamException();
            }

            return buf[0];
        }

        private enum WebSocketOpcode : byte
        {
            Continuation = 0,
            Text = 1,
            Binary = 2,
            Close = 8
        }

        private sealed class Frame
        {
            public Frame(bool fin, WebSocketOpcode opcode, byte[] payload)
            {
                Fin = fin;
                Opcode = opcode;
                Payload = payload ?? Array.Empty<byte>();
            }

            public bool Fin { get; }
            public WebSocketOpcode Opcode { get; }
            public byte[] Payload { get; }
        }

        internal enum WebSocketMessageType
        {
            Text,
            Binary,
            Close
        }

        internal sealed class WebSocketReceiveResult
        {
            public WebSocketReceiveResult(WebSocketMessageType messageType, byte[] data, bool endOfMessage)
            {
                MessageType = messageType;
                Data = data ?? Array.Empty<byte>();
                EndOfMessage = endOfMessage;
            }

            public WebSocketMessageType MessageType { get; }
            public byte[] Data { get; }
            public bool EndOfMessage { get; }
        }
    }
}
