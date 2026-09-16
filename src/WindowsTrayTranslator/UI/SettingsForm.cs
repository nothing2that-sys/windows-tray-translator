using WindowsTrayTranslator.App;
using WindowsTrayTranslator.Configuration;
using WindowsTrayTranslator.Translation;
using WindowsTrayTranslator.Translation.Gemini;

namespace WindowsTrayTranslator.UI;

public sealed class SettingsForm : Form
{
    private readonly ApplicationController controller;

    private readonly CheckBox translationEnabledCheckBox = new() { Text = "번역 기능 활성화", AutoSize = true };
    private readonly CheckBox startupCheckBox = new() { Text = "Windows 시작 시 자동 실행", AutoSize = true };
    private readonly CheckBox translatingPopupCheckBox = new() { Text = "번역 중 상태 팝업 표시", AutoSize = true };
    private readonly CheckBox loggingCheckBox = new() { Text = "진단 로그 기록 활성화 (번역 원문/결과 제외)", AutoSize = true };
    private readonly CheckBox historyCheckBox = new() { Text = "번역 히스토리 저장 (원문과 번역문 포함, 기본 꺼짐)", AutoSize = true };
    private readonly NumericUpDown readPopupTimeoutNumeric = CreateNumeric(1000, 60000, 1000);
    private readonly NumericUpDown replacePopupTimeoutNumeric = CreateNumeric(1000, 60000, 1000);
    private readonly NumericUpDown logRetentionNumeric = CreateNumeric(1, 365);
    private readonly NumericUpDown historyRetentionNumeric = CreateNumeric(1, 365);

