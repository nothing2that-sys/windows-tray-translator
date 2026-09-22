using WindowsTrayTranslator.App;
using System.Runtime.InteropServices;

namespace WindowsTrayTranslator.UI;

public sealed class LanguageSelectionPopupForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private bool completed;
    private readonly LanguageSelectionRequestedEventArgs request;
    private readonly List<(string Caption, string Language)> languages;
    private readonly List<Button> languageButtons = [];
    private int selectedIndex;

    public LanguageSelectionPopupForm(LanguageSelectionRequestedEventArgs request)
    {
        this.request = request;
        languages = BuildLanguageList(request.RecentLanguage);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Padding = new Padding(14);
        BackColor = Color.FromArgb(27, 31, 40);
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
            Font = new Font("Segoe UI Semibold", 11f),
            ForeColor = ForeColor,
            Margin = new Padding(4, 2, 4, 2)
        });
        content.Controls.Add(new Label
        {
            Text = "숫자키 또는 ↑ ↓ + Enter",
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(166, 177, 198),
            Margin = new Padding(4, 0, 4, 10)
        });

        for (int index = 0; index < languages.Count; index++)
        {
            (string caption, string language) = languages[index];
            AddLanguageButton(content, request, $"{index + 1}. {caption}", language);
        }

        Button cancel = CreateButton("취소");
        cancel.ForeColor = Color.FromArgb(190, 198, 214);
        cancel.Margin = new Padding(2, 8, 2, 2);
        cancel.Click += (_, _) => Complete(request, null);
        content.Controls.Add(cancel);
        Controls.Add(content);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        Shown += (_, _) =>
        {
            PositionNearCursor();
            RegisterSelectionKeys();
            UpdateSelectionHighlight();
        };
        FormClosed += (_, _) =>
        {
            UnregisterSelectionKeys();
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using SolidBrush accent = new(AppTheme.Primary);
        e.Graphics.FillRectangle(accent, 0, 0, 4, ClientSize.Height);
        using Pen border = new(Color.FromArgb(76, 84, 100));
        e.Graphics.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
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
        languageButtons.Add(button);
    }

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        Width = 220,
        Height = 36,
        FlatStyle = FlatStyle.Flat,
        FlatAppearance = { BorderColor = Color.FromArgb(70, 77, 91) },
        BackColor = Color.FromArgb(43, 48, 59),
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 9.5f),
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(10, 0, 8, 0),
        Cursor = Cursors.Hand,
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

    protected override void WndProc(ref Message message)
    {
        const int WmHotkey = 0x0312;
        if (message.Msg == WmHotkey)
        {
            int id = message.WParam.ToInt32();
            if (id is >= 100 and < 105)
            {
                int index = id - 100;
                if (index < languages.Count) Complete(request, languages[index].Language);
                return;
            }

            switch (id)
            {
                case 200:
                    selectedIndex = (selectedIndex - 1 + languages.Count) % languages.Count;
                    UpdateSelectionHighlight();
                    return;
                case 201:
                    selectedIndex = (selectedIndex + 1) % languages.Count;
                    UpdateSelectionHighlight();
                    return;
                case 202:
                    Complete(request, languages[selectedIndex].Language);
                    return;
                case 203:
                    Complete(request, null);
                    return;
            }
        }

        base.WndProc(ref message);
    }

    private static List<(string Caption, string Language)> BuildLanguageList(string recent)
    {
        (string Caption, string Language)[] all =
        [
            ("한국어", "Korean"),
            ("영어", "English"),
            ("일본어", "Japanese"),
            ("중국어(간체)", "Chinese (Simplified)"),
            ("베트남어", "Vietnamese")
        ];
        return all
            .OrderByDescending(item => string.Equals(item.Language, recent, StringComparison.OrdinalIgnoreCase))
            .Select(item => string.Equals(item.Language, recent, StringComparison.OrdinalIgnoreCase)
                ? ($"{item.Caption} (최근)", item.Language)
                : item)
            .ToList();
    }

    private void RegisterSelectionKeys()
    {
        for (int index = 0; index < languages.Count; index++)
        {
            RegisterHotKey(Handle, 100 + index, 0, (uint)(Keys.D1 + index));
        }
        RegisterHotKey(Handle, 200, 0, (uint)Keys.Up);
        RegisterHotKey(Handle, 201, 0, (uint)Keys.Down);
        RegisterHotKey(Handle, 202, 0, (uint)Keys.Enter);
        RegisterHotKey(Handle, 203, 0, (uint)Keys.Escape);
    }

    private void UnregisterSelectionKeys()
    {
        for (int id = 100; id < 105; id++) UnregisterHotKey(Handle, id);
        for (int id = 200; id <= 203; id++) UnregisterHotKey(Handle, id);
    }

    private void UpdateSelectionHighlight()
    {
        for (int index = 0; index < languageButtons.Count; index++)
        {
            languageButtons[index].BackColor = index == selectedIndex
                ? AppTheme.Primary
                : Color.FromArgb(43, 48, 59);
            languageButtons[index].FlatAppearance.BorderColor = index == selectedIndex
                ? Color.FromArgb(104, 163, 255)
                : Color.FromArgb(70, 77, 91);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
