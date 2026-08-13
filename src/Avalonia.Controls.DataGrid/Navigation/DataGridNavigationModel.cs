// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.ComponentModel;
using Avalonia.Input;
using Avalonia.Media;

namespace Avalonia.Controls.DataGridNavigation
{
    /// <summary>
    /// Identifies a semantic DataGrid navigation operation independently of its input gesture.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationCommand
    {
        /// <summary>No navigation operation.</summary>
        None,
        /// <summary>Moves to the preceding visible row.</summary>
        Up,
        /// <summary>Moves to the following visible row.</summary>
        Down,
        /// <summary>Moves to the visible column on the physical left.</summary>
        Left,
        /// <summary>Moves to the visible column on the physical right.</summary>
        Right,
        /// <summary>Moves backward by one viewport page.</summary>
        PageUp,
        /// <summary>Moves forward by one viewport page.</summary>
        PageDown,
        /// <summary>Moves to the first visible column in the current row.</summary>
        RowStart,
        /// <summary>Moves to the last visible column in the current row.</summary>
        RowEnd,
        /// <summary>Moves to the first visible row in the current column.</summary>
        ColumnStart,
        /// <summary>Moves to the last visible row in the current column.</summary>
        ColumnEnd,
        /// <summary>Moves to the first visible data cell.</summary>
        GridStart,
        /// <summary>Moves to the last visible data cell.</summary>
        GridEnd,
        /// <summary>Moves to the next cell in row-major order.</summary>
        Next,
        /// <summary>Moves to the previous cell in row-major order.</summary>
        Previous,
        /// <summary>Commits editing and applies the configured Enter behavior.</summary>
        Enter,
        /// <summary>Begins editing the current cell.</summary>
        BeginEdit,
        /// <summary>Cancels the current cell or row edit.</summary>
        CancelEdit,
        /// <summary>Expands all expandable hierarchy nodes.</summary>
        ExpandAll
    }

    /// <summary>
    /// Identifies the source that initiated a navigation request.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationOrigin
    {
        /// <summary>A keyboard gesture initiated the request.</summary>
        Keyboard,
        /// <summary>The DataGrid programmatic API initiated the request.</summary>
        Programmatic,
        /// <summary>An MVVM command initiated the request.</summary>
        Command,
        /// <summary>An accessibility or automation peer initiated the request.</summary>
        Automation,
        /// <summary>State restoration initiated the request.</summary>
        RestoredState
    }

    /// <summary>
    /// Describes how navigation behaves when it reaches an edge.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationBoundaryMode
    {
        /// <summary>Consumes navigation at the boundary and keeps the current cell.</summary>
        Contained,
        /// <summary>Wraps navigation to the opposite boundary.</summary>
        Wrap,
        /// <summary>Leaves the request unhandled so focus may leave the grid.</summary>
        Exit
    }

    /// <summary>
    /// Controls whether Tab traversal is active only while editing or for every current cell.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridTabNavigationMode
    {
        /// <summary>Uses grid-managed Tab traversal only while a cell is editing.</summary>
        EditingOnly,
        /// <summary>Uses grid-managed Tab traversal whenever a data cell is current.</summary>
        Always
    }

    /// <summary>
    /// Controls whether Left and Right are interpreted physically or relative to layout direction.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridHorizontalNavigationMode
    {
        /// <summary>Left and Right always identify physical directions.</summary>
        Physical,
        /// <summary>Left and Right follow the grid flow direction.</summary>
        Logical
    }

    /// <summary>
    /// Identifies the action the grid should take after a model resolves a request.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationDecision
    {
        /// <summary>Executes the existing built-in movement engine.</summary>
        Default,
        /// <summary>Moves to an explicit data-cell position.</summary>
        Move,
        /// <summary>Executes a different semantic command through the built-in engine.</summary>
        Redirect,
        /// <summary>Consumes the request and keeps the current cell.</summary>
        Stay,
        /// <summary>Leaves the request unhandled so focus may leave the grid.</summary>
        LeaveGrid
    }

