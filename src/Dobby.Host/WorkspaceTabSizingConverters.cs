using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Dobby.Host;

public sealed class WorkspaceTabStripMaxWidthConverter : IMultiValueConverter
{
    private const double FallbackNewTabButtonWidth = 42;
    private const double FallbackNativeCaptionReserveWidth = 144;
    private const double ChromeSpacingAllowance = 18;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var titleBarWidth = ReadPositiveDouble(values, 0);
        if (titleBarWidth <= 0)
        {
            return double.PositiveInfinity;
        }

        var newTabButtonWidth = ReadPositiveDouble(values, 1);
        if (newTabButtonWidth <= 0)
        {
            newTabButtonWidth = FallbackNewTabButtonWidth;
        }

        var captionButtonsWidth = ReadPositiveDouble(values, 2);
        if (captionButtonsWidth <= 0)
        {
            captionButtonsWidth = FallbackNativeCaptionReserveWidth;
        }

        return Math.Max(
            0,
            titleBarWidth - newTabButtonWidth - captionButtonsWidth - ChromeSpacingAllowance);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static double ReadPositiveDouble(IReadOnlyList<object> values, int index)
    {
        return index < values.Count &&
            values[index] is double value &&
            !double.IsNaN(value) &&
            !double.IsInfinity(value) &&
            value > 0
            ? value
            : 0;
    }
}

public sealed class WorkspaceTabWidthConverter : IMultiValueConverter
{
    private const double MaxTabWidth = 240;
    private const double TabHorizontalMargin = 6;
    private const double MinimumReadableTabWidth = 48;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var stripWidth = ReadPositiveDouble(values, 0);
        var tabCount = ReadPositiveInt(values, 1);
        if (stripWidth <= 0 || tabCount <= 0)
        {
            return MaxTabWidth;
        }

        var availablePerTab = (stripWidth / tabCount) - TabHorizontalMargin;
        return Math.Min(MaxTabWidth, Math.Max(MinimumReadableTabWidth, availablePerTab));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static double ReadPositiveDouble(IReadOnlyList<object> values, int index)
    {
        return index < values.Count &&
            values[index] is double value &&
            !double.IsNaN(value) &&
            !double.IsInfinity(value) &&
            value > 0
            ? value
            : 0;
    }

    private static int ReadPositiveInt(IReadOnlyList<object> values, int index)
    {
        return index < values.Count && values[index] is int value && value > 0
            ? value
            : 0;
    }
}
