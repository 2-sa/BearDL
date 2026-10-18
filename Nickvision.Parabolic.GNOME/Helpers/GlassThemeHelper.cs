using Nickvision.Parabolic.Shared.Models;

namespace Nickvision.Parabolic.GNOME.Helpers;

/// <summary>
/// Isolated Apple-inspired (visionOS / macOS Liquid Glass) theme helper for GTK4 / libadwaita.
/// To remove Glass Mode later, delete this file and Resources/glass-theme.css, and revert the calls in MainWindow.cs and PreferencesDialog.cs.
/// </summary>
public static class GlassThemeHelper
{
    private const string GlassCssClass = "glass-mode";

    public static void Apply(Gtk.Window window, Theme theme)
    {
        Adw.StyleManager.GetDefault().ColorScheme = theme switch
        {
            Theme.Light => Adw.ColorScheme.ForceLight,
            Theme.Dark or Theme.Glass => Adw.ColorScheme.ForceDark,
            _ => Adw.ColorScheme.Default
        };

        if (theme == Theme.Glass)
        {
            window.AddCssClass(GlassCssClass);
        }
        else
        {
            window.RemoveCssClass(GlassCssClass);
        }
    }
}
