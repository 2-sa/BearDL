using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nickvision.Desktop.Globalization;
using Nickvision.Parabolic.Shared.Controllers;
using System;
using System.IO;

namespace Nickvision.Parabolic.WinUI.Helpers;

/// <summary>
/// UI layout mode helper for WinUI 3 (Classic vs Modern layout).
/// </summary>
public static class GlassThemeHelper
{
    private static bool? _useModernLayout;

    public static event EventHandler<bool>? LayoutModeChanged;

    private static string LayoutModeConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Nickvision",
        "Parabolic",
        "ui-layout-mode.txt");

    public static bool UseModernLayout
    {
        get
        {
            if (_useModernLayout.HasValue)
            {
                return _useModernLayout.Value;
            }
            try
            {
                if (File.Exists(LayoutModeConfigPath))
                {
                    var text = File.ReadAllText(LayoutModeConfigPath).Trim();
                    _useModernLayout = string.Equals(text, "modern", StringComparison.OrdinalIgnoreCase);
                    return _useModernLayout.Value;
                }
            }
            catch
            {
                // Ignore read errors and fall back to Classic (false)
            }
            _useModernLayout = false;
            return false;
        }
    }

    public static void SetUseModernLayout(bool useModern)
    {
        if (_useModernLayout == useModern)
        {
            return;
        }
        _useModernLayout = useModern;
        try
        {
            var dir = Path.GetDirectoryName(LayoutModeConfigPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(LayoutModeConfigPath, useModern ? "modern" : "classic");
        }
        catch
        {
            // Ignore write errors
        }
        LayoutModeChanged?.Invoke(null, useModern);
    }

    public static void AttachSettingsCard(Panel parentPanel, SettingsCard rowTheme, ComboBox cmbTheme, PreferencesViewController controller, ITranslationService translationService)
    {
        foreach (var child in parentPanel.Children)
        {
            if (child is FrameworkElement fe && fe.Tag as string == "UiLayoutModeCard")
            {
                return;
            }
        }

        var cmbLayout = new ComboBox
        {
            Style = (Style)Application.Current.Resources["PageComboBoxStyle"],
            VerticalAlignment = VerticalAlignment.Center
        };
        cmbLayout.Items.Add(translationService._("Classic"));
        cmbLayout.Items.Add(translationService._("Modern"));
        cmbLayout.SelectedIndex = UseModernLayout ? 1 : 0;
        cmbLayout.SelectionChanged += (_, _) =>
        {
            if (cmbLayout.SelectedIndex >= 0)
            {
                SetUseModernLayout(cmbLayout.SelectedIndex == 1);
            }
        };

        var layoutCard = new SettingsCard
        {
            Style = (Style)Application.Current.Resources["PreferenceCardStyle"],
            Tag = "UiLayoutModeCard",
            Header = translationService._("Interface Layout"),
            Description = translationService._("Choose between the classic or modern interface layout"),
            HeaderIcon = new FontIcon
            {
                Glyph = "\uECA5"
            },
            Content = cmbLayout
        };

        var themeIndex = parentPanel.Children.IndexOf(rowTheme);
        if (themeIndex >= 0 && themeIndex + 1 < parentPanel.Children.Count)
        {
            parentPanel.Children.Insert(themeIndex + 1, layoutCard);
        }
        else
        {
            parentPanel.Children.Add(layoutCard);
        }
    }
}
