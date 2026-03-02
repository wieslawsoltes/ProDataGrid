using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Diagnostics.ViewModels;

namespace Avalonia.Diagnostics
{
    internal class ViewLocator : IDataTemplate
    {
        public Control? Build(object? data)
        {
            if (data is null)
                return null;

            var viewModelType = data.GetType();
            var name = GetViewTypeName(viewModelType);
            var type = viewModelType.Assembly.GetType(name);

            if (type is not null && typeof(Control).IsAssignableFrom(type))
            {
                return (Control)Activator.CreateInstance(type)!;
            }
            else
            {
                return new TextBlock { Text = name };
            }
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }

        private static string GetViewTypeName(Type viewModelType)
        {
            var fullName = viewModelType.FullName ?? viewModelType.Name;
            var mappedNamespace = fullName.Replace(".ViewModels.", ".Views.", StringComparison.Ordinal);

            const string suffix = "ViewModel";
            if (mappedNamespace.EndsWith(suffix, StringComparison.Ordinal))
            {
                return mappedNamespace[..^suffix.Length] + "View";
            }

            return mappedNamespace;
        }
    }
}
