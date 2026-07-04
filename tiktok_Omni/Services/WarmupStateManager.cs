using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public class WarmupStateManager
    {
        private const string StateFileName = "warmup_state.json";
        private const string ProfileChannelStatesFileName = "profile_channel_states.json";

        public async Task<WarmupRunState> LoadAsync()
        {
            var path = GetStatePath();
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = await Task.Run(() => File.ReadAllText(path, TextFileEncoding.Utf8)).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<WarmupRunState>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveAsync(WarmupRunState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var json = JsonConvert.SerializeObject(state, Formatting.Indented);
            await Task.Run(() => File.WriteAllText(GetStatePath(), json, TextFileEncoding.Utf8NoBom)).ConfigureAwait(false);
        }

        public Task ClearAsync()
        {
            var path = GetStatePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        public async Task<Dictionary<string, ProfileChannelState>> LoadProfileChannelStatesAsync()
        {
            var path = GetProfileChannelStatesPath();
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
                return list
                    .Where(s => s != null && !string.IsNullOrWhiteSpace(s.ProfileName))
                    .GroupBy(s => s.ProfileName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
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
            await Task.Run(() => File.WriteAllText(GetProfileChannelStatesPath(), json, TextFileEncoding.Utf8NoBom)).ConfigureAwait(false);
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

        private static string GetStatePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StateFileName);
        }

        private static string GetProfileChannelStatesPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ProfileChannelStatesFileName);
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
        /// <summary>Total watch seconds (all videos combined) for this warm-up run.</summary>
        public int WatchSecondsMin { get; set; } = 7;
        /// <summary>Total watch seconds (all videos combined) for this warm-up run.</summary>
        public int WatchSecondsMax { get; set; } = 18;
        public bool AutoComment { get; set; }
        public bool DryRun { get; set; }
        public int CompletedCount { get; set; }
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Thời điểm chạy theo giờ máy (local); null = đưa vào hàng đợi ngay.</summary>
        public DateTime? ScheduledAtLocal { get; set; }
    }
}
