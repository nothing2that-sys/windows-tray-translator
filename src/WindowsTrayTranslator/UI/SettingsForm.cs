using WindowsTrayTranslator.App;
using WindowsTrayTranslator.Configuration;
using WindowsTrayTranslator.Translation;

namespace WindowsTrayTranslator.UI;

public sealed class SettingsForm : Form
{
    private readonly ApplicationController controller;

    private readonly CheckBox translationEnabledCheckBox = new() { Text = "번역 기능 활성화", AutoSize = true };
    private readonly CheckBox startupCheckBox = new() { Text = "Windows 시작 시 자동 실행", AutoSize = true };
    private readonly CheckBox translatingPopupCheckBox = new() { Text = "번역 중 상태 팝업 표시", AutoSize = true };
    private readonly CheckBox loggingCheckBox = new() { Text = "로그 기록 활성화", AutoSize = true };
    private readonly NumericUpDown popupTimeoutNumeric = CreateNumeric(1000, 60000, 1000);
    private readonly NumericUpDown logRetentionNumeric = CreateNumeric(1, 365);

    private readonly ComboBox readLanguageCombo = CreateEditableCombo("Select", "Korean", "English", "Japanese", "Chinese (Simplified)", "Vietnamese");
    private readonly ComboBox replaceLanguageCombo = CreateEditableCombo("Select", "English", "Korean", "Japanese", "Chinese (Simplified)", "Vietnamese");
    private readonly ComboBox replaceFormatCombo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label replaceFormatPreviewLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly HotkeyTextBox readHotkeyTextBox = new() { Dock = DockStyle.Fill };
    private readonly HotkeyTextBox replaceHotkeyTextBox = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown maxInputNumeric = CreateNumeric(1, 100000);
    private readonly NumericUpDown copyTimeoutNumeric = CreateNumeric(100, 10000, 100);
    private readonly NumericUpDown restoreDelayNumeric = CreateNumeric(50, 5000, 50);
    private readonly NumericUpDown retryCountNumeric = CreateNumeric(1, 20);
    private readonly NumericUpDown retryDelayNumeric = CreateNumeric(10, 1000, 10);
    private readonly CheckBox clipboardFirstCheckBox = new() { Text = "Ctrl+C 방식 우선 사용", AutoSize = true };

    private readonly TextBox apiKeyTextBox = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    private readonly ComboBox modelComboBox = CreateEditableCombo(
        "gemini-3.5-flash-lite",
        "gemini-3.5-flash",
        "gemini-2.5-flash-lite",
        "gemini-2.5-flash");
    private readonly NumericUpDown requestTimeoutNumeric = CreateNumeric(5, 120);
    private readonly NumericUpDown transientRetryNumeric = CreateNumeric(0, 5);
    private readonly Label savedKeyLabel = new() { AutoSize = true };
    private readonly Label statusLabel = new() { AutoSize = true, MaximumSize = new Size(620, 0) };
    private readonly Button testButton = new() { Text = "API 연결 테스트", AutoSize = true };

    public SettingsForm(ApplicationController controller)
    {
        this.controller = controller;
        Text = "Windows Tray Translator 설정";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(700, 650);

        replaceFormatCombo.Items.AddRange(
        [
            new ReplaceFormatOption("번역문만 — Hello.", ReplaceOutputFormat.TranslationOnly),
            new ReplaceFormatOption("번역문 + 원문 — Hello.(안녕하세요.)", ReplaceOutputFormat.TranslationWithOriginal),
            new ReplaceFormatOption("원문 + 번역문 — 안녕하세요.(Hello.)", ReplaceOutputFormat.OriginalWithTranslation),
            new ReplaceFormatOption("줄바꿈 병기 — Hello. 다음 줄에 (안녕하세요.)", ReplaceOutputFormat.TranslationAndOriginalOnNewLine)
        ]);
        replaceFormatCombo.SelectedIndexChanged += (_, _) => UpdateReplaceFormatPreview();

        TabControl tabs = new() { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreateGeneralTab());
        tabs.TabPages.Add(CreateTranslationTab());
        tabs.TabPages.Add(CreateApiTab());

        Button saveButton = new() { Text = "저장", AutoSize = true };
        Button cancelButton = new() { Text = "취소", AutoSize = true, DialogResult = DialogResult.Cancel };
        saveButton.Click += (_, _) => SaveSettings();
        cancelButton.Click += (_, _) => Close();

        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.RightToLeft
        };
        actions.Controls.Add(cancelButton);
        actions.Controls.Add(saveButton);

