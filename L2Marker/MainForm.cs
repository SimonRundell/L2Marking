using System.ComponentModel;
using System.Diagnostics;
using L2Marker.Models;
using L2Marker.Services;

namespace L2Marker;

public class MainForm : Form
{
    private readonly BindingList<StudentRowViewModel> _rows = new();
    private readonly ClaudeMarkingService _markingService = new();
    private AppSettings _settings = SettingsService.Load();
    private readonly Dictionary<int, UnitReference> _unitCache = new();

    private readonly ComboBox _unitCombo;
    private readonly DataGridView _grid;
    private readonly Button _startButton;
    private readonly ProgressBar _progressBar;
    private readonly Label _statusLabel;
    private readonly Label _spendLabel;
    private bool _isRunning;
    private decimal _runCostSoFar;
    private bool _warnedApproachingBudget;
    private bool _warnedOverBudget;

    public MainForm()
    {
        Text = "L2 Marker - NCFE Level 2 Coding batch marking";
        Width = 1080;
        Height = 680;
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        // ---- menu bar ----
        var menuStrip = new MenuStrip();

        var fileMenu = new ToolStripMenuItem("&File");
        var settingsMenuItem = new ToolStripMenuItem("&Settings...");
        settingsMenuItem.Click += (_, _) => OpenSettings();
        var exitMenuItem = new ToolStripMenuItem("E&xit");
        exitMenuItem.Click += (_, _) => Close();
        fileMenu.DropDownItems.Add(settingsMenuItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(exitMenuItem);

        var helpMenu = new ToolStripMenuItem("&Help");
        var aboutMenuItem = new ToolStripMenuItem("&About L2 Marker...");
        aboutMenuItem.Click += (_, _) => ShowAbout();
        helpMenu.DropDownItems.Add(aboutMenuItem);

        menuStrip.Items.Add(fileMenu);
        menuStrip.Items.Add(helpMenu);
        MainMenuStrip = menuStrip;

        // ---- top toolbar ----
        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(8, 8, 8, 4)
        };

        topPanel.Controls.Add(new Label { Text = "Unit:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 6, 4, 0) });
        _unitCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        for (int i = 1; i <= 5; i++)
            _unitCombo.Items.Add($"Unit {i}");
        _unitCombo.SelectedIndex = 1; // Unit 2 default
        topPanel.Controls.Add(_unitCombo);

        var addFilesButton = new Button { Text = "Add Files...", AutoSize = true, Margin = new Padding(16, 0, 0, 0) };
        addFilesButton.Click += (_, _) => AddFilesDialog();
        topPanel.Controls.Add(addFilesButton);

        var removeButton = new Button { Text = "Remove Selected", AutoSize = true, Margin = new Padding(4, 0, 0, 0) };
        removeButton.Click += (_, _) => RemoveSelectedRows();
        topPanel.Controls.Add(removeButton);

        var clearButton = new Button { Text = "Clear All", AutoSize = true, Margin = new Padding(4, 0, 0, 0) };
        clearButton.Click += (_, _) => { _rows.Clear(); };
        topPanel.Controls.Add(clearButton);

        // ---- grid ----
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            AllowDrop = true
        };
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FileName", HeaderText = "File", DataPropertyName = "FileName", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LearnerName", HeaderText = "Learner", DataPropertyName = "LearnerName", Width = 180, ReadOnly = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", DataPropertyName = "Status", Width = 110, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Result", HeaderText = "Result", DataPropertyName = "Result", Width = 220, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cost", HeaderText = "Cost", DataPropertyName = "Cost", Width = 70, ReadOnly = true });
        _grid.DataSource = _rows;
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name != "LearnerName") ShowDetails(_rows[e.RowIndex]); };
        _grid.CellEndEdit += Grid_CellEndEdit;

        _grid.DragEnter += Grid_DragEnter;
        _grid.DragDrop += Grid_DragDrop;
        DragEnter += Grid_DragEnter;
        DragDrop += Grid_DragDrop;

