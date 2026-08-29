using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public static class AffiliateRevenueStore
    {
        private const string ReportFileName = "EarningsReport.json";
        private static readonly object _fileSyncObj = new object();

        public static string GetReportFilePath()
        {
            return AppDataPaths.PersistentPath("Data", ReportFileName);
        }

        private static string GetLegacyReportFilePath()
        {
            return Path.Combine(AppDataPaths.LegacyBinRoot, "Data", ReportFileName);
        }

        private static string ResolveReadableReportPath(out bool migrateFromLegacy)
        {
            migrateFromLegacy = false;
            var persistent = GetReportFilePath();
            if (File.Exists(persistent))
            {
                return persistent;
            }

            var legacy = GetLegacyReportFilePath();
            if (File.Exists(legacy))
            {
                migrateFromLegacy = true;
                return legacy;
            }

            return persistent;
        }

        public static Task<AffiliateRevenueReportFile> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var path = ResolveReadableReportPath(out var migrateFromLegacy);
                    if (!File.Exists(path))
                    {
                        return new AffiliateRevenueReportFile();
                    }

                    try
                    {
                        string json;
                        lock (_fileSyncObj)
                        {
                            json = File.ReadAllText(path, TextFileEncoding.Utf8);
                        }

                        if (string.IsNullOrWhiteSpace(json))
                        {
                            return new AffiliateRevenueReportFile();
                        }

                        var file = JsonConvert.DeserializeObject<AffiliateRevenueReportFile>(json)
                                   ?? new AffiliateRevenueReportFile();
                        if (migrateFromLegacy && (file.Items?.Count ?? 0) > 0)
                        {
                            SaveAsync(file.Items, file.ProfileName, cancellationToken)
                                .ConfigureAwait(false)
                                .GetAwaiter()
                                .GetResult();
                        }

                        return file;
                    }
                    catch
                    {
                        return new AffiliateRevenueReportFile();
                    }
                },
                cancellationToken);
        }

        public static Task SaveAsync(
            IEnumerable<AffiliateRevenueItem> items,
            string profileName,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var file = new AffiliateRevenueReportFile
                    {
                        LastFetchedUtc = DateTime.UtcNow,
                        ProfileName = profileName ?? string.Empty,
                        Items = (items ?? Array.Empty<AffiliateRevenueItem>())
                            .Where(x => x != null)
                            .OrderByDescending(x => x.Date)
                            .ToList()
                    };

                    var path = GetReportFilePath();
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppDataPaths.EnsurePersistentRoot());
                    var json = JsonConvert.SerializeObject(file, Formatting.Indented);
                    lock (_fileSyncObj)
                    {
                        File.WriteAllText(path, json, TextFileEncoding.Utf8NoBom);
                    }

                    TryDeleteLegacyReport();
                },
                cancellationToken);
        }

        private static void TryDeleteLegacyReport()
        {
            try
            {
                var legacy = GetLegacyReportFilePath();
                if (File.Exists(legacy))
                {
                    File.Delete(legacy);
                }
            }
            catch
            {
                // ignored
            }
        }

        public static List<AffiliateRevenueItem> GetLast14DaysSeries(IEnumerable<AffiliateRevenueItem> items)
        {
            var map = (items ?? Array.Empty<AffiliateRevenueItem>())
                .Where(x => x != null)
                .GroupBy(x => x.Date.Date)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Commission));

            var today = DateTime.Today;
            var list = new List<AffiliateRevenueItem>();
            for (var i = 13; i >= 0; i--)
            {
                var day = today.AddDays(-i);
                map.TryGetValue(day, out var commission);
                list.Add(new AffiliateRevenueItem
                {
                    Date = day,
                    Commission = commission,
                    OrderCount = 0,
                    Revenue = 0,
                    Status = commission > 0 ? "OK" : "—"
                });
            }

            return list;
        }
    }
}
