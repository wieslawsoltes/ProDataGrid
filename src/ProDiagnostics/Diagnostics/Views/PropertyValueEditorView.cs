using System;
using Avalonia.Controls;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.ViewModels;

namespace Avalonia.Diagnostics.Views
{
    partial class PropertyValueEditorView : UserControl
    {
        private readonly PropertyValueEditorService _editorService = new();

        private PropertyViewModel? Property => (PropertyViewModel?)DataContext;

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            UpdateEditor();
        }

        private void UpdateEditor()
        {
            if (Property?.PropertyType is not { } propertyType)
            {
                Content = null;
                return;
            }

            var update = _editorService.PrepareEditor(Property, propertyType);
            var content = update.Content;
            if (!ReferenceEquals(Content, content))
            {
                Content = null;
            }

            update.Activate();
            Content = content;
        }
    }
}
