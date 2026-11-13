using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nickvision.Desktop.Application;
using Nickvision.Desktop.Globalization;
using Nickvision.Parabolic.Shared.Controllers;
using Nickvision.Parabolic.Shared.Helpers;
using System;
using System.IO;

namespace Nickvision.Parabolic.WinUI.Helpers;

/// <summary>
/// UI layout mode helper for WinUI 3 (Classic vs Modern layout).
/// </summary>
public static class GlassThemeHelper
{
    private static IConfigurationService? _configurationService;
    private static bool _fallbackUseModernLayout = true;

    public static event EventHandler<bool>? LayoutModeChanged;

    private static string LayoutModeConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Nickvision",
        "Parabolic",
        "ui-layout-mode.txt");

    public static void Initialize(IConfigurationService configurationService, bool isPortable)
    {
        _configurationService = configurationService;
        if (!isPortable && File.Exists(LayoutModeConfigPath))
        {
            try
            {
                var hasSavedValue = configurationService.Get("UseModernLayout", true) == configurationService.Get("UseModernLayout", false);
                if (!hasSavedValue)
                {
                    var text = File.ReadAllText(LayoutModeConfigPath).Trim();
                    configurationService.UseModernLayout = !string.Equals(text, "classic", StringComparison.OrdinalIgnoreCase);
                }
                File.Delete(LayoutModeConfigPath);
            }
            catch
            {
                // Ignore migration errors and use configured/default Modern layout.
            }
        }
        _fallbackUseModernLayout = configurationService.UseModernLayout;
    }

    public static bool UseModernLayout => _configurationService is not null
        ? _configurationService.UseModernLayout
        : _fallbackUseModernLayout;

    public static void SetUseModernLayout(bool useModern)
    {
        if (UseModernLayout == useModern)
        {
            return;
        }
        _fallbackUseModernLayout = useModern;
        if (_configurationService is not null)
        {
            _configurationService.UseModernLayout = useModern;
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
