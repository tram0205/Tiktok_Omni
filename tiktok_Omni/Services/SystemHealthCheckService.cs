using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    public sealed class SystemHealthCheckService
    {
        private readonly ConfigManager _configManager;
        private readonly GeminiService _geminiService;

        public SystemHealthCheckService(ConfigManager configManager, GeminiService geminiService)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
        }

        public async Task<Dictionary<string, bool>> PerformSystemHealthCheckAsync(CancellationToken cancellationToken = default)
        {
            var settings = await _configManager.LoadAsync().ConfigureAwait(false);
            var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["ffmpeg"] = CheckFfmpegExecutable(settings),
                ["ytdlp"] = CheckYtDlpExecutable(settings),
                ["api_keys"] = await CheckApiKeysAsync(settings, cancellationToken).ConfigureAwait(false),
                ["storage"] = CheckStorageWritable(settings)
            };
            return result;
        }

        private static bool CheckFfmpegExecutable(AppSettings settings)
        {
            var path = (settings?.FfmpegPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return true;
            }

            var bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            return File.Exists(bundled);
        }

        private static bool CheckYtDlpExecutable(AppSettings settings)
        {
            try
            {
                var path = YtDlpToolResolver.Resolve(settings, null);
                return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> CheckApiKeysAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings?.AiApiKey))
            {
                return false;
            }

            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(12));
                    var reply = await _geminiService.GenerateScriptAsync(
                        "Reply with exactly one word: OK",
                        settings.AiProvider,
                        settings.AiApiKey,
                        settings.AiModel,
                        cts.Token).ConfigureAwait(false);
                    return !string.IsNullOrWhiteSpace(reply);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckStorageWritable(AppSettings settings)
        {
            try
            {
                var root = ProfileScopedPaths.ResolveStorageRoot(settings?.StorageRootPath);
                Directory.CreateDirectory(root);
                var probe = Path.Combine(root, ".health_write_probe");
                File.WriteAllText(probe, DateTime.UtcNow.ToString("O"), TextFileEncoding.Utf8NoBom);
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
