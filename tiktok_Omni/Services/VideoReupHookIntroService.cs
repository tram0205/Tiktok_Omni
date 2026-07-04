using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Hook intro AI Video reup: Gemini sinh ảnh (mascot + hook) → clip video karaoke riêng → concat với body.
    /// </summary>
    public sealed class VideoReupHookIntroService
    {
        public const string HookSceneImageFileName = "hook_scene.png";
        public const string HookSceneKeyFileName = "hook_scene.key";
        public const string HookIntroVideoFileName = "hook_intro.mp4";
        public const string BodyVideoNoAudioFileName = "body_noaudio.mp4";
        public const string CombinedVideoNoAudioFileName = "combined_noaudio.mp4";
        private const string HookIntroAssFileName = "hook_intro.ass";

        private const int OutputWidth = 1080;
        private const int OutputHeight = 1920;
        private const int OutputFps = 30;

        /// <summary>Sinh ảnh hook (Gemini) + clip intro (ảnh + phụ đề karaoke, không audio).</summary>
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

            if (hookDurationSec < 0.5d)
            {
                throw new InvalidOperationException("Thời lượng hook quá ngắn — không tạo clip intro.");
            }

            var hookText = (row.ReupHookDraft ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(hookText))
            {
                hookText = (row.Transcript ?? string.Empty).Trim();
            }

            var sceneImagePath = Path.Combine(stageFolder, HookSceneImageFileName);
            var sceneKeyPath = Path.Combine(stageFolder, HookSceneKeyFileName);
            var introVideoPath = Path.Combine(stageFolder, HookIntroVideoFileName);
            var cacheKey = BuildSceneCacheKey(hookText, mascotPath, hookDurationSec);

            var canReuseIntro = File.Exists(introVideoPath)
                && File.Exists(sceneImagePath)
                && File.Exists(sceneKeyPath)
                && string.Equals(File.ReadAllText(sceneKeyPath, TextFileEncoding.Utf8NoBom).Trim(), cacheKey, StringComparison.Ordinal);

            if (canReuseIntro)
            {
                log?.Invoke("[VideoReup] Hook intro: dùng cache " + HookIntroVideoFileName + ".");
                row.ReupHookIntroImagePath = sceneImagePath;
                row.ReupHookIntroVideoPath = introVideoPath;
                row.ReupHookIntroDurationSec = hookDurationSec;
                return;
            }

            log?.Invoke("[VideoReup] Hook intro: Gemini sinh ảnh (mascot + hook)…");
            await gemini.GenerateHookSceneImageAsync(
                hookText,
                mascotPath,
                apiKey,
                sceneImagePath,
                log,
                cancellationToken).ConfigureAwait(false);

            File.WriteAllText(sceneKeyPath, cacheKey, TextFileEncoding.Utf8NoBom);
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
                var baseScale =
                    "scale=" + OutputWidth + ":" + OutputHeight + ":force_original_aspect_ratio=decrease," +
                    "pad=" + OutputWidth + ":" + OutputHeight + ":(ow-iw)/2:(oh-ih)/2:color=black," +
                    "setsar=1,fps=" + OutputFps;

                string vf;
                if (karaoke != null && !string.IsNullOrWhiteSpace(karaoke.VideoFilterFragment))
                {
                    vf = baseScale + "," + karaoke.VideoFilterFragment;
                    log?.Invoke("[VideoReup] Hook intro: burn phụ đề karaoke lên ảnh AI…");
                }
                else
                {
                    vf = baseScale;
                    log?.Invoke("[VideoReup] Hook intro: không có phụ đề (thiếu text hoặc ASS) — chỉ ảnh AI.");
                }

                var metaArgs = RemixScramblerService.BuildFakeMetadataArgs();
                var encodeArgs =
                    "-y -loop 1 -i \"" + sceneImagePath + "\"" +
                    " -t " + durStr +
                    " -vf \"" + vf + "\"" +
                    " -an -c:v libx64 -preset medium -crf 20 -pix_fmt yuv420p -r " + OutputFps +
                    metaArgs +
                    " \"" + introVideoPath + "\"";

                log?.Invoke("[VideoReup] Hook intro: FFmpeg ảnh → " + HookIntroVideoFileName + " (" + durStr + "s)…");
                await VideoReupRemixService.RunFfmpegPublicAsync(ffmpegExe, encodeArgs, log, cancellationToken)
                    .ConfigureAwait(false);

                if (!File.Exists(introVideoPath))
                {
                    throw new InvalidOperationException("Không tạo được clip hook intro.");
                }
            }
            finally
            {
                KaraokeAssSubtitleService.SafeDeleteAssFile(karaoke?.AssFilePath);
            }

            row.ReupHookIntroVideoPath = introVideoPath;
            row.ReupHookIntroDurationSec = hookDurationSec;
            log?.Invoke("[VideoReup] Hook intro: sẵn sàng " + Path.GetFileName(introVideoPath) + ".");
        }

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
            var filter =
                "[0:v]fps=" + OutputFps + ",setsar=1,scale=" + OutputWidth + ":" + OutputHeight + "[v0];" +
                "[1:v]fps=" + OutputFps + ",setsar=1,scale=" + OutputWidth + ":" + OutputHeight + "[v1];" +
                "[v0][v1]concat=n=2:v=1:a=0[vout]";

            var args =
                "-y -i \"" + introVideoPath + "\" -i \"" + bodyVideoPath + "\"" +
                " -filter_complex \"" + filter + "\"" +
                " -map \"[vout]\" -an -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p -r " + OutputFps +
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

        private static string BuildSceneCacheKey(string hookText, string mascotPath, double hookDurationSec)
        {
            var payload = (hookText ?? string.Empty).Trim() + "|" +
                          (mascotPath ?? string.Empty).Trim().ToLowerInvariant() + "|" +
                          hookDurationSec.ToString("0.###", CultureInfo.InvariantCulture);
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }
    }
}
