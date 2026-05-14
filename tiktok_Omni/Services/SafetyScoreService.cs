using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;

namespace tiktok_Omni.Services
{
    public class SafetyScoreResult
    {
        public int Score { get; set; }
        public bool RequiresManualApproval { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
    }

    public class SafetyScoreService
    {
        private static readonly string[] RiskyTokens = new[]
        {
            "cam kết", "100%", "all in", "đảm bảo lãi", "hack", "crack", "đặt cọc", "chuyển khoản ngay"
        };
        private static readonly string[] BannedScriptKeywords = new[]
        {
            "lừa đảo", "lừa gạt", "đa cấp", "kiếm tiền nhanh", "đảm bảo lợi nhuận", "nạp tiền ngay", "chuyển khoản trước",
            "fake bill", "hack tài khoản", "vay nóng", "cờ bạc", "cá độ", "thuốc kích dục", "hàng cấm"
        };

        public SafetyScoreResult ScoreRender(string script, IList<AiVideoGenInputItem> items, string runningProfile)
        {
            return ScoreRender(script, string.Empty, items, runningProfile);
        }

        public SafetyScoreResult ScoreRender(string script, string hashtags, IList<AiVideoGenInputItem> items, string runningProfile)
        {
            var result = new SafetyScoreResult { Score = 100 };
            var safeScript = (script ?? string.Empty).Trim();
            var safeTags = (hashtags ?? string.Empty).Trim();
            var profile = (runningProfile ?? string.Empty).Trim();
            var inputItems = items ?? new List<AiVideoGenInputItem>();
            var count = inputItems.Count;

            if (string.IsNullOrWhiteSpace(profile))
            {
                result.Score -= 18;
                result.Reasons.Add("Chưa chọn profile chạy.");
            }

            if (safeScript.Length < 30)
            {
                result.Score -= 20;
                result.Reasons.Add("Script quá ngắn, dễ kém chất lượng.");
            }

            if (safeScript.Length > 700)
            {
                result.Score -= 12;
                result.Reasons.Add("Script quá dài, có thể gây khó đọc.");
            }

            if (count <= 0)
            {
                result.Score -= 40;
                result.Reasons.Add("Không có item sản phẩm.");
            }
            else if (count == 1)
            {
                result.Score -= 8;
                result.Reasons.Add("Chỉ có 1 sản phẩm, nội dung dễ thiếu đa dạng.");
            }

            ApplyBannedKeywordPenalty(result, safeScript);
            ApplyHashtagPenalty(result, safeTags);
            ApplyImageResolutionPenalty(result, inputItems);
            ApplyRiskyTokenPenalty(result, safeScript);
            return Finalize(result);
        }

        public SafetyScoreResult ScoreAutoPost(string caption, string hashtags, string videoFolder, string runningProfile)
        {
            var result = new SafetyScoreResult { Score = 100 };
            var safeCaption = (caption ?? string.Empty).Trim();
            var safeTags = (hashtags ?? string.Empty).Trim();
            var profile = (runningProfile ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(profile))
            {
                result.Score -= 20;
                result.Reasons.Add("Chưa chọn profile chạy.");
            }

            if (string.IsNullOrWhiteSpace(videoFolder))
            {
                result.Score -= 35;
                result.Reasons.Add("Thiếu thư mục video.");
            }

            if (safeCaption.Length < 8)
            {
                result.Score -= 18;
                result.Reasons.Add("Caption quá ngắn.");
            }

            if (safeCaption.Length > 220)
            {
                result.Score -= 12;
                result.Reasons.Add("Caption quá dài.");
            }

            ApplyHashtagPenalty(result, safeTags);

            ApplyRiskyTokenPenalty(result, safeCaption + " " + safeTags);
            return Finalize(result);
        }

