using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using System.Text.RegularExpressions;

using System.Threading;

using System.Threading.Tasks;



namespace tiktok_Omni.Services

{

    public enum MascotEmotionTag

    {

        Neutral,

        Happy,

        Sad

    }



    public sealed class MascotSceneEmotion

    {

        public int SceneIndex { get; set; }

        public string SceneScript { get; set; } = string.Empty;

        public MascotEmotionTag Tag { get; set; } = MascotEmotionTag.Neutral;

    }



    /// <summary>Gắn tag [HAPPY]/[SAD]/[NEUTRAL] và chọn ảnh base_* trong AvatarVault.</summary>

    public sealed class EmotionEngine

    {

        private static readonly Regex LeadingTagRegex = new Regex(

            @"^\s*\[(HAPPY|SAD|NEUTRAL)\]\s*",

            RegexOptions.IgnoreCase | RegexOptions.Compiled);



        private static readonly Regex AnyTagRegex = new Regex(

            @"\[(HAPPY|SAD|NEUTRAL)\]",

            RegexOptions.IgnoreCase | RegexOptions.Compiled);



        private readonly GeminiService _gemini = new GeminiService();



        public async Task<List<MascotSceneEmotion>> AnalyzeSceneEmotionsAsync(

            IList<string> sceneScripts,

            AppSettings settings,

            Action<string> log,

            CancellationToken cancellationToken)

        {

            var scripts = (sceneScripts ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            var result = new List<MascotSceneEmotion>();

            for (var i = 0; i < scripts.Count; i++)

            {

                var raw = scripts[i];

                result.Add(new MascotSceneEmotion

                {

                    SceneIndex = i,

                    SceneScript = raw,

                    Tag = ParseLeadingTag(raw)

                });

            }



            if (scripts.Count == 0)

            {

                return result;

            }



            if (result.All(x => LeadingTagRegex.IsMatch(x.SceneScript ?? string.Empty)))

            {

                log?.Invoke("[EmotionEngine] Tags đầu dòng: " + string.Join(", ", result.Select(x => x.Tag)));

                return result;

            }



            if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))

            {

                log?.Invoke("[EmotionEngine] Thiếu AI key — heuristic cảm xúc.");

                for (var i = 0; i < result.Count; i++)

                {

                    result[i].Tag = HeuristicTag(StripTagsForNarration(scripts[i]));

                }



                return result;

            }



            try

            {

                var joined = string.Join("\n", scripts.Select((s, i) => $"Cảnh {i + 1}: {s}"));

                var prompt =

                    "Phân tích cảm xúc từng cảnh mascot. Với mỗi dòng, thêm ĐÚNG MỘT tag ở ĐẦU dòng (trước 'Cảnh'): [HAPPY], [SAD] hoặc [NEUTRAL]. " +

                    "Chỉ dùng 3 tag này. Chỉ trả danh sách cảnh, không giải thích:\n" + joined;



                var raw = await _gemini.GenerateScriptAsync(

                    prompt,

                    settings.AiProvider,

                    settings.AiApiKey,

                    settings.AiModel,

                    cancellationToken).ConfigureAwait(false);



                var lines = (raw ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                for (var i = 0; i < result.Count && i < lines.Length; i++)

                {

                    result[i].Tag = ParseLeadingTag(lines[i]);

                }



                log?.Invoke("[EmotionEngine] Gemini tags: " + string.Join(", ", result.Select(x => x.Tag)));

            }

            catch (Exception ex)

            {

                log?.Invoke("[EmotionEngine] Gemini bỏ qua — heuristic: " + ex.Message);

                for (var i = 0; i < result.Count; i++)

                {

                    result[i].Tag = HeuristicTag(StripTagsForNarration(scripts[i]));

                }

            }



            return result;

        }



        public static MascotEmotionTag ParseLeadingTag(string text)

        {

            var m = LeadingTagRegex.Match(text ?? string.Empty);

            if (!m.Success)

            {

                m = AnyTagRegex.Match(text ?? string.Empty);

            }



            if (!m.Success)

            {

                return HeuristicTag(text);

            }



            switch (m.Groups[1].Value.ToUpperInvariant())

            {

                case "HAPPY":

                    return MascotEmotionTag.Happy;

                case "SAD":

                    return MascotEmotionTag.Sad;

                default:

                    return MascotEmotionTag.Neutral;

            }

        }



        public static string StripTagsForNarration(string text)

        {

            if (string.IsNullOrWhiteSpace(text))

            {

                return string.Empty;

            }



            var s = AnyTagRegex.Replace(text, " ");

            s = Regex.Replace(s, @"\s+", " ").Trim();

            return s;

        }



        private static MascotEmotionTag HeuristicTag(string script)

        {

            var s = (script ?? string.Empty).ToLowerInvariant();

            if (s.Contains("vui") || s.Contains("cười") || s.Contains("hạnh phúc") || s.Contains("happy"))

            {

                return MascotEmotionTag.Happy;

            }



            if (s.Contains("buồn") || s.Contains("khóc") || s.Contains("sad") || s.Contains("thất vọng"))

            {

                return MascotEmotionTag.Sad;

            }



            return MascotEmotionTag.Neutral;

        }



        public static string ResolveEmotionImagePath(

            string profileName,

            MascotEmotionTag tag,

            AvatarIdentityPackConfig pack,

            string fallbackMascotPath)

        {

            var vaultDir = AvatarIdentityPackStore.GetProfileDirectory(profileName);

            var baseFile = tag switch

            {

                MascotEmotionTag.Happy => "base_happy.png",

                MascotEmotionTag.Sad => "base_sad.png",

                _ => "base_neutral.png"

            };



            var basePath = Path.Combine(vaultDir, baseFile);

            if (File.Exists(basePath))

            {

                return basePath;

            }



            foreach (var ext in new[] { ".jpg", ".jpeg", ".webp" })

            {

                var alt = Path.Combine(vaultDir, Path.GetFileNameWithoutExtension(baseFile) + ext);

                if (File.Exists(alt))

                {

                    return alt;

                }

            }



            pack = pack ?? AvatarIdentityPackStore.LoadOrCreate(profileName);

            var fromConfig = GetPathFromPack(pack, tag);

            if (File.Exists(fromConfig))

            {

                return fromConfig;

            }



            var emotionsDir = Path.Combine(vaultDir, "emotions");

            if (Directory.Exists(emotionsDir))

            {

                var key = tag.ToString().ToLowerInvariant();

                var hit = Directory.GetFiles(emotionsDir)

                    .FirstOrDefault(f =>

                        Path.GetFileNameWithoutExtension(f).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);

                if (!string.IsNullOrWhiteSpace(hit) && File.Exists(hit))

                {

                    return hit;

                }

            }



            return File.Exists(fallbackMascotPath) ? fallbackMascotPath : string.Empty;

        }



        private static string GetPathFromPack(AvatarIdentityPackConfig pack, MascotEmotionTag tag)

        {

            switch (tag)

            {

                case MascotEmotionTag.Happy:

                    return pack.EmotionHappyPath ?? string.Empty;

                case MascotEmotionTag.Sad:

                    return pack.EmotionSadPath ?? string.Empty;

                default:

                    return pack.EmotionNeutralPath ?? string.Empty;

            }

        }

    }

}


