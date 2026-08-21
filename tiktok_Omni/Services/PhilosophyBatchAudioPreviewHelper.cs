using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

        public static string GetQuoteVoiceSourcePath(string sessionBase, int quoteIndex) =>
            Path.Combine(GetAudioDirectory(sessionBase), "quote_" + quoteIndex.ToString("D2", CultureInfo.InvariantCulture) + "_voice_source.mp3");

        public static string GetQuoteFullMixPreviewPath(string sessionBase, int quoteIndex) =>
            Path.Combine(GetAudioDirectory(sessionBase), "quote_" + quoteIndex.ToString("D2", CultureInfo.InvariantCulture) + "_full_mix.mp3");

        public static string GetQuoteVoiceStampPath(string sessionBase, int quoteIndex) =>
            Path.Combine(GetAudioDirectory(sessionBase), "quote_" + quoteIndex.ToString("D2", CultureInfo.InvariantCulture) + "_voice.stamp");

        public static string GetQuoteMixStampPath(string sessionBase, int quoteIndex) =>
            Path.Combine(GetAudioDirectory(sessionBase), "quote_" + quoteIndex.ToString("D2", CultureInfo.InvariantCulture) + "_mix.stamp");

        public static string ComputeQuoteVoiceStamp(
            PhilosophyScriptItem quote,
            PhilosophyBatchItem batch,
            ShowcaseVideoItem video,
            AppSettings settings)
        {
            var content = (quote?.Content ?? string.Empty).Trim();
            var speed = PhilosophyBatchHelper.ResolveQuoteNarrationSpeedPercent(quote, batch);
            ShowcaseTtsHelper.EnsureVideoDefaults(video, settings);
            var tts = ShowcaseTtsRenderOptions.FromVideo(video, settings);
            var sb = new StringBuilder();
            sb.Append(content).Append('|').Append(speed.ToString(CultureInfo.InvariantCulture));
            sb.Append('|').Append((int)tts.BodyEngine).Append('|').Append(tts.BodyVoicePresetId ?? string.Empty);
            sb.Append('|').Append(tts.BodyVoiceAgeId ?? string.Empty).Append('|').Append(tts.BodyVoiceLanguageId ?? string.Empty);
            sb.Append('|').Append(tts.BodyElevenPersona ?? string.Empty).Append('|').Append(tts.BodyVoiceToneId ?? string.Empty);
            sb.Append('|').Append(tts.BodyStyleKey ?? string.Empty);
            sb.Append('|').Append(tts.BodyEdgeRateOffsetPercent).Append('|').Append(tts.BodyEdgePitchOffsetHz);
            sb.Append('|').Append(tts.BodyElevenCustomStabilityPercent);
            sb.Append('|').Append(tts.BodyElevenCustomSimilarityPercent).Append('|').Append(tts.BodyElevenCustomStylePercent);
            return sb.ToString();
        }

        public static string ComputeQuoteMixStamp(
            PhilosophyScriptItem quote,
            PhilosophyBatchItem batch,
            ShowcaseVideoItem video,
            AppSettings settings)
        {
            var voiceStamp = ComputeQuoteVoiceStamp(quote, batch, video, settings);
            var music = (quote?.MusicFolder ?? batch?.MusicFolder ?? string.Empty).Trim();
            var ambient = PhilosophyAmbientCatalog.NormalizeKey(quote?.AmbientKey ?? batch?.AmbientKey);
            var volume = PhilosophyBatchHelper.ResolveQuoteMusicVolumePercent(quote, batch);
            var ambientVol = PhilosophyBatchHelper.ResolveAmbientVolumePercent();
            return voiceStamp + "||mix|" + music + "|" + ambient + "|" + volume.ToString(CultureInfo.InvariantCulture)
                   + "|amb" + ambientVol.ToString(CultureInfo.InvariantCulture);
        }

        public static void WriteQuoteVoiceStamp(
            string sessionBase,
            int quoteIndex,
            PhilosophyScriptItem quote,
            PhilosophyBatchItem batch,
            ShowcaseVideoItem video,
            AppSettings settings)
        {
            var path = GetQuoteVoiceStampPath(sessionBase, quoteIndex);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? GetAudioDirectory(sessionBase));
            File.WriteAllText(path, ComputeQuoteVoiceStamp(quote, batch, video, settings));
        }

        public static void WriteQuoteMixStamp(
            string sessionBase,
            int quoteIndex,
            PhilosophyScriptItem quote,
            PhilosophyBatchItem batch,
            ShowcaseVideoItem video,
            AppSettings settings)
        {
            var path = GetQuoteMixStampPath(sessionBase, quoteIndex);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? GetAudioDirectory(sessionBase));
            File.WriteAllText(path, ComputeQuoteMixStamp(quote, batch, video, settings));
        }

        public static bool IsQuoteVoiceCurrent(
            string sessionBase,
            int quoteIndex,
            PhilosophyScriptItem quote,
            PhilosophyBatchItem batch,
            ShowcaseVideoItem video,
            AppSettings settings)
        {
            if (!HasQuoteVoicePreview(sessionBase, quoteIndex))
            {
                return false;
            }

            var stampPath = GetQuoteVoiceStampPath(sessionBase, quoteIndex);
            if (!File.Exists(stampPath))
            {
                return false;
            }

            try
            {
                var saved = (File.ReadAllText(stampPath) ?? string.Empty).Trim();
                return string.Equals(saved, ComputeQuoteVoiceStamp(quote, batch, video, settings), StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        public static int ReadBakedQuoteVoiceSpeedPercent(string sessionBase, int quoteIndex)
        {
            var stampPath = GetQuoteVoiceStampPath(sessionBase, quoteIndex);
            if (!File.Exists(stampPath))
            {
                return ShowcaseNarrationSpeedHelper.DefaultManualSpeedPercent;
            }

            try
            {
                var parts = (File.ReadAllText(stampPath) ?? string.Empty).Trim().Split('|');
                if (parts.Length >= 2
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var speed))
                {
                    return ShowcaseNarrationSpeedHelper.ResolveEffectiveSpeedPercent(speed);
                }
            }
            catch
            {
                // ignored
            }

            return ShowcaseNarrationSpeedHelper.DefaultManualSpeedPercent;
        }

        public static bool IsQuoteVoiceStaleOnlyDueToSpeed(
            string sessionBase,
            int quoteIndex,
            PhilosophyScriptItem quote,
            PhilosophyBatchItem batch,
            ShowcaseVideoItem video,
            AppSettings settings)
        {
            if (!HasQuoteVoicePreview(sessionBase, quoteIndex))
            {
                return false;
            }

            if (IsQuoteVoiceCurrent(sessionBase, quoteIndex, quote, batch, video, settings))
            {
                return false;
            }

            var stampPath = GetQuoteVoiceStampPath(sessionBase, quoteIndex);
            if (!File.Exists(stampPath))
            {
                return false;
            }

            try
            {
                var stored = (File.ReadAllText(stampPath) ?? string.Empty).Trim();
                var expected = ComputeQuoteVoiceStamp(quote, batch, video, settings);
                if (string.Equals(stored, expected, StringComparison.Ordinal))
                {
                    return false;
                }

                var expParts = expected.Split('|');
                var stParts = stored.Split('|');
                if (expParts.Length < 2 || expParts.Length != stParts.Length)
                {
                    return false;
                }

                expParts[1] = stParts[1];
                return string.Equals(string.Join("|", expParts), stored, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        public static async Task PlayMp3AtListenSpeedAsync(
            string sourceMp3,
            int bakedSpeedPercent,
            int listenSpeedPercent,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken = default)
        {
            var source = (sourceMp3 ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(source) || !File.Exists(source))
            {
                throw new FileNotFoundException("Không tìm thấy file audio để phát.", source);
            }

            var baked = ShowcaseNarrationSpeedHelper.ResolveEffectiveSpeedPercent(bakedSpeedPercent);
            var listen = ShowcaseNarrationSpeedHelper.ResolveEffectiveSpeedPercent(listenSpeedPercent);
            var effectivePercent = (int)Math.Round(listen * 100d / Math.Max(1, baked));
            if (Math.Abs(effectivePercent - 100) <= 3)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = source,
                    UseShellExecute = true
                });
                return;
            }

            FfmpegToolkitService.TryResolve(settings, out var toolkit, out _);
            var ffmpeg = toolkit?.FfmpegExe ?? FfmpegToolkitService.GetBundledFfmpegPath();
            var ffprobe = toolkit?.FfprobeExe ?? FfmpegToolkitService.GetBundledFfprobePath();
            if (string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(ffmpeg))
            {
                throw new InvalidOperationException("Không tìm thấy FFmpeg để nghe thử tốc độ.");
            }

            var cacheDir = Path.Combine(Path.GetDirectoryName(source) ?? ".", "speed_listen");
            Directory.CreateDirectory(cacheDir);
            var cacheName = Path.GetFileNameWithoutExtension(source)
                            + "_at_"
                            + listen.ToString(CultureInfo.InvariantCulture)
                            + ".mp3";
            var cachePath = Path.Combine(cacheDir, cacheName);
            var sourceUtc = File.GetLastWriteTimeUtc(source);
            if (!File.Exists(cachePath) || File.GetLastWriteTimeUtc(cachePath) < sourceUtc)
            {
                var workDir = Path.Combine(cacheDir, "work");
                var adjusted = await ShowcaseNarrationAvSyncHelper.PrepareSpeedAdjustedMp3Async(
                    ffmpeg,
                    ffprobe,
                    source,
                    workDir,
                    effectivePercent,
                    cacheName,
                    log,
                    cancellationToken).ConfigureAwait(false);
                if (!string.Equals(adjusted, cachePath, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(adjusted))
                {
                    File.Copy(adjusted, cachePath, overwrite: true);
                }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = File.Exists(cachePath) ? cachePath : source,
                UseShellExecute = true
            });
        }

        /// <summary>Áp tốc độ mới lên file thoại (FFmpeg) — không gọi TTS lại; chỉ xóa mix cũ.</summary>
        public static async Task<bool> ReapplyQuoteVoiceSpeedAsync(
            string sessionBase,
            int quoteIndex,
            PhilosophyScriptItem quote,
            PhilosophyBatchItem batch,
            ShowcaseVideoItem video,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken = default)
        {
            if (quote == null || batch == null || video == null)
            {
                return false;
            }

            var voicePath = GetQuoteVoicePreviewPath(sessionBase, quoteIndex);
            if (string.IsNullOrWhiteSpace(voicePath) || !File.Exists(voicePath))
            {
                return false;
            }

            var targetSpeed = PhilosophyBatchHelper.ResolveQuoteNarrationSpeedPercent(quote, batch);
            var bakedSpeed = ReadBakedQuoteVoiceSpeedPercent(sessionBase, quoteIndex);
            if (Math.Abs(targetSpeed - bakedSpeed) <= 2)
            {
                return true;
            }

            FfmpegToolkitService.TryResolve(settings, out var toolkit, out _);
            var ffmpeg = toolkit?.FfmpegExe ?? FfmpegToolkitService.GetBundledFfmpegPath();
            var ffprobe = toolkit?.FfprobeExe ?? FfmpegToolkitService.GetBundledFfprobePath();
            if (string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(ffmpeg))
            {
                throw new InvalidOperationException("Không tìm thấy FFmpeg để chỉnh tốc độ thoại.");
            }

            var sourcePath = GetQuoteVoiceSourcePath(sessionBase, quoteIndex);
            var hasSource = File.Exists(sourcePath);
            var inputPath = hasSource ? sourcePath : voicePath;
            var applyPercent = hasSource
                ? targetSpeed
                : (int)Math.Round(targetSpeed * 100d / Math.Max(1, bakedSpeed));

            var workDir = Path.Combine(GetAudioDirectory(sessionBase), "speed_reapply_" + quoteIndex.ToString("D2", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(workDir);

            if (hasSource && Math.Abs(targetSpeed - 100) <= 2)
            {
                File.Copy(sourcePath, voicePath, overwrite: true);
            }
            else if (Math.Abs(applyPercent - 100) <= 2 && !hasSource)
            {
                // Already at natural speed in file — nothing to do.
            }
            else
            {
                var adjusted = await ShowcaseNarrationAvSyncHelper.PrepareSpeedAdjustedMp3Async(
                    ffmpeg,
                    ffprobe,
                    inputPath,
                    workDir,
                    applyPercent,
                    "voice_respeed.mp3",
                    log,
                    cancellationToken).ConfigureAwait(false);
                if (!string.Equals(adjusted, voicePath, StringComparison.OrdinalIgnoreCase) && File.Exists(adjusted))
                {
                    File.Copy(adjusted, voicePath, overwrite: true);
                }
            }

            WriteQuoteVoiceStamp(sessionBase, quoteIndex, quote, batch, video, settings);
            DeleteQuoteMixPreviewFiles(sessionBase, quoteIndex);
            log?.Invoke("[Quote] Tốc độ thoại dòng "
                         + (quoteIndex + 1).ToString(CultureInfo.InvariantCulture)
                         + " → "
                         + targetSpeed.ToString(CultureInfo.InvariantCulture)
                         + "% (FFmpeg, không TTS lại).");
            return true;
        }

        public static async Task ReapplyQuoteVoiceSpeedBatchAsync(
            PhilosophyBatchItem batch,
            ShowcaseVideoItem video,
            AppSettings settings,
            IEnumerable<int> quoteIndices,
            Action<string> log,
            CancellationToken cancellationToken = default)
        {
            if (batch?.Quotes == null || video == null)
            {
                return;
            }

            var sessionBase = GetSessionBase(batch, settings);
            foreach (var i in quoteIndices ?? Enumerable.Empty<int>())
            {
                if (i < 0 || i >= batch.Quotes.Count)
                {
                    continue;
                }

                var quote = batch.Quotes[i];
                if (quote == null || string.IsNullOrWhiteSpace(quote.Content))
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!HasQuoteVoicePreview(sessionBase, i))
                {
                    continue;
                }

                await ReapplyQuoteVoiceSpeedAsync(
                    sessionBase,
                    i,
                    quote,
                    batch,
                    video,
                    settings,
                    log,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public static bool IsQuoteMixCurrent(
            string sessionBase,
            int quoteIndex,
            PhilosophyScriptItem quote,
            PhilosophyBatchItem batch,
            ShowcaseVideoItem video,
            AppSettings settings)
        {
            if (!IsQuoteVoiceCurrent(sessionBase, quoteIndex, quote, batch, video, settings))
            {
                return false;
            }

            if (!HasQuoteFullMixPreview(sessionBase, quoteIndex))
            {
                return false;
            }

            var stampPath = GetQuoteMixStampPath(sessionBase, quoteIndex);
            if (!File.Exists(stampPath))
            {
                return false;
            }

            try
            {
                var saved = (File.ReadAllText(stampPath) ?? string.Empty).Trim();
                return string.Equals(saved, ComputeQuoteMixStamp(quote, batch, video, settings), StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        public static void DeleteQuoteMixPreviewFiles(string sessionBase, int quoteIndex)
        {
            if (string.IsNullOrWhiteSpace(sessionBase) || quoteIndex < 0)
            {
                return;
            }

            TryDeleteFile(GetQuoteFullMixPreviewPath(sessionBase, quoteIndex));
            TryDeleteFile(GetQuoteMixStampPath(sessionBase, quoteIndex));
        }

        public static void DeleteQuotePreviewFiles(string sessionBase, int quoteIndex)
        {
            if (string.IsNullOrWhiteSpace(sessionBase) || quoteIndex < 0)
            {
                return;
            }

            TryDeleteFile(GetQuoteVoicePreviewPath(sessionBase, quoteIndex));
            TryDeleteFile(GetQuoteVoiceSourcePath(sessionBase, quoteIndex));
            TryDeleteFile(GetQuoteFullMixPreviewPath(sessionBase, quoteIndex));
            TryDeleteFile(GetQuoteVoiceStampPath(sessionBase, quoteIndex));
            TryDeleteFile(GetQuoteMixStampPath(sessionBase, quoteIndex));
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
            PhilosophyBatchTtsHelper.SyncBodyVoiceToHookTrack(video);
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
                    video,
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
            ShowcaseVideoItem video,
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
            WriteQuoteVoiceStamp(sessionBase, quoteIndex, quote, batch, video, settings);
        }

        public static async Task RenderFullMixPreviewAsync(
            PhilosophyBatchItem batch,
            AppSettings settings,
            string profileName,
            Action<string> log,
            CancellationToken cancellationToken,
            IReadOnlyList<int> quoteIndices = null,
            ShowcaseVideoItem video = null,
            Action<string> progress = null)
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
            var skipped = 0;
            var total = indices.Count;

            log?.Invoke("[Quote] Render thành phẩm cho "
                         + total.ToString(CultureInfo.InvariantCulture)
                         + " câu (dòng "
                         + string.Join(", ", indices.Select(i => (i + 1).ToString(CultureInfo.InvariantCulture)))
                         + ")…");
            progress?.Invoke("Render audio 0/" + total + "…");

            for (var pass = 0; pass < indices.Count; pass++)
            {
                var i = indices[pass];
                var quote = batch.Quotes[i];
                var voiceMp3 = GetQuoteVoicePreviewPath(sessionBase, i);
                if (quote == null || string.IsNullOrWhiteSpace(voiceMp3) || !File.Exists(voiceMp3))
                {
                    continue;
                }

                if (video != null
                    && IsQuoteMixCurrent(sessionBase, i, quote, batch, video, settings))
                {
                    skipped++;
                    log?.Invoke("[Quote] Bỏ qua câu " + (i + 1).ToString(CultureInfo.InvariantCulture) + " — mix đã đúng.");
                    progress?.Invoke("Render audio " + (pass + 1) + "/" + total + " (bỏ qua câu " + (i + 1) + ")…");
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Invoke("Render audio " + (pass + 1) + "/" + total + " · câu " + (i + 1) + "…");

                var musicVol = PhilosophyBatchHelper.ResolveMusicBedLinearVolume(
                    PhilosophyBatchHelper.ResolveQuoteMusicVolumePercent(quote, batch));
                var ambientVol = PhilosophyBatchHelper.ResolveAmbientBedLinearVolume();
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

                var outputMp3 = GetQuoteFullMixPreviewPath(sessionBase, i);
                var voiceSeconds = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                    ffprobe,
                    voiceMp3,
                    cancellationToken).ConfigureAwait(false);
                if (voiceSeconds <= 0.05d)
                {
                    voiceSeconds = 8d;
                }

                // Preview: hết thoại → nhạc/đệm thêm 3s rồi cắt.
                var outputSeconds = PhilosophyRenderOptions.ResolveAudioMixOutputDuration(voiceSeconds);
                log?.Invoke("[Quote] Mix câu "
                             + (i + 1).ToString(CultureInfo.InvariantCulture)
                             + " · "
                             + voiceSeconds.ToString("0.#", CultureInfo.InvariantCulture)
                             + "s → "
                             + outputSeconds.ToString("0.#", CultureInfo.InvariantCulture)
                             + "s…");

                await BuildVoicePlusMusicMp3Async(
                    ffmpeg,
                    voiceMp3,
                    musicPath,
                    ambientPath,
                    voiceSeconds,
                    outputSeconds,
                    outputMp3,
                    musicVol,
                    ambientVol,
                    log,
                    cancellationToken).ConfigureAwait(false);

                if (video != null)
                {
                    WriteQuoteMixStamp(sessionBase, i, quote, batch, video, settings);
                }

                rendered++;
                log?.Invoke("[Quote] Audio thành phẩm câu " + (i + 1).ToString(CultureInfo.InvariantCulture) + " → " + outputMp3);
            }

            if (rendered == 0 && skipped == 0)
            {
                throw new InvalidOperationException("Chưa có audio thoại cho dòng đã chọn — bấm «Tạo audio quote» trước.");
            }

            if (rendered == 0 && skipped > 0)
            {
                log?.Invoke("[Quote] Tất cả mix đã đúng — không cần render lại.");
                progress?.Invoke("Mix đã đúng — không render lại.");
            }
            else
            {
                progress?.Invoke("Render audio xong (" + rendered + " câu).");
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

        private static async Task BuildVoicePlusMusicMp3Async(
            string ffmpeg,
            string voiceMp3,
            string musicMp3,
            string ambientPath,
            double voiceSourceSeconds,
            double outputSeconds,
            string outputMp3,
            double musicBedVolume,
            double ambientBedVolume,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var voiceStr = voiceSourceSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var outStr = outputSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var musicVol = musicBedVolume.ToString("0.###", CultureInfo.InvariantCulture);
            var ambientVol = ambientBedVolume.ToString("0.###", CultureInfo.InvariantCulture);
            var voiceGain = VoiceMixGain.ToString("0.###", CultureInfo.InvariantCulture);
            var hasMusic = !string.IsNullOrWhiteSpace(musicMp3) && File.Exists(musicMp3);
            var hasAmbient = !string.IsNullOrWhiteSpace(ambientPath) && File.Exists(ambientPath);
            var padVoice = outputSeconds > voiceSourceSeconds + 0.05d;
            var voiceFilter = padVoice
                ? "[0:a]atrim=0:" + voiceStr + ",asetpts=PTS-STARTPTS,aresample=48000,apad=whole_dur=" + outStr + ",volume=" + voiceGain + "[vo]"
                : "[0:a]atrim=0:" + voiceStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + voiceGain + "[vo]";

            if (!hasMusic && !hasAmbient)
            {
                if (Math.Abs(outputSeconds - voiceSourceSeconds) <= 0.05d)
                {
                    File.Copy(voiceMp3, outputMp3, overwrite: true);
                    return;
                }

                var padArgs = "-y -i \"" + voiceMp3 + "\" -filter:a \"apad=whole_dur=" + outStr + "\" -t " + outStr +
                              " -c:a libmp3lame -q:a 2 \"" + outputMp3 + "\"";
                await RunFfmpegAsync(ffmpeg, padArgs, log, cancellationToken).ConfigureAwait(false);
                return;
            }

            string filter;
            string inputs;
            if (hasMusic && hasAmbient)
            {
                inputs = "-y -i \"" + voiceMp3 + "\" -i \"" + musicMp3 + "\" -i \"" + ambientPath + "\"";
                filter =
                    voiceFilter + ";" +
                    "[1:a]atrim=0:" + outStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + musicVol + "[bg];" +
                    "[2:a]atrim=0:" + outStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + ambientVol + "[amb];" +
                    "[vo][bg][amb]amix=inputs=3:duration=longest:dropout_transition=2:normalize=0[aout]";
            }
            else if (hasMusic)
            {
                inputs = "-y -i \"" + voiceMp3 + "\" -i \"" + musicMp3 + "\"";
                filter =
                    voiceFilter + ";" +
                    "[1:a]atrim=0:" + outStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + musicVol + "[bg];" +
                    "[vo][bg]amix=inputs=2:duration=longest:dropout_transition=2:normalize=0[aout]";
            }
            else
            {
                inputs = "-y -i \"" + voiceMp3 + "\" -i \"" + ambientPath + "\"";
                filter =
                    voiceFilter + ";" +
                    "[1:a]atrim=0:" + outStr + ",asetpts=PTS-STARTPTS,aresample=48000,volume=" + ambientVol + "[amb];" +
                    "[vo][amb]amix=inputs=2:duration=longest:dropout_transition=2:normalize=0[aout]";
            }

            var args = inputs + " -filter_complex \"" + filter + "\" -map \"[aout]\" -t " + outStr +
                       " -c:a libmp3lame -q:a 2 \"" + outputMp3 + "\"";
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
            double ambientBedVolume,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var voiceStr = voiceSourceSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var outStr = outputSeconds.ToString("0.#####", CultureInfo.InvariantCulture);
            var musicVol = musicBedVolume.ToString("0.###", CultureInfo.InvariantCulture);
            var ambientVol = ambientBedVolume.ToString("0.###", CultureInfo.InvariantCulture);
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

                    var errTask = Task.Run(() => proc.StandardError.ReadToEnd());
                    if (!proc.WaitForExit(120000))
                    {
                        try
                        {
                            proc.Kill();
                        }
                        catch
                        {
                            // ignored
                        }

                        throw new InvalidOperationException("FFmpeg quá lâu (>120s) — có thể bị treo.");
                    }

                    var err = errTask.GetAwaiter().GetResult();
                    if (proc.ExitCode != 0)
                    {
                        log?.Invoke("[Quote] FFmpeg lỗi: " + err);
                        throw new InvalidOperationException("FFmpeg thất bại (mã " + proc.ExitCode + ").");
                    }
                }
            }, cancellationToken);
        }
    }
}
