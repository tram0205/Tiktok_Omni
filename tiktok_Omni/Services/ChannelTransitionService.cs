using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    public sealed class ChannelTransitionService
    {
        private readonly WarmupStateManager _warmupStateManager;
        private readonly ConfigManager _configManager;
        private readonly NotificationService _notificationService;

        public ChannelTransitionService(
            WarmupStateManager warmupStateManager,
            ConfigManager configManager,
            NotificationService notificationService)
        {
            _warmupStateManager = warmupStateManager ?? throw new ArgumentNullException(nameof(warmupStateManager));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        public async Task<ChannelTransitionResult> EvaluateAndTransitionAsync(
            ChannelHealthSnapshot snapshot,
            AppSettings settings,
            Action<string> logAction = null,
            CancellationToken cancellationToken = default)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            settings = settings ?? await _configManager.LoadAsync().ConfigureAwait(false);
            var profileName = ProfileScopedPaths.ResolveProfileName(snapshot.ProfileName);
            var result = new ChannelTransitionResult
            {
                ProfileName = profileName,
                Snapshot = snapshot
            };

            var states = await _warmupStateManager.LoadProfileChannelStatesAsync().ConfigureAwait(false);
            if (!states.TryGetValue(profileName, out var state) || state == null)
            {
                state = new ProfileChannelState
                {
                    ProfileName = profileName,
                    Mode = ProfileOperationalMode.Posting
                };
                states[profileName] = state;
            }

            state.LastHealthCheckUtc = DateTime.UtcNow;
            state.LastSnapshot = snapshot;

            if (!snapshot.SuspectedShadowban)
            {
                if (state.Mode == ProfileOperationalMode.HighIntensityWarmup)
                {
                    logAction?.Invoke("[ChannelHealth] Profile «" + profileName + "» đã hồi phục — giữ Warm-up cường độ cao cho đến khi bạn chuyển thủ công.");
                }
                else
                {
                    state.Mode = ProfileOperationalMode.Posting;
                }

                await _warmupStateManager.SaveProfileChannelStatesAsync(states.Values).ConfigureAwait(false);
                result.Action = ChannelTransitionAction.None;
                result.Message = snapshot.Summary;
                return result;
            }

            if (state.Mode == ProfileOperationalMode.HighIntensityWarmup)
            {
                await _warmupStateManager.SaveProfileChannelStatesAsync(states.Values).ConfigureAwait(false);
                result.Action = ChannelTransitionAction.AlreadyInHighIntensityWarmup;
                result.Message = "Profile đã ở chế độ Warm-up cường độ cao.";
                return result;
            }

            var cooldownHours = Math.Max(1, settings.ChannelHealthTransitionCooldownHours);
            if (state.LastTransitionUtc.HasValue)
            {
                var elapsed = DateTime.UtcNow - state.LastTransitionUtc.Value;
                if (elapsed.TotalHours < cooldownHours)
                {
                    await _warmupStateManager.SaveProfileChannelStatesAsync(states.Values).ConfigureAwait(false);
                    result.Action = ChannelTransitionAction.CooldownActive;
                    result.Message = "Bỏ qua chuyển trạng thái — cooldown "
                        + Math.Ceiling(cooldownHours - elapsed.TotalHours) + "h còn lại.";
                    logAction?.Invoke("[ChannelHealth] «" + profileName + "»: " + result.Message);
                    return result;
                }
            }

            state.Mode = ProfileOperationalMode.HighIntensityWarmup;
            state.LastTransitionUtc = DateTime.UtcNow;
            await _warmupStateManager.SaveProfileChannelStatesAsync(states.Values).ConfigureAwait(false);

            var warmupJob = BuildHighIntensityWarmupState(profileName, settings);
            result.Action = ChannelTransitionAction.TransitionedToHighIntensityWarmup;
            result.WarmupJobToEnqueue = warmupJob;
            result.Message = "Kênh @" + snapshot.TikTokUniqueId + " bị shadowban — đã chuyển sang Warm-up cường độ cao.";

            var notifyBody = "Kênh [" + profileName + " / @" + snapshot.TikTokUniqueId + "] bị shadowban "
                + "(view TB " + snapshot.RecentVideosAvgViews + " < " + snapshot.MinViewsThreshold + "), "
                + "đã tự động chuyển sang chế độ Warm-up cường độ cao!";
            logAction?.Invoke("[ChannelHealth] " + notifyBody);

            await _notificationService.SendAsync(
                settings,
                new NotificationMessage
                {
                    EventType = "channel_health_shadowban",
                    Severity = "warning",
                    Title = "Điều phối kênh — chuyển Warm-up",
                    Body = notifyBody,
                    ProfileName = profileName
                },
                logAction,
                cancellationToken).ConfigureAwait(false);

            return result;
        }

        public async Task<IReadOnlyList<ChannelTransitionResult>> ProcessSnapshotsAsync(
            IEnumerable<ChannelHealthSnapshot> snapshots,
            AppSettings settings,
            Action<string> logAction = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<ChannelTransitionResult>();
            foreach (var snapshot in snapshots ?? Enumerable.Empty<ChannelHealthSnapshot>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (snapshot == null)
                {
                    continue;
                }

                var result = await EvaluateAndTransitionAsync(snapshot, settings, logAction, cancellationToken).ConfigureAwait(false);
                results.Add(result);
            }

            return results;
        }

        private static WarmupRunState BuildHighIntensityWarmupState(string profileName, AppSettings settings)
        {
            var watchMin = Math.Max(3, settings?.ChannelHealthHighIntensityWatchSecondsMin ?? 45);
            var watchMax = Math.Max(watchMin, settings?.ChannelHealthHighIntensityWatchSecondsMax ?? 120);
            var videoCount = Math.Max(1, settings?.ChannelHealthHighIntensityVideoCount ?? 15);
            var keywords = (settings?.ChannelHealthWarmupKeywords ?? "tiktok shop affiliate").Trim();
            if (string.IsNullOrWhiteSpace(keywords))
            {
                keywords = "tiktok shop affiliate";
            }

            return new WarmupRunState
            {
                RunningProfileName = profileName,
                Keywords = keywords,
                VideoCount = videoCount,
                WatchSecondsMin = watchMin,
                WatchSecondsMax = watchMax,
                AutoComment = false,
                DryRun = false,
                CompletedCount = 0,
                LastUpdatedUtc = DateTime.UtcNow
            };
        }
    }

    public enum ChannelTransitionAction
    {
        None = 0,
        TransitionedToHighIntensityWarmup = 1,
        AlreadyInHighIntensityWarmup = 2,
        CooldownActive = 3
    }

    public sealed class ChannelTransitionResult
    {
        public string ProfileName { get; set; } = string.Empty;
        public ChannelHealthSnapshot Snapshot { get; set; }
        public ChannelTransitionAction Action { get; set; } = ChannelTransitionAction.None;
        public string Message { get; set; } = string.Empty;
        public WarmupRunState WarmupJobToEnqueue { get; set; }
    }
}
