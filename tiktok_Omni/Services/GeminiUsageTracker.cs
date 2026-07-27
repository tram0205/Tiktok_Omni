using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// In-session Gemini video-call counter + light persistence for quota awareness (no API keys).
    /// </summary>
    public sealed class GeminiUsageTracker
    {
        private static readonly Lazy<GeminiUsageTracker> Lazy = new Lazy<GeminiUsageTracker>(() => new GeminiUsageTracker());
        public static GeminiUsageTracker Instance => Lazy.Value;

        private readonly object _sync = new object();
        private int _sessionVideoAnalysisCalls;
        private GeminiUsageDiskState _disk;

        private GeminiUsageTracker()
        {
        }

        private static string StateFilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "tiktok_Omni",
                "gemini_usage.json");

        private void EnsureLoadedLocked()
        {
            if (_disk != null)
            {
                return;
            }

            _disk = new GeminiUsageDiskState();
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            try
            {
                if (File.Exists(StateFilePath))
                {
                    var json = File.ReadAllText(StateFilePath, TextFileEncoding.Utf8);
                    var jo = JObject.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
                    _disk.LastUtc = jo.Value<DateTime?>("last429Utc");
                    _disk.Last429Summary = (jo["last429Summary"]?.ToString() ?? string.Empty).Trim();
                    var day = jo["videoAnalysisDay"]?.ToString();
                    var count = jo.Value<int?>("videoAnalysisCountDay") ?? 0;
                    _disk.VideoAnalysisDayMarker = string.IsNullOrWhiteSpace(day) ? today : day;
                    if (string.Equals(day, today, StringComparison.Ordinal))
                    {
                        _disk.VideoAnalysisCountDay = count;
                    }
                    else
                    {
                        _disk.VideoAnalysisCountDay = 0;
                        _disk.VideoAnalysisDayMarker = today;
                    }
                }
                else
                {
                    _disk.VideoAnalysisDayMarker = today;
                }
            }
            catch
            {
                _disk.VideoAnalysisDayMarker = today;
            }
        }

        private void SaveDiskLocked()
        {
            try
            {
                EnsureLoadedLocked();
                var d = _disk;
                Directory.CreateDirectory(Path.GetDirectoryName(StateFilePath) ?? ".");
                var jo = new JObject
                {
                    ["videoAnalysisDay"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    ["videoAnalysisCountDay"] = d.VideoAnalysisCountDay,
                    ["last429Utc"] = d.LastUtc,
                    ["last429Summary"] = d.Last429Summary
                };
                File.WriteAllText(StateFilePath, jo.ToString(Formatting.Indented), TextFileEncoding.Utf8NoBom);
            }
            catch
            {
                // ignore disk errors
            }
        }

        /// <summary>Call after a successful Gemini affiliate video analysis.</summary>
        public void RecordVideoAnalysisSuccess()
        {
            lock (_sync)
            {
                _sessionVideoAnalysisCalls++;
                EnsureLoadedLocked();
                var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                if (!string.Equals(_disk.VideoAnalysisDayMarker, today, StringComparison.Ordinal))
                {
                    _disk.VideoAnalysisDayMarker = today;
                    _disk.VideoAnalysisCountDay = 0;
                }

                _disk.VideoAnalysisCountDay++;
                SaveDiskLocked();
            }
        }

        public void RecordRateLimit429(string safeSummary)
        {
            lock (_sync)
            {
                EnsureLoadedLocked();
                _disk.LastUtc = DateTime.UtcNow;
                _disk.Last429Summary = Truncate(safeSummary ?? string.Empty, 400);
                SaveDiskLocked();
            }
        }

        /// <summary>True nếu vừa gặp 429 trong vòng <paramref name="hours"/> giờ (UTC).</summary>
        public bool WasRecentlyRateLimited(double hours)
        {
            if (hours <= 0)
            {
                hours = 2;
            }

            lock (_sync)
            {
                EnsureLoadedLocked();
                var last = _disk.LastUtc;
                return last.HasValue && (DateTime.UtcNow - last.Value).TotalHours < hours;
            }
        }

        public void LogPrewarnIfNeeded(Action<string> log, string modelHint)
        {
            if (log == null) return;
            lock (_sync)
            {
                EnsureLoadedLocked();
                var session = _sessionVideoAnalysisCalls;
                var day = _disk.VideoAnalysisCountDay;
                if (session >= 10)
                {
                    log.Invoke(
                        $"[GeminiUsage] Cảnh báo: phiên này đã gọi phân tích video ~{session} lần. Nên nghỉ giữa các lần hoặc đổi model nếu gặp 429. Model: {modelHint ?? "(mặc định)"}");
                }
                else if (day >= 15)
                {
                    log.Invoke(
                        $"[GeminiUsage] Cảnh báo: theo file lưu trong AppData, hôm nay (UTC) đã ~{day} lần phân tích video — free tier dễ giới hạn. Model: {modelHint ?? "(mặc định)"}");
                }

                var last = _disk.LastUtc;
                if (last.HasValue && (DateTime.UtcNow - last.Value).TotalHours < 2 &&
                    !string.IsNullOrWhiteSpace(_disk.Last429Summary))
                {
                    log.Invoke("[GeminiUsage] Gần đây có 429/quota — chi tiết (rút gọn): " +
                               Truncate(_disk.Last429Summary, 200));
                }
            }
        }

        public string GetSessionSummaryForUi()
        {
            lock (_sync)
            {
                EnsureLoadedLocked();
                return $"Phiên: {_sessionVideoAnalysisCalls} video-Gemini | Hôm nay (UTC file): {_disk.VideoAnalysisCountDay}";
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }

        private sealed class GeminiUsageDiskState
        {
            public string VideoAnalysisDayMarker { get; set; } = string.Empty;
            public int VideoAnalysisCountDay { get; set; }
            public DateTime? LastUtc { get; set; }
            public string Last429Summary { get; set; } = string.Empty;
        }
    }
}
