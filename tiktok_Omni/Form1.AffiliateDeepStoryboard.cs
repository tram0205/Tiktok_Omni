using System;
using System.Collections.Generic;
using System.Drawing;
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
                Text = "Storyboard (kéo thả để đổi thứ tự cảnh trước khi Render)",
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

        private void RefreshAffiliateDeepStoryboard()
        {
            if (flpAffiliateDeepStoryboard == null)
            {
                return;
            }

            flpAffiliateDeepStoryboard.SuspendLayout();
            flpAffiliateDeepStoryboard.Controls.Clear();
            var scenes = GetDeepDiveStoryboardOrderedBuffer();
            for (var i = 0; i < scenes.Count; i++)
            {
                flpAffiliateDeepStoryboard.Controls.Add(CreateStoryboardCard(scenes[i], i));
            }

            flpAffiliateDeepStoryboard.ResumeLayout(true);
        }

        private List<AiVideoGenInputItem> GetDeepDiveStoryboardOrderedBuffer()
        {
            var buf = GetDeepDiveBuffer();
            if (buf == null || buf.Count == 0)
            {
                return new List<AiVideoGenInputItem>();
            }

            var firstName = (buf[0]?.ProductName ?? string.Empty).Trim();
            var sameProduct = buf
                .Where(x => x != null && string.Equals((x.ProductName ?? string.Empty).Trim(), firstName, StringComparison.OrdinalIgnoreCase))
                .Take(8)
                .ToList();
            return sameProduct.Count > 0 ? sameProduct : buf.Take(8).ToList();
        }

        private Panel CreateStoryboardCard(AiVideoGenInputItem item, int index)
        {
            var card = new Panel
            {
                Width = 108,
                Height = 128,
                Margin = new Padding(6),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(36, 40, 52),
                Tag = index
            };

            var pic = new PictureBox
            {
                Width = 96,
                Height = 96,
                Location = new Point(5, 4),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(20, 22, 28)
            };
            TryLoadStoryboardThumb(pic, item?.ImageUrl);

            var lbl = new Label
            {
                Text = "Cảnh " + (index + 1),
                AutoSize = false,
                Width = 96,
                Height = 18,
                Location = new Point(5, 104),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gainsboro
            };

            card.Controls.Add(pic);
            card.Controls.Add(lbl);
            card.MouseDown += StoryboardCard_MouseDown;
            card.AllowDrop = true;
            card.DragEnter += Storyboard_DragEnter;
            card.DragDrop += StoryboardCard_DragDrop;
            return card;
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
                    using (var img = Image.FromFile(imageUrl))
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
            }
        }

        private void SwapDeepDiveScenesByProductGroup(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex)
            {
                return;
            }

            var buf = GetDeepDiveBuffer();
            var scenes = GetDeepDiveStoryboardOrderedBuffer();
            if (fromIndex < 0 || toIndex < 0 || fromIndex >= scenes.Count || toIndex >= scenes.Count)
            {
                return;
            }

            var itemA = scenes[fromIndex];
            var itemB = scenes[toIndex];
            var idxA = buf.FindIndex(x => AiVideoGenItemsMatch(x, itemA));
            var idxB = buf.FindIndex(x => AiVideoGenItemsMatch(x, itemB));
            if (idxA < 0 || idxB < 0)
            {
                return;
            }

            var temp = buf[idxA];
            buf[idxA] = buf[idxB];
            buf[idxB] = temp;
        }
    }
}
