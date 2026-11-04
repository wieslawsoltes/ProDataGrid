// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls;
using Avalonia.Controls.Utils;
using Avalonia.Utilities;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System;

namespace Avalonia.Collections
{
    sealed partial class DataGridCollectionView
    {
        private int _sourceMutationDepth;
        private DataGridCollectionViewSourceMutationEventArgs _sourceMutationEventArgs;
        private NotifyCollectionChangedEventArgs _pendingSourceReset;
        private bool _pendingSortOnlyRefresh;
        private bool _isReorderOnlyReset;

        internal bool IsReorderOnlyReset => _isReorderOnlyReset;

        private SourceMutationScope BeginSourceMutation(NotifyCollectionChangedEventArgs change)
        {
            if (_sourceMutationDepth++ == 0)
            {
                _sourceMutationEventArgs = new DataGridCollectionViewSourceMutationEventArgs(
                    change,
                    SourceCollection,
                    isViewProjectionStable: !UsesLocalArray);
                try
                {
                    SourceMutationStarting?.Invoke(this, _sourceMutationEventArgs);
                }
                catch
                {
                    DataGridCollectionViewSourceMutationEventArgs args = _sourceMutationEventArgs;
                    _sourceMutationEventArgs = null;
                    _sourceMutationDepth = 0;
                    try
                    {
                        SourceMutationCompleted?.Invoke(this, args);
                    }
                    catch
                    {
                        // Preserve the proposal-handler failure that aborted the mutation
                        // boundary. Subscribers must keep their completion cleanup non-throwing.
                    }
                    throw;
                }
            }

            return new SourceMutationScope(this);
        }

        private void EndSourceMutation()
        {
            if (--_sourceMutationDepth != 0)
            {
                return;
            }

            DataGridCollectionViewSourceMutationEventArgs args = _sourceMutationEventArgs;
            _sourceMutationEventArgs = null;
            SourceMutationCompleted?.Invoke(this, args);
        }

        private void PromoteSourceMutationToStableProjection()
        {
            if (_sourceMutationDepth == 0 ||
                _sourceMutationEventArgs == null ||
                _sourceMutationEventArgs.IsViewProjectionStable)
            {
                return;
            }

            _sourceMutationEventArgs = new DataGridCollectionViewSourceMutationEventArgs(
                _sourceMutationEventArgs.CollectionChange,
                this,
                isViewProjectionStable: true);
            SourceMutationStarting?.Invoke(this, _sourceMutationEventArgs);
        }

        private readonly struct SourceMutationScope : IDisposable
        {
            private readonly DataGridCollectionView _owner;

            public SourceMutationScope(DataGridCollectionView owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                _owner?.EndSourceMutation();
            }
        }

        private ViewProjectionMutationScope BeginViewProjectionMutation()
        {
            var args = new DataGridCollectionViewSourceMutationEventArgs(
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset),
                this,
                isViewProjectionStable: true);
            try
            {
                SourceMutationStarting?.Invoke(this, args);
            }
            catch
            {
                try
                {
                    SourceMutationCompleted?.Invoke(this, args);
                }
                catch
                {
                    // Preserve the proposal-handler failure that aborted the refresh boundary.
                }
                throw;
            }

            return new ViewProjectionMutationScope(this, args);
        }

        private readonly struct ViewProjectionMutationScope : IDisposable
        {
            private readonly DataGridCollectionView _owner;
            private readonly DataGridCollectionViewSourceMutationEventArgs _args;

            public ViewProjectionMutationScope(
                DataGridCollectionView owner,
                DataGridCollectionViewSourceMutationEventArgs args)
            {
                _owner = owner;
                _args = args;
            }

            public void Dispose()
            {
                _owner?.SourceMutationCompleted?.Invoke(_owner, _args);
            }
        }

        /// <summary>
        /// Return true if the item belongs to this view.  No assumptions are
        /// made about the item. This method will behave similarly to IList.Contains().
        /// If the caller knows that the item belongs to the
        /// underlying collection, it is more efficient to call PassesFilter.
        /// </summary>
        /// <param name="item">The item we are checking to see whether it is within the collection</param>
        /// <returns>Boolean value of whether or not the collection contains the item</returns>
        public bool Contains(object item)
        {
            EnsureCollectionInSync();
            VerifyRefreshNotDeferred();
            return IndexOf(item) >= 0;
        }

