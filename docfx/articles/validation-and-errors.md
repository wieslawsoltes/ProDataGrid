# Validation and Error States

ProDataGrid uses Avalonia validation to surface invalid cells and rows. Any validation source that Avalonia recognizes (for example, `DataValidationException` or `INotifyDataErrorInfo`) will mark the cell and row as invalid.

## Throw DataValidationException

Raise `DataValidationException` from your model setters to trigger inline errors:

```csharp
using Avalonia.Data;

public string Name
{
    get => _name;
    set
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DataValidationException("Name is required.");
        }

        _name = value;
        OnPropertyChanged();
    }
}
```

## Style Invalid Rows and Cells

Invalid rows and cells expose the `:invalid` pseudo-class:

```xml
<Style Selector="DataGridCell:invalid">
  <Setter Property="BorderBrush" Value="#E11D48" />
  <Setter Property="BorderThickness" Value="1" />
</Style>

<Style Selector="DataGridRow:invalid">
  <Setter Property="Background" Value="#FFF7E6" />
</Style>
```

## Sample Reference

See the `Validation` page in `src/DataGridSample` for validation across every editable column type (numeric, date/time, masked text, combo boxes, toggles, hyperlinks, and more).
