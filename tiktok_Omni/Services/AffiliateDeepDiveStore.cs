using System;
using System.IO;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    /// <summary>Lưu kết quả Deep Dive theo profile (JSON + đường dẫn artifact).</summary>
    public static class AffiliateDeepDiveStore
    {
        public static string GetDeepDiveSessionFolder(
            AffiliateCandidate candidate,
            string storageRoot,
            bool create = true)
        {
            var profile = ProfileScopedPaths.ResolveProfileFromCandidate(candidate);
            var cat = ProfileScopedPaths.SanitizeSegment(candidate?.Category);
            if (string.IsNullOrWhiteSpace(cat))
            {
                cat = "Uncategorized";
            }

            var kw = ProfileScopedPaths.SanitizeSegment(candidate?.SourceKeyword);
            if (string.IsNullOrWhiteSpace(kw))
            {
                kw = "general";
            }

            var dir = Path.Combine(
                ProfileScopedPaths.GetVideoTypeFolder(storageRoot, profile, VideoStorageType.Original, create),
                "AffiliateDeepDive",
                cat,
                kw,
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            if (create)
            {
                Directory.CreateDirectory(dir);
            }

            return Path.GetFullPath(dir);
        }

        public static void SaveCandidateSnapshot(
            AffiliateCandidate candidate,
            VideoDeepAnalysisResult result,
            string storageRoot,
            string compressedVideoPath = null)
        {
            if (candidate == null)
            {
                return;
            }

            try
            {
                var folder = GetDeepDiveSessionFolder(candidate, storageRoot, create: true);
                var payload = new
                {
                    SavedAtUtc = DateTime.UtcNow,
                    candidate.ProfileName,
                    candidate.SourceKeyword,
                    candidate.Category,
                    candidate.VideoUrl,
                    candidate.ProductName,
                    candidate.SafetyScore,
                    candidate.VoiceoverTranscript,
                    candidate.VideoScript,
                    candidate.LinkedProduct,
                    result?.MoneyShotSummary,
                    CompressedVideoPath = compressedVideoPath ?? string.Empty
                };

                File.WriteAllText(
                    Path.Combine(folder, "deep_dive_snapshot.json"),
                    JsonConvert.SerializeObject(payload, Formatting.Indented),
                    TextFileEncoding.Utf8NoBom);
            }
            catch
            {
                // ignored — không chặn pipeline
            }
        }
    }
}
