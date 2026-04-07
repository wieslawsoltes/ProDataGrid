using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Diagnostics.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Converters;
using Avalonia.Media;
using Avalonia.Reactive;

namespace Avalonia.Diagnostics.Services
{
    internal sealed class PropertyValueEditorService
    {
        private static readonly Geometry ImageIcon = Geometry.Parse(
            "M12.25 6C8.79822 6 6 8.79822 6 12.25V35.75C6 37.1059 6.43174 38.3609 7.16525 39.3851L21.5252 25.0251C22.8921 23.6583 25.1081 23.6583 26.475 25.0251L40.8348 39.385C41.5683 38.3608 42 37.1058 42 35.75V12.25C42 8.79822 39.2018 6 35.75 6H12.25ZM34.5 17.5C34.5 19.7091 32.7091 21.5 30.5 21.5C28.2909 21.5 26.5 19.7091 26.5 17.5C26.5 15.2909 28.2909 13.5 30.5 13.5C32.7091 13.5 34.5 15.2909 34.5 17.5ZM39.0024 41.0881L24.7072 26.7929C24.3167 26.4024 23.6835 26.4024 23.293 26.7929L8.99769 41.0882C9.94516 41.6667 11.0587 42 12.25 42H35.75C36.9414 42 38.0549 41.6666 39.0024 41.0881Z");

        private static readonly Geometry GeometryIcon = Geometry.Parse(
            "M23.25 15.5H30.8529C29.8865 8.99258 24.2763 4 17.5 4C10.0442 4 4 10.0442 4 17.5C4 24.2763 8.99258 29.8865 15.5 30.8529V23.25C15.5 18.9698 18.9698 15.5 23.25 15.5ZM23.25 18C20.3505 18 18 20.3505 18 23.25V38.75C18 41.6495 20.3505 44 23.25 44H38.75C41.6495 44 44 41.6495 44 38.75V23.25C44 20.3505 41.6495 18 38.75 18H23.25Z");

        private static readonly ColorToBrushConverter Color2Brush = new();

        private readonly CompositeDisposable _subscriptions = new();
        private readonly Dictionary<PropertyValueEditorKind, EditorEntry> _editorCache = new();

        public Control GetOrCreateEditor(PropertyViewModel viewModel, Type propertyType)
        {
            var kind = TryGetRemoteEditorKind(viewModel, out var remoteKind)
                ? remoteKind
                : PropertyValueEditorTypeHelper.GetEditorKind(propertyType);
            if (!_editorCache.TryGetValue(kind, out var entry))
            {
                entry = CreateEditorEntry(kind);
                _editorCache.Add(kind, entry);
            }

            entry.Update(viewModel, propertyType);
            return entry.Control;
        }

