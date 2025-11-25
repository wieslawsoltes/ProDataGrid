// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;

namespace Avalonia.Controls.Primitives
{
    /// <summary>
    /// IScrollAnchorProvider implementation for DataGridRowsPresenter.
    /// This partial class provides scroll anchoring support to maintain scroll position
    /// during layout changes by tracking an anchor element.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    sealed partial class DataGridRowsPresenter : IScrollAnchorProvider
    {
        private Control? _currentAnchor;
        private HashSet<Control>? _anchorCandidates;

        #region IScrollAnchorProvider Implementation

        /// <summary>
        /// Gets the currently chosen anchor element to use for scroll anchoring.
        /// The anchor is typically the first visible row that can be used to maintain
        /// scroll position during layout changes.
        /// </summary>
        public Control? CurrentAnchor
        {
            get
            {
                // If we have an explicitly set anchor, use it
                if (_currentAnchor != null && _anchorCandidates?.Contains(_currentAnchor) == true)
                {
                    return _currentAnchor;
                }

                // Otherwise, find the first visible row as anchor
                return FindBestAnchor();
            }
        }

        /// <summary>
        /// Registers a control as a potential scroll anchor candidate.
        /// Rows and row group headers register themselves as candidates.
        /// </summary>
        /// <param name="element">A control within the DataGridRowsPresenter subtree.</param>
        public void RegisterAnchorCandidate(Control element)
        {
            if (element is DataGridRow || element is DataGridRowGroupHeader)
            {
                _anchorCandidates ??= new HashSet<Control>();
                _anchorCandidates.Add(element);
            }
        }

        /// <summary>
        /// Unregisters a control as a potential scroll anchor candidate.
        /// Called when rows are recycled or removed from the visual tree.
        /// </summary>
        /// <param name="element">A control within the DataGridRowsPresenter subtree.</param>
        public void UnregisterAnchorCandidate(Control element)
        {
            _anchorCandidates?.Remove(element);
            
            if (_currentAnchor == element)
            {
                _currentAnchor = null;
            }
        }

        #endregion

        #region Scroll Anchor Helper Methods

        /// <summary>
        /// Finds the best anchor element, typically the first fully visible row.
        /// </summary>
        private Control? FindBestAnchor()
        {
            if (OwningGrid == null || _anchorCandidates == null || _anchorCandidates.Count == 0)
            {
                return null;
            }

            // Get the first visible displayed element as the anchor
            var firstSlot = OwningGrid.DisplayData.FirstScrollingSlot;
            if (firstSlot >= 0)
            {
                var element = OwningGrid.DisplayData.GetDisplayedElement(firstSlot);
                if (element != null && _anchorCandidates.Contains(element))
                {
                    return element;
                }
            }

            return null;
        }

        /// <summary>
        /// Sets the current anchor explicitly. Used when we want to maintain
        /// scroll position relative to a specific element.
        /// </summary>
        /// <param name="anchor">The element to use as the anchor.</param>
        internal void SetAnchor(Control? anchor)
        {
            _currentAnchor = anchor;
        }

        /// <summary>
        /// Clears all anchor candidates. Called when the DataGrid is being reset.
        /// </summary>
        internal void ClearAnchorCandidates()
        {
            _anchorCandidates?.Clear();
            _currentAnchor = null;
        }

        /// <summary>
        /// Gets the anchor's position relative to the viewport.
        /// Used to restore scroll position after layout changes.
        /// </summary>
        /// <returns>The Y offset of the anchor from the top of the viewport, or null if no anchor.</returns>
        internal double? GetAnchorOffset()
        {
            var anchor = CurrentAnchor;
            if (anchor == null)
            {
                return null;
            }

            // Get the anchor's position relative to this presenter
            var transform = anchor.TransformToVisual(this);
            if (transform.HasValue)
            {
                return transform.Value.Transform(new Point(0, 0)).Y;
            }

            return null;
        }

        #endregion
    }
}
