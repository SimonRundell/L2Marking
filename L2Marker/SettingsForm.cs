using System.Text.Json.Nodes;
using L2Marker.Models;
using L2Marker.Services;

namespace L2Marker;

public class SettingsForm : Form
{
    private readonly TextBox _apiKeyBox;
    private readonly ComboBox _modelBox;
    private readonly TextBox _referenceFolderBox;
    private readonly TextBox _outputFolderBox;
    private readonly NumericUpDown _concurrencyBox;
    private readonly TextBox _assessorNameBox;
    private readonly Label _testResultLabel;
    private readonly NumericUpDown _inputPriceBox;
    private readonly NumericUpDown _outputPriceBox;
    private readonly NumericUpDown _cacheWritePriceBox;
    private readonly NumericUpDown _cacheReadPriceBox;
    private readonly TextBox _budgetBox;
    private readonly Label _spentLabel;
    private readonly AppSettings _current;

    public AppSettings Settings { get; private set; }

    public SettingsForm(AppSettings current)
    {
        Settings = current;
        _current = current;

        Text = "Settings";
        Width = 640;
        Height = 700;
        MinimumSize = new Size(640, 400);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;

        // The row count here has grown as features were added (cost tracking, output folder, ...)
        // and will likely grow again - scroll instead of Dock=Fill, so a dialog shorter than its
        // content gets a scrollbar rather than every row being squeezed to fit (which is what was
        // happening: fields overlapping their neighbours).
        var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            Padding = new Padding(16),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
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

        layout.Controls.Add(new Label { Text = "Output folder:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _outputFolderBox = new TextBox { Dock = DockStyle.Fill, Text = current.OutputFolder };
        layout.Controls.Add(_outputFolderBox, 1, row);
        var outputBrowseButton = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        outputBrowseButton.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = string.IsNullOrWhiteSpace(_outputFolderBox.Text) ? current.ReferenceFolder : _outputFolderBox.Text };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                _outputFolderBox.Text = dlg.SelectedPath;
        };
        layout.Controls.Add(outputBrowseButton, 2, row);
        row++;

        var outputHint = new Label
        {
            Text = "Where generated marksheets are saved. Leave blank to save each one next to the student's own file (the default).",
            AutoSize = true,
            ForeColor = Color.DimGray,
            MaximumSize = new Size(460, 0)
        };
        layout.Controls.Add(outputHint, 1, row);
        layout.SetColumnSpan(outputHint, 2);
        var clearOutputButton = new Button { Text = "Clear", Dock = DockStyle.Fill, Height = 24 };
        clearOutputButton.Click += (_, _) => _outputFolderBox.Text = "";
        layout.Controls.Add(clearOutputButton, 2, row);
        row++;

        layout.Controls.Add(new Label { Text = "Assessor name:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _assessorNameBox = new TextBox { Dock = DockStyle.Fill, Text = current.AssessorName };
        layout.Controls.Add(_assessorNameBox, 1, row);
        row++;

        layout.Controls.Add(new Label { Text = "Max concurrent calls:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _concurrencyBox = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 10, Value = Math.Clamp(current.MaxConcurrency, 1, 10) };
        layout.Controls.Add(_concurrencyBox, 1, row);
        row++;

        var separator = new Label
        {
            Text = "Cost tracking",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 12, 0, 4)
        };
        layout.Controls.Add(separator, 0, row);
        layout.SetColumnSpan(separator, 3);
        row++;

        var costHint = new Label
        {
            Text = "Anthropic has no \"check my balance\" API, so spend is estimated locally from each call's " +
                   "reported token usage at the $/million-token rates below. Treat it as a guide, not the invoice.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            MaximumSize = new Size(460, 0)
        };
        layout.Controls.Add(costHint, 1, row);
        layout.SetColumnSpan(costHint, 2);
        row++;

        NumericUpDown MakePriceBox(decimal value) => new()
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 1000,
            DecimalPlaces = 3,
            Increment = 0.01m,
            Value = value
        };

        layout.Controls.Add(new Label { Text = "Input $/million tokens:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _inputPriceBox = MakePriceBox(current.InputPricePerMillionTokens);
        layout.Controls.Add(_inputPriceBox, 1, row);
        var presetButton = new Button { Text = "Use standard pricing", Dock = DockStyle.Fill };
        presetButton.Click += (_, _) => ApplyPricingPreset();
        layout.Controls.Add(presetButton, 2, row);
        row++;

        layout.Controls.Add(new Label { Text = "Output $/million tokens:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _outputPriceBox = MakePriceBox(current.OutputPricePerMillionTokens);
        layout.Controls.Add(_outputPriceBox, 1, row);
        row++;

        layout.Controls.Add(new Label { Text = "Cache write $/million:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _cacheWritePriceBox = MakePriceBox(current.CacheWritePricePerMillionTokens);
        layout.Controls.Add(_cacheWritePriceBox, 1, row);
        row++;

        layout.Controls.Add(new Label { Text = "Cache read $/million:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _cacheReadPriceBox = MakePriceBox(current.CacheReadPricePerMillionTokens);
        layout.Controls.Add(_cacheReadPriceBox, 1, row);
        row++;

        layout.Controls.Add(new Label { Text = "Budget (USD, optional):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _budgetBox = new TextBox { Dock = DockStyle.Fill, Text = current.BudgetUsd?.ToString("0.00") ?? "" };
        layout.Controls.Add(_budgetBox, 1, row);
        row++;

        layout.Controls.Add(new Label { Text = "Spent so far:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
        _spentLabel = new Label { Dock = DockStyle.Fill, Text = $"${current.SpentUsd:0.00}", AutoSize = false, Height = 20 };
        layout.Controls.Add(_spentLabel, 1, row);
        var resetButton = new Button { Text = "Reset Spend", Dock = DockStyle.Fill };
        resetButton.Click += (_, _) => ResetSpend();
        layout.Controls.Add(resetButton, 2, row);
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

        scrollPanel.Controls.Add(layout);
        Controls.Add(scrollPanel);
        Controls.Add(buttonPanel);
    }

    private void SaveIntoSettings()
    {
        decimal? budget = null;
        if (!string.IsNullOrWhiteSpace(_budgetBox.Text) && decimal.TryParse(_budgetBox.Text, out var parsedBudget))
            budget = parsedBudget;

        Settings = new AppSettings
        {
            ApiKey = _apiKeyBox.Text.Trim(),
            Model = _modelBox.Text.Trim(),
            ReferenceFolder = _referenceFolderBox.Text.Trim(),
            OutputFolder = _outputFolderBox.Text.Trim(),
            MaxConcurrency = (int)_concurrencyBox.Value,
            AssessorName = _assessorNameBox.Text.Trim(),
            MaxOutputTokens = 4096,
            InputPricePerMillionTokens = _inputPriceBox.Value,
            OutputPricePerMillionTokens = _outputPriceBox.Value,
            CacheWritePricePerMillionTokens = _cacheWritePriceBox.Value,
            CacheReadPricePerMillionTokens = _cacheReadPriceBox.Value,
            BudgetUsd = budget,
            SpentUsd = _current.SpentUsd
        };
    }

    private void ApplyPricingPreset()
    {
        if (ClaudePricingPresets.TryGet(_modelBox.Text.Trim(), out var preset))
        {
            _inputPriceBox.Value = preset.Input;
            _outputPriceBox.Value = preset.Output;
            _cacheWritePriceBox.Value = preset.CacheWrite;
            _cacheReadPriceBox.Value = preset.CacheRead;
        }
        else
        {
            MessageBox.Show(this,
                $"No standard pricing is known for '{_modelBox.Text.Trim()}'. Enter it manually from Anthropic's pricing page.",
                "No preset available", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ResetSpend()
    {
        var confirm = MessageBox.Show(this,
            "Reset the recorded Claude spend total back to $0.00? This can't be undone.",
            "Reset spend", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        _current.SpentUsd = 0;
        SettingsService.Save(_current);
        _spentLabel.Text = "$0.00";
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
