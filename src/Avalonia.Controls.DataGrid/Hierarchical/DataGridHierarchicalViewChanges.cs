// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Avalonia.Controls.DataGridHierarchical
{
    /// <summary>
    /// Collects the row insertions and removals a filtered collection view reports for a
    /// hierarchical source, in view indexes, until the grid applies the matching flattened change.
    /// </summary>
    internal sealed class DataGridHierarchicalViewChanges
    {
        private int _index = -1;
        private int _oldCount;
        private int _newCount;

        /// <summary>
        /// Gets whether the view reported a change that is not a row insertion or removal, such as a reset.
        /// </summary>
        public bool RequiresRefresh { get; private set; }

        public void Record(NotifyCollectionChangedEventArgs e)
        {
            if (RequiresRefresh)
            {
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null && e.NewStartingIndex >= 0)
            {
                Include(e.NewStartingIndex, removed: 0, inserted: e.NewItems.Count);
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null && e.OldStartingIndex >= 0)
            {
                Include(e.OldStartingIndex, removed: e.OldItems.Count, inserted: 0);
            }
            else
            {
                RequiresRefresh = true;
            }
        }

        public void MarkRefreshRequired()
        {
            RequiresRefresh = true;
        }

        public void Clear()
        {
            _index = -1;
            _oldCount = 0;
            _newCount = 0;
            RequiresRefresh = false;
        }

        public bool Matches(IReadOnlyList<FlattenedChange> changes)
        {
            return changes.Count == 1 &&
                _index == changes[0].Index &&
                _oldCount == changes[0].OldCount &&
                _newCount == changes[0].NewCount;
        }

        public IReadOnlyList<FlattenedChange> ToChanges()
        {
            return _oldCount == 0 && _newCount == 0
                ? Array.Empty<FlattenedChange>()
                : new[] { new FlattenedChange(_index, _oldCount, _newCount) };
        }

        private void Include(int index, int removed, int inserted)
        {
            if (_index < 0)
            {
                _index = index;
                _oldCount = removed;
                _newCount = inserted;
                return;
            }

            // Widen the pending change to the smallest row range that also covers this
            // notification. Notifications use the row indexes left by the previous ones, and the
            // rows absorbed between them are counted as both removed and inserted.
            var start = Math.Min(_index, index);
            var end = Math.Max(_index + _newCount, index + removed);
            var absorbed = (_index - start) + (end - _index - _newCount);
            _index = start;
            _oldCount += absorbed;
            _newCount += absorbed - removed + inserted;
        }
    }
}
