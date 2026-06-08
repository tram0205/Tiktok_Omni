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

        private static readonly object _fileSyncObj = new object();



        public static string GetReportFilePath()

        {

            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

            Directory.CreateDirectory(dir);

            return Path.Combine(dir, "EarningsReport.json");

        }



        public static Task<AffiliateRevenueReportFile> LoadAsync(CancellationToken cancellationToken = default)

        {

            return Task.Run(

                () =>

                {

                    cancellationToken.ThrowIfCancellationRequested();

                    var path = GetReportFilePath();

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



                        return JsonConvert.DeserializeObject<AffiliateRevenueReportFile>(json)

                               ?? new AffiliateRevenueReportFile();

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

                    var json = JsonConvert.SerializeObject(file, Formatting.Indented);

                    lock (_fileSyncObj)

                    {

                        File.WriteAllText(path, json, TextFileEncoding.Utf8NoBom);

                    }

                },

                cancellationToken);

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


