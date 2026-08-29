using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using tiktok_Omni.Services.Jobs;

namespace tiktok_Omni.Services
{
    /// <summary>Revenue-Driven: tăng tần suất săn từ khóa CTR cao; gỡ job/link chết khỏi queue.</summary>
    public sealed class RevenueDrivenService
    {
        private const string MetricsFileName = "revenue_metrics.json";
        private const double HighCtrLikeRatePercent = 8.0d;

        private RevenueMetricsFile _data = new RevenueMetricsFile();

        public double GetHuntBufferMultiplier(string keyword)
        {
            var key = NormalizeKeyword(keyword);
            if (string.IsNullOrWhiteSpace(key) || _data.Keywords == null)
            {
                return 1.0d;
            }

            if (!_data.Keywords.TryGetValue(key, out var row))
            {
                return 1.0d;
            }

            if (row.AvgProxyCtrPercent >= HighCtrLikeRatePercent)
            {
                return 2.5d;
            }

            if (row.AvgProxyCtrPercent >= 5.0d)
            {
                return 1.75d;
            }

            return 1.0d;
        }

        public async Task RecordCandidateAsync(AffiliateCandidate candidate)
        {
            if (candidate == null || candidate.PlayCount <= 0)
            {
                return;
            }

            await LoadAsync().ConfigureAwait(false);
            var key = NormalizeKeyword(candidate.SourceKeyword);
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var ctr = candidate.LikeCount * 100.0 / Math.Max(1, candidate.PlayCount);
            if (!_data.Keywords.TryGetValue(key, out var row))
            {
                row = new KeywordRevenueRow { Keyword = key };
                _data.Keywords[key] = row;
            }

            row.SampleCount++;
            row.AvgProxyCtrPercent = ((row.AvgProxyCtrPercent * (row.SampleCount - 1)) + ctr) / row.SampleCount;
            row.LastVideoUrl = candidate.VideoUrl ?? string.Empty;
            row.UpdatedAtUtc = DateTime.UtcNow;
            await SaveAsync().ConfigureAwait(false);
        }

        public async Task<int> PruneDeadProductsFromQueueAsync(
            GlobalJobQueue queue,
            IEnumerable<AffiliateCandidate> candidates,
            Action<string> log)
        {
            if (queue == null)
            {
                return 0;
            }

            var deadUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in candidates ?? Array.Empty<AffiliateCandidate>())
            {
                if (c == null || string.IsNullOrWhiteSpace(c.VideoUrl))
                {
                    continue;
                }

                if (IsDeadProduct(c))
                {
                    deadUrls.Add(c.VideoUrl.Trim());
                }
            }

            if (deadUrls.Count == 0)
            {
                return 0;
            }

            var removed = queue.CancelWhere(job =>
            {
                if (job == null || job.Status != OmniJobStatus.Pending && job.Status != OmniJobStatus.RetryPending)
                {
                    return false;
                }

                var url = ExtractVideoUrlFromJob(job);
                return !string.IsNullOrWhiteSpace(url) && deadUrls.Contains(url);
            });

            if (removed > 0)
            {
                log?.Invoke("[Revenue] Đã gỡ " + removed + " job queue (sản phẩm/link chết).");
            }

            return removed;
        }

        public static bool IsDeadProduct(AffiliateCandidate c)
        {
            if (c == null)
            {
                return false;
            }

            var err = (c.LastDeepDiveError ?? string.Empty) + " " + (c.LastMetricsError ?? string.Empty);
            if (err.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("dead", StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("link", StringComparison.OrdinalIgnoreCase) >= 0 && err.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("không tồn tại", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return string.IsNullOrWhiteSpace(c.ImageUrl)
                   && string.IsNullOrWhiteSpace(c.VideoUrl)
                   && !string.IsNullOrWhiteSpace(c.LastDeepDiveError);
        }

        public static double ComputeProxyCtrPercent(AffiliateCandidate c)
        {
            if (c == null || c.PlayCount <= 0)
            {
                return 0;
            }

            return c.LikeCount * 100.0 / c.PlayCount;
        }

        private static string ExtractVideoUrlFromJob(OmniJob job)
        {
            try
            {
                var json = job.PayloadJson ?? "{}";
                if (json.IndexOf("VideoUrl", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var jo = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(json);
                    return (jo?["VideoUrl"] ?? jo?["Row"]?["VideoUrl"])?.ToString() ?? string.Empty;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string NormalizeKeyword(string keyword)
        {
            return (keyword ?? string.Empty).Trim().ToLowerInvariant();
        }

        public async Task LoadAsync()
        {
            var path = AppDataPaths.ResolveReadableJsonPath(MetricsFileName, out var migrateFromLegacy);
            if (!File.Exists(path))
            {
                _data = new RevenueMetricsFile();
                return;
            }

            try
            {
                var json = await Task.Run(() => File.ReadAllText(path, TextFileEncoding.Utf8)).ConfigureAwait(false);
                _data = JsonConvert.DeserializeObject<RevenueMetricsFile>(json) ?? new RevenueMetricsFile();
                if (migrateFromLegacy && (_data.Keywords?.Count ?? 0) > 0)
                {
                    await SaveAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                _data = new RevenueMetricsFile();
            }
        }

        private async Task SaveAsync()
        {
            var json = JsonConvert.SerializeObject(_data, Formatting.Indented);
            await Task.Run(() =>
            {
                AppDataPaths.WriteJson(MetricsFileName, json);
                AppDataPaths.TryDeleteLegacyJson(MetricsFileName);
            }).ConfigureAwait(false);
        }

        private sealed class RevenueMetricsFile
        {
            public Dictionary<string, KeywordRevenueRow> Keywords { get; set; } =
                new Dictionary<string, KeywordRevenueRow>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class KeywordRevenueRow
        {
            public string Keyword { get; set; } = string.Empty;
            public double AvgProxyCtrPercent { get; set; }
            public int SampleCount { get; set; }
            public string LastVideoUrl { get; set; } = string.Empty;
            public DateTime UpdatedAtUtc { get; set; }
        }
    }
}
