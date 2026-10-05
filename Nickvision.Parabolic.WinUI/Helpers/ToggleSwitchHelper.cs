using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Nickvision.Parabolic.WinUI.Helpers;

public static class ToggleSwitchHelper
{
    public static void Apply(FlowDirection direction, params ToggleSwitch[] toggles)
    {
        // Use the localized owner's direction directly. ElementName bindings
        // inside ViewStack.Pages depend on the page being attached to a namescope;
        // inactive pages must receive the same direction before they are shown.
        foreach (var toggle in toggles)
        {
            // Labels are separate and OnContent/OffContent are empty. Reflect
            // the native animation and input surface without changing IsOn.
            toggle.FlowDirection = FlowDirection.LeftToRight;
            toggle.RenderTransformOrigin = new Point(0.5, 0.5);
            toggle.RenderTransform = new ScaleTransform
            {
                ScaleX = direction == FlowDirection.RightToLeft ? -1 : 1
            };
        }
    }
}
