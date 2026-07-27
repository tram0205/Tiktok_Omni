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
            var path = ResolveReadablePath(QueueFileName, out var migrate);
            if (!File.Exists(path))
            {
                return new List<WarmupQueueSnapshotItem>();
            }

            try
            {
                var json = await Task.Run(() => File.ReadAllText(path, TextFileEncoding.Utf8)).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<WarmupQueueSnapshotItem>();
                }

                var items = JsonConvert.DeserializeObject<List<WarmupQueueSnapshotItem>>(json)
                       ?? new List<WarmupQueueSnapshotItem>();
                if (migrate && items.Count > 0)
                {
                    await SaveAsync(items).ConfigureAwait(false);
                }

                return items;
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
            await Task.Run(() =>
            {
                AppDataPaths.WriteJson(QueueFileName, json);
                AppDataPaths.TryDeleteLegacyJson(QueueFileName);
            }).ConfigureAwait(false);
        }

        public async Task<bool> LoadPausedFlagAsync()
        {
            var path = ResolveReadablePath(RuntimeFileName, out var migrate);
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var json = await Task.Run(() => File.ReadAllText(path, TextFileEncoding.Utf8)).ConfigureAwait(false);
                var state = JsonConvert.DeserializeObject<WarmupQueueRuntimeState>(json ?? string.Empty);
                var paused = state?.IsPaused ?? false;
                if (migrate && state != null)
                {
                    await SavePausedFlagAsync(paused).ConfigureAwait(false);
                }

                return paused;
            }
            catch
            {
                return false;
            }
        }

        public async Task SavePausedFlagAsync(bool isPaused)
        {
            var json = JsonConvert.SerializeObject(new WarmupQueueRuntimeState { IsPaused = isPaused }, Formatting.Indented);
            await Task.Run(() =>
            {
                AppDataPaths.WriteJson(RuntimeFileName, json);
                AppDataPaths.TryDeleteLegacyJson(RuntimeFileName);
            }).ConfigureAwait(false);
        }

        private static string ResolveReadablePath(string fileName, out bool migrateFromLegacy)
        {
            return AppDataPaths.ResolveReadableJsonPath(fileName, out migrateFromLegacy);
        }

        private class WarmupQueueRuntimeState
        {
            public bool IsPaused { get; set; }
        }
    }
}
