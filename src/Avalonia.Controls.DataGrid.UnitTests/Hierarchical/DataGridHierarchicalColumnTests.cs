// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Hierarchical;

public class DataGridHierarchicalColumnTests
{
    [AvaloniaFact]
    public void Presenter_Updates_Padding_From_Level_And_Indent()
    {
        var presenter = new DataGridHierarchicalPresenter
        {
            Indent = 8,
            Level = 2
        };

        Assert.Equal(new Thickness(16, 0, 0, 0), presenter.Padding);

        presenter.Indent = 10;

        Assert.Equal(new Thickness(20, 0, 0, 0), presenter.Padding);
    }

    [AvaloniaFact]
    public void Presenter_Reapplies_Padding_On_DataContext_Change()
    {
        var presenter = new DataGridHierarchicalPresenter
        {
            Indent = 8
        };

        presenter.Bind(DataGridHierarchicalPresenter.LevelProperty, new Binding(nameof(HierarchicalNode.Level)));

        var nodeA = new HierarchicalNode(new object(), level: 1);
        var nodeB = new HierarchicalNode(new object(), level: 1);

        presenter.DataContext = nodeA;
        Assert.Equal(new Thickness(8, 0, 0, 0), presenter.Padding);

        presenter.Padding = new Thickness(123, 0, 0, 0);
        presenter.DataContext = nodeB;

        Assert.Equal(new Thickness(8, 0, 0, 0), presenter.Padding);
    }

    [AvaloniaFact]
    public void Presenter_Raises_ToggleRequested_On_Click()
    {
        var presenter = new DataGridHierarchicalPresenter
        {
            Template = new FuncControlTemplate<DataGridHierarchicalPresenter>((owner, scope) =>
            {
                var toggle = new ToggleButton
                {
                    Name = "PART_Expander"
                };
                scope.Register(toggle.Name, toggle);

                return new Grid
                {
                    Children = { toggle }
                };
            })
        };

        bool raised = false;
        presenter.ToggleRequested += (_, _) => raised = true;

        presenter.ApplyTemplate();

        var toggleButton = presenter.GetTemplateChildren().OfType<ToggleButton>().Single();
        toggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(raised);
    }
}
