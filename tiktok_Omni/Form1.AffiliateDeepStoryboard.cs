using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using tiktok_Omni.Services;

namespace tiktok_Omni
{
    public partial class Form1
    {
        private FlowLayoutPanel flpAffiliateDeepStoryboard;
        private Label lblAffiliateDeepStoryboard;
        private int? _storyboardDragIndex;

        private void BuildAffiliateDeepStoryboardUi(Control parent)
        {
            if (parent == null || flpAffiliateDeepStoryboard != null)
            {
                return;
            }

            lblAffiliateDeepStoryboard = new Label
            {
                Text = "Storyboard",
                Dock = DockStyle.Top,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4),
                ForeColor = Color.FromArgb(180, 185, 198)
            };

            flpAffiliateDeepStoryboard = new FlowLayoutPanel
            {
                Name = "flpAffiliateDeepStoryboard",
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4),
                BackColor = Color.FromArgb(24, 26, 34),
                AllowDrop = true
            };
            flpAffiliateDeepStoryboard.DragEnter += Storyboard_DragEnter;
            flpAffiliateDeepStoryboard.DragDrop += Storyboard_DragDrop;

            parent.Controls.Add(flpAffiliateDeepStoryboard);
            parent.Controls.Add(lblAffiliateDeepStoryboard);
            lblAffiliateDeepStoryboard.BringToFront();
        }

        private void RefreshAffiliateDeepStoryboard(bool skipSourceImagePrune = false)
        {
            if (flpAffiliateDeepStoryboard == null)
            {
                return;
            }

            var video = GetActiveShowcaseVideo();
            var prunedScenes = 0;
            if (!skipSourceImagePrune && video != null && video.Scenes.Count > 0)
            {
                prunedScenes = SyncShowcaseSourceImagesForVideo(video, refreshUi: false);
            }

            flpAffiliateDeepStoryboard.SuspendLayout();
            flpAffiliateDeepStoryboard.Controls.Clear();
            var scenes = GetDeepDiveStoryboardOrderedBuffer();
            for (var i = 0; i < scenes.Count; i++)
            {
                flpAffiliateDeepStoryboard.Controls.Add(CreateStoryboardCard(scenes[i], i));
            }

            flpAffiliateDeepStoryboard.ResumeLayout(true);
            if (prunedScenes > 0 && video != null)
            {
                video.RefreshDisplayFields();
                SyncBuffersToGrids();
                RefreshAiVideoGenModeReadinessLabels();
            }
        }

        private List<AiVideoGenInputItem> GetDeepDiveStoryboardOrderedBuffer()
        {
            var video = GetActiveShowcaseVideo();
            if (video == null || video.Scenes.Count == 0)
            {
                return new List<AiVideoGenInputItem>();
            }

            return video.Scenes.ToList();
        }

        private Panel CreateStoryboardCard(AiVideoGenInputItem item, int index)
        {
            var card = new Panel
            {
                Width = 112,
                Height = 150,
                Margin = new Padding(6),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(36, 40, 52),
                Tag = index
            };

            var pic = new PictureBox
            {
                Width = 100,
                Height = 96,
                Location = new Point(5, 4),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(20, 22, 28)
            };
            TryLoadStoryboardThumb(pic, item?.ThumbnailPath ?? item?.ImageUrl);

            var sceneName = !string.IsNullOrWhiteSpace(item?.SceneTitle)
                ? item.SceneTitle.Trim()
                : "Cảnh " + (index + 1);
            var lbl = new Label
            {
                Text = "C" + (index + 1) + " — " + sceneName,
                AutoSize = false,
                Width = 100,
                Height = 32,
                Location = new Point(5, 102),
                TextAlign = ContentAlignment.TopCenter,
                ForeColor = Color.Gainsboro,
                Font = new Font(Font.FontFamily, 7.5f)
            };

            var hasClip = !string.IsNullOrWhiteSpace(item?.ClipPath) && File.Exists(item.ClipPath);
            var lblClipStatus = new Label
            {
                Text = hasClip ? "✓ Có clip" : "✗ Chưa có clip",
                AutoSize = false,
                Width = 100,
                Height = 16,
                Location = new Point(5, 132),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = hasClip ? Color.FromArgb(120, 220, 160) : Color.FromArgb(230, 140, 120),
                Font = new Font(Font.FontFamily, 7f, FontStyle.Bold)
            };

            var btnRemove = new Button
            {
                Text = "✕",
                Width = 20,
                Height = 20,
                Location = new Point(88, 2),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(80, 40, 44),
                ForeColor = Color.Gainsboro,
                Font = new Font(Font.FontFamily, 7f, FontStyle.Bold),
                Tag = index,
                TabStop = false
            };
            btnRemove.FlatAppearance.BorderSize = 0;
            btnRemove.Click += StoryboardCard_RemoveClick;

            card.Controls.Add(pic);
            card.Controls.Add(lbl);
            card.Controls.Add(lblClipStatus);
            card.Controls.Add(btnRemove);
            card.MouseDown += StoryboardCard_MouseDown;
            card.AllowDrop = true;
            card.DragEnter += Storyboard_DragEnter;
            card.DragDrop += StoryboardCard_DragDrop;

            var tip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 300, ShowAlways = true };
            var roleText = string.IsNullOrWhiteSpace(item?.SceneRole) ? string.Empty : " [" + item.SceneRole + "]";
            tip.SetToolTip(card, sceneName + roleText + (string.IsNullOrWhiteSpace(item?.SceneVoiceover) ? string.Empty : "\r\n" + item.SceneVoiceover));
            tip.SetToolTip(btnRemove, "Xoá cảnh này khỏi storyboard");
            return card;
        }

        private void StoryboardCard_RemoveClick(object sender, EventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is int index))
            {
                return;
            }

            var scenes = GetDeepDiveStoryboardOrderedBuffer();
            if (index < 0 || index >= scenes.Count)
            {
                return;
            }

            var target = scenes[index];
            var name = string.IsNullOrWhiteSpace(target?.SceneTitle) ? "Cảnh " + (index + 1) : target.SceneTitle;
            if (MessageBox.Show(this, "Xoá «" + name + "» khỏi storyboard?", "Xoá cảnh",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            var video = GetActiveShowcaseVideo();
            if (video == null)
            {
                return;
            }

            video.Scenes.RemoveAll(x => AiVideoGenItemsMatch(x, target));
            video.RefreshDisplayFields();
            SyncShowcaseVideoSettingsToScenes(video);
            SyncBuffersToGrids();
            RefreshAffiliateDeepStoryboard();
            RefreshAiVideoGenModeReadinessLabels();
            NotifyShowcaseDraftDirty();
        }

        private static void TryLoadStoryboardThumb(PictureBox pic, string imageUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    return;
                }

                if (imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    pic.LoadAsync(imageUrl);
                }
                else if (System.IO.File.Exists(imageUrl))
                {
                    using (var fs = new System.IO.FileStream(
                               imageUrl,
                               System.IO.FileMode.Open,
                               System.IO.FileAccess.Read,
                               System.IO.FileShare.ReadWrite))
                    using (var img = Image.FromStream(fs))
                    {
                        pic.Image = new Bitmap(img);
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        private void StoryboardCard_MouseDown(object sender, MouseEventArgs e)
        {
            if (sender is Panel panel && panel.Tag is int idx)
            {
                _storyboardDragIndex = idx;
                panel.DoDragDrop(idx, DragDropEffects.Move);
            }
        }

        private void Storyboard_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(int)))
            {
                e.Effect = DragDropEffects.Move;
            }
        }

        private void Storyboard_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(int)) || !_storyboardDragIndex.HasValue)
            {
                return;
            }

            var from = _storyboardDragIndex.Value;
            var buf = GetDeepDiveStoryboardOrderedBuffer();
            if (from < 0 || from >= buf.Count)
            {
                return;
            }

            var to = Math.Min(buf.Count - 1, flpAffiliateDeepStoryboard.Controls.Count - 1);
            SwapDeepDiveScenesByProductGroup(from, to);
            _storyboardDragIndex = null;
            SyncBuffersToGrids();
            RefreshAffiliateDeepStoryboard();
            NotifyShowcaseDraftDirty();
        }

        private void StoryboardCard_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(int)) || !_storyboardDragIndex.HasValue)
            {
                return;
            }

            var from = _storyboardDragIndex.Value;
            if (sender is Panel target && target.Tag is int to)
            {
                SwapDeepDiveScenesByProductGroup(from, to);
                _storyboardDragIndex = null;
                SyncBuffersToGrids();
                RefreshAffiliateDeepStoryboard();
                NotifyShowcaseDraftDirty();
            }
        }

        private void SwapDeepDiveScenesByProductGroup(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex)
            {
                return;
            }

            var video = GetActiveShowcaseVideo();
            var scenes = video?.Scenes;
            if (scenes == null || fromIndex < 0 || toIndex < 0 || fromIndex >= scenes.Count || toIndex >= scenes.Count)
            {
                return;
            }

            var temp = scenes[fromIndex];
            scenes[fromIndex] = scenes[toIndex];
            scenes[toIndex] = temp;
            video.RefreshDisplayFields();
        }
    }
}
