// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Diagnostics;

namespace Avalonia.Controls
{
    public partial class DataGrid
    {
        // Smooth scroll correction state
        private double _pendingScrollCorrection;
        private const double MaxScrollCorrectionPerFrame = 2.0;
        private const double ScrollCorrectionThreshold = 0.5;

        /// <summary>
        /// Gets the estimated or actual height for a slot without forcing realization.
        /// This is used for scroll calculations to avoid triggering InsertDisplayedElement.
        /// </summary>
        internal double GetEstimatedSlotHeight(int slot)
        {
            Debug.Assert(slot >= 0 && slot < SlotCount);
            
            // If visible, use actual height
            if (IsSlotVisible(slot))
            {
                return DisplayData.GetDisplayedElement(slot).DesiredSize.Height;
            }

            // Use estimator if available
            var estimator = RowHeightEstimator;
            if (estimator != null)
            {
                var rowGroupInfo = RowGroupHeadersTable.GetValueAt(slot);
                bool isHeader = rowGroupInfo != null;
                int level = isHeader ? rowGroupInfo!.Level : 0;
                bool hasDetails = !isHeader && GetRowDetailsVisibility(slot);
                return estimator.GetEstimatedHeight(slot, isHeader, level, hasDetails);
            }

            // Fallback to simple estimation
            return GetSlotElementHeight(slot);
        }

        /// <summary>
        /// Adds a scroll position correction to be applied gradually.
        /// </summary>
        internal void AddScrollCorrection(double correction)
        {
            _pendingScrollCorrection += correction;
        }

        /// <summary>
        /// Applies any pending scroll corrections gradually.
        /// Call this from render/frame update for smooth correction.
        /// </summary>
        internal void ApplyPendingScrollCorrection()
        {
            if (Math.Abs(_pendingScrollCorrection) < ScrollCorrectionThreshold)
            {
                _pendingScrollCorrection = 0;
                return;
            }

            double correction = Math.Sign(_pendingScrollCorrection) *
                Math.Min(Math.Abs(_pendingScrollCorrection), MaxScrollCorrectionPerFrame);

            _pendingScrollCorrection -= correction;
            
            // Apply the correction
            _verticalOffset += correction;
            SetVerticalOffset(_verticalOffset);
        }

        /// <summary>
        /// Whether there are pending scroll corrections.
        /// </summary>
        internal bool HasPendingScrollCorrection => Math.Abs(_pendingScrollCorrection) >= ScrollCorrectionThreshold;
    }
}
