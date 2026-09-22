using System.Diagnostics;
using System.Reflection;

namespace L2Marker;

/// <summary>App icon, name, author and licence - nothing interactive beyond the licence link.</summary>
public class AboutForm : Form
{
    public AboutForm(Icon? appIcon)
    {
        Text = "About L2 Marker";
        Width = 420;
        Height = 320;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        if (appIcon is not null) Icon = appIcon;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(20),
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var iconBox = new PictureBox
        {
            Width = 64,
            Height = 64,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        if (appIcon is not null) iconBox.Image = appIcon.ToBitmap();
        layout.Controls.Add(iconBox, 0, 0);
        layout.SetRowSpan(iconBox, 4);

        var version = Assembly.GetExecutingAssembly().GetName().Version;

        layout.Controls.Add(new Label
        {
            Text = "L2 Marker",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold)
        }, 1, 0);

        layout.Controls.Add(new Label
        {
            Text = $"Version {version?.ToString(3) ?? "1.0.0"}",
            AutoSize = true,
            ForeColor = Color.DimGray
        }, 1, 1);

        layout.Controls.Add(new Label
        {
            Text = "By Simon Rundell",
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 0)
        }, 1, 2);

        var licenceLabel = new Label
        {
            Text = "Licensed under Creative Commons\nAttribution-NonCommercial-ShareAlike 4.0 International.",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 4)
        };
        layout.Controls.Add(licenceLabel, 1, 3);

        var licenceLink = new LinkLabel
        {
            Text = "creativecommons.org/licenses/by-nc-sa/4.0",
            AutoSize = true
        };
        licenceLink.LinkClicked += (_, _) =>
        {
            Process.Start(new ProcessStartInfo("https://creativecommons.org/licenses/by-nc-sa/4.0/") { UseShellExecute = true });
        };
        layout.Controls.Add(licenceLink, 1, 4);

        var closeButton = new Button { Text = "Close", DialogResult = DialogResult.OK, Width = 90, Height = 30 };
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 50,
            Padding = new Padding(16)
        };
        buttonPanel.Controls.Add(closeButton);
        AcceptButton = closeButton;
        CancelButton = closeButton;

        Controls.Add(layout);
        Controls.Add(buttonPanel);
    }
}