        /// <summary>
        /// Chấm điểm Engagement (0-100) dựa trên metrics TikTok thực tế:
        /// Views (30) + Like rate (20) + Comment rate (15) + Share rate (15) + Save rate (15) + Duration sweet-spot (5).
        /// Trước khi enrich metrics, candidate.PlayCount = 0 → trả về 0 (sentinel "chưa scoring").
        /// </summary>
        public SafetyScoreResult ScoreAffiliateCandidate(AffiliateCandidate candidate)
        {
            var result = new SafetyScoreResult { Score = 0 };
            var item = candidate ?? new AffiliateCandidate();

            // Sentinel: nếu chưa enrich metrics (PlayCount==0) thì trả 0 = "pending".
            // Sẽ tính lại sau khi auto-enrich hoàn tất phase metrics.
            if (item.PlayCount <= 0)
            {
                result.Reasons.Add("Chưa có metrics — sẽ scoring sau khi auto-enrich xong.");
                return Finalize(result);
            }

            var views = (double)item.PlayCount;
            var likes = (double)item.LikeCount;
            var comments = (double)item.CommentCount;
            var shares = (double)item.ShareCount;
            var saves = (double)item.CollectCount;

            // --- Views (30 điểm, log scale) ---
            var viewsScore = ScoreByThresholds(views, new[]
            {
                (10_000_000d, 30),
                (1_000_000d,  27),
                (500_000d,    24),
                (100_000d,    20),
                (50_000d,     16),
                (10_000d,     12),
                (1_000d,       6),
                (1d,           2)
            });
            result.Score += viewsScore;
            result.Reasons.Add($"Views={FormatCompact(item.PlayCount)} → +{viewsScore}/30");

            // --- Like rate (20 điểm) ---
            var likeRate = likes / views * 100.0;
            var likeScore = ScoreByThresholds(likeRate, new[]
            {
                (15d, 20),
                (10d, 16),
                (5d,  12),
                (3d,   8),
                (1d,   4)
            });
            result.Score += likeScore;
            result.Reasons.Add($"Like rate={likeRate:0.##}% → +{likeScore}/20");

            // --- Comment rate (15 điểm) ---
            var commentRate = comments / views * 100.0;
            var commentScore = ScoreByThresholds(commentRate, new[]
            {
                (1d,    15),
                (0.5d,  10),
                (0.2d,   6),
                (0.05d,  3)
            });
            result.Score += commentScore;
            result.Reasons.Add($"Comment rate={commentRate:0.###}% → +{commentScore}/15");

            // --- Share rate (15 điểm) ---
            var shareRate = shares / views * 100.0;
            var shareScore = ScoreByThresholds(shareRate, new[]
            {
                (1d,    15),
                (0.5d,  10),
                (0.2d,   6),
                (0.05d,  3)
            });
            result.Score += shareScore;
            result.Reasons.Add($"Share rate={shareRate:0.###}% → +{shareScore}/15");

            // --- Save rate (15 điểm) — TikTok không trả collect_count cho mọi video ---
            if (saves > 0)
            {
                var saveRate = saves / views * 100.0;
                var saveScore = ScoreByThresholds(saveRate, new[]
                {
                    (3d,   15),
                    (1.5d, 10),
                    (0.5d,  6),
                    (0.1d,  3)
                });
                result.Score += saveScore;
                result.Reasons.Add($"Save rate={saveRate:0.###}% → +{saveScore}/15");
            }
            else
            {
                // TikWM không cho saves → bù bằng engagement rate tổng (likes+comments+shares)/views.
                var totalEngRate = (likes + comments + shares) / views * 100.0;
                var fallbackScore = ScoreByThresholds(totalEngRate, new[]
                {
                    (15d, 15),
                    (10d, 10),
                    (5d,   6),
                    (2d,   3)
                });
                result.Score += fallbackScore;
                result.Reasons.Add($"(Không có saves) Total engagement rate={totalEngRate:0.##}% → +{fallbackScore}/15");
            }

            // --- Duration sweet spot (5 điểm) — "thời gian ở lại / viewer" proxy ---
            var dur = item.DurationSeconds;
            var durScore = 0;
            string durBucket;
            if (dur >= 7 && dur <= 25) { durScore = 5; durBucket = "sweet spot 7-25s"; }
            else if (dur >= 5 && dur <= 60) { durScore = 3; durBucket = "OK 5-60s"; }
            else if (dur >= 3 && dur <= 90) { durScore = 1; durBucket = "biên 3-90s"; }
            else if (dur <= 0) { durBucket = "không rõ độ dài"; }
            else { durBucket = "quá ngắn/dài"; }
            result.Score += durScore;
            result.Reasons.Add($"Duration={dur}s ({durBucket}) → +{durScore}/5");

            return Finalize(result);
        }

        private static int ScoreByThresholds(double value, (double threshold, int score)[] tiers)
        {
            foreach (var (threshold, score) in tiers)
            {
                if (value >= threshold) return score;
            }
            return 0;
        }

