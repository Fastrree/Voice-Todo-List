using System.Globalization;

namespace TodoVoiceMaui.Converters;

public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "Tamamlandı" : "Devam Ediyor";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string s && s == "Tamamlandı";
    }
}
