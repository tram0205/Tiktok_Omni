using System;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private void SyncVideoReupSubtitleStyleFromUi(AppSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.ReupSubtitleFontName = cbReupSubtitleFont?.Text?.Trim() ?? settings.ReupSubtitleFontName;
            settings.ReupSubtitleFontSize = (int)(numReupSubtitleFontSize?.Value ?? settings.ReupSubtitleFontSize);
            settings.ReupSubtitlePosition = ReadReupSubtitlePositionFromUi();
            settings.ReupSubtitleAnimation = ReadReupSubtitleAnimationFromUi();
            settings.ReupSubtitleBold = chkReupSubtitleBold?.Checked ?? settings.ReupSubtitleBold;
            settings.ReupSubtitleItalic = chkReupSubtitleItalic?.Checked ?? settings.ReupSubtitleItalic;
            settings.ReupSubtitleWordsPerLine = (int)(numReupSubtitleWordsPerLine?.Value ?? settings.ReupSubtitleWordsPerLine);
        }

        private void ApplyVideoReupSubtitleStyleToUi(AppSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            if (cbReupSubtitleFont != null)
            {
                var font = (settings.ReupSubtitleFontName ?? "Segoe UI Bold").Trim();
                var idx = cbReupSubtitleFont.Items.IndexOf(font);
                cbReupSubtitleFont.SelectedIndex = idx >= 0 ? idx : 0;
            }

            if (numReupSubtitleFontSize != null)
            {
                numReupSubtitleFontSize.Value = Math.Max(
                    numReupSubtitleFontSize.Minimum,
                    Math.Min(numReupSubtitleFontSize.Maximum, settings.ReupSubtitleFontSize <= 0 ? 88 : settings.ReupSubtitleFontSize));
            }

            if (cbReupSubtitlePosition != null)
            {
                cbReupSubtitlePosition.SelectedIndex = ReupSubtitleStyleHelper.ParsePosition(settings.ReupSubtitlePosition) switch
                {
                    ReupSubtitleVerticalPosition.Top => 2,
                    ReupSubtitleVerticalPosition.Middle => 1,
                    _ => 0
                };
            }

            if (cbReupSubtitleAnimation != null)
            {
                cbReupSubtitleAnimation.SelectedIndex = ReupSubtitleStyleHelper.ParseAnimation(settings.ReupSubtitleAnimation) switch
                {
                    ReupKaraokeAnimationMode.Highlight => 1,
                    ReupKaraokeAnimationMode.FadeIn => 2,
                    ReupKaraokeAnimationMode.Plain => 3,
                    _ => 0
                };
            }

            if (chkReupSubtitleBold != null)
            {
                chkReupSubtitleBold.Checked = settings.ReupSubtitleBold;
            }

            if (chkReupSubtitleItalic != null)
            {
                chkReupSubtitleItalic.Checked = settings.ReupSubtitleItalic;
            }

            if (numReupSubtitleWordsPerLine != null)
            {
                numReupSubtitleWordsPerLine.Value = Math.Max(
                    numReupSubtitleWordsPerLine.Minimum,
                    Math.Min(numReupSubtitleWordsPerLine.Maximum, settings.ReupSubtitleWordsPerLine <= 0 ? 6 : settings.ReupSubtitleWordsPerLine));
            }
        }

        private string ReadReupSubtitlePositionFromUi()
        {
            if (cbReupSubtitlePosition == null || cbReupSubtitlePosition.SelectedIndex < 0)
            {
                return "Bottom";
            }

            return cbReupSubtitlePosition.SelectedIndex switch
            {
                2 => "Top",
                1 => "Middle",
                _ => "Bottom"
            };
        }

        private string ReadReupSubtitleAnimationFromUi()
        {
            if (cbReupSubtitleAnimation == null || cbReupSubtitleAnimation.SelectedIndex < 0)
            {
                return "Pop";
            }

            return cbReupSubtitleAnimation.SelectedIndex switch
            {
                1 => "Highlight",
                2 => "FadeIn",
                3 => "Plain",
                _ => "Pop"
            };
        }

        private void ShowReupSubtitleStyleEditor(VideoReupRowItem row, int gridRowIndex)
        {
            if (row == null)
            {
                return;
            }

            AppSettings defaults;
            try
            {
                defaults = _configManager.LoadAsync().ConfigureAwait(true).GetAwaiter().GetResult();
            }
            catch
            {
                defaults = new AppSettings();
            }

            ReupSubtitleStyleHelper.EnsureRowDefaults(row, defaults);
            using (var dlg = new ReupSubtitleStyleEditorForm(row, defaults))
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }
            }

            _videoReupDraftDirty = true;
            _videoReupBindingList?.ResetBindings();
            if (dgvVideoReupInput != null && gridRowIndex >= 0 && gridRowIndex < dgvVideoReupInput.Rows.Count)
            {
                dgvVideoReupInput.InvalidateRow(gridRowIndex);
            }
        }

        private void EnsureVideoReupRowSubtitleDefaults(VideoReupRowItem row, AppSettings settings)
        {
            if (row == null)
            {
                return;
            }

            ReupSubtitleStyleHelper.EnsureRowDefaults(row, settings ?? new AppSettings());
        }
    }
}
