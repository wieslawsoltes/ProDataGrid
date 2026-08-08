// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using Avalonia.Input;

namespace Avalonia.Controls
{
    /// <summary>Identifies command-oriented actions exposed by a generated DataGrid input map.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedInputAction
    {
        /// <summary>No generated action.</summary>
        None,
        /// <summary>Focus or open the generated search surface.</summary>
        Search,
        /// <summary>Fill the active selection downward.</summary>
        FillDown,
        /// <summary>Fill the active selection to the right.</summary>
        FillRight,
        /// <summary>Undo the latest domain edit.</summary>
        Undo,
        /// <summary>Redo the latest domain edit.</summary>
        Redo
    }

    /// <summary>Describes a typed generated input command without exposing a DataGrid to the ViewModel.</summary>
    /// <typeparam name="TItem">The generated row type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedInputEvent<TItem>
    {
        /// <summary>Initializes an input command payload.</summary>
        public DataGridGeneratedInputEvent(
            DataGridGeneratedInputAction action,
            Key key,
            KeyModifiers keyModifiers,
            TItem item,
            int rowIndex,
            int columnIndex)
        {
            Action = action;
            Key = key;
            KeyModifiers = keyModifiers;
            Item = item;
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
        }

        /// <summary>Gets the matched generated action.</summary>
        public DataGridGeneratedInputAction Action { get; }
        /// <summary>Gets the physical key reported by Avalonia.</summary>
        public Key Key { get; }
        /// <summary>Gets the active key modifiers.</summary>
        public KeyModifiers KeyModifiers { get; }
        /// <summary>Gets the current typed row, or its default value when no row is current.</summary>
        public TItem Item { get; }
        /// <summary>Gets the current row index, or -1 when no row is current.</summary>
        public int RowIndex { get; }
        /// <summary>Gets the current display column index, or -1 when no column is current.</summary>
        public int ColumnIndex { get; }
        /// <summary>Gets or sets whether the generated view should mark the key event handled.</summary>
        public bool Handled { get; set; } = true;
    }

    /// <summary>Creates built-in gesture overrides and matches command-oriented generated gestures.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedInputMap
    {
        /// <summary>Creates overrides for DataGrid's built-in navigation and editing gestures.</summary>
        DataGridKeyboardGestures CreateKeyboardGestureOverrides(KeyModifiers commandModifiers);

        /// <summary>Matches a command-oriented gesture without allocating or using reflection.</summary>
        bool TryMatch(
            Key key,
            KeyModifiers keyModifiers,
            KeyModifiers commandModifiers,
            out DataGridGeneratedInputAction action);
    }

    /// <summary>Provides the default generated keyboard map for an explicit performance profile.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedInputMap : IDataGridGeneratedInputMap
    {
        private readonly DataGridGeneratedPerformanceProfile _profile;

        private DataGridGeneratedInputMap(DataGridGeneratedPerformanceProfile profile)
        {
            _profile = profile;
        }

        /// <summary>Creates the default input map for a named generated performance profile.</summary>
        public static IDataGridGeneratedInputMap Create(DataGridGeneratedPerformanceProfile profile) => new DataGridGeneratedInputMap(profile);

        /// <inheritdoc />
        public DataGridKeyboardGestures CreateKeyboardGestureOverrides(KeyModifiers commandModifiers)
        {
            _ = commandModifiers;
            return new DataGridKeyboardGestures();
        }

        /// <inheritdoc />
        public bool TryMatch(
            Key key,
            KeyModifiers keyModifiers,
            KeyModifiers commandModifiers,
            out DataGridGeneratedInputAction action)
        {
            if (Matches(key, keyModifiers, Key.F, commandModifiers))
            {
                action = DataGridGeneratedInputAction.Search;
                return true;
            }

            if (_profile == DataGridGeneratedPerformanceProfile.Spreadsheet)
            {
                if (Matches(key, keyModifiers, Key.D, commandModifiers))
                {
                    action = DataGridGeneratedInputAction.FillDown;
                    return true;
                }
                if (Matches(key, keyModifiers, Key.R, commandModifiers))
                {
                    action = DataGridGeneratedInputAction.FillRight;
                    return true;
                }
                if (Matches(key, keyModifiers, Key.Z, commandModifiers))
                {
                    action = DataGridGeneratedInputAction.Undo;
                    return true;
                }
                if (Matches(key, keyModifiers, Key.Y, commandModifiers) ||
                    Matches(key, keyModifiers, Key.Z, commandModifiers | KeyModifiers.Shift))
                {
                    action = DataGridGeneratedInputAction.Redo;
                    return true;
                }
            }

            action = DataGridGeneratedInputAction.None;
            return false;
        }

        private static bool Matches(
            Key actualKey,
            KeyModifiers actualModifiers,
            Key expectedKey,
            KeyModifiers expectedModifiers) =>
            actualKey == expectedKey && actualModifiers == expectedModifiers;
    }
}