        private EditorEntry CreateEditorEntry(PropertyValueEditorKind kind)
        {
            switch (kind)
            {
                case PropertyValueEditorKind.Boolean:
                    {
                        var check = new CheckBox();
                        var commitState = new EditorCommitState(check);
                        BindValueControl(check, ToggleButton.IsCheckedProperty, new ValueConverter(), typeof(bool), commitState);
                        return new EditorEntry(check, (vm, _) =>
                        {
                            commitState.UpdateContext(vm);
                            check.IsEnabled = !vm.IsReadonly;
                        });
                    }
                case PropertyValueEditorKind.Numeric:
                    {
                        var numeric = new NumericUpDown
                        {
                            Increment = 1,
                            NumberFormat = new NumberFormatInfo { NumberDecimalDigits = 0 },
                            ParsingNumberStyle = NumberStyles.Integer
                        };
                        var commitState = new EditorCommitState(numeric);
                        var bindingSlot = new ValueBindingSlot(
                            numeric,
                            NumericUpDown.ValueProperty,
                            new GuardedValueConverter(new ValueToDecimalConverter(), () => commitState.CanCommit));
                        Type? boundType = null;
                        return new EditorEntry(numeric, (vm, type) =>
                        {
                            commitState.UpdateContext(vm);
                            if (!ReferenceEquals(boundType, type))
                            {
                                bindingSlot.Bind(type);
                                boundType = type;
                            }

                            numeric.IsReadOnly = vm.IsReadonly;
                        });
                    }
                case PropertyValueEditorKind.Color:
                    {
                        var swatch = new Ellipse { Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center };
                        swatch.Bind(
                                Shape.FillProperty,
                                new Binding(nameof(PropertyViewModel.Value)) { Converter = Color2Brush })
                            .DisposeWith(_subscriptions);

                        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
                        label.Bind(
                                TextBlock.TextProperty,
                                new Binding(nameof(PropertyViewModel.Value)))
                            .DisposeWith(_subscriptions);

                        var host = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 2,
                            Children = { swatch, label },
                            Background = Brushes.Transparent,
                            Cursor = new Cursor(StandardCursorType.Hand)
                        };

                        var flyout = new Flyout();
                        ColorView? picker = null;

                        void EnsurePicker()
                        {
                            if (picker != null)
                            {
                                return;
                            }

                            picker = new ColorView
                            {
                                HexInputAlphaPosition = AlphaComponentPosition.Leading,
                            };

                            picker.Bind(
                                    ColorView.ColorProperty,
                                    new Binding(nameof(PropertyViewModel.Value), BindingMode.TwoWay) { Converter = Color2Brush })
                                .DisposeWith(_subscriptions);

                            flyout.Content = picker;
                        }

                        FlyoutBase.SetAttachedFlyout(host, flyout);
                        host.PointerPressed += (_, _) =>
                        {
                            if (!host.IsEnabled)
                            {
                                return;
                            }

                            EnsurePicker();
                            FlyoutBase.ShowAttachedFlyout(host);
                        };

                        return new EditorEntry(host, (vm, _) => host.IsEnabled = !vm.IsReadonly);
                    }
                case PropertyValueEditorKind.Brush:
                    {
                        var brushEditor = new BrushEditor();
                        var commitState = new EditorCommitState(brushEditor);
                        BindValueControl(brushEditor, BrushEditor.BrushProperty, new ValueConverter(), typeof(IBrush), commitState);
                        return new EditorEntry(brushEditor, (vm, _) =>
                        {
                            commitState.UpdateContext(vm);
                            brushEditor.IsEnabled = !vm.IsReadonly;
                        });
                    }
                case PropertyValueEditorKind.Image:
                case PropertyValueEditorKind.Geometry:
                    {
                        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
                        label.Bind(TextBlock.TextProperty, new Binding(nameof(PropertyViewModel.Value)) { Converter = ValueToSizeTextConverter.Instance })
                            .DisposeWith(_subscriptions);

                        var icon = new Path
                        {
                            Data = kind == PropertyValueEditorKind.Image ? ImageIcon : GeometryIcon,
                            Fill = Brushes.Gray,
                            Width = 12,
                            Height = 12,
                            Stretch = Stretch.Uniform,
                            VerticalAlignment = VerticalAlignment.Center
                        };

                        var host = new StackPanel
                        {
                            Background = Brushes.Transparent,
                            Orientation = Orientation.Horizontal,
                            Spacing = 2,
                            Children = { icon, label }
                        };

                        if (kind == PropertyValueEditorKind.Image)
                        {
                            var previewImage = new Image { Stretch = Stretch.Uniform, Width = 300, Height = 300 };
                            previewImage.Bind(Image.SourceProperty, new Binding(nameof(PropertyViewModel.Value)))
                                .DisposeWith(_subscriptions);
                            ToolTip.SetTip(host, previewImage);
                        }
                        else
                        {
                            var previewShape = new Path
                            {
                                Stretch = Stretch.Uniform,
                                Fill = Brushes.White,
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Center
                            };
                            previewShape.Bind(Path.DataProperty, new Binding(nameof(PropertyViewModel.Value)))
                                .DisposeWith(_subscriptions);
                            ToolTip.SetTip(host, new Border { Child = previewShape, Width = 300, Height = 300 });
                        }

                        return new EditorEntry(host, (_, _) => { });
                    }
                case PropertyValueEditorKind.Enum:
                    {
                        var combo = new ComboBox();
                        var commitState = new EditorCommitState(combo);
                        var bindingSlot = new ValueBindingSlot(
                            combo,
                            SelectingItemsControl.SelectedItemProperty,
                            new GuardedValueConverter(new ValueConverter(), () => commitState.CanCommit));
                        Type? boundType = null;
                        IReadOnlyList<string>? boundRemoteOptions = null;
                        return new EditorEntry(combo, (vm, type) =>
                        {
                            commitState.UpdateContext(vm);
                            var remoteOptions = vm is RemotePropertyViewModel remoteViewModel &&
                                                remoteViewModel.EnumOptions.Count > 0 &&
                                                !PropertyValueEditorTypeHelper.TryGetEnumType(type, out _)
                                ? remoteViewModel.EnumOptions
                                : null;
                            if (!ReferenceEquals(boundType, type) || !ReferenceEquals(boundRemoteOptions, remoteOptions))
                            {
                                if (remoteOptions is not null)
                                {
                                    combo.ItemsSource = remoteOptions;
                                }
                                else
                                {
                                    var enumType = PropertyValueEditorTypeHelper.TryGetEnumType(type, out var resolved) ? resolved : type;
                                    combo.ItemsSource = PropertyValueEditorTypeHelper.GetEnumValues(enumType);
                                }

                                bindingSlot.Bind(type);
                                boundType = type;
                                boundRemoteOptions = remoteOptions;
                            }

                            combo.IsEnabled = !vm.IsReadonly;
                        });
                    }
                default:
                    {
                        var text = new CommitTextBox { Watermark = "(null)" };
                        var bindingSlot = new ValueBindingSlot(
                            text,
                            CommitTextBox.CommittedTextProperty,
                            new TextToValueConverter(),
                            UpdateSourceTrigger.Explicit);
                        var state = new TextEditorState();
                        Type? boundType = null;

                        Exception? ValidateText(string? input)
                        {
                            if (!state.CanEdit)
                            {
                                return null;
                            }

                            if (input == null)
                            {
                                return null;
                            }

                            try
                            {
                                var parsed = PropertyValueEditorStringConversion.FromString(input, state.PropertyType);
                                if (!PropertyValueEditorValidation.TryValidateParsedValue(state.ViewModel, state.PropertyType, parsed, out var error))
                                {
                                    return error;
                                }
                            }
                            catch (Exception ex)
                            {
                                return ex.GetBaseException();
                            }

                            return null;
                        }

                        text.CommitValidator = ValidateText;

                        text.GetObservable(TextBox.TextProperty).Subscribe(t =>
                        {
                            if (!state.CanEdit || text.IsReadOnly)
                            {
                                // Only validate active edits to avoid heavy parsing during scroll.
                                DataValidationErrors.ClearErrors(text);
                                return;
                            }

                            var hasErrors = DataValidationErrors.GetHasErrors(text);
                            if (!text.IsKeyboardFocusWithin && !hasErrors)
                            {
                                return;
                            }

                            var error = ValidateText(t);
                            if (error != null)
                            {
                                DataValidationErrors.SetError(text, error);
                                return;
                            }

                            DataValidationErrors.ClearErrors(text);
                        }).DisposeWith(_subscriptions);

                        return new EditorEntry(text, (vm, type) =>
                        {
                            var viewModelChanged = !ReferenceEquals(state.ViewModel, vm);
                            if (!ReferenceEquals(boundType, type))
                            {
                                bindingSlot.Bind(type);
                                boundType = type;
                            }

                            state.ViewModel = vm;
                            state.PropertyType = type;
                            state.CanEdit = type != typeof(object) && PropertyValueEditorStringConversion.CanConvertFromString(type);
                            text.IsReadOnly = vm.IsReadonly || !state.CanEdit;
                            if (viewModelChanged || !state.CanEdit || text.IsReadOnly)
                            {
                                DataValidationErrors.ClearErrors(text);
                            }
                        });
                    }
            }
        }