        Controls.Add(tabs);
        Controls.Add(actions);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
        LoadSettings();
    }

    private TabPage CreateGeneralTab()
    {
        TableLayoutPanel table = CreateTable();
        AddFullRow(table, translationEnabledCheckBox);
        AddFullRow(table, startupCheckBox);
        AddFullRow(table, translatingPopupCheckBox);
        AddRow(table, "팝업 자동 닫기 (ms)", popupTimeoutNumeric);
        AddFullRow(table, loggingCheckBox);
        AddRow(table, "로그 보존 기간 (일)", logRetentionNumeric);
        return WrapTab("일반", table);
    }

    private TabPage CreateTranslationTab()
    {
        TableLayoutPanel table = CreateTable();
        AddRow(table, "읽기 대상 언어", readLanguageCombo);
        AddFullRow(table, new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Text = "Select를 선택하면 단축키를 누를 때 번역할 언어를 고르는 팝업이 표시됩니다."
        });
        AddRow(table, "작성 대상 언어", replaceLanguageCombo);
        AddFullRow(table, new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Text = "작성 Select에서도 실제 입력할 목표 언어를 팝업에서 선택합니다."
        });
        AddRow(table, "작성 결과 형식", replaceFormatCombo);
        AddFullRow(table, replaceFormatPreviewLabel);
        AddRow(table, "읽기 단축키", readHotkeyTextBox);
        AddRow(table, "작성 단축키", replaceHotkeyTextBox);

        Button resetHotkeysButton = new() { Text = "단축키 기본값 복원", AutoSize = true };
        resetHotkeysButton.Click += (_, _) =>
        {
            readHotkeyTextBox.Text = "Alt+R";
            replaceHotkeyTextBox.Text = "Alt+T";
        };
        AddFullRow(table, resetHotkeysButton);
        AddRow(table, "최대 입력 문자 수", maxInputNumeric);
        AddRow(table, "복사 제한 시간 (ms)", copyTimeoutNumeric);
        AddRow(table, "클립보드 복원 지연 (ms)", restoreDelayNumeric);
        AddRow(table, "클립보드 재시도 횟수", retryCountNumeric);
        AddRow(table, "클립보드 재시도 간격 (ms)", retryDelayNumeric);
        AddFullRow(table, clipboardFirstCheckBox);
        AddFullRow(table, new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Text = "단축키 칸을 선택한 뒤 원하는 조합을 직접 누르세요. Delete 키로 지울 수 있습니다."
        });
        return WrapTab("번역", table);
    }

    private TabPage CreateApiTab()
    {
        TableLayoutPanel table = CreateTable();
        AddRow(table, "API 공급자", new Label { Text = "Gemini", AutoSize = true, Anchor = AnchorStyles.Left });
        AddRow(table, "API 키", apiKeyTextBox);
        AddRow(table, "저장 상태", savedKeyLabel);
        AddRow(table, "모델", modelComboBox);
        AddRow(table, "요청 제한 시간 (초)", requestTimeoutNumeric);
        AddRow(table, "일시 오류 재시도", transientRetryNumeric);

        FlowLayoutPanel apiActions = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        Button deleteButton = new() { Text = "저장된 API 키 삭제", AutoSize = true };
        testButton.Click += TestConnectionAsync;
        deleteButton.Click += (_, _) => DeleteApiKey();
        apiActions.Controls.Add(testButton);
        apiActions.Controls.Add(deleteButton);
        AddFullRow(table, apiActions);
        AddFullRow(table, statusLabel);
        return WrapTab("API", table);
    }

    private void LoadSettings()
    {
        AppSettings settings = controller.Settings;
        translationEnabledCheckBox.Checked = settings.General.TranslationEnabled;
        startupCheckBox.Checked = controller.IsStartupEnabled;
        translatingPopupCheckBox.Checked = settings.General.ShowTranslatingPopup;
        popupTimeoutNumeric.Value = settings.General.PopupTimeoutMs;
        loggingCheckBox.Checked = settings.General.EnableLogging;
        logRetentionNumeric.Value = settings.General.LogRetentionDays;

        readLanguageCombo.Text = settings.Translation.ReadTargetLanguage;
        replaceLanguageCombo.Text = settings.Translation.ReplaceTargetLanguage;
        ReplaceOutputFormat savedFormat = ReplaceOutputFormatter.Parse(settings.Translation.ReplaceFormat);
        replaceFormatCombo.SelectedItem = replaceFormatCombo.Items
            .OfType<ReplaceFormatOption>()
            .First(option => option.Value == savedFormat);
        UpdateReplaceFormatPreview();
        readHotkeyTextBox.Text = settings.Translation.ReadHotkey;
        replaceHotkeyTextBox.Text = settings.Translation.ReplaceHotkey;
        maxInputNumeric.Value = settings.Translation.MaxInputCharacters;
        copyTimeoutNumeric.Value = settings.Translation.ClipboardCopyTimeoutMs;
        restoreDelayNumeric.Value = settings.Translation.ClipboardRestoreDelayMs;
        retryCountNumeric.Value = settings.Translation.ClipboardRetryCount;
        retryDelayNumeric.Value = settings.Translation.ClipboardRetryDelayMs;
        clipboardFirstCheckBox.Checked = !settings.Translation.PreferUiAutomation;

        modelComboBox.Text = settings.Api.Model;
        requestTimeoutNumeric.Value = settings.Api.RequestTimeoutSeconds;
        transientRetryNumeric.Value = settings.Api.TransientRetryCount;
        RefreshSavedKeyLabel();
    }

    private void SaveSettings()
    {
        try
        {
            AppSettings candidate = BuildSettings();
            controller.ApplySettings(candidate, apiKeyTextBox.Text);
            apiKeyTextBox.Clear();
            LoadSettings();
            SetStatus("설정을 저장하고 즉시 적용했습니다.", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
        }
    }

    private AppSettings BuildSettings() => new()
    {
        General = new GeneralSettings
        {
            TranslationEnabled = translationEnabledCheckBox.Checked,
            RunAtWindowsStartup = startupCheckBox.Checked,
            ShowTranslatingPopup = translatingPopupCheckBox.Checked,
            PopupTimeoutMs = (int)popupTimeoutNumeric.Value,
            EnableLogging = loggingCheckBox.Checked,
            LogRetentionDays = (int)logRetentionNumeric.Value
        },
        Translation = new TranslationSettings
        {
            ReadTargetLanguage = readLanguageCombo.Text,
            ReplaceTargetLanguage = replaceLanguageCombo.Text,
            ReplaceFormat = replaceFormatCombo.SelectedItem is ReplaceFormatOption option
                ? option.Value.ToString()
                : ReplaceOutputFormat.TranslationWithOriginal.ToString(),
            ReadHotkey = readHotkeyTextBox.Text,
            ReplaceHotkey = replaceHotkeyTextBox.Text,
            MaxInputCharacters = (int)maxInputNumeric.Value,
            ClipboardCopyTimeoutMs = (int)copyTimeoutNumeric.Value,
            ClipboardRestoreDelayMs = (int)restoreDelayNumeric.Value,
            ClipboardRetryCount = (int)retryCountNumeric.Value,
            ClipboardRetryDelayMs = (int)retryDelayNumeric.Value,
            PreferUiAutomation = !clipboardFirstCheckBox.Checked
        },
        Api = new ApiSettings
        {
            Provider = "Gemini",
            Model = modelComboBox.Text,
            RequestTimeoutSeconds = (int)requestTimeoutNumeric.Value,
            TransientRetryCount = (int)transientRetryNumeric.Value
        }
    };

    private async void TestConnectionAsync(object? sender, EventArgs e)
    {
        testButton.Enabled = false;
        SetStatus("연결을 확인하고 있습니다...", isError: false);
        try
        {
            TranslationResult result = await controller.TestConnectionAsync(
                apiKeyTextBox.Text,
                modelComboBox.Text,
                (int)requestTimeoutNumeric.Value,
                CancellationToken.None);
            SetStatus(result.IsSuccess ? "Gemini API 연결에 성공했습니다." : result.ErrorMessage!, !result.IsSuccess);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            testButton.Enabled = true;
        }
    }

    private void DeleteApiKey()
    {
        if (MessageBox.Show("저장된 Gemini API 키를 삭제할까요?", "API 키 삭제",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            controller.DeleteApiKey();
            apiKeyTextBox.Clear();
            RefreshSavedKeyLabel();
            SetStatus("저장된 API 키를 삭제했습니다.", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
        }
    }

    private void RefreshSavedKeyLabel() =>
        savedKeyLabel.Text = controller.HasApiKey ? "저장됨 (DPAPI 보호)" : "저장되지 않음";

    private void SetStatus(string message, bool isError)
    {
        statusLabel.ForeColor = isError ? Color.Firebrick : Color.DarkGreen;
        statusLabel.Text = message;
    }

    private static TableLayoutPanel CreateTable()
    {
        TableLayoutPanel table = new()
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 0
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 8) }, 0, row);
        control.Margin = new Padding(3, 5, 3, 5);
        table.Controls.Add(control, 1, row);
    }

    private static void AddFullRow(TableLayoutPanel table, Control control)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(3, 7, 3, 7);
        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, 2);
    }

    private static TabPage WrapTab(string title, Control content)
    {
        TabPage tab = new(title) { Padding = new Padding(4) };
        tab.Controls.Add(content);
        return tab;
    }

    private static NumericUpDown CreateNumeric(int minimum, int maximum, int increment = 1) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        Increment = increment,
        Dock = DockStyle.Left,
        Width = 140
    };

    private static ComboBox CreateEditableCombo(params string[] items)
    {
        ComboBox combo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
        combo.Items.AddRange(items);
        return combo;
    }

    private void UpdateReplaceFormatPreview()
    {
        if (replaceFormatCombo.SelectedItem is not ReplaceFormatOption option)
        {
            replaceFormatPreviewLabel.Text = string.Empty;
            return;
        }

        replaceFormatPreviewLabel.Text = option.Value switch
        {
            ReplaceOutputFormat.TranslationOnly => "결과: Hello.",
            ReplaceOutputFormat.TranslationWithOriginal => "결과: Hello.(안녕하세요.)",
            ReplaceOutputFormat.OriginalWithTranslation => "결과: 안녕하세요.(Hello.)",
            ReplaceOutputFormat.TranslationAndOriginalOnNewLine => $"결과: Hello.{Environment.NewLine}(안녕하세요.)",
            _ => string.Empty
        };
    }

    private sealed record ReplaceFormatOption(string DisplayName, ReplaceOutputFormat Value)
    {
        public override string ToString() => DisplayName;
    }
}
