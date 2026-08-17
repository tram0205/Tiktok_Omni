using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Showcase
{
    internal static class ShowcaseClipDurationHelper
    {
        /// <summary>Sai lệch tối đa mỗi cảnh (giây) — ffprobe / re-encode Zoom thường lệch &lt;1s.</summary>
        public const double DefaultToleranceSeconds = 1d;

        public static async Task<string> BuildSignatureAsync(
            IList<AiVideoGenInputItem> scenes,
            string ffprobeExecutable,
            CancellationToken cancellationToken)
        {
            if (scenes == null || scenes.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            for (var i = 0; i < scenes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = (scenes[i]?.ClipPath ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    continue;
                }

                var seconds = await ShowcaseMediaProbeHelper.ProbeDurationSecondsAsync(
                        ffprobeExecutable,
                        path,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (seconds <= 0.01d)
                {
                    continue;
                }

                parts.Add((i + 1).ToString(CultureInfo.InvariantCulture) + ":"
                    + seconds.ToString("0.###", CultureInfo.InvariantCulture));
            }

            return string.Join(";", parts);
        }

        public static bool SignaturesMatch(
            string baselineSignature,
            string currentSignature,
            double toleranceSeconds = DefaultToleranceSeconds)
        {
            var baseline = ParseSignature(baselineSignature);
            if (baseline.Count == 0)
            {
                return true;
            }

            var current = ParseSignature(currentSignature);
            if (current.Count == 0)
            {
                return false;
            }

            // Chỉ so các cảnh đã có trong baseline — thêm/bớt clip khác cảnh không chặn nếu cảnh cũ vẫn ~cùng độ dài.
            foreach (var entry in baseline)
            {
                if (!current.TryGetValue(entry.Key, out var currentSeconds))
                {
                    return false;
                }

                if (Math.Abs(entry.Value - currentSeconds) > toleranceSeconds)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Mô tả lệch thời lượng từng cảnh (baseline → hiện tại). Rỗng nếu không lệch đáng kể.</summary>
        public static string BuildDriftSummary(string baselineSignature, string currentSignature)
        {
            var baseline = ParseSignature(baselineSignature);
            if (baseline.Count == 0)
            {
                return string.Empty;
            }

            var current = ParseSignature(currentSignature);
            var lines = new List<string>();
            foreach (var entry in baseline.OrderBy(pair => pair.Key))
            {
                if (!current.TryGetValue(entry.Key, out var currentSeconds))
                {
                    lines.Add("Cảnh " + entry.Key + ": thiếu clip (trước "
                        + entry.Value.ToString("0.#", CultureInfo.InvariantCulture) + "s)");
                    continue;
                }

                var delta = currentSeconds - entry.Value;
                if (Math.Abs(delta) < 0.05d)
                {
                    continue;
                }

                var sign = delta > 0 ? "+" : string.Empty;
                lines.Add("Cảnh " + entry.Key + ": "
                    + sign + delta.ToString("0.#", CultureInfo.InvariantCulture) + "s ("
                    + entry.Value.ToString("0.#", CultureInfo.InvariantCulture) + "s → "
                    + currentSeconds.ToString("0.#", CultureInfo.InvariantCulture) + "s)");
            }

            foreach (var sceneOrder in current.Keys.Where(key => !baseline.ContainsKey(key)).OrderBy(key => key))
            {
                lines.Add("Cảnh " + sceneOrder + ": clip mới ("
                    + current[sceneOrder].ToString("0.#", CultureInfo.InvariantCulture) + "s)");
            }

            return lines.Count == 0 ? string.Empty : string.Join("\r\n", lines);
        }

        private static Dictionary<int, double> ParseSignature(string signature)
        {
            var map = new Dictionary<int, double>();
            var text = (signature ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return map;
            }

            foreach (var part in text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                var colon = trimmed.IndexOf(':');
                if (colon <= 0 || colon >= trimmed.Length - 1)
                {
                    continue;
                }

                if (!int.TryParse(trimmed.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sceneOrder))
                {
                    continue;
                }

                if (!double.TryParse(trimmed.Substring(colon + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    continue;
                }

                map[sceneOrder] = seconds;
            }

            return map;
        }
    }
}