        private static bool TryGetRemoteEditorKind(PropertyViewModel viewModel, out PropertyValueEditorKind kind)
        {
            if (viewModel is RemotePropertyViewModel remoteViewModel)
            {
                switch (remoteViewModel.EditorKindToken)
                {
                    case "boolean":
                        kind = PropertyValueEditorKind.Boolean;
                        return true;
                    case "numeric":
                        kind = PropertyValueEditorKind.Numeric;
                        return true;
                    case "color":
                        kind = PropertyValueEditorKind.Color;
                        return true;
                    case "brush":
                        kind = PropertyValueEditorKind.Brush;
                        return true;
                    case "image":
                        kind = PropertyValueEditorKind.Image;
                        return true;
                    case "geometry":
                        kind = PropertyValueEditorKind.Geometry;
                        return true;
                    case "enum":
                        kind = PropertyValueEditorKind.Enum;
                        return true;
                }
            }

            kind = default;
            return false;
        }

        private void BindValueControl(
            Control control,
            AvaloniaProperty valueProperty,
            IValueConverter converter,
            Type propertyType,
            EditorCommitState? commitState = null)
        {
            if (commitState != null)
            {
                converter = new GuardedValueConverter(converter, () => commitState.CanCommit);
            }

            control.Bind(valueProperty,
                    new Binding(nameof(PropertyViewModel.Value), BindingMode.TwoWay)
                    {
                        Converter = converter,
                        ConverterParameter = propertyType
                    })
                .DisposeWith(_subscriptions);
        }

