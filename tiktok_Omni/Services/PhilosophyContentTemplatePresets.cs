using System;
using System.Collections.Generic;
using System.Linq;

namespace tiktok_Omni.Services
{
    public sealed class PhilosophyContentTemplatePreset
    {
        public PhilosophyContentTemplatePreset(
            string id,
            string displayLabel,
            string promptHint,
            string generationMode = "Quotes",
            int quoteCount = 5,
            int minDurationSeconds = PhilosophyRenderOptions.QuotesDefaultMinSeconds,
            int maxDurationSeconds = PhilosophyRenderOptions.QuotesDefaultMaxSeconds,
            string defaultAmbientKey = null,
            bool supportsMetadata = false,
            bool usesGemini = true)
        {
            Id = id ?? string.Empty;
            DisplayLabel = displayLabel ?? string.Empty;
            PromptHint = promptHint ?? string.Empty;
            GenerationMode = string.IsNullOrWhiteSpace(generationMode) ? "Quotes" : generationMode;
            QuoteCount = Math.Max(1, quoteCount);
            MinDurationSeconds = Math.Max(5, minDurationSeconds);
            MaxDurationSeconds = Math.Max(MinDurationSeconds, maxDurationSeconds);
            DefaultAmbientKey = defaultAmbientKey ?? string.Empty;
            SupportsMetadata = supportsMetadata;
            UsesGemini = usesGemini;
        }

        public string Id { get; }

        public string DisplayLabel { get; }

        public string PromptHint { get; }

        public string GenerationMode { get; }

        public int QuoteCount { get; }

        public int MinDurationSeconds { get; }

        public int MaxDurationSeconds { get; }

        public string DefaultAmbientKey { get; }

        public bool SupportsMetadata { get; }

        public bool UsesGemini { get; }

        public bool IsStory =>
            GenerationMode.Equals("Story", StringComparison.OrdinalIgnoreCase);

        public override string ToString() => DisplayLabel;
    }

    /// <summary>Loại nội dung batch Triết lý — map prompt Gemini + mặc định số câu/thời lượng/ambient.</summary>
    public static class PhilosophyContentTemplatePresets
    {
        public const string DefaultId = "philosophy";

        public static PhilosophyContentTemplatePreset Philosophy { get; } = new PhilosophyContentTemplatePreset(
            DefaultId,
            "Quote",
            "Viết câu quote sâu sắc, trầm, có chiều sâu cảm xúc — phù hợp TikTok quote video.",
            maxDurationSeconds: PhilosophyRenderOptions.QuotesDefaultMaxSeconds);

        public static PhilosophyContentTemplatePreset Story { get; } = new PhilosophyContentTemplatePreset(
            "story",
            "Story / monologue dài",
            "Viết MỘT bài monologue/kể chuyện ngắn có mở–thân–kết, giọng kể chậm, có nhịp thở giữa ý.",
            generationMode: "Story",
            quoteCount: 1,
            minDurationSeconds: PhilosophyRenderOptions.StoryDefaultMinSeconds,
            maxDurationSeconds: PhilosophyRenderOptions.StoryDefaultMaxSeconds);

        public static PhilosophyContentTemplatePreset Motivational { get; } = new PhilosophyContentTemplatePreset(
            "motivational",
            "Motivational / mindset",
            "Giọng điệu truyền cảm hứng, thúc đẩy hành động — mạnh mẽ nhưng chân thật, không sáo rỗng. Mood ưu tiên intense hoặc hopeful.");

        public static PhilosophyContentTemplatePreset Stoicism { get; } = new PhilosophyContentTemplatePreset(
            "stoicism",
            "Stoicism / bài học sống",
            "Phong cách Khắc kỷ / Marcus Aurelius / Seneca — bài học sống, chấp nhận nghịch cảnh, tự chủ nội tâm. Mood reflective hoặc calm.");

