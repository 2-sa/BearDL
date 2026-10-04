using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Nickvision.Parabolic.WinUI.Helpers;

public static class ScrollViewportHelper
{
    public static readonly DependencyProperty ConstrainContentWidthProperty = DependencyProperty.RegisterAttached(
        "ConstrainContentWidth", typeof(bool), typeof(ScrollViewportHelper), new PropertyMetadata(false, OnConstrainContentWidthChanged));

    public static bool GetConstrainContentWidth(DependencyObject element) => (bool)element.GetValue(ConstrainContentWidthProperty);

    public static void SetConstrainContentWidth(DependencyObject element, bool value) => element.SetValue(ConstrainContentWidthProperty, value);

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
        content.Width = width;
        content.MaxWidth = width;
    }
}
