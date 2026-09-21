using L2Marker.Models;

namespace L2Marker;

/// <summary>Read-only drill-down into one learner's full marking result.</summary>
public class DetailsForm : Form
{
    public DetailsForm(StudentMarkingResult result)
    {
        Text = $"{result.LearnerName} - marking detail";
        Width = 900;
        Height = 640;
        StartPosition = FormStartPosition.CenterParent;

        var top = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(12) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        top.Controls.Add(new Label { Text = "Learner:", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        top.Controls.Add(new Label { Text = result.LearnerName, AutoSize = true }, 1, 0);

        top.Controls.Add(new Label { Text = "Overall:", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 1);
        top.Controls.Add(new Label
        {
            Text = $"{(result.OverallAchieved ? "ACHIEVED" : "NOT YET ACHIEVED")}  ({result.AchievedCount}/{result.TotalCount} criteria)",
            AutoSize = true,
            ForeColor = result.OverallAchieved ? Color.DarkGreen : Color.DarkOrange
        }, 1, 1);

        top.Controls.Add(new Label { Text = "Feedback:", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 2);
        top.Controls.Add(new Label { Text = result.OverallFeedback, AutoSize = true, MaximumSize = new Size(700, 0) }, 1, 2);

        top.Controls.Add(new Label { Text = "Further actions:", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 3);
        top.Controls.Add(new Label { Text = result.FurtherActions, AutoSize = true, MaximumSize = new Size(700, 0) }, 1, 3);

        top.Controls.Add(new Label { Text = "Cost:", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 4);
        top.Controls.Add(new Label
        {
            Text = result.Usage is { } u
                ? $"${result.EstimatedCostUsd:0.0000}  ({u.InputTokens} in / {u.OutputTokens} out / {u.CacheCreationInputTokens} cache-write / {u.CacheReadInputTokens} cache-read)"
                : "(no usage recorded)",
            AutoSize = true,
            ForeColor = Color.DimGray
        }, 1, 4);

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "Criterion", Width = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Achieved", HeaderText = "Result", Width = 110 });
        var commentCol = new DataGridViewTextBoxColumn
        {
            Name = "Comment",
            HeaderText = "Assessor comment",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };
        commentCol.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.Columns.Add(commentCol);

        foreach (var c in result.Criteria)
        {
            var rowIndex = grid.Rows.Add(c.Id, c.Achieved ? "Achieved" : "Not Achieved", c.Comment);
            grid.Rows[rowIndex].DefaultCellStyle.ForeColor = c.Achieved ? Color.DarkGreen : Color.DarkRed;
        }

        var closeButton = new Button { Text = "Close", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK, Height = 36 };
        CancelButton = closeButton;
        AcceptButton = closeButton;

        Controls.Add(grid);
        Controls.Add(closeButton);
        Controls.Add(top);
    }
}
