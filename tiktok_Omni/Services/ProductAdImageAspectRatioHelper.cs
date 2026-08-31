using System;
using System.Collections.Generic;

namespace tiktok_Omni.Services
{
    public static class ProductAdImageAspectRatioHelper
    {
        public const string Original = "original";

        public static IReadOnlyList<string> AllIds { get; } = new[]
        {
            Original,
            "3:4",
            "9:16",
            "1:1",
            "4:3",
            "16:9"
        };

        public static string Normalize(string value)
        {
            var raw = (value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(raw))
            {
                return Original;
            }

            foreach (var id in AllIds)
            {
                if (string.Equals(id, raw, StringComparison.OrdinalIgnoreCase))
                {
                    return id;
                }
            }

            return Original;
        }

        public static string GetDisplayLabel(string value)
        {
            var id = Normalize(value);
            switch (id)
            {
                case Original:
                    return "Gốc (original)";
                case "3:4":
                    return "3:4";
                case "9:16":
                    return "9:16";
                case "1:1":
                    return "1:1";
                case "4:3":
                    return "4:3";
                case "16:9":
                    return "16:9";
                default:
                    return id;
            }
        }

        public static IReadOnlyList<KeyValuePair<string, string>> GetComboItems()
        {
            var list = new List<KeyValuePair<string, string>>(AllIds.Count);
            foreach (var id in AllIds)
            {
                list.Add(new KeyValuePair<string, string>(id, GetDisplayLabel(id)));
            }

            return list;
        }
    }
}
