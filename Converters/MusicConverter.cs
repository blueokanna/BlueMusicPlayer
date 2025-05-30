using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections;

namespace BlueMusicPlayer.Converters
{
    public class EditModeTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) =>
            (value is bool b && b) ? "Finished" : "Edit";

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) =>
            (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }

    // NEW: Inverted Bool Converter
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) =>
            value is bool b ? !b : true;

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            value is bool b ? !b : false;
    }

    // NEW: Empty String to Visibility Converter
    public class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string str = value?.ToString();
            return !string.IsNullOrEmpty(str) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }

    // NEW: Count to Bool Converter
    public class CountToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int count)
            {
                return count > 0;
            }
            if (value is ICollection collection)
            {
                return collection.Count > 0;
            }
            if (value is IEnumerable enumerable)
            {
                var enumerator = enumerable.GetEnumerator();
                bool hasItems = enumerator.MoveNext();
                if (enumerator is IDisposable disposable)
                    disposable.Dispose();
                return hasItems;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }

    // NEW: Multiple Bool to Visibility Converter (alternative to MultiBinding)
    public class MultipleBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // This converter expects a comma-separated string of boolean values
            // Usage: {Binding Path=DataContext, Converter={StaticResource MultipleBoolToVisibilityConverter}, ConverterParameter='IsNotLoading,HasSongs'}
            if (parameter is string parameterString && value != null)
            {
                string[] properties = parameterString.Split(',');
                var dataContext = value;

                foreach (string property in properties)
                {
                    var propertyInfo = dataContext.GetType().GetProperty(property.Trim());
                    if (propertyInfo != null)
                    {
                        var propertyValue = propertyInfo.GetValue(dataContext);
                        if (propertyValue is bool boolValue && !boolValue)
                        {
                            return Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        return Visibility.Collapsed;
                    }
                }
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }

    // String to Visibility Converter
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter != null)
            {
                string paramStr = parameter.ToString();
                string valueStr = value?.ToString() ?? string.Empty;
                return valueStr.Equals(paramStr, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            return !string.IsNullOrEmpty(value?.ToString())
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }

    // Inverted String to Visibility Converter
    public class InvertedStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return string.IsNullOrEmpty(value?.ToString())
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }

    public class TrackInCollectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var selected = value as System.Collections.ObjectModel.ObservableCollection<Models.Track>;
            var track = parameter as Models.Track;
            return selected != null && track != null && selected.Contains(track);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }

    public class PlayPauseGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) =>
            (value is bool b && b) ? "\uE769" /*Pause*/ : "\uE768" /*Play*/;

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }

    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b && b)
                return 1.0;
            return 0.5;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }

    public class BoolToAccentBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool flag = value is bool b && b;
            string key = flag
                ? "SystemAccentColorBrush"
                : "TextFillColorSecondaryBrush";

            if (Application.Current.Resources.TryGetValue(key, out var brush)
                && brush is SolidColorBrush scb)
            {
                return scb;
            }

            return Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue && boolValue)
            {
                return new Thickness(2);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || (value is string str && string.IsNullOrEmpty(str)))
            {
                return 1.0;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class EnhancedBoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return boolValue ? 1.0 : 0.3;
            }

            if (value == null || (value is string str && string.IsNullOrEmpty(str)))
            {
                return 1.0;
            }

            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class LoginStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isLoggedIn && isLoggedIn)
            {
                return "Logout";
            }
            return "Login";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class InvertedBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) =>
            (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }

    public class ObjectToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class CollectionCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ICollection collection)
            {
                return collection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Collapsed;
            }
            return true;
        }
    }

    // Helper converter for combining loading state and song count visibility
    public class LoadingAndCountVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // This expects the DataContext to be passed
            if (value != null)
            {
                var dataContext = value;
                var type = dataContext.GetType();

                // Get IsNetEaseLoading property
                var loadingProperty = type.GetProperty("IsNetEaseLoading");
                var isLoading = loadingProperty?.GetValue(dataContext) as bool? ?? true;

                // Get FilteredNetEaseSongs.Count property
                var songsProperty = type.GetProperty("FilteredNetEaseSongs");
                var songs = songsProperty?.GetValue(dataContext);
                var songsCount = 0;

                if (songs is ICollection collection)
                {
                    songsCount = collection.Count;
                }

                // Show if not loading AND has songs
                return (!isLoading && songsCount > 0) ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }
    public class BoolToLoginStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isLoggedIn && parameter is string parameterString)
            {
                var parts = parameterString.Split('|');
                if (parts.Length == 2)
                {
                    return isLoggedIn ? parts[0] : parts[1];
                }
            }
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}