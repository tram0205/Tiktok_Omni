using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public class WarmupQueueHistoryManager
    {
        private const string HistoryFileName = "warmup_queue_history.json";
        private const int MaxHistoryRecords = 300;

        public async Task<List<WarmupQueueHistoryRecord>> LoadAsync()
        {
            var path = GetPath();
            if (!File.Exists(path))
            {
                return new List<WarmupQueueHistoryRecord>();
            }

            try
            {
                var json = await Task.Run(() => File.ReadAllText(path)).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<WarmupQueueHistoryRecord>();
                }

                return JsonConvert.DeserializeObject<List<WarmupQueueHistoryRecord>>(json)
                       ?? new List<WarmupQueueHistoryRecord>();
            }
            catch
            {
                return new List<WarmupQueueHistoryRecord>();
            }
        }

        public async Task AppendAsync(WarmupQueueHistoryRecord record)
        {
            if (record == null)
            {
                return;
            }

            var list = await LoadAsync().ConfigureAwait(false);
            list.Insert(0, record);
            if (list.Count > MaxHistoryRecords)
            {
                list.RemoveRange(MaxHistoryRecords, list.Count - MaxHistoryRecords);
            }

            var json = JsonConvert.SerializeObject(list, Formatting.Indented);
            await Task.Run(() => File.WriteAllText(GetPath(), json)).ConfigureAwait(false);
        }

        private static string GetPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HistoryFileName);
        }
    }

    public class WarmupQueueHistoryRecord
    {
        public DateTime FinishedAtUtc { get; set; } = DateTime.UtcNow;
        public string Profile { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
        public int Videos { get; set; }
        public string Result { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public int Attempts { get; set; }
    }
}
