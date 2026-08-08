using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Diagnostics.Generated;
using Avalonia.Diagnostics.ViewModels;

namespace Avalonia.Diagnostics
{
    internal class ViewLocator : IDataTemplate
    {
        public Control? Build(object? data)
        {
            if (data is null)
                return null;

            if (ProDiagnosticsGeneratedSchemas.TryCreateView(data, out Control? view))
            {
                return view;
            }

            return new TextBlock { Text = $"No generated view registration for {data.GetType().FullName}." };
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
