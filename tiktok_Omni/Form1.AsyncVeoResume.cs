using System;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private async Task ResumeAsyncVeoTasksOnStartupAsync()
        {
            var pending = _asyncTasksRebootStore.GetProcessingEntries();
            if (pending.Count == 0)
            {
                return;
            }

            AppSettings settings;
            try
            {
                settings = await _configManager.LoadAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log("[VeoResume] Không nạp settings: " + ex.Message);
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.VeoApiKey) || string.IsNullOrWhiteSpace(settings.VeoEndpoint))
            {
                Log($"[VeoResume] Còn {pending.Count} task trong async_tasks_reboot.json — cần Veo API (tab Cài đặt) để resume.");
                return;
            }

            Log($"[VeoResume] Poll tiếp {pending.Count} Veo task (Processing) — không submit mới.");
            var mascotWorker = new MascotWorker(new VideoService(), _asyncTasksRebootStore);

            foreach (var entry in pending)
            {
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20)))
                    {
                        var url = await mascotWorker.ResumeVeoTaskAsync(
                            entry,
                            settings.VeoApiKey,
                            settings.VeoEndpoint,
                            cts.Token,
                            Log,
                            phase => Log("[VeoResume] " + (phase ?? string.Empty))).ConfigureAwait(true);

                        Log("[VeoResume] Task " + entry.TaskId + " OK → " + (url.Length > 80 ? url.Substring(0, 80) + "…" : url));
                    }
                }
                catch (Exception ex)
                {
                    Log("[VeoResume] Task " + entry.TaskId + ": " + ex.Message);
                }
            }
        }
    }
}
