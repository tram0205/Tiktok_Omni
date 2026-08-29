using System;
using System.Threading;
using System.Threading.Tasks;
using tiktok_Omni.Models;

namespace tiktok_Omni.Services
{
    /// <summary>Facade tab Triết lý — pipeline C# (không gọi Python).</summary>
    public sealed class PhilosophyVideoService
    {
        private readonly PhilosophyVideoPipelineService _pipeline = new PhilosophyVideoPipelineService();

        public static string DescribeBlockers(AppSettings settings)
        {
            return PhilosophyVideoPipelineService.DescribeBlockers(settings);
        }

        public async Task<PhilosophyVideoResult> GenerateFromScriptAsync(
            PhilosophyScriptItem item,
            PhilosophyRenderOptions renderOptions,
            AppSettings settings,
            AutomationProfile profile,
            Action<string> log,
            Action<string, int> progress,
            CancellationToken cancellationToken = default)
        {
            return await _pipeline.RunScriptAsync(
                item,
                renderOptions,
                settings,
                profile,
                log,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
