using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DataGridSample
{
    public partial class GroupingPage : UserControl
    {
        private DataGrid? _groupedGrid;

        public GroupingPage()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _groupedGrid = this.FindControl<DataGrid>("GroupedDataGrid");
        }

        private void OnExpandAllGroups(object? sender, RoutedEventArgs e)
        {
            _groupedGrid?.ExpandAllGroups();
        }

        private void OnCollapseAllGroups(object? sender, RoutedEventArgs e)
        {
            _groupedGrid?.CollapseAllGroups();
        }
    }
}
