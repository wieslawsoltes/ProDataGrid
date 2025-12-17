using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Controls.Primitives;
using Avalonia.ReactiveUI;
using DataGridSample.Models;
using DataGridSample.ViewModels;

namespace DataGridSample;

public static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Person))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Country))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Deployment))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(PixelItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(VariableHeightItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(ChangeItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(LiveDataItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(ThemedRow))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(CurrentPerson))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(AutoScrollViewModel.AutoItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(Contact))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(NoteEntry))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(FilteringModelSampleViewModel.Order))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(RoutedEventsViewModel.SampleItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(RoutedEventsViewModel.HierarchicalItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(HierarchicalRowDragDropViewModel.TreeItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(HierarchicalSampleViewModel.TreeItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(DataGridCollectionViewGroup))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(DataGridPathGroupDescription))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FilteringDescriptor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SortingDescriptor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FilteringModel))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SortingModel))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(HierarchicalModel<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(HierarchicalNode))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(HierarchicalOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FlattenedChange))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FlattenedIndexMap))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FlattenedChangedEventArgs))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(HierarchicalNodeEventArgs))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(HierarchicalNodeLoadFailedEventArgs))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(HierarchicalNodeRetryEventArgs))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DataGridHierarchicalAdapter))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DataGridHierarchicalAdapter<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DataGridHierarchicalRowReorderHandler))]
    // Preserve DataGrid controls referenced via XAML to ensure templates can instantiate cells/rows under trimming.
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DataGrid))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DataGridCell))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DataGridRow))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DataGridRowsPresenter))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DataGridCellsPresenter))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DataGridColumnHeader))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DataGridColumnHeadersPresenter))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DataGridRowGroupHeader))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DataGridHierarchicalColumn))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DataGridHierarchicalPresenter))]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();
}
