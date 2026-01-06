# Conditional Formatting

The conditional formatting model applies cell or row themes based on data. It mirrors the existing sorting, filtering, and search models: you supply descriptors and the grid evaluates them against the current view.

## Quick Start

Define a theme for each visual state you want to apply:

```xml
<ControlTheme x:Key="PositiveCellTheme"
              TargetType="DataGridCell"
              BasedOn="{StaticResource {x:Type DataGridCell}}">
  <Setter Property="Background" Value="#1C2ECC71" />
</ControlTheme>
<ControlTheme x:Key="NegativeCellTheme"
              TargetType="DataGridCell"
              BasedOn="{StaticResource {x:Type DataGridCell}}">
  <Setter Property="Background" Value="#1CE74C3C" />
  <Setter Property="Foreground" Value="#C0392B" />
</ControlTheme>
```

Create a model in code and assign it to the grid:

```csharp
var model = new ConditionalFormattingModel();
model.Apply(new[]
{
    new ConditionalFormattingDescriptor(
        ruleId: "positive",
        @operator: ConditionalFormattingOperator.GreaterThan,
        columnId: "Delta",
        value: 0d,
        themeKey: "PositiveCellTheme"),
    new ConditionalFormattingDescriptor(
        ruleId: "negative",
        @operator: ConditionalFormattingOperator.LessThan,
        columnId: "Delta",
        value: 0d,
        themeKey: "NegativeCellTheme")
});

dataGrid.ConditionalFormattingModel = model;
```

## Rule Basics

- `ColumnId` can be a column instance or a property path string (for bound columns).
- `Target` controls whether a rule formats a cell or an entire row.
- `ValueSource` chooses whether the rule evaluates the cell value or the row item.
- `Theme` and `ThemeKey` select a `ControlTheme` to apply; the key is resolved from the grid's resources.
- `Priority` and `StopIfTrue` control ordering when multiple rules match.

## Row Formatting Example

```csharp
var model = new ConditionalFormattingModel();
model.Apply(new[]
{
    new ConditionalFormattingDescriptor(
        ruleId: "row-alert",
        @operator: ConditionalFormattingOperator.Equals,
        propertyPath: nameof(RowStatus.Status),
        value: "Overdue",
        target: ConditionalFormattingTarget.Row,
        valueSource: ConditionalFormattingValueSource.Item,
        themeKey: "RowAlertTheme")
});
```

Use a row theme that tweaks the row background rectangle:

```xml
<ControlTheme x:Key="RowAlertTheme"
              TargetType="DataGridRow"
              BasedOn="{StaticResource {x:Type DataGridRow}}">
  <Style Selector="^ /template/ Rectangle#BackgroundRectangle">
    <Setter Property="Fill" Value="#1CD32F2F" />
    <Setter Property="Opacity" Value="1" />
  </Style>
</ControlTheme>
```

## Samples

See the sample gallery for the conditional formatting model page, and updated Power Fx + Excel-like pages that now use the model.
