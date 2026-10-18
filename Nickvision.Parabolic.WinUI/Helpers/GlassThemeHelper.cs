using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.UI;

namespace Nickvision.Parabolic.WinUI.Helpers;

/// <summary>
/// True see-through transparent window backdrop for WinUI 3 using DWM composition and a transparent CompositionColorBrush.
/// </summary>
public sealed partial class ClearGlassBackdrop : SystemBackdrop
{
    private const uint WM_ERASEBKGND = 0x0014;
    private const uint WM_DWMCOMPOSITIONCHANGED = 0x031E;
    private const nuint SubclassId = 0xBEA4D101;

    private static nint _dispatcherQueueController;
    private static Windows.UI.Composition.Compositor? _compositor;
    private static nint _blackBrush;

    private Windows.UI.Composition.CompositionColorBrush? _brush;
    private nint _hwnd;

    protected override unsafe void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        _hwnd = (nint)xamlRoot.ContentIslandEnvironment.AppWindowId.Value;
        if (_hwnd != 0)
        {
            SetWindowSubclass(_hwnd, &SubclassProc, SubclassId, 0);
            ConfigureDwm(_hwnd, true);
        }

        EnsureCompositor();
        _brush = _compositor!.CreateColorBrush(Color.FromArgb(0x22, 0x08, 0x10, 0x1E));
        connectedTarget.SystemBackdrop = _brush;

        base.OnTargetConnected(connectedTarget, xamlRoot);

