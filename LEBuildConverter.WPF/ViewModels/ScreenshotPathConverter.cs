using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace LEBuildConverter.WPF.ViewModels;

/// <summary>
/// Converts a simple filename like "step01_open_maxroll.png" into a
/// BitmapImage from the embedded Assets/screenshots resource bundle.
/// Returns null if the file is missing so the Image control can fall
/// back to a placeholder.
/// </summary>
public class ScreenshotPathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string filename || string.IsNullOrWhiteSpace(filename))
            return null;

        try
        {
            var uri = new Uri(
                $"pack://application:,,,/Assets/screenshots/{filename}",
                UriKind.Absolute);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
