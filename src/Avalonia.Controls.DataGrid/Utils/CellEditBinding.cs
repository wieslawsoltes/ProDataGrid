#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.PropertyStore;
using Avalonia.Reactive;

namespace Avalonia.Controls.Utils
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    interface ICellEditBinding
        : IDisposable
    {
        bool IsValid { get; }
        IEnumerable<Exception> ValidationErrors { get; }
        IObservable<bool> ValidationChanged { get; }
        bool CommitEdit();
    }

    internal abstract class CellEditBindingBase : ICellEditBinding
    {
        private readonly AvaloniaObject _target;
        private readonly AvaloniaProperty _property;
        private readonly string _bindingPath;
        private readonly bool _supportsDirectSourceWriteFallback;
        private readonly LightweightSubject<bool> _changedSubject = new();
        private readonly List<Exception> _validationErrors = new();
        private DataGridValidationSeverity _validationSeverity = DataGridValidationSeverity.None;
        private bool _disposed;

        protected CellEditBindingBase(AvaloniaObject target, AvaloniaProperty property, BindingBase binding)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _property = property ?? throw new ArgumentNullException(nameof(property));
            _supportsDirectSourceWriteFallback = BindingCloneHelper.SupportsDirectDataContextMemberWrite(binding);
            _bindingPath = _supportsDirectSourceWriteFallback && binding != null
                ? BindingCloneHelper.GetPath(binding)
                : null;
            _target.PropertyChanged += OnTargetPropertyChanged;
            RefreshValidationState();
        }

        public bool IsValid => _validationSeverity != DataGridValidationSeverity.Error;
        public IEnumerable<Exception> ValidationErrors => _validationErrors;
        public IObservable<bool> ValidationChanged => _changedSubject;

        public bool CommitEdit()
        {
            Exception commitError = null;
            var expression = BindingOperations.GetBindingExpressionBase(_target, _property);
            var preCommitValidationErrors = CaptureValidationErrors(expression);
            var shouldPreservePreCommitValidation = preCommitValidationErrors.Count > 0 &&
                                                   expression is IValueEntry preCommitValueEntry &&
                                                   TryGetSourceValue(preCommitValueEntry, out var preCommitSourceValue) &&
                                                   Equals(_target.GetValue(_property), preCommitSourceValue);
            if (expression != null)
            {
                try
                {
                    expression.UpdateSource();
                }
                catch (Exception ex)
                {
                    commitError = ex;
                }
            }

            if (commitError != null)
            {
                UpdateValidationErrorsFromException(commitError);
            }
            else
            {
                var usedToggleSwitchFallback = false;
                UpdateValidationErrorsFromTarget(expression);

                if (_validationErrors.Count == 0 &&
                    _supportsDirectSourceWriteFallback &&
                    _target is ToggleButton &&
                    expression is IValueEntry writeValueEntry &&
                    TryGetSourceValue(writeValueEntry, out var currentSourceValue))
                {
                    var targetValue = _target.GetValue(_property);
                    if (!Equals(targetValue, currentSourceValue) &&
                        TryWriteTargetValueToSource(targetValue, out var writeError))
                    {
                        usedToggleSwitchFallback = true;
                        if (writeError != null)
                        {
                            UpdateValidationErrorsFromException(writeError);
                        }
                        else
                        {
                            UpdateValidationErrorsFromTarget(expression);
                        }
                    }
                }

                if (_validationErrors.Count == 0 && shouldPreservePreCommitValidation)
                {
                    AlterValidationErrors(list =>
                    {
                        list.Clear();
                        list.AddRange(preCommitValidationErrors);
                    });
                }

                if (!IsValid &&
                    !usedToggleSwitchFallback &&
                    expression is IValueEntry valueEntry &&
                    TryGetSourceValue(valueEntry, out var sourceValue))
                {
                    var targetValue = _target.GetValue(_property);
                    if (Equals(targetValue, sourceValue))
                    {
                        expression?.UpdateTarget();
                        UpdateValidationErrorsFromTarget(expression);
                    }
                }
            }

            return IsValid;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _target.PropertyChanged -= OnTargetPropertyChanged;
        }

        private void OnTargetPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == _property ||
                e.Property == DataValidationErrors.ErrorsProperty ||
                e.Property == DataValidationErrors.HasErrorsProperty)
            {
                RefreshValidationState();
            }
        }

        private void RefreshValidationState()
        {
            UpdateValidationErrorsFromTarget(BindingOperations.GetBindingExpressionBase(_target, _property));
        }

        private List<Exception> CaptureValidationErrors(BindingExpressionBase? expression)
        {
            var result = new List<Exception>();

            if (expression is IValueEntry valueEntry)
            {
                valueEntry.GetDataValidationState(out _, out var error);
                if (error != null)
                {
                    result.AddRange(ValidationUtil.UnpackException(error));
                }
            }

            if (result.Count == 0 && _target is Control control)
            {
                AppendControlValidationErrors(control, result);
            }

            return result;
        }

        private void UpdateValidationErrorsFromException(Exception error)
        {
            AlterValidationErrors(errors =>
            {
                errors.Clear();
                errors.AddRange(ValidationUtil.UnpackException(error));
            });
        }

        private void UpdateValidationErrorsFromTarget(BindingExpressionBase expression)
        {
            if (expression is IValueEntry valueEntry)
            {
                valueEntry.GetDataValidationState(out var state, out var error);
                if (error != null)
                {
                    AlterValidationErrors(list =>
                    {
                        list.Clear();
                        list.AddRange(ValidationUtil.UnpackException(error));
                    });
                    return;
                }
            }

            if (_target is not Control control)
            {
                AlterValidationErrors(list => list.Clear());
                return;
            }

            AlterValidationErrors(list =>
            {
                list.Clear();
                AppendControlValidationErrors(control, list);
            });
        }

        private static void AppendControlValidationErrors(Control control, List<Exception> list)
        {
            var errors = DataValidationErrors.GetErrors(control);
            if (errors == null)
            {
                return;
            }

            foreach (var error in errors)
            {
                if (error == null)
                {
                    continue;
                }

                if (error is Exception exception)
                {
                    list.AddRange(ValidationUtil.UnpackException(exception));
                }
                else
                {
                    list.Add(new DataValidationException(error));
                }
            }
        }

        private void AlterValidationErrors(Action<List<Exception>> action)
        {
            var hadErrors = _validationErrors.Count > 0;

            action(_validationErrors);
            _validationSeverity = _validationErrors.Count == 0
                ? DataGridValidationSeverity.None
                : ValidationUtil.GetValidationSeverity(_validationErrors);
            var hasErrors = _validationErrors.Count > 0;

            if (hadErrors || hasErrors)
            {
                _changedSubject.OnNext(IsValid);
            }
        }

        private static bool TryGetSourceValue(IValueEntry valueEntry, out object sourceValue)
        {
            sourceValue = null;

            try
            {
                if (!valueEntry.HasValue())
                {
                    return false;
                }

                sourceValue = valueEntry.GetValue();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryWriteTargetValueToSource(object targetValue, out Exception? error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(_bindingPath) ||
                _target is not Control control ||
                control.DataContext == null)
            {
                return false;
            }

            object current = control.DataContext;
            var segments = _bindingPath.Split('.');

            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (!TryGetMemberValue(current, segments[i], out current) || current == null)
                {
                    return false;
                }
            }

            return TrySetMemberValue(current, segments[^1], targetValue, out error);
        }

        private static bool TryGetMemberValue(object instance, string memberName, out object? value)
        {
            value = null;

            var type = instance.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                value = property.GetValue(instance);
                return true;
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                value = field.GetValue(instance);
                return true;
            }

            return false;
        }

        private static bool TrySetMemberValue(object instance, string memberName, object? value, out Exception? error)
        {
            error = null;

            var type = instance.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                try
                {
                    property.SetValue(instance, ConvertValue(value, property.PropertyType));
                    return true;
                }
                catch (TargetInvocationException ex)
                {
                    error = ex.InnerException ?? ex;
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex;
                    return true;
                }
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                try
                {
                    field.SetValue(instance, ConvertValue(value, field.FieldType));
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex;
                    return true;
                }
            }

            return false;
        }

        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value == null)
            {
                return null;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
        }
    }

    internal sealed class CellEditBinding : CellEditBindingBase
    {
        public CellEditBinding(AvaloniaObject target, AvaloniaProperty property, BindingBase binding)
            : base(target, property, binding)
        {
        }
    }

    internal sealed class ExplicitCellEditBinding : CellEditBindingBase
    {
        public ExplicitCellEditBinding(AvaloniaObject target, AvaloniaProperty property, BindingBase binding)
            : base(target, property, binding)
        {
        }
    }
}
