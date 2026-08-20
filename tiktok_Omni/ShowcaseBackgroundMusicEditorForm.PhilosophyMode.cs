using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    internal sealed partial class ShowcaseBackgroundMusicEditorForm
    {
        /// <summary>Tiếng đệm Triết lý sau khi Lưu (chỉ khi <see cref="_philosophyMode"/>).</summary>
        public string PhilosophyAmbientKey =>
            PhilosophyAmbientCatalog.NormalizeKey(_philosophyAmbientKey);

        private void ApplyPhilosophyModeUi()
        {
            if (!_philosophyMode)
            {
                return;
            }

            Text = "Âm thanh — Quote"
                   + (string.IsNullOrWhiteSpace(_video?.ProductName) ? string.Empty : " · " + _video.ProductName.Trim());

            if (_hookVoice?.Shell != null)
            {
                _hookVoice.Shell.Visible = false;
            }

            if (_bodyVoice?.Shell != null)
            {
                _bodyVoice.Shell.AccessibleName = "Giọng đọc quote";
            }

            if (_btnReviewScript != null && !_btnReviewScript.IsDisposed)
            {
                _btnReviewScript.Visible = false;
            }

            if (_voiceReviewScriptRow != null)
            {
                _voiceReviewScriptRow.Visible = false;
                _voiceReviewScriptRow.Height = 0;
                _voiceReviewScriptRow.Margin = Padding.Empty;
            }

            if (_btnGenerateHookNarration != null)
            {
                _btnGenerateHookNarration.Visible = false;
            }

            if (_btnListenHookNarration != null)
            {
                _btnListenHookNarration.Visible = false;
            }

            if (_footerBarPanel != null)
            {
                _footerBarPanel.Padding = new Padding(0, 10, 0, 12);
            }

            if (_footerStack != null)
            {
                _footerStack.Padding = new Padding(0, 4, 2, 4);
            }

            Padding = new Padding(
                DialogOuterPaddingH,
                DialogOuterPaddingTop,
                DialogOuterPaddingH,
                8);

            SyncFooterBarHeight();
        }

        private void ApplyPhilosophyVoiceActionButtons()
        {
            if (!_philosophyMode)
            {
                return;
            }

            if (_btnListenBodyNarration != null)
            {
                _btnListenBodyNarration.Visible = false;
            }

            if (_btnListenFullMixedAudio != null)
            {
                _btnListenFullMixedAudio.Visible = false;
            }

            RefreshPhilosophyQuoteBatchActionLabels();
            if (_bodyVoice != null)
            {
                LayoutVoiceNarrationButtons(_bodyVoice);
            }
        }

        private void BuildPhilosophyAmbientTab(TabPage tab)
        {
            tab.Text = "Tiếng đệm";
            tab.AutoScroll = true;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                BackColor = BackColor,
                Padding = new Padding(0, 8, 0, 8)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, MusicTabLabelColumnWidth));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lbl = new Label
            {
                Text = "Tiếng đệm",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(160, 168, 182),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Margin = new Padding(0, 16, 24, 16)
            };

            _cbPhilosophyAmbient = CreateDropDownCombo();
            _cbPhilosophyAmbient.Dock = DockStyle.Fill;
            foreach (var key in PhilosophyAmbientCatalog.AllKeys)
            {
                _cbPhilosophyAmbient.Items.Add(key);
            }

            _lblPhilosophyAmbientHint = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(148, 156, 172),
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 4, 0, 0),
                Text = PhilosophyAmbientCatalog.GetLabel(_philosophyAmbientKey)
            };
            _cbPhilosophyAmbient.SelectedIndexChanged += (_, __) =>
            {
                var key = _cbPhilosophyAmbient.SelectedItem?.ToString();
                if (_lblPhilosophyAmbientHint != null && !string.IsNullOrWhiteSpace(key))
                {
                    _lblPhilosophyAmbientHint.Text = PhilosophyAmbientCatalog.GetLabel(key);
                }
            };

            root.Controls.Add(lbl, 0, 0);
            root.Controls.Add(_cbPhilosophyAmbient, 1, 0);
            root.Controls.Add(new Panel(), 0, 1);
            root.Controls.Add(_lblPhilosophyAmbientHint, 1, 1);
            tab.Controls.Add(root);
            LoadPhilosophyAmbientFromKey();
        }

        private void LoadPhilosophyAmbientFromKey()
        {
            if (_cbPhilosophyAmbient == null)
            {
                return;
            }

            var key = PhilosophyAmbientCatalog.NormalizeKey(_philosophyAmbientKey);
            var idx = _cbPhilosophyAmbient.Items.Cast<object>()
                .ToList()
                .FindIndex(x => string.Equals(x?.ToString(), key, StringComparison.OrdinalIgnoreCase));
            _cbPhilosophyAmbient.SelectedIndex = idx >= 0 ? idx : 0;
            if (_lblPhilosophyAmbientHint != null)
            {
                _lblPhilosophyAmbientHint.Text = PhilosophyAmbientCatalog.GetLabel(key);
            }
        }

        private void LoadPhilosophyMusicLibraryLabel()
        {
            if (!_philosophyMode || _lblLibrary == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_philosophyMusicLibrarySummary))
            {
                _lblLibrary.Text = _philosophyMusicLibrarySummary;
            }
        }
    }
}
