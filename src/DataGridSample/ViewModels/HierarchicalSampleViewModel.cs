// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSorting;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class HierarchicalSampleViewModel : ObservableObject
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        public record TreeItem(
            string Name,
            string FullPath,
            bool IsDirectory,
            string Kind,
            long Size,
            DateTime Modified,
            ObservableCollection<TreeItem> Children)
        {
            public bool HasScanned { get; set; }
            public bool ScanFailed { get; set; }
        }

        private const int MaxEntriesPerFolder = 200;

        private readonly EnumerationOptions _enumerationOptions = new()
        {
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
        };
        private readonly IComparer<TreeItem> _defaultComparer = new FileSystemNodeComparer();

        public HierarchicalSampleViewModel()
        {
            var rootPath = Path.GetPathRoot(Environment.CurrentDirectory) ?? "/";
            var root = CreateRoot(rootPath);
            SortingModel = new SortingModel();
            SortingModel.SortingChanged += OnSortingChanged;
            var options = new HierarchicalOptions<TreeItem>
            {
                ItemsSelector = item =>
                {
                    if (!item.IsDirectory)
                    {
                        return null;
                    }

                    EnsureChildren(item, allowRescan: true);
                    return item.Children;
                },
                AutoExpandRoot = true,
                MaxAutoExpandDepth = 0,
                VirtualizeChildren = true,
                SiblingComparer = _defaultComparer,
                IsLeafSelector = item =>
                {
                    if (!item.IsDirectory)
                    {
                        return true;
                    }

                    return item.HasScanned && item.Children.Count == 0;
                }
            };

            Model = new HierarchicalModel<TreeItem>(options);
            Model.SetRoot(root);

            ExpandAllCommand = new RelayCommand(_ =>
            {
                Model.ExpandAll();
            });
            CollapseAllCommand = new RelayCommand(_ =>
            {
                Model.CollapseAll();
            });
            RefreshRootCommand = new RelayCommand(_ =>
            {
                Model.Refresh(Model.Root);
            });
        }

        public HierarchicalModel<TreeItem> Model { get; }

        public IComparer<TreeItem> DefaultComparer => _defaultComparer;

        public ISortingModel SortingModel { get; }

        public RelayCommand ExpandAllCommand { get; }

        public RelayCommand CollapseAllCommand { get; }

        public RelayCommand RefreshRootCommand { get; }

        private TreeItem CreateRoot(string path)
        {
            var info = new DirectoryInfo(path);
            return new TreeItem(
                info.Name,
                info.FullName,
                IsDirectory: true,
                GetKind(info),
                Size: 0,
                Modified: info.LastWriteTime,
                Children: new ObservableCollection<TreeItem>());
        }

        private void EnsureChildren(TreeItem parent, bool allowRescan = false)
        {
            if (!parent.IsDirectory || parent.ScanFailed)
            {
                return;
            }

            if (parent.HasScanned && (!allowRescan || parent.Children.Count > 0))
            {
                return;
            }

            parent.HasScanned = true;
            parent.Children.Clear();

            try
            {
                var info = new DirectoryInfo(parent.FullPath);
                var entries = info.EnumerateFileSystemInfos("*", _enumerationOptions)
                    .Take(MaxEntriesPerFolder);

                foreach (var entry in entries)
                {
                    var child = CreateTreeItem(entry);
                    if (child != null)
                    {
                        parent.Children.Add(child);
                    }
                }

                parent.ScanFailed = false;
            }
            catch
            {
                parent.ScanFailed = true;
                // Ignore directories we can't read in the sample.
            }
        }

        private TreeItem? CreateTreeItem(FileSystemInfo entry)
        {
            try
            {
                if (entry is DirectoryInfo dir)
                {
                    return new TreeItem(
                        dir.Name,
                        dir.FullName,
                        IsDirectory: true,
                        GetKind(dir),
                        Size: 0,
                        Modified: dir.LastWriteTime,
                        Children: new ObservableCollection<TreeItem>());
                }

                if (entry is FileInfo file)
                {
                    return new TreeItem(
                        file.Name,
                        file.FullName,
                        IsDirectory: false,
                        GetKind(file),
                        file.Length,
                        file.LastWriteTime,
                        new ObservableCollection<TreeItem>());
                }
            }
            catch
            {
                // Ignore files we can't read in the sample.
            }

            return null;
        }

        private static string GetKind(FileSystemInfo info)
        {
            if (info is DirectoryInfo)
            {
                return "Folder";
            }

            if (info is FileInfo file)
            {
                var ext = file.Extension;
                if (string.IsNullOrWhiteSpace(ext))
                {
                    return "File";
                }

                return ext.TrimStart('.').ToUpperInvariant();
            }

            return "Item";
        }

        private void OnSortingChanged(object? sender, SortingChangedEventArgs e)
        {
            var descriptors = e.NewDescriptors;

            if (descriptors == null || descriptors.Count == 0)
            {
                Model.ApplySiblingComparer(_defaultComparer, recursive: true);
                return;
            }

            var comparer = BuildComparer(descriptors);
            Model.ApplySiblingComparer(comparer, recursive: true);
        }

        private static IComparer<TreeItem> BuildComparer(IReadOnlyList<SortingDescriptor> descriptors)
        {
            return Comparer<TreeItem>.Create((x, y) =>
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                if (x.IsDirectory != y.IsDirectory)
                {
                    return x.IsDirectory ? -1 : 1;
                }

                foreach (var descriptor in descriptors)
                {
                    var path = descriptor.PropertyPath;
                    var result = CompareByPath(x, y, path);
                    if (result != 0)
                    {
                        return descriptor.Direction == ListSortDirection.Descending ? -result : result;
                    }
                }

                return 0;
            });
        }

        private static int CompareByPath(TreeItem left, TreeItem right, string? propertyPath)
        {
            switch (propertyPath)
            {
                case "Item.Name":
                case "Name":
                    return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
                case "Item.Kind":
                case "Kind":
                    return string.Compare(left.Kind, right.Kind, StringComparison.OrdinalIgnoreCase);
                case "Item.Size":
                case "Size":
                    return left.Size.CompareTo(right.Size);
                case "Item.Modified":
                case "Modified":
                    return left.Modified.CompareTo(right.Modified);
                default:
                    return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        private sealed class FileSystemNodeComparer : IComparer<TreeItem>
        {
            public int Compare(TreeItem? a, TreeItem? b)
            {
                if (a == null && b == null) return 0;
                if (a == null) return -1;
                if (b == null) return 1;

                if (a.IsDirectory != b.IsDirectory)
                {
                    return a.IsDirectory ? -1 : 1; // folders first
                }

                var kindCompare = string.Compare(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase);
                if (kindCompare != 0)
                {
                    return kindCompare;
                }

                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
