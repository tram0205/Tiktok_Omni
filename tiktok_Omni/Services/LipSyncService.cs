using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Auto-Lipsync: phân tích biên độ âm thanh theo khung thời gian, chọn Closed / Open-Small / Open-Large,
    /// overlay mượt (hysteresis + giữ trạng thái tối thiểu) qua FFmpeg.
    /// </summary>
    public sealed class LipSyncService
    {
        private const double WindowSeconds = 0.04d;
        private const double MinStateHoldSeconds = 0.10d;
        private const double CrossfadeSeconds = 0.05d;
        private const int TargetFps = 30;

        public enum MouthState
        {
            Closed = 0,
            OpenSmall = 1,
            OpenLarge = 2
        }

        public sealed class MouthStateSegment
        {
            public double StartSeconds { get; set; }
            public double EndSeconds { get; set; }
            public MouthState State { get; set; }
        }

        public sealed class LipSyncRenderRequest
        {
            public string BaseImagePath { get; set; } = string.Empty;
            public string VideoPath { get; set; } = string.Empty;
            public string AudioPath { get; set; } = string.Empty;
            public string MouthClosedPath { get; set; } = string.Empty;
            public string MouthOpenSmallPath { get; set; } = string.Empty;
            public string MouthOpenPath { get; set; } = string.Empty;
            public string OutputPath { get; set; } = string.Empty;
            public int OverlayX { get; set; }
            public int OverlayY { get; set; } = 1180;
            public double OverlayScale { get; set; } = 1.0d;
            public int OutputWidth { get; set; } = 1080;
            public int OutputHeight { get; set; } = 1920;
        }

        /// <summary>Preview trên ảnh mascot gốc (loop 1 frame + audio).</summary>
        public Task<string> RenderPreviewOnBaseAsync(
            LipSyncRenderRequest request,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            request.VideoPath = string.Empty;
            if (string.IsNullOrWhiteSpace(request.BaseImagePath) || !File.Exists(request.BaseImagePath))
            {
                throw new FileNotFoundException("Ảnh mascot gốc không tồn tại.", request.BaseImagePath);
            }

            return RenderInternalAsync(request, settings, logAction, cancellationToken);
        }

        /// <summary>Overlay miệng lên video đã ghép (sau concat scene).</summary>
        public Task<string> ApplyToVideoAsync(
            LipSyncRenderRequest request,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            request.BaseImagePath = string.Empty;
            if (string.IsNullOrWhiteSpace(request.VideoPath) || !File.Exists(request.VideoPath))
            {
                throw new FileNotFoundException("Video nguồn không tồn tại.", request.VideoPath);
            }

            return RenderInternalAsync(request, settings, logAction, cancellationToken);
        }

        /// <summary>Thử LipSync; lỗi hoặc thiếu asset → trả video gốc (không ném exception ra pipeline).</summary>
        public async Task<string> TryApplyToVideoWithFallbackAsync(
            LipSyncRenderRequest request,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.VideoPath) || !File.Exists(request.VideoPath))
            {
                return request?.VideoPath ?? string.Empty;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(request.MouthOpenPath) || !File.Exists(request.MouthOpenPath))
                {
                    logAction?.Invoke("LipSync: thiếu mouth_open.png — giữ video gốc.");
                    return request.VideoPath;
                }

                if (string.IsNullOrWhiteSpace(request.MouthClosedPath) || !File.Exists(request.MouthClosedPath))
                {
                    request.MouthClosedPath = request.MouthOpenPath;
                }

                if (string.IsNullOrWhiteSpace(request.MouthOpenSmallPath) || !File.Exists(request.MouthOpenSmallPath))
                {
                    request.MouthOpenSmallPath = request.MouthOpenPath;
                }

                return await ApplyToVideoAsync(request, settings, logAction, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logAction?.Invoke("LipSync: lỗi FFmpeg — fallback video gốc: " + ex.Message);
                return request.VideoPath;
            }
        }

        private async Task<string> RenderInternalAsync(
            LipSyncRenderRequest request,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            ValidateMouthAssets(request);
            if (string.IsNullOrWhiteSpace(request.AudioPath) || !File.Exists(request.AudioPath))
            {
                throw new FileNotFoundException("File audio narration không tồn tại.", request.AudioPath);
            }

            if (!FfmpegToolkitService.TryResolve(settings, out var toolkit, out var resolveError))
            {
                throw new InvalidOperationException(resolveError ?? "FFmpeg chưa cấu hình.");
            }

            double duration;
            if (!string.IsNullOrWhiteSpace(request.VideoPath) && File.Exists(request.VideoPath))
            {
                duration = await ProbeVideoDurationAsync(toolkit.FfmpegExe, request.VideoPath, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                duration = await ProbeAudioDurationAsync(toolkit.FfmpegExe, request.AudioPath, cancellationToken)
                    .ConfigureAwait(false);
            }

            duration = Math.Max(0.5d, duration);

            var segments = await BuildSmoothedTimelineAsync(
                toolkit.FfmpegExe,
                request.AudioPath,
                duration,
                logAction,
                cancellationToken).ConfigureAwait(false);

            var outputPath = request.OutputPath;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                var dir = Path.GetDirectoryName(request.VideoPath ?? request.BaseImagePath) ?? Path.GetTempPath();
                outputPath = Path.Combine(dir, "lipsync_" + DateTime.Now.ToString("HHmmss") + ".mp4");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

            var smallPath = ResolveOpenSmallPath(request);
            var filter = BuildOverlayFilterComplex(
                request,
                smallPath,
                segments,
                duration,
                !string.IsNullOrWhiteSpace(request.BaseImagePath));

            var args = BuildFfmpegCommand(request, filter, duration, outputPath);
            logAction?.Invoke("LipSync: FFmpeg overlay (" + segments.Count + " đoạn trạng thái)…");
            await RunFfmpegAsync(toolkit.FfmpegExe, args, logAction, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length < 4096)
            {
                throw new InvalidOperationException("LipSync render thất bại — file output rỗng.");
            }

            logAction?.Invoke("LipSync: hoàn tất → " + outputPath);
            return outputPath;
        }

        public async Task<List<MouthStateSegment>> AnalyzeTimelineAsync(
            string audioPath,
            AppSettings settings,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (!FfmpegToolkitService.TryResolve(settings, out var toolkit, out _))
            {
                return new List<MouthStateSegment>();
            }

            var duration = await ProbeAudioDurationAsync(toolkit.FfmpegExe, audioPath, cancellationToken)
                .ConfigureAwait(false);
            return await BuildSmoothedTimelineAsync(toolkit.FfmpegExe, audioPath, duration, logAction, cancellationToken)
                .ConfigureAwait(false);
        }

        private static void ValidateMouthAssets(LipSyncRenderRequest request)
        {
            if (!File.Exists(request.MouthClosedPath))
            {
                throw new FileNotFoundException("Thiếu ảnh MouthClosed.", request.MouthClosedPath);
            }

            if (!File.Exists(request.MouthOpenPath) && !File.Exists(request.MouthOpenSmallPath))
            {
                throw new FileNotFoundException("Cần ít nhất MouthOpenPath hoặc MouthOpenSmallPath.");
            }
        }

        private static string ResolveOpenSmallPath(LipSyncRenderRequest request)
        {
            if (File.Exists(request.MouthOpenSmallPath))
            {
                return request.MouthOpenSmallPath;
            }

            return request.MouthOpenPath;
        }

        private static string ResolveOpenLargePath(LipSyncRenderRequest request)
        {
            return File.Exists(request.MouthOpenPath)
                ? request.MouthOpenPath
                : request.MouthOpenSmallPath;
        }

        private async Task<List<MouthStateSegment>> BuildSmoothedTimelineAsync(
            string ffmpegExe,
            string audioPath,
            double duration,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var samples = await SampleVolumeAsync(ffmpegExe, audioPath, duration, cancellationToken)
                .ConfigureAwait(false);
            if (samples.Count == 0)
            {
                logAction?.Invoke("LipSync: không đọc được volume — mặc định Closed.");
                return new List<MouthStateSegment>
                {
                    new MouthStateSegment { StartSeconds = 0, EndSeconds = duration, State = MouthState.Closed }
                };
            }

            var smoothed = ApplyEma(samples, 0.35d);
            var rawStates = ClassifyWithHysteresis(smoothed);
            var held = EnforceMinHold(rawStates, MinStateHoldSeconds);
            var merged = MergeAdjacent(held);
            return ApplyBoundaryCrossfade(merged, duration, CrossfadeSeconds);
        }

        private static async Task<List<(double Time, double Linear)>> SampleVolumeAsync(
            string ffmpegExe,
            string audioPath,
            double duration,
            CancellationToken cancellationToken)
        {
            var args =
                "-hide_banner -nostats -i \"" + audioPath + "\" -af \"" +
                "asetnsample=n=882,astats=metadata=1:reset=1,ametadata=print:key=lavfi.astats.Overall.RMS_level\" " +
                "-f null -";

            var stderr = await RunFfmpegCaptureStderrAsync(ffmpegExe, args, cancellationToken).ConfigureAwait(false);
            var rmsRegex = new Regex(
                @"lavfi\.astats\.Overall\.RMS_level=(-?\d+(?:\.\d+)?)",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

            var samples = new List<(double Time, double Linear)>();
            var index = 0;
            foreach (var line in stderr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = rmsRegex.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var db))
                {
                    continue;
                }

                var time = index * WindowSeconds;
                if (time > duration + WindowSeconds)
                {
                    break;
                }

                var linear = DbToLinear(db);
                samples.Add((time, linear));
                index++;
            }

            return samples;
        }

        private static double DbToLinear(double db)
        {
            if (double.IsNegativeInfinity(db) || db < -80d)
            {
                return 0d;
            }

            return Math.Pow(10d, db / 20d);
        }

        private static List<(double Time, double Linear)> ApplyEma(
            IList<(double Time, double Linear)> samples,
            double alpha)
        {
            var result = new List<(double Time, double Linear)>(samples.Count);
            double prev = 0;
            foreach (var sample in samples)
            {
                prev = alpha * sample.Linear + (1d - alpha) * prev;
                result.Add((sample.Time, prev));
            }

            return result;
        }

        private static List<(double Time, MouthState State)> ClassifyWithHysteresis(
            IList<(double Time, double Linear)> samples)
        {
            const double closeEnter = 0.028d;
            const double closeExit = 0.040d;
            const double largeEnter = 0.11d;
            const double largeExit = 0.085d;

            var result = new List<(double Time, MouthState State)>();
            var current = MouthState.Closed;
            foreach (var sample in samples)
            {
                switch (current)
                {
                    case MouthState.Closed:
                        if (sample.Linear >= largeEnter)
                        {
                            current = MouthState.OpenLarge;
                        }
                        else if (sample.Linear >= closeExit)
                        {
                            current = MouthState.OpenSmall;
                        }

                        break;
                    case MouthState.OpenSmall:
                        if (sample.Linear >= largeEnter)
                        {
                            current = MouthState.OpenLarge;
                        }
                        else if (sample.Linear < closeEnter)
                        {
                            current = MouthState.Closed;
                        }

                        break;
                    case MouthState.OpenLarge:
                        if (sample.Linear < largeExit)
                        {
                            current = sample.Linear < closeExit ? MouthState.Closed : MouthState.OpenSmall;
                        }

                        break;
                }

                result.Add((sample.Time, current));
            }

            return result;
        }

        private static List<(double Start, double End, MouthState State)> EnforceMinHold(
            IList<(double Time, MouthState State)> frames,
            double minHold)
        {
            if (frames == null || frames.Count == 0)
            {
                return new List<(double Start, double End, MouthState State)>();
            }

            var segments = new List<(double Start, double End, MouthState State)>();
            var segStart = frames[0].Time;
            var segState = frames[0].State;
            for (var i = 1; i < frames.Count; i++)
            {
                if (frames[i].State == segState)
                {
                    continue;
                }

                var segEnd = frames[i].Time;
                if (segEnd - segStart < minHold && segments.Count > 0)
                {
                    segments[segments.Count - 1] = (segments[segments.Count - 1].Start, segEnd, segments[segments.Count - 1].State);
                }
                else
                {
                    segments.Add((segStart, segEnd, segState));
                }

                segStart = frames[i].Time;
                segState = frames[i].State;
            }

            var lastEnd = frames[frames.Count - 1].Time + WindowSeconds;
            segments.Add((segStart, lastEnd, segState));
            return segments;
        }

        private static List<MouthStateSegment> MergeAdjacent(IList<(double Start, double End, MouthState State)> segments)
        {
            var merged = new List<MouthStateSegment>();
            foreach (var seg in segments)
            {
                if (merged.Count > 0 && merged[merged.Count - 1].State == seg.State)
                {
                    merged[merged.Count - 1].EndSeconds = seg.End;
                }
                else
                {
                    merged.Add(new MouthStateSegment
                    {
                        StartSeconds = seg.Start,
                        EndSeconds = seg.End,
                        State = seg.State
                    });
                }
            }

            return merged;
        }

        private static List<MouthStateSegment> ApplyBoundaryCrossfade(
            IList<MouthStateSegment> segments,
            double totalDuration,
            double crossfade)
        {
            if (segments == null || segments.Count == 0)
            {
                return new List<MouthStateSegment>
                {
                    new MouthStateSegment { StartSeconds = 0, EndSeconds = totalDuration, State = MouthState.Closed }
                };
            }

            var result = new List<MouthStateSegment>();
            for (var i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                var start = Math.Max(0, seg.StartSeconds);
                var end = Math.Min(totalDuration, seg.EndSeconds);
                if (i > 0 && crossfade > 0)
                {
                    start = Math.Max(0, start - crossfade * 0.5d);
                }

                if (i < segments.Count - 1 && crossfade > 0)
                {
                    end = Math.Min(totalDuration, end + crossfade * 0.5d);
                }

                if (end <= start)
                {
                    continue;
                }

                result.Add(new MouthStateSegment
                {
                    StartSeconds = start,
                    EndSeconds = end,
                    State = seg.State
                });
            }

            return result;
        }

        private static string BuildOverlayFilterComplex(
            LipSyncRenderRequest request,
            string openSmallPath,
            IList<MouthStateSegment> segments,
            double duration,
            bool useBaseImage)
        {
            var x = Math.Max(0, request.OverlayX);
            var y = Math.Max(0, request.OverlayY);
            var scale = Math.Max(0.15d, Math.Min(2.5d, request.OverlayScale));
            var w = request.OutputWidth;
            var h = request.OutputHeight;
            var inv = CultureInfo.InvariantCulture;
            var durText = duration.ToString("0.###", inv);

            var closedExpr = BuildEnableExpression(segments, MouthState.Closed, inv);
            var smallExpr = BuildEnableExpression(segments, MouthState.OpenSmall, inv);
            var largeExpr = BuildEnableExpression(segments, MouthState.OpenLarge, inv);
            var largePath = ResolveOpenLargePath(request);

            var sb = new StringBuilder();
            if (useBaseImage)
            {
                sb.AppendFormat(inv, "[0:v]scale={0}:{1}:force_original_aspect_ratio=decrease,pad={0}:{1}:(ow-iw)/2:(oh-ih)/2,setsar=1,fps={2}[base];", w, h, TargetFps);
            }
            else
            {
                sb.AppendFormat(inv, "[0:v]scale={0}:{1},setsar=1,fps={2}[base];", w, h, TargetFps);
            }

            sb.AppendFormat(inv, "[1:v]scale=iw*{0}:ih*{0}[m0];", scale.ToString("0.###", inv));
            sb.AppendFormat(inv, "[2:v]scale=iw*{0}:ih*{0}[m1];", scale.ToString("0.###", inv));
            sb.AppendFormat(inv, "[3:v]scale=iw*{0}:ih*{0}[m2];", scale.ToString("0.###", inv));

            sb.AppendFormat(inv, "[base][m0]overlay={0}:{1}:enable='{2}':eval=frame[v0];", x, y, closedExpr);
            sb.AppendFormat(inv, "[v0][m1]overlay={0}:{1}:enable='{2}':eval=frame[v1];", x, y, smallExpr);
            sb.AppendFormat(inv, "[v1][m2]overlay={0}:{1}:enable='{2}':eval=frame[vout];", x, y, largeExpr);

            return sb.ToString();
        }

        private static string BuildEnableExpression(IList<MouthStateSegment> segments, MouthState state, CultureInfo inv)
        {
            var parts = segments
                .Where(s => s.State == state)
                .Select(s =>
                    "between(t," +
                    s.StartSeconds.ToString("0.###", inv) + "," +
                    s.EndSeconds.ToString("0.###", inv) + ")")
                .ToList();

            return parts.Count == 0 ? "0" : string.Join("+", parts);
        }

        private static string BuildFfmpegCommand(
            LipSyncRenderRequest request,
            string filterComplex,
            double duration,
            string outputPath)
        {
            var inv = CultureInfo.InvariantCulture;
            var durText = duration.ToString("0.###", inv);
            var smallPath = ResolveOpenSmallPath(request);
            var largePath = ResolveOpenLargePath(request);
            var sb = new StringBuilder("-y ");

            var useBase = !string.IsNullOrWhiteSpace(request.BaseImagePath);
            if (useBase)
            {
                sb.Append("-loop 1 -t ").Append(durText).Append(" -i \"").Append(request.BaseImagePath).Append("\" ");
            }
            else
            {
                sb.Append("-i \"").Append(request.VideoPath).Append("\" ");
            }

            sb.Append("-loop 1 -t ").Append(durText).Append(" -i \"").Append(request.MouthClosedPath).Append("\" ");
            sb.Append("-loop 1 -t ").Append(durText).Append(" -i \"").Append(smallPath).Append("\" ");
            sb.Append("-loop 1 -t ").Append(durText).Append(" -i \"").Append(largePath).Append("\" ");

            if (useBase)
            {
                sb.Append("-i \"").Append(request.AudioPath).Append("\" ");
            }

            sb.Append("-filter_complex \"").Append(filterComplex).Append("\" ");
            sb.Append("-map \"[vout]\" ");
            if (useBase)
            {
                sb.Append("-map 4:a -c:a aac -b:a 192k ");
            }
            else
            {
                sb.Append("-map 0:a? -c:a copy ");
            }

            sb.Append("-c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p -shortest \"").Append(outputPath).Append("\"");
            return sb.ToString();
        }

        private static async Task<double> ProbeVideoDurationAsync(
            string ffmpegExe,
            string videoPath,
            CancellationToken cancellationToken)
        {
            var ffprobe = Path.Combine(Path.GetDirectoryName(ffmpegExe) ?? string.Empty, "ffprobe.exe");
            if (!File.Exists(ffprobe))
            {
                return 30d;
            }

            var args = "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"" + videoPath + "\"";
            var psi = new ProcessStartInfo
            {
                FileName = ffprobe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(psi))
            {
                if (proc == null)
                {
                    return 30d;
                }

                var output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                await ProcessCancellationHelper.WaitUntilExitAsync(proc, cancellationToken).ConfigureAwait(false);
                if (double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
                {
                    return dur;
                }
            }

            return 30d;
        }

        private static async Task<double> ProbeAudioDurationAsync(
            string ffmpegExe,
            string audioPath,
            CancellationToken cancellationToken)
        {
            var ffprobe = Path.Combine(Path.GetDirectoryName(ffmpegExe) ?? string.Empty, "ffprobe.exe");
            if (!File.Exists(ffprobe))
            {
                return 30d;
            }

            var args = "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"" + audioPath + "\"";
            var psi = new ProcessStartInfo
            {
                FileName = ffprobe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(psi))
            {
                if (proc == null)
                {
                    return 30d;
                }

                var output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                await ProcessCancellationHelper.WaitUntilExitAsync(proc, cancellationToken).ConfigureAwait(false);
                if (double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
                {
                    return dur;
                }
            }

            return 30d;
        }

        private static async Task RunFfmpegAsync(
            string ffmpegExe,
            string args,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(psi))
            {
                if (proc == null)
                {
                    throw new InvalidOperationException("Không khởi chạy được FFmpeg.");
                }

                var err = proc.StandardError.ReadToEndAsync();
                await ProcessCancellationHelper.WaitUntilExitAsync(proc, cancellationToken).ConfigureAwait(false);
                var errText = await err.ConfigureAwait(false);
                if (proc.ExitCode != 0)
                {
                    throw new InvalidOperationException("FFmpeg LipSync failed: " + errText);
                }

                if (!string.IsNullOrWhiteSpace(errText))
                {
                    var line = errText.Split('\n').LastOrDefault(l => !string.IsNullOrWhiteSpace(l));
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        logAction?.Invoke("LipSync FFmpeg: " + line.Trim());
                    }
                }
            }
        }

        private static async Task<string> RunFfmpegCaptureStderrAsync(
            string ffmpegExe,
            string args,
            CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(psi))
            {
                if (proc == null)
                {
                    return string.Empty;
                }

                var errTask = proc.StandardError.ReadToEndAsync();
                await ProcessCancellationHelper.WaitUntilExitAsync(proc, cancellationToken).ConfigureAwait(false);
                return await errTask.ConfigureAwait(false);
            }
        }
    }
}
