// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

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
        /// <summary>
        /// Add a new item to the underlying collection.  Returns the new item.
        /// After calling AddNew and changing the new item as desired, either
        /// CommitNew or CancelNew" should be called to complete the transaction.
        /// </summary>
        /// <returns>The new item we are adding</returns>
        //TODO Paging
        public object AddNew()
        {
            EnsureCollectionInSync();
            VerifyRefreshNotDeferred();

            if (IsEditingItem)
            {
                // Implicitly close a previous EditItem
                CommitEdit();
            }

            // Implicitly close a previous AddNew
            CommitNew();

            // Checking CanAddNew will validate that we have the correct itemConstructor
            if (!CanAddNew)
            {
                throw new InvalidOperationException(GetOperationNotAllowedText(nameof(AddNew)));
            }

            object newItem = null;

            if (_bindingList != null && _bindingList.AllowNew)
            {
                newItem = _bindingList.AddNew();
                CurrentAddItem = newItem;

                if (newItem is IEditableObject bindingEditableObject)
                {
                    bindingEditableObject.BeginEdit();
                }

                MoveCurrentTo(newItem);
                return newItem;
            }
            else if (_bindingList != null)
            {
                throw new InvalidOperationException(GetOperationNotAllowedText(nameof(AddNew)));
            }
            else if (_itemConstructor != null)
            {
                newItem = _itemConstructor.Invoke(null);
            }

            try
            {
                // temporarily disable the CollectionChanged event
                // handler so filtering, sorting, or grouping
                // doesn't get applied yet
                SetFlag(CollectionViewFlags.ShouldProcessCollectionChanged, false);

                if (_bindingList == null && SourceList != null)
                {
                    SourceList.Add(newItem);
                }
            }
            finally
            {
                SetFlag(CollectionViewFlags.ShouldProcessCollectionChanged, true);
            }

            // Modify our _trackingEnumerator so that it shows that our collection is "up to date"
            // and will not refresh for now.
            _trackingEnumerator = _sourceCollection.GetEnumerator();

            int addIndex;
            int removeIndex = -1;

            // Adjust index based on where it should be displayed in view.
            if (PageSize > 0)
            {
                // if the page is full (Count==PageSize), then replace last item (Count-1).
                // otherwise, we just append at end (Count).
                addIndex = Count - ((Count == PageSize) ? 1 : 0);

                // if the page is full, remove the last item to make space for the new one.
                removeIndex = (Count == PageSize) ? addIndex : -1;
            }
            else
            {
                // for non-paged lists, we want to insert the item
                // as the last item in the view
                addIndex = Count;
            }

            // if we need to remove an item from the view due to paging
            if (removeIndex > -1)
            {
                object removeItem = GetItemAt(removeIndex);
                if (IsGrouping)
                {
                    _group.RemoveFromSubgroups(removeItem);
                }

                OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove,
                removeItem,
                removeIndex));
            }

            // add the new item to the internal list
            _internalList.Insert(ConvertToInternalIndex(addIndex), newItem);
            OnPropertyChanged(nameof(ItemCount));

            object oldCurrentItem = CurrentItem;
            int oldCurrentPosition = CurrentPosition;
            bool oldIsCurrentAfterLast = IsCurrentAfterLast;
            bool oldIsCurrentBeforeFirst = IsCurrentBeforeFirst;

            AdjustCurrencyForAdd(null, addIndex);

            if (IsGrouping)
            {
                _group.InsertSpecialItem(_group.Items.Count, newItem, false);
                if (PageSize > 0)
                {
                    _temporaryGroup.InsertSpecialItem(_temporaryGroup.Items.Count, newItem, false);
                }
            }

            // fire collection changed.
            OnCollectionChanged(
            new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Add,
            newItem,
            addIndex));

            RaiseCurrencyChanges(false, oldCurrentItem, oldCurrentPosition, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);

            // set the current new item
            CurrentAddItem = newItem;

            MoveCurrentTo(newItem);

            // if the new item is editable, call BeginEdit on it
            if (newItem is IEditableObject editableObject)
            {
                editableObject.BeginEdit();
            }

            return newItem;
        }

        /// <summary>
        /// Complete the transaction started by <seealso cref="EditItem"/>.
        /// The pending changes (if any) to the item are discarded.
        /// </summary>
        public void CancelEdit()
        {
            if (IsAddingNew)
            {
                throw new InvalidOperationException(GetOperationNotAllowedText(nameof(CancelEdit), nameof(AddNew)));
            }
            else if (!CanCancelEdit)
            {
                throw new InvalidOperationException("CancelEdit is not supported for the current edit item.");
            }

            VerifyRefreshNotDeferred();

            if (CurrentEditItem == null)
            {
                return;
            }

            object editItem = CurrentEditItem;
            CurrentEditItem = null;

            if (editItem is IEditableObject ieo)
            {
                ieo.CancelEdit();
            }
            else
            {
                throw new InvalidOperationException("CancelEdit is not supported for the current edit item.");
            }
        }

        /// <summary>
        /// Complete the transaction started by AddNew. The new
        /// item is removed from the collection.
        /// </summary>
        //TODO Paging
        public void CancelNew()
        {
            if (IsEditingItem)
            {
                throw new InvalidOperationException(GetOperationNotAllowedText(nameof(CancelNew), nameof(EditItem)));
            }

            VerifyRefreshNotDeferred();

            if (CurrentAddItem == null)
            {
                return;
            }

            // get index of item before it is removed
            int index = IndexOf(CurrentAddItem);

            if (_bindingList != null)
            {
                var newItem = CurrentAddItem;
                EndAddNew(true);

                if (_bindingList is ICancelAddNew cancelAddNew && index >= 0)
                {
                    cancelAddNew.CancelNew(index);
                }
                else
                {
                    if (index >= 0 && index < _bindingList.Count)
                    {
                        _bindingList.RemoveAt(index);
                    }
                    else
                    {
                        _bindingList.Remove(newItem);
                    }
                }

                _trackingEnumerator = _sourceCollection.GetEnumerator();
                return;
            }

            // remove the new item from the underlying collection
            try
            {
                // temporarily disable the CollectionChanged event
                // handler so filtering, sorting, or grouping
                // doesn't get applied yet
                SetFlag(CollectionViewFlags.ShouldProcessCollectionChanged, false);

                if (SourceList != null)
                {
                    SourceList.Remove(CurrentAddItem);
                }
            }
            finally
            {
                SetFlag(CollectionViewFlags.ShouldProcessCollectionChanged, true);
            }

            // Modify our _trackingEnumerator so that it shows that our collection is "up to date"
            // and will not refresh for now.
            _trackingEnumerator = _sourceCollection.GetEnumerator();

            // fire the correct events
            if (CurrentAddItem != null)
            {
                object newItem = EndAddNew(true);

                int addIndex = -1;

                // Adjust index based on where it should be displayed in view.
                if (PageSize > 0 && !OnLastLocalPage)
                {
                    // if there is paging and we are not on the last page, we need
                    // to bring in an item from the next page.
                    addIndex = Count - 1;
                }

                // remove the new item from the internal list
                InternalList.Remove(newItem);

                if (IsGrouping)
                {
                    _group.RemoveSpecialItem(_group.Items.Count - 1, newItem, false);
                    if (PageSize > 0)
                    {
                        _temporaryGroup.RemoveSpecialItem(_temporaryGroup.Items.Count - 1, newItem, false);
                    }
                }

                OnPropertyChanged(nameof(ItemCount));

                object oldCurrentItem = CurrentItem;
                int oldCurrentPosition = CurrentPosition;
                bool oldIsCurrentAfterLast = IsCurrentAfterLast;
                bool oldIsCurrentBeforeFirst = IsCurrentBeforeFirst;

                AdjustCurrencyForRemove(index);

                // fire collection changed.
                OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove,
                newItem,
                index));

                RaiseCurrencyChanges(false, oldCurrentItem, oldCurrentPosition, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);

                // if we need to add an item into the view due to paging
                if (addIndex > -1)
                {
                    int internalIndex = ConvertToInternalIndex(addIndex);
                    object addItem = null;
                    if (IsGrouping)
                    {
                        addItem = _temporaryGroup.LeafAt(internalIndex);
                        _group.AddToSubgroups(addItem, loading: false);
                    }
                    else
                    {
                        addItem = InternalItemAt(internalIndex);
                    }

                    OnCollectionChanged(
                    new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    addItem,
                    IndexOf(addItem)));
                }
            }
        }

        /// <summary>
        /// Complete the transaction started by <seealso cref="EditItem"/>.
        /// The pending changes (if any) to the item are committed.
        /// </summary>
        //TODO Paging
        public void CommitEdit()
        {
            if (IsAddingNew)
            {
                throw new InvalidOperationException(GetOperationNotAllowedText(nameof(CommitEdit), nameof(AddNew)));
            }

            VerifyRefreshNotDeferred();

            if (CurrentEditItem == null)
            {
                return;
            }

            object editItem = CurrentEditItem;
            CurrentEditItem = null;

            if (editItem is IEditableObject ieo)
            {
                ieo.EndEdit();
            }

            if (UsesLocalArray)
            {
                // first remove the item from the array so that we can insert into the correct position
                int removeIndex = IndexOf(editItem);
                int internalRemoveIndex = InternalIndexOf(editItem);
                _internalList.Remove(editItem);

                // check whether to restore currency to the item being edited
                object restoreCurrencyTo = (editItem == CurrentItem) ? editItem : null;

                if (removeIndex >= 0 && IsGrouping)
                {
                    // we can't just call RemoveFromSubgroups, as the group name
                    // for the item may have changed during the edit.
                    _group.RemoveItemFromSubgroupsByExhaustiveSearch(editItem);
                    if (PageSize > 0)
                    {
                        _temporaryGroup.RemoveItemFromSubgroupsByExhaustiveSearch(editItem);
                    }
                }

                object oldCurrentItem = CurrentItem;
                int oldCurrentPosition = CurrentPosition;
                bool oldIsCurrentAfterLast = IsCurrentAfterLast;
                bool oldIsCurrentBeforeFirst = IsCurrentBeforeFirst;

                // only adjust currency and fire the event if we actually removed the item
                if (removeIndex >= 0)
                {
                    AdjustCurrencyForRemove(removeIndex);

                    // raise the remove event so we can next insert it into the correct place
                    OnCollectionChanged(
                    new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove,
                    editItem,
                    removeIndex));
                }

                // check to see that the item will be added back in
                bool passedFilter = PassesFilter(editItem);

                // if we removed all items from the current page,
                // move to the previous page. we do not need to
                // fire additional notifications, as moving the page will
                // trigger a reset.
                if (NeedToMoveToPreviousPage && !passedFilter)
                {
                    MoveToPreviousPage();
                    return;
                }

                // next process adding it into the correct location
                ProcessInsertToCollection(editItem, internalRemoveIndex);

                int pageStartIndex = PageIndex * PageSize;
                int nextPageStartIndex = pageStartIndex + PageSize;

                if (IsGrouping)
                {
                    int leafIndex = -1;
                    if (passedFilter && PageSize > 0)
                    {
                        _temporaryGroup.AddToSubgroups(editItem, false /*loading*/);
                        leafIndex = _temporaryGroup.LeafIndexOf(editItem);
                    }

                    // if we are not paging, we should just be able to add the item.
                    // otherwise, we need to validate that it is within the current page.
                    if (passedFilter && (PageSize == 0 ||
                    (pageStartIndex <= leafIndex && nextPageStartIndex > leafIndex)))
                    {
                        _group.AddToSubgroups(editItem, false /*loading*/);
                        int addIndex = IndexOf(editItem);
                        AdjustCurrencyForEdit(restoreCurrencyTo, addIndex);
                        OnCollectionChanged(
                        new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Add,
                        editItem,
                        addIndex));
                    }
                    else if (PageSize > 0)
                    {
                        int addIndex = -1;
                        if (passedFilter && leafIndex < pageStartIndex)
                        {
                            // if the item was added to an earlier page, then we need to bring
                            // in the item that would have been pushed down to this page
                            addIndex = pageStartIndex;
                        }
                        else if (!OnLastLocalPage && removeIndex >= 0)
                        {
                            // if the item was added to a later page, then we need to bring in the
                            // first item from the next page
                            addIndex = nextPageStartIndex - 1;
                        }

                        object addItem = _temporaryGroup.LeafAt(addIndex);
                        if (addItem != null)
                        {
                            _group.AddToSubgroups(addItem, false /*loading*/);
                            addIndex = IndexOf(addItem);
                            AdjustCurrencyForEdit(restoreCurrencyTo, addIndex);
                            OnCollectionChanged(
                            new NotifyCollectionChangedEventArgs(
                            NotifyCollectionChangedAction.Add,
                            addItem,
                            addIndex));
                        }
                    }
                }
                else
                {
                    // if we are still within the view
                    int addIndex = IndexOf(editItem);
                    if (addIndex >= 0)
                    {
                        AdjustCurrencyForEdit(restoreCurrencyTo, addIndex);
                        OnCollectionChanged(
                        new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Add,
                        editItem,
                        addIndex));
                    }
                    else if (PageSize > 0)
                    {
                        // calculate whether the item was inserted into the previous page
                        bool insertedToPreviousPage = PassesFilter(editItem) &&
                        (InternalIndexOf(editItem) < ConvertToInternalIndex(0));
                        addIndex = insertedToPreviousPage ? 0 : Count - 1;

                        // don't fire the event if we are on the last page
                        // and we don't have any items to bring in.
                        if (insertedToPreviousPage || (!OnLastLocalPage && removeIndex >= 0))
                        {
                            AdjustCurrencyForEdit(restoreCurrencyTo, addIndex);
                            OnCollectionChanged(
                            new NotifyCollectionChangedEventArgs(
                            NotifyCollectionChangedAction.Add,
                            GetItemAt(addIndex),
                            addIndex));
                        }
                    }
                }

                // now raise currency changes at the end
                RaiseCurrencyChanges(true, oldCurrentItem, oldCurrentPosition, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);
            }
            else if (!Contains(editItem))
            {
                // if the item did not belong to the collection, add it
                InternalList.Add(editItem);
            }
        }

        /// <summary>
        /// Complete the transaction started by AddNew. We follow the WPF
        /// convention in that the view's sort, filter, and paging
        /// specifications (if any) are applied to the new item.
        /// </summary>
        //TODO Paging
        public void CommitNew()
        {
            if (IsEditingItem)
            {
                throw new InvalidOperationException(GetOperationNotAllowedText(nameof(CommitNew), nameof(EditItem)));
            }

            VerifyRefreshNotDeferred();

            if (CurrentAddItem == null)
            {
                return;
            }

            if (_bindingList != null)
            {
                EndAddNew(false);
                _trackingEnumerator = _sourceCollection.GetEnumerator();
                return;
            }

            // End the AddNew transaction
            object newItem = EndAddNew(false);

            // keep track of the current item
            object previousCurrentItem = CurrentItem;

            // Modify our _trackingEnumerator so that it shows that our collection is "up to date"
            // and will not refresh for now.
            _trackingEnumerator = _sourceCollection.GetEnumerator();

            if (UsesLocalArray)
            {
                // first remove the item from the array so that we can insert into the correct position
                int removeIndex = Count - 1;
                int internalIndex = _internalList.IndexOf(newItem);
                _internalList.Remove(newItem);

                if (IsGrouping)
                {
                    _group.RemoveSpecialItem(_group.Items.Count - 1, newItem, false);
                    if (PageSize > 0)
                    {
                        _temporaryGroup.RemoveSpecialItem(_temporaryGroup.Items.Count - 1, newItem, false);
                    }
                }

                object oldCurrentItem = CurrentItem;
                int oldCurrentPosition = CurrentPosition;
                bool oldIsCurrentAfterLast = IsCurrentAfterLast;
                bool oldIsCurrentBeforeFirst = IsCurrentBeforeFirst;

                AdjustCurrencyForRemove(removeIndex);

                // raise the remove event so we can next insert it into the correct place
                OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove,
                newItem,
                removeIndex));

                // check to see that the item will be added back in
                bool passedFilter = PassesFilter(newItem);

                // next process adding it into the correct location
                ProcessInsertToCollection(newItem, internalIndex);

                int pageStartIndex = PageIndex * PageSize;
                int nextPageStartIndex = pageStartIndex + PageSize;

                if (IsGrouping)
                {
                    int leafIndex = -1;
                    if (passedFilter && PageSize > 0)
                    {
                        _temporaryGroup.AddToSubgroups(newItem, false /*loading*/);
                        leafIndex = _temporaryGroup.LeafIndexOf(newItem);
                    }

                    // if we are not paging, we should just be able to add the item.
                    // otherwise, we need to validate that it is within the current page.
                    if (passedFilter && (PageSize == 0 ||
                    (pageStartIndex <= leafIndex && nextPageStartIndex > leafIndex)))
                    {
                        _group.AddToSubgroups(newItem, false /*loading*/);
                        int addIndex = IndexOf(newItem);

                        // adjust currency to either the previous current item if possible
                        // or to the item at the end of the list where the new item was.
                        if (previousCurrentItem != null)
                        {
                            if (Contains(previousCurrentItem))
                            {
                                AdjustCurrencyForAdd(previousCurrentItem, addIndex);
                            }
                            else
                            {
                                AdjustCurrencyForAdd(GetItemAt(Count - 1), addIndex);
                            }
                        }

                        OnCollectionChanged(
                        new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Add,
                        newItem,
                        addIndex));
                    }
                    else
                    {
                        if (!passedFilter && (PageSize == 0 || OnLastLocalPage))
                        {
                            AdjustCurrencyForRemove(removeIndex);
                        }
                        else if (PageSize > 0)
                        {
                            int addIndex = -1;
                            if (passedFilter && leafIndex < pageStartIndex)
                            {
                                // if the item was added to an earlier page, then we need to bring
                                // in the item that would have been pushed down to this page
                                addIndex = pageStartIndex;
                            }
                            else if (!OnLastLocalPage)
                            {
                                // if the item was added to a later page, then we need to bring in the
                                // first item from the next page
                                addIndex = nextPageStartIndex - 1;
                            }

                            object addItem = _temporaryGroup.LeafAt(addIndex);
                            if (addItem != null)
                            {
                                _group.AddToSubgroups(addItem, false /*loading*/);
                                addIndex = IndexOf(addItem);

                                // adjust currency to either the previous current item if possible
                                // or to the item at the end of the list where the new item was.
                                if (previousCurrentItem != null)
                                {
                                    if (Contains(previousCurrentItem))
                                    {
                                        AdjustCurrencyForAdd(previousCurrentItem, addIndex);
                                    }
                                    else
                                    {
                                        AdjustCurrencyForAdd(GetItemAt(Count - 1), addIndex);
                                    }
                                }

                                OnCollectionChanged(
                                new NotifyCollectionChangedEventArgs(
                                NotifyCollectionChangedAction.Add,
                                addItem,
                                addIndex));
                            }
                        }
                    }
                }
                else
                {
                    // if we are still within the view
                    int addIndex = IndexOf(newItem);
                    if (addIndex >= 0)
                    {
                        AdjustCurrencyForAdd(newItem, addIndex);
                        OnCollectionChanged(
                        new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Add,
                        newItem,
                        addIndex));
                    }
                    else
                    {
                        if (!passedFilter && (PageSize == 0 || OnLastLocalPage))
                        {
                            AdjustCurrencyForRemove(removeIndex);
                        }
                        else if (PageSize > 0)
                        {
                            bool insertedToPreviousPage = InternalIndexOf(newItem) < ConvertToInternalIndex(0);
                            addIndex = insertedToPreviousPage ? 0 : Count - 1;

                            // don't fire the event if we are on the last page
                            // and we don't have any items to bring in.
                            if (insertedToPreviousPage || !OnLastLocalPage)
                            {
                                AdjustCurrencyForAdd(null, addIndex);
                                OnCollectionChanged(
                                new NotifyCollectionChangedEventArgs(
                                NotifyCollectionChangedAction.Add,
                                GetItemAt(addIndex),
                                addIndex));
                            }
                        }
                    }
                }

                // we want to fire the current changed event, even if we kept
                // the same current item and position, since the item was
                // removed/added back to the collection
                RaiseCurrencyChanges(true, oldCurrentItem, oldCurrentPosition, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);
            }
        }

        /// <summary>
        /// Begins an editing transaction on the given item.  The transaction is
        /// completed by calling either CommitEdit or CancelEdit.  Any changes made
        /// to the item during the transaction are considered "pending", provided
        /// that the view supports the notion of "pending changes" for the given item.
        /// </summary>
        /// <param name="item">Item we want to edit</param>
        public void EditItem(object item)
        {
            VerifyRefreshNotDeferred();

            if (IsAddingNew)
            {
                if (Object.Equals(item, CurrentAddItem))
                {
                    // EditItem(newItem) is a no-op
                    return;
                }

                // implicitly close a previous AddNew
                CommitNew();
            }

            // implicitly close a previous EditItem transaction
            CommitEdit();

            CurrentEditItem = item;

            if (item is IEditableObject ieo)
            {
                ieo.BeginEdit();
            }
        }

        /// <summary>
        /// Common functionality used by CommitNew, CancelNew, and when the
        /// new item is removed by Remove or Refresh.
        /// </summary>
        /// <param name="cancel">Whether we canceled the add</param>
        /// <returns>The new item we ended adding</returns>
        private object EndAddNew(bool cancel)
        {
            object newItem = CurrentAddItem;

            CurrentAddItem = null;    // leave "adding-new" mode

            if (newItem is IEditableObject ieo)
            {
                if (cancel)
                {
                    ieo.CancelEdit();
                }
                else
                {
                    ieo.EndEdit();
                }
            }

            return newItem;
        }

        /// <summary>
        /// Fix up CurrentPosition and CurrentItem after a collection change
        /// </summary>
        /// <param name="newCurrentItem">Item that we want to set currency to</param>
        /// <param name="index">Index of item involved in the collection change</param>
        private void AdjustCurrencyForAdd(object newCurrentItem, int index)
        {
            if (newCurrentItem != null)
            {
                int newItemIndex = IndexOf(newCurrentItem);

                // if we already have the correct currency set, we don't
                // want to unnecessarily fire events
                if (newItemIndex >= 0 && (newItemIndex != CurrentPosition || !IsCurrentInSync))
                {
                    OnCurrentChanging();
                    SetCurrent(newCurrentItem, newItemIndex);
                }
                return;
            }

            if (Count == 1)
            {
                if (CurrentItem != null || CurrentPosition != -1)
                {
                    // fire current changing notification
                    OnCurrentChanging();
                }

                // added first item; set current at BeforeFirst
                SetCurrent(null, -1);
            }
            else if (index <= CurrentPosition)
            {
                // fire current changing notification
                OnCurrentChanging();

                // adjust current index if insertion is earlier
                int newPosition = CurrentPosition + 1;
                if (newPosition >= Count)
                {
                    // if currency was on last item and it got shifted up,
                    // keep currency on last item.
                    newPosition = Count - 1;
                }
                SetCurrent(GetItemAt(newPosition), newPosition);
            }
        }

        /// <summary>
        /// Fix up CurrentPosition and CurrentItem after a collection change
        /// </summary>
        /// <param name="newCurrentItem">Item that we want to set currency to</param>
        /// <param name="index">Index of item involved in the collection change</param>
        private void AdjustCurrencyForEdit(object newCurrentItem, int index)
        {
            if (newCurrentItem != null && IndexOf(newCurrentItem) >= 0)
            {
                OnCurrentChanging();
                SetCurrent(newCurrentItem, IndexOf(newCurrentItem));
                return;
            }

            if (index <= CurrentPosition)
            {
                // fire current changing notification
                OnCurrentChanging();

                // adjust current index if insertion is earlier
                int newPosition = CurrentPosition + 1;
                if (newPosition < Count)
                {
                    // CurrentItem might be out of sync if underlying list is not INCC
                    // or if this Add is the result of a Replace (Rem + Add)
                    SetCurrent(GetItemAt(newPosition), newPosition);
                }
                else
                {
                    SetCurrent(null, Count);
                }
            }
        }

        /// <summary>
        /// Fix up CurrentPosition and CurrentItem after a collection change
        /// The index can be -1 if the item was removed from a previous page
        /// </summary>
        /// <param name="index">Index of item involved in the collection change</param>
        private void AdjustCurrencyForRemove(int index)
        {
            // adjust current index if deletion is earlier
            if (index < CurrentPosition)
            {
                // fire current changing notification
                OnCurrentChanging();

                SetCurrent(CurrentItem, CurrentPosition - 1);
            }

            // adjust current index if > Count
            if (CurrentPosition >= Count)
            {
                // fire current changing notification
                OnCurrentChanging();

                SetCurrentToPosition(Count - 1);
            }

            // make sure that current position and item are in sync
            if (!IsCurrentInSync)
            {
                // fire current changing notification
                OnCurrentChanging();

                SetCurrentToPosition(CurrentPosition);
            }
        }

    }
}
