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
    private TranslationPopupForm? translationPopup;

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
        menu.Items.Add("설정 열기", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(enabledItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("로그 폴더 열기", null, (_, _) => OpenLogDirectory());
        menu.Items.Add("프로그램 정보", null, (_, _) => ShowAbout());
        menu.Items.Add("종료", null, (_, _) => ExitApplication());

        notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Windows Tray Translator",
            ContextMenuStrip = menu,
            Visible = true
        };
        notifyIcon.DoubleClick += (_, _) => OpenSettings();
        controller.NotificationRequested += OnNotificationRequested;
        controller.TranslationPopupRequested += OnTranslationPopupRequested;

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

    private static void ShowAbout() => MessageBox.Show(
        ".NET 8 기반 Gemini 번역 트레이 애플리케이션\n\n현재 구현 범위: Phase 1~4",
        "Windows Tray Translator",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);

    private void OnNotificationRequested(object? sender, UserNotificationEventArgs e) =>
        ShowBalloon(e.Message, e.Icon);

    private void OnTranslationPopupRequested(object? sender, TranslationPopupRequestedEventArgs e)
    {
        if (translationPopup is null || translationPopup.IsDisposed)
        {
            translationPopup = new TranslationPopupForm();
            translationPopup.FormClosed += (_, _) => translationPopup = null;
        }

        translationPopup.ShowContent(e.Kind, e.Message, e.CopyText, e.TimeoutMs);
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
        translationPopup?.Close();
        notifyIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            controller.NotificationRequested -= OnNotificationRequested;
            controller.TranslationPopupRequested -= OnTranslationPopupRequested;
            translationPopup?.Dispose();
            notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
