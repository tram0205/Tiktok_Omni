using System;

using System.Globalization;

using System.IO;

using System.Threading;

using System.Threading.Tasks;



namespace tiktok_Omni.Services.Showcase

{

    /// <summary>

    /// Chuẩn hoá clip quay tay thật (do người dùng chọn) về khung hình render —
    /// scale/crop theo canvas, giữ nguyên tốc độ + âm thanh gốc của clip.

    /// </summary>

    internal static class ShowcaseCtaBrollAppendHelper

    {

        public static async Task<string> NormalizeRealClipAsync(

            string sourcePath,

            string outputPath,

            ShowcaseOutputAspectPreset canvas,

            string ffmpegExecutable,

            Action<string> logAction,

            CancellationToken cancellationToken)

        {

            var source = (sourcePath ?? string.Empty).Trim();

            var output = (outputPath ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(source) || !File.Exists(source))

            {

                throw new FileNotFoundException("Không tìm thấy clip quay tay.", source);

            }



            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");

            var w = canvas?.Width > 0 ? canvas.Width : 1080;

            var h = canvas?.Height > 0 ? canvas.Height : 1920;

            var vf = ShowcaseOutputAspectPresets.FormatScaleIncrease(w, h) + "," +

                     ShowcaseOutputAspectPresets.FormatScaleCrop(w, h) + ",setsar=1";

            var hasAudio = await ShowcaseMediaProbeHelper.HasAudioStreamAsync(

                    null,

                    source,

                    cancellationToken)

                .ConfigureAwait(false);

            string args;

            if (hasAudio)

            {

                args = "-y -i \"" + source + "\" -vf \"" + vf + "\" -r 30 -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p " +

                       "-c:a aac -b:a 192k -ar 48000 -ac 2 \"" + output + "\"";

            }

            else

            {

                args = "-y -i \"" + source + "\" -vf \"" + vf + "\" -r 30 -an -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p \"" +

                       output + "\"";

            }



            logAction?.Invoke("[Showcase] Chuẩn hóa clip quay tay (giữ nguyên tốc độ + âm thanh gốc)…");

            await RunFfmpegAsync(ffmpegExecutable, args, logAction, cancellationToken).ConfigureAwait(false);

            return output;

        }



        private static Task RunFfmpegAsync(

            string ffmpegExecutable,

            string args,

            Action<string> logAction,

            CancellationToken cancellationToken) =>

            FfmpegProcessRunner.RunAsync(

                ffmpegExecutable,

                args,

                logAction,

                cancellationToken,

                logPrefix: "[Showcase B-roll]",

                timeoutSeconds: FfmpegProcessRunner.DefaultTimeoutSeconds);

    }

}


