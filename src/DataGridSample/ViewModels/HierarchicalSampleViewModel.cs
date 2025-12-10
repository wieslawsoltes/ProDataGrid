// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls.DataGridHierarchical;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class HierarchicalSampleViewModel : ObservableObject
    {
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

        public HierarchicalSampleViewModel()
        {
            var rootPath = Path.GetPathRoot(Environment.CurrentDirectory) ?? "/";
            var root = CreateRoot(rootPath);
            var options = new HierarchicalOptions
            {
                ItemsSelector = item =>
                {
                    if (item is TreeItem tree)
                    {
                        if (!tree.IsDirectory)
                        {
                            return null;
                        }

                        EnsureChildren(tree, allowRescan: true);
                        return tree.Children;
                    }

                    return null;
                },
                AutoExpandRoot = true,
                MaxAutoExpandDepth = 0,
                VirtualizeChildren = true,
                SiblingComparer = new FileSystemNodeComparer(),
                IsLeafSelector = item =>
                {
                    if (item is TreeItem tree)
                    {
                        if (!tree.IsDirectory)
                        {
                            return true;
                        }

                        return tree.HasScanned && tree.Children.Count == 0;
                    }

                    if (item is FileSystemInfo fsi)
                    {
                        return (fsi.Attributes & FileAttributes.Directory) == 0;
                    }

                    return false;
                }
            };

            Model = new HierarchicalModel(options);
            var adapter = new DataGridHierarchicalAdapter(Model);
            adapter.FlattenedChanged += (_, __) => OnPropertyChanged(nameof(Rows));
            Model.SetRoot(root);

            ExpandAllCommand = new RelayCommand(_ =>
            {
                Model.ExpandAll();
                OnPropertyChanged(nameof(Rows));
            });
            CollapseAllCommand = new RelayCommand(_ =>
            {
                Model.CollapseAll();
                OnPropertyChanged(nameof(Rows));
            });
            RefreshRootCommand = new RelayCommand(_ =>
            {
                Model.Refresh(Model.Root);
                OnPropertyChanged(nameof(Rows));
            });
        }

        public IHierarchicalModel Model { get; }

        public IEnumerable<HierarchicalNode> Rows => Model.Flattened;

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

        private sealed class FileSystemNodeComparer : IComparer<object>
        {
            public int Compare(object? x, object? y)
            {
                var a = x as TreeItem;
                var b = y as TreeItem;

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
