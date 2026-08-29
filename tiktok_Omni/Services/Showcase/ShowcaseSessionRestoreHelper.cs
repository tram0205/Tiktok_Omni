using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase
{
    /// <summary>Quét thư mục Processed/Showcase và gắn lại phiên + storyboard cho dòng lưới (khi draft mất metadata).</summary>
    public static class ShowcaseSessionRestoreHelper
    {
        private const int DefaultLookbackDays = 60;
        private const int MinMatchScore = 40;

        private static readonly Regex ExcelSlugPattern = new Regex(
            @"_Showcase_(.+?)_\d{8}_\d{6}$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public sealed class SessionCandidate
        {
            public string BaseDir { get; set; }
            public string ClipsDir { get; set; }
            public string SourceImagesDir { get; set; }
            public string ProductSlug { get; set; }
            public int ClipCount { get; set; }
            public int SourceCount { get; set; }
            public DateTime SessionStamp { get; set; }
            public string ExcelPath { get; set; }
        }

        public sealed class RestoreSummary
        {
            public int NewlyLinked { get; set; }
            public int ClipPathsRepaired { get; set; }
            public int AlreadyLinked { get; set; }
            public int StillNeedLink { get; set; }
            public List<string> UnmatchedProductNames { get; } = new List<string>();
        }

        /// <summary>Gắn phiên Showcase trên đĩa vào các dòng video — trả về số dòng đã khôi phục.</summary>
        public static int RestoreVideosFromDisk(
            string profileName,
            IList<ShowcaseVideoItem> videos,
            Action<string> log)
        {
            return RestoreOrRepairVideosFromDisk(profileName, videos, log).NewlyLinked;
        }

        /// <summary>Gắn phiên mới + sửa đường dẫn clip cho dòng đã liên kết; trả về thống kê chi tiết.</summary>
        public static RestoreSummary RestoreOrRepairVideosFromDisk(
            string profileName,
            IList<ShowcaseVideoItem> videos,
            Action<string> log)
        {
            var summary = new RestoreSummary();
            if (videos == null || videos.Count == 0)
            {
                return summary;
            }

            foreach (var video in videos)
            {
                if (video == null)
                {
                    continue;
                }

                if (TryRepairClipPaths(video, log))
                {
                    summary.ClipPathsRepaired++;
                }
            }

            var candidates = ListCandidates(profileName, DefaultLookbackDays)
                .Where(c => c.ClipCount > 0)
                .ToList();
            if (candidates.Count == 0)
            {
                log?.Invoke("[Showcase] Không tìm thấy phiên nào có clip trong thư mục Showcase.");
                FinalizeRestoreSummary(videos, summary);
                return summary;
            }

            var usedSessions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedVideos = new HashSet<Guid>();
            var pairs = BuildAssignmentPairs(videos, candidates);

            foreach (var pair in pairs.OrderByDescending(p => p.Score).ThenByDescending(p => p.Candidate.SessionStamp))
            {
                if (pair.Video == null || pair.Candidate == null)
                {
                    continue;
                }

                if (usedVideos.Contains(pair.Video.VideoId) || usedSessions.Contains(pair.Candidate.BaseDir))
                {
                    continue;
                }

                if (!ShouldHydrateVideo(pair.Video))
                {
                    continue;
                }

                HydrateVideoFromSession(profileName, pair.Video, pair.Candidate, pair.Score, log);
                usedSessions.Add(pair.Candidate.BaseDir);
                usedVideos.Add(pair.Video.VideoId);
                summary.NewlyLinked++;
            }

            FinalizeRestoreSummary(videos, summary);
            if (summary.NewlyLinked == 0 && summary.StillNeedLink > 0)
            {
                log?.Invoke("[Showcase] Không gắn được phiên nào — kiểm tra tên SP (vd. «Zoom AD trắng», «Áo dài…»).");
            }

            return summary;
        }

        /// <summary>Quét lại clips_render và cập nhật ClipPath (vd. scene_01.mp4.mp4 → file thật trên đĩa).</summary>
        public static bool TryRepairClipPaths(ShowcaseVideoItem video, Action<string> log)
        {
            if (video?.Scenes == null || video.Scenes.Count == 0)
            {
                return false;
            }

            var clipsDir = ResolveClipsDir(video);
            if (string.IsNullOrWhiteSpace(clipsDir) || !Directory.Exists(clipsDir))
            {
                return false;
            }

            var before = CountValidClipsOnDisk(video);
            ShowcaseSessionService.RefreshClipStatus(clipsDir, video.Scenes, log);
            video.RefreshDisplayFields();
            var after = CountValidClipsOnDisk(video);
            if (after <= before)
            {
                return false;
            }

            log?.Invoke("[Showcase] Sửa đường dẫn clip «"
                        + (video.ProductName ?? string.Empty).Trim()
                        + "»: "
                        + before + " → " + after + " clip trên đĩa.");
            return true;
        }

        /// <summary>Gắn phiên tốt nhất cho một dòng (khi chọn dòng / sau khi đổi tên SP).</summary>
        public static bool TryRestoreSingleVideo(
            string profileName,
            ShowcaseVideoItem video,
            Action<string> log,
            ISet<string> reservedSessionDirs = null)
        {
            if (!NeedsDiskRestore(video))
            {
                return false;
            }

            var name = (video.ProductName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var candidates = ListCandidates(profileName, DefaultLookbackDays)
                .Where(c => c.ClipCount > 0)
                .Where(c => reservedSessionDirs == null || !reservedSessionDirs.Contains(c.BaseDir))
                .ToList();
            if (candidates.Count == 0)
            {
                return false;
            }

            SessionCandidate best = null;
            var bestScore = 0;
            foreach (var candidate in candidates)
            {
                var score = ScoreCandidate(name, candidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best == null || bestScore <= 0)
            {
                return false;
            }

            HydrateVideoFromSession(profileName, video, best, bestScore, log);
            return true;
        }

        public static bool NeedsDiskRestore(ShowcaseVideoItem video) => ShouldHydrateVideo(video);

        public static IReadOnlyList<SessionCandidate> ListCandidates(string profileName, int lookbackDays)
        {
            var resolvedProfile = ProfileScopedPaths.ResolveProfileName(profileName);
            var processedRoot = ProfileScopedPaths.GetVideoTypeFolder(
                null,
                resolvedProfile,
                VideoStorageType.Processed,
                create: false);
            var showcaseRoot = Path.Combine(processedRoot, "Showcase");
            if (!Directory.Exists(showcaseRoot))
            {
                return Array.Empty<SessionCandidate>();
            }

            var cutoff = DateTime.Now.Date.AddDays(-Math.Max(1, lookbackDays));
            var list = new List<SessionCandidate>();

            foreach (var dateDir in Directory.EnumerateDirectories(showcaseRoot))
            {
                if (!DateTime.TryParseExact(
                        Path.GetFileName(dateDir),
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var folderDate)
                    || folderDate < cutoff)
                {
                    continue;
                }

                foreach (var sessionDir in Directory.EnumerateDirectories(dateDir))
                {
                    var clipsDir = ShowcaseRenderClipsPaths.ResolveDirectory(sessionDir, createIfMissing: false);
                    if (string.IsNullOrWhiteSpace(clipsDir) || !Directory.Exists(clipsDir))
                    {
                        continue;
                    }

                    var clipCount = ShowcaseSessionService.CountClipSlots(clipsDir);
                    if (clipCount <= 0)
                    {
                        continue;
                    }

                    var sourceDir = Path.Combine(sessionDir, "source_images");
                    var sourceCount = Directory.Exists(sourceDir)
                        ? ShowcaseSessionService.CountSourceMediaSlots(sourceDir)
                        : 0;

                    var excelPath = FindNewestExcel(sessionDir);
                    list.Add(new SessionCandidate
                    {
                        BaseDir = sessionDir,
                        ClipsDir = clipsDir,
                        SourceImagesDir = sourceDir,
                        ProductSlug = InferProductSlug(sessionDir, excelPath),
                        ClipCount = clipCount,
                        SourceCount = sourceCount,
                        SessionStamp = ResolveSessionStamp(dateDir, sessionDir),
                        ExcelPath = excelPath
                    });
                }
            }

            return list
                .OrderByDescending(c => c.SessionStamp)
                .ToList();
        }

        public static bool ProductNameMatchesSlug(string productName, string sessionSlug)
        {
            return ScoreProductMatch(productName, sessionSlug) >= MinMatchScore;
        }

        private static bool ShouldHydrateVideo(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return false;
            }

            if (video.Scenes == null || video.Scenes.Count == 0)
            {
                return true;
            }

            if (VideoHasPersistedWorkflowData(video))
            {
                return false;
            }

            return CountValidClipsOnDisk(video) <= 0;
        }

        /// <summary>Đã có thoại/prompt/clip gán — không ghi đè storyboard bằng hydrate từ đĩa.</summary>
        private static bool VideoHasPersistedWorkflowData(ShowcaseVideoItem video)
        {
            if (video == null)
            {
                return false;
            }

            if (video.ShowcaseVoiceoverClipFingerprint != 0)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(video.ShowcaseHookText)
                || !string.IsNullOrWhiteSpace(video.ShowcaseCtaText))
            {
                return true;
            }

            if (video.Scenes == null || video.Scenes.Count == 0)
            {
                return false;
            }

            foreach (var scene in video.Scenes)
            {
                if (scene == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(scene.SceneVoiceover))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(scene.VeoPrompt) || !string.IsNullOrWhiteSpace(scene.KlingPrompt))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(scene.ClipPath) || !string.IsNullOrWhiteSpace(scene.ShowcaseRealClipSourcePath))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountValidClipsOnDisk(ShowcaseVideoItem video)
        {
            if (video?.Scenes == null || video.Scenes.Count == 0)
            {
                return 0;
            }

            var clipsDir = ResolveClipsDir(video);
            if (!string.IsNullOrWhiteSpace(clipsDir) && Directory.Exists(clipsDir))
            {
                return ShowcaseSessionService.CountValidClipsOnDisk(clipsDir, video.Scenes);
            }

            return video.Scenes.Count(s =>
                !string.IsNullOrWhiteSpace(s?.ClipPath) && File.Exists(s.ClipPath));
        }

        private static string ResolveClipsDir(ShowcaseVideoItem video)
        {
            var clipsDir = (video?.ShowcaseClipsDir ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(clipsDir))
            {
                return clipsDir;
            }

            var baseDir = (video?.ShowcaseSessionBaseDir ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(baseDir) ? string.Empty : ShowcaseRenderClipsPaths.Combine(baseDir);
        }

        private static void FinalizeRestoreSummary(IList<ShowcaseVideoItem> videos, RestoreSummary summary)
        {
            if (videos == null || summary == null)
            {
                return;
            }

            foreach (var video in videos)
            {
                if (video == null)
                {
                    continue;
                }

                var name = (video.ProductName ?? string.Empty).Trim();
                if (NeedsDiskRestore(video))
                {
                    summary.StillNeedLink++;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        summary.UnmatchedProductNames.Add(name);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(name))
                {
                    summary.AlreadyLinked++;
                }
            }
        }

        private static List<(ShowcaseVideoItem Video, SessionCandidate Candidate, int Score)> BuildAssignmentPairs(
            IList<ShowcaseVideoItem> videos,
            IList<SessionCandidate> candidates)
        {
            var pairs = new List<(ShowcaseVideoItem, SessionCandidate, int)>();
            foreach (var video in videos)
            {
                if (!ShouldHydrateVideo(video))
                {
                    continue;
                }

                var name = (video.ProductName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                foreach (var candidate in candidates)
                {
                    var score = ScoreCandidate(name, candidate);
                    if (score > 0)
                    {
                        pairs.Add((video, candidate, score));
                    }
                }
            }

            if (pairs.Count == 0)
            {
                return PairByFallbackOrder(videos, candidates);
            }

            return pairs;
        }

        private static List<(ShowcaseVideoItem Video, SessionCandidate Candidate, int Score)> PairByFallbackOrder(
            IList<ShowcaseVideoItem> videos,
            IList<SessionCandidate> candidates)
        {
            var result = new List<(ShowcaseVideoItem, SessionCandidate, int)>();
            var hydrateVideos = videos.Where(ShouldHydrateVideo)
                .Where(v => !string.IsNullOrWhiteSpace(v?.ProductName))
                .ToList();
            if (hydrateVideos.Count == 0 || candidates.Count == 0)
            {
                return result;
            }

            if (hydrateVideos.Count != candidates.Count)
            {
                return result;
            }

            for (var i = 0; i < hydrateVideos.Count; i++)
            {
                result.Add((hydrateVideos[i], candidates[i], 10));
            }

            return result;
        }

        private static int ScoreCandidate(string productName, SessionCandidate candidate)
        {
            var score = ScoreProductMatch(productName, candidate.ProductSlug);
            if (score <= 0 && string.IsNullOrWhiteSpace(candidate.ProductSlug))
            {
                score = ScoreFallbackForUnlabeledSession(productName, candidate);
            }

            var rowSlug = NormalizeMatchKey(productName);
            if (ContainsAoDai(rowSlug))
            {
                score += Math.Min(30, candidate.SourceCount * 2 + candidate.ClipCount);
                if (candidate.SourceCount <= 0 && score > 0)
                {
                    score -= 20;
                }

                if (rowSlug.Length > 40 && candidate.SourceCount >= 5)
                {
                    score += 25;
                }
            }

            return Math.Max(0, score);
        }

        private static int ScoreProductMatch(string productName, string sessionSlug)
        {
            var rowSlug = NormalizeMatchKey(productName);
            var slug = NormalizeMatchKey(sessionSlug);
            if (string.IsNullOrWhiteSpace(rowSlug))
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(slug))
            {
                return 0;
            }

            if (string.Equals(rowSlug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return 100;
            }

            if (slug.StartsWith(rowSlug, StringComparison.OrdinalIgnoreCase)
                || rowSlug.StartsWith(slug, StringComparison.OrdinalIgnoreCase))
            {
                return 90;
            }

            if (slug.IndexOf(rowSlug, StringComparison.OrdinalIgnoreCase) >= 0
                || rowSlug.IndexOf(slug, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 80;
            }

            if (ContainsToken(rowSlug, "zoom") && ContainsToken(slug, "zoom"))
            {
                return 75;
            }

            if (ContainsAoDai(rowSlug) && ContainsAoDai(slug))
            {
                return 75;
            }

            return 0;
        }

        private static int ScoreFallbackForUnlabeledSession(string productName, SessionCandidate candidate)
        {
            var rowSlug = NormalizeMatchKey(productName);
            if (string.IsNullOrWhiteSpace(rowSlug))
            {
                return 0;
            }

            if (ContainsAoDai(rowSlug))
            {
                return 35;
            }

            if (ContainsToken(rowSlug, "zoom"))
            {
                return 30;
            }

            return 0;
        }

        private static void HydrateVideoFromSession(
            string profileName,
            ShowcaseVideoItem video,
            SessionCandidate candidate,
            int score,
            Action<string> log)
        {
            var productName = (video.ProductName ?? string.Empty).Trim();
            var session = ShowcaseSessionService.TryRestoreSessionFromBaseDir(
                profileName,
                productName,
                candidate.BaseDir);
            if (session == null)
            {
                return;
            }

            video.ShowcaseSessionBaseDir = session.BaseDir ?? string.Empty;
            video.ShowcaseClipsDir = session.ClipsDir ?? string.Empty;

            if (video.Scenes != null && video.Scenes.Count > 0 && VideoHasPersistedWorkflowData(video))
            {
                ShowcaseSessionService.RefreshClipStatus(session.ClipsDir, video.Scenes, log);
                video.RefreshDisplayFields();
                log?.Invoke("[Showcase] Giữ kịch bản/thoại «" + productName
                            + "» — chỉ sửa đường dẫn clip từ clips_render.");
                return;
            }

            var sceneCount = candidate.ClipCount;
            if (candidate.SourceCount > sceneCount)
            {
                sceneCount = candidate.SourceCount;
            }

            if (sceneCount <= 0)
            {
                sceneCount = candidate.ClipCount;
            }

            if (video.Scenes == null)
            {
                return;
            }

            video.Scenes.Clear();
            for (var order = 1; order <= sceneCount; order++)
            {
                var media = ShowcaseSessionService.TryDetectSourceMediaForOrder(session.SourceImagesDir, order);
                var item = new AiVideoGenInputItem
                {
                    ProfileName = ProfileScopedPaths.ResolveProfileName(profileName),
                    ProductName = productName,
                    ImageUrl = media ?? string.Empty,
                    ThumbnailPath = media ?? string.Empty,
                    PipelineStatus = "Chờ"
                };
                video.Scenes.Add(item);
            }

            ShowcaseSessionService.RefreshSourceImageStatus(session.SourceImagesDir, video.Scenes, log);
            ShowcaseSessionService.RefreshClipStatus(session.ClipsDir, video.Scenes, log);

            if (!string.IsNullOrWhiteSpace(candidate.ExcelPath) && File.Exists(candidate.ExcelPath))
            {
                try
                {
                    ShowcaseExcelHelper.ImportEdits(candidate.ExcelPath, video.Scenes);
                }
                catch (Exception ex)
                {
                    log?.Invoke("[Showcase] Không đọc Excel phiên «" + productName + "»: " + ex.Message);
                }
            }

            var outputPath = ShowcaseContentDisplayHelper.TryResolveFinishedVideoPath(video);
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                video.OutputVideoPath = outputPath;
            }

            video.RefreshDisplayFields();

            var slugHint = string.IsNullOrWhiteSpace(candidate.ProductSlug)
                ? "(không có Excel)"
                : candidate.ProductSlug;
            log?.Invoke("[Showcase] Gắn «" + productName + "» → "
                        + candidate.BaseDir
                        + " · " + video.Scenes.Count + " cảnh · slug=" + slugHint
                        + (score < MinMatchScore ? " (khớp yếu — kiểm tra tên SP)" : string.Empty));
        }

        private static string FindNewestExcel(string sessionDir)
        {
            try
            {
                return Directory.GetFiles(sessionDir, "*.xlsx")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static string InferProductSlug(string sessionDir, string excelPath)
        {
            var fromExcel = InferSlugFromExcelFileName(excelPath);
            if (!string.IsNullOrWhiteSpace(fromExcel))
            {
                return fromExcel;
            }

            return string.Empty;
        }

        private static string InferSlugFromExcelFileName(string excelPath)
        {
            if (string.IsNullOrWhiteSpace(excelPath))
            {
                return string.Empty;
            }

            var name = Path.GetFileNameWithoutExtension(excelPath) ?? string.Empty;
            var match = ExcelSlugPattern.Match(name);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static DateTime ResolveSessionStamp(string dateDir, string sessionDir)
        {
            var stampText = Path.GetFileName(dateDir) + Path.GetFileName(sessionDir);
            if (DateTime.TryParseExact(
                    stampText,
                    "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                return parsed;
            }

            try
            {
                return Directory.GetLastWriteTime(sessionDir);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private static string Slugify(string text)
        {
            var s = NormalizeMatchKey(text);
            if (s.Length > 40)
            {
                s = s.Substring(0, 40);
            }

            return s;
        }

        private static string NormalizeMatchKey(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var formD = text.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var ch in formD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                sb.Append(char.ToLowerInvariant(ch));
            }

            var s = sb.ToString().Normalize(NormalizationForm.FormC);
            return new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
        }

        private static bool ContainsToken(string slug, string token)
        {
            return (slug ?? string.Empty).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAoDai(string slug)
        {
            var s = slug ?? string.Empty;
            return s.IndexOf("ao", StringComparison.OrdinalIgnoreCase) >= 0
                   && s.IndexOf("dai", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
