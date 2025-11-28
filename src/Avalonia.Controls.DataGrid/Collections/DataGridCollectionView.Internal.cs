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
        /// Returns true if specified flag in flags is set.
        /// </summary>
        /// <param name="flags">Flag we are checking for</param>
        /// <returns>Whether the specified flag is set</returns>
        private bool CheckFlag(CollectionViewFlags flags)
        {
            return _flags.HasAllFlags(flags);
        }

        /// <summary>
        /// Sets the specified Flag(s)
        /// </summary>
        /// <param name="flags">Flags we want to set</param>
        /// <param name="value">Value we want to set these flags to</param>
        private void SetFlag(CollectionViewFlags flags, bool value)
        {
            if (value)
            {
                _flags = _flags | flags;
            }
            else
            {
                _flags = _flags & ~flags;
            }
        }

        /// <summary>
        /// Convert a value for the index passed in to the index it would be
        /// relative to the InternalIndex property.
        /// </summary>
        /// <param name="index">Index to convert</param>
        /// <returns>Value for the InternalIndex</returns>
        //TODO Paging
        private int ConvertToInternalIndex(int index)
        {
            Debug.Assert(index > -1, "Unexpected index == -1");
            if (PageSize > 0)
            {
                return (_pageSize * PageIndex) + index;
            }
            else
            {
                return index;
            }
        }

        /// <summary>
        /// Copy all items from the source collection to the internal list for processing.
        /// </summary>
        private void CopySourceToInternalList()
        {
            _internalList = new List<object>();

            IEnumerator enumerator = SourceCollection.GetEnumerator();

            while (enumerator.MoveNext())
            {
                _internalList.Add(enumerator.Current);
            }
        }

        /// <summary>
        /// Subtracts from the deferLevel counter and calls Refresh() if there are no other defers
        /// </summary>
        private void EndDefer()
        {
            --_deferLevel;

            if (_deferLevel == 0)
            {
                if (CheckFlag(CollectionViewFlags.IsUpdatePageSizeDeferred))
                {
                    SetFlag(CollectionViewFlags.IsUpdatePageSizeDeferred, false);
                    PageSize = _cachedPageSize;
                }

                if (CheckFlag(CollectionViewFlags.IsMoveToPageDeferred))
                {
                    SetFlag(CollectionViewFlags.IsMoveToPageDeferred, false);
                    MoveToPage(_cachedPageIndex);
                    _cachedPageIndex = -1;
                }

                if (CheckFlag(CollectionViewFlags.NeedsRefresh))
                {
                    Refresh();
                }
            }
        }

        /// <summary>
        /// Makes sure that the ItemConstructor is set for the correct type
        /// </summary>
        private void EnsureItemConstructor()
        {
            if (!_itemConstructorIsValid)
            {
                Type itemType = ItemType;
                if (itemType != null)
                {
                    _itemConstructor = itemType.GetConstructor(Type.EmptyTypes);
                    _itemConstructorIsValid = true;
                }
            }
        }

        /// <summary>
        ///  If the IEnumerable has changed, bring the collection up to date.
        ///  (This isn't necessary if the IEnumerable is also INotifyCollectionChanged
        ///  because we keep the collection in sync incrementally.)
        /// </summary>
        private void EnsureCollectionInSync()
        {
            // if the IEnumerable is not a INotifyCollectionChanged
            if (_pollForChanges)
            {
                try
                {
                    _trackingEnumerator.MoveNext();
                }
                catch (InvalidOperationException)
                {
                    // When the collection has been modified, calling MoveNext()
                    // on the enumerator throws an InvalidOperationException, stating
                    // that the collection has been modified. Therefore, we know when
                    // to update our internal collection.
                    _trackingEnumerator = SourceCollection.GetEnumerator();
                    RefreshOrDefer();
                }
            }
        }

        private void OnBindingListChanged(object sender, ListChangedEventArgs e)
        {
            if (!CheckFlag(CollectionViewFlags.ShouldProcessCollectionChanged))
            {
                return;
            }

            // Use Reset when we have local transformations to ensure consistency.
            bool useReset = UsesLocalArray;

            switch (e.ListChangedType)
            {
                case ListChangedType.Reset:
                    ProcessCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    return;
                case ListChangedType.ItemAdded:
                    if (useReset)
                {
                    ProcessCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    return;
                }

                    var addedItem = GetBindingListItem(e.NewIndex);
                    if (IndexOf(addedItem) >= 0)
                    {
                        return;
                    }

                    ProcessCollectionChanged(
                        new NotifyCollectionChangedEventArgs(
                            NotifyCollectionChangedAction.Add,
                            addedItem,
                            e.NewIndex));
                    return;
                case ListChangedType.ItemDeleted:
                    if (useReset)
                    {
                        ProcessCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                        return;
                    }

                    ProcessCollectionChanged(
                        new NotifyCollectionChangedEventArgs(
                            NotifyCollectionChangedAction.Remove,
                            GetBindingListItemFromSnapshot(e.NewIndex),
                            e.NewIndex));
                    return;
                case ListChangedType.ItemMoved:
                    if (useReset)
                    {
                        ProcessCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                        return;
                    }

                    ProcessCollectionChanged(
                        new NotifyCollectionChangedEventArgs(
                            NotifyCollectionChangedAction.Move,
                            GetBindingListItem(e.NewIndex),
                            e.NewIndex,
                            e.OldIndex));
                    return;
                case ListChangedType.ItemChanged:
                    if (useReset)
                    {
                        ProcessCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                        return;
                    }

                    var changedItem = GetBindingListItem(e.NewIndex);
                    ProcessCollectionChanged(
                        new NotifyCollectionChangedEventArgs(
                            NotifyCollectionChangedAction.Replace,
                            changedItem,
                            changedItem,
                            e.NewIndex));
                    return;
                default:
                    ProcessCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    return;
            }
        }

        private object GetBindingListItem(int index)
        {
            if (_bindingList == null || index < 0 || index >= _bindingList.Count)
            {
                return null;
            }

            return _bindingList[index];
        }

        private object GetBindingListItemFromSnapshot(int index)
        {
            if (index >= 0 && index < _internalList.Count)
            {
                return _internalList[index];
            }

            return null;
        }

        /// <summary>
        /// Helper function used to determine the type of an item
        /// </summary>
        /// <param name="useRepresentativeItem">Whether we should use a representative item</param>
        /// <returns>The type of the items in the collection</returns>
        private Type GetItemType(bool useRepresentativeItem)
        {
            Type collectionType = SourceCollection.GetType();
            Type[] interfaces = collectionType.GetInterfaces();

            // Look for IEnumerable<T>.  All generic collections should implement
            //   We loop through the interface list, rather than call
            // GetInterface(IEnumerableT), so that we handle an ambiguous match
            // (by using the first match) without an exception.
            for (int i = 0; i < interfaces.Length; ++i)
            {
                Type interfaceType = interfaces[i];
                if (interfaceType.Name == typeof(IEnumerable<>).Name)
                {
                    // found IEnumerable<>, extract T
                    Type[] typeParameters = interfaceType.GetGenericArguments();
                    if (typeParameters.Length == 1)
                    {
                        return typeParameters[0];
                    }
                }
            }

            // No generic information found.  Use a representative item instead.
            if (useRepresentativeItem)
            {
                // get type of a representative item
                object item = GetRepresentativeItem();
                if (item != null)
                {
                    return item.GetType();
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a representative item from the collection
        /// </summary>
        /// <returns>An item that can represent the collection</returns>
        private object GetRepresentativeItem()
        {
            if (IsEmpty)
            {
                return null;
            }

            IEnumerator enumerator = GetEnumerator();
            while (enumerator.MoveNext())
            {
                object item = enumerator.Current;
                // Since this collection view does not support a NewItemPlaceholder,
                // simply return the first non-null item.
                if (item != null)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Return index of item in the internal list.
        /// </summary>
        /// <param name="item">The item we are checking</param>
        /// <returns>Integer value on where in the InternalList the object is located</returns>
        private int InternalIndexOf(object item)
        {
            return InternalList.IndexOf(item);
        }

        /// <summary>
        /// Return item at the given index in the internal list.
        /// </summary>
        /// <param name="index">The index we are checking</param>
        /// <returns>The item at the specified index</returns>
        private object InternalItemAt(int index)
        {
            if (index >= 0 && index < InternalList.Count)
            {
                return InternalList[index];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Raises a PropertyChanged event.
        /// </summary>
        /// <param name="e">PropertyChangedEventArgs for this change</param>
        private void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Create, filter and sort the local index array.
        /// called from Refresh(), override in derived classes as needed.
        /// </summary>
        /// <param name="enumerable">new IEnumerable to associate this view with</param>
        /// <returns>new local array to use for this view</returns>
        private IList PrepareLocalArray(IEnumerable enumerable)
        {
            Debug.Assert(enumerable != null, "Input list to filter/sort should not be null");

            // filter the collection's array into the local array
            List<object> localList = new List<object>();

            foreach (object item in enumerable)
            {
                if (Filter == null || PassesFilter(item))
                {
                    localList.Add(item);
                }
            }

            // sort the local array
            if (!CheckFlag(CollectionViewFlags.IsDataSorted) && SortDescriptions.Count > 0)
            {
                localList = SortList(localList);
            }

            return localList;
        }

        /// <summary>
        /// Set currency back to the previous value it had if possible. If the item is no longer in view
        /// then either use the first item in the view, or if the list is empty, use null.
        /// </summary>
        /// <param name="oldCurrentItem">CurrentItem before processing changes</param>
        /// <param name="oldIsCurrentBeforeFirst">IsCurrentBeforeFirst before processing changes</param>
        /// <param name="oldIsCurrentAfterLast">IsCurrentAfterLast before processing changes</param>
        private void ResetCurrencyValues(object oldCurrentItem, bool oldIsCurrentBeforeFirst, bool oldIsCurrentAfterLast)
        {
            if (oldIsCurrentBeforeFirst || IsEmpty)
            {
                SetCurrent(null, -1);
            }
            else if (oldIsCurrentAfterLast)
            {
                SetCurrent(null, Count);
            }
            else
            {
                // try to set currency back to old current item
                // if there are duplicates, use the position of the first matching item
                int newPosition = IndexOf(oldCurrentItem);

                // if the old current item is no longer in view
                if (newPosition < 0)
                {
                    // if we are adding a new item, set it as the current item, otherwise, set it to null
                    newPosition = 0;

                    if (newPosition < Count)
                    {
                        SetCurrent(GetItemAt(newPosition), newPosition);
                    }
                    else if (!IsEmpty)
                    {
                        SetCurrent(GetItemAt(0), 0);
                    }
                    else
                    {
                        SetCurrent(null, -1);
                    }
                }
                else
                {
                    SetCurrent(oldCurrentItem, newPosition);
                }
            }
        }

        /// <summary>
        /// Set new SortDescription collection; re-hook collection change notification handler
        /// </summary>
        /// <param name="descriptions">SortDescriptionCollection to set the property value to</param>
        private void SetSortDescriptions(DataGridSortDescriptionCollection descriptions)
        {
            if (_sortDescriptions != null)
            {
                _sortDescriptions.CollectionChanged -= SortDescriptionsChanged;
            }

            _sortDescriptions = descriptions;

            if (_sortDescriptions != null)
            {
                Debug.Assert(_sortDescriptions.Count == 0, "must be empty SortDescription collection");
                _sortDescriptions.CollectionChanged += SortDescriptionsChanged;
            }
        }

        /// <summary>
        /// SortDescription was added/removed, refresh DataGridCollectionView
        /// </summary>
        /// <param name="sender">Sender that triggered this handler</param>
        /// <param name="e">NotifyCollectionChangedEventArgs for this change</param>
        private void SortDescriptionsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsAddingNew || IsEditingItem)
            {
                throw new InvalidOperationException(GetOperationNotAllowedDuringAddOrEditText("Sorting"));
            }

            bool handledByBindingList = TryApplyBindingListSort();
            if (handledByBindingList && SortDescriptions.Count > 0)
            {
                SetFlag(CollectionViewFlags.IsDataSorted, true);
            }
            else
            {
                SetFlag(CollectionViewFlags.IsDataSorted, false);
            }

            // we want to make sure that the data is refreshed before we try to move to a page
            // since the refresh would take care of the filtering, sorting, and grouping.
            RefreshOrDefer();

            if (PageSize > 0)
            {
                if (IsRefreshDeferred)
                {
                    // set cached value and flag so that we move to first page on EndDefer
                    _cachedPageIndex = 0;
                    SetFlag(CollectionViewFlags.IsMoveToPageDeferred, true);
                }
                else
                {
                    MoveToFirstPage();
                }
            }

            OnPropertyChanged("SortDescriptions");
        }

        /// <summary>
        /// Sort the List based on the SortDescriptions property.
        /// </summary>
        /// <param name="list">List of objects to sort</param>
        /// <returns>The sorted list</returns>
        private List<object> SortList(List<object> list)
        {
            Debug.Assert(list != null, "Input list to sort should not be null");

            IEnumerable<object> seq = (IEnumerable<object>)list;
            IComparer<object> comparer = new CultureSensitiveComparer(Culture);
            var itemType = ItemType;

            foreach (DataGridSortDescription sort in SortDescriptions)
            {
                sort.Initialize(itemType);

                if (seq is IOrderedEnumerable<object> orderedEnum)
                {
                    seq = sort.ThenBy(orderedEnum);
                }
                else
                {
                    seq = sort.OrderBy(seq);
                }
            }

            return seq.ToList();
        }

        private bool TryApplyBindingListSort()
        {
            if (_bindingList == null || !_bindingList.SupportsSorting)
            {
                return false;
            }

            if (SortDescriptions.Count == 0)
            {
                _bindingList.RemoveSort();
                return true;
            }

            if (SortDescriptions.Count != 1)
            {
                return false;
            }

            var sort = SortDescriptions[0];
            if (string.IsNullOrEmpty(sort.PropertyPath))
            {
                return false;
            }

            PropertyDescriptorCollection properties = null;
            if (_bindingList is ITypedList typedList)
            {
                properties = typedList.GetItemProperties(null);
            }
            else if (ItemType != null)
            {
                properties = TypeDescriptor.GetProperties(ItemType);
            }
            else if (_bindingList.Count > 0)
            {
                properties = TypeDescriptor.GetProperties(_bindingList[0]!);
            }

            var descriptor = properties?.Find(NormalizeBindingListPropertyName(sort.PropertyPath), true);
            if (descriptor == null)
            {
                return false;
            }

            _bindingList.ApplySort(descriptor, sort.Direction);
            return true;
        }

        private static string NormalizeBindingListPropertyName(string propertyPath)
        {
            var propertyNames = TypeHelper.SplitPropertyPath(propertyPath);
            if (propertyNames.Count == 0)
            {
                return propertyPath;
            }

            var first = TypeHelper.RemoveDefaultMemberName(propertyNames[0]);
            if (!string.IsNullOrEmpty(first) && first[0] == TypeHelper.LeftIndexerToken && first[first.Length - 1] == TypeHelper.RightIndexerToken)
            {
                first = first.Substring(1, first.Length - 2);
            }

            return first;
        }

        /// <summary>
        /// Helper to validate that we are not in the middle of a DeferRefresh
        /// and throw if that is the case.
        /// </summary>
        private void VerifyRefreshNotDeferred()
        {
            // If the Refresh is being deferred to change filtering or sorting of the
            // data by this DataGridCollectionView, then DataGridCollectionView will not reflect the correct
            // state of the underlying data.
            if (IsRefreshDeferred)
            {
                throw new InvalidOperationException("Cannot change or check the contents or current position of the CollectionView while Refresh is being deferred.");
            }
        }

        /// <summary>
        /// Helper for SortList to handle nested properties (e.g. Address.Street)
        /// </summary>
        /// <param name="item">parent object</param>
        /// <param name="propertyPath">property names path</param>
        /// <param name="propertyType">property type that we want to check for</param>
        /// <returns>child object</returns>
        private static object InvokePath(object item, string propertyPath, Type propertyType)
        {
            object propertyValue = TypeHelper.GetNestedPropertyValue(item, propertyPath, propertyType, out Exception exception);
            if (exception != null)
            {
                throw exception;
            }
            return propertyValue;
        }

    }
}
