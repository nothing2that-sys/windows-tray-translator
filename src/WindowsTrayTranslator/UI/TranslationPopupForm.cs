using WindowsTrayTranslator.App;

namespace WindowsTrayTranslator.UI;

public sealed class TranslationPopupForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int MaximumTextWidth = 480;

    private readonly TextBox messageLabel = new()
    {
        AutoSize = false,
        Font = new Font("Segoe UI", 10f),
        Multiline = true,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        ScrollBars = ScrollBars.None,
        WordWrap = true,
        TabStop = false,
        Cursor = Cursors.Arrow
    };
    private readonly Button copyButton = new() { Text = "복사", AutoSize = true, TabStop = false };
    private readonly Button closeButton = new() { Text = "닫기", AutoSize = true, TabStop = false };
    private readonly System.Windows.Forms.Timer closeTimer = new();
    private string? copyText;
    private int closeTimeoutMs;

    public TranslationPopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Padding = new Padding(12);
        BackColor = Color.FromArgb(35, 38, 45);
        ForeColor = Color.White;
        Text = "WindowsTrayTranslator.TranslationPopup";

        messageLabel.ForeColor = ForeColor;
        messageLabel.BackColor = BackColor;
        copyButton.Click += (_, _) => CopyResult();
        closeButton.Click += (_, _) => Close();
        closeTimer.Tick += (_, _) => Close();
        MouseEnter += (_, _) => closeTimer.Stop();
        MouseLeave += (_, _) => RestartTimer();
        messageLabel.MouseEnter += (_, _) => closeTimer.Stop();
        messageLabel.MouseLeave += (_, _) => RestartTimer();
        Click += (_, _) => Close();

        Controls.Add(messageLabel);
        Controls.Add(copyButton);
        Controls.Add(closeButton);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= WsExNoActivate | WsExToolWindow;
            return parameters;
        }
    }

    public void ShowContent(
        TranslationPopupKind kind,
        string message,
        string? resultToCopy,
        int timeoutMs)
    {
        closeTimer.Stop();
        copyText = resultToCopy;
        closeTimeoutMs = kind == TranslationPopupKind.Translating ? 0 : timeoutMs;
        messageLabel.Text = message;
        copyButton.Visible = !string.IsNullOrEmpty(resultToCopy);

        BackColor = kind switch
        {
            TranslationPopupKind.Error => Color.FromArgb(92, 35, 39),
            TranslationPopupKind.Warning => Color.FromArgb(92, 70, 24),
            _ => Color.FromArgb(35, 38, 45)
        };
        messageLabel.BackColor = BackColor;

        LayoutContent();
        PositionNearCursor();
        if (!Visible)
        {
            Show();
        }
        else
        {
            Invalidate();
        }

        RestartTimer();
    }

    private void LayoutContent()
    {
        Size measured = TextRenderer.MeasureText(
            messageLabel.Text,
            messageLabel.Font,
            new Size(MaximumTextWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);

        Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        int maximumTextHeight = Math.Max(120, workingArea.Height - 140);
        int textWidth = Math.Clamp(measured.Width + 4, 180, MaximumTextWidth);
        int textHeight = Math.Clamp(measured.Height + 4, 28, maximumTextHeight);
        messageLabel.ScrollBars = measured.Height + 4 > maximumTextHeight
            ? ScrollBars.Vertical
            : ScrollBars.None;
        messageLabel.SetBounds(Padding.Left, Padding.Top, textWidth, textHeight);

        int buttonTop = messageLabel.Bottom + 10;
        closeButton.Location = new Point(Padding.Left + textWidth - closeButton.PreferredSize.Width, buttonTop);
        copyButton.Location = new Point(closeButton.Left - copyButton.PreferredSize.Width - 8, buttonTop);
        int buttonsBottom = Math.Max(copyButton.Bottom, closeButton.Bottom);
        ClientSize = new Size(textWidth + Padding.Horizontal, buttonsBottom + Padding.Bottom);
    }

    private void PositionNearCursor()
    {
        Point cursor = Cursor.Position;
        Rectangle workingArea = Screen.FromPoint(cursor).WorkingArea;
        Location = PopupPositionCalculator.Calculate(cursor, Size, workingArea);
    }

    private void CopyResult()
    {
        if (string.IsNullOrEmpty(copyText))
        {
            return;
        }

        System.Windows.Forms.Clipboard.SetText(copyText, TextDataFormat.UnicodeText);
        Close();
    }

    private void RestartTimer()
    {
        closeTimer.Stop();
        if (closeTimeoutMs <= 0 || IsDisposed)
        {
            return;
        }

        closeTimer.Interval = Math.Clamp(closeTimeoutMs, 1000, 60000);
        closeTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            closeTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