        /// <summary>
        /// Enter a Defer Cycle.
        /// Defer cycles are used to coalesce changes to the ICollectionView.
        /// </summary>
        /// <returns>IDisposable used to notify that we no longer need to defer, when we dispose</returns>
        public IDisposable DeferRefresh()
        {
            if (IsAddingNew || IsEditingItem)
            {
                throw new InvalidOperationException(GetOperationNotAllowedDuringAddOrEditText(nameof(DeferRefresh)));
            }

            ++_deferLevel;
            return new DeferHelper(this);
        }

        /// <summary>
        /// Implementation of IEnumerable.GetEnumerator().
        /// This provides a way to enumerate the members of the collection
        /// without changing the currency.
        /// </summary>
        /// <returns>IEnumerator for the collection</returns>
        //TODO Paging
        public IEnumerator GetEnumerator()
        {
            EnsureCollectionInSync();
            VerifyRefreshNotDeferred();

            if (IsGrouping)
            {
                return RootGroup?.GetLeafEnumerator();
            }

            // if we are paging
            if (PageSize > 0)
            {
                List<object> list = new List<object>();

                // if we are in the middle of asynchronous load
                if (PageIndex < 0)
                {
                    return list.GetEnumerator();
                }

                for (int index = _pageSize * PageIndex;
                index < (int)Math.Min(_pageSize * (PageIndex + 1), InternalList.Count);
                index++)
                {
                    list.Add(InternalList[index]);
                }

                return new NewItemAwareEnumerator(this, list.GetEnumerator(), CurrentAddItem);
            }
            else
            {
                return new NewItemAwareEnumerator(this, InternalList.GetEnumerator(), CurrentAddItem);
            }
        }

        /// <summary>
        /// Retrieve item at the given zero-based index in this DataGridCollectionView, after the source collection
        /// is filtered, sorted, and paged.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if index is out of range
        /// </exception>
        /// <param name="index">Index of the item we want to retrieve</param>
        /// <returns>Item at specified index</returns>
        public object GetItemAt(int index)
        {
            EnsureCollectionInSync();
            VerifyRefreshNotDeferred();

            // for indices larger than the count
            if (index >= Count || index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (IsGrouping)
            {
                return RootGroup?.LeafAt(_isUsingTemporaryGroup ? ConvertToInternalIndex(index) : index);
            }

            if (IsAddingNew && UsesLocalArray && index == Count - 1)
            {
                return CurrentAddItem;
            }

            return InternalItemAt(ConvertToInternalIndex(index));
        }

        /// <summary>
        /// Return the index where the given item appears, or -1 if doesn't appear.
        /// </summary>
        /// <param name="item">Item we are searching for</param>
        /// <returns>Index of specified item</returns>
        //TODO Paging
        public int IndexOf(object item)
        {
            EnsureCollectionInSync();
            VerifyRefreshNotDeferred();

            if (IsGrouping)
            {
                return RootGroup?.LeafIndexOf(item) ?? -1;
            }
            if (IsAddingNew && Object.Equals(item, CurrentAddItem) && UsesLocalArray)
            {
                return Count - 1;
            }

            int internalIndex = InternalIndexOf(item);

            if (PageSize > 0 && internalIndex != -1)
            {
                if ((internalIndex >= (PageIndex * _pageSize)) &&
                (internalIndex < ((PageIndex + 1) * _pageSize)))
                {
                    return internalIndex - (PageIndex * _pageSize);
                }
                else
                {
                    return -1;
                }
            }
            else
            {
                return internalIndex;
            }
        }

        /// <summary>
        /// Return true if the item belongs to this view.  The item is assumed to belong to the
        /// underlying DataCollection;  this method merely takes filters into account.
        /// It is commonly used during collection-changed notifications to determine if the added/removed
        /// item requires processing.
        /// Returns true if no filter is set on collection view.
        /// </summary>
        /// <param name="item">The item to compare against the Filter</param>
        /// <returns>Whether the item passes the filter</returns>
        public bool PassesFilter(object item)
        {
            if (Filter != null)
            {
                return Filter(item);
            }

            return true;
        }

        /// <summary>
        /// Re-create the view, using any SortDescriptions and/or Filters.
        /// </summary>
        public void Refresh()
        {
            if (this is IDataGridEditableCollectionView ecv && (ecv.IsAddingNew || ecv.IsEditingItem))
            {
                throw new InvalidOperationException(GetOperationNotAllowedDuringAddOrEditText(nameof(Refresh)));
            }

            RefreshInternal();
        }

