using System.Drawing;

namespace SystemAnalysis.UI;

public sealed class RunStatusOverlayForm : Form
{
    private readonly Label _clickCountValueLabel;
    private readonly Label _completedItemsValueLabel;

    public RunStatusOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.FromArgb(18, 18, 18);
        ForeColor = Color.White;
        Opacity = 0.92;
        Size = new Size(320, 220);
        Padding = new Padding(20);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var clickLabel = new Label
        {
            Text = "Sol Tik",
            AutoSize = false,
            Width = 260,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.Gainsboro
        };
        _clickCountValueLabel = new Label
        {
            Text = "0",
            AutoSize = false,
            Width = 260,
            Height = 48,
            Font = new Font("Segoe UI", 32, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var completedLabel = new Label
        {
            Text = "Tamamlanan Item",
            AutoSize = false,
            Width = 260,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.Gainsboro,
            Margin = new Padding(0, 16, 0, 0)
        };
        _completedItemsValueLabel = new Label
        {
            Text = "0",
            AutoSize = false,
            Width = 260,
            Height = 48,
            Font = new Font("Segoe UI", 32, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };

        layout.Controls.Add(clickLabel, 0, 0);
        layout.Controls.Add(_clickCountValueLabel, 0, 1);
        layout.Controls.Add(completedLabel, 0, 2);
        layout.Controls.Add(_completedItemsValueLabel, 0, 3);

        Controls.Add(layout);
    }

    public void UpdateProgress(int totalClicks, int completedItems)
    {
        _clickCountValueLabel.Text = totalClicks.ToString();
        _completedItemsValueLabel.Text = completedItems.ToString();
    }

    public void PositionOnScreen()
    {
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(workingArea.Right - Width - 24, workingArea.Top + 24);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExToolWindow = 0x00000080;
            const int wsExNoActivate = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= wsExToolWindow | wsExNoActivate;
            return cp;
        }
    }
}
