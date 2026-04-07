using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.Services;
using Avalonia.Markup.Xaml;

namespace Avalonia.Diagnostics.Views
{
    internal partial class SourceLocationToolTipView : UserControl
    {
        public static readonly StyledProperty<string?> SourceLocationProperty =
            AvaloniaProperty.Register<SourceLocationToolTipView, string?>(nameof(SourceLocation));

        public static readonly StyledProperty<string> OpenLinkTextProperty =
            AvaloniaProperty.Register<SourceLocationToolTipView, string>(nameof(OpenLinkText), "Open");

        public SourceLocationToolTipView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public string? SourceLocation
        {
            get => GetValue(SourceLocationProperty);
            set => SetValue(SourceLocationProperty, value);
        }

        public string OpenLinkText
        {
            get => GetValue(OpenLinkTextProperty);
            set => SetValue(OpenLinkTextProperty, value);
        }

        public ICommand OpenSourceLocationCommand => SourceLocationOpenCommand.Instance;
    }
}
