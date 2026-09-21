using System.Text.Json.Nodes;
using L2Marker.Models;
using L2Marker.Services;

namespace L2Marker;

public class SettingsForm : Form
{
    private readonly TextBox _apiKeyBox;
    private readonly ComboBox _modelBox;
    private readonly TextBox _referenceFolderBox;
    private readonly NumericUpDown _concurrencyBox;
    private readonly TextBox _assessorNameBox;
    private readonly Label _testResultLabel;

    public AppSettings Settings { get; private set; }

    public SettingsForm(AppSettings current)
    {
        Settings = current;

        Text = "Settings";
        Width = 640;
        Height = 420;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(16),
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

        int row = 0;

        layout.Controls.Add(new Label { Text = "Claude API key:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _apiKeyBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, Text = current.ApiKey };
        layout.Controls.Add(_apiKeyBox, 1, row);
        var testButton = new Button { Text = "Test", Dock = DockStyle.Fill };
        testButton.Click += async (_, _) => await TestApiKeyAsync();
        layout.Controls.Add(testButton, 2, row);
        row++;

        layout.Controls.Add(new Label { Text = "", AutoSize = true }, 0, row);
        _testResultLabel = new Label { Dock = DockStyle.Fill, AutoSize = false, Height = 20, ForeColor = Color.DimGray };
        layout.Controls.Add(_testResultLabel, 1, row);
        layout.SetColumnSpan(_testResultLabel, 2);
        row++;

        layout.Controls.Add(new Label { Text = "Model:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _modelBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
        _modelBox.Items.AddRange(new object[]
        {
            "claude-haiku-4-5-20251001",
            "claude-sonnet-5",
            "claude-opus-5"
        });
        _modelBox.Text = current.Model;
        layout.Controls.Add(_modelBox, 1, row);
        layout.SetColumnSpan(_modelBox, 2);
        row++;

        layout.Controls.Add(new Label { Text = "Reference folder:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _referenceFolderBox = new TextBox { Dock = DockStyle.Fill, Text = current.ReferenceFolder };
        layout.Controls.Add(_referenceFolderBox, 1, row);
        var browseButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        browseButton.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = _referenceFolderBox.Text };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                _referenceFolderBox.Text = dlg.SelectedPath;
        };
        layout.Controls.Add(browseButton, 2, row);
        row++;

        var refHint = new Label
        {
            Text = "Folder containing the workbook model answers and blank 'Assessor Feedback to Learner' templates for each unit.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            MaximumSize = new Size(460, 0)
        };
        layout.Controls.Add(refHint, 1, row);
        layout.SetColumnSpan(refHint, 2);
        row++;

        layout.Controls.Add(new Label { Text = "Assessor name:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _assessorNameBox = new TextBox { Dock = DockStyle.Fill, Text = current.AssessorName };
        layout.Controls.Add(_assessorNameBox, 1, row);
        row++;

        layout.Controls.Add(new Label { Text = "Max concurrent calls:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _concurrencyBox = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 10, Value = Math.Clamp(current.MaxConcurrency, 1, 10) };
        layout.Controls.Add(_concurrencyBox, 1, row);
        row++;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 50,
            Padding = new Padding(16)
        };
        var okButton = new Button { Text = "Save", DialogResult = DialogResult.OK, Width = 90 };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
        okButton.Click += (_, _) => SaveIntoSettings();
        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(layout);
        Controls.Add(buttonPanel);
    }

    private void SaveIntoSettings()
    {
        Settings = new AppSettings
        {
            ApiKey = _apiKeyBox.Text.Trim(),
            Model = _modelBox.Text.Trim(),
            ReferenceFolder = _referenceFolderBox.Text.Trim(),
            MaxConcurrency = (int)_concurrencyBox.Value,
            AssessorName = _assessorNameBox.Text.Trim(),
            MaxOutputTokens = 4096
        };
    }

    private async Task TestApiKeyAsync()
    {
        _testResultLabel.Text = "Testing...";
        _testResultLabel.ForeColor = Color.DimGray;

        try
        {
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", _apiKeyBox.Text.Trim());
            request.Headers.Add("anthropic-version", "2023-06-01");
            var body = new JsonObject
            {
                ["model"] = _modelBox.Text.Trim(),
                ["max_tokens"] = 8,
                ["messages"] = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = "Say OK." } }
            };
            request.Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _testResultLabel.Text = "Connection OK.";
                _testResultLabel.ForeColor = Color.DarkGreen;
            }
            else
            {
                var text = await response.Content.ReadAsStringAsync();
                _testResultLabel.Text = $"Failed: HTTP {(int)response.StatusCode}";
                _testResultLabel.ForeColor = Color.DarkRed;
                MessageBox.Show(this, text, "API test failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _testResultLabel.Text = "Failed: " + ex.Message;
            _testResultLabel.ForeColor = Color.DarkRed;
        }
    }
}