        /// <summary>
        /// Remove the given item from the underlying collection. It
        /// needs to be in the current filtered, sorted, and paged view
        /// to call
        /// </summary>
        /// <param name="item">Item we want to remove</param>
        public void Remove(object item)
        {
            int index = IndexOf(item);
            if (index >= 0)
            {
                RemoveAt(index);
            }
        }

        /// <summary>
        /// Remove the item at the given index from the underlying collection.
        /// The index is interpreted with respect to the view (filtered, sorted,
        /// and paged list).
        /// </summary>
        /// <param name="index">Index of the item we want to remove</param>
        //TODO Paging
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index was out of range. Must be non-negative and less than the size of the collection.");
            }

            if (IsEditingItem || IsAddingNew)
            {
                throw new InvalidOperationException(GetOperationNotAllowedDuringAddOrEditText(nameof(RemoveAt)));
            }
            else if (!CanRemove)
            {
                throw new InvalidOperationException("Remove/RemoveAt is not supported.");
            }

            VerifyRefreshNotDeferred();

            // convert the index from "view-relative" to "list-relative"
            object item = GetItemAt(index);

            // before we remove the item, see if we are not on the last page
            // and will have to bring in a new item to replace it
            bool replaceItem = PageSize > 0 && !OnLastLocalPage;

            try
            {
                // temporarily disable the CollectionChanged event
                // handler so filtering, sorting, or grouping
                // doesn't get applied yet
                SetFlag(CollectionViewFlags.ShouldProcessCollectionChanged, false);

                if (SourceList != null)
                {
                    SourceList.Remove(item);
                }
            }
            finally
            {
                SetFlag(CollectionViewFlags.ShouldProcessCollectionChanged, true);
            }

            // Modify our _trackingEnumerator so that it shows that our collection is "up to date"
            // and will not refresh for now.
            _trackingEnumerator = _sourceCollection.GetEnumerator();

            if (!IsUsingSourceList)
            {
                Debug.Assert(index == IndexOf(item), "IndexOf returned unexpected value");

                // remove the item from the internal list
                _internalList.Remove(item);
            }

            if (IsGrouping)
            {
                if (PageSize > 0)
                {
                    _temporaryGroup.RemoveFromSubgroups(item);
                }
                _group.RemoveFromSubgroups(item);
            }

            object oldCurrentItem = CurrentItem;
            int oldCurrentPosition = CurrentPosition;
            bool oldIsCurrentAfterLast = IsCurrentAfterLast;
            bool oldIsCurrentBeforeFirst = IsCurrentBeforeFirst;

            AdjustCurrencyForRemove(index);

