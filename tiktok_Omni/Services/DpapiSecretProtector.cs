using System;
using System.Security.Cryptography;
using System.Text;

namespace tiktok_Omni.Services
{
    /// <summary>
    /// Encrypts secrets at rest under the current Windows user (DPAPI).
    /// Legacy plaintext values in appsettings.json remain readable until next save.
    /// </summary>
    public static class DpapiSecretProtector
    {
        private const string Prefix = "DPAPI1:";

        public static string ProtectForStorage(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return plainText;
            }

            if (plainText.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return plainText;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(plainText);
                var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                return Prefix + Convert.ToBase64String(protectedBytes);
            }
            catch (CryptographicException)
            {
                return plainText;
            }
        }

        public static string UnprotectAfterLoad(string stored)
        {
            if (string.IsNullOrEmpty(stored))
            {
                return stored;
            }

            if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return stored;
            }

            try
            {
                var raw = Convert.FromBase64String(stored.Substring(Prefix.Length));
                var bytes = ProtectedData.Unprotect(raw, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                return stored;
            }
            catch (CryptographicException)
            {
                return stored;
            }
        }
    }
}
