using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Phân loại ngách video affiliate bằng Gemini (chạy nền sau Hunt).</summary>
    public sealed class AffiliateCategoryService
    {
        private readonly GeminiService _gemini = new GeminiService();

        public async Task CategorizeBatchAsync(
            IList<AffiliateCandidate> candidates,
            AppSettings settings,
            Action<string> log,
            CancellationToken cancellationToken,
            int maxConcurrent = 3)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return;
            }

            if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                log?.Invoke("[Category] Bỏ qua — chưa có AI API Key.");
                return;
            }

            var pending = candidates
                .Where(c => c != null && string.IsNullOrWhiteSpace(c.Category))
                .Where(c => !string.IsNullOrWhiteSpace(BuildCaptionContext(c)))
                .ToList();

            if (pending.Count == 0)
            {
                return;
            }

            log?.Invoke($"[Category] Gemini phân loại {pending.Count} video…");
            var sem = new SemaphoreSlim(Math.Max(1, maxConcurrent), Math.Max(1, maxConcurrent));
            var tasks = pending.Select(async c =>
            {
                await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var cat = await ClassifyOneAsync(c, settings, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(cat))
                    {
                        c.Category = cat;
                    }
                }
                catch (Exception ex)
                {
                    log?.Invoke("[Category] Lỗi: " + ex.Message);
                }
                finally
                {
                    sem.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
            log?.Invoke("[Category] Hoàn tất phân loại.");
        }

        private async Task<string> ClassifyOneAsync(
            AffiliateCandidate candidate,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            var context = BuildCaptionContext(candidate);
            var prompt =
                "Phân loại video TikTok affiliate vào MỘT ngách ngắn (2-4 từ tiếng Việt hoặc tiếng Anh), ví dụ: Gia dụng, Làm đẹp, Thời trang, Đồ chơi, Ẩm thực. " +
                "Chỉ trả tên ngách, không giải thích.\r\n\r\nThông tin video:\r\n" + context;

            var raw = await _gemini.GenerateScriptAsync(
                prompt,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                cancellationToken).ConfigureAwait(false);

            var cat = (raw ?? string.Empty).Trim().Trim('"', '\'', '.');
            cat = Regex.Replace(cat, @"\s+", " ");
            if (cat.Length > 48)
            {
                cat = cat.Substring(0, 48);
            }

            return cat;
        }

        private static string BuildCaptionContext(AffiliateCandidate c)
        {
            if (c == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(c.ProductName))
            {
                parts.Add("Tiêu đề/sản phẩm: " + c.ProductName.Trim());
            }

            if (!string.IsNullOrWhiteSpace(c.Creator))
            {
                parts.Add("Creator: " + c.Creator.Trim());
            }

            if (!string.IsNullOrWhiteSpace(c.Hashtags))
            {
                parts.Add("Hashtag: " + c.Hashtags.Trim());
            }

            if (!string.IsNullOrWhiteSpace(c.LinkedProduct))
            {
                parts.Add("Anchor: " + c.LinkedProduct.Trim());
            }

            return string.Join("\r\n", parts);
        }
    }
}
