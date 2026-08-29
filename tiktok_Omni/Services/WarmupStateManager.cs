using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public class WarmupStateManager
    {
        private const string StateFileName = "warmup_state.json";
        private const string ProfileChannelStatesFileName = "profile_channel_states.json";
        private static readonly SemaphoreSlim StateFileGate = new SemaphoreSlim(1, 1);

        public async Task<WarmupRunState> LoadAsync()
        {
            var path = AppDataPaths.ResolveReadableJsonPath(StateFileName, out var migrate);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                await StateFileGate.WaitAsync().ConfigureAwait(false);
                string json;
                try
                {
                    json = await Task.Run(() => File.ReadAllText(path, TextFileEncoding.Utf8)).ConfigureAwait(false);
                }
                finally
                {
                    StateFileGate.Release();
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                var state = JsonConvert.DeserializeObject<WarmupRunState>(json);
                if (state != null)
                {
                    MigrateLegacyWarmupRunState(json, state);
                }

                if (migrate && state != null)
                {
                    await SaveAsync(state).ConfigureAwait(false);
                }

                return state;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Đọc JSON cũ (WatchSeconds*, AutoComment, ScheduledAtLocal) sang model mới.</summary>
        private static void MigrateLegacyWarmupRunState(string json, WarmupRunState state)
        {
            if (string.IsNullOrWhiteSpace(json) || state == null)
            {
                return;
            }

            try
            {
                var jo = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(json);
                if (jo == null)
                {
                    return;
                }

                if (state.ScheduledAtUtc == null && jo["ScheduledAtLocal"] != null && jo["ScheduledAtLocal"].Type != Newtonsoft.Json.Linq.JTokenType.Null)
                {
                    var local = jo["ScheduledAtLocal"].ToObject<DateTime?>();
                    if (local.HasValue)
                    {
                        var dt = local.Value;
                        if (dt.Kind == DateTimeKind.Unspecified)
                        {
                            dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);
                        }

                        state.ScheduledAtUtc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
                    }
                }

                // JSON cũ chưa có % giữ chân — nếu còn WatchSeconds* thì giữ default 80/150 (không map giây→%).
                if (jo["WatchPercentageMin"] == null && jo["WatchSecondsMin"] != null)
                {
                    state.WatchPercentageMin = 80;
                }

                if (jo["WatchPercentageMax"] == null && jo["WatchSecondsMax"] != null)
                {
                    state.WatchPercentageMax = 150;
                }

                if (jo["CommentProbability"] == null && jo["AutoComment"] != null)
                {
                    state.CommentProbability = jo.Value<bool>("AutoComment") ? 10 : 0;
                }

                state.WatchPercentageMin = ClampPercentAllowOver(state.WatchPercentageMin, 1, 300, 80);
                state.WatchPercentageMax = ClampPercentAllowOver(
                    state.WatchPercentageMax,
                    Math.Max(1, state.WatchPercentageMin),
                    300,
                    Math.Max(150, state.WatchPercentageMin));
                state.LikeProbability = ClampPercent(state.LikeProbability, 50);
                state.CommentProbability = ClampPercent(state.CommentProbability, 10);
                state.ShareProbability = ClampPercent(state.ShareProbability, 20);
            }
            catch
            {
                // Best-effort migration only.
            }
        }

        private static int ClampPercent(int value, int fallback)
        {
            if (value < 0 || value > 100)
            {
                return Math.Max(0, Math.Min(100, fallback));
            }

            return value;
        }

        private static int ClampPercentAllowOver(int value, int min, int max, int fallback)
        {
            if (value < min || value > max)
            {
                return fallback;
            }

            return value;
        }

        public async Task SaveAsync(WarmupRunState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var json = JsonConvert.SerializeObject(state, Formatting.Indented);
            await StateFileGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await Task.Run(() =>
                {
                    AppDataPaths.WriteJson(StateFileName, json);
                    AppDataPaths.TryDeleteLegacyJson(StateFileName);
                }).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // Không để race ghi file làm crash UI (async void progress callback).
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                StateFileGate.Release();
            }
        }

        public async Task ClearAsync()
        {
            await StateFileGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var path = AppDataPaths.PersistentFile(StateFileName);
                for (var attempt = 1; attempt <= 8; attempt++)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                        AppDataPaths.TryDeleteLegacyJson(StateFileName);
                        break; // Thành công thì thoát
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        if (attempt >= 8) throw;
                    }
                    // Giải phóng luồng thay vì khóa chết bằng Thread.Sleep
                    await Task.Delay(30 * attempt).ConfigureAwait(false);
                }
            }
            finally
            {
                StateFileGate.Release();
            }
        }

        public async Task<Dictionary<string, ProfileChannelState>> LoadProfileChannelStatesAsync()
        {
            var path = AppDataPaths.ResolveReadableJsonPath(ProfileChannelStatesFileName, out var migrate);
            if (!File.Exists(path))
            {
                return new Dictionary<string, ProfileChannelState>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var json = await Task.Run(() => File.ReadAllText(path, TextFileEncoding.Utf8)).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new Dictionary<string, ProfileChannelState>(StringComparer.OrdinalIgnoreCase);
                }

                var list = JsonConvert.DeserializeObject<List<ProfileChannelState>>(json) ?? new List<ProfileChannelState>();
                var map = list
                    .Where(s => s != null && !string.IsNullOrWhiteSpace(s.ProfileName))
                    .GroupBy(s => s.ProfileName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
                if (migrate && map.Count > 0)
                {
                    await SaveProfileChannelStatesAsync(map.Values).ConfigureAwait(false);
                }

                return map;
            }
            catch
            {
                return new Dictionary<string, ProfileChannelState>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public async Task SaveProfileChannelStatesAsync(IEnumerable<ProfileChannelState> states)
        {
            var list = (states ?? Enumerable.Empty<ProfileChannelState>())
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.ProfileName))
                .Select(s =>
                {
                    s.ProfileName = s.ProfileName.Trim();
                    return s;
                })
                .GroupBy(s => s.ProfileName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .OrderBy(s => s.ProfileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var json = JsonConvert.SerializeObject(list, Formatting.Indented);
            await Task.Run(() =>
            {
                AppDataPaths.WriteJson(ProfileChannelStatesFileName, json);
                AppDataPaths.TryDeleteLegacyJson(ProfileChannelStatesFileName);
            }).ConfigureAwait(false);
        }

        public async Task<ProfileChannelState> GetOrCreateProfileChannelStateAsync(string profileName)
        {
            var key = (profileName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(key))
            {
                key = "default";
            }

            var map = await LoadProfileChannelStatesAsync().ConfigureAwait(false);
            if (map.TryGetValue(key, out var existing) && existing != null)
            {
                return existing;
            }

            return new ProfileChannelState
            {
                ProfileName = key,
                Mode = ProfileOperationalMode.Posting
            };
        }
    }

    public enum ProfileOperationalMode
    {
        Posting = 0,
        WarmupNormal = 1,
        HighIntensityWarmup = 2
    }

    public class ProfileChannelState
    {
        public string ProfileName { get; set; } = string.Empty;
        public ProfileOperationalMode Mode { get; set; } = ProfileOperationalMode.Posting;
        public DateTime? LastTransitionUtc { get; set; }
        public DateTime? LastHealthCheckUtc { get; set; }
        public ChannelHealthSnapshot LastSnapshot { get; set; }
    }

    public class WarmupRunState
    {
        public string Keywords { get; set; } = string.Empty;
        public string RunningProfileName { get; set; } = string.Empty;
        public int VideoCount { get; set; }

        // --- NÂNG CẤP BẢO VỆ THUẬT TOÁN TIKTOK ---
        private int _watchPercentageMin = 80;
        /// <summary>Tỷ lệ thời lượng xem tối thiểu (ví dụ: 80%)</summary>
        public int WatchPercentageMin
        {
            get => _watchPercentageMin;
            // Dưới 30% là hành vi spam, tự động ép về 80%
            set => _watchPercentageMin = value < 30 ? 80 : Math.Min(300, value);
        }

        private int _watchPercentageMax = 150;
        /// <summary>Tỷ lệ thời lượng xem tối đa (ví dụ: 150% - xem vòng lặp)</summary>
        public int WatchPercentageMax
        {
            get => _watchPercentageMax;
            // Dưới 30% là hành vi spam, tự động ép về 150%
            set => _watchPercentageMax = value < 30 ? 150 : Math.Min(300, value);
        }

        public int LikeProbability { get; set; } = 50;
        public int CommentProbability { get; set; } = 10;
        public int ShareProbability { get; set; } = 20;

        public bool DryRun { get; set; }
        public int CompletedCount { get; set; }
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ScheduledAtUtc { get; set; }
    }
}
