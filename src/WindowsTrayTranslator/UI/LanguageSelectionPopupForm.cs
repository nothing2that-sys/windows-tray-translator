using WindowsTrayTranslator.App;

namespace WindowsTrayTranslator.UI;

public sealed class LanguageSelectionPopupForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private bool completed;

    public LanguageSelectionPopupForm(LanguageSelectionRequestedEventArgs request)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Padding = new Padding(12);
        BackColor = Color.FromArgb(35, 38, 45);
        ForeColor = Color.White;
        Text = "WindowsTrayTranslator.LanguageSelection";

        FlowLayoutPanel content = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        content.Controls.Add(new Label
        {
            Text = "번역할 언어를 선택하세요",
            AutoSize = true,
            ForeColor = ForeColor,
            Margin = new Padding(4, 2, 4, 8)
        });

        AddLanguageButton(content, request, "한국어", "Korean");
        AddLanguageButton(content, request, "영어", "English");
        AddLanguageButton(content, request, "일본어", "Japanese");
        AddLanguageButton(content, request, "중국어(간체)", "Chinese (Simplified)");
        AddLanguageButton(content, request, "베트남어", "Vietnamese");

        Button cancel = CreateButton("취소");
        cancel.Click += (_, _) => Complete(request, null);
        content.Controls.Add(cancel);
        Controls.Add(content);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        Shown += (_, _) => PositionNearCursor();
        FormClosed += (_, _) =>
        {
            if (!completed)
            {
                request.Complete(null);
            }
        };
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

    private void AddLanguageButton(
        FlowLayoutPanel panel,
        LanguageSelectionRequestedEventArgs request,
        string caption,
        string language)
    {
        Button button = CreateButton(caption);
        button.Click += (_, _) => Complete(request, language);
        panel.Controls.Add(button);
    }

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        Width = 170,
        Height = 30,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(55, 59, 68),
        ForeColor = Color.White,
        TabStop = false,
        Margin = new Padding(2)
    };

    private void Complete(LanguageSelectionRequestedEventArgs request, string? language)
    {
        completed = true;
        request.Complete(language);
        Close();
    }

    private void PositionNearCursor()
    {
        Point cursor = Cursor.Position;
        Rectangle workingArea = Screen.FromPoint(cursor).WorkingArea;
        Location = PopupPositionCalculator.Calculate(cursor, Size, workingArea);
    }
}
