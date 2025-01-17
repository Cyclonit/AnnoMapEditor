﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AnnoMapEditor.UI.Controls
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    internal sealed class ObjectToVisibility : IValueConverter
    {
        public Visibility OnNull { get; set; }

        public ObjectToVisibility()
        {
            OnNull = Visibility.Collapsed;
        }

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is null ? OnNull : Visibility.Visible;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
