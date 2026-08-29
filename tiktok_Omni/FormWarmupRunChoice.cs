using System;
using System.Drawing;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public enum WarmupRunChoice
    {
        Cancel = 0,
        Resume,
        SelectedRows,
        AllQueue
    }

    /// <summary>
    /// Hỏi người dùng chạy dòng đã chọn hay cả bảng hàng đợi.
    /// </summary>
    public sealed class FormWarmupRunChoice : Form
    {
        private readonly RadioButton _rbResume;
        private readonly RadioButton _rbSelected;
        private readonly RadioButton _rbAll;

        public WarmupRunChoice Choice { get; private set; } = WarmupRunChoice.Cancel;

        public FormWarmupRunChoice(
            bool canResume,
            int resumeCompleted,
            int resumeTotal,
            int selectedRunnableCount,
            int allRunnableCount)
        {
            Text = "Chọn cách chạy warm-up";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(640, 260);
            MinimumSize = new Size(600, 260);
            BackColor = Color.FromArgb(28, 31, 38);
            ForeColor = Color.FromArgb(210, 215, 225);
            Padding = new Padding(24, 18, 24, 18);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));

            var lblTitle = new Label
            {
                Text = "Bạn muốn chạy theo cách nào?",
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 200, 220),
                Margin = new Padding(0, 0, 0, 14)
            };

            _rbResume = CreateOption(
                canResume,
                $"Tiếp tục phiên dở ({resumeCompleted}/{resumeTotal} video)",
                canResume);
            _rbSelected = CreateOption(
                selectedRunnableCount > 0,
                selectedRunnableCount > 0
                    ? $"Chạy {selectedRunnableCount} dòng đã chọn"
                    : "Chạy dòng đã chọn (chưa chọn dòng nào)",
                !canResume && selectedRunnableCount > 0);
            _rbAll = CreateOption(
                allRunnableCount > 0,
                allRunnableCount > 0
                    ? $"Chạy cả bảng ({allRunnableCount} dòng sẵn sàng)"
                    : "Chạy cả bảng (không có dòng sẵn sàng)",
                !canResume && selectedRunnableCount <= 0 && allRunnableCount > 0);

            var optionsPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = false,
                WrapContents = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(0)
            };
            optionsPanel.Controls.Add(_rbResume);
            optionsPanel.Controls.Add(_rbSelected);
            optionsPanel.Controls.Add(_rbAll);

            var btnRun = new Button
            {
                Text = "Chạy",
                DialogResult = DialogResult.OK,
                Size = new Size(112, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 130, 80),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Margin = new Padding(10, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };
            btnRun.FlatAppearance.BorderColor = Color.FromArgb(60, 180, 110);
            btnRun.Click += (_, __) => CommitChoice();

            var btnCancel = new Button
            {
                Text = "Hủy",
                DialogResult = DialogResult.Cancel,
                Size = new Size(112, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 50, 55),
                ForeColor = Color.FromArgb(220, 200, 200),
                Font = new Font("Segoe UI", 10f),
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(120, 70, 80);

            var btnPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 0),
                Margin = Padding.Empty
            };
            btnPanel.Controls.Add(btnCancel);
            btnPanel.Controls.Add(btnRun);

            layout.Controls.Add(lblTitle, 0, 0);
            layout.Controls.Add(optionsPanel, 0, 1);
            layout.Controls.Add(btnPanel, 0, 2);

            Controls.Add(layout);
            AcceptButton = btnRun;
            CancelButton = btnCancel;
        }

        private static RadioButton CreateOption(bool enabled, string text, bool isChecked)
        {
            return new RadioButton
            {
                Text = text,
                AutoSize = true,
                Enabled = enabled,
                Checked = enabled && isChecked,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(200, 205, 215),
                Margin = new Padding(0, 0, 0, 12),
                MaximumSize = new Size(560, 0)
            };
        }

        private void CommitChoice()
        {
            if (_rbResume.Checked)
            {
                Choice = WarmupRunChoice.Resume;
            }
            else if (_rbSelected.Checked)
            {
                Choice = WarmupRunChoice.SelectedRows;
            }
            else if (_rbAll.Checked)
            {
                Choice = WarmupRunChoice.AllQueue;
            }
            else
            {
                Choice = WarmupRunChoice.Cancel;
            }
        }
    }
}