    /// <summary>
    /// Identifies why a completed request did not move to its requested target.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridNavigationFailureReason
    {
        /// <summary>The request completed without a failure.</summary>
        None,
        /// <summary>A policy or observer canceled the request.</summary>
        Canceled,
        /// <summary>The requested movement crossed a configured boundary.</summary>
        BoundaryReached,
        /// <summary>The explicit target was not a visible data cell.</summary>
        InvalidTarget,
        /// <summary>No data cell is current.</summary>
        NoCurrentCell,
        /// <summary>The active view contains no rows.</summary>
        NoRows,
        /// <summary>The grid contains no visible data columns.</summary>
        NoColumns,
        /// <summary>Editing validation prevented the grid from leaving the cell.</summary>
        EditCommitFailed
    }

    /// <summary>
    /// Represents a data-cell position by collection-view row index and column display index.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridNavigationPosition : IEquatable<DataGridNavigationPosition>
    {
        /// <summary>Initializes a data-cell position.</summary>
        /// <param name="rowIndex">The collection-view row index.</param>
        /// <param name="columnDisplayIndex">The column display index.</param>
        public DataGridNavigationPosition(int rowIndex, int columnDisplayIndex)
        {
            RowIndex = rowIndex;
            ColumnDisplayIndex = columnDisplayIndex;
        }

        /// <summary>Gets the collection-view row index.</summary>
        public int RowIndex { get; }

        /// <summary>Gets the column display index.</summary>
        public int ColumnDisplayIndex { get; }

        /// <summary>Gets whether both coordinates are non-negative.</summary>
        public bool IsValid => RowIndex >= 0 && ColumnDisplayIndex >= 0;

        /// <summary>Gets an invalid position that does not identify a cell.</summary>
        public static DataGridNavigationPosition Unset => new(-1, -1);

        /// <inheritdoc />
        public bool Equals(DataGridNavigationPosition other) =>
            RowIndex == other.RowIndex && ColumnDisplayIndex == other.ColumnDisplayIndex;

        /// <inheritdoc />
        public override bool Equals(object obj) =>
            obj is DataGridNavigationPosition other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (RowIndex * 397) ^ ColumnDisplayIndex;
            }
        }

        /// <summary>Compares two positions for equality.</summary>
        public static bool operator ==(DataGridNavigationPosition left, DataGridNavigationPosition right) =>
            left.Equals(right);

        /// <summary>Compares two positions for inequality.</summary>
        public static bool operator !=(DataGridNavigationPosition left, DataGridNavigationPosition right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// Describes a semantic navigation request and the grid state relevant to policy decisions.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridNavigationRequest
    {
        /// <summary>Initializes a complete semantic navigation request.</summary>
        /// <param name="command">The semantic operation.</param>
        /// <param name="origin">The request source.</param>
        /// <param name="currentPosition">The current data-cell position.</param>
        /// <param name="proposedPosition">The built-in engine's proposed target, when one exists.</param>
        /// <param name="modifiers">Active keyboard modifiers.</param>
        /// <param name="isEditing">Whether a cell is currently editing.</param>
        /// <param name="selectionMode">The active selection mode.</param>
        /// <param name="selectionUnit">The active selection unit.</param>
        /// <param name="flowDirection">The grid layout direction.</param>
        /// <param name="firstRowIndex">The first navigable row index.</param>
        /// <param name="lastRowIndex">The last navigable row index.</param>
        /// <param name="firstColumnDisplayIndex">The first navigable column display index.</param>
        /// <param name="lastColumnDisplayIndex">The last navigable column display index.</param>
        public DataGridNavigationRequest(
            DataGridNavigationCommand command,
            DataGridNavigationOrigin origin,
            DataGridNavigationPosition currentPosition,
            DataGridNavigationPosition? proposedPosition,
            KeyModifiers modifiers,
            bool isEditing,
            DataGridSelectionMode selectionMode,
            DataGridSelectionUnit selectionUnit,
            FlowDirection flowDirection,
            int firstRowIndex,
            int lastRowIndex,
            int firstColumnDisplayIndex,
            int lastColumnDisplayIndex)
        {
            Command = command;
            Origin = origin;
            CurrentPosition = currentPosition;
            ProposedPosition = proposedPosition;
            Modifiers = modifiers;
            IsEditing = isEditing;
            SelectionMode = selectionMode;
            SelectionUnit = selectionUnit;
            FlowDirection = flowDirection;
            FirstRowIndex = firstRowIndex;
            LastRowIndex = lastRowIndex;
            FirstColumnDisplayIndex = firstColumnDisplayIndex;
            LastColumnDisplayIndex = lastColumnDisplayIndex;
        }

        /// <summary>Gets the semantic operation.</summary>
        public DataGridNavigationCommand Command { get; }

        /// <summary>Gets the request source.</summary>
        public DataGridNavigationOrigin Origin { get; }

        /// <summary>Gets the current data-cell position.</summary>
        public DataGridNavigationPosition CurrentPosition { get; }

        /// <summary>Gets the built-in engine's proposed target, when one exists.</summary>
        public DataGridNavigationPosition? ProposedPosition { get; }

        /// <summary>Gets the active keyboard modifiers.</summary>
        public KeyModifiers Modifiers { get; }

        /// <summary>Gets whether a cell is currently editing.</summary>
        public bool IsEditing { get; }

        /// <summary>Gets the active selection mode.</summary>
        public DataGridSelectionMode SelectionMode { get; }

        /// <summary>Gets the active selection unit.</summary>
        public DataGridSelectionUnit SelectionUnit { get; }

        /// <summary>Gets the grid layout direction.</summary>
        public FlowDirection FlowDirection { get; }

        /// <summary>Gets the first navigable row index.</summary>
        public int FirstRowIndex { get; }

        /// <summary>Gets the last navigable row index.</summary>
        public int LastRowIndex { get; }

        /// <summary>Gets the first navigable column display index.</summary>
        public int FirstColumnDisplayIndex { get; }

        /// <summary>Gets the last navigable column display index.</summary>
        public int LastColumnDisplayIndex { get; }

        /// <summary>Gets whether the request describes at least one navigable row.</summary>
        public bool HasRows => FirstRowIndex >= 0 && LastRowIndex >= FirstRowIndex;

        /// <summary>Gets whether the request describes at least one navigable column.</summary>
        public bool HasColumns => FirstColumnDisplayIndex >= 0 && LastColumnDisplayIndex >= FirstColumnDisplayIndex;
    }

    /// <summary>
    /// Contains the policy decision returned by an <see cref="IDataGridNavigationModel"/>.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridNavigationResult : IEquatable<DataGridNavigationResult>
    {
        private DataGridNavigationResult(
            DataGridNavigationDecision decision,
            DataGridNavigationPosition target,
            DataGridNavigationCommand redirectedCommand,
            DataGridNavigationFailureReason failureReason)
        {
            Decision = decision;
            Target = target;
            RedirectedCommand = redirectedCommand;
            FailureReason = failureReason;
        }

        /// <summary>Gets the action the grid should apply.</summary>
        public DataGridNavigationDecision Decision { get; }

        /// <summary>Gets the explicit target for a Move decision.</summary>
        public DataGridNavigationPosition Target { get; }

        /// <summary>Gets the semantic command for a Redirect decision.</summary>
        public DataGridNavigationCommand RedirectedCommand { get; }

        /// <summary>Gets the policy-level reason for staying in place, when supplied.</summary>
        public DataGridNavigationFailureReason FailureReason { get; }

        /// <summary>Creates a decision that uses the existing built-in movement engine.</summary>
        /// <returns>A default-engine decision.</returns>
        public static DataGridNavigationResult UseDefault() =>
            new(DataGridNavigationDecision.Default, DataGridNavigationPosition.Unset, DataGridNavigationCommand.None,
                DataGridNavigationFailureReason.None);

        /// <summary>Creates a decision that moves to an explicit data cell.</summary>
        /// <param name="target">The target row and column display indexes.</param>
        /// <returns>An explicit move decision.</returns>
        public static DataGridNavigationResult MoveTo(DataGridNavigationPosition target) =>
            new(DataGridNavigationDecision.Move, target, DataGridNavigationCommand.None,
                DataGridNavigationFailureReason.None);

        /// <summary>Creates a decision that redirects to another semantic command.</summary>
        /// <param name="command">The command executed by the built-in engine.</param>
        /// <returns>A redirect decision.</returns>
        public static DataGridNavigationResult RedirectTo(DataGridNavigationCommand command) =>
            new(DataGridNavigationDecision.Redirect, DataGridNavigationPosition.Unset, command,
                DataGridNavigationFailureReason.None);

        /// <summary>Creates a decision that consumes a boundary request without moving.</summary>
        /// <returns>A boundary stay decision.</returns>
        public static DataGridNavigationResult Stay() =>
            new(DataGridNavigationDecision.Stay, DataGridNavigationPosition.Unset, DataGridNavigationCommand.None,
                DataGridNavigationFailureReason.BoundaryReached);

        /// <summary>Creates a decision that consumes a canceled request without moving.</summary>
        /// <returns>A canceled stay decision.</returns>
        public static DataGridNavigationResult Cancel() =>
            new(DataGridNavigationDecision.Stay, DataGridNavigationPosition.Unset, DataGridNavigationCommand.None,
                DataGridNavigationFailureReason.Canceled);

        /// <summary>Creates a decision that permits focus to leave the grid.</summary>
        /// <returns>A leave-grid decision.</returns>
        public static DataGridNavigationResult LeaveGrid() =>
            new(DataGridNavigationDecision.LeaveGrid, DataGridNavigationPosition.Unset, DataGridNavigationCommand.None,
                DataGridNavigationFailureReason.BoundaryReached);

        /// <inheritdoc />
        public bool Equals(DataGridNavigationResult other) =>
            Decision == other.Decision && Target == other.Target && RedirectedCommand == other.RedirectedCommand &&
            FailureReason == other.FailureReason;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is DataGridNavigationResult other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Decision;
                hash = (hash * 397) ^ Target.GetHashCode();
                hash = (hash * 397) ^ (int)RedirectedCommand;
                hash = (hash * 397) ^ (int)FailureReason;
                return hash;
            }
        }

        /// <summary>Compares two results for equality.</summary>
        public static bool operator ==(DataGridNavigationResult left, DataGridNavigationResult right) => left.Equals(right);

        /// <summary>Compares two results for inequality.</summary>
        public static bool operator !=(DataGridNavigationResult left, DataGridNavigationResult right) => !left.Equals(right);
    }

    /// <summary>
    /// Describes the completed outcome of a navigation request without requiring event allocation.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridNavigationCompleted
    {
        /// <summary>Initializes a completed navigation outcome.</summary>
        /// <param name="request">The original semantic request.</param>
        /// <param name="result">The resolved policy decision.</param>
        /// <param name="oldPosition">The position before execution.</param>
        /// <param name="newPosition">The position after execution.</param>
        /// <param name="handled">Whether the grid consumed the request.</param>
        /// <param name="failureReason">Why execution did not move as requested.</param>
        public DataGridNavigationCompleted(
            DataGridNavigationRequest request,
            DataGridNavigationResult result,
            DataGridNavigationPosition oldPosition,
            DataGridNavigationPosition newPosition,
            bool handled,
            DataGridNavigationFailureReason failureReason)
        {
            Request = request;
            Result = result;
            OldPosition = oldPosition;
            NewPosition = newPosition;
            Handled = handled;
            FailureReason = failureReason;
        }

        /// <summary>Gets the original semantic request.</summary>
        public DataGridNavigationRequest Request { get; }

        /// <summary>Gets the resolved policy decision.</summary>
        public DataGridNavigationResult Result { get; }

        /// <summary>Gets the position before execution.</summary>
        public DataGridNavigationPosition OldPosition { get; }

        /// <summary>Gets the position after execution.</summary>
        public DataGridNavigationPosition NewPosition { get; }

        /// <summary>Gets whether the grid consumed the request.</summary>
        public bool Handled { get; }

        /// <summary>Gets whether execution changed to a valid data-cell position.</summary>
        public bool Moved => OldPosition != NewPosition && NewPosition.IsValid;

        /// <summary>Gets why execution did not move as requested.</summary>
        public DataGridNavigationFailureReason FailureReason { get; }
    }

    /// <summary>
    /// Provides cancelable preview data for a navigation request.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridNavigationChangingEventArgs : CancelEventArgs
    {
        /// <summary>Initializes cancelable navigation preview data.</summary>
        /// <param name="request">The semantic navigation request.</param>
        /// <param name="result">The initial policy result.</param>
        public DataGridNavigationChangingEventArgs(
            DataGridNavigationRequest request,
            DataGridNavigationResult result)
        {
            Request = request;
            Result = result;
        }

        /// <summary>Gets the semantic navigation request.</summary>
        public DataGridNavigationRequest Request { get; }

        /// <summary>Gets or sets the result applied when the preview is not canceled.</summary>
        public DataGridNavigationResult Result { get; set; }
    }

    /// <summary>
    /// Provides completion data after the grid applies a navigation request.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridNavigationChangedEventArgs : EventArgs
    {
        /// <summary>Initializes completion event data.</summary>
        /// <param name="completed">The completed navigation outcome.</param>
        public DataGridNavigationChangedEventArgs(DataGridNavigationCompleted completed)
        {
            Completed = completed;
        }

        /// <summary>Gets the completed navigation outcome.</summary>
        public DataGridNavigationCompleted Completed { get; }
    }

    /// <summary>
    /// Resolves DataGrid navigation policy independently of input gesture and layout mechanics.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridNavigationModel
    {
        /// <summary>Raised before a policy decision is applied.</summary>
        event EventHandler<DataGridNavigationChangingEventArgs> NavigationChanging;

        /// <summary>Raised after the grid applies a policy decision.</summary>
        event EventHandler<DataGridNavigationChangedEventArgs> NavigationChanged;

        /// <summary>Resolves policy and raises cancelable preview observers.</summary>
        /// <param name="request">The semantic navigation request.</param>
        /// <returns>The policy decision applied by the grid.</returns>
        DataGridNavigationResult Resolve(DataGridNavigationRequest request);

        /// <summary>Publishes completion after the grid applies a request.</summary>
        /// <param name="completed">The completed navigation outcome.</param>
        void NotifyCompleted(DataGridNavigationCompleted completed);
    }

    /// <summary>
    /// Provides side-effect-free navigation queries for command enablement.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridNavigationQueryModel
    {
        /// <summary>
        /// Resolves a request without raising preview or completion events.
        /// </summary>
        /// <param name="request">The semantic navigation request.</param>
        /// <returns>The current navigation policy decision.</returns>
        DataGridNavigationResult Query(DataGridNavigationRequest request);
    }

    /// <summary>
    /// Creates a navigation model for a DataGrid instance.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridNavigationModelFactory
    {
        /// <summary>Creates a navigation model for one DataGrid.</summary>
        /// <returns>A new navigation model.</returns>
        IDataGridNavigationModel Create();
    }

    /// <summary>
    /// Default, extensible navigation policy. Its default settings preserve the legacy DataGrid behavior.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridNavigationModel : IDataGridNavigationModel, IDataGridNavigationQueryModel, INotifyPropertyChanged
    {
        private DataGridNavigationBoundaryMode _horizontalBoundaryMode = DataGridNavigationBoundaryMode.Contained;
        private DataGridNavigationBoundaryMode _verticalBoundaryMode = DataGridNavigationBoundaryMode.Contained;
        private DataGridNavigationBoundaryMode _tabBoundaryMode = DataGridNavigationBoundaryMode.Exit;
        private DataGridTabNavigationMode _tabNavigationMode = DataGridTabNavigationMode.EditingOnly;
        private DataGridHorizontalNavigationMode _horizontalNavigationMode = DataGridHorizontalNavigationMode.Physical;

        /// <inheritdoc />
        public event EventHandler<DataGridNavigationChangingEventArgs> NavigationChanging;

        /// <inheritdoc />
        public event EventHandler<DataGridNavigationChangedEventArgs> NavigationChanged;

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Gets or sets the Left/Right boundary policy.</summary>
        public DataGridNavigationBoundaryMode HorizontalBoundaryMode
        {
            get => _horizontalBoundaryMode;
            set => SetProperty(ref _horizontalBoundaryMode, value, nameof(HorizontalBoundaryMode));
        }

        /// <summary>Gets or sets the Up/Down boundary policy.</summary>
        public DataGridNavigationBoundaryMode VerticalBoundaryMode
        {
            get => _verticalBoundaryMode;
            set => SetProperty(ref _verticalBoundaryMode, value, nameof(VerticalBoundaryMode));
        }

        /// <summary>Gets or sets the Tab/Shift+Tab boundary policy.</summary>
        public DataGridNavigationBoundaryMode TabBoundaryMode
        {
            get => _tabBoundaryMode;
            set => SetProperty(ref _tabBoundaryMode, value, nameof(TabBoundaryMode));
        }

        /// <summary>Gets or sets when the grid manages Tab traversal.</summary>
        public DataGridTabNavigationMode TabNavigationMode
        {
            get => _tabNavigationMode;
            set => SetProperty(ref _tabNavigationMode, value, nameof(TabNavigationMode));
        }

        /// <summary>Gets or sets whether Left/Right are physical or flow-relative.</summary>
        public DataGridHorizontalNavigationMode HorizontalNavigationMode
        {
            get => _horizontalNavigationMode;
            set => SetProperty(ref _horizontalNavigationMode, value, nameof(HorizontalNavigationMode));
        }

        /// <inheritdoc />
        public DataGridNavigationResult Resolve(DataGridNavigationRequest request)
        {
            DataGridNavigationResult result = ResolveCore(request);
            EventHandler<DataGridNavigationChangingEventArgs> handler = NavigationChanging;
            if (handler == null)
            {
                return result;
            }

            var args = new DataGridNavigationChangingEventArgs(request, result);
            handler(this, args);
            return args.Cancel ? DataGridNavigationResult.Cancel() : args.Result;
        }

        /// <inheritdoc />
        public DataGridNavigationResult Query(DataGridNavigationRequest request) => ResolveCore(request);

        /// <inheritdoc />
        public void NotifyCompleted(DataGridNavigationCompleted completed)
        {
            EventHandler<DataGridNavigationChangedEventArgs> handler = NavigationChanged;
            if (handler != null)
            {
                handler(this, new DataGridNavigationChangedEventArgs(completed));
            }
        }

        /// <summary>Resolves the reusable built-in policy before preview observers run.</summary>
        /// <param name="request">The semantic navigation request.</param>
        /// <returns>The default policy decision.</returns>
        protected virtual DataGridNavigationResult ResolveCore(DataGridNavigationRequest request)
        {
            if (HorizontalNavigationMode == DataGridHorizontalNavigationMode.Logical &&
                request.FlowDirection == FlowDirection.RightToLeft)
            {
                if (request.Command == DataGridNavigationCommand.Left)
                {
                    return DataGridNavigationResult.RedirectTo(DataGridNavigationCommand.Right);
                }

                if (request.Command == DataGridNavigationCommand.Right)
                {
                    return DataGridNavigationResult.RedirectTo(DataGridNavigationCommand.Left);
                }
            }

            if (!request.CurrentPosition.IsValid)
            {
                return DataGridNavigationResult.UseDefault();
            }

            if (request.Command is DataGridNavigationCommand.Next or DataGridNavigationCommand.Previous)
            {
                if (!request.IsEditing && TabNavigationMode == DataGridTabNavigationMode.EditingOnly)
                {
                    return DataGridNavigationResult.UseDefault();
                }

                if (request.ProposedPosition is { } proposed)
                {
                    return TabNavigationMode == DataGridTabNavigationMode.Always && !request.IsEditing
                        ? DataGridNavigationResult.MoveTo(proposed)
                        : DataGridNavigationResult.UseDefault();
                }

                return ResolveTabBoundary(request);
            }

            if (request.ProposedPosition.HasValue)
            {
                return DataGridNavigationResult.UseDefault();
            }

            return request.Command switch
            {
                DataGridNavigationCommand.Left => ResolveHorizontalBoundary(request, moveToEnd: true),
                DataGridNavigationCommand.Right => ResolveHorizontalBoundary(request, moveToEnd: false),
                DataGridNavigationCommand.Up => ResolveVerticalBoundary(request, moveToEnd: true),
                DataGridNavigationCommand.Down => ResolveVerticalBoundary(request, moveToEnd: false),
                _ => DataGridNavigationResult.UseDefault()
            };
        }

        private DataGridNavigationResult ResolveHorizontalBoundary(
            DataGridNavigationRequest request,
            bool moveToEnd)
        {
            return HorizontalBoundaryMode switch
            {
                DataGridNavigationBoundaryMode.Wrap when request.HasColumns =>
                    DataGridNavigationResult.MoveTo(new DataGridNavigationPosition(
                        request.CurrentPosition.RowIndex,
                        moveToEnd ? request.LastColumnDisplayIndex : request.FirstColumnDisplayIndex)),
                DataGridNavigationBoundaryMode.Exit => DataGridNavigationResult.LeaveGrid(),
                _ => DataGridNavigationResult.Stay()
            };
        }

        private DataGridNavigationResult ResolveVerticalBoundary(
            DataGridNavigationRequest request,
            bool moveToEnd)
        {
            return VerticalBoundaryMode switch
            {
                DataGridNavigationBoundaryMode.Wrap when request.HasRows =>
                    DataGridNavigationResult.MoveTo(new DataGridNavigationPosition(
                        moveToEnd ? request.LastRowIndex : request.FirstRowIndex,
                        request.CurrentPosition.ColumnDisplayIndex)),
                DataGridNavigationBoundaryMode.Exit => DataGridNavigationResult.LeaveGrid(),
                _ => DataGridNavigationResult.Stay()
            };
        }

        private DataGridNavigationResult ResolveTabBoundary(DataGridNavigationRequest request)
        {
            if (TabBoundaryMode == DataGridNavigationBoundaryMode.Exit)
            {
                return DataGridNavigationResult.LeaveGrid();
            }

            if (TabBoundaryMode == DataGridNavigationBoundaryMode.Wrap && request.HasRows && request.HasColumns)
            {
                bool previous = request.Command == DataGridNavigationCommand.Previous;
                return DataGridNavigationResult.MoveTo(new DataGridNavigationPosition(
                    previous ? request.LastRowIndex : request.FirstRowIndex,
                    previous ? request.LastColumnDisplayIndex : request.FirstColumnDisplayIndex));
            }

            return DataGridNavigationResult.Stay();
        }

        private void SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
