using System;

using System.Drawing;

using System.Windows.Forms;

using tiktok_Omni.Services;



namespace tiktok_Omni

{

    internal sealed class ShowcaseTtsEngineChoiceForm : Form

    {

        private readonly RadioButton _rbEdge;

        private readonly RadioButton _rbEleven;



        public TtsEngineKind SelectedEngine =>

            _rbEleven.Checked ? TtsEngineKind.ElevenLabs : TtsEngineKind.EdgeTts;



        public ShowcaseTtsEngineChoiceForm(AppSettings settings, TtsEngineKind initialChoice)

        {

            const int formW = 620;

            const int formH = 320;

            const int pad = 28;

            var innerW = formW - pad * 2;



            Text = "Chọn nguồn giọng đọc";

            FormBorderStyle = FormBorderStyle.FixedDialog;

            MaximizeBox = false;

            MinimizeBox = false;

            StartPosition = FormStartPosition.CenterParent;

            BackColor = Color.FromArgb(31, 34, 42);

            ClientSize = new Size(formW, formH);



            var lbl = new Label

            {

                Text = "Dùng engine nào để tạo narration.mp3?",

                AutoSize = false,

                Width = innerW,

                Height = 44,

                Location = new Point(pad, pad),

                ForeColor = Color.WhiteSmoke,

                Font = new Font("Segoe UI", 11F, FontStyle.Bold)

            };



            var y = lbl.Bottom + 20;



            _rbEdge = new RadioButton

            {

                Text = "Edge TTS (miễn phí — tiếng Việt neural, mặc định)",

                Location = new Point(pad, y),

                Width = innerW,

                Height = 36,

                ForeColor = Color.Gainsboro,

                Font = new Font("Segoe UI", 10F),

                AutoSize = false,

                Enabled = Environment.OSVersion.Platform == PlatformID.Win32NT

            };



            y = _rbEdge.Bottom + 16;



            _rbEleven = new RadioButton

            {

                Text = "ElevenLabs (online — chất lượng cao, tốn token)",

                Location = new Point(pad, y),

                Width = innerW,

                Height = 36,

                ForeColor = Color.Gainsboro,

                Font = new Font("Segoe UI", 10F),

                AutoSize = false,

                Enabled = TtsAvailabilityHelper.IsElevenLabsConfigured(settings)

            };



            if (initialChoice == TtsEngineKind.ElevenLabs && _rbEleven.Enabled)

            {

                _rbEleven.Checked = true;

            }

            else if (_rbEdge.Enabled)

            {

                _rbEdge.Checked = true;

            }

            else if (_rbEleven.Enabled)

            {

                _rbEleven.Checked = true;

            }



            if (!_rbEdge.Enabled && !_rbEleven.Enabled)

            {

                lbl.Text = "Chưa cấu hình TTS.\r\nEdge cần Windows + mạng; hoặc cấu hình ElevenLabs trong Cài đặt.";

                lbl.Height = 52;

            }



            const int btnH = 36;

            const int btnW = 100;

            var btnY = formH - pad - btnH;

            var btnCancel = new Button

            {

                Text = "Hủy",

                DialogResult = DialogResult.Cancel,

                Location = new Point(formW - pad - btnW, btnY),

                Width = btnW,

                Height = btnH

            };

            var btnOk = new Button

            {

                Text = "Tạo",

                DialogResult = DialogResult.OK,

                Location = new Point(btnCancel.Left - 12 - btnW, btnY),

                Width = btnW,

                Height = btnH,

                Enabled = _rbEdge.Enabled || _rbEleven.Enabled

            };



            AcceptButton = btnOk;

            CancelButton = btnCancel;

            Controls.Add(lbl);

            Controls.Add(_rbEdge);

            Controls.Add(_rbEleven);

            Controls.Add(btnOk);

            Controls.Add(btnCancel);

        }

    }

}


