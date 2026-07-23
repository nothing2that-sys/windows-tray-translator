namespace WindowsTrayTranslator.UI;

internal static class AppTheme
{
    public static readonly Color Primary = Color.FromArgb(31, 103, 224);
    public static readonly Color PrimaryDark = Color.FromArgb(23, 74, 166);
    public static readonly Color PrimarySoft = Color.FromArgb(232, 240, 255);
    public static readonly Color Canvas = Color.FromArgb(246, 248, 252);
    public static readonly Color Surface = Color.White;
    public static readonly Color Text = Color.FromArgb(35, 42, 55);
    public static readonly Color MutedText = Color.FromArgb(99, 108, 123);
    public static readonly Color Border = Color.FromArgb(218, 224, 234);
    public static readonly Color Success = Color.FromArgb(25, 135, 84);
    public static readonly Color Danger = Color.FromArgb(190, 52, 61);

    public static void StylePrimaryButton(Button button)
    {
        StyleButtonBase(button);
        button.BackColor = Primary;
        button.ForeColor = Color.White;
        button.FlatAppearance.BorderSize = 0;
    }

    public static void StyleSecondaryButton(Button button)
    {
        StyleButtonBase(button);
        button.BackColor = Surface;
        button.ForeColor = Text;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.BorderSize = 1;
    }

    public static void StyleDangerButton(Button button)
    {
        StyleSecondaryButton(button);
        button.ForeColor = Danger;
        button.FlatAppearance.BorderColor = Color.FromArgb(235, 195, 199);
    }

    public static Label CreateSectionLabel(string text) => new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 10f),
        ForeColor = PrimaryDark,
        Text = text,
        Margin = new Padding(3, 15, 3, 5)
    };

    private static void StyleButtonBase(Button button)
    {
        button.AutoSize = true;
        button.FlatStyle = FlatStyle.Flat;
        button.Padding = new Padding(10, 2, 10, 2);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }
}

internal sealed class AppMenuColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelected => AppTheme.PrimarySoft;
    public override Color MenuItemBorder => AppTheme.PrimarySoft;
    public override Color MenuBorder => AppTheme.Border;
    public override Color ToolStripDropDownBackground => AppTheme.Surface;
    public override Color ImageMarginGradientBegin => AppTheme.Surface;
    public override Color ImageMarginGradientMiddle => AppTheme.Surface;
    public override Color ImageMarginGradientEnd => AppTheme.Surface;
    public override Color SeparatorDark => AppTheme.Border;
    public override Color SeparatorLight => AppTheme.Surface;
}
