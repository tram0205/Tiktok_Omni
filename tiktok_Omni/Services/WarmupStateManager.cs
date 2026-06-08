using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public class WarmupStateManager
    {
        private const string StateFileName = "warmup_state.json";

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

        private static string GetStatePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StateFileName);
        }
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