        private sealed class EditorEntry
        {
            private readonly Action<PropertyViewModel, Type> _update;

            public EditorEntry(Control control, Action<PropertyViewModel, Type> update)
            {
                Control = control;
                _update = update;
            }

            public Control Control { get; }

            public void Update(PropertyViewModel viewModel, Type propertyType)
            {
                _update(viewModel, propertyType);
            }
        }

        private sealed class ValueBindingSlot
        {
            private readonly Control _control;
            private readonly AvaloniaProperty _property;
            private readonly IValueConverter _converter;
            private readonly UpdateSourceTrigger? _updateSourceTrigger;
            private IDisposable? _binding;

            public ValueBindingSlot(Control control, AvaloniaProperty property, IValueConverter converter, UpdateSourceTrigger? updateSourceTrigger = null)
            {
                _control = control;
                _property = property;
                _converter = converter;
                _updateSourceTrigger = updateSourceTrigger;
            }

            public void Bind(Type propertyType)
            {
                _binding?.Dispose();
                var binding = new Binding(nameof(PropertyViewModel.Value), BindingMode.TwoWay)
                {
                    Converter = _converter,
                    ConverterParameter = propertyType
                };

                if (_updateSourceTrigger.HasValue)
                {
                    binding.UpdateSourceTrigger = _updateSourceTrigger.Value;
                }

                _binding = _control.Bind(_property, binding);
            }
        }

        private sealed class TextEditorState
        {
            public PropertyViewModel? ViewModel { get; set; }

            public Type PropertyType { get; set; } = typeof(object);

            public bool CanEdit { get; set; }
        }

        private sealed class EditorCommitState
        {
            private static readonly object StaleContext = new();
            private object? _editContext;
            private bool _isActive;

            public EditorCommitState(Control control)
            {
                control.GotFocus += (_, _) => Begin(control.DataContext);
                control.LostFocus += (_, _) => End();
                control.AddHandler(InputElement.PointerPressedEvent, (_, _) => Begin(control.DataContext), RoutingStrategies.Tunnel);
                control.AddHandler(InputElement.PointerWheelChangedEvent, (_, _) => Begin(control.DataContext), RoutingStrategies.Tunnel);
                control.AddHandler(InputElement.KeyDownEvent, (_, _) => Begin(control.DataContext), RoutingStrategies.Tunnel);
            }

            public bool CanCommit => _isActive && !ReferenceEquals(_editContext, StaleContext);

            public void UpdateContext(object? context)
            {
                if (_isActive && _editContext != null && !ReferenceEquals(_editContext, context))
                {
                    _editContext = StaleContext;
                }
            }

            private void Begin(object? context)
            {
                _isActive = true;
                _editContext = context;
            }

            private void End()
            {
                _isActive = false;
                _editContext = null;
            }
        }

        private sealed class ValueToSizeTextConverter : IValueConverter
        {
            public static ValueToSizeTextConverter Instance { get; } = new ValueToSizeTextConverter();

