using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    public class PhilosophyVideoService
    {
        public async Task<string> GenerateAsync(
            string inputTextOrUrl,
            Action<string> logAction,
            CancellationToken cancellationToken)
        {
            var input = (inputTextOrUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new InvalidOperationException("Philosophy input is empty.");
            }

            var workspaceRoot = ResolveWorkspaceRoot();
            var moduleDir = Path.Combine(workspaceRoot, "philosophy_video_module");
            if (!Directory.Exists(moduleDir))
            {
                throw new InvalidOperationException("Missing philosophy_video_module folder. Please make sure module is present.");
            }

            var outputDir = Path.Combine(workspaceRoot, "philosophy_video_outputs", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(outputDir);

            var escapedInput = input.Replace("\"", "\\\"");
            var escapedOutput = outputDir.Replace("\"", "\\\"");
            var args = $"-m philosophy_video_module.main --input \"{escapedInput}\" --output-dir \"{escapedOutput}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = args,
                WorkingDirectory = workspaceRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                var outTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();

                while (!process.HasExited)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(120, cancellationToken).ConfigureAwait(false);
                }

                var outText = await outTask.ConfigureAwait(false);
                var errText = await errTask.ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(outText))
                {
                    logAction?.Invoke("[Philosophy] " + outText.Trim());
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("Philosophy module failed: " + (string.IsNullOrWhiteSpace(errText) ? outText : errText));
                }

                var outputPath = ParseOutputPath(outText);
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    outputPath = Path.Combine(outputDir, "philosophy_video.mp4");
                }

                if (!File.Exists(outputPath))
                {
                    throw new InvalidOperationException("Philosophy module completed but output file not found.");
                }

                return outputPath;
            }
        }

        private static string ResolveWorkspaceRoot()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            var dir = new DirectoryInfo(current);
            while (dir != null)
            {
                var module = Path.Combine(dir.FullName, "philosophy_video_module");
                var sln = Path.Combine(dir.FullName, "tiktok_Omni.sln");
                if (Directory.Exists(module) && File.Exists(sln))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            return Directory.GetCurrentDirectory();
        }

        private static string ParseOutputPath(string stdOut)
        {
            var text = stdOut ?? string.Empty;
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("Output:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var path = trimmed.Substring("Output:".Length).Trim();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }
    }
}
