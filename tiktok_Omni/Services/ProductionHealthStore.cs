using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public sealed class PipelineRunStats
    {
        public int Success { get; set; }
        public int Failed { get; set; }

        public double SuccessRatePercent =>
            Success + Failed == 0 ? 0 : Success * 100.0 / (Success + Failed);
    }

    public sealed class ApiHealthAlert
    {
        public string Service { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class ProductionHealthSnapshot
    {
        public int TotalVideosProduced { get; set; }
        public int TotalErrors { get; set; }
        public int PendingApprovals { get; set; }
        public string TodayDateUtc { get; set; } = string.Empty;
        public Dictionary<string, int> VideosTodayByPipeline { get; set; } =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, PipelineRunStats> PipelineStats { get; set; } =
            new Dictionary<string, PipelineRunStats>(StringComparer.OrdinalIgnoreCase);
        public List<ApiHealthAlert> ApiAlerts { get; set; } = new List<ApiHealthAlert>();
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class ProductionHealthStore
    {
        private const string FileName = "production_health.json";
        private const int MaxAlerts = 30;
        private const int MaxIoAttempts = 5;
        private static readonly SemaphoreSlim IoGate = new SemaphoreSlim(1, 1);
        private ProductionHealthSnapshot _snapshot = new ProductionHealthSnapshot();

        public ProductionHealthSnapshot Snapshot => _snapshot;

        public async Task LoadAsync()
        {
            await IoGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var path = GetPath();
                if (!File.Exists(path))
                {
                    TryMigrateLegacyFile(path);
                }

                if (!File.Exists(path))
                {
                    _snapshot = NewSnapshot();
                    return;
                }

                try
                {
                    var json = await ReadAllTextWithRetryAsync(path).ConfigureAwait(false);
                    _snapshot = JsonConvert.DeserializeObject<ProductionHealthSnapshot>(json) ?? NewSnapshot();
                    EnsureTodayBucket();
                }
                catch (Exception)
                {
                    _snapshot = NewSnapshot();
                }
            }
            finally
            {
                IoGate.Release();
            }
        }

        public async Task SaveAsync()
        {
            await IoGate.WaitAsync().ConfigureAwait(false);
            try
            {
                _snapshot.UpdatedAtUtc = DateTime.UtcNow;
                var json = JsonConvert.SerializeObject(_snapshot, Formatting.Indented);
                var path = GetPath();
                for (var attempt = 1; attempt <= MaxIoAttempts; attempt++)
                {
                    try
                    {
                        await Task.Run(() => WriteAtomic(path, json)).ConfigureAwait(false);
                        return;
                    }
                    catch (IOException)
                    {
                        if (attempt >= MaxIoAttempts)
                        {
                            break;
                        }

                        await Task.Delay(50 * attempt).ConfigureAwait(false);
                    }
                }

                // Không ném exception — tránh crash debugger khi OneDrive/antivirus lock file.
                return;
            }
            finally
            {
                IoGate.Release();
            }
        }

        public void RecordVideoCompleted(string pipelineName)
        {
            EnsureTodayBucket();
            _snapshot.TotalVideosProduced++;
            BumpToday(pipelineName);
            BumpPipeline(pipelineName, success: true);
        }

        public void RecordPipelineFailure(string pipelineName)
        {
            EnsureTodayBucket();
            _snapshot.TotalErrors++;
            BumpPipeline(pipelineName, success: false);
        }

        public void RecordError()
        {
            _snapshot.TotalErrors++;
        }

        public void RecordApiError(string service, string message)
        {
            if (_snapshot.ApiAlerts == null)
            {
                _snapshot.ApiAlerts = new List<ApiHealthAlert>();
            }

            _snapshot.ApiAlerts.Insert(0, new ApiHealthAlert
            {
                Service = (service ?? "API").Trim(),
                Message = (message ?? string.Empty).Trim(),
                AtUtc = DateTime.UtcNow
            });

            if (_snapshot.ApiAlerts.Count > MaxAlerts)
            {
                _snapshot.ApiAlerts = _snapshot.ApiAlerts.Take(MaxAlerts).ToList();
            }
        }

        public void SetPendingApprovals(int count)
        {
            _snapshot.PendingApprovals = Math.Max(0, count);
        }

        private void BumpToday(string pipelineName)
        {
            var key = string.IsNullOrWhiteSpace(pipelineName) ? "Other" : pipelineName.Trim();
            if (!_snapshot.VideosTodayByPipeline.ContainsKey(key))
            {
                _snapshot.VideosTodayByPipeline[key] = 0;
            }

            _snapshot.VideosTodayByPipeline[key]++;
        }

        private void BumpPipeline(string pipelineName, bool success)
        {
            var key = string.IsNullOrWhiteSpace(pipelineName) ? "Other" : pipelineName.Trim();
            if (!_snapshot.PipelineStats.TryGetValue(key, out var stats))
            {
                stats = new PipelineRunStats();
                _snapshot.PipelineStats[key] = stats;
            }

            if (success)
            {
                stats.Success++;
            }
            else
            {
                stats.Failed++;
            }
        }

        private void EnsureTodayBucket()
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (_snapshot.TodayDateUtc == today)
            {
                return;
            }

            _snapshot.TodayDateUtc = today;
            _snapshot.VideosTodayByPipeline = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private static ProductionHealthSnapshot NewSnapshot()
        {
            return new ProductionHealthSnapshot
            {
                TodayDateUtc = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                VideosTodayByPipeline = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                PipelineStats = new Dictionary<string, PipelineRunStats>(StringComparer.OrdinalIgnoreCase),
                ApiAlerts = new List<ApiHealthAlert>()
            };
        }

        private static string GetPath()
        {
            return Path.Combine(GetStorageDirectory(), FileName);
        }

        /// <summary>Tránh ghi vào bin\Debug dưới OneDrive (hay bị sync lock).</summary>
        private static string GetStorageDirectory()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "tiktok_Omni");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string GetLegacyPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", FileName);
        }

        private static void TryMigrateLegacyFile(string targetPath)
        {
            var legacy = GetLegacyPath();
            if (!File.Exists(legacy) || File.Exists(targetPath))
            {
                return;
            }

            try
            {
                File.Copy(legacy, targetPath, overwrite: false);
            }
            catch (Exception)
            {
            }
        }

        private static async Task<string> ReadAllTextWithRetryAsync(string path)
        {
            IOException last = null;
            for (var attempt = 1; attempt <= MaxIoAttempts; attempt++)
            {
                try
                {
                    return await Task.Run(() => File.ReadAllText(path, TextFileEncoding.Utf8NoBom)).ConfigureAwait(false);
                }
                catch (IOException ex)
                {
                    last = ex;
                    if (attempt >= MaxIoAttempts)
                    {
                        throw;
                    }

                    await Task.Delay(50 * attempt).ConfigureAwait(false);
                }
            }

            throw last ?? new IOException("Không đọc được " + path);
        }

        private static void WriteAtomic(string path, string json)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json, TextFileEncoding.Utf8NoBom);
            if (File.Exists(path))
            {
                File.Replace(tmp, path, null);
            }
            else
            {
                File.Move(tmp, path);
            }
        }
    }
}
