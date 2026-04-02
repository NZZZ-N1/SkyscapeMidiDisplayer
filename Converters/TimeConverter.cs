using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SkyscapeMidiDisplayer.Converters;

public class TimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double milliseconds)
        {
            var timeSpan = TimeSpan.FromMilliseconds(milliseconds);
            return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
        return "00:00";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
