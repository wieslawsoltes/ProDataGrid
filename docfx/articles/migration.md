# Migration

## Package Rename

This package has been renamed from `Avalonia.Controls.DataGrid` to `ProDataGrid`.

The new name gives the fork its own NuGet identity (so it can ship independently of Avalonia), avoids collisions with the built-in control, and signals the performance/features added in this branch.

The fork is maintained at https://github.com/wieslawsoltes/ProDataGrid.

## Migration

To migrate from the original package, update your NuGet reference:

```xml
<!-- Old -->
<PackageReference Include="Avalonia.Controls.DataGrid" Version="..." />

<!-- New -->
<PackageReference Include="ProDataGrid" Version="..." />
```