        public static PhilosophyContentTemplatePreset Affirmations { get; } = new PhilosophyContentTemplatePreset(
            "affirmations",
            "Affirmations / thiền / mindfulness",
            "Câu khẳng định tích cực hoặc hướng dẫn thở/thiền ngắn — giọng chậm, nhẹ, an nhiên. Mood calm.",
            quoteCount: 7,
            minDurationSeconds: 8,
            maxDurationSeconds: 20,
            defaultAmbientKey: "rain");

        public static PhilosophyContentTemplatePreset BookQuote { get; } = new PhilosophyContentTemplatePreset(
            "book_quote",
            "Trích dẫn sách / podcast",
            "Trích dẫn nguyên văn hoặc paraphrase sát nghĩa — có thể kèm tên tác giả/nguồn ở cuối câu nếu người dùng cung cấp metadata.",
            quoteCount: 3,
            minDurationSeconds: 10,
            maxDurationSeconds: 30,
            supportsMetadata: true);

        public static PhilosophyContentTemplatePreset Facts { get; } = new PhilosophyContentTemplatePreset(
            "facts",
            "Facts / «Did you know»",
            "Mở đầu bằng hook gây tò mò (số liệu, câu hỏi, sự thật bất ngờ) rồi giải thích ngắn gọn. Mood intense hoặc hopeful.",
            quoteCount: 5,
            minDurationSeconds: 10,
            maxDurationSeconds: 25);

        public static PhilosophyContentTemplatePreset Poetry { get; } = new PhilosophyContentTemplatePreset(
            "poetry",
            "Thơ / haiku / câu ngắn",
            "Thơ tự do hoặc haiku — ít từ, hình ảnh gợi, nhịp chậm. Mỗi câu rất ngắn.",
            quoteCount: 3,
            minDurationSeconds: 5,
            maxDurationSeconds: 15);

        public static PhilosophyContentTemplatePreset Spiritual { get; } = new PhilosophyContentTemplatePreset(
            "spiritual",
            "Tôn giáo / tâm linh",
            "Nội dung tâm linh, triết lý đạo đức, lòng biết ơn — tôn trọng, không phán xét. Mood calm hoặc reflective.",
            defaultAmbientKey: "wind");

        public static PhilosophyContentTemplatePreset Manual { get; } = new PhilosophyContentTemplatePreset(
            "manual",
            "Nhập tay (không Gemini)",
            string.Empty,
            usesGemini: false);

        public static IReadOnlyList<PhilosophyContentTemplatePreset> All { get; } = new[]
        {
            Philosophy,
            Story,
            Motivational,
            Stoicism,
            Affirmations,
            BookQuote,
            Facts,
            Poetry,
            Spiritual,
            Manual
        };

        public static PhilosophyContentTemplatePreset Resolve(string templateId)
        {
            var id = (templateId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(id))
            {
                return Philosophy;
            }

            return All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? Philosophy;
        }

        public static string NormalizeId(string templateId)
        {
            return Resolve(templateId).Id;
        }

        public static void ApplyDefaults(PhilosophyContentTemplatePreset preset, Models.PhilosophyBatchItem batch)
        {
            if (preset == null || batch == null || !preset.UsesGemini)
            {
                return;
            }

            batch.GenerationMode = preset.IsStory ? "Story" : "Quotes";
            batch.QuoteCount = preset.IsStory ? 1 : preset.QuoteCount;
            batch.MinDurationSeconds = preset.MinDurationSeconds;
            batch.MaxDurationSeconds = preset.MaxDurationSeconds;

            if (!string.IsNullOrWhiteSpace(preset.DefaultAmbientKey)
                && string.IsNullOrWhiteSpace(batch.AmbientKey))
            {
                batch.AmbientKey = PhilosophyAmbientCatalog.NormalizeKey(preset.DefaultAmbientKey);
            }
        }

        public static string BuildTopicWithMetadata(string topic, string metadata, PhilosophyContentTemplatePreset preset)
        {
            var subject = (topic ?? string.Empty).Trim();
            var meta = (metadata ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(meta) || preset == null || !preset.SupportsMetadata)
            {
                return subject;
            }

            return subject + " — nguồn: " + meta;
        }
    }
}
