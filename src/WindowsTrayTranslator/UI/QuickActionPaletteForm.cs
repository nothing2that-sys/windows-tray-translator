using WindowsTrayTranslator.App;
using WindowsTrayTranslator.Translation;

namespace WindowsTrayTranslator.UI;

/// <summary>
/// Mouse-only, non-activating palette. It never registers a global key, never installs a hook and
/// holds no timer once closed.
/// </summary>
internal sealed class QuickActionPaletteForm : Form
{
    internal const int TimeoutMs = 8000;

    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    private readonly QuickActionSession session;
    private readonly Func<QuickActionSession, BuiltInActionKind, PaletteAdmission> requestAdmission;
    private readonly System.Windows.Forms.Timer closeTimer = new() { Interval = TimeoutMs };
    private readonly Label statusLabel = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI", 8.5f),
        ForeColor = Color.FromArgb(166, 177, 198),
        Margin = new Padding(4, 6, 4, 0),
        Visible = false
    };

    public QuickActionPaletteForm(
        QuickActionSession session,
        Func<QuickActionSession, BuiltInActionKind, PaletteAdmission> requestAdmission)
    {
        this.session = session;
        this.requestAdmission = requestAdmission;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Padding = new Padding(14);
        BackColor = Color.FromArgb(27, 31, 40);
        ForeColor = Color.White;
        Text = "WindowsTrayTranslator.QuickActionPalette";

        FlowLayoutPanel content = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        content.Controls.Add(new Label
        {
            Text = "선택한 문장으로 실행할 작업",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10f),
            ForeColor = ForeColor,
            Margin = new Padding(4, 2, 4, 8)
        });

        AddActionButton(content, BuiltInActionKind.RewriteNatural);
        AddActionButton(content, BuiltInActionKind.Summarize);
        AddActionButton(content, BuiltInActionKind.SummarizeTranslated);

        Button close = CreateButton("닫기");
        close.ForeColor = Color.FromArgb(190, 198, 214);
        close.Margin = new Padding(2, 8, 2, 2);
        close.Click += (_, _) => Close();
        content.Controls.Add(close);
        content.Controls.Add(statusLabel);
        Controls.Add(content);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        closeTimer.Tick += (_, _) => Close();
        foreach (Control control in EnumerateControls(this))
        {
            control.MouseEnter += (_, _) => closeTimer.Stop();
            control.MouseLeave += (_, _) => RestartTimer();
        }
        MouseEnter += (_, _) => closeTimer.Stop();
        MouseLeave += (_, _) => RestartTimer();

        session.Completing += OnSessionCompleting;
        Shown += (_, _) =>
        {
            PositionNearCursor();
            closeTimer.Start();
        };
        FormClosed += (_, _) =>
        {
            closeTimer.Stop();
            session.Completing -= OnSessionCompleting;
            Dismissed?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? Dismissed;

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

    private void OnSessionCompleting(object? sender, EventArgs e)
    {
        closeTimer.Stop();
        if (!IsDisposed)
        {
            Close();
        }
    }

    private void AddActionButton(FlowLayoutPanel panel, BuiltInActionKind kind)
    {
        Button button = CreateButton(PromptComposer.DisplayName(kind));
        button.Click += (_, _) => OnActionClicked(kind);
        panel.Controls.Add(button);
    }

    /// <summary>
    /// Fully synchronous: the admission check, the session claim and the close all happen in this
    /// one callback, so a pending timeout tick can never complete the session in between.
    /// </summary>
    private void OnActionClicked(BuiltInActionKind kind)
    {
        if (session.Completed)
        {
            return;
        }

        switch (requestAdmission(session, kind))
        {
            case PaletteAdmission.Started:
                // The session's Completing event already closed this form.
                break;
            case PaletteAdmission.Busy:
                // Keep the captured text and the original timeout; the user can retry.
                statusLabel.Text = "다른 작업이 끝난 뒤 다시 눌러 주세요.";
                statusLabel.Visible = true;
                break;
            default:
                closeTimer.Stop();
                Close();
                break;
        }
    }

    private void RestartTimer()
    {
        if (IsDisposed || session.Completed)
        {
            return;
        }

        closeTimer.Stop();
        closeTimer.Start();
    }

    private void PositionNearCursor()
    {
        Point cursor = Cursor.Position;
        Rectangle workingArea = Screen.FromPoint(cursor).WorkingArea;
        Location = PopupPositionCalculator.Calculate(cursor, Size, workingArea);
    }

    private static Button CreateButton(string text)
    {
        Button button = new()
        {
            Text = text,
            Width = 220,
            Height = 34,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            Margin = new Padding(2),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(43, 48, 59),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.75f),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            TabStop = false
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(70, 77, 91);
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in EnumerateControls(child))
            {
                yield return nested;
            }
        }
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
