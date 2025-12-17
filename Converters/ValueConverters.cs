using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SuperSMPLauncher.Converters
{
    public class StringIsNullOrEmptyConverter : IValueConverter
    {
        public static StringIsNullOrEmptyConverter Instance { get; } = new StringIsNullOrEmptyConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return string.Empty;
            }
            return "1.20.1";
        }
    }

    public class BoolToTextConverter : IValueConverter
    {
        public static BoolToTextConverter Instance { get; } = new BoolToTextConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDownloading)
            {
                return isDownloading ? "⏳ Lade herunter..." : "📥 Modpack herunterladen";
            }
            return "📥 Modpack herunterladen";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }
}