        private static string FormatCompact(long n)
        {
            if (n < 0) n = 0;
            if (n < 1_000) return n.ToString("N0");
            if (n < 1_000_000) return (n / 1000.0).ToString("0.#") + "K";
            if (n < 1_000_000_000) return (n / 1_000_000.0).ToString("0.#") + "M";
            return (n / 1_000_000_000.0).ToString("0.#") + "B";
        }

        private static void ApplyRiskyTokenPenalty(SafetyScoreResult result, string text)
        {
            var source = (text ?? string.Empty).ToLowerInvariant();
            foreach (var token in RiskyTokens)
            {
                if (source.Contains(token))
                {
                    result.Score -= 8;
                    result.Reasons.Add("Nội dung chứa cụm rủi ro: " + token);
                }
            }
        }

        private static void ApplyBannedKeywordPenalty(SafetyScoreResult result, string script)
        {
            var source = (script ?? string.Empty).ToLowerInvariant();
            foreach (var token in BannedScriptKeywords)
            {
                if (!source.Contains(token))
                {
                    continue;
                }

                result.Score -= 18;
                result.Reasons.Add("Script chứa từ khóa cấm/nhạy cảm: " + token);
            }
        }

        private static void ApplyHashtagPenalty(SafetyScoreResult result, string hashtags)
        {
            var hashCount = (hashtags ?? string.Empty)
                .Split(new[] { ' ', '\n', '\r', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(x => x.StartsWith("#", StringComparison.Ordinal));

            if (hashCount <= 0)
            {
                return;
            }

            if (hashCount <= 8)
            {
                return;
            }

            var overflow = hashCount - 8;
            var penalty = Math.Min(20, overflow * 2);
            result.Score -= penalty;
            result.Reasons.Add($"Hashtag vượt ngưỡng an toàn ({hashCount}/8).");
        }

        private static void ApplyImageResolutionPenalty(SafetyScoreResult result, IList<AiVideoGenInputItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            var lowResCount = 0;
            var missingCount = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var imageRef = (items[i]?.ImageUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(imageRef))
                {
                    missingCount++;
                    continue;
                }

                var resolution = TryReadImageResolution(imageRef);
                if (!resolution.HasValue)
                {
                    missingCount++;
                    continue;
                }

                var (w, h) = resolution.Value;
                if (w < 720 || h < 720)
                {
                    lowResCount++;
                }
            }

            if (lowResCount > 0)
            {
                var penalty = Math.Min(24, lowResCount * 6);
                result.Score -= penalty;
                result.Reasons.Add($"Có {lowResCount} ảnh độ phân giải thấp (<720px).");
            }

            if (missingCount > 0)
            {
                var penalty = Math.Min(16, missingCount * 4);
                result.Score -= penalty;
                result.Reasons.Add($"Không đọc được độ phân giải của {missingCount} ảnh.");
            }
        }

        private static (int width, int height)? TryReadImageResolution(string imageRef)
        {
            try
            {
                if (Uri.TryCreate(imageRef, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    var request = WebRequest.CreateHttp(uri);
                    request.Timeout = 6000;
                    request.ReadWriteTimeout = 6000;
                    request.Method = "GET";
                    using (var response = request.GetResponse())
                    using (var stream = response.GetResponseStream())
                    using (var image = Image.FromStream(stream))
                    {
                        return (image.Width, image.Height);
                    }
                }

                if (!Path.IsPathRooted(imageRef))
                {
                    var resolved = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imageRef));
                    if (File.Exists(resolved))
                    {
                        using (var image = Image.FromFile(resolved))
                        {
                            return (image.Width, image.Height);
                        }
                    }
                }
                else if (File.Exists(imageRef))
                {
                    using (var image = Image.FromFile(imageRef))
                    {
                        return (image.Width, image.Height);
                    }
                }
            }
            catch
            {
                // Ignore and return null so caller can apply graceful penalty.
            }

            return null;
        }

        private static SafetyScoreResult Finalize(SafetyScoreResult result)
        {
            if (result.Score < 0) result.Score = 0;
            if (result.Score > 100) result.Score = 100;
            // Engagement: <60 = video yếu → cần xem lại thủ công.
            result.RequiresManualApproval = result.Score > 0 && result.Score < 60;
            return result;
        }
    }
}