        // ---- bottom bar ----
        var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 78, Padding = new Padding(8) };

        _startButton = new Button { Text = "Start Marking", Width = 140, Height = 34, Location = new Point(8, 8) };
        _startButton.Click += async (_, _) => await StartMarkingAsync();
        bottomPanel.Controls.Add(_startButton);

        var viewDetailsButton = new Button { Text = "View Details", Width = 120, Height = 34, Location = new Point(156, 8) };
        viewDetailsButton.Click += (_, _) =>
        {
            if (_grid.SelectedRows.Count > 0)
                ShowDetails(_rows[_grid.SelectedRows[0].Index]);
        };
        bottomPanel.Controls.Add(viewDetailsButton);

        var openFolderButton = new Button { Text = "Open Output Folder", Width = 150, Height = 34, Location = new Point(284, 8) };
        openFolderButton.Click += (_, _) => OpenOutputFolderForSelection();
        bottomPanel.Controls.Add(openFolderButton);

        _progressBar = new ProgressBar { Location = new Point(8, 48), Width = 1040, Height = 20, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
        bottomPanel.Controls.Add(_progressBar);

        _statusLabel = new Label { Text = "Ready.", Location = new Point(444, 12), AutoSize = true };
        bottomPanel.Controls.Add(_statusLabel);

        _spendLabel = new Label { Location = new Point(444, 32), AutoSize = true, ForeColor = Color.DimGray };
        bottomPanel.Controls.Add(_spendLabel);
        UpdateSpendLabel();

        Controls.Add(_grid);
        Controls.Add(bottomPanel);
        Controls.Add(topPanel);
        Controls.Add(menuStrip);
    }

    private void ShowAbout()
    {
        using var about = new AboutForm(Icon);
        about.ShowDialog(this);
    }

    private void Grid_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void Grid_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths) return;
        AddFiles(paths);
    }

    /// <summary>Learner names are auto-detected but often wrong (bad "Name" cell, odd filename), so
    /// the Learner column is user-editable. An empty edit falls back to a fresh filename guess
    /// rather than leaving the marksheet with a blank learner name.</summary>
    private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "LearnerName") return;

        var row = _rows[e.RowIndex];
        var trimmed = row.LearnerName.Trim();
        row.LearnerName = string.IsNullOrWhiteSpace(trimmed) ? SafeGuessLearnerName(row.FilePath) : trimmed;
        RefreshRow(row);
    }

    private void AddFilesDialog()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Word documents (*.docx)|*.docx",
            Multiselect = true,
            Title = "Select student submissions to mark"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            AddFiles(dlg.FileNames);
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)) continue;
            var fileName = Path.GetFileName(path);
            if (fileName.StartsWith("~$")) continue;
            if (fileName.Contains("Assessor Feedback", StringComparison.OrdinalIgnoreCase)) continue;
            if (fileName.Contains("ModelAnswers", StringComparison.OrdinalIgnoreCase)) continue;
            if (fileName.EndsWith(" FB.docx", StringComparison.OrdinalIgnoreCase)) continue;
            if (_rows.Any(r => string.Equals(r.FilePath, path, StringComparison.OrdinalIgnoreCase))) continue;

            _rows.Add(new StudentRowViewModel
            {
                FilePath = path,
                LearnerName = SafeGuessLearnerName(path),
                Status = "Pending"
            });
        }
    }

    private static string SafeGuessLearnerName(string path)
    {
        try { return DocxTextExtractor.ExtractLearnerName(path); }
        catch { return Path.GetFileNameWithoutExtension(path); }
    }

    private void RemoveSelectedRows()
    {
        var indexes = _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Index).OrderByDescending(i => i).ToList();
        foreach (var i in indexes)
            _rows.RemoveAt(i);
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings);
        var result = form.ShowDialog(this);

        if (result == DialogResult.OK)
        {
            _settings = form.Settings;
            SettingsService.Save(_settings);
            _unitCache.Clear();
        }

        // Refresh regardless of OK/Cancel - "Reset Spend" inside the dialog saves immediately,
        // independent of Save/Cancel, so the label can be stale even after a cancelled dialog.
        UpdateSpendLabel();
    }

    private void UpdateSpendLabel()
    {
        _spendLabel.Text = _settings.BudgetUsd is { } budget && budget > 0
            ? $"Claude spend: ${_settings.SpentUsd:0.00} of ${budget:0.00} budget"
            : $"Claude spend: ${_settings.SpentUsd:0.00} (lifetime)";
    }

    /// <summary>
    /// Turns one call's billed usage into a dollar cost, adds it to the persisted lifetime total
    /// and this run's running total, and warns - at most once per threshold per run - as a set
    /// budget is approached or passed. Never blocks a run; this is visibility, not a cap.
    /// </summary>
    private void RecordSpend(StudentMarkingResult result)
    {
        if (result.Usage is null) return; // no response was ever billed (e.g. request never reached the API)

        _settings.SpentUsd += result.EstimatedCostUsd;
        _runCostSoFar += result.EstimatedCostUsd;
        SettingsService.Save(_settings);
        UpdateSpendLabel();

        var budget = _settings.BudgetUsd;
        if (budget is not { } b || b <= 0) return;

        if (_settings.SpentUsd >= b && !_warnedOverBudget)
        {
            _warnedOverBudget = true;
            MessageBox.Show(this,
                $"You've now spent ${_settings.SpentUsd:0.00} on Claude, over your ${b:0.00} budget. Open Settings to review or raise it.",
                "Budget exceeded", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        else if (_settings.SpentUsd >= b * 0.8m && !_warnedApproachingBudget)
        {
            _warnedApproachingBudget = true;
            MessageBox.Show(this,
                $"You've spent ${_settings.SpentUsd:0.00} of your ${b:0.00} Claude budget - getting close.",
                "Approaching budget", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ShowDetails(StudentRowViewModel row)
    {
        if (row.MarkingResult is null)
        {
            MessageBox.Show(this, "This submission hasn't been marked yet.", "No result", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var details = new DetailsForm(row.MarkingResult);
        details.ShowDialog(this);
    }

    private void OpenOutputFolderForSelection()
    {
        if (_grid.SelectedRows.Count == 0)
        {
            MessageBox.Show(this, "Select a row first.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var row = _rows[_grid.SelectedRows[0].Index];
        var folder = Path.GetDirectoryName(row.FilePath);
        if (folder is null || !Directory.Exists(folder)) return;

        var target = row.MarkingResult?.GeneratedMarksheetPath ?? row.FilePath;
        Process.Start("explorer.exe", $"/select,\"{target}\"");
    }

    private int SelectedUnitNumber => _unitCombo.SelectedIndex + 1;

    private async Task StartMarkingAsync()
    {
        if (_isRunning) return;

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            MessageBox.Show(this, "Please set your Claude API key in Settings first.", "API key required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            OpenSettings();
            if (string.IsNullOrWhiteSpace(_settings.ApiKey)) return;
        }

        if (_rows.Count == 0)
        {
            MessageBox.Show(this, "Add some student submissions first.", "Nothing to mark", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        UnitReference unitRef;
        try
        {
            unitRef = GetUnitReference(SelectedUnitNumber);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not load unit reference material", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _isRunning = true;
        _startButton.Enabled = false;
        _unitCombo.Enabled = false;
        _grid.ReadOnly = true; // freeze Learner edits mid-run to avoid racing with in-flight marking calls
        _progressBar.Value = 0;
        _progressBar.Maximum = _rows.Count;
        _statusLabel.Text = $"Marking {_rows.Count} submission(s) against {unitRef.UnitTitle} ({unitRef.Criteria.Count} criteria)...";
        _runCostSoFar = 0;
        _warnedApproachingBudget = false;
        _warnedOverBudget = false;

        foreach (var row in _rows)
        {
            row.Status = "Queued";
            row.Result = "";
        }
        _grid.Refresh();

        using var semaphore = new SemaphoreSlim(Math.Max(1, _settings.MaxConcurrency));
        var tasks = _rows.Select(row => MarkOneAsync(row, unitRef, semaphore)).ToList();

        await Task.WhenAll(tasks);

        var achievedCount = _rows.Count(r => r.MarkingResult is { Error: null } m && m.OverallAchieved);
        var errorCount = _rows.Count(r => r.MarkingResult?.Error is not null || r.Status == "Error");
        _statusLabel.Text = $"Done. {achievedCount}/{_rows.Count} fully achieved" +
                             (errorCount > 0 ? $", {errorCount} error(s)." : ".") +
                             $" This run cost approx ${_runCostSoFar:0.0000}.";

        _isRunning = false;
        _startButton.Enabled = true;
        _unitCombo.Enabled = true;
        _grid.ReadOnly = false;
    }

    private async Task MarkOneAsync(StudentRowViewModel row, UnitReference unitRef, SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        try
        {
            row.Status = "Marking...";
            RefreshRow(row);

            var result = await _markingService.MarkStudentAsync(_settings, unitRef, row.FilePath, row.LearnerName);
            row.MarkingResult = result;
            RecordSpend(result);

            if (result.Error is not null)
            {
                row.Status = "Error";
                row.Result = result.Error;
            }
            else
            {
                try
                {
                    var marksheetPath = MarksheetFiller.CreateFilledMarksheet(unitRef, result, _settings.AssessorName, _settings.OutputFolder);
                    result.GeneratedMarksheetPath = marksheetPath;
                    row.Status = "Done";
                    row.Result = $"{result.AchievedCount}/{result.TotalCount} - {(result.OverallAchieved ? "ACHIEVED" : "NOT YET ACHIEVED")}";
                }
                catch (Exception ex)
                {
                    row.Status = "Marked (file error)";
                    row.Result = $"Marking OK but could not write marksheet: {ex.Message}";
                }
            }
        }
        catch (Exception ex)
        {
            row.Status = "Error";
            row.Result = ex.Message;
        }
        finally
        {
            RefreshRow(row);
            _progressBar.Value = Math.Min(_progressBar.Maximum, _progressBar.Value + 1);
            semaphore.Release();
        }
    }

    private void RefreshRow(StudentRowViewModel row)
    {
        var index = _rows.IndexOf(row);
        if (index >= 0) _rows.ResetItem(index);
    }

    private UnitReference GetUnitReference(int unitNumber)
    {
        if (_unitCache.TryGetValue(unitNumber, out var cached))
            return cached;

        var reference = UnitReferenceService.Load(_settings.ReferenceFolder, unitNumber);
        _unitCache[unitNumber] = reference;
        return reference;
    }
}
