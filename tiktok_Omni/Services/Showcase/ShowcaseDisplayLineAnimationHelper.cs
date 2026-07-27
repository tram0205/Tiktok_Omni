using System;
using System.Windows.Forms;

namespace tiktok_Omni.Services.Showcase
{
    public enum ShowcaseDisplayLineEffectKind
    {
        Hook,
        Body
    }

    public static class ShowcaseDisplayLineAnimationHelper
    {
        public static readonly string DefaultStorage = string.Empty;

        public const string DefaultComboLabel = "(Mặc định Kiểu chữ)";

        public static void PopulateCombo(ComboBox combo, ShowcaseDisplayLineEffectKind kind)
        {
            if (combo == null)
            {
                return;
            }

            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.Clear();
            combo.Items.Add(DefaultComboLabel);
            if (kind == ShowcaseDisplayLineEffectKind.Hook)
            {
                foreach (var entry in ShowcaseHookAnimationCatalog.All)
                {
                    combo.Items.Add(entry.ComboLabel);
                }
            }
            else
            {
                combo.Items.Add("Pop (phóng to)");
                combo.Items.Add("Karaoke (tô màu)");
                combo.Items.Add("Hiện dần");
                combo.Items.Add("Cả dòng");
            }
        }

        public static void SelectStorage(ComboBox combo, ShowcaseDisplayLineEffectKind kind, string storage)
        {
            if (combo == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(storage))
            {
                combo.SelectedIndex = 0;
                return;
            }

            if (kind == ShowcaseDisplayLineEffectKind.Hook)
            {
                combo.SelectedIndex = ShowcaseHookAnimationCatalog.SelectedIndexFromStorage(storage) + 1;
                return;
            }

            var anim = ReupSubtitleStyleHelper.ParseAnimation(storage);
            combo.SelectedIndex = anim switch
            {
                ReupKaraokeAnimationMode.Highlight => 2,
                ReupKaraokeAnimationMode.FadeIn => 3,
                ReupKaraokeAnimationMode.Plain => 4,
                _ => 1
            };
        }

        public static string GetSelectedStorage(ComboBox combo, ShowcaseDisplayLineEffectKind kind)
        {
            if (combo == null || combo.SelectedIndex <= 0)
            {
                return DefaultStorage;
            }

            if (kind == ShowcaseDisplayLineEffectKind.Hook)
            {
                return ShowcaseHookAnimationCatalog.StorageFromSelectedIndex(combo.SelectedIndex - 1);
            }

            return combo.SelectedIndex switch
            {
                1 => "Pop",
                2 => "Highlight",
                3 => "FadeIn",
                4 => "Plain",
                _ => DefaultStorage
            };
        }
    }
}
