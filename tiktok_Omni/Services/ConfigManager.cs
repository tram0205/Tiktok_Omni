using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace tiktok_Omni.Services
{
    public class ConfigManager
    {
        private const string ConfigFileName = "appsettings.json";
        private static readonly string[] StableUserAgentPool = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:124.0) Gecko/20100101 Firefox/124.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.6312.122 Safari/537.36 Edg/123.0.2420.81"
        };

        public async Task<AppSettings> LoadAsync()
        {
            var path = GetConfigPath();
            if (!File.Exists(path))
            {
                var defaults = new AppSettings();
                await SaveAsync(defaults).ConfigureAwait(false);
                return defaults;
            }

            try
            {
                var json = await Task.Run(() => File.ReadAllText(path, TextFileEncoding.Utf8)).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new AppSettings();
                }

                var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                settings = settings ?? new AppSettings();
                MigrateLegacyLyriaTtsSettings(settings, json);
                return Normalize(DecryptSecretsInPlace(settings));
            }
            catch (JsonException)
            {
                // Keep app functional if settings file is manually edited incorrectly.
                var backupPath = path + ".invalid." + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(path, backupPath, true);
                return new AppSettings();
            }
        }

        public async Task SaveAsync(AppSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var forDisk = CloneSettings(settings);
            EncryptSecretsForStorage(forDisk);
            var json = JsonConvert.SerializeObject(forDisk, Formatting.Indented);
            await Task.Run(() => File.WriteAllText(GetConfigPath(), json, TextFileEncoding.Utf8NoBom)).ConfigureAwait(false);
        }

        /// <summary>
        /// Persists TikTok account info on the profile row matching <paramref name="profileName"/> (dropdown «Profile chạy»).
        /// </summary>
        public async Task UpdateAutomationProfileTikTokAsync(
            string profileName,
            TikTokAccountSnapshot account,
            Action<string> logAction = null)
        {
            if (account == null || string.IsNullOrWhiteSpace(account.UniqueId))
            {
                return;
            }

            var settings = await LoadAsync().ConfigureAwait(false);
            settings.Profiles = settings.Profiles ?? new List<AutomationProfile>();
            var key = string.IsNullOrWhiteSpace(profileName) ? "default" : profileName.Trim();
            AutomationProfile target = null;
            if (!string.Equals(key, "default", StringComparison.OrdinalIgnoreCase))
            {
                target = settings.Profiles.FirstOrDefault(p =>
                    p != null && string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                target = settings.Profiles.FirstOrDefault(p => p != null);
            }

            if (target == null)
            {
                target = new AutomationProfile
                {
                    Name = string.Equals(key, "default", StringComparison.OrdinalIgnoreCase)
                        ? "default"
                        : key
                };
                settings.Profiles.Add(target);
            }

            target.TikTokUniqueId = (account.UniqueId ?? string.Empty).Trim();
            target.TikTokNickname = TikTokDisplayNicknameSanitizer.Sanitize(
                (account.Nickname ?? string.Empty).Trim(),
                target.TikTokUniqueId);
            target.TikTokUserId = (account.UserId ?? string.Empty).Trim();
            await SaveAsync(settings).ConfigureAwait(false);
            logAction?.Invoke(
                $"[LOGIN] Đã lưu TikTok @{target.TikTokUniqueId}" +
                (string.IsNullOrWhiteSpace(target.TikTokNickname) ? string.Empty : $" ({target.TikTokNickname})") +
                $" vào profile «{target.Name}».");
        }

        public async Task UpdateProfilePlatformLoginFlagsAsync(
            string profileName,
            BrowserPlatform platform,
            bool loggedIn,
            Action<string> logAction = null)
        {
            var settings = await LoadAsync().ConfigureAwait(false);
            settings.Profiles = settings.Profiles ?? new List<AutomationProfile>();
            var key = string.IsNullOrWhiteSpace(profileName) ? "default" : profileName.Trim();
            var target = settings.Profiles.FirstOrDefault(p =>
                p != null && string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                target = new AutomationProfile { Name = key };
                settings.Profiles.Add(target);
            }

            if (platform == BrowserPlatform.Facebook)
            {
                target.IsFBLoggedIn = loggedIn;
            }
            else if (platform == BrowserPlatform.YouTube)
            {
                target.IsYTLoggedIn = loggedIn;
            }

            await SaveAsync(settings).ConfigureAwait(false);
            logAction?.Invoke(
                $"[LOGIN] Đã lưu trạng thái {platform} = {(loggedIn ? "đã đăng nhập" : "chưa")} cho profile «{target.Name}».");
        }

        public async Task UpdateProfileSocialLoginStatusAsync(
            string profileName,
            bool tikTokLoggedIn,
            bool facebookLoggedIn,
            bool youTubeLoggedIn,
            Action<string> logAction = null)
        {
            var settings = await LoadAsync().ConfigureAwait(false);
            settings.Profiles = settings.Profiles ?? new List<AutomationProfile>();
            var key = string.IsNullOrWhiteSpace(profileName) ? "default" : profileName.Trim();
            var target = settings.Profiles.FirstOrDefault(p =>
                p != null && string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                target = new AutomationProfile { Name = key };
                settings.Profiles.Add(target);
            }

            target.IsTTLoggedIn = tikTokLoggedIn;
            target.IsFBLoggedIn = facebookLoggedIn;
            target.IsYTLoggedIn = youTubeLoggedIn;
            await SaveAsync(settings).ConfigureAwait(false);
            logAction?.Invoke(
                $"[LOGIN] Profile «{target.Name}» — TT={(tikTokLoggedIn ? "✓" : "✗")} FB={(facebookLoggedIn ? "✓" : "✗")} YT={(youTubeLoggedIn ? "✓" : "✗")}");
        }

        private static AppSettings CloneSettings(AppSettings source)
        {
            var json = JsonConvert.SerializeObject(source);
            return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
        }

        /// <summary>
        /// DPAPI decrypt after JSON load (mutates instance).
        /// </summary>
        private static AppSettings DecryptSecretsInPlace(AppSettings settings)
        {
            settings.AiApiKey = DpapiSecretProtector.UnprotectAfterLoad(settings.AiApiKey ?? string.Empty);
            settings.TwoCaptchaApiKey = DpapiSecretProtector.UnprotectAfterLoad(settings.TwoCaptchaApiKey ?? string.Empty);
            settings.VeoApiKey = DpapiSecretProtector.UnprotectAfterLoad(settings.VeoApiKey ?? string.Empty);
            settings.TtsApiKey = DpapiSecretProtector.UnprotectAfterLoad(settings.TtsApiKey ?? string.Empty);
            settings.TikTokRapidApiKey = DpapiSecretProtector.UnprotectAfterLoad(settings.TikTokRapidApiKey ?? string.Empty);
            settings.NotificationSmtpPassword = DpapiSecretProtector.UnprotectAfterLoad(settings.NotificationSmtpPassword ?? string.Empty);
            if (settings.Profiles != null)
            {
                foreach (var p in settings.Profiles)
                {
                    if (p == null)
                    {
                        continue;
                    }

                    p.ProxyPass = DpapiSecretProtector.UnprotectAfterLoad(p.ProxyPass ?? string.Empty);
                }
            }

            return settings;
        }

        /// <summary>
        /// DPAPI encrypt before writing JSON (mutates clone only).
        /// </summary>
        private static void EncryptSecretsForStorage(AppSettings settings)
        {
            settings.AiApiKey = DpapiSecretProtector.ProtectForStorage(settings.AiApiKey ?? string.Empty);
            settings.TwoCaptchaApiKey = DpapiSecretProtector.ProtectForStorage(settings.TwoCaptchaApiKey ?? string.Empty);
            settings.VeoApiKey = DpapiSecretProtector.ProtectForStorage(settings.VeoApiKey ?? string.Empty);
            settings.TtsApiKey = DpapiSecretProtector.ProtectForStorage(settings.TtsApiKey ?? string.Empty);
            settings.TikTokRapidApiKey = DpapiSecretProtector.ProtectForStorage(settings.TikTokRapidApiKey ?? string.Empty);
            settings.NotificationSmtpPassword = DpapiSecretProtector.ProtectForStorage(settings.NotificationSmtpPassword ?? string.Empty);
            if (settings.Profiles != null)
            {
                foreach (var p in settings.Profiles)
                {
                    if (p == null)
                    {
                        continue;
                    }

                    p.ProxyPass = DpapiSecretProtector.ProtectForStorage(p.ProxyPass ?? string.Empty);
                }
            }
        }

        public async Task<AutomationProfile> EnsureProfileFingerprintAsync(
            AppSettings settings,
            string runningProfileName,
            Action<string> logAction = null)
        {
            var profile = ResolveProfile(settings, runningProfileName);
            if (profile == null)
            {
                return null;
            }

            var changed = false;
            var seed = BuildStableSeed(profile.Name);
            if (string.IsNullOrWhiteSpace(profile.UserAgent))
            {
                profile.UserAgent = StableUserAgentPool[seed % StableUserAgentPool.Length];
                changed = true;
                logAction?.Invoke($"[LIVE] Assigned fixed User-Agent for profile '{profile.Name}'.");
            }

            if (profile.ViewportWidth < 800 || profile.ViewportHeight < 600)
            {
                var viewport = BuildStableViewport(seed);
                profile.ViewportWidth = viewport.width;
                profile.ViewportHeight = viewport.height;
                changed = true;
                logAction?.Invoke($"[LIVE] Assigned stable viewport {viewport.width}x{viewport.height} for profile '{profile.Name}'.");
            }

            if (changed)
            {
                await SaveAsync(settings).ConfigureAwait(false);
            }

            return profile;
        }

        /// <summary>
        /// Lưu cấu hình ngoài thư mục bin\Debug (tránh mất key khi Rebuild/Clean).
        /// Tự migrate từ appsettings.json cạnh .exe nếu có.
        /// </summary>
        private static string GetConfigPath()
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "tiktok_Omni");
            Directory.CreateDirectory(appDataDir);
            var persistentPath = Path.Combine(appDataDir, ConfigFileName);

            var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", ConfigFileName);
            if (!File.Exists(persistentPath) && File.Exists(legacyPath))
            {
                try
                {
                    File.Copy(legacyPath, persistentPath, overwrite: false);
                }
                catch
                {
                    // ignored — user can re-enter keys if copy fails
                }
            }

            return persistentPath;
        }

        /// <summary>Đường dẫn cũ trỏ OneDrive → thư mục cài exe hiện tại (sau khi chuyển sang C:\Dev).</summary>
        private static string RemapToolPathAwayFromOneDrive(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            if (path.IndexOf("onedrive", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return path;
            }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            if (fileName.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
            {
                var mapped = Path.Combine(baseDir, "Tools", "ffmpeg", "bin", "ffmpeg.exe");
                return File.Exists(mapped) ? mapped : string.Empty;
            }

            if (fileName.Equals("yt-dlp.exe", StringComparison.OrdinalIgnoreCase))
            {
                var mapped = Path.Combine(baseDir, "yt-dlp.exe");
                return File.Exists(mapped) ? mapped : string.Empty;
            }

            if (fileName.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase))
            {
                var mapped = Path.Combine(baseDir, "Tools", "ffmpeg", "bin", "ffprobe.exe");
                return File.Exists(mapped) ? mapped : string.Empty;
            }

            const string marker = @"tiktok_Omni\tiktok_Omni\";
            var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var tail = path.Substring(idx + marker.Length);
                var mapped = Path.Combine(baseDir, tail);
                return File.Exists(mapped) || Directory.Exists(Path.GetDirectoryName(mapped) ?? mapped)
                    ? mapped
                    : string.Empty;
            }

            return string.Empty;
        }

        /// <summary>Đọc key/endpoint cũ từ appsettings.json (Lyria*) sang Tts*.</summary>
        private static void MigrateLegacyLyriaTtsSettings(AppSettings settings, string json)
        {
            if (settings == null || string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                var root = JObject.Parse(json);
                if (string.IsNullOrWhiteSpace(settings.TtsApiKey))
                {
                    var legacyKey = root["LyriaApiKey"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(legacyKey))
                    {
                        settings.TtsApiKey = legacyKey;
                    }
                }

                if (string.IsNullOrWhiteSpace(settings.TtsEndpoint))
                {
                    var legacyEndpoint = root["LyriaEndpoint"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(legacyEndpoint))
                    {
                        settings.TtsEndpoint = legacyEndpoint;
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore — Normalize will apply defaults.
            }
        }

        private static AppSettings Normalize(AppSettings settings)
        {
            settings.AiProvider = (settings.AiProvider ?? string.Empty).Trim();
            settings.AiModel = (settings.AiModel ?? string.Empty).Trim();
            settings.AiApiKey = (settings.AiApiKey ?? string.Empty).Trim();
            settings.TwoCaptchaApiKey = (settings.TwoCaptchaApiKey ?? string.Empty).Trim();
            settings.VeoApiKey = (settings.VeoApiKey ?? string.Empty).Trim();
            settings.TtsApiKey = (settings.TtsApiKey ?? string.Empty).Trim();
            settings.TtsEndpoint = (settings.TtsEndpoint ?? string.Empty).Trim();
            settings.TikTokRapidApiKey = (settings.TikTokRapidApiKey ?? string.Empty).Trim();
            settings.TikTokHuntMethod = NormalizeTikTokHuntMethod(settings.TikTokHuntMethod);
            settings.NotificationSmtpHost = (settings.NotificationSmtpHost ?? string.Empty).Trim();
            settings.NotificationSmtpUser = (settings.NotificationSmtpUser ?? string.Empty).Trim();
            settings.NotificationSmtpPassword = (settings.NotificationSmtpPassword ?? string.Empty).Trim();
            settings.NotificationFromEmail = (settings.NotificationFromEmail ?? string.Empty).Trim();
            settings.NotificationToEmail = (settings.NotificationToEmail ?? string.Empty).Trim();
            settings.NotificationWebhookUrl = (settings.NotificationWebhookUrl ?? string.Empty).Trim();
            settings.VideoBackgroundMusicFileName = (settings.VideoBackgroundMusicFileName ?? string.Empty).Trim();
            settings.FfmpegPath = RemapToolPathAwayFromOneDrive((settings.FfmpegPath ?? string.Empty).Trim());
            settings.YtDlpPath = RemapToolPathAwayFromOneDrive((settings.YtDlpPath ?? string.Empty).Trim());
            settings.VideoReupMusicLibraryPath = RemapToolPathAwayFromOneDrive((settings.VideoReupMusicLibraryPath ?? string.Empty).Trim());
            settings.CommentStyle = (settings.CommentStyle ?? string.Empty).Trim();
            settings.Profiles = settings.Profiles ?? new List<AutomationProfile>();

            if (string.IsNullOrWhiteSpace(settings.AiProvider))
            {
                settings.AiProvider = "gemini";
            }

            if (string.IsNullOrWhiteSpace(settings.AiModel))
            {
                settings.AiModel = "gemini-2.0-flash";
            }

            if (string.IsNullOrWhiteSpace(settings.CommentStyle))
            {
                settings.CommentStyle = "ngắn gọn, tự nhiên, đúng ngữ cảnh, không spam emoji";
            }

            if (settings.WatchSecondsMin < 3)
            {
                settings.WatchSecondsMin = 3;
            }

            if (settings.WatchSecondsMax < settings.WatchSecondsMin)
            {
                settings.WatchSecondsMax = settings.WatchSecondsMin;
            }

            const int maxWatchSeconds = 10800; // 180 minutes (UI uses minutes; JSON stays in seconds)
            if (settings.WatchSecondsMin > maxWatchSeconds)
            {
                settings.WatchSecondsMin = maxWatchSeconds;
            }

            if (settings.WatchSecondsMax > maxWatchSeconds)
            {
                settings.WatchSecondsMax = maxWatchSeconds;
            }

            if (settings.VideoTransitionDurationSeconds < 0.2d || settings.VideoTransitionDurationSeconds > 2.0d)
            {
                settings.VideoTransitionDurationSeconds = 0.6d;
            }

            if (settings.VideoTextSize < 24 || settings.VideoTextSize > 96)
            {
                settings.VideoTextSize = 50;
            }

            if (settings.VideoMusicVolume < 0 || settings.VideoMusicVolume > 100)
            {
                settings.VideoMusicVolume = 14;
            }

            if (settings.NotificationSmtpPort <= 0 || settings.NotificationSmtpPort > 65535)
            {
                settings.NotificationSmtpPort = 587;
            }

            // default: auto resume paused queue
            if (!settings.AutoResumeQueueOnStartup.HasValue)
            {
                settings.AutoResumeQueueOnStartup = true;
            }

            if (!settings.AlwaysRequirePrePostApproval.HasValue)
            {
                settings.AlwaysRequirePrePostApproval = true;
            }

            if (!settings.AlwaysRequirePreRenderApproval.HasValue)
            {
                settings.AlwaysRequirePreRenderApproval = false;
            }

            if (!settings.AutoRunApprovedQueue.HasValue)
            {
                settings.AutoRunApprovedQueue = false;
            }

            if (settings.BlockPostingSafetyScoreBelow < 0 || settings.BlockPostingSafetyScoreBelow > 100)
            {
                settings.BlockPostingSafetyScoreBelow = 75;
            }

            for (var i = settings.Profiles.Count - 1; i >= 0; i--)
            {
                var profile = settings.Profiles[i] ?? new AutomationProfile();
                profile.Name = (profile.Name ?? string.Empty).Trim();
                profile.ChromeUserDataPath = (profile.ChromeUserDataPath ?? string.Empty).Trim();
                profile.ProxyHost = (profile.ProxyHost ?? string.Empty).Trim();
                profile.ProxyUser = (profile.ProxyUser ?? string.Empty).Trim();
                profile.ProxyPass = (profile.ProxyPass ?? string.Empty).Trim();
                profile.UserAgent = (profile.UserAgent ?? string.Empty).Trim();
                profile.TikTokUniqueId = (profile.TikTokUniqueId ?? string.Empty).Trim();
                profile.TikTokNickname = TikTokDisplayNicknameSanitizer.Sanitize(
                    (profile.TikTokNickname ?? string.Empty).Trim(),
                    profile.TikTokUniqueId);
                profile.FacebookName = (profile.FacebookName ?? string.Empty).Trim();
                profile.YouTubeName = (profile.YouTubeName ?? string.Empty).Trim();
                profile.TikTokUserId = (profile.TikTokUserId ?? string.Empty).Trim();
                if (profile.ViewportWidth < 800 || profile.ViewportWidth > 3840)
                {
                    profile.ViewportWidth = 0;
                }
                if (profile.ViewportHeight < 600 || profile.ViewportHeight > 2160)
                {
                    profile.ViewportHeight = 0;
                }

                if (profile.ProxyPort < 0)
                {
                    profile.ProxyPort = 0;
                }

                // Drop completely empty rows (often added by grid UX).
                if (string.IsNullOrWhiteSpace(profile.Name) &&
                    string.IsNullOrWhiteSpace(profile.ChromeUserDataPath) &&
                    string.IsNullOrWhiteSpace(profile.ProxyHost) &&
                    profile.ProxyPort == 0 &&
                    string.IsNullOrWhiteSpace(profile.ProxyUser) &&
                    string.IsNullOrWhiteSpace(profile.ProxyPass) &&
                    string.IsNullOrWhiteSpace(profile.TikTokUniqueId) &&
                    string.IsNullOrWhiteSpace(profile.TikTokNickname) &&
                    string.IsNullOrWhiteSpace(profile.FacebookName) &&
                    string.IsNullOrWhiteSpace(profile.YouTubeName) &&
                    string.IsNullOrWhiteSpace(profile.TikTokUserId))
                {
                    settings.Profiles.RemoveAt(i);
                    continue;
                }

                settings.Profiles[i] = profile;
            }

            return settings;
        }

        private static string NormalizeTikTokHuntMethod(string method)
        {
            if (TikTokHuntMethods.IsRapidApi(method))
            {
                return TikTokHuntMethods.RapidApi;
            }

            return TikTokHuntMethods.Browser;
        }

        private static AutomationProfile ResolveProfile(AppSettings settings, string runningProfileName)
        {
            var profiles = settings?.Profiles;
            if (profiles == null || profiles.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(runningProfileName))
            {
                for (var i = 0; i < profiles.Count; i++)
                {
                    var profile = profiles[i];
                    if (profile != null &&
                        string.Equals(profile.Name, runningProfileName.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return profile;
                    }
                }
            }

            for (var i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null)
                {
                    return profiles[i];
                }
            }

            return null;
        }

        private static int BuildStableSeed(string profileName)
        {
            var key = string.IsNullOrWhiteSpace(profileName) ? "default" : profileName.Trim().ToLowerInvariant();
            unchecked
            {
                var hash = 2166136261;
                for (var i = 0; i < key.Length; i++)
                {
                    hash ^= key[i];
                    hash *= 16777619;
                }
                return (int)(hash & 0x7fffffff);
            }
        }

        private static (int width, int height) BuildStableViewport(int seed)
        {
            var pool = new[]
            {
                (1280, 720),
                (1366, 768),
                (1440, 900),
                (1536, 864),
                (1600, 900)
            };

            var selected = pool[seed % pool.Length];
            var widthJitter = (seed % 5) * 4; // 0..16
            var heightJitter = ((seed / 11) % 5) * 3; // 0..12
            return (selected.Item1 + widthJitter, selected.Item2 + heightJitter);
        }
    }

    public class AppSettings
    {
        public string AiProvider { get; set; } = "gemini";
        public string AiModel { get; set; } = "gemini-2.0-flash";
        public string AiApiKey { get; set; } = string.Empty;
        public string TwoCaptchaApiKey { get; set; } = string.Empty;
        public string VeoApiKey { get; set; } = string.Empty;
        public string TtsApiKey { get; set; } = string.Empty;
        public string VeoEndpoint { get; set; } = "https://api.veo.example.com/v1/videos";
        public string TtsEndpoint { get; set; } = "https://api.example.com/v1/tts/synthesize";
        public bool NotificationEnabled { get; set; } = false;
        public bool NotificationEmailEnabled { get; set; } = false;
        public string NotificationSmtpHost { get; set; } = "smtp.gmail.com";
        public int NotificationSmtpPort { get; set; } = 587;
        public bool NotificationUseSsl { get; set; } = true;
        public string NotificationSmtpUser { get; set; } = string.Empty;
        public string NotificationSmtpPassword { get; set; } = string.Empty;
        public string NotificationFromEmail { get; set; } = string.Empty;
        public string NotificationToEmail { get; set; } = string.Empty;
        public bool NotificationWebhookEnabled { get; set; } = false;
        public string NotificationWebhookUrl { get; set; } = string.Empty;
        public string FfmpegPath { get; set; } = string.Empty;
        public string YtDlpPath { get; set; } = string.Empty;

        /// <summary>RootPath — gốc lưu video: {StorageRootPath}\{Profile}\{VideoType}\</summary>
        public string StorageRootPath { get; set; } = string.Empty;

        /// <summary>Số job chạy song song tối đa (render / hunt / đăng bài).</summary>
        public int MaxConcurrentJobs { get; set; } = 2;

        /// <summary>Thư mục chứa .mp3 cho tab Video reup (Affiliate). Để trống = [exe]\VideoReup\Music.</summary>
        public string VideoReupMusicLibraryPath { get; set; } = string.Empty;

        /// <summary>File âm thanh Hook SFX 3s (Reup) — mp3/wav trong VideoReup\Hooks.</summary>
        public string ReupVisualHookSfxPath { get; set; } = string.Empty;

        public bool ReupUseVisualHookSfx { get; set; }

        /// <summary>Knowledge | Review | Storytelling — mẫu prompt Gemini khi render.</summary>
        public string GeminiStyleTemplate { get; set; } = "Storytelling";

        /// <summary>Ngày (yyyy-MM-dd) đã cào báo cáo Affiliate gần nhất — tránh cào trùng trong ngày.</summary>
        public string LastAffiliateRevenueFetchDate { get; set; } = string.Empty;

        /// <summary>Mở Chrome tự động 1 lần/ngày để cào báo cáo Affiliate khi khởi động app (mặc định tắt).</summary>
        public bool? AutoFetchAffiliateRevenueOnStartup { get; set; } = false;
        /// <summary>Total watch seconds (all videos combined) per warm-up session — minimum.</summary>
        public int WatchSecondsMin { get; set; } = 7;
        /// <summary>Total watch seconds (all videos combined) per warm-up session — maximum.</summary>
        public int WatchSecondsMax { get; set; } = 18;
        public double VideoTransitionDurationSeconds { get; set; } = 0.6d;
        public int VideoTextSize { get; set; } = 50;
        public int VideoMusicVolume { get; set; } = 14;
        public string VideoBackgroundMusicFileName { get; set; } = string.Empty;
        public bool VideoMusicRandomizeStartTime { get; set; } = true;
        public bool? AutoResumeQueueOnStartup { get; set; } = true;
        public bool? AlwaysRequirePrePostApproval { get; set; } = true;
        public bool? AlwaysRequirePreRenderApproval { get; set; } = false;
        public int BlockPostingSafetyScoreBelow { get; set; } = 75;
        public bool? AutoRunApprovedQueue { get; set; } = false;
        public string CommentStyle { get; set; } = "ngắn gọn, tự nhiên, đúng ngữ cảnh, không spam emoji";
        /// <summary>Bật tự động enrich (Metrics + Anchor) sau khi Hunt Affiliate hoàn tất.</summary>
        public bool AffiliateAutoEnrichEnabled { get; set; } = true;

        /// <summary>
        /// Chế độ Video: săn thêm ứng viên (buffer), gọi TikWM để chấm engagement, giữ top «Max Results».
        /// </summary>
        public bool AffiliateRankByEngagementEnabled { get; set; } = true;

        /// <summary>Hệ số nhân buffer so với Max Results (ví dụ 2.5 × 20 → săn tối đa ~50 rồi cắt còn 20).</summary>
        public double AffiliateHuntBufferMultiplier { get; set; } = 2.5d;

        /// <summary>RapidAPI key cho tiktok-api23 (săn video TikTok).</summary>
        public string TikTokRapidApiKey { get; set; } = string.Empty;

        /// <summary>Phương thức săn TikTok Video: RapidApi hoặc Browser.</summary>
        public string TikTokHuntMethod { get; set; } = TikTokHuntMethods.RapidApi;

        /// <summary>Khi RapidAPI lỗi, tự chuyển sang Playwright/Chrome.</summary>
        public bool TikTokRapidApiFallbackToBrowser { get; set; } = true;

        public List<AutomationProfile> Profiles { get; set; } = new List<AutomationProfile>();
    }

    public static class TikTokHuntMethods
    {
        public const string RapidApi = "RapidApi";
        public const string Browser = "Browser";

        public static bool IsRapidApi(string method) =>
            string.Equals(method, RapidApi, StringComparison.OrdinalIgnoreCase);
    }

    public class AutomationProfile
    {
        public string Name { get; set; } = string.Empty;
        public string ChromeUserDataPath { get; set; } = string.Empty;
        public string ProxyHost { get; set; } = string.Empty;
        public int ProxyPort { get; set; }
        public string ProxyUser { get; set; } = string.Empty;
        public string ProxyPass { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public int ViewportWidth { get; set; }
        public int ViewportHeight { get; set; }

        /// <summary>Filled after browser login (TikTok handle without @).</summary>
        public string TikTokUniqueId { get; set; } = string.Empty;

        /// <summary>TikTok display name (not the app profile name in <see cref="Name"/>).</summary>
        public string TikTokNickname { get; set; } = string.Empty;

        /// <summary>Fanpage / channel label for Facebook (user-editable memo).</summary>
        public string FacebookName { get; set; } = string.Empty;

        /// <summary>YouTube channel label (user-editable memo).</summary>
        public string YouTubeName { get; set; } = string.Empty;

        /// <summary>Internal user id from TikTok page state when available.</summary>
        public string TikTokUserId { get; set; } = string.Empty;

        /// <summary>Đã đăng nhập TikTok (cookie sessionid / @nick) — quét từ user-data-dir.</summary>
        public bool IsTTLoggedIn { get; set; }

        /// <summary>Đã đăng nhập Facebook (cookie c_user) trong user-data-dir của profile.</summary>
        public bool IsFBLoggedIn { get; set; }

        /// <summary>Đã đăng nhập YouTube/Google trong user-data-dir của profile.</summary>
        public bool IsYTLoggedIn { get; set; }

        /// <summary>Voice ID / tên giọng TTS (Lyria, Google TTS, v.v.) cho video Triết lý.</summary>
        public string VoiceId { get; set; } = string.Empty;

        /// <summary>Phong cách video (mô tả Veo/gradient): warm, dark, neon…</summary>
        public string VideoStyle { get; set; } = string.Empty;

        /// <summary>Persona / phong cách Mascot Story (đưa vào prompt Gemini — mỗi nick một style).</summary>
        public string MascotStyle { get; set; } = string.Empty;

        /// <summary>DNA kênh Mascot (giọng nam/nữ, vui/buồn…) — đồng bộ với <see cref="MascotStyle"/>.</summary>
        public string MascotPersonality
        {
            get => MascotStyle;
            set => MascotStyle = value ?? string.Empty;
        }
    }

    /// <summary>Snapshot read from TikTok web after successful login.</summary>
    public class TikTokAccountSnapshot
    {
        public string UniqueId { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Drops sidebar/header UI strings mistaken for a TikTok display name (e.g. English «Profile»).
    /// </summary>
    internal static class TikTokDisplayNicknameSanitizer
    {
        private static readonly HashSet<string> UiPlaceholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "profile",
            "view profile",
            "my profile",
            "go to profile",
            "your profile",
            "account",
            "accounts",
            "account center",
            "hồ sơ",
            "xem hồ sơ",
            "hồ sơ của bạn",
            "tài khoản",
            "trang cá nhân",
            "cá nhân"
        };

        public static string Sanitize(string nickname, string uniqueId)
        {
            if (string.IsNullOrWhiteSpace(nickname))
            {
                return string.Empty;
            }

            var t = nickname.Trim();
            if (t.Length <= 48 && UiPlaceholders.Contains(t))
            {
                return string.Empty;
            }

            var h = (uniqueId ?? string.Empty).Trim().TrimStart('@');
            if (h.Length > 0)
            {
                if (string.Equals(t, h, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                if (string.Equals(t, "@" + h, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }
            }

            return t;
        }
    }
}
