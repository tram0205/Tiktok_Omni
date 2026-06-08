using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace tiktok_Omni.Services
{
    /// <summary>Kho video hook ngắn (B-Roll) — chèn làm cảnh mở đầu Affiliate Deep.</summary>
    public static class HookBRollLibraryService
    {
        private static readonly string[] SupportedExtensions = { ".mp4", ".mov", ".webm" };

        /// <summary>
        /// Lấy ngẫu nhiên một file hook từ <see cref="ProfileScopedPaths.GetHookBRollsDirectory"/>.
        /// Trả về null nếu thư mục không tồn tại hoặc không có file hợp lệ — không ném lỗi.
        /// </summary>
        public static Task<string> GetRandomHookBRollAsync(
            Random random = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetRandomHookBRoll(random));
        }

        public static string GetRandomHookBRoll(Random random = null)
        {
            try
            {
                var dir = ProfileScopedPaths.GetHookBRollsDirectory(ensureExists: false);
                if (!Directory.Exists(dir))
                {
                    return null;
                }

                var files = Directory
                    .EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(path =>
                    {
                        var ext = Path.GetExtension(path);
                        return !string.IsNullOrWhiteSpace(ext) &&
                               SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
                    })
                    .Where(path =>
                    {
                        try
                        {
                            return new FileInfo(path).Length > 2048;
                        }
                        catch
                        {
                            return false;
                        }
                    })
                    .ToList();

                if (files.Count == 0)
                {
                    return null;
                }

                var rnd = random ?? new Random();
                return files[rnd.Next(files.Count)];
            }
            catch
            {
                return null;
            }
        }
    }
}
