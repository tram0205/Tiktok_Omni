using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace tiktok_Omni
{
  internal sealed class ShowcaseRenderCompleteDialog : Form
  {
    public ShowcaseRenderCompleteDialog(string productName, string outputPath)
    {
      var product = (productName ?? string.Empty).Trim();
      if (product.Length == 0)
      {
        product = "Showcase";
      }

      var fileName = Path.GetFileName((outputPath ?? string.Empty).Trim());
      if (fileName.Length == 0)
      {
        fileName = outputPath ?? string.Empty;
      }

      Text = "Render video xong";
      StartPosition = FormStartPosition.CenterParent;
      FormBorderStyle = FormBorderStyle.FixedDialog;
      MaximizeBox = false;
      MinimizeBox = false;
      ShowInTaskbar = false;
      ClientSize = new Size(520, 196);
      BackColor = Color.FromArgb(31, 34, 42);
      ForeColor = Color.Gainsboro;
      Font = new Font("Segoe UI", 10.5F);
      Padding = new Padding(24, 20, 24, 16);

      var layout = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        RowCount = 3,
        Margin = Padding.Empty,
        Padding = Padding.Empty
      };
      layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
      layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));

      var title = new Label
      {
        AutoSize = true,
        MaximumSize = new Size(460, 0),
        Font = new Font("Segoe UI", 11F, FontStyle.Bold),
        ForeColor = Color.FromArgb(120, 210, 150),
        Text = "«" + product + "» đã render xong."
      };

      var body = new Label
      {
        AutoSize = true,
        MaximumSize = new Size(460, 0),
        Margin = new Padding(0, 10, 0, 0),
        ForeColor = Color.FromArgb(175, 182, 196),
        Text = fileName + "\r\n\r\nBấm «Xem thành phẩm» để mở video ngay, hoặc «Xem sau» nếu muốn xem lúc khác."
      };

      var buttons = new FlowLayoutPanel
      {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.RightToLeft,
        WrapContents = false,
        Margin = new Padding(0, 12, 0, 0),
        BackColor = BackColor
      };

      var btnLater = new Button
      {
        Text = "Xem sau",
        AutoSize = true,
        MinimumSize = new Size(112, 36),
        Margin = new Padding(8, 0, 0, 0),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(72, 78, 92),
        ForeColor = Color.White,
        DialogResult = DialogResult.Cancel
      };
      btnLater.FlatAppearance.BorderSize = 0;

      var btnWatch = new Button
      {
        Text = "Xem thành phẩm",
        AutoSize = true,
        MinimumSize = new Size(148, 36),
        Margin = Padding.Empty,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(56, 120, 82),
        ForeColor = Color.White,
        DialogResult = DialogResult.OK
      };
      btnWatch.FlatAppearance.BorderSize = 0;

      buttons.Controls.Add(btnLater);
      buttons.Controls.Add(btnWatch);

      layout.Controls.Add(title, 0, 0);
      layout.Controls.Add(body, 0, 1);
      layout.Controls.Add(buttons, 0, 2);
      Controls.Add(layout);

      AcceptButton = btnWatch;
      CancelButton = btnLater;
    }
  }
}