        if (_hwnd != 0)
        {
            var hdc = GetDC(_hwnd);
            if (hdc != 0)
            {
                ClearBackground(_hwnd, hdc);
                ReleaseDC(_hwnd, hdc);
            }
        }
    }

    protected override unsafe void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        if (_hwnd != 0)
        {
            RemoveWindowSubclass(_hwnd, &SubclassProc, SubclassId);
            ConfigureDwm(_hwnd, false);
            _hwnd = 0;
        }

        var backdrop = disconnectedTarget.SystemBackdrop;
        disconnectedTarget.SystemBackdrop = null;
        backdrop?.Dispose();
        _brush?.Dispose();
        _brush = null;

        base.OnTargetDisconnected(disconnectedTarget);
    }

    private static void EnsureCompositor()
    {
        if (_compositor is not null)
        {
            return;
        }
        if (Windows.System.DispatcherQueue.GetForCurrentThread() is null && _dispatcherQueueController == 0)
        {
            var options = new DispatcherQueueOptions
            {
                dwSize = Marshal.SizeOf<DispatcherQueueOptions>(),
                threadType = 2,    // DQTYPE_THREAD_CURRENT
                apartmentType = 2  // DQTAT_COM_STA
            };
            CreateDispatcherQueueController(options, out _dispatcherQueueController);
        }
        _compositor = new Windows.UI.Composition.Compositor();
    }

    private static void ConfigureDwm(nint hwnd, bool enable)
    {
        if (enable)
        {
            var margins = new MARGINS();
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
            var rgn = CreateRectRgn(-2, -2, -1, -1);
            var blur = new DWM_BLURBEHIND
            {
                dwFlags = 3, // DWM_BB_ENABLE | DWM_BB_BLURREGION
                fEnable = 1,
                hRgnBlur = rgn,
                fTransitionOnMaximized = 0
            };
            DwmEnableBlurBehindWindow(hwnd, ref blur);
            if (rgn != 0)
            {
                DeleteObject(rgn);
            }
        }
        else
        {
            var blur = new DWM_BLURBEHIND
            {
                dwFlags = 1, // DWM_BB_ENABLE
                fEnable = 0,
                hRgnBlur = 0,
                fTransitionOnMaximized = 0
            };
            DwmEnableBlurBehindWindow(hwnd, ref blur);
        }
    }

    private static bool ClearBackground(nint hwnd, nint hdc)
    {
        if (GetClientRect(hwnd, out var rect) != 0)
        {
            if (_blackBrush == 0)
            {
                _blackBrush = CreateSolidBrush(0);
            }
            FillRect(hdc, ref rect, _blackBrush);
            return true;
        }
        return false;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint SubclassProc(nint hWnd, uint uMsg, nuint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
    {
        if (uMsg == WM_ERASEBKGND)
        {
            if (ClearBackground(hWnd, (nint)wParam))
            {
                return 1;
            }
        }
        else if (uMsg == WM_DWMCOMPOSITIONCHANGED)
        {
            ConfigureDwm(hWnd, true);
            return 0;
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_BLURBEHIND
    {
        public uint dwFlags;
        public int fEnable;
        public nint hRgnBlur;
        public int fTransitionOnMaximized;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    [DllImport("ComCtl32.dll", ExactSpelling = true)]
    private static extern unsafe int SetWindowSubclass(nint hWnd, delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nuint, nuint, nint> pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("ComCtl32.dll", ExactSpelling = true)]
    private static extern unsafe int RemoveWindowSubclass(nint hWnd, delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nuint, nuint, nint> pfnSubclass, nuint uIdSubclass);

    [DllImport("ComCtl32.dll", ExactSpelling = true)]
    private static extern nint DefSubclassProc(nint hWnd, uint uMsg, nuint wParam, nint lParam);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmExtendFrameIntoClientArea(nint hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmEnableBlurBehindWindow(nint hWnd, ref DWM_BLURBEHIND pBlurBehind);

    [DllImport("User32.dll", ExactSpelling = true)]
    private static extern nint GetDC(nint hWnd);

    [DllImport("User32.dll", ExactSpelling = true)]
    private static extern int ReleaseDC(nint hWnd, nint hDC);

    [DllImport("User32.dll", ExactSpelling = true)]
    private static extern int GetClientRect(nint hWnd, out RECT lpRect);

    [DllImport("User32.dll", ExactSpelling = true)]
    private static extern int FillRect(nint hDC, ref RECT lprc, nint hbr);

    [DllImport("Gdi32.dll", ExactSpelling = true)]
    private static extern nint CreateRectRgn(int x1, int y1, int x2, int y2);

    [DllImport("Gdi32.dll", ExactSpelling = true)]
    private static extern nint CreateSolidBrush(uint color);

    [DllImport("Gdi32.dll", ExactSpelling = true)]
    private static extern int DeleteObject(nint hObject);

    [DllImport("CoreMessaging.dll", ExactSpelling = true)]
    private static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, out nint dispatcherQueueController);
}

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
            if (window.SystemBackdrop is not ClearGlassBackdrop)
            {
                window.SystemBackdrop = new ClearGlassBackdrop();
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
            // Pure transparent canvas so the desktop/windows behind shine through clearly
            _atmosphereLayer = new Grid
            {
                IsHitTestVisible = false
            };

            // Subtle top specular reflection
            var topGlow = new Border
            {
                IsHitTestVisible = false,
                Background = CreateLinearGradient(
                    new Point(0.5, 0),
                    new Point(0.5, 0.35),
                    (Color.FromArgb(0x22, 0x38, 0xBD, 0xF8), 0.0),
                    (Color.FromArgb(0x0C, 0xFF, 0xFF, 0xFF), 0.4),
                    (Color.FromArgb(0x00, 0x00, 0x00, 0x00), 1.0))
            };

            // Luminous 1px inner crystal window rim
            var innerRim = new Border
            {
                IsHitTestVisible = false,
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
                BorderBrush = CreateLinearGradient(
                    new Point(0, 0),
                    new Point(1, 1),
                    (Color.FromArgb(0x58, 0xFF, 0xFF, 0xFF), 0.0),
                    (Color.FromArgb(0x22, 0x38, 0xBD, 0xF8), 0.5),
                    (Color.FromArgb(0x32, 0xFF, 0xFF, 0xFF), 1.0))
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
            (Color.FromArgb(0x38, 0x0C, 0x16, 0x28), 0.0),
            (Color.FromArgb(0x24, 0x18, 0x28, 0x44), 1.0));
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
        if (mainGrid.Children.Count > 0 && mainGrid.Children[0] is Grid headerGrid)
        {
            headerGrid.Background = isGlass
                ? CreateLinearGradient(
                    new Point(0, 0),
                    new Point(0, 1),
                    (Color.FromArgb(0x48, 0x0A, 0x12, 0x22), 0.0),
                    (Color.FromArgb(0x28, 0x0A, 0x12, 0x22), 1.0))
                : null;
        }

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
                        (Color.FromArgb(0x4A, 0x0C, 0x16, 0x28), 0.0),
                        (Color.FromArgb(0x32, 0x1E, 0x32, 0x54), 1.0));
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

        // Smoked crystal card surface so white text stays readable over any bright wallpaper while remaining translucent
        var cardSurfaceBrush = CreateLinearGradient(
            new Point(0, 0),
            new Point(1, 1),
            (Color.FromArgb(0x52, 0x0C, 0x16, 0x28), 0.0),
            (Color.FromArgb(0x3E, 0x16, 0x26, 0x42), 0.6),
            (Color.FromArgb(0x34, 0x24, 0x3C, 0x64), 1.0));

        var cardSecondaryBrush = CreateLinearGradient(
            new Point(0, 0),
            new Point(1, 1),
            (Color.FromArgb(0x3C, 0x0C, 0x16, 0x28), 0.0),
            (Color.FromArgb(0x28, 0x16, 0x26, 0x42), 1.0));

        var specularBorderBrush = CreateSpecularBorderBrush();

        // Translucent pill controls with smoked crystal backing for crisp contrast
        var controlFillBrush = CreateLinearGradient(
            new Point(0, 0),
            new Point(0, 1),
            (Color.FromArgb(0x54, 0x0E, 0x1A, 0x30), 0.0),
            (Color.FromArgb(0x3C, 0x1C, 0x2E, 0x4E), 1.0));

        var controlHoverBrush = CreateLinearGradient(
            new Point(0, 0),
            new Point(0, 1),
            (Color.FromArgb(0x68, 0x16, 0x28, 0x48), 0.0),
            (Color.FromArgb(0x4E, 0x28, 0x42, 0x6E), 1.0));

        var controlPressedBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x0A, 0x14, 0x26));

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
            (Color.FromArgb(0xD8, 0x0E, 0x18, 0x2C), 0.0),
            (Color.FromArgb(0xCC, 0x08, 0x10, 0x20), 1.0));

        // Page & Layer backgrounds (100% transparent so see-through glass backdrop shines through)
        target["ApplicationPageBackgroundThemeBrush"] = transparentBrush;
        target["LayerFillColorDefaultBrush"] = transparentBrush;
        target["LayerFillColorAltBrush"] = transparentBrush;
        target["LayerOnMicaBaseAltFillColorDefaultBrush"] = transparentBrush;
        target["LayerOnAcrylicFillColorDefaultBrush"] = transparentBrush;
        target["SolidBackgroundFillColorBaseBrush"] = transparentBrush;
        target["SolidBackgroundFillColorSecondaryBrush"] = transparentBrush;
        target["SolidBackgroundFillColorTertiaryBrush"] = cardSecondaryBrush;

        // Cards & Expanders (Settings rows, DownloadRow cards, Quick Actions)
        target["CardBackgroundFillColorDefaultBrush"] = cardSurfaceBrush;
        target["CardBackgroundFillColorSecondaryBrush"] = cardSecondaryBrush;
        target["CardStrokeColorDefaultBrush"] = specularBorderBrush;
        target["CardStrokeColorDefaultSolidBrush"] = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
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
        target["DividerStrokeColorDefaultBrush"] = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));

        // Rounded Apple geometry
        target["ControlCornerRadius"] = new CornerRadius(12);
        target["OverlayCornerRadius"] = new CornerRadius(18);
    }

    private static LinearGradientBrush CreateSpecularBorderBrush()
    {
        return CreateLinearGradient(
            new Point(0, 0),
            new Point(1, 1),
            (Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF), 0.0),
            (Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF), 0.5),
            (Color.FromArgb(0x42, 0x38, 0xBD, 0xF8), 1.0));
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
