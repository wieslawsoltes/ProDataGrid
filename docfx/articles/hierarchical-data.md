# Hierarchical Data

ProDataGrid supports hierarchical rows with a dedicated column type and model adapters. This allows tree-like views while keeping DataGrid features such as selection, sorting, and summaries.

## Hierarchical Model Integration

Hierarchical rows are driven by `IHierarchicalModel` (flattened view of visible nodes) plus a thin adapter and a built-in `DataGridHierarchicalColumn` that renders indentation and the expander glyph.

- Plug in a model or factory: bind `HierarchicalModel`/`HierarchicalModelFactory` and set `HierarchicalRowsEnabled="True"`.
- When hierarchical rows are enabled and no `ItemsSource` is provided, the grid auto-binds to the model's flattened view so callers don't have to manage or refresh a separate flattened collection; `ObservableFlattened` is available when you need `INotifyCollectionChanged` or reactive pipelines.
- Provide children/leaves via `HierarchicalOptions` (`ChildrenSelector`, optional `IsLeafSelector`, `AutoExpandRoot/MaxAutoExpandDepth`, `SiblingComparer`/`SiblingComparerSelector`, `VirtualizeChildren`). Use the typed flavor (`HierarchicalOptions<T>`/`HierarchicalModel<T>`) when you want strongly-typed selectors and observable flattened nodes.
- The adapter exposes `Count/ItemAt/Toggle/Expand/Collapse` and raises `FlattenedChanged`; selection mapping uses the flattened indices.
- Use `DataGridHierarchicalColumn` for the tree column; per-level indent is configurable via `Indent`.

```xml
<DataGrid ItemsSource="{Binding Rows}"
          HierarchicalModel="{Binding Model}"
          HierarchicalRowsEnabled="True"
          AutoGenerateColumns="False">
  <DataGrid.Columns>
    <DataGridHierarchicalColumn Header="Name"
                                Width="2*"
                                Binding="{Binding Item.Name}" />
    <DataGridTextColumn Header="Kind" Binding="{Binding Item.Kind}" />
  </DataGrid.Columns>
</DataGrid>
```

```csharp
using Avalonia.Controls.DataGridHierarchical;

var options = new HierarchicalOptions
{
    ChildrenSelector = item => ((TreeNode)item).Children,
    IsLeafSelector = item => !((TreeNode)item).IsDirectory,
    AutoExpandRoot = true,
    MaxAutoExpandDepth = 0,
    VirtualizeChildren = true
};

var model = new HierarchicalModel(options);
model.SetRoot(rootNode);
```

Sample: see `Hierarchical Model` page in `src/DataGridSample` for a file-system tree with Name/Kind/Size/Modified columns.

## Expanded State and Path Selection

Bind expanded state to your model with `IsExpandedSelector`/`IsExpandedSetter` so expander toggles update your items:

```csharp
var options = new HierarchicalOptions<TreeItem>
{
    ItemsSelector = item => item.Children,
    IsExpandedSelector = item => item.IsExpanded,
    IsExpandedSetter = (item, value) => item.IsExpanded = value,
    ExpandedStateKeyMode = ExpandedStateKeyMode.Path
};
```

For stable selection across refreshes or duplicate labels, supply an `ItemPathSelector` and enable automatic expansion:

```xml
<DataGrid HierarchicalRowsEnabled="True"
          HierarchicalModel="{Binding Model}"
          AutoScrollToSelectedItem="True"
          AutoExpandSelectedItem="True" />
```

`ExpandedStateKeyMode` can be `Item`, `Path`, or `Custom` (with `ExpandedStateKeySelector`) to control how expansion state is preserved. When `ItemPathSelector` is present, the grid expands to a selected item without traversing the entire tree.

## Undo/Redo

Hierarchical scenarios can capture actions for undo/redo to keep selection and expansion consistent across edits.
