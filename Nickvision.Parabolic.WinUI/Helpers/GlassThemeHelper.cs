using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace Nickvision.Parabolic.WinUI.Helpers;

/// <summary>
/// Isolated Apple-inspired (visionOS / macOS Liquid Glass) theme helper for WinUI 3.
/// To remove Glass Mode later, delete this file and remove the single GlassThemeHelper.Apply(...) call in MainWindow.xaml.cs.
/// </summary>
public static class GlassThemeHelper
{
    private static Grid? _atmosphereLayer;
    private static ResourceDictionary? _glassDictionary;
    private static object? _origNavContentBackground;
    private static object? _origNavContentBorderBrush;
    private static bool _wasGlass;

    public static void Apply(Window window, Grid windowRoot, Grid mainGrid, NavigationView navViewDownloads, bool isGlass)
    {
        if (isGlass)
        {
            if (window.SystemBackdrop is not DesktopAcrylicBackdrop)
            {
                window.SystemBackdrop = new DesktopAcrylicBackdrop();
            }
            EnsureAtmosphereLayer(windowRoot);
            EnsureGlassResources(windowRoot, navViewDownloads);
            ApplyFloatingGlassStructure(mainGrid, navViewDownloads, true);
            _wasGlass = true;
        }
        else if (_wasGlass)
        {
            if (window.SystemBackdrop is not MicaBackdrop)
            {
                window.SystemBackdrop = new MicaBackdrop();
            }
            RemoveAtmosphereLayer(windowRoot);
            RemoveGlassResources(windowRoot, navViewDownloads);
            ApplyFloatingGlassStructure(mainGrid, navViewDownloads, false);
            _wasGlass = false;
        }
    }

    private static void EnsureAtmosphereLayer(Grid windowRoot)
    {
        if (_atmosphereLayer is null)
        {
            _atmosphereLayer = new Grid
            {
                IsHitTestVisible = false,
                Background = CreateLinearGradient(
                    new Point(0, 0),
                    new Point(1, 1),
                    (Color.FromArgb(0x42, 0x06, 0x0C, 0x1A), 0.0),
                    (Color.FromArgb(0x28, 0x0D, 0x1E, 0x3A), 0.5),
                    (Color.FromArgb(0x3C, 0x07, 0x0B, 0x16), 1.0))
            };

            // Top spatial aurora glow inspired by Apple visionOS / macOS dark glass
            var topGlow = new Border
            {
                IsHitTestVisible = false,
                Background = CreateLinearGradient(
                    new Point(0.5, 0),
                    new Point(0.5, 0.45),
                    (Color.FromArgb(0x30, 0x0A, 0x84, 0xFF), 0.0),
                    (Color.FromArgb(0x14, 0x38, 0xBD, 0xF8), 0.45),
                    (Color.FromArgb(0x00, 0x00, 0x00, 0x00), 1.0))
            };

            // Luminous 1px inner specular window rim
            var innerRim = new Border
            {
                IsHitTestVisible = false,
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
                BorderBrush = CreateLinearGradient(
                    new Point(0, 0),
                    new Point(1, 1),
                    (Color.FromArgb(0x48, 0xFF, 0xFF, 0xFF), 0.0),
                    (Color.FromArgb(0x18, 0x38, 0xBD, 0xF8), 0.5),
                    (Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF), 1.0))
            };

            _atmosphereLayer.Children.Add(topGlow);
            _atmosphereLayer.Children.Add(innerRim);
        }

