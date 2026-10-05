using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Nickvision.Parabolic.WinUI.Helpers;

public static class ToggleSwitchHelper
{
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.RegisterAttached(
        "Direction", typeof(FlowDirection), typeof(ToggleSwitchHelper),
        new PropertyMetadata(FlowDirection.LeftToRight, OnDirectionChanged));

    public static FlowDirection GetDirection(DependencyObject element) => (FlowDirection)element.GetValue(DirectionProperty);

    public static void SetDirection(DependencyObject element, FlowDirection value) => element.SetValue(DirectionProperty, value);

    private static void OnDirectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not ToggleSwitch toggle)
        {
            return;
        }
        // These switches have separate labels and empty on/off content. Mirror
        // the native control, including its animation and drag surface, without
        // reversing text or changing IsOn. Keep template layout LTR to avoid
        // combining implicit RTL layout with this explicit reflection.
        toggle.FlowDirection = FlowDirection.LeftToRight;
        toggle.RenderTransformOrigin = new Point(0.5, 0.5);
        toggle.RenderTransform = new ScaleTransform
        {
            ScaleX = (FlowDirection)args.NewValue == FlowDirection.RightToLeft ? -1 : 1
        };
    }
}
