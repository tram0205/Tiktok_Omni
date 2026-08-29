using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    public sealed class PhilosophySessionRestoreSummary
    {
        public int AudioSessionsFound { get; set; }

        public int OutputsAlreadyLinked { get; set; }

        public int OutputsNewlyLinked { get; set; }

        public int StillNeedLink { get; set; }

        public int OrphanOutputs { get; set; }
    }

    /// <summary>Gắn phiên render / preview âm thanh từ đĩa vào batch trên lưới.</summary>
    public static class PhilosophyBatchSessionRestoreHelper
    {
        public static PhilosophySessionRestoreSummary RestoreFromDisk(
            IList<PhilosophyBatchItem> batches,
            AppSettings settings,
            Action<string> log)
        {
            var summary = new PhilosophySessionRestoreSummary();
            if (batches == null || batches.Count == 0)
            {
                return summary;
            }

            ProfileScopedPaths.SetConfiguredStorageRoot(settings?.StorageRootPath);
            var linkedOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var batch in batches)
            {
                if (batch == null)
                {
                    continue;
                }

                var sessionBase = PhilosophyBatchAudioPreviewHelper.GetSessionBase(batch, settings);
                if (PhilosophyBatchAudioPreviewHelper.HasVoicePreview(sessionBase))
                {
                    summary.AudioSessionsFound++;
                }

                foreach (var quote in batch.Quotes ?? Enumerable.Empty<PhilosophyScriptItem>())
                {
                    var path = (quote?.OutputPath ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    {
                        continue;
                    }

                    linkedOutputs.Add(Path.GetFullPath(path));
                    summary.OutputsAlreadyLinked++;
                }
            }

            var orphanOutputs = new List<(string Path, DateTime TimeUtc)>();
            foreach (var outputRoot in PhilosophyVideoPipelineService.EnumerateFinishedProductSearchRoots())
            {
                if (!Directory.Exists(outputRoot))
                {
                    continue;
                }

                foreach (var branded in Directory.GetFiles(outputRoot, "philosophy_video_branded.mp4", SearchOption.AllDirectories))
                {
                    var full = Path.GetFullPath(branded);
                    if (linkedOutputs.Contains(full))
                    {
                        continue;
                    }

                    orphanOutputs.Add((full, File.GetLastWriteTimeUtc(branded)));
                }
            }

            orphanOutputs.Sort((a, b) => b.TimeUtc.CompareTo(a.TimeUtc));
            summary.OrphanOutputs = orphanOutputs.Count;

            var needLink = batches
                .SelectMany(b => b?.Quotes ?? Enumerable.Empty<PhilosophyScriptItem>())
                .Where(q => q != null && !string.IsNullOrWhiteSpace(q.Content))
                .Where(q =>
                {
                    var path = (q.OutputPath ?? string.Empty).Trim();
                    return string.IsNullOrEmpty(path) || !File.Exists(path);
                })
                .ToList();

            foreach (var quote in needLink)
            {
                var prefix = PhilosophySceneHelper.BuildFilePrefix(quote.Content);
                for (var i = 0; i < orphanOutputs.Count; i++)
                {
                    var candidate = orphanOutputs[i];
                    if (!StageMatchesQuote(Path.GetDirectoryName(candidate.Path), prefix))
                    {
                        continue;
                    }

                    quote.OutputPath = candidate.Path;
                    quote.Status = "Xong";
                    quote.LastError = string.Empty;
                    linkedOutputs.Add(candidate.Path);
                    orphanOutputs.RemoveAt(i);
                    summary.OutputsNewlyLinked++;
                    log?.Invoke("[Quote] Gắn thành phẩm: " +
                                PhilosophyBatchHelper.TrimGridLabel(quote.Content, 48, "—"));
                    break;
                }
            }

            summary.StillNeedLink = batches
                .SelectMany(b => b?.Quotes ?? Enumerable.Empty<PhilosophyScriptItem>())
                .Count(q =>
                {
                    if (q == null || string.IsNullOrWhiteSpace(q.Content))
                    {
                        return false;
                    }

                    var path = (q.OutputPath ?? string.Empty).Trim();
                    return string.IsNullOrEmpty(path) || !File.Exists(path);
                });

            foreach (var batch in batches)
            {
                batch?.RefreshDerivedFields();
            }

            return summary;
        }

        private static bool StageMatchesQuote(string stageDir, string prefix)
        {
            if (string.IsNullOrWhiteSpace(stageDir) || string.IsNullOrWhiteSpace(prefix))
            {
                return false;
            }

            if (stageDir.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            try
            {
                foreach (var file in Directory.GetFiles(stageDir, "*.*", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileNameWithoutExtension(file) ?? string.Empty;
                    if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }
    }
}