    private readonly ComboBox readLanguageCombo = CreateEditableCombo("Select", "Auto", "Korean", "English", "Japanese", "Chinese (Simplified)", "Vietnamese");
    private readonly ComboBox replaceLanguageCombo = CreateEditableCombo("Select", "English", "Korean", "Japanese", "Chinese (Simplified)", "Vietnamese");
    private readonly ComboBox replaceFormatCombo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label replaceFormatPreviewLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly HotkeyTextBox readHotkeyTextBox = new() { Dock = DockStyle.Fill };
    private readonly HotkeyTextBox replaceHotkeyTextBox = new() { Dock = DockStyle.Fill };
    private readonly HotkeyTextBox actionPaletteHotkeyTextBox = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown maxInputNumeric = CreateNumeric(1, 100000);
    private readonly NumericUpDown copyTimeoutNumeric = CreateNumeric(100, 10000, 100);
    private readonly NumericUpDown restoreDelayNumeric = CreateNumeric(50, 5000, 50);
    private readonly NumericUpDown retryCountNumeric = CreateNumeric(1, 20);
    private readonly NumericUpDown retryDelayNumeric = CreateNumeric(10, 1000, 10);
    private readonly CheckBox clipboardFirstCheckBox = new() { Text = "Ctrl+C 방식 우선 사용", AutoSize = true };
    private readonly ComboBox styleCombo = CreateDropDownList("Natural", "Literal", "Business");
    private readonly ComboBox politenessCombo = CreateDropDownList("Auto", "Formal", "Casual");
    private readonly TextBox customInstructionsTextBox = new() { Dock = DockStyle.Fill, Multiline = true, Height = 70, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox glossaryTextBox = new() { Dock = DockStyle.Fill, Multiline = true, Height = 70, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox excludedTermsTextBox = new() { Dock = DockStyle.Fill, Multiline = true, Height = 55, ScrollBars = ScrollBars.Vertical };

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
    private readonly Button refreshModelsButton = new() { Text = "모델 목록 새로고침", AutoSize = true };
    private readonly CheckBox fallbackModelCheckBox = new() { Text = "기본 모델 실패 시 보조 모델 사용", AutoSize = true };
    private readonly ComboBox fallbackModelComboBox = CreateEditableCombo("gemini-3.1-flash-lite", "gemini-3.5-flash-lite");
    private readonly CheckBox fallbackAfterCompletionCheckBox = new()
    {
        Text = "번역 완료 후 다른 모델로 다시 번역할 수 있게 표시",
        AutoSize = true
    };
    private readonly CheckBox actionModelCheckBox = new()
    {
        Text = "Quick Action(요약·다듬기·번역 후 요약)에 다른 모델 사용",
        AutoSize = true
    };
    private readonly NumericUpDown actionTimeoutNumeric = CreateNumeric(5, 300);
    private readonly ComboBox actionModelComboBox = CreateEditableCombo(
        "gemini-3.5-flash",
        "gemini-3.5-flash-lite",
        "gemini-2.5-flash");

    public SettingsForm(ApplicationController controller)
    {
        this.controller = controller;
        Text = "Windows Tray Translator 설정";
        Icon = AppIconProvider.ApplicationIcon;
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(700, 720);
        BackColor = AppTheme.Canvas;

        replaceFormatCombo.Items.AddRange(
        [
            new ReplaceFormatOption("번역문만 — Hello.", ReplaceOutputFormat.TranslationOnly),
            new ReplaceFormatOption("번역문 + 원문 — Hello.(안녕하세요.)", ReplaceOutputFormat.TranslationWithOriginal),
            new ReplaceFormatOption("원문 + 번역문 — 안녕하세요.(Hello.)", ReplaceOutputFormat.OriginalWithTranslation),
            new ReplaceFormatOption("줄바꿈 병기 — Hello. 다음 줄에 (안녕하세요.)", ReplaceOutputFormat.TranslationAndOriginalOnNewLine)
        ]);
        replaceFormatCombo.SelectedIndexChanged += (_, _) => UpdateReplaceFormatPreview();
        actionModelCheckBox.CheckedChanged += (_, _) =>
            actionModelComboBox.Enabled = actionModelCheckBox.Checked;
        fallbackModelCheckBox.CheckedChanged += (_, _) =>
        {
            fallbackModelComboBox.Enabled = fallbackModelCheckBox.Checked;
            fallbackAfterCompletionCheckBox.Enabled = fallbackModelCheckBox.Checked;
            if (!fallbackModelCheckBox.Checked)
            {
                fallbackAfterCompletionCheckBox.Checked = false;
            }
        };

        TabControl tabs = new()
        {
            Dock = DockStyle.Fill,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(120, 36),
            SizeMode = TabSizeMode.Fixed
        };
        tabs.DrawItem += DrawTab;
        tabs.TabPages.Add(CreateGeneralTab());
        tabs.TabPages.Add(CreateTranslationTab());
        tabs.TabPages.Add(CreateApiTab());

        Button saveButton = CreatePrimaryButton("저장");
        Button cancelButton = new() { Text = "취소", AutoSize = true, DialogResult = DialogResult.Cancel };
        AppTheme.StyleSecondaryButton(cancelButton);
        saveButton.Click += (_, _) => SaveSettings();
        cancelButton.Click += (_, _) => Close();

        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            Padding = new Padding(12, 10, 12, 8),
            FlowDirection = FlowDirection.RightToLeft
        };
        actions.Controls.Add(cancelButton);
        actions.Controls.Add(saveButton);

        Panel header = CreateHeader();
        Controls.Add(tabs);
        Controls.Add(actions);
        Controls.Add(header);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
        LoadSettings();
    }

    private static Panel CreateHeader()
    {
        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 82,
            BackColor = AppTheme.PrimaryDark,
            Padding = new Padding(18, 12, 18, 10)
        };

        PictureBox icon = new()
        {
            Image = AppIconProvider.ApplicationIcon.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(18, 13),
            Size = new Size(54, 54)
        };
        Label title = new()
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 14f),
            ForeColor = Color.White,
            Location = new Point(86, 15),
            Text = "Windows Tray Translator"
        };
        Label subtitle = new()
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(218, 231, 255),
            Location = new Point(88, 47),
            Text = "Alt+R 읽기 번역  ·  Alt+T 작성 번역"
        };
        Label status = new()
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoSize = true,
            BackColor = Color.FromArgb(40, 94, 183),
            ForeColor = Color.FromArgb(211, 255, 228),
            Font = new Font("Segoe UI Semibold", 8.5f),
            Padding = new Padding(9, 4, 9, 4),
            Location = new Point(598, 27),
            Text = "● 실행 중"
        };

        header.Controls.Add(icon);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(status);
        return header;
    }

    private static Button CreatePrimaryButton(string text)
    {
        Button button = new() { Text = text };
        AppTheme.StylePrimaryButton(button);
        return button;
    }

    private static void DrawTab(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs)
        {
            return;
        }

        bool selected = e.Index == tabs.SelectedIndex;
        Rectangle bounds = e.Bounds;
        using SolidBrush background = new(selected ? AppTheme.Surface : AppTheme.Canvas);
        e.Graphics.FillRectangle(background, bounds);
        if (selected)
        {
            using SolidBrush accent = new(AppTheme.Primary);
            e.Graphics.FillRectangle(accent, bounds.Left + 10, bounds.Bottom - 3, bounds.Width - 20, 3);
        }

        using Font selectedFont = new("Segoe UI Semibold", 9.5f);
        TextRenderer.DrawText(
            e.Graphics,
            tabs.TabPages[e.Index].Text,
            selected ? selectedFont : tabs.Font,
            bounds,
            selected ? AppTheme.PrimaryDark : AppTheme.MutedText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private TabPage CreateGeneralTab()
    {
        TableLayoutPanel table = CreateTable();
        AddSectionRow(table, "프로그램 동작");
        AddFullRow(table, translationEnabledCheckBox);
        AddFullRow(table, startupCheckBox);
        AddSectionRow(table, "알림 및 기록");
        AddFullRow(table, translatingPopupCheckBox);
        AddRow(table, "읽기 팝업 자동 닫기 (ms)", readPopupTimeoutNumeric);
        AddRow(table, "작성 팝업 자동 닫기 (ms)", replacePopupTimeoutNumeric);
        AddFullRow(table, loggingCheckBox);
        AddRow(table, "진단 로그 보존 기간 (일)", logRetentionNumeric);
        AddFullRow(table, historyCheckBox);
        AddRow(table, "히스토리 보존 기간 (일)", historyRetentionNumeric);
        return WrapTab("일반", table);
    }

    private TabPage CreateTranslationTab()
    {
        TableLayoutPanel table = CreateTable();
        AddSectionRow(table, "언어 및 출력");
        AddRow(table, "읽기 대상 언어", readLanguageCombo);
        AddFullRow(table, new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Text = "Select: 단축키를 누를 때 언어 선택 / Auto: 주 언어가 한국어면 영어로, 그 외에는 한국어로 번역"
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
        AddSectionRow(table, "단축키");
        AddRow(table, "읽기 단축키", readHotkeyTextBox);
        AddRow(table, "작성 단축키", replaceHotkeyTextBox);
        AddRow(table, "Quick Action 단축키 (비우면 사용 안 함)", actionPaletteHotkeyTextBox);

        Button resetHotkeysButton = new() { Text = "단축키 기본값 복원", AutoSize = true };
        AppTheme.StyleSecondaryButton(resetHotkeysButton);
        resetHotkeysButton.Click += (_, _) =>
        {
            readHotkeyTextBox.Text = "Alt+R";
            replaceHotkeyTextBox.Text = "Alt+T";
            actionPaletteHotkeyTextBox.Text = "Ctrl+Alt+A";
        };
        AddFullRow(table, resetHotkeysButton);
        AddSectionRow(table, "선택 영역 가져오기");
        AddRow(table, "최대 입력 문자 수", maxInputNumeric);
        AddRow(table, "복사 제한 시간 (ms)", copyTimeoutNumeric);
        AddRow(table, "클립보드 복원 지연 (ms)", restoreDelayNumeric);
        AddRow(table, "클립보드 재시도 횟수", retryCountNumeric);
        AddRow(table, "클립보드 재시도 간격 (ms)", retryDelayNumeric);
        AddFullRow(table, clipboardFirstCheckBox);
        AddSectionRow(table, "번역 품질");
        AddRow(table, "번역 방식", styleCombo);
        AddFullRow(table, new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Text = "Natural: 자연스럽게 / Literal: 직역 중심 / Business: 비즈니스 문체" });
        AddRow(table, "말투", politenessCombo);
        AddFullRow(table, new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Text = "Auto: 원문 유지 / Formal: 존댓말·격식 / Casual: 친근한 말투" });
        AddRow(table, "추가 자연어 지침", customInstructionsTextBox);
        AddFullRow(table, new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Text = "예: 개발자끼리 대화하듯 간결하게 번역하고, 제품명은 원문으로 유지해 주세요."
        });
        AddRow(table, "사용자 용어집", glossaryTextBox);
        AddFullRow(table, new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Text = "한 줄에 하나씩 입력하세요. 예: fixture=지그" });
        AddRow(table, "번역 제외 단어", excludedTermsTextBox);
        AddFullRow(table, new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Text = "쉼표 또는 줄바꿈으로 구분합니다. 입력한 용어는 그대로 유지합니다." });
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
        AddSectionRow(table, "Gemini 연결");
        AddRow(table, "API 공급자", new Label { Text = "Gemini", AutoSize = true, Anchor = AnchorStyles.Left });
        AddRow(table, "API 키", apiKeyTextBox);
        AddRow(table, "저장 상태", savedKeyLabel);
        AddRow(table, "모델", modelComboBox);
        AppTheme.StyleSecondaryButton(refreshModelsButton);
        AddFullRow(table, refreshModelsButton);
        AddSectionRow(table, "Quick Action 모델");
        AddFullRow(table, actionModelCheckBox);
        AddRow(table, "Quick Action 모델", actionModelComboBox);
        AddRow(table, "Quick Action 제한 시간 (초)", actionTimeoutNumeric);
        AddFullRow(table, new Label
        {
            Text = "요약은 번역보다 더 큰 모델이 필요합니다. 요약이 핵심을 잃고 단어만 남으면 여기서 상위 모델을 지정하세요.\r\n"
                + "상위 모델은 한 번의 요약에 10초 이상 걸릴 수 있어 제한 시간을 번역과 따로 둡니다.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(600, 0)
        });
        AddSectionRow(table, "안정성 옵션");
        AddFullRow(table, fallbackModelCheckBox);
        AddRow(table, "보조 모델", fallbackModelComboBox);
        AddFullRow(table, fallbackAfterCompletionCheckBox);
        AddRow(table, "요청 제한 시간 (초)", requestTimeoutNumeric);
        AddRow(table, "일시 오류 재시도", transientRetryNumeric);

        FlowLayoutPanel apiActions = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        Button deleteButton = new() { Text = "저장된 API 키 삭제", AutoSize = true };
        AppTheme.StylePrimaryButton(testButton);
        AppTheme.StyleDangerButton(deleteButton);
        testButton.Click += TestConnectionAsync;
        refreshModelsButton.Click += RefreshModelsAsync;
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
        readPopupTimeoutNumeric.Value = settings.General.ReadPopupTimeoutMs;
        replacePopupTimeoutNumeric.Value = settings.General.ReplacePopupTimeoutMs;
        loggingCheckBox.Checked = settings.General.EnableLogging;
        logRetentionNumeric.Value = settings.General.LogRetentionDays;
        historyCheckBox.Checked = settings.General.EnableTranslationHistory;
        historyRetentionNumeric.Value = settings.General.HistoryRetentionDays;

        readLanguageCombo.Text = settings.Translation.ReadTargetLanguage;
        replaceLanguageCombo.Text = settings.Translation.ReplaceTargetLanguage;
        ReplaceOutputFormat savedFormat = ReplaceOutputFormatter.Parse(settings.Translation.ReplaceFormat);
        replaceFormatCombo.SelectedItem = replaceFormatCombo.Items
            .OfType<ReplaceFormatOption>()
            .First(option => option.Value == savedFormat);
        UpdateReplaceFormatPreview();
        readHotkeyTextBox.Text = settings.Translation.ReadHotkey;
        replaceHotkeyTextBox.Text = settings.Translation.ReplaceHotkey;
        actionPaletteHotkeyTextBox.Text = settings.Translation.ActionPaletteHotkey;
        maxInputNumeric.Value = settings.Translation.MaxInputCharacters;
        copyTimeoutNumeric.Value = settings.Translation.ClipboardCopyTimeoutMs;
        restoreDelayNumeric.Value = settings.Translation.ClipboardRestoreDelayMs;
        retryCountNumeric.Value = settings.Translation.ClipboardRetryCount;
        retryDelayNumeric.Value = settings.Translation.ClipboardRetryDelayMs;
        clipboardFirstCheckBox.Checked = !settings.Translation.PreferUiAutomation;
        styleCombo.SelectedItem = settings.Translation.TranslationStyle;
        politenessCombo.SelectedItem = settings.Translation.Politeness;
        customInstructionsTextBox.Text = settings.Translation.CustomInstructions;
        glossaryTextBox.Text = settings.Translation.Glossary;
        excludedTermsTextBox.Text = settings.Translation.ExcludedTerms;

        modelComboBox.Text = settings.Api.Model;
        requestTimeoutNumeric.Value = settings.Api.RequestTimeoutSeconds;
        transientRetryNumeric.Value = settings.Api.TransientRetryCount;
        fallbackModelCheckBox.Checked = settings.Api.EnableFallbackModel;
        fallbackModelComboBox.Text = settings.Api.FallbackModel;
        fallbackModelComboBox.Enabled = fallbackModelCheckBox.Checked;
        fallbackAfterCompletionCheckBox.Checked = settings.Api.OfferFallbackAfterCompletion;
        fallbackAfterCompletionCheckBox.Enabled = fallbackModelCheckBox.Checked;
        actionModelCheckBox.Checked = settings.Api.EnableActionModel;
        actionModelComboBox.Text = settings.Api.ActionModel;
        actionModelComboBox.Enabled = actionModelCheckBox.Checked;
        actionTimeoutNumeric.Value = settings.Api.ActionRequestTimeoutSeconds;
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
            ReadPopupTimeoutMs = (int)readPopupTimeoutNumeric.Value,
            ReplacePopupTimeoutMs = (int)replacePopupTimeoutNumeric.Value,
            EnableLogging = loggingCheckBox.Checked,
            LogRetentionDays = (int)logRetentionNumeric.Value,
            EnableTranslationHistory = historyCheckBox.Checked,
            HistoryRetentionDays = (int)historyRetentionNumeric.Value
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
            ActionPaletteHotkey = actionPaletteHotkeyTextBox.Text,
            MaxInputCharacters = (int)maxInputNumeric.Value,
            ClipboardCopyTimeoutMs = (int)copyTimeoutNumeric.Value,
            ClipboardRestoreDelayMs = (int)restoreDelayNumeric.Value,
            ClipboardRetryCount = (int)retryCountNumeric.Value,
            ClipboardRetryDelayMs = (int)retryDelayNumeric.Value,
            PreferUiAutomation = !clipboardFirstCheckBox.Checked,
            ReadRecentTargetLanguage = controller.Settings.Translation.ReadRecentTargetLanguage,
            ReplaceRecentTargetLanguage = controller.Settings.Translation.ReplaceRecentTargetLanguage,
            TranslationStyle = styleCombo.SelectedItem?.ToString() ?? "Natural",
            Politeness = politenessCombo.SelectedItem?.ToString() ?? "Auto",
            CustomInstructions = customInstructionsTextBox.Text,
            Glossary = glossaryTextBox.Text,
            ExcludedTerms = excludedTermsTextBox.Text
        },
        Api = new ApiSettings
        {
            Provider = "Gemini",
            Model = modelComboBox.Text,
            RequestTimeoutSeconds = (int)requestTimeoutNumeric.Value,
            TransientRetryCount = (int)transientRetryNumeric.Value,
            EnableFallbackModel = fallbackModelCheckBox.Checked,
            FallbackModel = fallbackModelComboBox.Text,
            OfferFallbackAfterCompletion = fallbackAfterCompletionCheckBox.Checked,
            EnableActionModel = actionModelCheckBox.Checked,
            ActionModel = actionModelComboBox.Text,
            ActionRequestTimeoutSeconds = (int)actionTimeoutNumeric.Value
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

    private async void RefreshModelsAsync(object? sender, EventArgs e)
    {
        refreshModelsButton.Enabled = false;
        SetStatus("사용 가능한 Gemini 모델을 확인하고 있습니다...", isError: false);
        try
        {
            GeminiModelListResult result = await controller.ListModelsAsync(
                apiKeyTextBox.Text,
                (int)requestTimeoutNumeric.Value,
                CancellationToken.None);
            if (!result.IsSuccess)
            {
                SetStatus(result.ErrorMessage!, isError: true);
                return;
            }

            string current = modelComboBox.Text;
            string fallback = fallbackModelComboBox.Text;
            string action = actionModelComboBox.Text;
            modelComboBox.Items.Clear();
            fallbackModelComboBox.Items.Clear();
            actionModelComboBox.Items.Clear();
            modelComboBox.Items.AddRange(result.Models.Cast<object>().ToArray());
            fallbackModelComboBox.Items.AddRange(result.Models.Cast<object>().ToArray());
            actionModelComboBox.Items.AddRange(result.Models.Cast<object>().ToArray());
            actionModelComboBox.Text = action;
            modelComboBox.Text = current;
            fallbackModelComboBox.Text = fallback;
            SetStatus($"번역 가능한 모델 {result.Models.Count}개를 불러왔습니다.", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            refreshModelsButton.Enabled = true;
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
            Padding = new Padding(20, 12, 20, 20),
            BackColor = AppTheme.Surface,
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

    private static void AddSectionRow(TableLayoutPanel table, string title) =>
        AddFullRow(table, AppTheme.CreateSectionLabel(title));

    private static TabPage WrapTab(string title, Control content)
    {
        TabPage tab = new(title) { Padding = new Padding(4), BackColor = AppTheme.Surface };
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

    private static ComboBox CreateDropDownList(params string[] items)
    {
        ComboBox combo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(items);
        combo.SelectedIndex = 0;
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
