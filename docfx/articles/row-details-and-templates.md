# Row Details and Templates

Row details let you show expandable content under each row (for example, summaries, nested lists, or diagnostics).

## Basic Row Details

Set a grid-level `RowDetailsTemplate` and control visibility with `RowDetailsVisibilityMode`.

```xml
<DataGrid ItemsSource="{Binding Items}"
          RowDetailsVisibilityMode="VisibleWhenSelected">
  <DataGrid.RowDetailsTemplate>
    <DataTemplate>
      <Border Padding="8" Background="#08000000">
        <TextBlock Text="{Binding Details}" TextWrapping="Wrap" />
      </Border>
    </DataTemplate>
  </DataGrid.RowDetailsTemplate>
</DataGrid>
```

Available modes:

- `Collapsed`: no details.
- `Visible`: show for every row.
- `VisibleWhenSelected`: expand only the current selection.

## Per-Row Templates

Override details per row with `DataGridRow.DetailsTemplate`:

```xml
<DataGrid>
  <DataGrid.RowStyle>
    <Style Selector="DataGridRow">
      <Setter Property="DetailsTemplate">
        <DataTemplate>
          <TextBlock Text="{Binding Notes}" TextWrapping="Wrap" />
        </DataTemplate>
      </Setter>
    </Style>
  </DataGrid.RowStyle>
</DataGrid>
```

## Frozen Details

Set `AreRowDetailsFrozen="True"` to keep details aligned with frozen columns when you scroll horizontally.

## Row Details Events

Handle details lifecycle events when you need to hook state or metrics:

- `LoadingRowDetails`
- `UnloadingRowDetails`
- `RowDetailsVisibilityChanged`
