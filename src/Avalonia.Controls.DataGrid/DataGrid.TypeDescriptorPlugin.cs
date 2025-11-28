// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;
using Avalonia.Data;
using Avalonia.Data.Core.Plugins;

namespace Avalonia.Controls
{
    /// <summary>
    /// Registers a binding plugin that uses <see cref="TypeDescriptor"/> to read and write
    /// non-reflection properties (e.g. <see cref="System.Data.DataRowView"/> columns).
    /// </summary>
    internal static class DataGridTypeDescriptorPlugin
    {
        private static bool _registered;

        [ModuleInitializer]
        internal static void EnsureRegistered()
        {
            if (_registered)
            {
                return;
            }

            BindingPlugins.PropertyAccessors.Insert(0, new TypeDescriptorPropertyAccessorPlugin());
            _registered = true;
        }

        private sealed class TypeDescriptorPropertyAccessorPlugin : IPropertyAccessorPlugin
        {
            public bool Match(object obj, string propertyName)
            {
                return TryGetDescriptor(obj, propertyName, out _);
            }

            public IPropertyAccessor Start(WeakReference<object?> reference, string propertyName)
            {
                if (!reference.TryGetTarget(out var target) || target is null ||
                    !TryGetDescriptor(target, propertyName, out var descriptor))
                {
                    return new PropertyError(new BindingNotification(new InvalidOperationException($"Property '{propertyName}' not found."), BindingErrorType.Error));
                }

                if (descriptor == null)
                {
                    return new PropertyError(new BindingNotification(new InvalidOperationException($"Property '{propertyName}' not found."), BindingErrorType.Error));
                }

                return new Accessor(reference, descriptor);
            }

            private static bool TryGetDescriptor(object instance, string propertyName, out PropertyDescriptor? descriptor)
            {
                descriptor = null;

                if (instance is not System.Data.DataRowView && instance is not System.Data.DataRow)
                {
                    return false;
                }

                var name = Normalize(propertyName);
                if (string.IsNullOrEmpty(name))
                {
                    return false;
                }

                descriptor = TypeDescriptor.GetProperties(instance).Find(name, ignoreCase: false);
                return descriptor != null;
            }

            private static string? Normalize(string propertyName)
            {
                if (string.IsNullOrEmpty(propertyName))
                {
                    return null;
                }

                if (propertyName.Length > 1 &&
                    propertyName[0] == '[' &&
                    propertyName[propertyName.Length - 1] == ']')
                {
                    return propertyName.Substring(1, propertyName.Length - 2);
                }

                return propertyName;
            }

            private sealed class Accessor : IPropertyAccessor
            {
                private readonly WeakReference<object?> _reference;
                private readonly PropertyDescriptor _descriptor;
                private Action<object?>? _listener;
                private INotifyPropertyChanged? _inpc;

                public Accessor(WeakReference<object?> reference, PropertyDescriptor descriptor)
                {
                    _reference = reference;
                    _descriptor = descriptor;
                }

                public Type PropertyType => _descriptor.PropertyType;

                public object? Value => TryGetValue(out var value) ? value : AvaloniaProperty.UnsetValue;

                public bool SetValue(object? value, BindingPriority priority)
                {
                    if (!_reference.TryGetTarget(out var target) || target is null)
                    {
                        return false;
                    }

                    _descriptor.SetValue(target, value);
                    return true;
                }

                public void Subscribe(Action<object?> listener)
                {
                    _listener = listener;

                    _listener?.Invoke(Value);

                    if (_reference.TryGetTarget(out var target) && target is INotifyPropertyChanged inpc)
                    {
                        _inpc = inpc;
                        _inpc.PropertyChanged += OnPropertyChanged;
                    }
                }

                public void Unsubscribe()
                {
                    if (_inpc != null)
                    {
                        _inpc.PropertyChanged -= OnPropertyChanged;
                        _inpc = null;
                    }

                    _listener = null;
                }

                public void Dispose() => Unsubscribe();

                private bool TryGetValue(out object? value)
                {
                    if (_reference.TryGetTarget(out var target) && target != null)
                    {
                        try
                        {
                            value = _descriptor.GetValue(target);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            value = new BindingNotification(ex, BindingErrorType.Error);
                            return true;
                        }
                    }

                    value = AvaloniaProperty.UnsetValue;
                    return false;
                }

                private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
                {
                    if (string.IsNullOrEmpty(e.PropertyName) ||
                        string.Equals(e.PropertyName, _descriptor.Name, StringComparison.Ordinal))
                    {
                        _listener?.Invoke(Value);
                    }
                }
            }
        }
    }
}
