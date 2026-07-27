using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseSfxMixHelper
    {
        public sealed class CtaSfxOptions
        {
            public bool Enabled { get; set; }

            public string FileName { get; set; } = string.Empty;

            public double OffsetSeconds { get; set; }

            public int VolumePercent { get; set; } = ShowcaseSfxCatalog.DefaultVolumePercent;
        }

        public sealed class HookSfxOptions
        {
            public bool Enabled { get; set; }

            public string FileName { get; set; } = string.Empty;

            public double OffsetSeconds { get; set; }

            public int VolumePercent { get; set; } = ShowcaseSfxCatalog.DefaultVolumePercent;
        }

        public static async Task<string> MixIntoNarrationIfNeededAsync(
            string ffmpegExecutable,
            string ffprobeExecutable,
            string narrationPath,
            IList<AiVideoGenInputItem> orderedScenes,
            IList<string> clipPaths,
            HookSfxOptions hookSfx,
            CtaSfxOptions ctaSfx,
            bool masterEnabled,
            double transitionSeconds,
            double finalTimelineSeconds,
            AppSettings settings,
            string workDirectory,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var narrIn = (narrationPath ?? string.Empty).Trim();
            if (!masterEnabled || string.IsNullOrEmpty(narrIn) || !File.Exists(narrIn))
            {
                return narrIn;
            }

            var timeline = await ShowcaseSfxTimelineHelper.BuildAsync(
                    orderedScenes,
                    clipPaths,
                    transitionSeconds,
                    ffprobeExecutable,
                    cancellationToken)
                .ConfigureAwait(false);

            var cues = CollectCues(
                orderedScenes,
                hookSfx,
                ctaSfx,
                timeline,
                finalTimelineSeconds,
                settings);
            if (cues.Count == 0)
            {
                return narrIn;
            }

            Directory.CreateDirectory(workDirectory ?? ".");
            var output = Path.Combine(workDirectory ?? ".", "narration_with_sfx.mp3");
            await RunMixAsync(
                    ffmpegExecutable,
                    narrIn,
                    cues,
                    output,
                    logAction,
                    cancellationToken)
                .ConfigureAwait(false);
            logAction?.Invoke("[Showcase SFX] Đã chèn " + cues.Count + " hiệu ứng âm thanh lên timeline thoại.");
            return output;
        }

        private sealed class SfxCue
        {
            public string Path { get; set; }

            public double StartSeconds { get; set; }

            public double VolumeLinear { get; set; }
        }

        private static List<SfxCue> CollectCues(
            IList<AiVideoGenInputItem> orderedScenes,
            HookSfxOptions hookSfx,
            CtaSfxOptions ctaSfx,
            ShowcaseSfxTimelineHelper.TimelinePlan timeline,
            double finalTimelineSeconds,
            AppSettings settings)
        {
            var cues = new List<SfxCue>();
            var rawTotal = timeline.RawStitchedSeconds;
            var finalDur = finalTimelineSeconds > 0.01d ? finalTimelineSeconds : rawTotal;
            var sceneCount = orderedScenes?.Count ?? 0;

            if (hookSfx != null && hookSfx.Enabled)
            {
                var hookFile = (hookSfx.FileName ?? string.Empty).Trim();
                if (hookFile.Length > 0)
                {
                    var hookPath = ShowcaseSfxCatalog.ResolveFilePath(settings, hookFile);
                    if (!string.IsNullOrWhiteSpace(hookPath) && File.Exists(hookPath))
                    {
                        var start = Math.Max(0d, hookSfx.OffsetSeconds);
                        var vol = ShowcaseSfxCatalog.ClampVolumePercent(hookSfx.VolumePercent) / 100d;
                        cues.Add(new SfxCue { Path = hookPath, StartSeconds = start, VolumeLinear = vol });
                    }
                }
            }

            for (var i = 0; i < sceneCount; i++)
            {
                var scene = orderedScenes[i];
                if (scene == null || !scene.ShowcaseSfxEnabled)
                {
                    continue;
                }

                var file = (scene.ShowcaseSfxFile ?? string.Empty).Trim();
                if (file.Length == 0)
                {
                    continue;
                }

                var sfxPath = ShowcaseSfxCatalog.ResolveFilePath(settings, file);
                if (string.IsNullOrWhiteSpace(sfxPath) || !File.Exists(sfxPath))
                {
                    continue;
                }

                var placement = ShowcaseSfxCatalog.NormalizePlacement(scene.ShowcaseSfxPlacement);
                var rawStart = i < timeline.SceneStartSeconds.Length ? timeline.SceneStartSeconds[i] : 0d;
                var dur = i < timeline.SceneDurationSeconds.Length ? timeline.SceneDurationSeconds[i] : 6d;
                if (placement == ShowcaseSfxCatalog.PlacementSceneEnd)
                {
                    rawStart += Math.Max(0d, dur - 0.2d);
                }
                else if (placement == ShowcaseSfxCatalog.PlacementSceneMiddle)
                {
                    rawStart += Math.Max(0d, dur * 0.5d);
                }

                rawStart += scene.ShowcaseSfxOffsetSeconds;
                var start = ShowcaseSfxTimelineHelper.ScaleTime(rawStart, rawTotal, finalDur);
                var vol = ShowcaseSfxCatalog.ClampVolumePercent(scene.ShowcaseSfxVolumePercent) / 100d;
                cues.Add(new SfxCue { Path = sfxPath, StartSeconds = start, VolumeLinear = vol });
            }

            if (ctaSfx != null && ctaSfx.Enabled && sceneCount > 0)
            {
                var ctaFile = (ctaSfx.FileName ?? string.Empty).Trim();
                if (ctaFile.Length > 0)
                {
                    var ctaPath = ShowcaseSfxCatalog.ResolveFilePath(settings, ctaFile);
                    if (!string.IsNullOrWhiteSpace(ctaPath) && File.Exists(ctaPath))
                    {
                        var last = sceneCount - 1;
                        var rawCta = last < timeline.SceneStartSeconds.Length ? timeline.SceneStartSeconds[last] : 0d;
                        rawCta += ctaSfx.OffsetSeconds;
                        var startCta = ShowcaseSfxTimelineHelper.ScaleTime(rawCta, rawTotal, finalDur);
                        var volCta = ShowcaseSfxCatalog.ClampVolumePercent(ctaSfx.VolumePercent) / 100d;
                        cues.Add(new SfxCue { Path = ctaPath, StartSeconds = startCta, VolumeLinear = volCta });
                    }
                }
            }

            return cues
                .OrderBy(c => c.StartSeconds)
                .ToList();
        }

        private static async Task RunMixAsync(
            string ffmpegExecutable,
            string narrationPath,
            IList<SfxCue> cues,
            string outputPath,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var ffmpeg = (ffmpegExecutable ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ffmpeg) || !File.Exists(ffmpeg))
            {
                ffmpeg = FfmpegToolkitService.GetBundledFfmpegPath();
            }

            var inputs = new List<string> { "-i \"" + narrationPath + "\"" };
            foreach (var cue in cues)
            {
                inputs.Add("-i \"" + cue.Path + "\"");
            }

            var filterParts = new List<string> { "[0:a]aresample=48000,volume=1.0[narr]" };
            var mixLabels = new List<string> { "[narr]" };
            for (var i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                var delayMs = (int)Math.Round(Math.Max(0d, cue.StartSeconds) * 1000d);
                var vol = cue.VolumeLinear.ToString("0.###", CultureInfo.InvariantCulture);
                var label = "s" + i;
                filterParts.Add("[" + (i + 1) + ":a]aresample=48000,adelay=" + delayMs + "|" + delayMs + ",volume=" + vol + "[" + label + "]");
                mixLabels.Add("[" + label + "]");
            }

            filterParts.Add(string.Join("", mixLabels) + "amix=inputs=" + mixLabels.Count +
                            ":duration=first:dropout_transition=0.3[aout]");

            var args = "-y " + string.Join(" ", inputs) +
                       " -filter_complex \"" + string.Join(";", filterParts) + "\"" +
                       " -map \"[aout]\" -c:a libmp3lame -q:a 4 \"" + outputPath + "\"";

            logAction?.Invoke("[Showcase SFX] FFmpeg trộn " + cues.Count + " track…");
            await RunFfmpegAsync(ffmpeg, args, logAction, cancellationToken).ConfigureAwait(false);
        }

        private static async Task RunFfmpegAsync(
            string ffmpegExecutable,
            string args,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExecutable,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken, 180).ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    var err = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    logAction?.Invoke("[Showcase SFX] FFmpeg: " + err);
                    throw new InvalidOperationException("FFmpeg SFX mix failed (exit " + process.ExitCode + ").");
                }
            }
        }
    }
}