        if (!windowRoot.Children.Contains(_atmosphereLayer))
        {
            windowRoot.Children.Insert(0, _atmosphereLayer);
        }
    }

    private static void RemoveAtmosphereLayer(Grid windowRoot)
    {
        if (_atmosphereLayer is not null && windowRoot.Children.Contains(_atmosphereLayer))
        {
            windowRoot.Children.Remove(_atmosphereLayer);
        }
    }

    private static void EnsureGlassResources(Grid windowRoot, NavigationView navViewDownloads)
    {
        _glassDictionary ??= BuildGlassResourceDictionary();
        if (!windowRoot.Resources.MergedDictionaries.Contains(_glassDictionary))
        {
            windowRoot.Resources.MergedDictionaries.Add(_glassDictionary);
        }

        if (_origNavContentBackground is null && navViewDownloads.Resources.TryGetValue("NavigationViewContentBackground", out var origBg))
        {
            _origNavContentBackground = origBg;
        }
        if (_origNavContentBorderBrush is null && navViewDownloads.Resources.TryGetValue("NavigationViewContentGridBorderBrush", out var origBorder))
        {
            _origNavContentBorderBrush = origBorder;
        }

        navViewDownloads.Resources["NavigationViewContentBackground"] = CreateLinearGradient(
            new Point(0, 0),
            new Point(1, 1),
            (Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF), 0.0),
            (Color.FromArgb(0x10, 0xC8, 0xDC, 0xFF), 1.0));
        navViewDownloads.Resources["NavigationViewContentGridBorderBrush"] = CreateSpecularBorderBrush();
    }

    private static void RemoveGlassResources(Grid windowRoot, NavigationView navViewDownloads)
    {
        if (_glassDictionary is not null && windowRoot.Resources.MergedDictionaries.Contains(_glassDictionary))
        {
            windowRoot.Resources.MergedDictionaries.Remove(_glassDictionary);
        }

        if (_origNavContentBackground is not null)
        {
            navViewDownloads.Resources["NavigationViewContentBackground"] = _origNavContentBackground;
        }
        if (_origNavContentBorderBrush is not null)
        {
            navViewDownloads.Resources["NavigationViewContentGridBorderBrush"] = _origNavContentBorderBrush;
        }
    }

    private static void ApplyFloatingGlassStructure(Grid mainGrid, NavigationView navViewDownloads, bool isGlass)
    {
        if (mainGrid.Children.Count > 1 && mainGrid.Children[1] is MenuFlyoutSeparator topSeparator)
        {
            topSeparator.Visibility = isGlass ? Visibility.Collapsed : Visibility.Visible;
        }

        if (navViewDownloads.Parent is Grid downloadsGrid)
        {
            if (downloadsGrid.Children.Count > 0 && downloadsGrid.Children[0] is Grid toolbarGrid)
            {
                if (isGlass)
                {
                    toolbarGrid.Margin = new Thickness(14, 8, 14, 6);
                    toolbarGrid.Padding = new Thickness(8, 4, 8, 4);
                    toolbarGrid.CornerRadius = new CornerRadius(14);
                    toolbarGrid.BorderThickness = new Thickness(1);
                    toolbarGrid.Background = CreateLinearGradient(
                        new Point(0, 0),
                        new Point(1, 1),
                        (Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF), 0.0),
                        (Color.FromArgb(0x16, 0xD8, 0xE8, 0xFF), 1.0));
                    toolbarGrid.BorderBrush = CreateSpecularBorderBrush();
                }
                else
                {
                    toolbarGrid.Margin = new Thickness(0);
                    toolbarGrid.Padding = new Thickness(0);
                    toolbarGrid.CornerRadius = new CornerRadius(0);
                    toolbarGrid.BorderThickness = new Thickness(0);
                    toolbarGrid.Background = null;
                    toolbarGrid.BorderBrush = null;
                }
            }

            if (downloadsGrid.Children.Count > 1 && downloadsGrid.Children[1] is MenuFlyoutSeparator downloadsSeparator)
            {
                downloadsSeparator.Visibility = isGlass ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        navViewDownloads.Margin = isGlass ? new Thickness(12, 2, 12, 12) : new Thickness(0);
    }

    private static ResourceDictionary BuildGlassResourceDictionary()
    {
        var dict = new ResourceDictionary();
        var darkDict = new ResourceDictionary();
        var defaultDict = new ResourceDictionary();

        PopulateGlassTokens(darkDict);
        PopulateGlassTokens(defaultDict);
        PopulateGlassTokens(dict);

        dict.ThemeDictionaries["Dark"] = darkDict;
        dict.ThemeDictionaries["Default"] = defaultDict;
        return dict;
    }

    private static void PopulateGlassTokens(ResourceDictionary target)
    {
        var transparentBrush = new SolidColorBrush(Color.FromArgb(0x00, 0x00, 0x00, 0x00));

        // Frosted spatial card surface with subtle diagonal luminance
        var cardSurfaceBrush = CreateLinearGradient(
            new Point(0, 0),
            new Point(1, 1),
            (Color.FromArgb(0x32, 0xFF, 0xFF, 0xFF), 0.0),
            (Color.FromArgb(0x1A, 0xE2, 0xEE, 0xFF), 0.55),
            (Color.FromArgb(0x12, 0x94, 0xB8, 0xFF), 1.0));

        var cardSecondaryBrush = CreateLinearGradient(
            new Point(0, 0),
            new Point(1, 1),
            (Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF), 0.0),
            (Color.FromArgb(0x10, 0xC8, 0xDC, 0xFF), 1.0));

        var specularBorderBrush = CreateSpecularBorderBrush();

        // Frosted interactive pill controls
        var controlFillBrush = CreateLinearGradient(
            new Point(0, 0),
            new Point(0, 1),
            (Color.FromArgb(0x34, 0xFF, 0xFF, 0xFF), 0.0),
            (Color.FromArgb(0x1C, 0xE2, 0xEE, 0xFF), 1.0));

        var controlHoverBrush = CreateLinearGradient(
            new Point(0, 0),
            new Point(0, 1),
            (Color.FromArgb(0x4A, 0xFF, 0xFF, 0xFF), 0.0),
            (Color.FromArgb(0x28, 0xE2, 0xEE, 0xFF), 1.0));

        var controlPressedBrush = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));

        // Apple Liquid Blue accent (#38BDF8 -> #0A84FF)
        var accentGlassBrush = CreateLinearGradient(
            new Point(0, 0),
            new Point(1, 1),
            (Color.FromArgb(0xEE, 0x38, 0xBD, 0xF8), 0.0),
            (Color.FromArgb(0xEE, 0x0A, 0x84, 0xFF), 1.0));

        var accentHoverBrush = CreateLinearGradient(
            new Point(0, 0),
            new Point(1, 1),
            (Color.FromArgb(0xFF, 0x56, 0xCC, 0xFF), 0.0),
            (Color.FromArgb(0xFF, 0x24, 0x94, 0xFF), 1.0));

        var accentPressedBrush = new SolidColorBrush(Color.FromArgb(0xD8, 0x00, 0x70, 0xE0));

        var dialogSurfaceBrush = CreateLinearGradient(
            new Point(0, 0),
            new Point(1, 1),
            (Color.FromArgb(0xE8, 0x10, 0x1A, 0x2E), 0.0),
            (Color.FromArgb(0xE0, 0x0A, 0x10, 0x20), 1.0));

        // Page & Layer backgrounds (transparent so DesktopAcrylic + spatial glow shine through)
        target["ApplicationPageBackgroundThemeBrush"] = transparentBrush;
        target["LayerFillColorDefaultBrush"] = cardSecondaryBrush;
        target["LayerFillColorAltBrush"] = transparentBrush;
        target["LayerOnMicaBaseAltFillColorDefaultBrush"] = transparentBrush;
        target["LayerOnAcrylicFillColorDefaultBrush"] = cardSecondaryBrush;
        target["SolidBackgroundFillColorBaseBrush"] = transparentBrush;
        target["SolidBackgroundFillColorSecondaryBrush"] = transparentBrush;
        target["SolidBackgroundFillColorTertiaryBrush"] = cardSecondaryBrush;
        target["SolidBackgroundFillColorQuarternaryBrush"] = cardSurfaceBrush;

        // Cards & Expanders (Settings rows, DownloadRow cards, Quick Actions)
        target["CardBackgroundFillColorDefaultBrush"] = cardSurfaceBrush;
        target["CardBackgroundFillColorSecondaryBrush"] = cardSecondaryBrush;
        target["CardStrokeColorDefaultBrush"] = specularBorderBrush;
        target["CardStrokeColorDefaultSolidBrush"] = new SolidColorBrush(Color.FromArgb(0x45, 0xFF, 0xFF, 0xFF));
        target["ExpanderHeaderBackground"] = cardSurfaceBrush;
        target["ExpanderContentBackground"] = cardSecondaryBrush;
        target["ExpanderHeaderBorderBrush"] = specularBorderBrush;
        target["ExpanderContentBorderBrush"] = specularBorderBrush;

        // Interactive Controls & Buttons
        target["ControlFillColorDefaultBrush"] = controlFillBrush;
        target["ControlFillColorSecondaryBrush"] = controlHoverBrush;
        target["ControlFillColorTertiaryBrush"] = controlPressedBrush;
        target["ControlStrokeColorDefaultBrush"] = specularBorderBrush;
        target["ControlStrokeColorSecondaryBrush"] = specularBorderBrush;
        target["ControlElevationBorderBrush"] = specularBorderBrush;
        target["ButtonBackground"] = controlFillBrush;
        target["ButtonBackgroundPointerOver"] = controlHoverBrush;
        target["ButtonBackgroundPressed"] = controlPressedBrush;
        target["ButtonBorderBrush"] = specularBorderBrush;
        target["ButtonBorderBrushPointerOver"] = specularBorderBrush;
        target["SubtleFillColorSecondaryBrush"] = controlFillBrush;
        target["SubtleFillColorTertiaryBrush"] = controlPressedBrush;

        // Apple Liquid Blue Accent
        target["SystemAccentColor"] = Color.FromArgb(0xFF, 0x0A, 0x84, 0xFF);
        target["AccentFillColorDefaultBrush"] = accentGlassBrush;
        target["AccentFillColorSecondaryBrush"] = accentHoverBrush;
        target["AccentFillColorTertiaryBrush"] = accentPressedBrush;
        target["AccentButtonBackground"] = accentGlassBrush;
        target["AccentButtonBackgroundPointerOver"] = accentHoverBrush;
        target["AccentButtonBackgroundPressed"] = accentPressedBrush;
        target["AccentButtonBorderBrush"] = specularBorderBrush;

        // NavigationView & Floating Sidebar
        target["NavigationViewContentBackground"] = cardSecondaryBrush;
        target["NavigationViewContentGridBorderBrush"] = specularBorderBrush;
        target["NavigationViewDefaultPaneBackground"] = transparentBrush;
        target["NavigationViewTopPaneBackground"] = transparentBrush;
        target["NavigationViewExpandedPaneBackground"] = dialogSurfaceBrush;

        // Flyouts & ContentDialogs
        target["AcrylicBackgroundFillColorDefaultBrush"] = dialogSurfaceBrush;
        target["AcrylicInAppFillColorDefaultBrush"] = dialogSurfaceBrush;
        target["ContentDialogBackground"] = dialogSurfaceBrush;
        target["ContentDialogBorderBrush"] = specularBorderBrush;
        target["FlyoutPresenterBackground"] = dialogSurfaceBrush;
        target["FlyoutBorderThemeBrush"] = specularBorderBrush;
        target["SurfaceStrokeColorFlyoutBrush"] = specularBorderBrush;
        target["SurfaceStrokeColorDefaultBrush"] = specularBorderBrush;
        target["DividerStrokeColorDefaultBrush"] = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));

        // Rounded Apple geometry
        target["ControlCornerRadius"] = new CornerRadius(12);
        target["OverlayCornerRadius"] = new CornerRadius(18);
    }

    private static LinearGradientBrush CreateSpecularBorderBrush()
    {
        return CreateLinearGradient(
            new Point(0, 0),
            new Point(1, 1),
            (Color.FromArgb(0x68, 0xFF, 0xFF, 0xFF), 0.0),
            (Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF), 0.5),
            (Color.FromArgb(0x38, 0x38, 0xBD, 0xF8), 1.0));
    }

    private static LinearGradientBrush CreateLinearGradient(Point start, Point end, params (Color Color, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = start,
            EndPoint = end
        };
        foreach (var (color, offset) in stops)
        {
            brush.GradientStops.Add(new GradientStop
            {
                Color = color,
                Offset = offset
            });
        }
        return brush;
    }
}
