using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    /// <summary>Hộp thoại chỉnh màu / filter body video reup (FFmpeg eq).</summary>
    internal sealed class ReupColorGradeEditorForm : Form
    {
        private readonly VideoReupRowItem _row;
        private readonly AppSettings _defaults;
        private bool _suppressPresetEvents;

        private ComboBox _cbPreset;
        private TrackBar _tbBrightness;
        private TrackBar _tbContrast;
        private TrackBar _tbSaturation;
        private TrackBar _tbGamma;
        private Label _lblBrightnessVal;
        private Label _lblContrastVal;
        private Label _lblSaturationVal;
        private Label _lblGammaVal;
        private Label _lblPreview;

        public ReupColorGradeEditorForm(VideoReupRowItem row, AppSettings defaults)
        {
            _row = row ?? throw new ArgumentNullException(nameof(row));
            _defaults = defaults ?? new AppSettings();
            ReupColorGradeHelper.EnsureRowDefaults(_row, _defaults);

            Text = "Màu sắc / Filter — " + ((_row.ProductName ?? string.Empty).Trim().Length > 0
                ? _row.ProductName.Trim()
                : "Video reup");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(31, 34, 42);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(480, 420);
            AutoScroll = true;
            Padding = new Padding(12);

            BuildUi();
            LoadFromRow();
        }

        private void BuildUi()
        {
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 7,
                BackColor = BackColor
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));  // preset
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));  // brightness
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));  // contrast
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));  // saturation
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));  // gamma
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));  // preview
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));  // buttons

            Label MkLbl(string text) => new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 168, 182),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 10, 4, 0)
            };

            _cbPreset = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 49, 60),
                ForeColor = Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 0)
            };
            _cbPreset.Items.AddRange(ReupColorGradeHelper.PresetDisplayNames.Cast<object>().ToArray());
            _cbPreset.Items.Add("Tùy chỉnh");
            _cbPreset.SelectedIndexChanged += (_, __) =>
            {
                if (_suppressPresetEvents)
                {
                    return;
                }

                if (_cbPreset.SelectedIndex == _cbPreset.Items.Count - 1)
                {
                    _row.ReupColorPreset = ReupColorGradeHelper.PresetCustom;
                }
                else
                {
                    var key = ReupColorGradeHelper.GetPresetKeyByDisplayName(_cbPreset.Text);
                    ReupColorGradeHelper.ApplyPresetToRow(_row, key);
                    LoadSlidersFromRow();
                }

                UpdatePreview();
            };

            _tbBrightness = CreateSlider(-15, 15);
            _tbContrast = CreateSlider(85, 125);
            _tbSaturation = CreateSlider(75, 135);
            _tbGamma = CreateSlider(88, 115);

            _tbBrightness.Scroll += (_, __) => OnSliderChanged();
            _tbContrast.Scroll += (_, __) => OnSliderChanged();
            _tbSaturation.Scroll += (_, __) => OnSliderChanged();
            _tbGamma.Scroll += (_, __) => OnSliderChanged();

            tbl.Controls.Add(MkLbl("Preset"), 0, 0);
            tbl.SetColumnSpan(_cbPreset, 2);
            tbl.Controls.Add(_cbPreset, 1, 0);

            tbl.Controls.Add(MkLbl("Độ sáng"), 0, 1);
            tbl.Controls.Add(_tbBrightness, 1, 1);
            _lblBrightnessVal = MkValLabel();
            tbl.Controls.Add(_lblBrightnessVal, 2, 1);

            tbl.Controls.Add(MkLbl("Tương phản"), 0, 2);
            tbl.Controls.Add(_tbContrast, 1, 2);
            _lblContrastVal = MkValLabel();
            tbl.Controls.Add(_lblContrastVal, 2, 2);

            tbl.Controls.Add(MkLbl("Bão hòa"), 0, 3);
            tbl.Controls.Add(_tbSaturation, 1, 3);
            _lblSaturationVal = MkValLabel();
            tbl.Controls.Add(_lblSaturationVal, 2, 3);

            tbl.Controls.Add(MkLbl("Gamma"), 0, 4);
            tbl.Controls.Add(_tbGamma, 1, 4);
            _lblGammaVal = MkValLabel();
            tbl.Controls.Add(_lblGammaVal, 2, 4);

            _lblPreview = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(130, 175, 255),
                TextAlign = ContentAlignment.TopLeft,
                AutoEllipsis = true,
                AutoSize = false,
                Margin = new Padding(0, 6, 0, 0)
            };
            tbl.Controls.Add(MkLbl("Xem trước"), 0, 5);
            tbl.SetColumnSpan(_lblPreview, 2);
            tbl.Controls.Add(_lblPreview, 1, 5);

            var flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = BackColor,
                Margin = new Padding(0, 8, 0, 0)
            };
            var btnOk = CreateButton("Lưu", Color.FromArgb(50, 120, 90));
            btnOk.DialogResult = DialogResult.OK;
            var btnCancel = CreateButton("Hủy", Color.FromArgb(70, 78, 96));
            btnCancel.DialogResult = DialogResult.Cancel;
            var btnReset = CreateButton("Mặc định app", Color.FromArgb(55, 100, 140));
            btnReset.Click += (_, __) =>
            {
                ReupColorGradeHelper.ApplyPresetToRow(
                    _row,
                    string.IsNullOrWhiteSpace(_defaults.ReupColorPreset)
                        ? ReupColorGradeHelper.PresetDefault
                        : _defaults.ReupColorPreset);
                LoadFromRow();
            };
            flp.Controls.Add(btnOk);
            flp.Controls.Add(btnCancel);
            flp.Controls.Add(btnReset);
            tbl.SetColumnSpan(flp, 3);
            tbl.Controls.Add(flp, 0, 6);

            Controls.Add(tbl);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void OnSliderChanged()
        {
            _row.ReupColorPreset = ReupColorGradeHelper.PresetCustom;
            _row.ReupColorBrightness = SliderToBrightness(_tbBrightness);
            _row.ReupColorContrast = SliderToRatio(_tbContrast);
            _row.ReupColorSaturation = SliderToRatio(_tbSaturation);
            _row.ReupColorGamma = SliderToRatio(_tbGamma);
            _suppressPresetEvents = true;
            _cbPreset.SelectedIndex = _cbPreset.Items.Count - 1;
            _suppressPresetEvents = false;
            UpdatePreview();
            RefreshValueLabels();
        }

        private void LoadFromRow()
        {
            _suppressPresetEvents = true;
            if (string.Equals(_row.ReupColorPreset, ReupColorGradeHelper.PresetCustom, StringComparison.OrdinalIgnoreCase))
            {
                _cbPreset.SelectedIndex = _cbPreset.Items.Count - 1;
            }
            else
            {
                var label = ReupColorGradeHelper.GetPresetDisplayName(_row.ReupColorPreset);
                var idx = _cbPreset.Items.IndexOf(label);
                _cbPreset.SelectedIndex = idx >= 0 ? idx : 0;
            }

            _suppressPresetEvents = false;
            LoadSlidersFromRow();
            UpdatePreview();
            RefreshValueLabels();
        }

        private void LoadSlidersFromRow()
        {
            ReupColorGradeHelper.ResolveValues(_row, _defaults, out var v);
            _tbBrightness.Value = (int)Math.Round(v.Brightness * 100d);
            _tbContrast.Value = (int)Math.Round(v.Contrast * 100d);
            _tbSaturation.Value = (int)Math.Round(v.Saturation * 100d);
            _tbGamma.Value = (int)Math.Round(v.Gamma * 100d);
            RefreshValueLabels();
        }

        private void UpdatePreview()
        {
            _row.ReupColorGradeLabel = ReupColorGradeHelper.FormatStyleSummary(_row);
            _lblPreview.Text = ReupColorGradeHelper.BuildEqFilterChain(_row, _defaults);
        }

        private void RefreshValueLabels()
        {
            if (_lblBrightnessVal != null)
            {
                _lblBrightnessVal.Text = FormatSliderValue(_tbBrightness);
            }

            if (_lblContrastVal != null)
            {
                _lblContrastVal.Text = FormatSliderValue(_tbContrast);
            }

            if (_lblSaturationVal != null)
            {
                _lblSaturationVal.Text = FormatSliderValue(_tbSaturation);
            }

            if (_lblGammaVal != null)
            {
                _lblGammaVal.Text = FormatSliderValue(_tbGamma);
            }
        }

        private static Label MkValLabel()
        {
            return new Label
            {
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(4, 10, 0, 0)
            };
        }

        private static string FormatSliderValue(TrackBar tb)
        {
            if (tb == null)
            {
                return string.Empty;
            }

            if (tb.Minimum < 0)
            {
                return (tb.Value >= 0 ? "+" : string.Empty) + tb.Value.ToString(CultureInfo.InvariantCulture) + "%";
            }

            return tb.Value.ToString(CultureInfo.InvariantCulture) + "%";
        }

        private static TrackBar CreateSlider(int min, int max)
        {
            return new TrackBar
            {
                Minimum = min,
                Maximum = max,
                TickFrequency = 5,
                SmallChange = 1,
                LargeChange = 5,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 0),
                BackColor = Color.FromArgb(31, 34, 42)
            };
        }

        private static double SliderToBrightness(TrackBar tb) => tb.Value / 100d;

        private static double SliderToRatio(TrackBar tb) => tb.Value / 100d;

        private static Button CreateButton(string text, Color back)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(72, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Margin = new Padding(6, 0, 0, 0)
            };
        }
    }
}
