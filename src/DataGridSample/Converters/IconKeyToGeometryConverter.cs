// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DataGridSample.Converters
{
    public class IconKeyToGeometryConverter : IValueConverter
    {
        // Fluent UI System Icons (24px regular) path data.
        private static readonly Geometry FolderGeometry = Geometry.Parse(
            "M3.5 6.25V8H8.12868C8.32759 8 8.51836 7.92098 8.65901 7.78033L10.1893 6.25L8.65901 4.71967C8.51836 4.57902 8.32759 4.5 8.12868 4.5H5.25C4.2835 4.5 3.5 5.2835 3.5 6.25ZM2 6.25C2 4.45507 3.45507 3 5.25 3H8.12868C8.72542 3 9.29771 3.23705 9.71967 3.65901L11.5607 5.5H18.75C20.5449 5.5 22 6.95507 22 8.75V17.75C22 19.5449 20.5449 21 18.75 21H5.25C3.45507 21 2 19.5449 2 17.75V6.25ZM3.5 9.5V17.75C3.5 18.7165 4.2835 19.5 5.25 19.5H18.75C19.7165 19.5 20.5 18.7165 20.5 17.75V8.75C20.5 7.7835 19.7165 7 18.75 7H11.5607L9.71967 8.84099C9.29771 9.26295 8.72542 9.5 8.12868 9.5H3.5Z");

        private static readonly Geometry DocumentGeometry = Geometry.Parse(
            "M6 2C4.89543 2 4 2.89543 4 4V20C4 21.1046 4.89543 22 6 22H18C19.1046 22 20 21.1046 20 20V9.82777C20 9.29733 19.7893 8.78863 19.4142 8.41355L13.5864 2.58579C13.2114 2.21071 12.7027 2 12.1722 2H6ZM5.5 4C5.5 3.72386 5.72386 3.5 6 3.5H12V8C12 9.10457 12.8954 10 14 10H18.5V20C18.5 20.2761 18.2761 20.5 18 20.5H6C5.72386 20.5 5.5 20.2761 5.5 20V4ZM17.3793 8.5H14C13.7239 8.5 13.5 8.27614 13.5 8V4.62066L17.3793 8.5Z");

        private static readonly Geometry CodeGeometry = Geometry.Parse(
            "M8.06562 18.9434L14.5656 4.44339C14.7351 4.06542 15.1788 3.89637 15.5568 4.0658C15.9033 4.22112 16.0742 4.60695 15.9698 4.96131L15.9344 5.05698L9.43438 19.557C9.26495 19.935 8.82118 20.104 8.44321 19.9346C8.09673 19.7793 7.92581 19.3934 8.03024 19.0391L8.06562 18.9434L14.5656 4.44339L8.06562 18.9434ZM2.21967 11.4699L6.46967 7.21986C6.76256 6.92696 7.23744 6.92696 7.53033 7.21986C7.7966 7.48612 7.8208 7.90279 7.60295 8.1964L7.53033 8.28052L3.81066 12.0002L7.53033 15.7199C7.82322 16.0127 7.82322 16.4876 7.53033 16.7805C7.26406 17.0468 6.8474 17.071 6.55379 16.8531L6.46967 16.7805L2.21967 12.5305C1.9534 12.2642 1.9292 11.8476 2.14705 11.554L2.21967 11.4699L6.46967 7.21986L2.21967 11.4699ZM16.4697 7.21986C16.7359 6.95359 17.1526 6.92938 17.4462 7.14724L17.5303 7.21986L21.7803 11.4699C22.0466 11.7361 22.0708 12.1528 21.8529 12.4464L21.7803 12.5305L17.5303 16.7805C17.2374 17.0734 16.7626 17.0734 16.4697 16.7805C16.2034 16.5143 16.1792 16.0976 16.3971 15.804L16.4697 15.7199L20.1893 12.0002L16.4697 8.28052C16.1768 7.98762 16.1768 7.51275 16.4697 7.21986Z");

        private static readonly Dictionary<string, Geometry> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Folder"] = FolderGeometry,
            ["Document"] = DocumentGeometry,
            ["Code"] = CodeGeometry
        };

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            if (value is string key && Map.TryGetValue(key, out var geometry))
            {
                return geometry;
            }

            return DocumentGeometry;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            throw new NotSupportedException();
        }
    }
}
