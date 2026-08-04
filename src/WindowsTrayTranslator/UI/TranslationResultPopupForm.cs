using WindowsTrayTranslator.App;

namespace WindowsTrayTranslator.UI;

public sealed class TranslationResultPopupForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int MaximumTextWidth = 540;
    private static readonly Color Surface = Color.FromArgb(31, 35, 43);
    private static readonly Color SecondarySurface = Color.FromArgb(38, 43, 53);
    private static readonly Color MutedText = Color.FromArgb(159, 168, 184);

    private readonly Label titleLabel = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 10f),
        BackColor = Color.Transparent
    };
    private readonly Label primaryModelLabel = CreateModelLabel();
    private readonly Label secondaryModelLabel = CreateModelLabel();
    private readonly TextBox primaryTextBox = CreateTextBox(Surface);
    private readonly TextBox secondaryTextBox = CreateTextBox(SecondarySurface);
    private readonly Button actionButton = new() { AutoSize = true, TabStop = false };
    private readonly Button primaryCopyButton = new() { AutoSize = true, TabStop = false };
    private readonly Button secondaryCopyButton = new() { Text = "백업 번역 복사", AutoSize = true, TabStop = false };
    private readonly Button closeButton = new() { Text = "닫기", AutoSize = true, TabStop = false };
    private readonly System.Windows.Forms.Timer closeTimer = new();
    private string? primaryCopyText;
    private string? secondaryCopyText;
    private int closeTimeoutMs;
    private long operationId;
    private TranslationPopupAction currentAction;
    private Color accentColor = Color.FromArgb(54, 155, 255);
    private Rectangle secondaryCardBounds;

    public TranslationResultPopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Padding = new Padding(18, 14, 14, 14);
        BackColor = Surface;
        ForeColor = Color.White;
        Text = "WindowsTrayTranslator.TranslationPopup";

        StyleButton(actionButton, primary: false);
        StyleButton(primaryCopyButton, primary: true);
        StyleButton(secondaryCopyButton, primary: true);
        StyleButton(closeButton, primary: false);
        actionButton.Click += (_, _) => RequestFallback();
        primaryCopyButton.Click += (_, _) => CopyResult(primaryCopyText);
        secondaryCopyButton.Click += (_, _) => CopyResult(secondaryCopyText);
        closeButton.Click += (_, _) => Close();
        closeTimer.Tick += (_, _) => Close();

        Controls.AddRange([
            titleLabel,
            primaryModelLabel,
            primaryTextBox,
            secondaryModelLabel,
            secondaryTextBox,
            actionButton,
            primaryCopyButton,
            secondaryCopyButton,
            closeButton]);
        foreach (Control control in Controls)
        {
            control.MouseEnter += (_, _) => closeTimer.Stop();
            control.MouseLeave += (_, _) => RestartTimer();
        }
        MouseEnter += (_, _) => closeTimer.Stop();
        MouseLeave += (_, _) => RestartTimer();
    }

    public event EventHandler<FallbackTranslationRequestedEventArgs>? FallbackTranslationRequested;
    public event EventHandler<TranslationPopupDismissedEventArgs>? Dismissed;

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

    public void ShowContent(TranslationPopupRequestedEventArgs content)
    {
        closeTimer.Stop();
        operationId = content.OperationId;
        currentAction = content.Action;
        primaryCopyText = content.CopyText;
        secondaryCopyText = IsCompletedSecondary(content) ? content.SecondaryMessage : null;
        closeTimeoutMs = content.Kind == TranslationPopupKind.Translating ? 0 : content.TimeoutMs;
        primaryTextBox.Text = content.Message;
        secondaryTextBox.Text = content.SecondaryMessage ?? string.Empty;
        titleLabel.Text = content.Title ?? content.Kind switch
        {
            TranslationPopupKind.Translating => "번역 중",
            TranslationPopupKind.Success => "번역 완료",
            TranslationPopupKind.Warning => "확인이 필요합니다",
            TranslationPopupKind.Error => "번역 오류",
            _ => "알림"
        };

        bool hasSecondary = content.SecondaryMessage is not null;
        primaryModelLabel.Text = FormatModelLabel(hasSecondary ? "기본 번역" : "사용 모델", content.PrimaryModel);
        primaryModelLabel.Visible = !string.IsNullOrWhiteSpace(content.PrimaryModel);
        secondaryModelLabel.Text = FormatModelLabel("백업 번역", content.SecondaryModel);
        secondaryModelLabel.Visible = hasSecondary;
        secondaryTextBox.Visible = hasSecondary;
        ConfigureActionButton(content.Action);
        primaryCopyButton.Visible = !string.IsNullOrEmpty(content.CopyText);
        primaryCopyButton.Text = hasSecondary ? "기본 번역 복사" : "번역 복사";
        secondaryCopyButton.Visible = secondaryCopyText is not null;

        accentColor = content.Kind switch
        {
            TranslationPopupKind.Error => Color.FromArgb(255, 100, 110),
            TranslationPopupKind.Warning => Color.FromArgb(255, 191, 72),
            _ => Color.FromArgb(54, 155, 255)
        };
        titleLabel.ForeColor = accentColor;

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

    private static Label CreateModelLabel() => new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 8.5f),
        ForeColor = MutedText,
        BackColor = Color.Transparent
    };

    private static TextBox CreateTextBox(Color background) => new()
    {
        Font = new Font("Segoe UI", 10f),
        ForeColor = Color.White,
        BackColor = background,
        Multiline = true,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        WordWrap = true,
        TabStop = false,
        Cursor = Cursors.Arrow
    };

    private static string FormatModelLabel(string role, string? model) =>
        string.IsNullOrWhiteSpace(model) ? role : $"{role}  ·  {model}";

    private static bool IsCompletedSecondary(TranslationPopupRequestedEventArgs content) =>
        content.SecondaryMessage is not null &&
        content.Action is not (TranslationPopupAction.FallbackInProgress or TranslationPopupAction.RetryFallback);

    private void ConfigureActionButton(TranslationPopupAction action)
    {
        actionButton.Visible = action != TranslationPopupAction.None;
        actionButton.Enabled = action != TranslationPopupAction.FallbackInProgress;
        actionButton.Text = action switch
        {
            TranslationPopupAction.SwitchToFallback => "백업 모델로 전환",
            TranslationPopupAction.ShowFallback => "다른 모델 번역 보기",
            TranslationPopupAction.FallbackInProgress => "백업 모델 응답 대기 중…",
            TranslationPopupAction.RetryFallback => "백업 모델 다시 시도",
            _ => string.Empty
        };
        actionButton.BackColor = action == TranslationPopupAction.SwitchToFallback
            ? Color.FromArgb(43, 77, 116)
            : Color.FromArgb(52, 56, 65);
        actionButton.ForeColor = action == TranslationPopupAction.FallbackInProgress ? MutedText : Color.White;
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(105, 112, 126);
        button.BackColor = primary ? Color.FromArgb(35, 119, 220) : Color.FromArgb(52, 56, 65);
        button.ForeColor = Color.White;
        button.Padding = new Padding(9, 2, 9, 2);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using SolidBrush accentBrush = new(accentColor);
        e.Graphics.FillRectangle(accentBrush, 0, 0, 4, ClientSize.Height);
        using Pen borderPen = new(Color.FromArgb(80, 255, 255, 255));
        e.Graphics.DrawRectangle(borderPen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        if (!secondaryCardBounds.IsEmpty)
        {
            using SolidBrush cardBrush = new(SecondarySurface);
            e.Graphics.FillRectangle(cardBrush, secondaryCardBounds);
            using Pen cardBorder = new(Color.FromArgb(72, 112, 154));
            e.Graphics.DrawRectangle(cardBorder, secondaryCardBounds);
        }
    }

    private void LayoutContent()
    {
        Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        List<Button> buttons = VisibleButtons();
        int buttonsWidth = buttons.Sum(button => button.PreferredSize.Width) + Math.Max(0, buttons.Count - 1) * 8;
        int contentWidth = Math.Clamp(
            Math.Max(Math.Max(MeasureWidth(primaryTextBox.Text), MeasureWidth(secondaryTextBox.Text)), buttonsWidth),
            320,
            MaximumTextWidth);

        int y = Padding.Top;
        titleLabel.Location = new Point(Padding.Left, y);
        y = titleLabel.Bottom + 10;
        if (primaryModelLabel.Visible)
        {
            primaryModelLabel.Location = new Point(Padding.Left, y);
            y = primaryModelLabel.Bottom + 5;
        }

        int maximumContentHeight = Math.Max(150, workingArea.Height - 190);
        int sectionMaximum = secondaryTextBox.Visible
            ? Math.Max(72, (maximumContentHeight - 42) / 2)
            : maximumContentHeight;
        int primaryHeight = MeasureHeight(primaryTextBox.Text, contentWidth, sectionMaximum, primaryTextBox);
        primaryTextBox.SetBounds(Padding.Left, y, contentWidth, primaryHeight);
        y = primaryTextBox.Bottom;

        secondaryCardBounds = Rectangle.Empty;
        if (secondaryTextBox.Visible)
        {
            y += 12;
            int cardTop = y;
            secondaryModelLabel.Location = new Point(Padding.Left + 10, y + 9);
            y = secondaryModelLabel.Bottom + 5;
            int secondaryHeight = MeasureHeight(secondaryTextBox.Text, contentWidth - 20, sectionMaximum, secondaryTextBox);
            secondaryTextBox.SetBounds(Padding.Left + 10, y, contentWidth - 20, secondaryHeight);
            y = secondaryTextBox.Bottom + 10;
            secondaryCardBounds = new Rectangle(Padding.Left, cardTop, contentWidth, y - cardTop);
        }

        y += 11;
        int buttonX = Padding.Left + contentWidth;
        for (int index = buttons.Count - 1; index >= 0; index--)
        {
            Button button = buttons[index];
            buttonX -= button.PreferredSize.Width;
            button.Location = new Point(buttonX, y);
            buttonX -= 8;
        }
        int bottom = buttons.Count == 0 ? y : buttons.Max(button => button.Bottom);
        ClientSize = new Size(contentWidth + Padding.Horizontal, bottom + Padding.Bottom);
    }

    private List<Button> VisibleButtons() =>
        new[] { actionButton, primaryCopyButton, secondaryCopyButton, closeButton }
            .Where(button => button.Visible)
            .ToList();

    private int MeasureWidth(string text) => TextRenderer.MeasureText(
        text,
        primaryTextBox.Font,
        new Size(MaximumTextWidth, int.MaxValue),
        TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Width + 4;

    private static int MeasureHeight(string text, int width, int maximum, TextBox textBox)
    {
        int desired = TextRenderer.MeasureText(
            text,
            textBox.Font,
            new Size(width, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height + 4;
        textBox.ScrollBars = desired > maximum ? ScrollBars.Vertical : ScrollBars.None;
        return Math.Clamp(desired, 30, maximum);
    }

    private void PositionNearCursor()
    {
        Point cursor = Cursor.Position;
        Location = PopupPositionCalculator.Calculate(cursor, Size, Screen.FromPoint(cursor).WorkingArea);
    }

    private void RequestFallback()
    {
        if (operationId <= 0 || currentAction == TranslationPopupAction.FallbackInProgress)
        {
            return;
        }
        closeTimer.Stop();
        FallbackTranslationRequested?.Invoke(this, new FallbackTranslationRequestedEventArgs(operationId));
    }

    private void CopyResult(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        System.Windows.Forms.Clipboard.SetText(text, TextDataFormat.UnicodeText);
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

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        closeTimer.Stop();
        if (operationId > 0)
        {
            Dismissed?.Invoke(this, new TranslationPopupDismissedEventArgs(operationId));
        }
        base.OnFormClosing(e);
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

public sealed class FallbackTranslationRequestedEventArgs(long operationId) : EventArgs
{
    public long OperationId { get; } = operationId;
}

public sealed class TranslationPopupDismissedEventArgs(long operationId) : EventArgs
{
    public long OperationId { get; } = operationId;
}
