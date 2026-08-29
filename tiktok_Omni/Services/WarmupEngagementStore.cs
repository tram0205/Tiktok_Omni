using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Lưu video đã xem / comment / tim theo profile — dùng giữa các job warm-up.
    /// </summary>
    public sealed class WarmupEngagementStore
    {
        private const string HistoryFileName = "warmup_engagement_history.json";
        private const int MaxEntriesPerList = 800;
        private static readonly SemaphoreSlim FileGate = new SemaphoreSlim(1, 1);

        public static string NormalizeCaptionKey(string caption)
        {
            if (string.IsNullOrWhiteSpace(caption))
            {
                return string.Empty;
            }

            var s = caption.Trim().ToLowerInvariant();
            s = Regex.Replace(s, @"\s+", " ");
            return s;
        }

        public async Task<WarmupProfileEngagementRecord> LoadProfileAsync(string profileName)
        {
            var key = NormalizeProfileKey(profileName);
            var file = await LoadFileAsync().ConfigureAwait(false);
            if (file.Profiles.TryGetValue(key, out var record) && record != null)
            {
                EnsureLists(record);
                return record;
            }

            return new WarmupProfileEngagementRecord { ProfileName = key };
        }

        public async Task SaveProfileAsync(string profileName, WarmupProfileEngagementRecord record)
        {
            if (record == null)
            {
                return;
            }

            var key = NormalizeProfileKey(profileName);
            record.ProfileName = key;
            record.LastUpdatedUtc = DateTime.UtcNow;
            EnsureLists(record);

            await FileGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = await LoadFileUnsafeAsync().ConfigureAwait(false);
                file.Profiles[key] = record;
                var json = JsonConvert.SerializeObject(file, Formatting.Indented);
                await Task.Run(() =>
                {
                    AppDataPaths.WriteJson(HistoryFileName, json);
                    AppDataPaths.TryDeleteLegacyJson(HistoryFileName);
                }).ConfigureAwait(false);
            }
            finally
            {
                FileGate.Release();
            }
        }

        public static string BuildContentFingerprint(string author, string caption)
        {
            var a = (author ?? string.Empty).Trim().TrimStart('@').ToLowerInvariant();
            var c = NormalizeCaptionKey(caption);
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(c))
            {
                return string.Empty;
            }

            return a + "|" + c;
        }

        public HashSet<string> GetAllExcludeVideoIds(WarmupProfileEngagementRecord record)
        {
            EnsureLists(record);
            var set = new HashSet<string>(StringComparer.Ordinal);
            AddRange(set, record.WatchedVideoIds);
            AddRange(set, record.CommentedVideoIds);
            AddRange(set, record.LikedVideoIds);
            return set;
        }

        public HashSet<string> GetForyouExcludeFingerprints(WarmupProfileEngagementRecord record)
        {
            EnsureLists(record);
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddRange(set, record.WatchedFingerprints);
            foreach (var cap in record.CommentedCaptionKeys)
            {
                if (!string.IsNullOrWhiteSpace(cap))
                {
                    set.Add("|" + cap.Trim());
                }
            }

            return set;
        }

        public bool HasWatchedFingerprint(WarmupProfileEngagementRecord record, string author, string caption)
        {
            var fp = BuildContentFingerprint(author, caption);
            if (record == null || string.IsNullOrWhiteSpace(fp))
            {
                return false;
            }

            EnsureLists(record);
            return record.WatchedFingerprints.Contains(fp, StringComparer.OrdinalIgnoreCase);
        }

        public bool HasCommentedVideo(WarmupProfileEngagementRecord record, string videoId)
        {
            if (record == null || string.IsNullOrWhiteSpace(videoId))
            {
                return false;
            }

            EnsureLists(record);
            return record.CommentedVideoIds.Contains(videoId.Trim(), StringComparer.Ordinal);
        }

        public bool HasCommentedCaption(WarmupProfileEngagementRecord record, string caption)
        {
            var key = NormalizeCaptionKey(caption);
            if (record == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            EnsureLists(record);
            return record.CommentedCaptionKeys.Contains(key, StringComparer.Ordinal);
        }

        public bool HasLikedVideo(WarmupProfileEngagementRecord record, string videoId)
        {
            if (record == null || string.IsNullOrWhiteSpace(videoId))
            {
                return false;
            }

            EnsureLists(record);
            return record.LikedVideoIds.Contains(videoId.Trim(), StringComparer.Ordinal);
        }

        public void RecordWatched(WarmupProfileEngagementRecord record, string videoId)
        {
            PushId(record?.WatchedVideoIds, videoId);
        }

        public void RecordWatchedFingerprint(WarmupProfileEngagementRecord record, string author, string caption)
        {
            var fp = BuildContentFingerprint(author, caption);
            PushKey(record?.WatchedFingerprints, fp);
        }

        public void RecordComment(WarmupProfileEngagementRecord record, string videoId, string caption, string author = null)
        {
            PushId(record?.CommentedVideoIds, videoId);
            RecordWatched(record, videoId);
            RecordWatchedFingerprint(record, author, caption);
            var key = NormalizeCaptionKey(caption);
            if (!string.IsNullOrWhiteSpace(key))
            {
                PushKey(record?.CommentedCaptionKeys, key);
            }
        }

        public void RecordLike(WarmupProfileEngagementRecord record, string videoId)
        {
            PushId(record?.LikedVideoIds, videoId);
            RecordWatched(record, videoId);
        }

        private static void PushId(List<string> list, string videoId)
        {
            if (list == null || string.IsNullOrWhiteSpace(videoId))
            {
                return;
            }

            var id = videoId.Trim();
            list.RemoveAll(x => string.Equals(x, id, StringComparison.Ordinal));
            list.Add(id);
            TrimList(list);
        }

        private static void PushKey(List<string> list, string key)
        {
            if (list == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            list.RemoveAll(x => string.Equals(x, key, StringComparison.Ordinal));
            list.Add(key);
            TrimList(list);
        }

        private static void TrimList(List<string> list)
        {
            if (list == null || list.Count <= MaxEntriesPerList)
            {
                return;
            }

            var skip = list.Count - MaxEntriesPerList;
            list.RemoveRange(0, skip);
        }

        private static void AddRange(HashSet<string> set, List<string> ids)
        {
            if (set == null || ids == null)
            {
                return;
            }

            foreach (var id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    set.Add(id.Trim());
                }
            }
        }

        private static void EnsureLists(WarmupProfileEngagementRecord record)
        {
            if (record == null)
            {
                return;
            }

            if (record.WatchedVideoIds == null)
            {
                record.WatchedVideoIds = new List<string>();
            }

            if (record.CommentedVideoIds == null)
            {
                record.CommentedVideoIds = new List<string>();
            }

            if (record.LikedVideoIds == null)
            {
                record.LikedVideoIds = new List<string>();
            }

            if (record.CommentedCaptionKeys == null)
            {
                record.CommentedCaptionKeys = new List<string>();
            }

            if (record.WatchedFingerprints == null)
            {
                record.WatchedFingerprints = new List<string>();
            }
        }

        private static string NormalizeProfileKey(string profileName)
        {
            var key = (profileName ?? string.Empty).Trim();
            return string.IsNullOrEmpty(key) ? "default" : key;
        }

        private async Task<WarmupEngagementHistoryFile> LoadFileAsync()
        {
            await FileGate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await LoadFileUnsafeAsync().ConfigureAwait(false);
            }
            finally
            {
                FileGate.Release();
            }
        }

        private static async Task<WarmupEngagementHistoryFile> LoadFileUnsafeAsync()
        {
            var path = AppDataPaths.ResolveReadableJsonPath(HistoryFileName, out _);
            if (!File.Exists(path))
            {
                return new WarmupEngagementHistoryFile();
            }

            try
            {
                var json = await Task.Run(() => File.ReadAllText(path, TextFileEncoding.Utf8)).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new WarmupEngagementHistoryFile();
                }

                var file = JsonConvert.DeserializeObject<WarmupEngagementHistoryFile>(json);
                if (file?.Profiles == null)
                {
                    return new WarmupEngagementHistoryFile();
                }

                return file;
            }
            catch
            {
                return new WarmupEngagementHistoryFile();
            }
        }
    }

    public sealed class WarmupEngagementHistoryFile
    {
        public Dictionary<string, WarmupProfileEngagementRecord> Profiles { get; set; }
            = new Dictionary<string, WarmupProfileEngagementRecord>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class WarmupProfileEngagementRecord
    {
        public string ProfileName { get; set; } = string.Empty;
        public List<string> WatchedVideoIds { get; set; } = new List<string>();
        public List<string> CommentedVideoIds { get; set; } = new List<string>();
        public List<string> LikedVideoIds { get; set; } = new List<string>();
        public List<string> CommentedCaptionKeys { get; set; } = new List<string>();
        public List<string> WatchedFingerprints { get; set; } = new List<string>();
        public DateTime? LastUpdatedUtc { get; set; }
    }
}
