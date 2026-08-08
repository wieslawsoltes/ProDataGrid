using ProDataGrid.SourceGeneration;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Diagnostics.Views;

[assembly: GenerateDataGridRegistry(
    RegistryName = "ProDiagnosticsGeneratedSchemas",
    RegistryNamespace = "Avalonia.Diagnostics.Generated")]
[assembly: DataGridViewRegistration(typeof(MainViewModel), typeof(MainView))]
[assembly: DataGridViewRegistration(typeof(TreePageViewModel), typeof(TreePageView))]
[assembly: DataGridViewRegistration(typeof(ControlDetailsViewModel), typeof(ControlDetailsView))]
[assembly: DataGridViewRegistration(typeof(EventsPageViewModel), typeof(EventsPageView))]
[assembly: DataGridViewRegistration(typeof(ResourcesPageViewModel), typeof(ResourcesPageView))]
[assembly: DataGridViewRegistration(typeof(ResourceDetailsViewModel), typeof(ResourceDetailsView))]
[assembly: DataGridViewRegistration(typeof(AssetsPageViewModel), typeof(AssetsPageView))]
[assembly: DataGridViewRegistration(typeof(HotKeyPageViewModel), typeof(HotKeyPageView))]
