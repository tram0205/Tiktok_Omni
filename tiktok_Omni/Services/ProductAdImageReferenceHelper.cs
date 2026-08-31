using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace tiktok_Omni.Services
{
    public static class ProductAdImageReferenceHelper
    {
        public static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        public static string OpenFileFilter =>
            "Ảnh mẫu (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|Tất cả tệp (*.*)|*.*";

        public static bool IsSupportedImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var ext = Path.GetExtension(path);
            return SupportedExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetRefsDirectory(string profileName, string storageRoot = null, bool create = true)
        {
            return ProfileScopedPaths.GetProductAdImageRefsDirectory(storageRoot, profileName, create);
        }

        public static string CopyMasterImage(string sourcePath, string profileName, string storageRoot = null)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Không tìm thấy ảnh mẫu.", sourcePath);
            }

            if (!IsSupportedImage(sourcePath))
            {
                throw new InvalidOperationException("Chỉ nhận file jpg / png / webp.");
            }

            var refsDir = GetRefsDirectory(profileName, storageRoot, create: true);
            var ext = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".jpg";
            }

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var baseName = SanitizeFileStem(Path.GetFileNameWithoutExtension(sourcePath));
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "master";
            }

            var dest = Path.Combine(refsDir, baseName + "_" + stamp + ext);
            var n = 1;
            while (File.Exists(dest))
            {
                dest = Path.Combine(refsDir, baseName + "_" + stamp + "_" + n.ToString(CultureInfo.InvariantCulture) + ext);
                n++;
            }

            File.Copy(sourcePath, dest, overwrite: false);
            return Path.GetFullPath(dest);
        }

        /// <summary>Copy ảnh master sang refs của profile mới, rồi xoá bản trong refs cũ (nếu đang nằm trong product_ad_image\refs).</summary>
        public static string RelocateMasterImage(string sourcePath, string newProfileName, string storageRoot = null)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return sourcePath ?? string.Empty;
            }

            var fullSource = Path.GetFullPath(sourcePath);
            var newRefs = Path.GetFullPath(GetRefsDirectory(newProfileName, storageRoot, create: true));
            if (fullSource.StartsWith(newRefs + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetDirectoryName(fullSource), newRefs, StringComparison.OrdinalIgnoreCase))
            {
                return fullSource;
            }

            var dest = CopyMasterImage(fullSource, newProfileName, storageRoot);
            if (IsUnderProductAdImageRefs(fullSource)
                && !string.Equals(fullSource, dest, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(fullSource);
                }
                catch
                {
                    // giữ file cũ nếu đang mở / không xoá được
                }
            }

            return dest;
        }

        public static bool IsUnderProductAdImageRefs(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                var refsDir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (string.IsNullOrWhiteSpace(refsDir))
                {
                    return false;
                }

                var refsName = Path.GetFileName(refsDir);
                var parentName = Path.GetFileName(Path.GetDirectoryName(refsDir));
                return string.Equals(refsName, ProfileScopedPaths.ProductAdImageRefsFolderName, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(parentName, ProfileScopedPaths.ProductAdImageFolderName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void OpenRefsFolder(string profileName, string storageRoot = null)
        {
            var dir = GetRefsDirectory(profileName, storageRoot, create: true);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }

        public static string SanitizeFileStem(string raw)
        {
            var s = ProfileScopedPaths.SanitizeSegment(raw ?? string.Empty);
            if (s.Length > 40)
            {
                s = s.Substring(0, 40);
            }

            return s;
        }
    }
}
