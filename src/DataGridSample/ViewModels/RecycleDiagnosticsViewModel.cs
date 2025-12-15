using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels;

public class RecycleDiagnosticsViewModel : ObservableObject
{
    private int _realizedRows;
    private int _recycledRows;
    private double _viewportHeight;
    private bool _trimRecycledContainers = false;
    private bool _keepRecycledContainersInVisualTree = true;
    private DataGridRecycleHidingMode _recycledContainerHidingMode = DataGridRecycleHidingMode.MoveOffscreen;

    public RecycleDiagnosticsViewModel()
    {
        Items = new ObservableCollection<PixelItem>();
        Populate(500);
    }

    public ObservableCollection<PixelItem> Items { get; }

    public int RealizedRows
    {
        get => _realizedRows;
        set => SetProperty(ref _realizedRows, value);
    }

    public int RecycledRows
    {
        get => _recycledRows;
        set => SetProperty(ref _recycledRows, value);
    }

    public double ViewportHeight
    {
        get => _viewportHeight;
        set => SetProperty(ref _viewportHeight, value);
    }

    public bool TrimRecycledContainers
    {
        get => _trimRecycledContainers;
        set => SetProperty(ref _trimRecycledContainers, value);
    }

    public bool KeepRecycledContainersInVisualTree
    {
        get => _keepRecycledContainersInVisualTree;
        set => SetProperty(ref _keepRecycledContainersInVisualTree, value);
    }

    public DataGridRecycleHidingMode RecycledContainerHidingMode
    {
        get => _recycledContainerHidingMode;
        set => SetProperty(ref _recycledContainerHidingMode, value);
    }

    public Array RecycleHidingModes { get; } = Enum.GetValues(typeof(DataGridRecycleHidingMode));

    private void Populate(int count)
    {
        Items.Clear();

        var random = new Random(17);
        for (int i = 1; i <= count; i++)
        {
            Items.Add(PixelItem.Create(i, random));
        }
    }
}
