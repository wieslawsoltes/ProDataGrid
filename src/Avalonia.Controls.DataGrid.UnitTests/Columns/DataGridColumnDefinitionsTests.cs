using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridColumnDefinitionsTests
{
    public static System.Collections.Generic.IEnumerable<object[]> ColumnDefinitionMappings => new[]
    {
        new object[] { new DataGridTextColumnDefinition(), typeof(DataGridTextColumn) },
        new object[] { new DataGridCheckBoxColumnDefinition(), typeof(DataGridCheckBoxColumn) },
        new object[] { new DataGridComboBoxColumnDefinition(), typeof(DataGridComboBoxColumn) },
        new object[] { new DataGridButtonColumnDefinition(), typeof(DataGridButtonColumn) },
        new object[] { new DataGridHyperlinkColumnDefinition(), typeof(DataGridHyperlinkColumn) },
        new object[] { new DataGridImageColumnDefinition(), typeof(DataGridImageColumn) },
        new object[] { new DataGridTemplateColumnDefinition(), typeof(DataGridTemplateColumn) },
        new object[] { new DataGridNumericColumnDefinition(), typeof(DataGridNumericColumn) },
        new object[] { new DataGridProgressBarColumnDefinition(), typeof(DataGridProgressBarColumn) },
        new object[] { new DataGridSliderColumnDefinition(), typeof(DataGridSliderColumn) },
        new object[] { new DataGridDatePickerColumnDefinition(), typeof(DataGridDatePickerColumn) },
        new object[] { new DataGridTimePickerColumnDefinition(), typeof(DataGridTimePickerColumn) },
        new object[] { new DataGridMaskedTextColumnDefinition(), typeof(DataGridMaskedTextColumn) },
        new object[] { new DataGridAutoCompleteColumnDefinition(), typeof(DataGridAutoCompleteColumn) },
        new object[] { new DataGridToggleButtonColumnDefinition(), typeof(DataGridToggleButtonColumn) },
        new object[] { new DataGridToggleSwitchColumnDefinition(), typeof(DataGridToggleSwitchColumn) },
        new object[] { new DataGridHierarchicalColumnDefinition(), typeof(DataGridHierarchicalColumn) }
    };

    [Theory]
    [MemberData(nameof(ColumnDefinitionMappings))]
    public void ColumnDefinitions_Create_BuiltIn_Columns(DataGridColumnDefinition definition, Type expectedType)
    {
        var column = definition.CreateColumn(new DataGridColumnDefinitionContext(new DataGrid()));
        Assert.IsType(expectedType, column);
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Materializes_Columns()
    {
        var definitions = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "Name",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            },
            new DataGridCheckBoxColumnDefinition
            {
                Header = "Active",
                Binding = DataGridBindingDefinition.Create<Person, bool>(p => p.IsActive)
            }
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = definitions
        };

        var columns = GetNonFillerColumns(grid);

        Assert.Equal(2, columns.Count);
        var textColumn = Assert.IsType<DataGridTextColumn>(columns[0]);
        Assert.Equal("Name", textColumn.Header);
        var textBinding = Assert.IsType<CompiledBindingExtension>(textColumn.Binding);
        Assert.Equal(nameof(Person.Name), textBinding.Path?.ToString());

        var checkBoxColumn = Assert.IsType<DataGridCheckBoxColumn>(columns[1]);
        Assert.Equal("Active", checkBoxColumn.Header);
        var checkBinding = Assert.IsType<CompiledBindingExtension>(checkBoxColumn.Binding);
        Assert.Equal(nameof(Person.IsActive), checkBinding.Path?.ToString());
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Updates_On_Collection_Changes()
    {
        var definitions = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "Name",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            }
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = definitions
        };

        definitions.Add(new DataGridTextColumnDefinition
        {
            Header = "Age",
            Binding = DataGridBindingDefinition.Create<Person, int>(p => p.Age)
        });

        var columns = GetNonFillerColumns(grid);
        Assert.Equal(2, columns.Count);
        Assert.Contains(columns, c => Equals(c.Header, "Age"));

        definitions.RemoveAt(0);
        columns = GetNonFillerColumns(grid);
        Assert.Single(columns);
        Assert.Equal("Age", columns[0].Header);
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Refreshes_After_Reattach()
    {
        var definitions = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "Name",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            }
        };

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ColumnDefinitionsSource = definitions
        };

        var window = new Window
        {
            Width = 300,
            Height = 200,
            Content = grid
        };

        window.SetThemeStyles();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();

        Assert.Single(GetNonFillerColumns(grid));

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        definitions.Add(new DataGridTextColumnDefinition
        {
            Header = "Age",
            Binding = DataGridBindingDefinition.Create<Person, int>(p => p.Age)
        });

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();

        var headers = GetNonFillerColumns(grid).Select(c => c.Header).ToArray();
        Assert.Equal(2, headers.Length);
        Assert.Contains("Age", headers);

        window.Close();
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_AddRange_Materializes_Columns()
    {
        var definitions = new DataGridColumnDefinitionList();

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = definitions
        };

        definitions.AddRange(new DataGridColumnDefinition[]
        {
            new DataGridTextColumnDefinition
            {
                Header = "Name",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            },
            new DataGridCheckBoxColumnDefinition
            {
                Header = "Active",
                Binding = DataGridBindingDefinition.Create<Person, bool>(p => p.IsActive)
            }
        });

        var columns = GetNonFillerColumns(grid);
        Assert.Equal(2, columns.Count);
        Assert.IsType<DataGridTextColumn>(columns[0]);
        Assert.IsType<DataGridCheckBoxColumn>(columns[1]);
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_SuspendNotifications_Batches_Changes()
    {
        var definitions = new DataGridColumnDefinitionList();

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = definitions
        };

        using (definitions.SuspendNotifications())
        {
            definitions.Add(new DataGridTextColumnDefinition
            {
                Header = "Name",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            });

            definitions.Add(new DataGridTextColumnDefinition
            {
                Header = "Age",
                Binding = DataGridBindingDefinition.Create<Person, int>(p => p.Age)
            });

            Assert.Empty(GetNonFillerColumns(grid));
        }

        var columns = GetNonFillerColumns(grid);
        Assert.Equal(2, columns.Count);
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Moves_Columns_When_Definitions_Move()
    {
        var definitions = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "First",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            },
            new DataGridTextColumnDefinition
            {
                Header = "Second",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            },
            new DataGridTextColumnDefinition
            {
                Header = "Third",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            }
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = definitions
        };

        var columns = GetNonFillerColumns(grid);
        Assert.Equal(new[] { "First", "Second", "Third" }, columns.Select(c => c.Header));

        definitions.Move(0, 2);

        columns = GetNonFillerColumns(grid);
        Assert.Equal(new[] { "Second", "Third", "First" }, columns.Select(c => c.Header));
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Replaces_Columns_When_Definitions_Replace()
    {
        var definitions = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "First",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            },
            new DataGridTextColumnDefinition
            {
                Header = "Second",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            }
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = definitions
        };

        var oldColumn = GetNonFillerColumns(grid)[1];

        definitions[1] = new DataGridTextColumnDefinition
        {
            Header = "Replacement",
            Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
        };

        var columns = GetNonFillerColumns(grid);
        Assert.Equal(new[] { "First", "Replacement" }, columns.Select(c => c.Header));
        Assert.DoesNotContain(oldColumn, columns);
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Updates_On_Definition_Change()
    {
        var definition = new DataGridTextColumnDefinition
        {
            Header = "Name",
            Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
        };

        var definitions = new ObservableCollection<DataGridColumnDefinition> { definition };
        var grid = new DataGrid
        {
            ColumnDefinitionsSource = definitions
        };

        var column = Assert.IsType<DataGridTextColumn>(GetNonFillerColumns(grid).Single());
        Assert.Equal("Name", column.Header);

        definition.Header = "Display Name";

        Assert.Equal("Display Name", column.Header);
        Assert.Same(column, GetNonFillerColumns(grid).Single());
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Batches_Definition_Updates()
    {
        var definition = new CountingColumnDefinition
        {
            Header = "Start"
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition> { definition }
        };

        definition.ResetApplyCount();

        definition.BeginUpdate();
        definition.Header = "Updated";
        definition.IsReadOnly = true;
        definition.EndUpdate();

        var column = Assert.IsType<DataGridTextColumn>(GetNonFillerColumns(grid).Single());
        Assert.Equal("Updated", column.Header);
        Assert.True(column.IsReadOnly);
        Assert.Equal(1, definition.ApplyCount);
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Reapplies_ColumnProperties_On_Base_Changes()
    {
        var definition = new CountingColumnDefinition
        {
            Header = "Start"
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition> { definition }
        };

        definition.ResetApplyCount();

        definition.Header = "Updated";

        var column = Assert.IsType<DataGridTextColumn>(GetNonFillerColumns(grid).Single());
        Assert.Equal("Updated", column.Header);
        Assert.Equal(1, definition.ApplyCount);
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Clears_ColumnProperties_When_Definition_Reset()
    {
        var definition = new DataGridSliderColumnDefinition
        {
            ValueTextFormat = "F2",
            Minimum = 10,
            Maximum = 20
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition> { definition }
        };

        var column = Assert.IsType<DataGridSliderColumn>(GetNonFillerColumns(grid).Single());
        Assert.Equal("F2", column.ValueTextFormat);
        Assert.Equal(10, column.Minimum);
        Assert.Equal(20, column.Maximum);

        definition.ValueTextFormat = null;
        definition.Minimum = null;
        definition.Maximum = null;

        Assert.Equal("N0", column.ValueTextFormat);
        Assert.Equal(0, column.Minimum);
        Assert.Equal(100, column.Maximum);
    }

    [Fact]
    public void ColumnDefinition_EndUpdate_Without_BeginUpdate_Throws()
    {
        var definition = new DataGridTextColumnDefinition();

        Assert.Throws<InvalidOperationException>(() => definition.EndUpdate());
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Respects_DisplayIndex()
    {
        var definitions = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "First",
                DisplayIndex = 1,
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            },
            new DataGridTextColumnDefinition
            {
                Header = "Second",
                DisplayIndex = 0,
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            },
            new DataGridTextColumnDefinition
            {
                Header = "Third",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            }
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = definitions
        };

        var columns = GetNonFillerColumns(grid);
        var first = columns.Single(c => Equals(c.Header, "First"));
        var second = columns.Single(c => Equals(c.Header, "Second"));
        var third = columns.Single(c => Equals(c.Header, "Third"));

        Assert.Equal(1, first.DisplayIndex);
        Assert.Equal(0, second.DisplayIndex);
        Assert.Equal(2, third.DisplayIndex);
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Keeps_DisplayIndex_On_Collection_Changes()
    {
        var definitions = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "First",
                DisplayIndex = 1,
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            },
            new DataGridTextColumnDefinition
            {
                Header = "Second",
                DisplayIndex = 0,
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            }
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = definitions
        };

        definitions.Add(new DataGridTextColumnDefinition
        {
            Header = "Third",
            Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
        });

        var columns = GetNonFillerColumns(grid);
        var first = columns.Single(c => Equals(c.Header, "First"));
        var second = columns.Single(c => Equals(c.Header, "Second"));
        var third = columns.Single(c => Equals(c.Header, "Third"));

        Assert.Equal(1, first.DisplayIndex);
        Assert.Equal(0, second.DisplayIndex);
        Assert.Equal(2, third.DisplayIndex);
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Can_Be_Replaced()
    {
        var first = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "First",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            }
        };

        var second = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "Second",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            }
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = first
        };

        grid.ColumnDefinitionsSource = second;

        var columns = GetNonFillerColumns(grid);
        Assert.Single(columns);
        Assert.Equal("Second", columns[0].Header);
    }

    [AvaloniaFact]
    public void TextColumnDefinition_Clears_Font_Properties_When_Unset()
    {
        var definition = new DataGridTextColumnDefinition
        {
            Header = "Name",
            Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name),
            FontSize = 14,
            FontStyle = FontStyle.Italic,
            FontWeight = FontWeight.Bold,
            FontStretch = FontStretch.Expanded
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition> { definition }
        };

        var column = Assert.IsType<DataGridTextColumn>(GetNonFillerColumns(grid).Single());
        Assert.True(column.IsSet(DataGridTextColumn.FontSizeProperty));
        Assert.True(column.IsSet(DataGridTextColumn.FontStyleProperty));
        Assert.True(column.IsSet(DataGridTextColumn.FontWeightProperty));
        Assert.True(column.IsSet(DataGridTextColumn.FontStretchProperty));

        definition.FontSize = null;
        definition.FontStyle = null;
        definition.FontWeight = null;
        definition.FontStretch = null;

        Assert.False(column.IsSet(DataGridTextColumn.FontSizeProperty));
        Assert.False(column.IsSet(DataGridTextColumn.FontStyleProperty));
        Assert.False(column.IsSet(DataGridTextColumn.FontWeightProperty));
        Assert.False(column.IsSet(DataGridTextColumn.FontStretchProperty));
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Does_Not_Set_TextColumn_Foreground_When_Unset()
    {
        var definitions = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "Name",
                Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
            }
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = definitions
        };

        var column = Assert.IsType<DataGridTextColumn>(GetNonFillerColumns(grid).Single());
        Assert.False(column.IsSet(DataGridTextColumn.ForegroundProperty));
    }

    [AvaloniaFact]
    public void BindingDefinition_Provides_ValueAccessor_And_Type()
    {
        var definition = new DataGridTextColumnDefinition
        {
            Header = "Name",
            Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition> { definition }
        };

        var column = Assert.IsType<DataGridTextColumn>(GetNonFillerColumns(grid).Single());
        var accessor = DataGridColumnMetadata.GetValueAccessor(column);
        Assert.NotNull(accessor);
        Assert.Equal(typeof(string), DataGridColumnMetadata.GetValueType(column));

        var person = new Person { Name = "Ada" };
        Assert.Equal("Ada", accessor.GetValue(person));
    }

    [AvaloniaFact]
    public void BindingDefinition_Allows_Prebuilt_Path_And_Accessor()
    {
        var path = BuildNamePath();
        var definition = new DataGridTextColumnDefinition
        {
            Header = "Name",
            Binding = DataGridBindingDefinition.Create<Person, string>(path, GetName, SetName)
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition> { definition }
        };

        var column = Assert.IsType<DataGridTextColumn>(GetNonFillerColumns(grid).Single());
        var binding = Assert.IsType<CompiledBindingExtension>(column.Binding);
        Assert.Equal(nameof(Person.Name), binding.Path?.ToString());

        var accessor = DataGridColumnMetadata.GetValueAccessor(column);
        var person = new Person { Name = "Ada" };
        Assert.Equal("Ada", accessor.GetValue(person));
        accessor.SetValue(person, "Grace");
        Assert.Equal("Grace", person.Name);
    }

    [AvaloniaFact]
    public void BindingDefinition_Allows_PropertyInfo_Path()
    {
        var propertyInfo = new ClrPropertyInfo(
            nameof(Person.Name),
            target => ((Person)target).Name,
            (target, value) => ((Person)target).Name = (string)value,
            typeof(string));

        var definition = new DataGridTextColumnDefinition
        {
            Header = "Name",
            Binding = DataGridBindingDefinition.Create<Person, string>(propertyInfo, GetName, SetName)
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition> { definition }
        };

        var column = Assert.IsType<DataGridTextColumn>(GetNonFillerColumns(grid).Single());
        var binding = Assert.IsType<CompiledBindingExtension>(column.Binding);
        Assert.Equal(nameof(Person.Name), binding.Path?.ToString());
    }

    [Fact]
    public void CompiledBindingPathCache_Reuses_Path_For_Same_PropertyInfo()
    {
        var propertyInfo = new ClrPropertyInfo(
            nameof(Person.Name),
            target => ((Person)target).Name,
            (target, value) => ((Person)target).Name = (string)value,
            typeof(string));

        var first = DataGridCompiledBindingPathCache.GetOrCreate(propertyInfo);
        var second = DataGridCompiledBindingPathCache.GetOrCreate(propertyInfo);

        Assert.Same(first, second);
    }

    [Fact]
    public void BindingDefinition_CreateCached_Uses_Cached_Path()
    {
        var propertyInfo = new ClrPropertyInfo(
            nameof(Person.Name),
            target => ((Person)target).Name,
            (target, value) => ((Person)target).Name = (string)value,
            typeof(string));

        var cached = DataGridCompiledBindingPathCache.GetOrCreate(propertyInfo);
        var bindingDefinition = DataGridBindingDefinition.CreateCached<Person, string>(propertyInfo, GetName, SetName);
        var binding = Assert.IsType<CompiledBindingExtension>(bindingDefinition.CreateBinding());

        Assert.Same(cached, binding.Path);
    }

    [AvaloniaFact]
    public void BindingDefinition_Binds_To_TextBlock()
    {
        var propertyInfo = new ClrPropertyInfo(
            nameof(Person.Name),
            target => ((Person)target).Name,
            (target, value) => ((Person)target).Name = (string)value,
            typeof(string));

        var bindingDefinition = DataGridBindingDefinition.Create<Person, string>(propertyInfo, GetName, SetName);
        var binding = bindingDefinition.CreateBinding();

        var textBlock = new TextBlock
        {
            DataContext = new Person { Name = "Ada" }
        };

        textBlock.Bind(TextBlock.TextProperty, binding);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Ada", textBlock.Text);
    }

    [AvaloniaFact]
    public void ColumnDefinitionBuilder_Creates_Bound_TextColumn()
    {
        var propertyInfo = new ClrPropertyInfo(
            nameof(Person.Name),
            target => ((Person)target).Name,
            (target, value) => ((Person)target).Name = (string)value,
            typeof(string));

        var definition = DataGridColumnDefinitionBuilder.For<Person>()
            .Text("Name", propertyInfo, GetName, SetName);

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition> { definition }
        };

        var column = Assert.IsType<DataGridTextColumn>(GetNonFillerColumns(grid).Single());
        var accessor = DataGridColumnMetadata.GetValueAccessor(column);
        Assert.NotNull(accessor);
        Assert.Equal("Ada", accessor.GetValue(new Person { Name = "Ada" }));
    }

    [AvaloniaFact]
    public void ColumnKey_Maps_To_Metadata_Id()
    {
        var definition = new DataGridTextColumnDefinition
        {
            Header = "Name",
            ColumnKey = "name-key",
            Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition> { definition }
        };

        var column = Assert.IsType<DataGridTextColumn>(GetNonFillerColumns(grid).Single());
        Assert.True(DataGridColumnMetadata.MatchesColumnId(column, "name-key"));
        Assert.True(DataGridColumnMetadata.MatchesColumnId(column, definition));
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Binds_Text_In_DataGrid()
    {
        var propertyInfo = new ClrPropertyInfo(
            nameof(Person.Name),
            target => ((Person)target).Name,
            (target, value) => ((Person)target).Name = (string)value,
            typeof(string));

        var definitions = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "Name",
                Binding = DataGridBindingDefinition.Create<Person, string>(propertyInfo, GetName, SetName)
            }
        };

        var items = new ObservableCollection<Person>
        {
            new Person { Name = "Ada" }
        };

        var root = new Window
        {
            Width = 300,
            Height = 200
        };
        root.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ColumnDefinitionsSource = definitions,
            ItemsSource = items
        };

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        try
        {
            var cell = grid.GetVisualDescendants().OfType<DataGridCell>().First();
            var textBlock = Assert.IsAssignableFrom<TextBlock>(cell.Content);
            Assert.Equal("Ada", textBlock.Text);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void TextColumn_DisplayBinding_Uses_OneTime_Mode()
    {
        var person = new NotifyingPerson { Name = "Ada" };
        var column = new TestTextColumn
        {
            Binding = new Binding(nameof(NotifyingPerson.Name))
            {
                Mode = BindingMode.OneTime
            }
        };

        var cell = new DataGridCell();
        var element = column.GenerateElementPublic(cell, person);
        var textBlock = Assert.IsAssignableFrom<TextBlock>(element);

        textBlock.DataContext = person;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Ada", textBlock.Text);

        person.Name = "Grace";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Ada", textBlock.Text);
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Resolves_Template_Keys()
    {
        var grid = new DataGrid();

        var headerTemplate = new FuncDataTemplate<object>((_, _) => new TextBlock());
        var cellTemplate = new FuncDataTemplate<object>((_, _) => new TextBlock());
        var editTemplate = new FuncDataTemplate<object>((_, _) => new TextBox());

        grid.Resources["HeaderTemplate"] = headerTemplate;
        grid.Resources["CellTemplate"] = cellTemplate;
        grid.Resources["EditTemplate"] = editTemplate;

        grid.ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTemplateColumnDefinition
            {
                Header = "Template",
                HeaderTemplateKey = "HeaderTemplate",
                CellTemplateKey = "CellTemplate",
                CellEditingTemplateKey = "EditTemplate"
            }
        };

        var column = Assert.IsType<DataGridTemplateColumn>(GetNonFillerColumns(grid).Single());
        Assert.Same(headerTemplate, column.HeaderTemplate);
        Assert.Same(cellTemplate, column.CellTemplate);
        Assert.Same(editTemplate, column.CellEditingTemplate);
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Defers_Template_Resolution_When_Resources_Applied_Later()
    {
        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition>
            {
                new DataGridTemplateColumnDefinition
                {
                    Header = "Template",
                    CellTemplateKey = "CellTemplate"
                }
            }
        };

        var column = Assert.IsType<DataGridTemplateColumn>(GetNonFillerColumns(grid).Single());
        Assert.NotNull(column.CellTemplate);

        grid.Resources["CellTemplate"] = new FuncDataTemplate<object>((_, _) => new TextBlock());

        var built = column.CellTemplate.Build(new object());
        Assert.IsType<TextBlock>(built);
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Rejects_Inline_Columns()
    {
        var grid = new DataGrid();
        grid.Columns.Add(new DataGridTextColumn { Header = "Inline", Binding = new Binding("Name") });

        Assert.Throws<InvalidOperationException>(() =>
            grid.ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition>());
    }

    [AvaloniaFact]
    public void ColumnDefinitionsSource_Rejects_Bound_Columns()
    {
        var grid = new DataGrid
        {
            Columns = new ObservableCollection<DataGridColumn>
            {
                new DataGridTextColumn { Header = "Bound", Binding = new Binding("Name") }
            }
        };

        Assert.Throws<InvalidOperationException>(() =>
            grid.ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition>());
    }

    [AvaloniaFact]
    public void AutoGeneratedColumnsPlacement_BeforeSource_Uses_Definitions()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = true,
            AutoGeneratedColumnsPlacement = AutoGeneratedColumnsPlacement.BeforeSource,
            ItemsSource = new[] { new AutoPerson { Name = "A", Age = 1 } }
        };

        grid.ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "Manual",
                Binding = DataGridBindingDefinition.Create<AutoPerson, string>(p => p.Name)
            }
        };

        typeof(DataGrid)
            .GetField("_measured", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grid, true);

        typeof(DataGrid)
            .GetMethod("AutoGenerateColumnsPrivate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(grid, null);

        var columns = GetNonFillerColumns(grid);
        Assert.Equal(3, columns.Count);
        Assert.Equal(2, columns.Count(c => c.IsAutoGenerated));
        Assert.Equal(1, columns.Count(c => !c.IsAutoGenerated));
        Assert.True(columns[0].IsAutoGenerated);
        Assert.True(columns[1].IsAutoGenerated);
        Assert.False(columns[2].IsAutoGenerated);
    }

    [AvaloniaFact]
    public void ValueAccessor_Controls_CanUserSort()
    {
        var accessor = new DataGridColumnValueAccessor<Person, NonComparable>(p => p.Token);
        var definition = new DataGridTextColumnDefinition
        {
            Header = "Token",
            Binding = DataGridBindingDefinition.Create<Person, NonComparable>(p => p.Token),
            ValueAccessor = accessor
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition> { definition }
        };

        var column = Assert.IsType<DataGridTextColumn>(GetNonFillerColumns(grid).Single());
        Assert.Same(accessor, DataGridColumnMetadata.GetValueAccessor(column));
        Assert.Equal(typeof(NonComparable), DataGridColumnMetadata.GetValueType(column));
        Assert.False(column.CanUserSort);
    }

    private sealed class CountingColumnDefinition : DataGridColumnDefinition
    {
        public int ApplyCount { get; private set; }

        public void ResetApplyCount()
        {
            ApplyCount = 0;
        }

        protected override DataGridColumn CreateColumnCore()
        {
            return new DataGridTextColumn();
        }

        protected override void ApplyColumnProperties(DataGridColumn column, DataGridColumnDefinitionContext context)
        {
            ApplyCount++;
        }
    }

    private static System.Collections.Generic.List<DataGridColumn> GetNonFillerColumns(DataGrid grid)
    {
        return grid.ColumnsInternal.ItemsInternal
            .Where(column => column is not DataGridFillerColumn)
            .ToList();
    }

    private sealed class Person
    {
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public int Age { get; set; }

        public NonComparable Token { get; set; } = new();
    }

    private sealed class NonComparable
    {
    }

    private sealed class NotifyingPerson : INotifyPropertyChanged
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    private sealed class TestTextColumn : DataGridTextColumn
    {
        public Control GenerateElementPublic(DataGridCell cell, object dataItem)
        {
            return GenerateElement(cell, dataItem);
        }
    }

    private sealed class AutoPerson
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }

    private static string GetName(Person person) => person.Name;

    private static void SetName(Person person, string value) => person.Name = value;

    private static CompiledBindingPath BuildNamePath()
    {
        var info = new ClrPropertyInfo(
            nameof(Person.Name),
            target => ((Person)target).Name,
            (target, value) => ((Person)target).Name = (string)value,
            typeof(string));
        return new CompiledBindingPathBuilder()
            .Property(info, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
            .Build();
    }
}
