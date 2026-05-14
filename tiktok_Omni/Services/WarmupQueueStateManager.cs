using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public class WarmupQueueStateManager
    {
        private const string QueueFileName = "warmup_queue.json";
        private const string RuntimeFileName = "warmup_queue.runtime.json";

        public async Task<List<WarmupQueueSnapshotItem>> LoadAsync()
        {
            var path = GetPath();
            if (!File.Exists(path))
            {
                return new List<WarmupQueueSnapshotItem>();
            }

            try
            {
                var json = await Task.Run(() => File.ReadAllText(path)).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<WarmupQueueSnapshotItem>();
                }

                return JsonConvert.DeserializeObject<List<WarmupQueueSnapshotItem>>(json)
                       ?? new List<WarmupQueueSnapshotItem>();
            }
            catch
            {
                return new List<WarmupQueueSnapshotItem>();
            }
        }

        public async Task SaveAsync(IList<WarmupQueueSnapshotItem> items)
        {
            var safeItems = items == null ? new List<WarmupQueueSnapshotItem>() : new List<WarmupQueueSnapshotItem>(items);
            var json = JsonConvert.SerializeObject(safeItems, Formatting.Indented);
            await Task.Run(() => File.WriteAllText(GetPath(), json)).ConfigureAwait(false);
        }

        public async Task<bool> LoadPausedFlagAsync()
        {
            var path = GetRuntimePath();
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var json = await Task.Run(() => File.ReadAllText(path)).ConfigureAwait(false);
                var state = JsonConvert.DeserializeObject<WarmupQueueRuntimeState>(json ?? string.Empty);
                return state?.IsPaused ?? false;
            }
            catch
            {
                return false;
            }
        }

        public async Task SavePausedFlagAsync(bool isPaused)
        {
            var json = JsonConvert.SerializeObject(new WarmupQueueRuntimeState { IsPaused = isPaused }, Formatting.Indented);
            await Task.Run(() => File.WriteAllText(GetRuntimePath(), json)).ConfigureAwait(false);
        }

        private static string GetPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, QueueFileName);
        }

        private static string GetRuntimePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RuntimeFileName);
        }

        private class WarmupQueueRuntimeState
        {
            public bool IsPaused { get; set; }
        }
    }
}
