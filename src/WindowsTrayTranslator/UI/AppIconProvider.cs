using System.Drawing;

namespace WindowsTrayTranslator.UI;

internal static class AppIconProvider
{
    private static readonly Lazy<Icon> IconInstance = new(LoadApplicationIcon);

    public static Icon ApplicationIcon => IconInstance.Value;

    private static Icon LoadApplicationIcon()
    {
        try
        {
            Icon? icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon is not null)
            {
                return icon;
            }
        }
        catch
        {
            // The system icon remains a safe fallback for unusual launch environments.
        }

        return SystemIcons.Application;
    }
}
