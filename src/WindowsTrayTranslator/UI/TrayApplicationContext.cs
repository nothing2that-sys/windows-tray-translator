using System.Diagnostics;
using System.Drawing;
using WindowsTrayTranslator.App;
using WindowsTrayTranslator.Logging;

namespace WindowsTrayTranslator.UI;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ApplicationController controller;
    private readonly ILogger logger;
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem enabledItem;
    private SettingsForm? settingsForm;
    private TranslationHistoryForm? historyForm;
    private TranslationResultPopupForm? translationPopup;
    private LanguageSelectionPopupForm? languageSelectionPopup;
    private QuickActionPaletteForm? actionPalette;

    public TrayApplicationContext(ApplicationController controller, ILogger logger)
    {
        this.controller = controller;
        this.logger = logger;

        enabledItem = new ToolStripMenuItem("번역 기능 활성화")
        {
            Checked = controller.Settings.General.TranslationEnabled,
            CheckOnClick = true
        };
        enabledItem.CheckedChanged += (_, _) => controller.SetTranslationEnabled(enabledItem.Checked);

        ContextMenuStrip menu = new();
        menu.Font = new Font("Segoe UI", 9.5f);
        menu.Padding = new Padding(4);
        menu.Renderer = new ToolStripProfessionalRenderer(new AppMenuColorTable());
        ToolStripMenuItem appTitle = new("Windows Tray Translator")
        {
            Enabled = false,
            Font = new Font("Segoe UI Semibold", 9.5f),
            Image = AppIconProvider.ApplicationIcon.ToBitmap()
        };
        menu.Items.Add(appTitle);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("설정 열기", null, (_, _) => OpenSettings());
        menu.Items.Add("번역 히스토리", null, (_, _) => OpenHistory());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(enabledItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("로그 폴더 열기", null, (_, _) => OpenLogDirectory());
        menu.Items.Add("프로그램 정보", null, (_, _) => ShowAbout());
        menu.Items.Add("종료", null, (_, _) => ExitApplication());

        notifyIcon = new NotifyIcon
        {
            Icon = AppIconProvider.ApplicationIcon,
            Text = "Windows Tray Translator",
            ContextMenuStrip = menu,
            Visible = true
        };
        notifyIcon.DoubleClick += (_, _) => OpenSettings();
        controller.NotificationRequested += OnNotificationRequested;
        controller.TranslationPopupRequested += OnTranslationPopupRequested;
        controller.SettingsApplied += OnSettingsApplied;
        controller.LanguageSelectionRequested += OnLanguageSelectionRequested;
        controller.TranslationHistoryChanged += OnTranslationHistoryChanged;
        controller.ActionPaletteRequested += OnActionPaletteRequested;

        if (!string.IsNullOrWhiteSpace(controller.StartupWarning))
        {
            System.Windows.Forms.Timer warningTimer = new() { Interval = 750 };
            warningTimer.Tick += (_, _) =>
            {
                warningTimer.Stop();
                warningTimer.Dispose();
                ShowBalloon(controller.StartupWarning, ToolTipIcon.Error);
            };
            warningTimer.Start();
        }
    }

    private void OpenSettings()
    {
        if (settingsForm is { IsDisposed: false })
        {
            settingsForm.Activate();
            return;
        }

        settingsForm = new SettingsForm(controller);
        settingsForm.FormClosed += (_, _) => settingsForm = null;
        settingsForm.Show();
        settingsForm.Activate();
    }

    private void OpenLogDirectory()
    {
        try
        {
            Directory.CreateDirectory(controller.Paths.LogDirectory);
            Process.Start(new ProcessStartInfo(controller.Paths.LogDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            logger.Error("로그 폴더를 열지 못했습니다.", ex);
            ShowBalloon("로그 폴더를 열지 못했습니다.", ToolTipIcon.Error);
        }
    }

    private void OpenHistory()
    {
        if (historyForm is { IsDisposed: false })
        {
            historyForm.RefreshEntries();
            historyForm.Activate();
            return;
        }

        historyForm = new TranslationHistoryForm(controller);
        historyForm.FormClosed += (_, _) => historyForm = null;
        historyForm.Show();
        historyForm.Activate();
    }

    private static void ShowAbout() => MessageBox.Show(
        $"Gemini 기반 Windows 번역 도우미\n\nAlt+R  읽기 번역\nAlt+T  작성 번역\n\nVersion {Application.ProductVersion} · .NET 10",
        "Windows Tray Translator",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);

    private void OnNotificationRequested(object? sender, UserNotificationEventArgs e) =>
        ShowBalloon(e.Message, e.Icon);

    private void OnTranslationPopupRequested(object? sender, TranslationPopupRequestedEventArgs e)
    {
        if (translationPopup is null || translationPopup.IsDisposed)
        {
            translationPopup = new TranslationResultPopupForm();
            translationPopup.FallbackTranslationRequested += (_, request) =>
                controller.RequestFallbackTranslation(request.OperationId);
            translationPopup.Dismissed += (_, dismissed) =>
                controller.CancelTranslationInteraction(dismissed.OperationId);
            translationPopup.FormClosed += (_, _) => translationPopup = null;
        }

        translationPopup.ShowContent(e);
    }

    private void OnSettingsApplied(object? sender, EventArgs e)
    {
        if (enabledItem.Checked != controller.Settings.General.TranslationEnabled)
        {
            enabledItem.Checked = controller.Settings.General.TranslationEnabled;
        }
    }

    private void OnLanguageSelectionRequested(object? sender, LanguageSelectionRequestedEventArgs e)
    {
        e.MarkHandled();
        languageSelectionPopup?.Close();
        languageSelectionPopup = new LanguageSelectionPopupForm(e);
        languageSelectionPopup.FormClosed += (_, _) => languageSelectionPopup = null;
        languageSelectionPopup.Show();
    }

    private void OnTranslationHistoryChanged(object? sender, EventArgs e) =>
        historyForm?.RefreshEntries();

    private void OnActionPaletteRequested(object? sender, QuickActionSession session)
    {
        actionPalette?.Close();
        QuickActionPaletteForm palette = new(session, controller.TryStartPaletteAction);
        actionPalette = palette;
        palette.Dismissed += (_, _) =>
        {
            if (ReferenceEquals(actionPalette, palette))
            {
                actionPalette = null;
            }

            controller.DismissActionPalette(session);
        };
        palette.Show();
    }

    private void ShowBalloon(string message, ToolTipIcon icon)
    {
        notifyIcon.BalloonTipTitle = "Windows Tray Translator";
        notifyIcon.BalloonTipText = message;
        notifyIcon.BalloonTipIcon = icon;
        notifyIcon.ShowBalloonTip(4000);
    }

    private void ExitApplication()
    {
        settingsForm?.Close();
        historyForm?.Close();
        translationPopup?.Close();
        languageSelectionPopup?.Close();
        actionPalette?.Close();
        notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            controller.NotificationRequested -= OnNotificationRequested;
            controller.TranslationPopupRequested -= OnTranslationPopupRequested;
            controller.SettingsApplied -= OnSettingsApplied;
            controller.LanguageSelectionRequested -= OnLanguageSelectionRequested;
            controller.TranslationHistoryChanged -= OnTranslationHistoryChanged;
            controller.ActionPaletteRequested -= OnActionPaletteRequested;
            translationPopup?.Dispose();
            historyForm?.Dispose();
            languageSelectionPopup?.Dispose();
            actionPalette?.Dispose();
            notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
