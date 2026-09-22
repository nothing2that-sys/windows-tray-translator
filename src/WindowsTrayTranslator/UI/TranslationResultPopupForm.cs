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
        Font = new Font("Segoe UI Semibold", 8.75f),
        BackColor = Color.Transparent
    };
    private readonly Label primaryModelLabel = CreateModelLabel();
    private readonly Label secondaryModelLabel = CreateModelLabel();
    private readonly TextBox primaryTextBox = CreateTextBox(Surface);
    private readonly TextBox secondaryTextBox = CreateTextBox(SecondarySurface);
    private readonly Button actionButton = new() { AutoSize = true, TabStop = false };
    private readonly Button primaryCopyButton = new() { AutoSize = true, TabStop = false };
    private readonly Button secondaryCopyButton = new() { Text = "보조 번역 복사", AutoSize = true, TabStop = false };
    private readonly Button closeButton = new() { Text = "닫기", AutoSize = true, TabStop = false };
    private readonly System.Windows.Forms.Timer closeTimer = new();
    private string? primaryCopyText;
    private string? secondaryCopyText;
    private int closeTimeoutMs;
    private long operationId;
    private TranslationPopupAction currentAction;
    private bool showSecondary;
    private bool showAction;
    private bool showPrimaryCopy;
    private bool showSecondaryCopy;
    private Color accentColor = Color.FromArgb(54, 155, 255);
    private Rectangle secondaryCardBounds;

    public TranslationResultPopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Padding = new Padding(14, 10, 10, 10);
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
        showSecondary = hasSecondary;
        primaryModelLabel.Text = "기본 번역";
        primaryModelLabel.Visible = hasSecondary;
        secondaryModelLabel.Text = "보조 모델 번역";
        secondaryModelLabel.Visible = hasSecondary;
        secondaryTextBox.Visible = hasSecondary;
        ConfigureActionButton(content.Action);
        showPrimaryCopy = !string.IsNullOrEmpty(content.CopyText);
        primaryCopyButton.Visible = showPrimaryCopy;
        primaryCopyButton.Text = hasSecondary ? "기본 복사" : "복사";
        showSecondaryCopy = secondaryCopyText is not null;
        secondaryCopyButton.Visible = showSecondaryCopy;

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
        Font = new Font("Segoe UI Semibold", 8f),
        ForeColor = MutedText,
        BackColor = Color.Transparent
    };

    private static TextBox CreateTextBox(Color background) => new()
    {
        Font = new Font("Segoe UI", 10.75f),
        ForeColor = Color.White,
        BackColor = background,
        Multiline = true,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        WordWrap = true,
        TabStop = false,
        Cursor = Cursors.Arrow
    };

    private static bool IsCompletedSecondary(TranslationPopupRequestedEventArgs content) =>
        content.SecondaryMessage is not null &&
        content.Action is not (TranslationPopupAction.FallbackInProgress or TranslationPopupAction.RetryFallback);

    private void ConfigureActionButton(TranslationPopupAction action)
    {
        showAction = action != TranslationPopupAction.None;
        actionButton.Visible = showAction;
        actionButton.Enabled = action != TranslationPopupAction.FallbackInProgress;
        actionButton.Text = action switch
        {
            TranslationPopupAction.SwitchToFallback => "보조 모델로 재진행",
            TranslationPopupAction.ShowFallback => "보조 모델로 번역",
            TranslationPopupAction.FallbackInProgress => "보조 모델 응답 대기 중…",
            TranslationPopupAction.RetryFallback => "보조 모델로 재시도",
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
        button.Font = new Font("Segoe UI", 8.25f);
        button.Padding = new Padding(6, 0, 6, 0);
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
        int buttonsWidth = buttons.Sum(button => button.PreferredSize.Width) + Math.Max(0, buttons.Count - 1) * 6;
        int headerWidth = titleLabel.PreferredWidth + 12 + buttonsWidth;
        int contentWidth = Math.Clamp(
            Math.Max(Math.Max(MeasureWidth(primaryTextBox.Text), MeasureWidth(secondaryTextBox.Text)), headerWidth),
            280,
            MaximumTextWidth);

        int y = Padding.Top;
        int headerHeight = Math.Max(
            titleLabel.PreferredHeight,
            buttons.Count == 0 ? 0 : buttons.Max(button => button.PreferredSize.Height));
        titleLabel.Location = new Point(Padding.Left, y + Math.Max(0, (headerHeight - titleLabel.PreferredHeight) / 2));
        int buttonX = Padding.Left + contentWidth;
        for (int index = buttons.Count - 1; index >= 0; index--)
        {
            Button button = buttons[index];
            buttonX -= button.PreferredSize.Width;
            button.Location = new Point(
                buttonX,
                y + Math.Max(0, (headerHeight - button.PreferredSize.Height) / 2));
            buttonX -= 6;
        }
        y += headerHeight + 8;
        if (showSecondary)
        {
            primaryModelLabel.Location = new Point(Padding.Left, y);
            y = primaryModelLabel.Bottom + 3;
        }

        int maximumContentHeight = Math.Max(140, workingArea.Height - 150);
        int sectionMaximum = showSecondary
            ? Math.Max(68, (maximumContentHeight - 34) / 2)
            : maximumContentHeight;
        int primaryHeight = MeasureHeight(primaryTextBox.Text, contentWidth, sectionMaximum, primaryTextBox);
        primaryTextBox.SetBounds(Padding.Left, y, contentWidth, primaryHeight);
        y = primaryTextBox.Bottom;

        secondaryCardBounds = Rectangle.Empty;
        if (showSecondary)
        {
            y += 8;
            int cardTop = y;
            secondaryModelLabel.Location = new Point(Padding.Left + 8, y + 7);
            y = secondaryModelLabel.Bottom + 3;
            int secondaryHeight = MeasureHeight(secondaryTextBox.Text, contentWidth - 20, sectionMaximum, secondaryTextBox);
            secondaryTextBox.SetBounds(Padding.Left + 8, y, contentWidth - 16, secondaryHeight);
            y = secondaryTextBox.Bottom + 8;
            secondaryCardBounds = new Rectangle(Padding.Left, cardTop, contentWidth, y - cardTop);
        }

        ClientSize = new Size(contentWidth + Padding.Horizontal, y + Padding.Bottom);
    }

    private List<Button> VisibleButtons() =>
        new[]
        {
            (Button: actionButton, Include: showAction),
            (Button: primaryCopyButton, Include: showPrimaryCopy),
            (Button: secondaryCopyButton, Include: showSecondaryCopy),
            (Button: closeButton, Include: true)
        }
        .Where(item => item.Include)
        .Select(item => item.Button)
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
        return Math.Clamp(desired, 26, maximum);
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
