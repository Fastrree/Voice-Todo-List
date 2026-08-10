using System.Globalization;

namespace TodoVoiceMaui.Converters;

public class BoolToRecordingColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Colors.Red : Colors.Green;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Color c && c == Colors.Red;
    }
}