            public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                return value switch
                {
                    IImage img => $"{img.Size.Width} x {img.Size.Height}",
                    Geometry geom => $"{geom.Bounds.Width} x {geom.Bounds.Height}",
                    _ => "(null)"
                };
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                return BindingOperations.DoNothing;
            }
        }

        private class ValueConverter : IValueConverter
        {
            object? IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                return Convert(value, targetType, parameter, culture);
            }

            object? IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                //Note: targetType provided by Converter is simply "object"
                return ConvertBack(value, (Type)parameter!, parameter, culture);
            }

            protected virtual object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                return value;
            }

            protected virtual object? ConvertBack(object? value, Type targetType, object? parameter,
                CultureInfo culture)
            {
                return value;
            }
        }

        private sealed class GuardedValueConverter : IValueConverter
        {
            private readonly IValueConverter _inner;
            private readonly Func<bool> _canCommit;

            public GuardedValueConverter(IValueConverter inner, Func<bool> canCommit)
            {
                _inner = inner;
                _canCommit = canCommit;
            }

            public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                return _inner.Convert(value, targetType, parameter, culture);
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                if (!_canCommit())
                {
                    return BindingOperations.DoNothing;
                }

                return _inner.ConvertBack(value, targetType, parameter, culture);
            }
        }

        private sealed class ValueToDecimalConverter : ValueConverter
        {
            protected override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                return System.Convert.ToDecimal(value);
            }

            protected override object? ConvertBack(object? value, Type targetType, object? parameter,
                CultureInfo culture)
            {
                if (value is null)
                {
                    return null;
                }

                var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;

                if (nonNullable.IsEnum)
                {
                    return value is string text
                        ? Enum.Parse(nonNullable, text)
                        : Enum.ToObject(nonNullable, value);
                }

                return System.Convert.ChangeType(value, nonNullable, culture);
            }
        }

        private sealed class TextToValueConverter : ValueConverter
        {
            protected override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                return value is null ? null : PropertyValueEditorStringConversion.ToString(value);
            }

            protected override object? ConvertBack(object? value, Type targetType, object? parameter,
                CultureInfo culture)
            {
                if (value is not string s)
                    return null;

                try
                {
                    return PropertyValueEditorStringConversion.FromString(s, targetType);
                }
                catch
                {
                    return BindingOperations.DoNothing;
                }
            }
        }
    }

    internal enum PropertyValueEditorKind
    {
        Boolean,
        Numeric,
        Color,
        Brush,
        Image,
        Geometry,
        Enum,
        Text
    }

    internal static class PropertyValueEditorTypeHelper
    {
        private static readonly object EditorKindCacheLock = new();
        private static readonly Dictionary<Type, PropertyValueEditorKind> EditorKindCache = new();
        private static readonly object EnumValuesCacheLock = new();
        private static readonly Dictionary<Type, Array> EnumValuesCache = new();

        internal static PropertyValueEditorKind GetEditorKind(Type propertyType)
        {
            lock (EditorKindCacheLock)
            {
                if (EditorKindCache.TryGetValue(propertyType, out var cachedKind))
                {
                    return cachedKind;
                }
            }

            var kind = ComputeEditorKind(propertyType);

            lock (EditorKindCacheLock)
            {
                EditorKindCache[propertyType] = kind;
            }

            return kind;
        }

        internal static bool TryGetEnumType(Type type, out Type enumType)
        {
            if (type.IsEnum)
            {
                enumType = type;
                return true;
            }

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null && underlying.IsEnum)
            {
                enumType = underlying;
                return true;
            }

            enumType = type;
            return false;
        }

        internal static Array GetEnumValues(Type enumType)
        {
            lock (EnumValuesCacheLock)
            {
                if (!EnumValuesCache.TryGetValue(enumType, out var values))
                {
                    values = Enum.GetValues(enumType);
                    EnumValuesCache[enumType] = values;
                }

                return values;
            }
        }

        private static PropertyValueEditorKind ComputeEditorKind(Type propertyType)
        {
            if (propertyType == typeof(bool))
            {
                return PropertyValueEditorKind.Boolean;
            }

            if (TryGetEnumType(propertyType, out _))
            {
                return PropertyValueEditorKind.Enum;
            }

            if (IsValidNumeric(propertyType))
            {
                return PropertyValueEditorKind.Numeric;
            }

            if (propertyType == typeof(Color))
            {
                return PropertyValueEditorKind.Color;
            }

            if (ImplementsInterface<IBrush>(propertyType))
            {
                return PropertyValueEditorKind.Brush;
            }

            if (ImplementsInterface<IImage>(propertyType))
            {
                return PropertyValueEditorKind.Image;
            }

            if (propertyType == typeof(Geometry))
            {
                return PropertyValueEditorKind.Geometry;
            }

            return PropertyValueEditorKind.Text;
        }

        private static bool IsValidNumeric(Type? type)
        {
            if (type == null || type.IsEnum == true || IsNullableEnum(type))
            {
                return false;
            }

            var typeCode = Type.GetTypeCode(type);
            if (typeCode == TypeCode.Object)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    typeCode = Type.GetTypeCode(Nullable.GetUnderlyingType(type));
                }
                else
                {
                    return false;
                }
            }

            switch (typeCode)
            {
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsNullableEnum(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type);
            return underlying != null && underlying.IsEnum;
        }

        private static bool ImplementsInterface<TInterface>(Type type)
        {
            var interfaceType = typeof(TInterface);
            return type == interfaceType || interfaceType.IsAssignableFrom(type);
        }
    }

    internal static class PropertyValueEditorValidation
    {
        internal static bool TryValidateParsedValue(PropertyViewModel? viewModel, Type propertyType, object? value, out Exception? error)
        {
            error = null;
            if (viewModel is AvaloniaPropertyViewModel avaloniaPropertyViewModel)
            {
                try
                {
                    if (!avaloniaPropertyViewModel.Property.IsValidValue(value))
                    {
                        error = new ArgumentException("Value is not valid for the selected property.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                    return false;
                }
            }
            else if (viewModel is ClrPropertyViewModel)
            {
                if (!TryValidateClrValue(propertyType, value, out error))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryValidateClrValue(Type propertyType, object? value, out Exception? error)
        {
            error = null;
            if (value == null)
            {
                if (propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) == null)
                {
                    error = new ArgumentNullException(nameof(value), "Value is not valid for the selected property.");
                    return false;
                }

                return true;
            }

            var nonNullable = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (nonNullable.IsInstanceOfType(value))
            {
                return true;
            }

            if (nonNullable.IsEnum)
            {
                try
                {
                    if (value is string text)
                    {
                        Enum.Parse(nonNullable, text);
                        return true;
                    }

                    Enum.ToObject(nonNullable, value);
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.GetBaseException();
                    return false;
                }
            }

            if (value is IConvertible)
            {
                try
                {
                    System.Convert.ChangeType(value, nonNullable, CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.GetBaseException();
                    return false;
                }
            }

            error = new ArgumentException("Value is not valid for the selected property.");
            return false;
        }
    }

    internal static class PropertyValueEditorStringConversion
    {
        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
        private static readonly Type[] StringParameter = { typeof(string) };
        private static readonly Type[] StringFormatProviderParameters = { typeof(string), typeof(IFormatProvider) };
        private static readonly object CanConvertCacheLock = new();
        private static readonly Dictionary<Type, bool> CanConvertCache = new();
        private static readonly object ParseMethodCacheLock = new();
        private static readonly Dictionary<Type, ParseMethodCacheEntry> ParseMethodCache = new();

        public static bool CanConvertFromString(Type type)
        {
            lock (CanConvertCacheLock)
            {
                if (CanConvertCache.TryGetValue(type, out var cached))
                {
                    return cached;
                }
            }

            var converter = TypeDescriptor.GetConverter(type);

            var canConvert = converter.CanConvertFrom(typeof(string)) || GetParseMethod(type, out _) != null;

            lock (CanConvertCacheLock)
            {
                CanConvertCache[type] = canConvert;
            }

            return canConvert;
        }

        public static string? ToString(object o)
        {
            var converter = TypeDescriptor.GetConverter(o);

            // CollectionConverter does not deliver any important information. It just displays "(Collection)".
            if (!converter.CanConvertTo(typeof(string)) ||
                converter.GetType() == typeof(CollectionConverter))
            {
                return o.ToString();
            }

            return converter.ConvertToInvariantString(o);
        }

        public static object? FromString(string str, Type type)
        {
            var converter = TypeDescriptor.GetConverter(type);

            return converter.CanConvertFrom(typeof(string))
                ? converter.ConvertFrom(null, CultureInfo.InvariantCulture, str)
                : InvokeParse(str, type);
        }

        private static object? InvokeParse(string s, Type targetType)
        {
            var m = GetParseMethod(targetType, out var hasFormat);

            if (m == null)
            {
                throw new InvalidOperationException();
            }

            return m.Invoke(null,
                hasFormat
                    ? new object[] { s, CultureInfo.InvariantCulture }
                    : new object[] { s });
        }

        private static MethodInfo? GetParseMethod(Type type, out bool hasFormat)
        {
            lock (ParseMethodCacheLock)
            {
                if (ParseMethodCache.TryGetValue(type, out var cached))
                {
                    hasFormat = cached.HasFormat;
                    return cached.Method;
                }
            }

            var m = type.GetMethod("Parse", PublicStatic, null, StringFormatProviderParameters, null);
            if (m != null)
            {
                hasFormat = true;
            }
            else
            {
                hasFormat = false;
                m = type.GetMethod("Parse", PublicStatic, null, StringParameter, null);
            }

            lock (ParseMethodCacheLock)
            {
                ParseMethodCache[type] = new ParseMethodCacheEntry(m, hasFormat);
            }

            return m;
        }

        private readonly struct ParseMethodCacheEntry
        {
            public ParseMethodCacheEntry(MethodInfo? method, bool hasFormat)
            {
                Method = method;
                HasFormat = hasFormat;
            }

            public MethodInfo? Method { get; }

            public bool HasFormat { get; }
        }
    }
}
