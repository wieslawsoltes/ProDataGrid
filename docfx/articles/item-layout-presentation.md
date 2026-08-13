# Item-template layout presentation

Layout models can choose whether DataGrid realizes its traditional row/cell visual tree or one lightweight templated item per data row. This makes the same control suitable for table, list, card, tile, and wrap views without replacing the item source or the model-based selection, filtering, sorting, state, and navigation systems.

## Choose the presentation in the model

The optional `IDataGridLayoutPresentationModel` contract supplies two values:

- `PresentationMode`: `Rows` for `DataGridRow`/`DataGridCell` presentation or `Items` for `DataGridItemContainer` presentation;
- `ItemSizeEstimate`: the positive width and height used for off-screen extent, anchor, and navigation estimates before an item is measured.

All built-in models derive from `DataGridLayoutModelBase`, so they expose both properties directly:

```csharp
public IDataGridLayoutModel CardsLayout { get; } =
    new DataGridUniformGridLayoutModel
    {
        PresentationMode = DataGridLayoutPresentationMode.Items,
        ItemSizeEstimate = new Size(260, 96),
        MinItemWidth = 260,
        MinItemHeight = 96,
        MinColumnSpacing = 8,
        MinRowSpacing = 8
    };
```

Changing either property raises a reset invalidation. Bind stable model instances to `DataGrid.LayoutModel` when switching views at runtime.

## Supply a compiled item template

Set `DataGrid.ItemTemplate` exactly as you would set an ItemsControl template. Give every binding scope an `x:DataType` and use compiled bindings:

```xml
<DataGrid x:DataType="viewModels:CatalogViewModel"
          ItemsSource="{CompiledBinding Products}"
          LayoutModel="{CompiledBinding LayoutModel}"
          UseLogicalScrollable="True"
          SelectionUnit="FullRow">
  <DataGrid.ItemTemplate>
    <DataTemplate x:DataType="models:Product">
      <Border Width="248"
              Height="88"
              Padding="10"
              AutomationProperties.Name="{CompiledBinding Name}">
        <StackPanel>
          <TextBlock FontWeight="SemiBold"
                     Text="{CompiledBinding Name}" />
          <TextBlock Text="{CompiledBinding Category}" />
        </StackPanel>
      </Border>
    </DataTemplate>
  </DataGrid.ItemTemplate>

  <!-- Columns remain semantic metadata in item mode. -->
  <DataGrid.Columns>
    <DataGridTextColumn Header="Name"
                        Binding="{CompiledBinding Name}"
                        x:DataType="models:Product" />
  </DataGrid.Columns>
</DataGrid>
```

In item mode:

- column and row headers are hidden;
- `DataGridRow` and `DataGridCell` are not created for data items;
- one recyclable `DataGridItemContainer` hosts the template root;
- columns remain available to filtering, sorting, clipboard/state models, current-position identity, and navigation policy;
- grouping slots continue to use DataGrid group header/footer containers.

Keep at least one visible semantic column when current-cell navigation is required. The column is not rendered as a cell, but the navigation position still preserves its display index.

## Recycling and runtime switching

`DataGridItemContainer` has `:selected`, `:current`, and `:pointerover` pseudo-classes. Its `Index`, `IsSelected`, and read-only `IsCurrent` properties are public for themes, tests, and automation-aware customization.

The container pool is separate from the row pool. An `IRecyclingDataTemplate` receives the old root only when the same template is still active; a normal `IDataTemplate` builds a fresh root. Replacing `ItemTemplate`, switching between row and item presentation, or changing model geometry resets realization without replacing selection/currency state.

`KeepRecycledContainersInVisualTree` and the existing recycle-pool limits apply to item containers as well as rows. Virtualizing layouts retain containers proportional to the realization window. `DataGridNonVirtualizingStackLayoutModel` intentionally realizes the full sequence.

Use a representative `ItemSizeEstimate`. The measured desired size replaces the estimate for realized variable-size items, while the estimate keeps far scrolling and non-realized navigation allocation-free.

## Selection, navigation, editing, and accessibility

Pointer selection is row/item selection even if `SelectionUnit` was configured as `Cell`; `SelectionUnit="FullRow"` communicates the intent most clearly. Keyboard and programmatic navigation still flow through the semantic navigation model, then through the active layout's optional geometry resolver. Runtime switching preserves the current row, selected items, and current semantic column.

Item presentation does not enter DataGrid cell edit mode: `BeginEdit()` returns `false` and cannot create a hidden row/cell tree. Put editable controls in the item template and bind them to view-model properties or commands when a card/list view needs editing.

Each realized item exposes a `DataItem` automation peer with `ISelectionItemProvider`. Position-in-set, size-of-set, selected state, and selection-container relationships are supplied by DataGrid. Set a meaningful `AutomationProperties.Name` on the template root when the data item's `ToString()` is not an appropriate accessible name.

## Custom models

A custom model can opt into item presentation by implementing `IDataGridLayoutPresentationModel`. Deriving from `DataGridLayoutModelBase` provides the implementation automatically:

```csharp
public sealed class MasonryLayoutModel : DataGridLayoutModelBase
{
    public override IDataGridLayoutAlgorithm CreateAlgorithm() =>
        new MasonryLayoutAlgorithm(this);
}

var model = new MasonryLayoutModel
{
    PresentationMode = DataGridLayoutPresentationMode.Items,
    ItemSizeEstimate = new Size(240, 100)
};
```

Algorithms remain presentation-agnostic. `IDataGridLayoutContext.GetOrCreateElementAt` returns the DataGrid-owned row, group, or item container selected by the active model; custom algorithms measure and arrange it without constructing containers themselves.

See [Model-based layouts](model-based-layouts.md), [Custom layouts](custom-layouts.md), [Generated layouts](source-generators/layouts.md), and the `Layout Gallery` sample.
