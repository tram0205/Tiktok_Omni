using System;
using System.Threading;
using System.Threading.Tasks;

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

        public async Task<PhilosophyVideoResult> GenerateAsync(
            string inputTextOrUrl,
            AppSettings settings,
            Action<string> log,
            Action<string, int> progress,
            CancellationToken cancellationToken = default)
        {
            return await _pipeline.RunAsync(
                inputTextOrUrl,
                settings,
                log,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
