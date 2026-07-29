using System;
using System.Windows.Forms;
using tiktok_Omni.Services;
using tiktok_Omni.Services.Showcase;

namespace tiktok_Omni
{
    internal sealed partial class ShowcaseBackgroundMusicEditorForm
    {
        private const string PreviewPlayMusicLabel = "▶ Nghe thử";
        private const string PreviewPlaySfxLabel = "▶ Nghe thử dòng chọn";
        private const string PreviewStopLabel = "■ Dừng";

        private enum AudioPreviewKind
        {
            None,
            Music,
            Sfx
        }

        private JellyButton _btnPreviewMusic;
        private JellyButton _btnPreviewSfx;
        private AudioPreviewKind _audioPreviewKind = AudioPreviewKind.None;
        private Timer _audioPreviewPollTimer;

        private void WireAudioPreviewLifecycle()
        {
            _audioPreviewPollTimer = new Timer { Interval = 400 };
            _audioPreviewPollTimer.Tick += (_, __) =>
            {
                if (_audioPreviewKind == AudioPreviewKind.None)
                {
                    _audioPreviewPollTimer.Stop();
                    return;
                }

                if (!UiAudioPreviewPlayer.IsPlaying())
                {
                    _audioPreviewKind = AudioPreviewKind.None;
                    _audioPreviewPollTimer.Stop();
                    RefreshPreviewButtons();
                }
            };

            FormClosed += (_, __) => StopAudioPreview();
        }

        private void StopAudioPreview()
        {
            UiAudioPreviewPlayer.Stop();
            _audioPreviewKind = AudioPreviewKind.None;
            _audioPreviewPollTimer?.Stop();
            RefreshPreviewButtons();
        }

        private void BeginAudioPreview(AudioPreviewKind kind)
        {
            _audioPreviewKind = kind;
            RefreshPreviewButtons();
            _audioPreviewPollTimer?.Start();
        }

        private void RefreshPreviewButtons()
        {
            RefreshMusicPreviewButton();
            RefreshSfxPreviewButton();
        }

        private void UpdateMusicPreviewButtonState()
        {
            RefreshMusicPreviewButton();
        }

        private void RefreshMusicPreviewButton()
        {
            if (_btnPreviewMusic == null || _btnPreviewMusic.IsDisposed || _cbMusic == null)
            {
                return;
            }

            if (_audioPreviewKind == AudioPreviewKind.Music && UiAudioPreviewPlayer.IsPlaying())
            {
                ApplyActionButtonLabel(_btnPreviewMusic, PreviewStopLabel);
                _btnPreviewMusic.Enabled = true;
                return;
            }

            ApplyActionButtonLabel(_btnPreviewMusic, PreviewPlayMusicLabel);
            var selected = _cbMusic.SelectedItem?.ToString()?.Trim() ?? string.Empty;
            _btnPreviewMusic.Enabled = !VideoReupRowItem.IsNoMusicSelection(selected)
                                       && !string.IsNullOrWhiteSpace(selected);
        }

        private void RefreshSfxPreviewButton()
        {
            if (_btnPreviewSfx == null || _btnPreviewSfx.IsDisposed)
            {
                return;
            }

            if (_audioPreviewKind == AudioPreviewKind.Sfx && UiAudioPreviewPlayer.IsPlaying())
            {
                ApplyActionButtonLabel(_btnPreviewSfx, PreviewStopLabel);
                _btnPreviewSfx.Enabled = true;
                return;
            }

            ApplyActionButtonLabel(_btnPreviewSfx, PreviewPlaySfxLabel, minWidth: 120);
            _btnPreviewSfx.Enabled = true;
        }

        private void TryPreviewMusic()
        {
            if (_audioPreviewKind == AudioPreviewKind.Music && UiAudioPreviewPlayer.IsPlaying())
            {
                StopAudioPreview();
                return;
            }

            var selected = _cbMusic.SelectedItem?.ToString()?.Trim() ?? string.Empty;
            if (VideoReupRowItem.IsNoMusicSelection(selected) || string.IsNullOrWhiteSpace(selected))
            {
                MessageBox.Show(this,
                    "Chọn một file nhạc trong danh sách trước khi nghe thử.",
                    "Nhạc nền",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var path = OmniAudioLibrary.ResolveMusicFilePath(selected, _settings);
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(this,
                    "Không tìm thấy file nhạc trên đĩa. Bấm «Làm mới danh sách» hoặc kiểm tra thư mục Music.",
                    "Nhạc nền",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var vol = MusicVolumePercent;
                UiAudioPreviewPlayer.Play(path, vol);
                BeginAudioPreview(AudioPreviewKind.Music);
            }
            catch (Exception ex)
            {
                StopAudioPreview();
                MessageBox.Show(this, ex.Message, "Nghe thử nhạc", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void TryPreviewSfxCurrentRow()
        {
            if (_audioPreviewKind == AudioPreviewKind.Sfx && UiAudioPreviewPlayer.IsPlaying())
            {
                StopAudioPreview();
                return;
            }

            if (_dgvSfx?.CurrentRow == null)
            {
                MessageBox.Show(this,
                    "Chọn một dòng (Hook, cảnh hoặc CTA) trên lưới SFX.",
                    "Hiệu ứng âm thanh",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var row = _dgvSfx.CurrentRow;
            if (!TryParseRow(row, out _, out _, out var file, out var volume, out _))
            {
                MessageBox.Show(this,
                    "Âm lượng hoặc lệch thời gian không hợp lệ trên dòng này.",
                    "Hiệu ứng âm thanh",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(file))
            {
                MessageBox.Show(this,
                    "Dòng này chưa chọn file SFX.",
                    "Hiệu ứng âm thanh",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var path = ShowcaseSfxCatalog.ResolveFilePath(_settings, file);
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(this,
                    "Không tìm thấy file SFX: " + file,
                    "Hiệu ứng âm thanh",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                UiAudioPreviewPlayer.Play(path, ShowcaseSfxCatalog.ClampVolumePercent(volume));
                BeginAudioPreview(AudioPreviewKind.Sfx);
            }
            catch (Exception ex)
            {
                StopAudioPreview();
                MessageBox.Show(this, ex.Message, "Nghe thử SFX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
