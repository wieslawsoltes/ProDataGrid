using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using DataGridSample.ViewModels;

namespace DataGridSample.Pages;

public partial class ChartHierarchyPage : UserControl
{
    public ChartHierarchyPage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DataContext ??= new ChartHierarchyViewModel();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is ChartHierarchyViewModel model) model.Dispose();
        DataContext = null;
        base.OnDetachedFromVisualTree(e);
    }
}
