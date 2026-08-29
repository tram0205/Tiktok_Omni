using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    /// <summary>Lưu Veo Task_ID đang Processing để resume poll sau khi khởi động lại app.</summary>
    public sealed class AsyncVeoTaskEntry
    {
        public string TaskId { get; set; } = string.Empty;
        public string StatusPollUrl { get; set; } = string.Empty;
        public string Status { get; set; } = "Processing";
        public string Kind { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public Guid? OmniJobId { get; set; }
        public int SceneIndex { get; set; }
        public bool ExpectVideo { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class AsyncTasksRebootStore
    {
        private const string FileName = "async_tasks_reboot.json";

        private readonly object _sync = new object();

        public List<AsyncVeoTaskEntry> Load()
        {
            lock (_sync)
            {
                var path = AppDataPaths.ResolveReadableJsonPath(FileName, out var migrateFromLegacy);
                if (!File.Exists(path))
                {
                    return new List<AsyncVeoTaskEntry>();
                }

                try
                {
                    var json = File.ReadAllText(path, TextFileEncoding.Utf8);
                    var list = JsonConvert.DeserializeObject<List<AsyncVeoTaskEntry>>(json);
                    var result = list?.Where(e => e != null && !string.IsNullOrWhiteSpace(e.TaskId)).ToList()
                           ?? new List<AsyncVeoTaskEntry>();
                    if (migrateFromLegacy && result.Count > 0)
                    {
                        Save(result);
                    }

                    return result;
                }
                catch
                {
                    return new List<AsyncVeoTaskEntry>();
                }
            }
        }

        public void Save(IEnumerable<AsyncVeoTaskEntry> entries)
        {
            var list = (entries ?? Enumerable.Empty<AsyncVeoTaskEntry>())
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.TaskId))
                .ToList();
            lock (_sync)
            {
                try
                {
                    AppDataPaths.WriteJson(FileName, JsonConvert.SerializeObject(list, Formatting.Indented));
                    AppDataPaths.TryDeleteLegacyJson(FileName);
                }
                catch
                {
                    // ignored
                }
            }
        }

        public void Upsert(AsyncVeoTaskEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.TaskId))
            {
                return;
            }

            var list = Load();
            list.RemoveAll(e => string.Equals(e.TaskId, entry.TaskId, StringComparison.OrdinalIgnoreCase));
            entry.Status = string.IsNullOrWhiteSpace(entry.Status) ? "Processing" : entry.Status;
            entry.CreatedAtUtc = entry.CreatedAtUtc == default ? DateTime.UtcNow : entry.CreatedAtUtc;
            list.Add(entry);
            Save(list);
        }

        public void Remove(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return;
            }

            var list = Load();
            var removed = list.RemoveAll(e => string.Equals(e.TaskId, taskId, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                Save(list);
            }
        }

        public List<AsyncVeoTaskEntry> GetProcessingEntries()
        {
            return Load()
                .Where(e => VeoAsyncJobResult.IsProcessingStatus(e.Status)
                            || string.Equals(e.Status, "Processing", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
