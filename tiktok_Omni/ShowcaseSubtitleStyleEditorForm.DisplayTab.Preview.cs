using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed partial class ShowcaseSubtitleStyleEditorForm
    {
        private void PreviewSelectedRowDisplayEffect()
        {
            if (_dgvDisplay == null)
            {
                return;
            }

            var row = _dgvDisplay.CurrentRow;
            if (row == null || row.IsNewRow)
            {
                MessageBox.Show(this,
                    "Chọn một dòng trong bảng (Hook / cảnh / CTA) rồi bấm «Xem thử hiệu ứng».",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var tag = GetDisplayRowTag(row);
            if (tag == null)
            {
                return;
            }

            if (!SaveTemplateToVideo())
            {
                return;
            }

            if (!SaveStyleFromGridToVideo())
            {
                return;
            }

            var subtitle = (row.Cells["colDisplaySubtitle"].Value?.ToString() ?? string.Empty).Trim();
            if (subtitle.Length == 0)
            {
                subtitle = (tag.SourceSpeech ?? string.Empty).Trim();
            }

            var sceneLabel = row.Cells["colDisplayScene"].Value?.ToString()?.Trim() ?? string.Empty;
            var enabled = ReadBoolCell(row.Cells["colStyleEnabled"]);
            var effectKind = tag.Role == DisplayGridRowRole.Hook
                ? ShowcaseDisplayLineEffectKind.Hook
                : ShowcaseDisplayLineEffectKind.Body;
            var effectStorage = GetDisplayEffectStorage(
                row.Cells["colDisplayEffect"] as DataGridViewComboBoxCell,
                effectKind);

            ShowcaseSubtitleLineRenderOverride lineStyle = null;
            if (tag.Role != DisplayGridRowRole.Hook)
            {
                lineStyle = BuildLineRenderOverrideFromRow(row);
            }

            var request = new ShowcaseSubtitleDisplayEffectPreviewRequest
            {
                SubtitleText = subtitle,
                SceneLabel = sceneLabel,
                IsHook = tag.Role == DisplayGridRowRole.Hook,
                StyleEnabled = enabled,
                EffectStorage = effectStorage,
                LineStyle = lineStyle
            };

            using (var dlg = new ShowcaseSubtitleEffectPreviewForm(sceneLabel, ct => RenderEffectPreviewAsync(request, ct)))
            {
                dlg.ShowDialog(this);
            }
        }

        private async Task<string> RenderEffectPreviewAsync(
            ShowcaseSubtitleDisplayEffectPreviewRequest request,
            CancellationToken cancellationToken)
        {
            return await ShowcaseSubtitleDisplayEffectPreviewHelper.RenderPreviewClipAsync(
                _video,
                _defaults,
                request,
                null,
                cancellationToken).ConfigureAwait(false);
        }

        private ShowcaseSubtitleLineRenderOverride BuildLineRenderOverrideFromRow(DataGridViewRow row)
        {
            if (row == null)
            {
                return null;
            }

            var lookStorage = ShowcaseSubtitleLookPresetCatalog.StorageFromLabel(
                row.Cells["colStyleLook"].Value?.ToString(),
                ShowcaseDisplayLineEffectKind.Body);
            TryParseFontSize(row.Cells["colStyleFontSize"].Value, 32, 160, out var size);
            var posLabel = row.Cells["colStylePosition"].Value?.ToString() ?? string.Empty;
            var position = string.Equals(posLabel, "Trên", StringComparison.Ordinal) ? "Top"
                : string.Equals(posLabel, "Giữa", StringComparison.Ordinal) ? "Middle"
                : "Bottom";

            var o = new ShowcaseSubtitleLineRenderOverride
            {
                LookPreset = lookStorage,
                FontSize = size,
                Position = position,
                LineBackgroundColourAss = ShowcaseSubtitleHighlightColourCatalog.LineBackgroundAssFromLabel(
                    row.Cells["colStyleHighlight"].Value?.ToString())
            };

            return o.HasAny() ? o : null;
        }
    }
}
