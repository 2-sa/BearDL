using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Nickvision.Parabolic.WinUI.Helpers;

public static class ScrollViewportHelper
{
    public static readonly DependencyProperty ConstrainContentWidthProperty = DependencyProperty.RegisterAttached(
        "ConstrainContentWidth", typeof(bool), typeof(ScrollViewportHelper), new PropertyMetadata(false, OnConstrainContentWidthChanged));

    public static readonly DependencyProperty ReserveScrollbarRightProperty = DependencyProperty.RegisterAttached(
        "ReserveScrollbarRight", typeof(double), typeof(ScrollViewportHelper), new PropertyMetadata(0.0, OnReserveScrollbarRightChanged));

    public static bool GetConstrainContentWidth(DependencyObject element) => (bool)element.GetValue(ConstrainContentWidthProperty);

    public static void SetConstrainContentWidth(DependencyObject element, bool value) => element.SetValue(ConstrainContentWidthProperty, value);

    public static double GetReserveScrollbarRight(DependencyObject element) => (double)element.GetValue(ReserveScrollbarRightProperty);

    public static void SetReserveScrollbarRight(DependencyObject element, double value) => element.SetValue(ReserveScrollbarRightProperty, value);

    private static void OnReserveScrollbarRightChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }
        element.Loaded -= OnReserveLoaded;
        element.Loaded += OnReserveLoaded;
        element.RegisterPropertyChangedCallback(FrameworkElement.FlowDirectionProperty, (_, _) => UpdateReservedMargin(element));
        UpdateReservedMargin(element);
    }

    private static void OnReserveLoaded(object sender, RoutedEventArgs args) => UpdateReservedMargin((FrameworkElement)sender);

    private static void UpdateReservedMargin(FrameworkElement element)
    {
        var reserve = GetReserveScrollbarRight(element);
        if (reserve <= 0)
        {
            return;
        }
        var baseHorizontal = Math.Min(element.Margin.Left, element.Margin.Right);
        var isRtl = element.FlowDirection == FlowDirection.RightToLeft;
        var targetLeft = isRtl ? baseHorizontal + reserve : baseHorizontal;
        var targetRight = isRtl ? baseHorizontal : baseHorizontal + reserve;
        var desired = new Thickness(targetLeft, element.Margin.Top, targetRight, element.Margin.Bottom);
        if (element.Margin != desired)
        {
            element.Margin = desired;
        }
    }

    private static void OnConstrainContentWidthChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }
        if ((bool)args.NewValue)
        {
            scrollViewer.Loaded += OnLoaded;
            scrollViewer.SizeChanged += OnSizeChanged;
            UpdateWidth(scrollViewer);
        }
        else
        {
            scrollViewer.Loaded -= OnLoaded;
            scrollViewer.SizeChanged -= OnSizeChanged;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs args) => UpdateWidth((ScrollViewer)sender);

    private static void OnSizeChanged(object sender, SizeChangedEventArgs args) => UpdateWidth((ScrollViewer)sender);

    private static void UpdateWidth(ScrollViewer scrollViewer)
    {
        if (scrollViewer.Content is not FrameworkElement content || scrollViewer.ActualWidth <= 0)
        {
            return;
        }
        // A direction change inside ScrollViewer can leave RTL content arranged
        // at its desired width, beyond the clipped viewport. Use the measured
        // viewport, including its reserved scrollbar space and content margins.
        var width = Math.Max(0, scrollViewer.ActualWidth - scrollViewer.Padding.Left - scrollViewer.Padding.Right
            - content.Margin.Left - content.Margin.Right);
        if (content.Width != width)
        {
            content.Width = width;
        }
        if (content.MaxWidth != width)
        {
            content.MaxWidth = width;
        }
    }
}
