using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>Microsoft Edge Read Aloud TTS (vi-VN neural), miễn phí qua WebSocket.</summary>
    public sealed class EdgeTtsService
    {
        private const int MaxChunkChars = 240;
        private const int ConnectAttempts = 3;

        static EdgeTtsService()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch
            {
                // ignored
            }
        }

        public Task SynthesizeVietnameseFemaleToMp3Async(
            string text,
            string outputMp3Path,
            Action<string> log,
            CancellationToken cancellationToken = default)
        {
            return SynthesizeToMp3FileAsync(
                text,
                ShowcaseEdgeTtsVoiceResolver.ResolveFemaleSouthYoung(),
                outputMp3Path,
                log,
                cancellationToken);
        }

        public async Task SynthesizeToMp3FileAsync(
            string text,
            EdgeTtsSynthesisOptions synthesis,
            string outputMp3Path,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                throw new ArgumentException("Nội dung TTS trống.", nameof(text));
            }

            synthesis = synthesis ?? new EdgeTtsSynthesisOptions();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputMp3Path)) ?? ".");

            var temp = await SynthesizeToTempMp3Async(line, synthesis, log, cancellationToken).ConfigureAwait(false);
            try
            {
                if (File.Exists(outputMp3Path))
                {
                    File.Delete(outputMp3Path);
                }

                File.Move(temp, outputMp3Path);
            }
            catch
            {
                File.Copy(temp, outputMp3Path, true);
            }
        }

        public async Task<string> SynthesizeLongTextToTempMp3Async(
            string text,
            EdgeTtsSynthesisOptions synthesis,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var line = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(line))
            {
                throw new ArgumentException("Nội dung TTS trống.", nameof(text));
            }

            var chunks = SplitForEdge(line);
            if (chunks.Count <= 1)
            {
                return await SynthesizeToTempMp3Async(line, synthesis, log, cancellationToken).ConfigureAwait(false);
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "tiktok_Omni_edge");
            Directory.CreateDirectory(tempDir);
            var parts = new List<string>();
            for (var i = 0; i < chunks.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var part = Path.Combine(tempDir, "edge_part_" + (i + 1).ToString("D3", CultureInfo.InvariantCulture) + ".mp3");
                await SynthesizeToMp3FileAsync(chunks[i], synthesis, part, log, cancellationToken).ConfigureAwait(false);
                parts.Add(part);
            }

            var merged = Path.Combine(tempDir, "edge_merged_" + Guid.NewGuid().ToString("N") + ".mp3");
            await ConcatMp3PartsAsync(parts, merged, settings, log, cancellationToken).ConfigureAwait(false);
            foreach (var p in parts)
            {
                TryDelete(p);
            }

            return merged;
        }

        public async Task<string> SynthesizeToTempMp3Async(
            string text,
            EdgeTtsSynthesisOptions synthesis,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            synthesis = synthesis ?? new EdgeTtsSynthesisOptions();
            var voice = (synthesis.VoiceShortName ?? EdgeTtsVoices.HoaiMyNeural).Trim();
            log?.Invoke("[TTS] Edge: " + voice + " rate=" + (synthesis.Rate ?? "+0%") + "…");

            Exception lastError = null;
            for (var attempt = 1; attempt <= ConnectAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var audio = await DownloadAudioAsync(text, synthesis, cancellationToken).ConfigureAwait(false);
                    if (audio == null || audio.Length < 128)
                    {
                        throw new InvalidOperationException("Edge TTS không trả audio hợp lệ.");
                    }

                    var tempDir = Path.Combine(Path.GetTempPath(), "tiktok_Omni_edge");
                    Directory.CreateDirectory(tempDir);
                    var mp3 = Path.Combine(tempDir, "edge_" + Guid.NewGuid().ToString("N") + ".mp3");
                    File.WriteAllBytes(mp3, audio);
                    return mp3;
                }
                catch (Exception ex) when (attempt < ConnectAttempts && IsTransientConnectError(ex))
                {
                    lastError = ex;
                    log?.Invoke("[TTS] Edge: lỗi mạng (lần " + attempt + "/" + ConnectAttempts + ") — thử lại…");
                    await Task.Delay(800 * attempt, cancellationToken).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException(
                "Edge TTS không kết nối được speech.platform.bing.com. Kiểm tra mạng/VPN/firewall hoặc thử ElevenLabs.",
                lastError);
        }

        private static bool IsTransientConnectError(Exception ex)
        {
            for (var cur = ex; cur != null; cur = cur.InnerException)
            {
                var msg = cur.Message ?? string.Empty;
                if (cur is InvalidOperationException && msg.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (msg.IndexOf("Forbidden", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (msg.IndexOf("Unable to connect", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("remote server", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task<byte[]> DownloadAudioAsync(
            string text,
            EdgeTtsSynthesisOptions synthesis,
            CancellationToken cancellationToken)
        {
            text = RemoveIncompatibleCharacters(text);
            for (var connectTry = 0; connectTry < 2; connectTry++)
            {
                try
                {
                    return await DownloadAudioOnceAsync(text, synthesis, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (connectTry == 0 && IsHandshake403(ex))
                {
                    EdgeTtsDrm.AdjustClockSkewFromServerDate(TryGetServerDateFromException(ex));
                }
            }

            throw new InvalidOperationException("Edge TTS: handshake 403 sau khi chỉnh lệch giờ.");
        }

        private static bool IsHandshake403(Exception ex)
        {
            for (var cur = ex; cur != null; cur = cur.InnerException)
            {
                var msg = cur.Message ?? string.Empty;
                if (msg.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("Forbidden", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string TryGetServerDateFromException(Exception ex)
        {
            for (var cur = ex; cur != null; cur = cur.InnerException)
            {
                var msg = cur.Message ?? string.Empty;
                var idx = msg.IndexOf("Date:", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    return msg.Substring(idx + 5).Trim();
                }
            }

            return null;
        }

        private static async Task<byte[]> DownloadAudioOnceAsync(
            string text,
            EdgeTtsSynthesisOptions synthesis,
            CancellationToken cancellationToken)
        {
            var connectId = Guid.NewGuid().ToString("N");
            var gec = EdgeTtsDrm.GenerateSecMsGec();
            var url = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1"
                      + "?TrustedClientToken=" + EdgeTtsDrm.TrustedClientToken
                      + "&ConnectionId=" + connectId
                      + "&Sec-MS-GEC=" + gec
                      + "&Sec-MS-GEC-Version=" + EdgeTtsDrm.SecMsGecVersion;

            var headers = new Dictionary<string, string>
            {
                ["Cookie"] = EdgeTtsDrm.GenerateMuidCookieHeader()
            };

            using (var ws = await EdgeTtsWebSocket.ConnectAsync(new Uri(url), headers, cancellationToken)
                       .ConfigureAwait(false))
            {
                var timestamp = EdgeJsTimestamp();
                var configJson =
                    "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"},"
                    + "\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}";
                var configMsg = "X-Timestamp:" + timestamp + "\r\n"
                                + "Content-Type:application/json; charset=utf-8\r\n"
                                + "Path:speech.config\r\n\r\n"
                                + configJson + "\r\n";
                await SendTextFrameAsync(ws, configMsg, cancellationToken).ConfigureAwait(false);

                var ssmlRequestId = Guid.NewGuid().ToString("N");
                var ssml = BuildSsml(text, synthesis);
                var ssmlMsg = "X-RequestId:" + ssmlRequestId + "\r\n"
                              + "Content-Type:application/ssml+xml\r\n"
                              + "X-Timestamp:" + timestamp + "Z\r\n"
                              + "Path:ssml\r\n\r\n"
                              + ssml;
                await SendTextFrameAsync(ws, ssmlMsg, cancellationToken).ConfigureAwait(false);

                var buffer = new MemoryStream();
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await ws.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == EdgeTtsWebSocket.WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (result.Data.Length == 0)
                    {
                        continue;
                    }

                    if (result.MessageType == EdgeTtsWebSocket.WebSocketMessageType.Binary)
                    {
                        if (TryExtractBinaryAudio(result.Data, result.Data.Length, out var audioChunk)
                            && audioChunk.Length > 0)
                        {
                            buffer.Write(audioChunk, 0, audioChunk.Length);
                        }

                        continue;
                    }

                    var textChunk = Encoding.UTF8.GetString(result.Data);
                    if (textChunk.IndexOf("Path:turn.end", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        break;
                    }
                }

                return buffer.ToArray();
            }
        }

        private static string EdgeJsTimestamp() =>
            DateTime.UtcNow.ToString(
                "ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'",
                CultureInfo.InvariantCulture);

        private static string RemoveIncompatibleCharacters(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }

            var chars = raw.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var code = chars[i];
                if ((code >= 0 && code <= 8) || (code >= 11 && code <= 12) || (code >= 14 && code <= 31))
                {
                    chars[i] = ' ';
                }
            }

            return new string(chars);
        }

        private static bool TryExtractBinaryAudio(byte[] data, int length, out byte[] audio)
        {
            audio = Array.Empty<byte>();
            if (data == null || length < 4)
            {
                return false;
            }

            var headerLengthField = (data[0] << 8) | data[1];
            if (headerLengthField < 4 || headerLengthField + 2 > length)
            {
                return false;
            }

            var headerTextLen = headerLengthField - 2;
            var header = Encoding.UTF8.GetString(data, 2, headerTextLen);
            if (header.IndexOf("Path:audio", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            var bodyStart = headerLengthField + 2;
            if (bodyStart >= length)
            {
                return false;
            }

            var bodyLen = length - bodyStart;
            audio = new byte[bodyLen];
            Buffer.BlockCopy(data, bodyStart, audio, 0, bodyLen);
            return true;
        }

        private static string BuildSsml(string text, EdgeTtsSynthesisOptions synthesis)
        {
            var voice = XmlEscape((synthesis.VoiceShortName ?? EdgeTtsVoices.HoaiMyNeural).Trim());
            var rate = XmlEscape(string.IsNullOrWhiteSpace(synthesis.Rate) ? "+0%" : synthesis.Rate.Trim());
            var pitch = XmlEscape(string.IsNullOrWhiteSpace(synthesis.Pitch) ? "+0Hz" : synthesis.Pitch.Trim());
            var volume = XmlEscape(string.IsNullOrWhiteSpace(synthesis.Volume) ? "+0%" : synthesis.Volume.Trim());
            var inner = XmlEscape(text ?? string.Empty);
            return "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='vi-VN'>"
                   + "<voice name='" + voice + "'>"
                   + "<prosody pitch='" + pitch + "' rate='" + rate + "' volume='" + volume + "'>"
                   + inner
                   + "</prosody></voice></speak>";
        }

        private static string XmlEscape(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private static Task SendTextFrameAsync(
            EdgeTtsWebSocket ws,
            string message,
            CancellationToken cancellationToken) =>
            ws.SendTextAsync(message, cancellationToken);

        private static List<string> SplitForEdge(string text)
        {
            var parts = Regex.Split(text.Trim(), @"(?<=[\.\!\?…])\s+")
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => x.Length > 0)
                .ToList();
            if (parts.Count == 0)
            {
                return new List<string> { text.Trim() };
            }

            var merged = new List<string>();
            var buf = new StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length > MaxChunkChars)
                {
                    FlushBuf();
                    merged.AddRange(SplitHard(part, MaxChunkChars));
                    continue;
                }

                if (buf.Length > 0 && buf.Length + 1 + part.Length > MaxChunkChars)
                {
                    FlushBuf();
                }

                if (buf.Length > 0)
                {
                    buf.Append(' ');
                }

                buf.Append(part);
            }

            FlushBuf();
            return merged.Count > 0 ? merged : new List<string> { text.Trim() };

            void FlushBuf()
            {
                if (buf.Length > 0)
                {
                    merged.Add(buf.ToString());
                    buf.Clear();
                }
            }
        }

        private static IEnumerable<string> SplitHard(string text, int maxLen)
        {
            for (var i = 0; i < text.Length; i += maxLen)
            {
                var len = Math.Min(maxLen, text.Length - i);
                yield return text.Substring(i, len);
            }
        }

        private static async Task ConcatMp3PartsAsync(
            IList<string> parts,
            string outputMp3,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (parts == null || parts.Count == 0)
            {
                throw new InvalidOperationException("Không có đoạn audio để ghép.");
            }

            if (parts.Count == 1)
            {
                File.Copy(parts[0], outputMp3, true);
                return;
            }

            var ffmpeg = ResolveFfmpeg(settings);
            var listPath = Path.Combine(Path.GetTempPath(), "edge_concat_" + Guid.NewGuid().ToString("N") + ".txt");
            var sb = new StringBuilder();
            foreach (var seg in parts)
            {
                sb.AppendLine("file '" + seg.Replace("'", "'\\''") + "'");
            }

            File.WriteAllText(listPath, sb.ToString(), TextFileEncoding.Utf8NoBom);
            var args = "-y -f concat -safe 0 -i \"" + listPath + "\" -c:a libmp3lame -q:a 4 \"" + outputMp3 + "\"";
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken, 180).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    var err = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    log?.Invoke("[TTS] FFmpeg ghép Edge: " + err);
                    throw new InvalidOperationException("FFmpeg ghép MP3 Edge thất bại.");
                }
            }

            TryDelete(listPath);
        }

        private static string ResolveFfmpeg(AppSettings settings)
        {
            if (FfmpegToolkitService.TryResolve(settings, out var toolkit, out _))
            {
                return toolkit.FfmpegExe;
            }

            var bundled = FfmpegToolkitService.GetBundledFfmpegPath();
            if (File.Exists(bundled))
            {
                return bundled;
            }

            var p = (settings?.FfmpegPath ?? string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(p) && File.Exists(p) ? p : "ffmpeg";
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}
