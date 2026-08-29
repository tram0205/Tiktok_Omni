using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace tiktok_Omni.Services
{
    public class DuplicateGuardRecord
    {
        public string Type { get; set; } = string.Empty; // render | post
        public string Fingerprint { get; set; } = string.Empty;
        public string Profile { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class DuplicateGuardManager
    {
        private const string FileName = "duplicate_guard.json";
        private const int MaxRecords = 2000;

        public async Task<bool> ExistsRecentAsync(string type, string fingerprint, TimeSpan lookbackWindow)
        {
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(fingerprint))
            {
                return false;
            }

            var records = await LoadAsync().ConfigureAwait(false);
            var from = DateTime.UtcNow.Subtract(lookbackWindow);
            return records.Any(x =>
                string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase) &&
                x.CreatedAtUtc >= from);
        }

        public async Task AddAsync(DuplicateGuardRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Fingerprint))
            {
                return;
            }

            var records = await LoadAsync().ConfigureAwait(false);
            records.Insert(0, record);
            if (records.Count > MaxRecords)
            {
                records = records.Take(MaxRecords).ToList();
            }

            await SaveAsync(records).ConfigureAwait(false);
        }

        public static string ComputeSha256Fingerprint(string raw)
        {
            var source = raw ?? string.Empty;
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(source));
                var sb = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private async Task<List<DuplicateGuardRecord>> LoadAsync()
        {
            var path = AppDataPaths.ResolveReadableJsonPath(FileName, out var migrate);
            if (!File.Exists(path))
            {
                return new List<DuplicateGuardRecord>();
            }

            try
            {
                var text = await Task.Run(() => File.ReadAllText(path, TextFileEncoding.Utf8)).ConfigureAwait(false);
                var records = JsonConvert.DeserializeObject<List<DuplicateGuardRecord>>(text) ?? new List<DuplicateGuardRecord>();
                if (migrate && records.Count > 0)
                {
                    await SaveAsync(records).ConfigureAwait(false);
                }

                return records;
            }
            catch
            {
                return new List<DuplicateGuardRecord>();
            }
        }

        private static async Task SaveAsync(List<DuplicateGuardRecord> records)
        {
            var json = JsonConvert.SerializeObject(records ?? new List<DuplicateGuardRecord>(), Formatting.Indented);
            await Task.Run(() =>
            {
                AppDataPaths.WriteJson(FileName, json);
                AppDataPaths.TryDeleteLegacyJson(FileName);
            }).ConfigureAwait(false);
        }
    }
}
