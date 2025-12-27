# ProDataGrid for Avalonia

**ProDataGrid** is a high-performance, feature-rich DataGrid control for [Avalonia](https://github.com/AvaloniaUI/Avalonia). It is a hard fork of the in-box `Avalonia.Controls.DataGrid`, tuned for stronger scrolling, richer selection, and advanced data operations while staying API compatible where it matters.

## Getting Started

### Install

```bash
dotnet add package ProDataGrid
```

```xml
<PackageReference Include="ProDataGrid" Version="..." />
```

### Add Themes in App.axaml

Include the ProDataGrid theme styles so the templates are available application-wide.

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml" />
</Application.Styles>
```

### Basic Usage

```xml
<DataGrid ItemsSource="{Binding People}"
          AutoGenerateColumns="False"
          UseLogicalScrollable="True">
  <DataGrid.Columns>
    <DataGridTextColumn Header="Id" Binding="{Binding Id}" Width="60" />
    <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="*" />
    <DataGridCheckBoxColumn Header="Active" Binding="{Binding IsActive}" Width="80" />
  </DataGrid.Columns>
</DataGrid>
```

### Sample App

Run the sample gallery to explore features and configuration patterns:

```bash
dotnet run --project src/DataGridSample/DataGridSample.csproj
```

## Highlights

| Area | Highlights |
| --- | --- |
| Virtualization & scrolling | ScrollViewer-based `ILogicalScrollable`, snap points, row height estimators, frozen columns. |
| Selection & navigation | Avalonia `SelectionModel` integration, stable selection across sorting/filtering/paging. |
| Data operations | Sorting, filtering, grouping, paging, and search with pluggable models/adapters. |
| Drag & drop | Row reordering with flat/hierarchical handlers and routed events. |
| Styling | Updated templates, column themes, and richer visuals. |

## Documentation Sections

- **[Articles](articles/intro.md)**: Practical guides and feature-focused walkthroughs.
- **[API Documentation](api/index.md)**: Reference for all public types and members.

## License

ProDataGrid is licensed under the [MIT License](https://github.com/wieslawsoltes/ProDataGrid/blob/master/licence.md).
