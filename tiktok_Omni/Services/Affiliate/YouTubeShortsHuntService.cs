using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Affiliate
{
    /// <summary>Quét YouTube Shorts qua yt-dlp (ytsearch).</summary>
    public sealed class YouTubeShortsHuntService
    {
        public async Task<List<AffiliateCandidate>> HuntAsync(
            string keyword,
            int limit,
            string ytDlpPath,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                throw new ArgumentException("Keyword is required.", nameof(keyword));
            }

            limit = Math.Max(1, Math.Min(limit, 50));
            if (string.IsNullOrWhiteSpace(ytDlpPath) || !File.Exists(ytDlpPath))
            {
                throw new InvalidOperationException("yt-dlp.exe không hợp lệ — cần cho săn YouTube Shorts.");
            }

            var searchUrl = $"ytsearch{limit}:{keyword.Trim()} #shorts";
            logAction?.Invoke($"[YouTube] yt-dlp tìm kiếm: {searchUrl}");

            var args =
                "--skip-download --no-warnings --ignore-errors --no-playlist --flat-playlist " +
                "--print \"%(webpage_url)s\\t%(title)s\\t%(uploader)s\\t%(view_count)s\\t%(duration)s\" " +
                $"\"{searchUrl}\"";

            var stdout = await RunYtDlpAsync(ytDlpPath, args, cancellationToken).ConfigureAwait(false);
            var results = new List<AffiliateCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var safety = new SafetyScoreService();

            foreach (var line in stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parts = line.Split('\t');
                if (parts.Length < 2)
                {
                    continue;
                }

                var videoUrl = (parts[0] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(videoUrl) ||
                    (!videoUrl.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) &&
                     !videoUrl.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (!seen.Add(videoUrl))
                {
                    continue;
                }

                var title = parts.Length > 1 ? (parts[1] ?? string.Empty).Trim() : string.Empty;
                var uploader = parts.Length > 2 ? (parts[2] ?? string.Empty).Trim() : string.Empty;
                long.TryParse(parts.Length > 3 ? parts[3] : "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out var views);
                int.TryParse(parts.Length > 4 ? parts[4] : "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out var durationSec);

                var candidate = new AffiliateCandidate
                {
                    SourceKeyword = keyword.Trim(),
                    SourcePlatform = AffiliateSourceIds.YouTube,
                    ProductName = string.IsNullOrWhiteSpace(title) ? "YouTube Short" : title,
                    Creator = string.IsNullOrWhiteSpace(uploader) ? string.Empty : uploader,
                    VideoUrl = videoUrl,
                    ProfileUrl = string.IsNullOrWhiteSpace(uploader)
                        ? string.Empty
                        : "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(uploader),
                    PlayCount = views,
                    DurationSeconds = durationSec,
                    LinkedProduct = "YouTube Shorts"
                };

                var safetyResult = safety.ScoreAffiliateCandidate(candidate);
                candidate.SafetyScore = safetyResult.Score;
                candidate.SafetyRiskSummary = safetyResult.Reasons.Count == 0
                    ? "Low risk"
                    : string.Join("; ", safetyResult.Reasons.Take(3));

                results.Add(candidate);
                logAction?.Invoke($"[YouTube] {results.Count}/{limit}: {candidate.ProductName} | views {views}");

                if (results.Count >= limit)
                {
                    break;
                }
            }

            if (results.Count == 0)
            {
                logAction?.Invoke($"[YouTube] Không có Shorts nào cho «{keyword}». Kiểm tra yt-dlp hoặc thử từ khoá khác.");
            }

            return results;
        }

        private static async Task<string> RunYtDlpAsync(string ytDlpPath, string arguments, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await ProcessCancellationHelper.WaitUntilExitAsync(process, cancellationToken).ConfigureAwait(false);
                var stdout = await stdoutTask.ConfigureAwait(false);
                var stderr = await stderrTask.ConfigureAwait(false);

                if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
                {
                    throw new InvalidOperationException(
                        "yt-dlp thoát với mã " + process.ExitCode + (string.IsNullOrWhiteSpace(stderr) ? string.Empty : ": " + stderr.Trim()));
                }

                return stdout ?? string.Empty;
            }
        }
    }
}