            // fire remove notification
            OnCollectionChanged(
            new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Remove,
            item,
            index));

            RaiseCurrencyChanges(false, oldCurrentItem, oldCurrentPosition, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);

            // if we removed all items from the current page,
            // move to the previous page. we do not need to
            // fire additional notifications, as moving the page will
            // trigger a reset.
            if (NeedToMoveToPreviousPage)
            {
                MoveToPreviousPage();
                return;
            }

            // if we are paging, we may have to fire another notification for the item
            // that needs to replace the one we removed on this page.
            if (replaceItem)
            {
                // we first need to add the item into the current group
                if (IsGrouping)
                {
                    object newItem = _temporaryGroup.LeafAt((PageSize * (PageIndex + 1)) - 1);
                    if (newItem != null)
                    {
                        _group.AddToSubgroups(newItem, loading: false);
                    }
                }

                // fire the add notification
                OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add,
                GetItemAt(PageSize - 1),
                PageSize - 1));
            }
        }

        /// <summary>
        /// Process an Add operation from an INotifyCollectionChanged event handler.
        /// </summary>
        /// <param name="addedItem">Item added to the source collection</param>
        /// <param name="addIndex">Index item was added into</param>
        //TODO Paging
        private void ProcessAddEvent(object addedItem, int addIndex)
        {
            if (IsUsingSourceList)
            {
                ProcessAddEventUsingSourceList(addedItem, addIndex);
                return;
            }

            // item to fire remove notification for if necessary
            object removeNotificationItem = null;
            if (PageSize > 0 && !IsGrouping)
            {
                removeNotificationItem = (Count == PageSize) ?
                GetItemAt(PageSize - 1) : null;
            }

            // process the add by filtering and sorting the item
            ProcessInsertToCollection(
            addedItem,
            addIndex);

            // next check if we need to add an item into the current group
            bool needsGrouping = false;
            if (Count == 1 && GroupDescriptions.Count > 0)
            {
                // if this is the first item being added
                // we want to setup the groups with the
                // correct element type comparer
                if (PageSize > 0)
                {
                    PrepareGroupingComparer(_temporaryGroup);
                }
                PrepareGroupingComparer(_group);
            }

            if (IsGrouping)
            {
                int leafIndex = -1;

                if (PageSize > 0)
                {
                    _temporaryGroup.AddToSubgroups(addedItem, false /*loading*/);
                    leafIndex = _temporaryGroup.LeafIndexOf(addedItem);
                }

                // if we are not paging, we should just be able to add the item.
                // otherwise, we need to validate that it is within the current page.
                if (PageSize == 0 || (PageIndex + 1) * PageSize > leafIndex)
                {
                    needsGrouping = true;

                    int pageStartIndex = PageIndex * PageSize;

                    // if the item was inserted on a previous page
                    if (pageStartIndex > leafIndex && PageSize > 0)
                    {
                        addedItem = _temporaryGroup.LeafAt(pageStartIndex);
                    }

                    // if we're grouping and have more items than the
                    // PageSize will allow, remove the last item
                    if (PageSize > 0 && _group.ItemCount == PageSize)
                    {
                        removeNotificationItem = _group.LeafAt(PageSize - 1);
                        _group.RemoveFromSubgroups(removeNotificationItem);
                    }
                }
            }

            // if we are paging, we may have to fire another notification for the item
            // that needs to be removed for the one we added on this page.
            if (PageSize > 0 && !OnLastLocalPage &&
            (((IsGrouping && removeNotificationItem != null) ||
            (!IsGrouping && (PageIndex + 1) * PageSize > InternalIndexOf(addedItem)))))
            {
                if (removeNotificationItem != null && removeNotificationItem != addedItem)
                {
                    AdjustCurrencyForRemove(PageSize - 1);

                    OnCollectionChanged(
                    new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove,
                    removeNotificationItem,
                    PageSize - 1));
                }
            }

            // if we need to add the item into the current group
            // that will be displayed
            if (needsGrouping)
            {
                this._group.AddToSubgroups(addedItem, false /*loading*/);
            }

            int addedIndex = IndexOf(addedItem);

            // if the item is within the current page
            if (addedIndex >= 0)
            {
                object oldCurrentItem = CurrentItem;
                int oldCurrentPosition = CurrentPosition;
                bool oldIsCurrentAfterLast = IsCurrentAfterLast;
                bool oldIsCurrentBeforeFirst = IsCurrentBeforeFirst;

                AdjustCurrencyForAdd(null, addedIndex);

                // fire add notification
                OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add,
                addedItem,
                addedIndex));

                RaiseCurrencyChanges(false, oldCurrentItem, oldCurrentPosition, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);
            }
            else if (PageSize > 0)
            {
                // otherwise if the item was added into a previous page
                int internalIndex = InternalIndexOf(addedItem);

                if (internalIndex < ConvertToInternalIndex(0))
                {
                    // fire add notification for item pushed in
                    OnCollectionChanged(
                    new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    GetItemAt(0),
                    0));
                }
            }
        }

        private void ProcessAddEventUsingSourceList(object addedItem, int addIndex)
        {
            int addedIndex = addIndex;
            if (addedIndex < 0 || addedIndex >= Count)
            {
                addedIndex = IndexOf(addedItem);
            }

            if (addedIndex < 0)
            {
                return;
            }

            object oldCurrentItem = CurrentItem;
            int oldCurrentPosition = CurrentPosition;
            bool oldIsCurrentAfterLast = IsCurrentAfterLast;
            bool oldIsCurrentBeforeFirst = IsCurrentBeforeFirst;

            AdjustCurrencyForAdd(null, addedIndex);

            OnCollectionChanged(
            new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Add,
            addedItem,
            addedIndex));

            RaiseCurrencyChanges(false, oldCurrentItem, oldCurrentPosition, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);
        }

        /// <summary>
        /// Process CollectionChanged event on source collection
        /// that implements INotifyCollectionChanged.
        /// </summary>
        /// <param name="args">
        /// The NotifyCollectionChangedEventArgs to be processed.
        /// </param>
        private void ProcessCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            // if we do not want to handle the CollectionChanged event, return
            if (!CheckFlag(CollectionViewFlags.ShouldProcessCollectionChanged))
            {
                return;
            }

            if (args.Action == NotifyCollectionChangedAction.Reset)
            {
                SourceMutationScope sourceMutation = default;
                if (IsRefreshDeferred)
                {
                    _pendingSourceReset = args;
                }
                else
                {
                    sourceMutation = BeginSourceMutation(args);
                }

                try
                {
                if (!IsUsingSourceList)
                {
                    // if we have no items now, clear our own internal list
                    if (!SourceCollection.GetEnumerator().MoveNext())
                    {
                        _internalList.Clear();
                    }
                }

                // calling Refresh, will fire the collectionchanged event
                RefreshOrDefer();
                }
                finally
                {
                    sourceMutation.Dispose();
                }
                return;
            }

            using var sourceMutationScope = BeginSourceMutation(args);

            if (args.Action == NotifyCollectionChangedAction.Move)
            {
                if (IsUsingSourceList && (args.OldStartingIndex < 0 || args.NewStartingIndex < 0))
                {
                    RefreshOrDefer();
                    return;
                }

                if (args.OldItems != null)
                {
                    for (var i = 0; i < args.OldItems.Count; i++)
                    {
                        ProcessMoveEvent(args.OldItems[i], args.OldStartingIndex + i, args.NewStartingIndex + i);
                    }
                }

                return;
            }

            if (IsUsingSourceList &&
                (args.Action == NotifyCollectionChangedAction.Remove ||
                args.Action == NotifyCollectionChangedAction.Replace) &&
                args.OldStartingIndex < 0)
            {
                RefreshOrDefer();
                return;
            }

            // fire notifications for removes
            if (args.OldItems != null &&
            (args.Action == NotifyCollectionChangedAction.Remove ||
            args.Action == NotifyCollectionChangedAction.Replace))
            {
                var isReplace = args.Action == NotifyCollectionChangedAction.Replace;
                var oldStartingIndex = args.OldStartingIndex;

                if (oldStartingIndex >= 0 && args.OldItems.Count > 1)
                {
                    for (var i = args.OldItems.Count - 1; i >= 0; i--)
                    {
                        ProcessRemoveEvent(args.OldItems[i], isReplace, oldStartingIndex + i);
                    }
                }
                else
                {
                    for (var i = 0; i < args.OldItems.Count; i++)
                    {
                        var indexHint = oldStartingIndex >= 0 ? oldStartingIndex + i : (int?)null;
                        ProcessRemoveEvent(args.OldItems[i], isReplace, indexHint);
                    }
                }
            }

            // fire notifications for adds
            if (args.NewItems != null &&
            (args.Action == NotifyCollectionChangedAction.Add ||
            args.Action == NotifyCollectionChangedAction.Replace))
            {
                for (var i = 0; i < args.NewItems.Count; i++)
                {
                    if (Filter == null || PassesFilter(args.NewItems[i]))
                    {
                        ProcessAddEvent(args.NewItems[i], args.NewStartingIndex + i);
                    }
                }
            }
            if (args.Action != NotifyCollectionChangedAction.Replace)
            {
                OnPropertyChanged(nameof(ItemCount));
            }
        }

        /// <summary>
        /// Process a Remove operation from an INotifyCollectionChanged event handler.
        /// </summary>
        /// <param name="removedItem">Item removed from the source collection</param>
        /// <param name="isReplace">Whether this was part of a Replace operation</param>
        //TODO Paging
        private void ProcessRemoveEvent(object removedItem, bool isReplace, int? oldIndexHint = null)
        {
            if (IsUsingSourceList)
            {
                ProcessRemoveEventUsingSourceList(removedItem, isReplace, oldIndexHint);
                return;
            }

            int internalRemoveIndex = -1;
            int removeIndex = -1;

            bool usingOriginalOrder = SortDescriptions.Count == 0 && Filter == null && GroupDescriptions.Count == 0;

            if (usingOriginalOrder && oldIndexHint.HasValue)
            {
                internalRemoveIndex = oldIndexHint.Value;

                if (PageSize > 0)
                {
                    int pageStartIndex = PageIndex * PageSize;
                    int pageEndIndex = (PageIndex + 1) * PageSize;
                    removeIndex = (internalRemoveIndex >= pageStartIndex && internalRemoveIndex < pageEndIndex) ?
                        internalRemoveIndex - pageStartIndex :
                        -1;
                }
                else
                {
                    removeIndex = internalRemoveIndex;
                }
            }
            else if (IsGrouping)
            {
                internalRemoveIndex = PageSize > 0 ? _temporaryGroup.LeafIndexOf(removedItem) :
                _group.LeafIndexOf(removedItem);
            }
            else
            {
                internalRemoveIndex = InternalIndexOf(removedItem);
            }

            if (removeIndex < 0)
            {
                removeIndex = IndexOf(removedItem);
            }

            // remove the item from the collection
            if (internalRemoveIndex >= 0 && internalRemoveIndex < _internalList.Count)
            {
                _internalList.RemoveAt(internalRemoveIndex);
            }
            else
            {
                _internalList.Remove(removedItem);
            }

            // only fire the remove if it was removed from either the current page, or a previous page
            bool needToRemove;
            if (PageSize == 0)
            {
                needToRemove = removeIndex >= 0;
            }
            else
            {
                needToRemove = internalRemoveIndex >= 0 && internalRemoveIndex < (PageIndex + 1) * PageSize;
            }

            if (IsGrouping)
            {
                if (PageSize > 0)
                {
                    _temporaryGroup.RemoveFromSubgroups(removedItem);
                }

                if (needToRemove)
                {
                    _group.RemoveFromSubgroups(removeIndex >= 0 ? removedItem : _group.LeafAt(0));
                }
            }

            if (needToRemove)
            {
                object oldCurrentItem = CurrentItem;
                int oldCurrentPosition = CurrentPosition;
                bool oldIsCurrentAfterLast = IsCurrentAfterLast;
                bool oldIsCurrentBeforeFirst = IsCurrentBeforeFirst;

                AdjustCurrencyForRemove(removeIndex);

                // fire remove notification
                // if we removed from current page, remove from removeIndex,
                // if we removed from previous page, remove first item (index=0)
                OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove,
                removedItem,
                Math.Max(0, removeIndex)));

                RaiseCurrencyChanges(false, oldCurrentItem, oldCurrentPosition, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);

                // if we removed all items from the current page,
                // move to the previous page. we do not need to
                // fire additional notifications, as moving the page will
                // trigger a reset.
                if (NeedToMoveToPreviousPage && !isReplace)
                {
                    MoveToPreviousPage();
                    return;
                }

                // if we are paging, we may have to fire another notification for the item
                // that needs to replace the one we removed on this page.
                if (PageSize > 0 && Count == PageSize)
                {
                    // we first need to add the item into the current group
                    if (IsGrouping)
                    {
                        object newItem = _temporaryGroup.LeafAt((PageSize * (PageIndex + 1)) - 1);
                        if (newItem != null)
                        {
                            _group.AddToSubgroups(newItem, false /*loading*/);
                        }
                    }

                    // fire the add notification
                    OnCollectionChanged(
                    new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    GetItemAt(PageSize - 1),
                    PageSize - 1));
                }
            }
        }

        private void ProcessRemoveEventUsingSourceList(object removedItem, bool isReplace, int? oldIndexHint)
        {
            int removeIndex = oldIndexHint ?? -1;
            if (removeIndex < 0)
            {
                RefreshOrDefer();
                return;
            }

            object oldCurrentItem = CurrentItem;
            int oldCurrentPosition = CurrentPosition;
            bool oldIsCurrentAfterLast = IsCurrentAfterLast;
            bool oldIsCurrentBeforeFirst = IsCurrentBeforeFirst;

            AdjustCurrencyForRemove(removeIndex);

            OnCollectionChanged(
            new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Remove,
            removedItem,
            removeIndex));

            RaiseCurrencyChanges(false, oldCurrentItem, oldCurrentPosition, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);

            if (NeedToMoveToPreviousPage && !isReplace)
            {
                MoveToPreviousPage();
            }
        }

        /// <summary>
        /// Process a Move operation from an INotifyCollectionChanged event handler.
        /// Moves are handled as a remove followed by an add so existing paging
        /// and grouping logic can update correctly.
        /// </summary>
        /// <param name="movedItem">Item moved in the source collection.</param>
        /// <param name="oldIndex">Original index in the source collection.</param>
        /// <param name="newIndex">New index in the source collection.</param>
        private void ProcessMoveEvent(object movedItem, int oldIndex, int newIndex)
        {
            _ = oldIndex;
            object oldCurrentItem = CurrentItem;
            bool oldIsCurrentBeforeFirst = IsCurrentBeforeFirst;
            bool oldIsCurrentAfterLast = IsCurrentAfterLast;

            // Treat move as replace to avoid paging side effects when removing.
            ProcessRemoveEvent(movedItem, isReplace: true, oldIndexHint: oldIndex);
            ProcessAddEvent(movedItem, newIndex);

            if (oldCurrentItem != null && IndexOf(oldCurrentItem) >= 0)
            {
                MoveCurrentTo(oldCurrentItem);
            }
            else if (oldIsCurrentBeforeFirst)
            {
                MoveCurrentToPosition(-1);
            }
            else if (oldIsCurrentAfterLast)
            {
                MoveCurrentToPosition(Count);
            }
        }

        /// <summary>
        /// Handles adding an item into the collection, and applying sorting, filtering, grouping, paging.
        /// </summary>
        /// <param name="item">Item to insert in the collection</param>
        /// <param name="index">Index to insert item into</param>
        private void ProcessInsertToCollection(object item, int index)
        {
            // first check to see if it passes the filter
            if (Filter == null || PassesFilter(item))
            {
                if (SortDescriptions.Count > 0)
                {
                    var itemType = ItemType;
                    foreach (var sort in SortDescriptions)
                    sort.Initialize(itemType);

                    // create the SortFieldComparer to use
                    var sortFieldComparer = new MergedComparer(this);

                    // check if the item would be in sorted order if inserted into the specified index
                    // otherwise, calculate the correct sorted index
                    if (index < 0 || /* if item was not originally part of list */
                    (index > 0 && (sortFieldComparer.Compare(item, InternalItemAt(index - 1)) < 0)) || /* item has moved up in the list */
                    ((index < InternalList.Count - 1) && (sortFieldComparer.Compare(item, InternalItemAt(index)) > 0))) /* item has moved down in the list */
                    {
                        index = sortFieldComparer.FindInsertIndex(item, _internalList);
                    }
                }

                // make sure that the specified insert index is within the valid range
                // otherwise, just add it to the end. the index can be set to an invalid
                // value if the item was originally not in the collection, on a different
                // page, or if it had been previously filtered out.
                if (index < 0 || index > _internalList.Count)
                {
                    index = _internalList.Count;
                }

                _internalList.Insert(index, item);
            }
        }

        /// <summary>
        /// Will call RefreshOverride and clear the NeedsRefresh flag
        /// </summary>
        private void RefreshInternal()
        {
            ViewProjectionMutationScope viewProjectionMutation = default;
            try
            {
                viewProjectionMutation = RefreshOverride();
                SetFlag(CollectionViewFlags.NeedsRefresh, false);
            }
            finally
            {
                viewProjectionMutation.Dispose();
                _pendingSortOnlyRefresh = false;
            }
        }

        /// <summary>
        /// Refresh, or mark that refresh is needed when defer cycle completes.
        /// </summary>
        private void RefreshOrDefer()
        {
            _pendingSortOnlyRefresh = false;
            if (IsRefreshDeferred)
            {
                SetFlag(CollectionViewFlags.NeedsRefresh, true);
            }
            else
            {
                RefreshInternal();
            }
        }

        private void RefreshOrDeferForSort()
        {
            if (IsRefreshDeferred)
            {
                if (!CheckFlag(CollectionViewFlags.NeedsRefresh))
                {
                    _pendingSortOnlyRefresh = true;
                }

                SetFlag(CollectionViewFlags.NeedsRefresh, true);
            }
            else
            {
                _pendingSortOnlyRefresh = true;
                RefreshInternal();
            }
        }

        /// <summary>
        /// Re-create the view, using any SortDescriptions.
        /// Also updates currency information.
        /// </summary>
        //TODO Paging
        private ViewProjectionMutationScope RefreshOverride()
        {
            NotifyCollectionChangedEventArgs pendingSourceReset = _pendingSourceReset;
            _pendingSourceReset = null;
            using var pendingSourceMutation = pendingSourceReset != null && _sourceMutationDepth == 0
                ? BeginSourceMutation(pendingSourceReset)
                : default;
            using var activity = DataGridDiagnostics.CollectionRefresh();
            using var _ = DataGridDiagnostics.BeginCollectionRefresh();
            activity?.SetTag(DataGridDiagnostics.Tags.UsesLocalArray, UsesLocalArray);
            activity?.SetTag(DataGridDiagnostics.Tags.FilterEnabled, Filter != null);
            activity?.SetTag(DataGridDiagnostics.Tags.SortDescriptions, SortDescriptions?.Count ?? 0);
            activity?.SetTag(DataGridDiagnostics.Tags.GroupDescriptions, GroupDescriptions?.Count ?? 0);
            activity?.SetTag(DataGridDiagnostics.Tags.PageSize, PageSize);
            activity?.SetTag(DataGridDiagnostics.Tags.PageIndex, PageIndex);

            object oldCurrentItem = CurrentItem;
            int oldCurrentPosition = CurrentPosition;
            bool oldIsCurrentAfterLast = IsCurrentAfterLast;
            bool oldIsCurrentBeforeFirst = IsCurrentBeforeFirst;

            // set IsGrouping to false
            _isGrouping = false;

            // force currency off the collection (gives user a chance to save dirty information)
            OnCurrentChanging();

            // if there's no sort/filter/paging/grouping, just use the collection's array
            if (UsesLocalArray)
            {
                try
                {
                    // apply filtering/sorting through the PrepareLocalArray method
                    _internalList = PrepareLocalArray(_sourceCollection);

                    // apply grouping
                    if (PageSize == 0)
                    {
                        PrepareGroups();
                    }
                    else
                    {
                        PrepareTemporaryGroups();
                        PrepareGroupsForCurrentPage();
                    }
                }
                catch (TargetInvocationException e)
                {
                    // If there's an exception while invoking PrepareLocalArray,
                    // we want to unwrap it and throw its inner exception
                    if (e.InnerException != null)
                    {
                        throw e.InnerException;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            else
            {
                ResetInternalList();
            }

            // check if PageIndex is still valid after filter/sort
            if (PageSize > 0 &&
            PageIndex > 0 &&
            PageIndex >= PageCount)
            {
                MoveToPage(PageCount - 1);
            }

            // Sorting, filtering, grouping, and paging materialize a private projection. The
            // source-level mutation boundary cannot expose that projection before this point.
            // Start a second, stable boundary before currency/reset notifications so consumers
            // can reconcile identity and indexes atomically against the final view.
            if (UsesLocalArray && _sourceMutationDepth > 0)
            {
                PromoteSourceMutationToStableProjection();
            }
            ViewProjectionMutationScope viewProjectionMutation = UsesLocalArray && _sourceMutationDepth == 0
                ? BeginViewProjectionMutation()
                : default;

            _isReorderOnlyReset = _pendingSortOnlyRefresh;
            try
            {
                // reset currency values
                ResetCurrencyValues(oldCurrentItem, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);

                OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));

                // now raise currency changes at the end
                RaiseCurrencyChanges(false, oldCurrentItem, oldCurrentPosition, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);

                activity?.SetTag(DataGridDiagnostics.Tags.IsGrouping, IsGrouping);
                return viewProjectionMutation;
            }
            catch
            {
                viewProjectionMutation.Dispose();
                throw;
            }
            finally
            {
                _isReorderOnlyReset = false;
            }
        }

        /// <summary>
        ///     Notify listeners that this View has changed
        /// </summary>
        /// <remarks>
        ///     CollectionViews (and sub-classes) should take their filter/sort/grouping/paging
        ///     into account before calling this method to forward CollectionChanged events.
        /// </remarks>
        /// <param name="args">
        ///     The NotifyCollectionChangedEventArgs to be passed to the EventHandler
        /// </param>
        //TODO Paging
        private void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            // Incremental sorted/filtered/grouped/paged mutations finish updating the local
            // projection immediately before their first public view notification. Promote the
            // outer source boundary here so it remains active through the subsequent currency
            // notifications and completes only when the whole source mutation unwinds.
            if (UsesLocalArray)
            {
                PromoteSourceMutationToStableProjection();
            }

            unchecked
            {
                // invalidate enumerators because of a change
                ++_timestamp;
            }

            if (CollectionChanged != null)
            {
                if (args.Action != NotifyCollectionChangedAction.Add || PageSize == 0 || args.NewStartingIndex < Count)
                {
                    CollectionChanged(this, args);
                }
            }

            // Collection changes change the count unless an item is being
            // replaced within the collection.
            if (args.Action != NotifyCollectionChangedAction.Replace)
            {
                OnPropertyChanged(nameof(Count));
            }

            bool listIsEmpty = IsEmpty;
            if (listIsEmpty != CheckFlag(CollectionViewFlags.CachedIsEmpty))
            {
                SetFlag(CollectionViewFlags.CachedIsEmpty, listIsEmpty);
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

    }
}
