using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Hook intro Video reup: render clip hook_intro.mp4 từ video stock (ưu tiên) hoặc ảnh Gemini.
    /// Cả hai đường đều burn-in phụ đề karaoke từ hook text + hook WAV.
    /// </summary>
    public sealed class VideoReupHookIntroService
    {
        public const string HookSceneImageFileName   = "hook_scene.png";
        public const string HookSceneKeyFileName     = "hook_scene.key";
        public const string HookIntroVideoFileName   = "hook_intro.mp4";
        public const string BodyVideoNoAudioFileName = "body_noaudio.mp4";
        public const string CombinedVideoNoAudioFileName = "combined_noaudio.mp4";
        private const string HookIntroAssFileName    = "hook_intro.ass";

        private const int OutputWidth  = 1080;
        private const int OutputHeight = 1920;
        private const int OutputFps    = 30;

        // ─────────────────────────────────────────────────────────────────────
        //  Public entry point
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tạo hook_intro.mp4:
        /// 1) Nếu catalog stock có clip phù hợp với <see cref="VideoReupRowItem.HookStyleKey"/> → dùng stock clip + karaoke.
        /// 2) Ngược lại → sinh ảnh Gemini + karaoke (hành vi cũ).
        /// </summary>
        public async Task PrepareHookIntroVideoAsync(
            VideoReupRowItem row,
            AppSettings settings,
            GeminiService gemini,
            string ffmpegExe,
            string hookNormWav,
            double hookDurationSec,
            string stageFolder,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            if (string.IsNullOrWhiteSpace(stageFolder))
            {
                throw new InvalidOperationException("Stage folder trống — không render hook intro.");
            }

            Directory.CreateDirectory(stageFolder);

            if (hookDurationSec < 0.5d)
            {
                throw new InvalidOperationException("Thời lượng hook quá ngắn — không tạo clip intro.");
            }

            // Chọn clip stock (ưu tiên clip riêng của profile, fallback về clip chung)
            if (HookStyleCatalog.TryPickClip(row.HookStyleKey, settings, row.HookStockClipPath, row.ProfileName, out var stockClip))
            {
                log?.Invoke($"[VideoReup] Hook intro: dùng stock clip «{Path.GetFileName(stockClip)}» (style={row.HookStyleKey}).");
                row.HookStockClipPath = stockClip;
                await PrepareStockHookIntroVideoAsync(
                    row, settings, ffmpegExe, hookNormWav, hookDurationSec, stageFolder, stockClip, log, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            // Fallback: Gemini sinh ảnh
            await PrepareGeminiHookIntroVideoAsync(
                row, settings, gemini, ffmpegExe, hookNormWav, hookDurationSec, stageFolder, log, cancellationToken)
                .ConfigureAwait(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Stock clip path
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Render hook_intro.mp4 từ video stock clip + karaoke burn-in.</summary>
        private async Task PrepareStockHookIntroVideoAsync(
            VideoReupRowItem row,
            AppSettings settings,
            string ffmpegExe,
            string hookNormWav,
            double hookDurationSec,
            string stageFolder,
            string stockClipPath,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var introVideoPath = Path.Combine(stageFolder, HookIntroVideoFileName);
            var cacheKeyPath   = Path.Combine(stageFolder, HookSceneKeyFileName);

            var hookText = ResolveHookText(row);
            var videoCacheKey = ReupStageCacheHelper.BuildStockHookIntroVideoCacheKey(
                hookText, stockClipPath, hookDurationSec, row, settings);

            if (File.Exists(introVideoPath) && File.Exists(cacheKeyPath)
                && string.Equals(
                    File.ReadAllText(cacheKeyPath, TextFileEncoding.Utf8NoBom).Trim(),
                    videoCacheKey, StringComparison.Ordinal))
            {
                log?.Invoke("[VideoReup] Hook intro (stock): dùng cache " + HookIntroVideoFileName + ".");
                row.ReupHookIntroImagePath = string.Empty;
                row.ReupHookIntroVideoPath = introVideoPath;
                row.ReupHookIntroDurationSec = hookDurationSec;
                return;
            }

            KaraokeAssSubtitleService.KaraokeAssBurnInResult karaoke = null;
            try
            {
                var assOptions = ReupSubtitleStyleHelper.BuildOptions(row, settings);
                karaoke = await KaraokeAssSubtitleService.TryCreateBurnInAsync(
                    ffmpegExe,
                    hookText,
                    hookNormWav,
                    stageFolder,
                    log,
                    cancellationToken,
                    assOptions: assOptions,
                    assFileName: HookIntroAssFileName,
                    knownAudioDurationSeconds: hookDurationSec).ConfigureAwait(false);

                var durStr = hookDurationSec.ToString("0.#####", CultureInfo.InvariantCulture);
                var subtitleFragment = karaoke?.VideoFilterFragment;
                var vf = BuildHookIntroVideoFilter(subtitleFragment);
                if (!string.IsNullOrWhiteSpace(subtitleFragment))
                {
                    log?.Invoke("[VideoReup] Hook intro (stock): burn phụ đề karaoke lên clip…");
                }
                else
                {
                    log?.Invoke("[VideoReup] Hook intro (stock): không có phụ đề — chỉ clip stock.");
                }

                var metaArgs   = RemixScramblerService.BuildFakeMetadataArgs();
                var encodeArgs =
                    "-y -i \"" + stockClipPath + "\"" +
                    " -t " + durStr +
                    " -vf \"" + vf + "\"" +
                    " -an -c:v libx264 -preset slow -crf 17 -profile:v high -pix_fmt yuv420p -r " + OutputFps +
                    metaArgs +
                    " \"" + introVideoPath + "\"";

                log?.Invoke("[VideoReup] Hook intro (stock): FFmpeg clip → " + HookIntroVideoFileName + " (" + durStr + "s)…");
                await VideoReupRemixService.RunFfmpegPublicAsync(ffmpegExe, encodeArgs, log, cancellationToken)
                    .ConfigureAwait(false);

                if (!File.Exists(introVideoPath))
                {
                    throw new InvalidOperationException("Không tạo được clip hook intro từ stock.");
                }

                File.WriteAllText(cacheKeyPath, videoCacheKey, TextFileEncoding.Utf8NoBom);
                ReupStageCacheHelper.WriteCacheKey(
                    stageFolder,
                    ReupStageCacheHelper.HookSceneImageKeyFile,
                    ReupStageCacheHelper.BuildStockClipCacheKey(hookText, stockClipPath));
            }
            finally
            {
                KaraokeAssSubtitleService.SafeDeleteAssFile(karaoke?.AssFilePath);
            }

            row.ReupHookIntroImagePath  = string.Empty;
            row.ReupHookIntroVideoPath  = introVideoPath;
            row.ReupHookIntroDurationSec = hookDurationSec;
            log?.Invoke("[VideoReup] Hook intro (stock): sẵn sàng " + Path.GetFileName(introVideoPath) + ".");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Gemini / ảnh AI path (hành vi gốc)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Sinh ảnh hook (Gemini) + clip intro (ảnh + phụ đề karaoke, không audio).</summary>
        private async Task PrepareGeminiHookIntroVideoAsync(
            VideoReupRowItem row,
            AppSettings settings,
            GeminiService gemini,
            string ffmpegExe,
            string hookNormWav,
            double hookDurationSec,
            string stageFolder,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (!VideoReupRemixService.TryValidateHookIntroMascotStep(row, out var mascotErr))
            {
                throw new InvalidOperationException(mascotErr);
            }

            if (!AvatarIdentityPackStore.TryGetMascotImagePath(row.ProfileName, out var mascotPath, out mascotErr))
            {
                throw new InvalidOperationException(mascotErr);
            }

            var apiKey = (settings?.AiApiKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("Cần AI API Key (Gemini) để sinh ảnh hook intro.");
            }

            var hookText       = ResolveHookText(row);
            var sceneImagePath = Path.Combine(stageFolder, HookSceneImageFileName);
            var sceneKeyPath   = Path.Combine(stageFolder, HookSceneKeyFileName);
            var introVideoPath = Path.Combine(stageFolder, HookIntroVideoFileName);
            var imageCacheKey  = ReupStageCacheHelper.BuildHookSceneImageCacheKey(hookText, mascotPath);
            var videoCacheKey  = ReupStageCacheHelper.BuildGeminiHookIntroVideoCacheKey(
                hookText, mascotPath, hookDurationSec, row, settings);

            var canReuseIntro =
                File.Exists(introVideoPath)
                && File.Exists(sceneImagePath)
                && File.Exists(sceneKeyPath)
                && string.Equals(
                    File.ReadAllText(sceneKeyPath, TextFileEncoding.Utf8NoBom).Trim(),
                    videoCacheKey, StringComparison.Ordinal);

            if (canReuseIntro)
            {
                log?.Invoke("[VideoReup] Hook intro (Gemini): dùng cache " + HookIntroVideoFileName + ".");
                row.ReupHookIntroImagePath  = sceneImagePath;
                row.ReupHookIntroVideoPath  = introVideoPath;
                row.ReupHookIntroDurationSec = hookDurationSec;
                return;
            }

            var hasCachedSceneImage = ReupStageCacheHelper.TryReuseCachedArtifacts(
                stageFolder,
                ReupStageCacheHelper.HookSceneImageKeyFile,
                imageCacheKey,
                sceneImagePath);

            if (!hasCachedSceneImage)
            {
                log?.Invoke("[VideoReup] Hook intro (Gemini): sinh ảnh (mascot + hook)…");
                try
                {
                    await gemini.GenerateHookSceneImageAsync(
                        hookText,
                        mascotPath,
                        apiKey,
                        sceneImagePath,
                        log,
                        cancellationToken).ConfigureAwait(false);
                    ReupStageCacheHelper.WriteCacheKey(stageFolder, ReupStageCacheHelper.HookSceneImageKeyFile, imageCacheKey);
                }
                catch (Exception ex) when (VideoReupRemixService.IsGeminiQuotaError(ex))
                {
                    if (File.Exists(sceneImagePath))
                    {
                        log?.Invoke("[VideoReup] Hook intro (Gemini): hết quota — dùng lại ảnh cache cũ.");
                    }
                    else if (File.Exists(introVideoPath))
                    {
                        log?.Invoke("[VideoReup] Hook intro (Gemini): hết quota — dùng lại " + HookIntroVideoFileName + " cũ.");
                        row.ReupHookIntroImagePath = string.Empty;
                        row.ReupHookIntroVideoPath = introVideoPath;
                        row.ReupHookIntroDurationSec = hookDurationSec;
                        return;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            VideoReupRemixService.FormatGeminiQuotaUserMessage(ex), ex);
                    }
                }
            }
            else
            {
                log?.Invoke("[VideoReup] Hook intro (Gemini): dùng lại ảnh cache — bỏ qua Gemini.");
            }

            row.ReupHookIntroImagePath = sceneImagePath;

            KaraokeAssSubtitleService.KaraokeAssBurnInResult karaoke = null;
            try
            {
                var assOptions = ReupSubtitleStyleHelper.BuildOptions(row, settings);
                karaoke = await KaraokeAssSubtitleService.TryCreateBurnInAsync(
                    ffmpegExe,
                    hookText,
                    hookNormWav,
                    stageFolder,
                    log,
                    cancellationToken,
                    assOptions: assOptions,
                    assFileName: HookIntroAssFileName,
                    knownAudioDurationSeconds: hookDurationSec).ConfigureAwait(false);

                var durStr = hookDurationSec.ToString("0.#####", CultureInfo.InvariantCulture);
                var subtitleFragment = karaoke?.VideoFilterFragment;
                var vf = BuildHookIntroVideoFilter(subtitleFragment);
                if (!string.IsNullOrWhiteSpace(subtitleFragment))
                {
                    log?.Invoke("[VideoReup] Hook intro (Gemini): burn phụ đề karaoke lên ảnh AI…");
                }
                else
                {
                    log?.Invoke("[VideoReup] Hook intro (Gemini): không có phụ đề — chỉ ảnh AI.");
                }

                var metaArgs   = RemixScramblerService.BuildFakeMetadataArgs();
                var encodeArgs =
                    "-y -loop 1 -i \"" + sceneImagePath + "\"" +
                    " -t " + durStr +
                    " -vf \"" + vf + "\"" +
                    " -an -c:v libx264 -preset slow -crf 17 -profile:v high -pix_fmt yuv420p -r " + OutputFps +
                    metaArgs +
                    " \"" + introVideoPath + "\"";

                log?.Invoke("[VideoReup] Hook intro (Gemini): FFmpeg ảnh → " + HookIntroVideoFileName + " (" + durStr + "s)…");
                await VideoReupRemixService.RunFfmpegPublicAsync(ffmpegExe, encodeArgs, log, cancellationToken)
                    .ConfigureAwait(false);

                if (!File.Exists(introVideoPath))
                {
                    throw new InvalidOperationException("Không tạo được clip hook intro.");
                }

                File.WriteAllText(sceneKeyPath, videoCacheKey, TextFileEncoding.Utf8NoBom);
            }
            finally
            {
                KaraokeAssSubtitleService.SafeDeleteAssFile(karaoke?.AssFilePath);
            }

            row.ReupHookIntroVideoPath  = introVideoPath;
            row.ReupHookIntroDurationSec = hookDurationSec;
            log?.Invoke("[VideoReup] Hook intro (Gemini): sẵn sàng " + Path.GetFileName(introVideoPath) + ".");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Concat helper
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Ghép hook intro + body (cùng 1080×1920, không audio) → combined_noaudio.mp4.</summary>
        public static async Task ConcatIntroAndBodyAsync(
            string ffmpegExe,
            string introVideoPath,
            string bodyVideoPath,
            string combinedOutputPath,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(introVideoPath) || !File.Exists(introVideoPath))
            {
                throw new FileNotFoundException("Thiếu clip hook intro.", introVideoPath);
            }

            if (string.IsNullOrWhiteSpace(bodyVideoPath) || !File.Exists(bodyVideoPath))
            {
                throw new FileNotFoundException("Thiếu clip body.", bodyVideoPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(combinedOutputPath) ?? ".");

            var metaArgs = RemixScramblerService.BuildFakeMetadataArgs();
            var normalize = BuildConcatNormalizeFilterChain();

            var filter =
                "[0:v]" + normalize + "[v0];" +
                "[1:v]" + normalize + "[v1];" +
                "[v0][v1]concat=n=2:v=1:a=0[vout]";

            var args =
                "-y -i \"" + introVideoPath + "\" -i \"" + bodyVideoPath + "\"" +
                " -filter_complex \"" + filter + "\"" +
                " -map \"[vout]\" -an -c:v libx264 -preset slow -crf 17 -profile:v high -pix_fmt yuv420p -r " + OutputFps +
                metaArgs +
                " \"" + combinedOutputPath + "\"";

            log?.Invoke("[VideoReup] FFmpeg: concat hook intro + body → " + Path.GetFileName(combinedOutputPath) + "…");
            await VideoReupRemixService.RunFfmpegPublicAsync(ffmpegExe, args, log, cancellationToken)
                .ConfigureAwait(false);

            if (!File.Exists(combinedOutputPath))
            {
                throw new InvalidOperationException("Concat hook intro + body thất bại.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Scale/pad 9:16; setsar=1 luôn ở cuối (sau subtitles nếu có).</summary>
        private static string BuildHookIntroVideoFilter(string subtitleFragment)
        {
            var vf = BuildPortraitScalePadFilter();
            if (!string.IsNullOrWhiteSpace(subtitleFragment))
            {
                vf += "," + subtitleFragment;
            }

            return vf + "," + BuildPortraitOutputTailFilter();
        }

        /// <summary>Chuẩn hóa từng nhánh trước concat — ép SAR/pix_fmt khớp nhau.</summary>
        private static string BuildConcatNormalizeFilterChain()
        {
            return BuildPortraitScalePadFilter() + "," + BuildPortraitOutputTailFilter();
        }

        private static string BuildPortraitScalePadFilter()
        {
            return
                "scale=" + OutputWidth + ":" + OutputHeight + ":force_original_aspect_ratio=decrease:flags=lanczos," +
                "pad=" + OutputWidth + ":" + OutputHeight + ":(ow-iw)/2:(oh-ih)/2:color=black";
        }

        private static string BuildPortraitOutputTailFilter()
        {
            return "fps=" + OutputFps + ",format=yuv420p,setsar=1";
        }

        private static string ResolveHookText(VideoReupRowItem row)
        {
            var text = (row.ReupHookDraft ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                text = (row.Transcript ?? string.Empty).Trim();
            }

            return text;
        }
    }
}
