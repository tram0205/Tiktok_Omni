using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services.Mascot
{
    internal static class MascotMediaHelper
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        public static async Task DownloadAsync(string url, string outputPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL is empty.", nameof(url));
            if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(new Uri(url).LocalPath, outputPath, true);
                return;
            }
            if (File.Exists(url))
            {
                File.Copy(url, outputPath, true);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
            using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var file = File.Create(outputPath))
                {
                    await stream.CopyToAsync(file).ConfigureAwait(false);
                }
            }
        }

        public static string BuildImageDataUrl(string imagePath)
        {
            var bytes = File.ReadAllBytes(imagePath);
            var ext = Path.GetExtension(imagePath)?.TrimStart('.').ToLowerInvariant() ?? "jpeg";
            if (ext == "jpg") ext = "jpeg";
            return "data:image/" + ext + ";base64," + Convert.ToBase64String(bytes);
        }
    }
}