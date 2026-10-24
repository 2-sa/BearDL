using CommunityToolkit.WinUI.Controls;
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

    public static void ApplyExpanders(FlowDirection direction, params SettingsExpander[] expanders)
    {
        if (direction != FlowDirection.RightToLeft)
        {
            return;
        }
        foreach (var expander in expanders)
        {
            expander.Loaded -= OnExpanderLoaded;
            expander.Loaded += OnExpanderLoaded;
            SwapExpanderChevronColumn(expander);
        }
    }

    private static void OnExpanderLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is SettingsExpander expander)
        {
            SwapExpanderChevronColumn(expander);
        }
    }

    private static void SwapExpanderChevronColumn(DependencyObject root)
    {
        // SettingsExpander's ExpanderHeader ToggleButton uses ToggleButtonGrid with
        // Column 0 (*) for ContentPresenter (the inner SettingsCard) and Column 1 (Auto)
        // for ExpandCollapseChevronBorder. In RTL, Column 1 is at the far left edge,
        // placing the chevron to the left of the ToggleSwitch instead of its right.
        // Moving ExpandCollapseChevronBorder into the inner SettingsCard's PART_RootGrid
        // Column 2 and shifting PART_ContentPresenter (the ToggleSwitch) to Column 3
        // places the ToggleSwitch at the far left edge (aligned with all SettingsCards)
        // and the expand chevron immediately to its right.
        var toggleButtonGrid = FindDescendant<Grid>(root, "ToggleButtonGrid");
        var chevronBorder = FindDescendant<FrameworkElement>(root, "ExpandCollapseChevronBorder");
        var cardRootGrid = FindDescendant<Grid>(root, "PART_RootGrid");
        var cardContentPresenter = FindDescendant<FrameworkElement>(root, "PART_ContentPresenter");
        if (toggleButtonGrid is null || chevronBorder is null || cardRootGrid is null || cardContentPresenter is null)
        {
            return;
        }
        if (VisualTreeHelper.GetParent(chevronBorder) == toggleButtonGrid)
        {
            if (VisualTreeHelper.GetParent(cardRootGrid) is SettingsCard headerCard)
            {
                headerCard.Padding = new Thickness(16, 16, 16, 16);
            }
            toggleButtonGrid.Children.Remove(chevronBorder);
            Grid.SetColumn(chevronBorder, 2);
            chevronBorder.Margin = new Thickness(0, 0, 8, 0);
            cardRootGrid.Children.Add(chevronBorder);
            Grid.SetColumn(cardContentPresenter, 3);
        }
    }

    private static T? FindDescendant<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && match.Name == name)
            {
                return match;
            }
            var nested = FindDescendant<T>(child, name);
            if (nested is not null)
            {
                return nested;
            }
        }
        return null;
    }
}
