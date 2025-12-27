# Getting Started

This guide shows how to install ProDataGrid, apply the v2 templates, and run the sample app.

## Install the Package

```bash
dotnet add package ProDataGrid
```

```xml
<PackageReference Include="ProDataGrid" Version="..." />
```

## Enable the v2 Templates

The v2 templates provide the ScrollViewer-based layout and enable logical scrolling by default.

## Add Themes in App.axaml

Include the ProDataGrid theme styles in your `App.axaml` so the templates are available application-wide.

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml" />
</Application.Styles>
```

```xml
<Application.Styles>
  <SimpleTheme />
  <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Simple.v2.xaml" />
</Application.Styles>
```

## Generic and Theme-Specific Styles

ProDataGrid ships a shared `Generic.xaml` with core control themes, and theme-specific wrappers for Fluent and Simple.

- **Fluent/Simple (recommended)**: include `Fluent.v2.xaml` or `Simple.v2.xaml` as shown above.
- **Generic only**: include `Generic.xaml` when you maintain a custom theme or want to layer your own visuals on top.
- **Legacy templates**: `Fluent.xaml` and `Simple.xaml` are the non-v2 templates if you need to stay on the classic ScrollBar layout.

```xml
<!-- Generic base styles only -->
<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Generic.xaml" />
```

```xml
<!-- Fluent -->
<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml" />

<!-- Simple -->
<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Simple.v2.xaml" />
```

## Create a Basic Grid

```xml
<DataGrid ItemsSource="{Binding Items}"
          AutoGenerateColumns="False"
          UseLogicalScrollable="True"
          CanUserResizeColumns="True">
  <DataGrid.Columns>
    <DataGridTextColumn Header="Id" Binding="{Binding Id}" Width="60" />
    <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="*" />
    <DataGridCheckBoxColumn Header="Active" Binding="{Binding IsActive}" Width="80" />
  </DataGrid.Columns>
</DataGrid>
```

## Run the Sample App

```bash
dotnet run --project src/DataGridSample/DataGridSample.csproj
```
