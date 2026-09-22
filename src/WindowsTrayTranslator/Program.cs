using WindowsTrayTranslator.App;
using WindowsTrayTranslator.Configuration;
using WindowsTrayTranslator.Logging;
using WindowsTrayTranslator.UI;

namespace WindowsTrayTranslator;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        AppPaths paths = AppPaths.CreateDefault();
        paths.EnsureDirectories();

        using FileLogger logger = new(paths.LogDirectory, enabled: true, retentionDays: 14);
        Application.ThreadException += (_, e) => logger.Error("처리되지 않은 UI 예외", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.Error("처리되지 않은 애플리케이션 예외", e.ExceptionObject as Exception);

        using SingleInstanceManager singleInstance = new("WindowsTrayTranslator.SingleInstance");
        if (!singleInstance.IsPrimaryInstance)
        {
            logger.Warning("중복 실행이 감지되어 두 번째 프로세스를 종료합니다.");
            return;
        }

        try
        {
            ConfigurationService configuration = new(paths.SettingsFile, logger);
            AppSettings settings = configuration.Load();
            logger.Configure(settings.General.EnableLogging, settings.General.LogRetentionDays);

            using ApplicationController controller = new(paths, configuration, settings, logger);
            using TrayApplicationContext context = new(controller, logger);
            logger.Information("프로그램을 시작합니다.");
            Application.Run(context);
            logger.Information("프로그램을 종료합니다.");
        }
        catch (Exception ex)
        {
            logger.Error("프로그램을 시작하지 못했습니다.", ex);
            MessageBox.Show(
                "프로그램을 시작하지 못했습니다. 로그를 확인해 주세요.",
                "Windows Tray Translator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
