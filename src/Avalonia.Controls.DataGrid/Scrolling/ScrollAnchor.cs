// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;

namespace Avalonia.Controls
{
    /// <summary>
    /// Represents a scroll anchor point that can be used to maintain scroll position
    /// during virtualization changes. The anchor remembers a reference row and its
    /// visual position relative to the viewport.
    /// </summary>
    internal readonly struct ScrollAnchor
    {
        /// <summary>
        /// The slot index of the anchor row. -1 if no anchor is set.
        /// </summary>
        public int Slot { get; }

        /// <summary>
        /// How much of this row is scrolled out of view (above the viewport).
        /// This corresponds to NegVerticalOffset when the anchor slot is the first visible slot.
        /// </summary>
        public double OffsetIntoSlot { get; }

        /// <summary>
        /// The vertical offset at the time the anchor was captured.
        /// Used to calculate corrections when heights change.
        /// </summary>
        public double VerticalOffset { get; }

        /// <summary>
        /// The measured height of the anchor slot at capture time.
        /// Used to detect if the row height changed.
        /// </summary>
        public double SlotHeight { get; }

        /// <summary>
        /// Whether this anchor is valid and can be used.
        /// </summary>
        public bool IsValid => Slot >= 0;

        /// <summary>
        /// Creates a new scroll anchor.
        /// </summary>
        public ScrollAnchor(int slot, double offsetIntoSlot, double verticalOffset, double slotHeight)
        {
            Slot = slot;
            OffsetIntoSlot = offsetIntoSlot;
            VerticalOffset = verticalOffset;
            SlotHeight = slotHeight;
        }

        /// <summary>
        /// Creates an invalid/empty anchor.
        /// </summary>
        public static ScrollAnchor Invalid => new(-1, 0, 0, 0);

        /// <summary>
        /// Returns a string representation for debugging.
        /// </summary>
        public override string ToString()
        {
            return IsValid 
                ? $"Anchor[Slot={Slot}, Offset={OffsetIntoSlot:F2}, Height={SlotHeight:F2}]"
                : "Anchor[Invalid]";
        }
    }

    /// <summary>
    /// Represents the result of a scroll target calculation.
    /// </summary>
    internal readonly struct ScrollTarget
    {
        /// <summary>
        /// The target first visible slot after scrolling.
        /// </summary>
        public int FirstSlot { get; }

        /// <summary>
        /// The NegVerticalOffset (how much of the first slot is scrolled out).
        /// </summary>
        public double NegVerticalOffset { get; }

        /// <summary>
        /// The estimated vertical offset for the scroll position.
        /// </summary>
        public double EstimatedVerticalOffset { get; }

        /// <summary>
        /// Whether the scroll target is at the boundary (top or bottom).
        /// </summary>
        public bool IsAtBoundary { get; }

        /// <summary>
        /// Creates a new scroll target.
        /// </summary>
        public ScrollTarget(int firstSlot, double negVerticalOffset, double estimatedVerticalOffset, bool isAtBoundary = false)
        {
            FirstSlot = firstSlot;
            NegVerticalOffset = negVerticalOffset;
            EstimatedVerticalOffset = estimatedVerticalOffset;
            IsAtBoundary = isAtBoundary;
        }
    }
}
