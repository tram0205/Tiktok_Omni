using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Models;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni.Services
{
    /// <summary>Cache TTS / mix preview cho popup âm thanh batch Triết lý (mirror ShowcaseNarrationCacheHelper).</summary>
    public static class PhilosophyBatchAudioPreviewHelper
    {
        private const double VoiceMixGain = 1.65d;
        private const double AmbientBedVolume = 0.05d;

        public static string GetSessionBase(PhilosophyBatchItem batch, AppSettings settings)
        {
            ProfileScopedPaths.SetConfiguredStorageRoot(settings?.StorageRootPath);
            var root = ProfileScopedPaths.ResolveStorageRoot(settings?.StorageRootPath);
            var id = batch?.BatchId ?? Guid.Empty;
            return Path.Combine(root, "philosophy-audio-preview", id.ToString("N"));
        }

        public static string GetAudioDirectory(string sessionBase) =>
            ShowcaseNarrationCacheHelper.GetAudioDirectory(sessionBase);

        public static string GetBodyPreviewPath(string sessionBase) =>
            ShowcaseNarrationCacheHelper.GetBodyPreviewPath(sessionBase);

        public static string GetHookPreviewPath(string sessionBase) =>
            ShowcaseNarrationCacheHelper.GetHookPreviewPath(sessionBase);

        public static string GetFullMixPreviewPath(string sessionBase) =>
            ShowcaseNarrationCacheHelper.GetFullMixPreviewPath(sessionBase);

        public static string GetQuoteVoicePreviewPath(string sessionBase, int quoteIndex) =>
            Path.Combine(GetAudioDirectory(sessionBase), "quote_" + quoteIndex.ToString("D2", CultureInfo.InvariantCulture) + "_voice.mp3");

        public static string GetQuoteFullMixPreviewPath(string sessionBase, int quoteIndex) =>
            Path.Combine(GetAudioDirectory(sessionBase), "quote_" + quoteIndex.ToString("D2", CultureInfo.InvariantCulture) + "_full_mix.mp3");

        public static void DeleteQuotePreviewFiles(string sessionBase, int quoteIndex)
        {
            if (string.IsNullOrWhiteSpace(sessionBase) || quoteIndex < 0)
            {
                return;
            }

            TryDeleteFile(GetQuoteVoicePreviewPath(sessionBase, quoteIndex));
            TryDeleteFile(GetQuoteFullMixPreviewPath(sessionBase, quoteIndex));
        }

        public static void ClearAllQuotePreviewFiles(string sessionBase, int quoteCount)
        {
            if (string.IsNullOrWhiteSpace(sessionBase) || quoteCount <= 0)
            {
                return;
            }

            for (var i = 0; i < quoteCount; i++)
            {
                DeleteQuotePreviewFiles(sessionBase, i);
            }

            TryDeleteFile(GetFullMixPreviewPath(sessionBase));
            TryDeleteFile(GetBodyPreviewPath(sessionBase));
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch
            {
                // ignored — preview cache only
            }
        }

        public static bool HasQuoteVoicePreview(string sessionBase, int quoteIndex)
        {
            var path = GetQuoteVoicePreviewPath(sessionBase, quoteIndex);
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        public static bool HasQuoteFullMixPreview(string sessionBase, int quoteIndex)
        {
            var path = GetQuoteFullMixPreviewPath(sessionBase, quoteIndex);
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        public static bool HasAnyQuoteVoicePreview(string sessionBase, int quoteCount)
        {
            for (var i = 0; i < quoteCount; i++)
            {
                if (HasQuoteVoicePreview(sessionBase, i))
                {
                    return true;
                }
            }

            return HasVoicePreview(sessionBase);
        }

        public static bool HasAnyQuoteFullMixPreview(string sessionBase, int quoteCount)
        {
            for (var i = 0; i < quoteCount; i++)
            {
                if (HasQuoteFullMixPreview(sessionBase, i))
                {
                    return true;
                }
            }

            return HasFullMixPreview(sessionBase);
        }

        public static bool HasVoicePreview(string sessionBase)
        {
            var body = GetBodyPreviewPath(sessionBase);
            if (!string.IsNullOrWhiteSpace(body) && File.Exists(body))
            {
                return true;
            }

            var hook = GetHookPreviewPath(sessionBase);
            return !string.IsNullOrWhiteSpace(hook) && File.Exists(hook);
        }

        public static bool HasFullMixPreview(string sessionBase)
        {
            var path = GetFullMixPreviewPath(sessionBase);
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        public static string ResolveVoicePreviewPath(string sessionBase)
        {
            var body = GetBodyPreviewPath(sessionBase);
            if (!string.IsNullOrWhiteSpace(body) && File.Exists(body))
            {
                return body;
            }

            var hook = GetHookPreviewPath(sessionBase);
            return !string.IsNullOrWhiteSpace(hook) && File.Exists(hook) ? hook : string.Empty;
        }

        public static IReadOnlyList<int> ResolveQuoteIndicesForOperation(
            PhilosophyBatchItem batch,
            IReadOnlyList<int> quoteIndices)
        {
            if (batch?.Quotes == null || batch.Quotes.Count == 0)
            {
                return Array.Empty<int>();
            }

            IEnumerable<int> candidates;
            if (quoteIndices != null && quoteIndices.Count > 0)
            {
                candidates = quoteIndices
                    .Where(i => i >= 0 && i < batch.Quotes.Count)
                    .Distinct()
                    .OrderBy(i => i);
            }
            else
            {
                candidates = Enumerable.Range(0, batch.Quotes.Count);
            }

            return candidates
                .Where(i =>
                {
                    var quote = batch.Quotes[i];
                    return quote != null && !string.IsNullOrWhiteSpace(quote.Content);
                })
                .ToList();
        }

        public static async Task GenerateQuoteVoicePreviewAsync(
            PhilosophyBatchItem batch,
            ShowcaseVideoItem video,
            AppSettings settings,
            string profileName,
            VideoProcessingService videoProcessingService,
            Action<string> log,
            CancellationToken cancellationToken,
            IReadOnlyList<int> quoteIndices = null)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            if (videoProcessingService == null)
            {
                throw new ArgumentNullException(nameof(videoProcessingService));
            }

            var sessionBase = GetSessionBase(batch, settings);
            Directory.CreateDirectory(GetAudioDirectory(sessionBase));
            ShowcaseTtsHelper.EnsureVideoDefaults(video, settings);
            FfmpegToolkitService.TryResolve(settings, out var toolkit, out _);
            var ffmpeg = toolkit?.FfmpegExe ?? FfmpegToolkitService.GetBundledFfmpegPath();
            var ffprobe = toolkit?.FfprobeExe ?? FfmpegToolkitService.GetBundledFfprobePath();
            var ttsOptions = ShowcaseTtsRenderOptions.FromVideo(video, settings);

            var indices = ResolveQuoteIndicesForOperation(batch, quoteIndices);
            if (indices.Count == 0)
            {
                throw new InvalidOperationException("Chọn ít nhất một câu quote có nội dung để tạo audio.");
            }

            log?.Invoke("[Quote] Tạo audio thoại cho "
                         + indices.Count.ToString(CultureInfo.InvariantCulture)
                         + " câu (dòng "
                         + string.Join(", ", indices.Select(i => (i + 1).ToString(CultureInfo.InvariantCulture)))
                         + ")…");
            foreach (var i in indices)
            {
                var quote = batch.Quotes[i];
                if (quote == null || string.IsNullOrWhiteSpace(quote.Content))
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                await GenerateSingleQuoteVoicePreviewAsync(
                    batch,
                    quote,
                    i,
                    sessionBase,
                    settings,
                    ffmpeg,
                    ffprobe,
                    ttsOptions,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }

            var firstDest = GetQuoteVoicePreviewPath(sessionBase, indices[0]);
            if (File.Exists(firstDest))
            {
                var legacyBody = GetBodyPreviewPath(sessionBase);
                Directory.CreateDirectory(Path.GetDirectoryName(legacyBody) ?? GetAudioDirectory(sessionBase));
                File.Copy(firstDest, legacyBody, overwrite: true);
            }
        }

        private static async Task GenerateSingleQuoteVoicePreviewAsync(
            PhilosophyBatchItem batch,
            PhilosophyScriptItem quote,
            int quoteIndex,
            string sessionBase,
            AppSettings settings,
            string ffmpeg,
            string ffprobe,
            ShowcaseTtsRenderOptions ttsOptions,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var quoteSession = Path.Combine(sessionBase, "quotes", quoteIndex.ToString("D2", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(GetAudioDirectory(quoteSession));
            var dest = GetQuoteVoicePreviewPath(sessionBase, quoteIndex);
            var speedPct = PhilosophyBatchHelper.ResolveQuoteNarrationSpeedPercent(quote, batch);

            await PhilosophyBatchTtsHelper.GenerateQuoteVoiceMp3Async(
                quote.Content.Trim(),
                ttsOptions,
                settings,
                dest,
                quoteSession,
                speedPct,
                ffmpeg,
                ffprobe,
                log,
                cancellationToken).ConfigureAwait(false);

            log?.Invoke("[Quote] Thoại câu "
                         + (quoteIndex + 1).ToString(CultureInfo.InvariantCulture) + " → " + dest);
        }

        public static async Task RenderFullMixPreviewAsync(
            PhilosophyBatchItem batch,
            AppSettings settings,
            string profileName,
            Action<string> log,
            CancellationToken cancellationToken,
            IReadOnlyList<int> quoteIndices = null)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            var sessionBase = GetSessionBase(batch, settings);
            var indices = ResolveQuoteIndicesForOperation(batch, quoteIndices);
            if (indices.Count == 0)
            {
                throw new InvalidOperationException("Chọn ít nhất một câu quote có nội dung để render audio.");
            }

            FfmpegToolkitService.TryResolve(settings, out var toolkit, out _);
            var ffmpeg = toolkit?.FfmpegExe ?? FfmpegToolkitService.GetBundledFfmpegPath();
            var ffprobe = toolkit?.FfprobeExe ?? FfmpegToolkitService.GetBundledFfprobePath();
            if (string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(ffmpeg))
            {
                throw new InvalidOperationException("Không tìm thấy FFmpeg.");
            }

            var audioDir = GetAudioDirectory(sessionBase);
            Directory.CreateDirectory(audioDir);
            var rendered = 0;

            log?.Invoke("[Quote] Render thành phẩm cho "
                         + indices.Count.ToString(CultureInfo.InvariantCulture)
                         + " câu (dòng "
                         + string.Join(", ", indices.Select(i => (i + 1).ToString(CultureInfo.InvariantCulture)))
                         + ")…");

            foreach (var i in indices)
            {
                var quote = batch.Quotes[i];
                var voiceMp3 = GetQuoteVoicePreviewPath(sessionBase, i);
                if (quote == null || string.IsNullOrWhiteSpace(voiceMp3) || !File.Exists(voiceMp3))
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                var musicVol = PhilosophyBatchHelper.ResolveMusicBedLinearVolume(
                    PhilosophyBatchHelper.ResolveQuoteMusicVolumePercent(quote, batch));
                var mood = quote.Mood ?? "reflective";
                var musicPath = PhilosophyProfileAssets.ResolveMusicPath(
                    quote.MusicFolder,
                    profileName,
                    settings,
                    mood);
                if (string.IsNullOrEmpty(musicPath))
                {
                    musicPath = PhilosophyProfileAssets.ResolveMusicPath(
                        batch.MusicFolder,
                        profileName,
                        settings,
                        mood);
                }

                if (string.IsNullOrEmpty(musicPath))
                {
                    musicPath = PhilosophyProfileAssets.TryPickMusicFile(profileName, mood);
                }

                var ambientPath = PhilosophyAmbientCatalog.ResolveAmbientMediaPath(
                    quote.AmbientKey,
                    profileName,
                    settings,
                    mood);
                if (string.IsNullOrEmpty(ambientPath))
                {
                    ambientPath = PhilosophyAmbientCatalog.ResolveAmbientMediaPath(
                        batch.AmbientKey,
                        profileName,
                        settings,
                        mood);
                }

                var workDir = Path.Combine(audioDir, "full_mix_work_" + i.ToString("D2", CultureInfo.InvariantCulture));
                Directory.CreateDirectory(workDir);
                var voiceWav = Path.Combine(workDir, "voice.wav");
                var mixWav = Path.Combine(workDir, "mix.wav");
                var outputMp3 = GetQuoteFullMixPreviewPath(sessionBase, i);

                await ConvertToWavAsync(ffmpeg, voiceMp3, voiceWav, log, cancellationToken).ConfigureAwait(false);
                var voiceSeconds = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                    ffprobe,
                    voiceWav,
                    cancellationToken).ConfigureAwait(false);
                if (voiceSeconds <= 0.05d)
                {
                    voiceSeconds = 8d;
                }

                var outputSeconds = Math.Max(voiceSeconds, batch.MinDurationSeconds > 0 ? batch.MinDurationSeconds : 15d);
                await BuildVoicePlusMusicWavAsync(
                    ffmpeg,
                    voiceWav,
                    musicPath,
                    ambientPath,
                    voiceSeconds,
                    outputSeconds,
                    mixWav,
                    musicVol,
                    log,
                    cancellationToken).ConfigureAwait(false);

                await ConvertToMp3Async(ffmpeg, mixWav, outputMp3, log, cancellationToken).ConfigureAwait(false);
                rendered++;
                log?.Invoke("[Quote] Audio thành phẩm câu " + (i + 1).ToString(CultureInfo.InvariantCulture) + " → " + outputMp3);
            }

            if (rendered == 0)
            {
                throw new InvalidOperationException("Chưa có audio thoại cho dòng đã chọn — bấm «Tạo audio quote» trước.");
            }

            var firstMix = GetQuoteFullMixPreviewPath(sessionBase, indices[0]);
            if (File.Exists(firstMix))
            {
                File.Copy(firstMix, GetFullMixPreviewPath(sessionBase), overwrite: true);
            }
        }

        private static async Task ConvertToWavAsync(
            string ffmpeg,
            string inputPath,
            string outputWav,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var args = "-y -i \"" + inputPath + "\" -ac 1 -ar 48000 -c:a pcm_s16le \"" + outputWav + "\"";
            await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
        }

        private static async Task ConvertToMp3Async(
            string ffmpeg,
            string inputWav,
            string outputMp3,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var args = "-y -i \"" + inputWav + "\" -c:a libmp3lame -q:a 2 \"" + outputMp3 + "\"";
            await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
        }

        private static async Task BuildVoicePlusMusicWavAsync(
            string ffmpeg,
            string voiceWav,
            string musicMp3,
            string ambientPath,
            double voiceSourceSeconds,
            double outputSeconds,
            string outputWav,
            double musicBedVolume,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var voiceStr = voiceSourceSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var outStr = outputSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var musicVol = musicBedVolume.ToString("0.###", CultureInfo.InvariantCulture);
            var ambientVol = AmbientBedVolume.ToString("0.###", CultureInfo.InvariantCulture);
            var voiceGain = VoiceMixGain.ToString("0.###", CultureInfo.InvariantCulture);
            var hasMusic = !string.IsNullOrWhiteSpace(musicMp3) && File.Exists(musicMp3);
            var hasAmbient = !string.IsNullOrWhiteSpace(ambientPath) && File.Exists(ambientPath);
            var padVoice = outputSeconds > voiceSourceSeconds + 0.05d;
            var voiceFilter = padVoice
                ? "[0:a]atrim=0:" + voiceStr + ",asetpts=PTS-STARTPTS,aresample=48000,apad=whole_dur=" + outStr + ",volume=" + voiceGain + "[vo]"
                : "[0:a]atrim=0:" + voiceStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + voiceGain + "[vo]";

            string filter;
            string inputs;
            if (hasMusic && hasAmbient)
            {
                inputs = "-y -i \"" + voiceWav + "\" -i \"" + musicMp3 + "\" -i \"" + ambientPath + "\"";
                filter =
                    voiceFilter + ";" +
                    "[1:a]atrim=0:" + outStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + musicVol + "[bg];" +
                    "[2:a]atrim=0:" + outStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + ambientVol + "[amb];" +
                    "[vo][bg][amb]amix=inputs=3:duration=longest:dropout_transition=2:normalize=0[aout]";
            }
            else if (hasMusic)
            {
                inputs = "-y -i \"" + voiceWav + "\" -i \"" + musicMp3 + "\"";
                filter =
                    voiceFilter + ";" +
                    "[1:a]atrim=0:" + outStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + musicVol + "[bg];" +
                    "[vo][bg]amix=inputs=2:duration=longest:dropout_transition=2:normalize=0[aout]";
            }
            else if (hasAmbient)
            {
                inputs = "-y -i \"" + voiceWav + "\" -i \"" + ambientPath + "\"";
                filter =
                    voiceFilter + ";" +
                    "[1:a]atrim=0:" + outStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + ambientVol + "[amb];" +
                    "[vo][amb]amix=inputs=2:duration=longest:dropout_transition=2:normalize=0[aout]";
            }
            else
            {
                var padArgs = "-y -i \"" + voiceWav + "\" -filter:a \"apad=whole_dur=" + outStr + "\" -t " + outStr +
                              " \"" + outputWav + "\"";
                await RunFfmpegAsync(ffmpeg, padArgs, log, cancellationToken).ConfigureAwait(false);
                return;
            }

            var args = inputs + " -filter_complex \"" + filter + "\" -map \"[aout]\" -t " + outStr +
                       " -c:a pcm_s16le \"" + outputWav + "\"";
            await RunFfmpegAsync(ffmpeg, args, log, cancellationToken).ConfigureAwait(false);
        }

        private static Task RunFfmpegAsync(
            string ffmpegExecutable,
            string args,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegExecutable,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        throw new InvalidOperationException("Không khởi chạy được FFmpeg.");
                    }

                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        var err = proc.StandardError.ReadToEnd();
                        log?.Invoke("[Quote] FFmpeg lỗi: " + err);
                        throw new InvalidOperationException("FFmpeg thất bại (mã " + proc.ExitCode + ").");
                    }
                }
            }, cancellationToken);
        }
    }
}
