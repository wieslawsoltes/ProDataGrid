using System;
using System.ComponentModel;
using System.Reflection;
using Avalonia.Data;
using Avalonia.Diagnostics.Services;

namespace Avalonia.Diagnostics.ViewModels
{
    internal class ClrPropertyViewModel : PropertyViewModel
    {
        private readonly object _target;
        private Type _assignedType;
        private object? _value;
        private readonly Type _propertyType;

#nullable disable
        // Remove "nullable disable" after MemberNotNull will work on our CI.
        public ClrPropertyViewModel(object o, PropertyInfo property)
#nullable restore
        {
            _target = o;
            Property = property;

            if (property.DeclaringType == null || !property.DeclaringType.IsInterface)
            {
                Name = property.Name;
            }
            else
            {
                Name = property.DeclaringType.Name + '.' + property.Name;
            }

            DeclaringType = property.DeclaringType;
            _propertyType = property.PropertyType;

            Update();
        }

        public PropertyInfo Property { get; }
        public override object Key => Name;
        public override string Name { get; }
        public override string Group => IsPinned ? "Pinned" : "CLR Properties";

        public override Type AssignedType => _assignedType;
        public override Type PropertyType => _propertyType;
        public override bool IsReadonly => !Property.CanWrite;
        protected override object Target => _target;
        protected override string XamlPropertyName => Property.Name;
        protected override bool IsAvaloniaProperty => false;

        public override object? Value
        {
            get => _value;
            set
            {
                try
                {
                    if (ReferenceEquals(value, BindingOperations.DoNothing) || !Property.CanWrite)
                    {
                        return;
                    }

                    var oldValue = _value;
                    Property.SetValue(_target, value);
                    Update();
                    NotifyPropertyEdited(oldValue, _value);
                }
                catch { }
            }
        }

        public override string Priority => string.Empty;

        public override bool? IsAttached => default;

        public override Type? DeclaringType { get; }

        internal override bool TrySetResourceReference(ResourceReferenceCandidate candidate, out string? error)
        {
            error = null;

            if (candidate.Kind == DevToolsResourceReferenceKind.Dynamic)
            {
                error = "DynamicResource can only be applied to Avalonia properties.";
                return false;
            }

            if (!Property.CanWrite)
            {
                error = "The selected property is read-only.";
                return false;
            }

            try
            {
                var oldValue = _value;
                Property.SetValue(_target, candidate.Value);
                Update();
                NotifyPropertyEdited(oldValue, _value, candidate.Kind, candidate.Key, candidate.KeyText);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                return false;
            }
        }

        // [MemberNotNull(nameof(_type))]
        public override void Update()
        {
            object? value;
            Type? valueType = null;

            try
            {
                value = Property.GetValue(_target);
                valueType = value?.GetType();
            }
            catch (Exception e)
            {
                value = e.GetBaseException();
            }

            RaiseAndSetIfChanged(ref _value, value, nameof(Value));
            RaiseAndSetIfChanged(ref _assignedType, valueType ?? Property.PropertyType, nameof(AssignedType));
            RaisePropertyChanged(nameof(Type));
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName == nameof(IsPinned))
            {
                RaisePropertyChanged(nameof(Group));
            }
        }
    }
}
