using WindowsTrayTranslator.App;

namespace WindowsTrayTranslator.UI;

public sealed class TranslationPopupForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int MaximumTextWidth = 480;

    private readonly Label titleLabel = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 9.5f),
        ForeColor = Color.FromArgb(120, 181, 255),
        BackColor = Color.Transparent
    };
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
    private Color accentColor = Color.FromArgb(39, 139, 245);

    public TranslationPopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Padding = new Padding(16, 12, 12, 12);
        BackColor = Color.FromArgb(35, 38, 45);
        ForeColor = Color.White;
        Text = "WindowsTrayTranslator.TranslationPopup";

        messageLabel.ForeColor = ForeColor;
        messageLabel.BackColor = BackColor;
        StyleActionButton(copyButton, primary: true);
        StyleActionButton(closeButton, primary: false);
        copyButton.Click += (_, _) => CopyResult();
        closeButton.Click += (_, _) => Close();
        closeTimer.Tick += (_, _) => Close();
        MouseEnter += (_, _) => closeTimer.Stop();
        MouseLeave += (_, _) => RestartTimer();
        titleLabel.MouseEnter += (_, _) => closeTimer.Stop();
        titleLabel.MouseLeave += (_, _) => RestartTimer();
        messageLabel.MouseEnter += (_, _) => closeTimer.Stop();
        messageLabel.MouseLeave += (_, _) => RestartTimer();
        Click += (_, _) => Close();

        Controls.Add(titleLabel);
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
        titleLabel.Text = kind switch
        {
            TranslationPopupKind.Translating => "번역 중",
            TranslationPopupKind.Success => "번역 완료",
            TranslationPopupKind.Warning => "확인이 필요합니다",
            TranslationPopupKind.Error => "번역 오류",
            _ => "알림"
        };
        copyButton.Visible = !string.IsNullOrEmpty(resultToCopy);

        BackColor = Color.FromArgb(31, 35, 43);
        accentColor = kind switch
        {
            TranslationPopupKind.Error => Color.FromArgb(255, 100, 110),
            TranslationPopupKind.Warning => Color.FromArgb(255, 191, 72),
            _ => Color.FromArgb(54, 155, 255)
        };
        titleLabel.ForeColor = accentColor;
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

    private static void StyleActionButton(Button button, bool primary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(105, 112, 126);
        button.BackColor = primary ? Color.FromArgb(35, 119, 220) : Color.FromArgb(52, 56, 65);
        button.ForeColor = Color.White;
        button.Padding = new Padding(8, 1, 8, 1);
        button.UseVisualStyleBackColor = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using SolidBrush accentBrush = new(accentColor);
        e.Graphics.FillRectangle(accentBrush, 0, 0, 4, ClientSize.Height);
        using Pen borderPen = new(Color.FromArgb(80, 255, 255, 255));
        e.Graphics.DrawRectangle(borderPen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
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
        titleLabel.Location = new Point(Padding.Left, Padding.Top);
        messageLabel.SetBounds(Padding.Left, titleLabel.Bottom + 6, textWidth, textHeight);

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
