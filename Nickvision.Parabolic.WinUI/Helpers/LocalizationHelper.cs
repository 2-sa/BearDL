using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Nickvision.Desktop.Globalization;
using System.Globalization;
using System.Linq;

namespace Nickvision.Parabolic.WinUI.Helpers;

public static class LocalizationHelper
{
    public static StackPanel CreateLabeledInput(FrameworkElement input, string label, FlowDirection direction)
    {
        var heading = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetLabeledBy(input, heading);
        return new StackPanel
        {
            FlowDirection = direction,
            Spacing = 6,
            Children = { heading, input }
        };
    }

    public static void Apply(FrameworkElement element, ITranslationService translator)
    {
        // Match gettext's explicit English, selected catalog, and system-language modes.
        var culture = translator.Language == "C" ? CultureInfo.GetCultureInfo("en-US")
            : string.IsNullOrEmpty(translator.Language) || !translator.AvailableLanguages.Contains(translator.Language)
                ? CultureInfo.CurrentUICulture
                : CultureInfo.GetCultureInfo(translator.Language.Replace('_', '-'));
        element.Language = string.IsNullOrEmpty(culture.Name) ? "en-US" : culture.Name;
        element.FlowDirection = culture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }
}
