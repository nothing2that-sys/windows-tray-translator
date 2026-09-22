using WindowsTrayTranslator.App;
using WindowsTrayTranslator.History;

namespace WindowsTrayTranslator.UI;

public sealed class TranslationHistoryForm : Form
{
    private readonly ApplicationController controller;
    private readonly DataGridView historyGrid = new();
    private readonly TextBox originalTextBox = CreateDetailTextBox();
    private readonly TextBox translatedTextBox = CreateDetailTextBox();
    private readonly Button copyButton = new() { Text = "번역문 복사", AutoSize = true };
    private IReadOnlyList<TranslationHistoryEntry> entries = [];

    public TranslationHistoryForm(ApplicationController controller)
    {
        this.controller = controller;
        Text = "번역 히스토리";
        Icon = AppIconProvider.ApplicationIcon;
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 520);
        ClientSize = new Size(940, 650);
        BackColor = AppTheme.Canvas;

        ConfigureGrid();
        SplitContainer details = CreateDetails();
        Button refreshButton = new() { Text = "새로고침", AutoSize = true };
        Button closeButton = new() { Text = "닫기", AutoSize = true };
        AppTheme.StyleSecondaryButton(refreshButton);
        AppTheme.StyleSecondaryButton(closeButton);
        AppTheme.StylePrimaryButton(copyButton);
        copyButton.Enabled = false;
        refreshButton.Click += (_, _) => RefreshEntries();
        copyButton.Click += (_, _) => CopyTranslation();
        closeButton.Click += (_, _) => Close();

        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            Padding = new Padding(12, 10, 12, 8),
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = AppTheme.Surface
        };
        actions.Controls.Add(closeButton);
        actions.Controls.Add(copyButton);
        actions.Controls.Add(refreshButton);

        Label header = new()
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(14, 14, 8, 0),
            Font = new Font("Segoe UI Semibold", 12f),
            ForeColor = AppTheme.PrimaryDark,
            BackColor = AppTheme.Surface,
            Text = "최근 번역 기록"
        };

        Controls.Add(details);
        Controls.Add(actions);
        Controls.Add(header);
        RefreshEntries();
    }

    public void RefreshEntries()
    {
        TranslationHistoryEntry? selected = SelectedEntry;
        entries = controller.LoadTranslationHistory();
        historyGrid.Rows.Clear();
        foreach (TranslationHistoryEntry entry in entries)
        {
            int rowIndex = historyGrid.Rows.Add(
                entry.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                entry.Kind == TranslationHistoryKind.Read ? "읽기" : "작성",
                entry.TargetLanguage,
                CreatePreview(entry.OriginalText),
                CreatePreview(entry.TranslatedText));
            historyGrid.Rows[rowIndex].Tag = entry;
        }

        if (historyGrid.Rows.Count > 0)
        {
            int selectedIndex = selected is null
                ? 0
                : entries.ToList().FindIndex(item => item == selected);
            historyGrid.Rows[Math.Max(0, selectedIndex)].Selected = true;
            historyGrid.CurrentCell = historyGrid.Rows[Math.Max(0, selectedIndex)].Cells[0];
        }
        else
        {
            ShowDetails(null);
        }
    }

    private TranslationHistoryEntry? SelectedEntry =>
        historyGrid.SelectedRows.Count > 0
            ? historyGrid.SelectedRows[0].Tag as TranslationHistoryEntry
            : null;

    private void ConfigureGrid()
    {
        historyGrid.Dock = DockStyle.Fill;
        historyGrid.BackgroundColor = AppTheme.Surface;
        historyGrid.BorderStyle = BorderStyle.None;
        historyGrid.AllowUserToAddRows = false;
        historyGrid.AllowUserToDeleteRows = false;
        historyGrid.AllowUserToResizeRows = false;
        historyGrid.ReadOnly = true;
        historyGrid.MultiSelect = false;
        historyGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        historyGrid.RowHeadersVisible = false;
        historyGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        historyGrid.Columns.Add("Time", "시간");
        historyGrid.Columns.Add("Kind", "구분");
        historyGrid.Columns.Add("Target", "대상 언어");
        historyGrid.Columns.Add("Original", "원문");
        historyGrid.Columns.Add("Translation", "번역문");
        historyGrid.Columns["Time"]!.FillWeight = 85;
        historyGrid.Columns["Kind"]!.FillWeight = 40;
        historyGrid.Columns["Target"]!.FillWeight = 60;
        historyGrid.Columns["Original"]!.FillWeight = 130;
        historyGrid.Columns["Translation"]!.FillWeight = 130;
        historyGrid.SelectionChanged += (_, _) => ShowDetails(SelectedEntry);
    }

    private SplitContainer CreateDetails()
    {
        SplitContainer vertical = new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 315,
            BackColor = AppTheme.Border
        };
        vertical.Panel1.Controls.Add(historyGrid);

        SplitContainer textDetails = new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 455,
            BackColor = AppTheme.Border
        };
        textDetails.Panel1.Controls.Add(CreateDetailPanel("원문", originalTextBox));
        textDetails.Panel2.Controls.Add(CreateDetailPanel("번역문", translatedTextBox));
        vertical.Panel2.Controls.Add(textDetails);
        return vertical;
    }

    private static Panel CreateDetailPanel(string title, TextBox textBox)
    {
        Panel panel = new() { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = AppTheme.Surface };
        Label label = new()
        {
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI Semibold", 9.5f),
            ForeColor = AppTheme.Text,
            Text = title
        };
        panel.Controls.Add(textBox);
        panel.Controls.Add(label);
        return panel;
    }

    private void ShowDetails(TranslationHistoryEntry? entry)
    {
        originalTextBox.Text = entry?.OriginalText ?? string.Empty;
        translatedTextBox.Text = entry?.TranslatedText ?? string.Empty;
        copyButton.Enabled = entry is not null;
    }

    private void CopyTranslation()
    {
        if (SelectedEntry is { TranslatedText.Length: > 0 } entry)
        {
            System.Windows.Forms.Clipboard.SetText(entry.TranslatedText, TextDataFormat.UnicodeText);
        }
    }

    private static TextBox CreateDetailTextBox() => new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = true,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = AppTheme.Canvas
    };

    private static string CreatePreview(string value)
    {
        string preview = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        return preview.Length <= 100 ? preview : preview[..100] + "…";
    }
}
