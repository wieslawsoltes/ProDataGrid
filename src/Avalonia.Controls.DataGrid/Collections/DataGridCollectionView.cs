// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Utils;
using Avalonia.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Avalonia.Collections
{
    /// <summary>
    /// Event argument used for page index change notifications. The requested page move
    /// can be canceled by setting e.Cancel to True.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    sealed class PageChangingEventArgs : CancelEventArgs
    {
        /// <summary>
        /// Constructor that takes the target page index
        /// </summary>
        /// <param name="newPageIndex">Index of the requested page</param>
        public PageChangingEventArgs(int newPageIndex)
        {
            NewPageIndex = newPageIndex;
        }

        /// <summary>
        /// Gets the index of the requested page
        /// </summary>
        public int NewPageIndex
        {
            get;
            private set;
        }
    }

    /// <summary>Defines a method that enables a collection to provide a custom view for specialized sorting, filtering, grouping, and currency.</summary>
#if !DATAGRID_INTERNAL
    public
#endif
    interface IDataGridCollectionViewFactory
    {
        /// <summary>Returns a custom view for specialized sorting, filtering, grouping, and currency.</summary>
        /// <returns>A custom view for specialized sorting, filtering, grouping, and currency.</returns>
        IDataGridCollectionView CreateView();
    }

    /// <summary>
    /// DataGrid-readable view over an IEnumerable.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    sealed partial class DataGridCollectionView : IDataGridCollectionView, IDataGridEditableCollectionView, IList, INotifyPropertyChanged, ITypedList
    {
        /// <summary>
        /// Since there's nothing in the un-cancelable event args that is mutable,
        /// just create one instance to be used universally.
        /// </summary>
        private static readonly DataGridCurrentChangingEventArgs uncancelableCurrentChangingEventArgs = new DataGridCurrentChangingEventArgs(false);

        /// <summary>
        /// Value that we cache for the PageIndex if we are in a DeferRefresh,
        /// and the user has attempted to move to a different page.
        /// </summary>
        private int _cachedPageIndex = -1;

        /// <summary>
        /// Value that we cache for the PageSize if we are in a DeferRefresh,
        /// and the user has attempted to change the PageSize.
        /// </summary>
        private int _cachedPageSize;

        /// <summary>
        /// CultureInfo used in this DataGridCollectionView
        /// </summary>
        private CultureInfo _culture;

        /// <summary>
        /// Private accessor for the Monitor we use to prevent recursion
        /// </summary>
        private SimpleMonitor _currentChangedMonitor = new SimpleMonitor();

        /// <summary>
        /// Private accessor for the CurrentItem
        /// </summary>
        private object _currentItem;

        /// <summary>
        /// Private accessor for the CurrentPosition
        /// </summary>
        private int _currentPosition;

        /// <summary>
        /// The number of requests to defer Refresh()
        /// </summary>
        private int _deferLevel;

        /// <summary>
        /// The item we are currently editing
        /// </summary>
        private object _editItem;

        /// <summary>
        /// Private accessor for the Filter
        /// </summary>
        private Func<object, bool> _filter;

        /// <summary>
        /// Private accessor for the CollectionViewFlags
        /// </summary>
        private CollectionViewFlags _flags = CollectionViewFlags.ShouldProcessCollectionChanged;

        /// <summary>
        /// Optional binding list source to track ListChanged notifications and AddNew support.
        /// </summary>
        private IBindingList _bindingList;

        /// <summary>
        /// Private accessor for the Grouping data
        /// </summary>
        private CollectionViewGroupRoot _group;

        /// <summary>
        /// Private accessor for the InternalList
        /// </summary>
        private IList _internalList;

        /// <summary>
        /// Keeps track of whether groups have been applied to the
        /// collection already or not. Note that this can still be set
        /// to false even though we specify a GroupDescription, as the 
        /// collection may not have gone through the PrepareGroups function.
        /// </summary>
        private bool _isGrouping;

        /// <summary>
        /// Private accessor for indicating whether we want to point to the temporary grouping data for calculations
        /// </summary>
        private bool _isUsingTemporaryGroup;

        /// <summary>
        /// ConstructorInfo obtained from reflection for generating new items
        /// </summary>
        private ConstructorInfo _itemConstructor;

        /// <summary>
        /// Whether we have the correct ConstructorInfo information for the ItemConstructor
        /// </summary>
        private bool _itemConstructorIsValid;

        /// <summary>
        /// The new item we are getting ready to add to the collection
        /// </summary>
        private object _newItem;

        /// <summary>
        /// Private accessor for the PageIndex
        /// </summary>
        private int _pageIndex = -1;

        /// <summary>
        /// Private accessor for the PageSize
        /// </summary>
        private int _pageSize;

        /// <summary>
        /// Whether the source needs to poll for changes
        /// (if it did not implement INotifyCollectionChanged)
        /// </summary>
        private bool _pollForChanges;

        /// <summary>
        /// Private accessor for the SortDescriptions
        /// </summary>
        private DataGridSortDescriptionCollection _sortDescriptions;

        /// <summary>
        /// Private accessor for the SourceCollection
        /// </summary>
        private IEnumerable _sourceCollection;

        /// <summary>
        /// Private accessor for the Grouping data on the entire collection
        /// </summary>
        private CollectionViewGroupRoot _temporaryGroup;

        /// <summary>
        /// Timestamp used to see if there was a collection change while 
        /// processing enumerator changes
        /// </summary>
        private int _timestamp;

        /// <summary>
        /// Private accessor for the TrackingEnumerator
        /// </summary>
        private IEnumerator _trackingEnumerator;

        /// <summary>
        /// Helper constructor that sets default values for isDataSorted and isDataInGroupOrder.
        /// </summary>
        /// <param name="source">The source for the collection</param>
        public DataGridCollectionView(IEnumerable source)
            : this(source, false /*isDataSorted*/, false /*isDataInGroupOrder*/)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DataGridCollectionView class.
        /// </summary>
        /// <param name="source">The source for the collection</param>
        /// <param name="isDataSorted">Determines whether the source is already sorted</param>
        /// <param name="isDataInGroupOrder">Whether the source is already in the correct order for grouping</param>
        public DataGridCollectionView(IEnumerable source, bool isDataSorted, bool isDataInGroupOrder)
        {
            _sourceCollection = source ?? throw new ArgumentNullException(nameof(source));

            SetFlag(CollectionViewFlags.IsDataSorted, isDataSorted);
            SetFlag(CollectionViewFlags.IsDataInGroupOrder, isDataInGroupOrder);

            _temporaryGroup = new CollectionViewGroupRoot(this, isDataInGroupOrder);
            _group = new CollectionViewGroupRoot(this, false);
            _group.GroupDescriptionChanged += OnGroupDescriptionChanged;
            _group.GroupDescriptions.CollectionChanged += OnGroupByChanged;

            CopySourceToInternalList();
            _trackingEnumerator = source.GetEnumerator();

            // set currency
            if (_internalList.Count > 0)
            {
                SetCurrent(_internalList[0], 0, 1);
            }
            else
            {
                SetCurrent(null, -1, 0);
            }

            // Set flag for whether the collection is empty
            SetFlag(CollectionViewFlags.CachedIsEmpty, Count == 0);

            // If we implement change notifications, hook them up
            if (source is INotifyCollectionChanged coll)
            {
                coll.CollectionChanged += (_, args) => ProcessCollectionChanged(args);
            }
            else if (source is IBindingList bindingList)
            {
                _bindingList = bindingList;
                _bindingList.ListChanged += OnBindingListChanged;
            }
            else
            {
                // If the source doesn't raise collection change events, try to
                // detect changes by polling the enumerator
                _pollForChanges = true;
            }
        }

        /// <summary>
        /// Raise this event when the (filtered) view changes
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// CollectionChanged event (per INotifyCollectionChanged).
        /// </summary>
        event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged
        {
            add { CollectionChanged += value; }
            remove { CollectionChanged -= value; }
        }

        /// <summary>
        /// Raised when the CurrentItem property changed
        /// </summary>
        public event EventHandler CurrentChanged;

        /// <summary>
        /// Raised when the CurrentItem property is changing
        /// </summary>
        public event EventHandler<DataGridCurrentChangingEventArgs> CurrentChanging;

        /// <summary>
        /// Raised when a page index change completed
        /// </summary>
        //TODO Paging
        public event EventHandler<EventArgs> PageChanged;

        /// <summary>
        /// Raised when a page index change is requested
        /// </summary>
        //TODO Paging
        public event EventHandler<PageChangingEventArgs> PageChanging;

        /// <summary>
        /// PropertyChanged event.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// PropertyChanged event (per INotifyPropertyChanged)
        /// </summary>
        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add { PropertyChanged += value; }
            remove { PropertyChanged -= value; }
        }

        /// <summary>
        /// Enum for CollectionViewFlags
        /// </summary>
        //TODO Paging
        [Flags]
        private enum CollectionViewFlags
        {
            /// <summary>
            /// Whether the list of items (after applying the sort and filters, if any) 
            /// is already in the correct order for grouping. 
            /// </summary>
            IsDataInGroupOrder = 0x01,

            /// <summary>
            /// Whether the source collection is already sorted according to the SortDescriptions collection
            /// </summary>
            IsDataSorted = 0x02,

            /// <summary>
            /// Whether we should process the collection changed event
            /// </summary>
            ShouldProcessCollectionChanged = 0x04,

            /// <summary>
            /// Whether the current item is before the first
            /// </summary>
            IsCurrentBeforeFirst = 0x08,

            /// <summary>
            /// Whether the current item is after the last
            /// </summary>
            IsCurrentAfterLast = 0x10,

            /// <summary>
            /// Whether we need to refresh
            /// </summary>
            NeedsRefresh = 0x20,

            /// <summary>
            /// Whether we cache the IsEmpty value
            /// </summary>
            CachedIsEmpty = 0x40,

            /// <summary>
            /// Indicates whether a page index change is in process or not
            /// </summary>
            IsPageChanging = 0x80,

            /// <summary>
            /// Whether we need to move to another page after EndDefer
            /// </summary>
            IsMoveToPageDeferred = 0x100,

            /// <summary>
            /// Whether we need to update the PageSize after EndDefer
            /// </summary>
            IsUpdatePageSizeDeferred = 0x200
        }

        private Type _itemType;
        private Type ItemType
        {
            get
            {
                if (_itemType == null)
                    _itemType = GetItemType(true);

                return _itemType;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the view supports AddNew.
        /// </summary>
        public bool CanAddNew
        {
            get
            {
                return !IsEditingItem &&
                    (SourceList != null && !SourceList.IsFixedSize &&
                    ((_bindingList?.AllowNew ?? false) || CanConstructItem));
            }
        }

        /// <summary>
        /// Gets a value indicating whether the view supports the notion of "pending changes" 
        /// on the current edit item.  This may vary, depending on the view and the particular
        /// item.  For example, a view might return true if the current edit item
        /// implements IEditableObject, or if the view has special knowledge about 
        /// the item that it can use to support rollback of pending changes.
        /// </summary>
        public bool CanCancelEdit
        {
            get { return _editItem is IEditableObject; }
        }

        /// <summary>
        /// Gets a value indicating whether the PageIndex value is allowed to change or not.
        /// </summary>
        //TODO Paging
        public bool CanChangePage
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether we support filtering with this ICollectionView.
        /// </summary>
        public bool CanFilter
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether this view supports grouping.
        /// When this returns false, the rest of the interface is ignored.
        /// </summary>
        public bool CanGroup
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether the view supports Remove and RemoveAt.
        /// </summary>
        public bool CanRemove
        {
            get
            {
                return !IsEditingItem && !IsAddingNew &&
                    (SourceList != null && !SourceList.IsFixedSize);
            }
        }

        /// <summary>
        /// Gets a value indicating whether we support sorting with this ICollectionView.
        /// </summary>
        public bool CanSort
        {
            get { return true; }
        }

        /// <summary>
        /// Gets the number of records in the view after 
        /// filtering, sorting, and paging.
        /// </summary>
        //TODO Paging
        public int Count
        {
            get
            {
                EnsureCollectionInSync();
                VerifyRefreshNotDeferred();

                // if we have paging
                if (PageSize > 0 && PageIndex > -1)
                {
                    if (IsGrouping && !_isUsingTemporaryGroup)
                    {
                        return _group.ItemCount;
                    }
                    else
                    {
                        return Math.Max(0, Math.Min(PageSize, InternalCount - (_pageSize * PageIndex)));
                    }
                }
                else
                {
                    if (IsGrouping)
                    {
                        if (_isUsingTemporaryGroup)
                        {
                            return _temporaryGroup.ItemCount;
                        }
                        else
                        {
                            return _group.ItemCount;
                        }
                    }
                    else
                    {
                        return InternalCount;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets Culture to use during sorting.
        /// </summary>
        public CultureInfo Culture
        {
            get
            {
                return _culture;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (_culture != value)
                {
                    _culture = value;
                    OnPropertyChanged(nameof(Culture));
                }
            }
        }

        /// <summary>
        /// Gets the new item when an AddNew transaction is in progress
        /// Otherwise it returns null.
        /// </summary>
        public object CurrentAddItem
        {
            get
            {
                return _newItem;
            }

            private set
            {
                if (_newItem != value)
                {
                    Debug.Assert(value == null || _newItem == null, "Old and new _newItem values are unexpectedly non null");
                    _newItem = value;
                    OnPropertyChanged(nameof(IsAddingNew));
                    OnPropertyChanged(nameof(CurrentAddItem));
                }
            }
        }

        /// <summary>
        /// Gets the affected item when an EditItem transaction is in progress
        /// Otherwise it returns null.
        /// </summary>
        public object CurrentEditItem
        {
            get
            {
                return _editItem;
            }

            private set
            {
                if (_editItem != value)
                {
                    Debug.Assert(value == null || _editItem == null, "Old and new _editItem values are unexpectedly non null");
                    bool oldCanCancelEdit = CanCancelEdit;
                    _editItem = value;
                    OnPropertyChanged(nameof(IsEditingItem));
                    OnPropertyChanged(nameof(CurrentEditItem));
                    if (oldCanCancelEdit != CanCancelEdit)
                    {
                        OnPropertyChanged(nameof(CanCancelEdit));
                    }
                }
            }
        }

        /// <summary> 
        /// Gets the "current item" for this view 
        /// </summary>
        public object CurrentItem
        {
            get
            {
                VerifyRefreshNotDeferred();
                return _currentItem;
            }
        }

        /// <summary>
        /// Gets the ordinal position of the CurrentItem within the 
        /// (optionally sorted and filtered) view.
        /// </summary>
        public int CurrentPosition
        {
            get
            {
                VerifyRefreshNotDeferred();
                return _currentPosition;
            }
        }

        private string GetOperationNotAllowedDuringAddOrEditText(string action)
        {
            return $"'{action}' is not allowed during an AddNew or EditItem transaction.";
        }
        private string GetOperationNotAllowedText(string action, string transaction = null)
        {
            if (String.IsNullOrWhiteSpace(transaction))
            {
                return $"'{action}' is not allowed for this view.";
            }
            else
            {
                return $"'{action}' is not allowed during a transaction started by '{transaction}'.";
            }
        }

        /// <summary>
        /// Gets or sets the Filter, which is a callback set by the consumer of the ICollectionView
        /// and used by the implementation of the ICollectionView to determine if an
        /// item is suitable for inclusion in the view.
        /// </summary>        
        /// <exception cref="NotSupportedException">
        /// Simpler implementations do not support filtering and will throw a NotSupportedException.
        /// Use <seealso cref="CanFilter"/> property to test if filtering is supported before
        /// assigning a non-null value.
        /// </exception>
        public Func<object, bool> Filter
        {
            get
            {
                return _filter;
            }

            set
            {
                if (IsAddingNew || IsEditingItem)
                {
                    throw new InvalidOperationException(GetOperationNotAllowedDuringAddOrEditText(nameof(Filter)));
                }

                if (!CanFilter)
                {
                    throw new NotSupportedException("The Filter property cannot be set when the CanFilter property returns false.");
                }

                if (_filter != value)
                {
                    _filter = value;
                    RefreshOrDefer();
                    OnPropertyChanged(nameof(Filter));
                }
            }
        }

        /// <summary>
        /// Gets the description of grouping, indexed by level.
        /// </summary>
        public AvaloniaList<DataGridGroupDescription> GroupDescriptions
        {
            get
            {
                return _group?.GroupDescriptions;
            }
        }

        int IDataGridCollectionView.GroupingDepth => GroupDescriptions?.Count ?? 0;
        string IDataGridCollectionView.GetGroupingPropertyNameAtDepth(int level)
        {
            var groups = GroupDescriptions;
            if(groups != null && level >= 0 && level < groups.Count)
            {
                return groups[level].PropertyName;
            }
            else
            {
                return String.Empty;
            }
        }

        /// <summary>
        /// Gets the top-level groups, constructed according to the descriptions
        /// given in GroupDescriptions.
        /// </summary>
        public IAvaloniaReadOnlyList<object> Groups
        {
            get
            {
                if (!IsGrouping)
                {
                    return null;
                }

                return RootGroup?.Items;
            }
        }

        /// <summary>
        /// Gets a value indicating whether an "AddNew" transaction is in progress.
        /// </summary>
        public bool IsAddingNew
        {
            get { return _newItem != null; }
        }

        /// <summary> 
        /// Gets a value indicating whether currency is beyond the end (End-Of-File). 
        /// </summary>
        /// <returns>Whether IsCurrentAfterLast</returns>
        public bool IsCurrentAfterLast
        {
            get
            {
                VerifyRefreshNotDeferred();
                return CheckFlag(CollectionViewFlags.IsCurrentAfterLast);
            }
        }

        /// <summary> 
        /// Gets a value indicating whether currency is before the beginning (Beginning-Of-File). 
        /// </summary>
        /// <returns>Whether IsCurrentBeforeFirst</returns>
        public bool IsCurrentBeforeFirst
        {
            get
            {
                VerifyRefreshNotDeferred();
                return CheckFlag(CollectionViewFlags.IsCurrentBeforeFirst);
            }
        }

        /// <summary>
        /// Gets a value indicating whether an EditItem transaction is in progress.
        /// </summary>
        public bool IsEditingItem
        {
            get { return _editItem != null; }
        }

        /// <summary>
        /// Gets a value indicating whether the resulting (filtered) view is empty.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                EnsureCollectionInSync();
                return InternalCount == 0;
            }
        }

        /// <summary>
        /// Gets a value indicating whether a page index change is in process or not.
        /// </summary>
        //TODO Paging
        public bool IsPageChanging
        {
            get
            {
                return CheckFlag(CollectionViewFlags.IsPageChanging);
            }

            private set
            {
                if (CheckFlag(CollectionViewFlags.IsPageChanging) != value)
                {
                    SetFlag(CollectionViewFlags.IsPageChanging, value);
                    OnPropertyChanged(nameof(IsPageChanging));
                }
            }
        }

        /// <summary>
        /// Gets the minimum number of items known to be in the source collection
        /// that verify the current filter if any
        /// </summary>
        public int ItemCount
        {
            get
            {
                return InternalList.Count;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this view needs to be refreshed.
        /// </summary>
        public bool NeedsRefresh
        {
            get { return CheckFlag(CollectionViewFlags.NeedsRefresh); }
        }

        /// <summary>
        /// Gets the current page we are on. (zero based)
        /// </summary>
        //TODO Paging
        public int PageIndex
        {
            get
            {
                return _pageIndex;
            }
        }

        /// <summary>
        /// Gets or sets the number of items to display on a page. If the
        /// PageSize = 0, then we are not paging, and will display all items
        /// in the collection. Otherwise, we will have separate pages for 
        /// the items to display.
        /// </summary>
        //TODO Paging
        public int PageSize
        {
            get
            {
                return _pageSize;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "PageSize cannot have a negative value.");
                }

                // if the Refresh is currently deferred, cache the desired PageSize
                // and set the flag so that once the defer is over, we can then
                // update the PageSize.
                if (IsRefreshDeferred)
                {
                    // set cached value and flag so that we update the PageSize on EndDefer
                    _cachedPageSize = value;
                    SetFlag(CollectionViewFlags.IsUpdatePageSizeDeferred, true);
                    return;
                }

                // to see whether or not to fire an OnPropertyChanged
                int oldCount = Count;

                if (_pageSize != value)
                {
                    // Remember current currency values for upcoming OnPropertyChanged notifications
                    object oldCurrentItem = CurrentItem;
                    int oldCurrentPosition = CurrentPosition;
                    bool oldIsCurrentAfterLast = IsCurrentAfterLast;
                    bool oldIsCurrentBeforeFirst = IsCurrentBeforeFirst;

                    // Check if there is a current edited or new item so changes can be committed first.
                    if (CurrentAddItem != null || CurrentEditItem != null)
                    {
                        // Check with the ICollectionView.CurrentChanging listeners if it's OK to
                        // change the currency. If not, then we can't fire the event to allow them to
                        // commit their changes. So, we will not be able to change the PageSize.
                        if (!OkToChangeCurrent())
                        {
                            throw new InvalidOperationException("Changing the PageSize is not allowed during an AddNew or EditItem transaction.");
                        }

                        // Currently CommitNew()/CommitEdit()/CancelNew()/CancelEdit() can't handle committing or 
                        // cancelling an item that is no longer on the current page. That's acceptable and means that
                        // the potential _newItem or _editItem needs to be committed before this PageSize change.
                        // The reason why we temporarily reset currency here is to give a chance to the bound
                        // controls to commit or cancel their potential edits/addition. The DataForm calls ForceEndEdit()
                        // for example as a result of changing currency.
                        SetCurrentToPosition(-1);
                        RaiseCurrencyChanges(true /*fireChangedEvent*/, oldCurrentItem, oldCurrentPosition, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);

                        // If the bound controls did not successfully end their potential item editing/addition, we 
                        // need to throw an exception to show that the PageSize change failed. 
                        if (CurrentAddItem != null || CurrentEditItem != null)
                        {
                            throw new InvalidOperationException("Changing the PageSize is not allowed during an AddNew or EditItem transaction.");
                        }
                    }

                    _pageSize = value;
                    OnPropertyChanged(nameof(PageSize));

                    if (_pageSize == 0)
                    {
                        // update the groups for the current page
                        //***************************************
                        PrepareGroups();

                        // if we are not paging
                        MoveToPage(-1);
                    }
                    else if (_pageIndex != 0)
                    {
                        if (!CheckFlag(CollectionViewFlags.IsMoveToPageDeferred))
                        {
                            // if the temporaryGroup was not created yet and is out of sync
                            // then create it so that we can use it as a reference while paging.
                            if (IsGrouping && _temporaryGroup.ItemCount != InternalList.Count)
                            {
                                PrepareTemporaryGroups();
                            }

                            MoveToFirstPage();
                        }
                    }
                    else if (IsGrouping)
                    {
                        // if the temporaryGroup was not created yet and is out of sync
                        // then create it so that we can use it as a reference while paging.
                        if (_temporaryGroup.ItemCount != InternalList.Count)
                        {
                            // update the groups that get created for the
                            // entire collection as well as the current page
                            PrepareTemporaryGroups();
                        }

                        // update the groups for the current page
                        PrepareGroupsForCurrentPage();
                    }

                    // if the count has changed
                    if (Count != oldCount)
                    {
                        OnPropertyChanged(nameof(Count));
                    }

                    // reset currency values
                    ResetCurrencyValues(oldCurrentItem, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);

                    // send a notification that our collection has been updated
                    OnCollectionChanged(
                        new NotifyCollectionChangedEventArgs(
                            NotifyCollectionChangedAction.Reset));

                    // now raise currency changes at the end
                    RaiseCurrencyChanges(false, oldCurrentItem, oldCurrentPosition, oldIsCurrentBeforeFirst, oldIsCurrentAfterLast);
                }
            }
        }

        /// <summary>
        /// Gets the Sort criteria to sort items in collection.
        /// </summary>
        /// <remarks>
        /// <p>
        /// Clear a sort criteria by assigning SortDescription.Empty to this property.
        /// One or more sort criteria in form of <seealso cref="DataGridSortDescription"/>
        /// can be used, each specifying a property and direction to sort by.
        /// </p>
        /// </remarks>
        /// <exception cref="NotSupportedException">
        /// Simpler implementations do not support sorting and will throw a NotSupportedException.
        /// Use <seealso cref="CanSort"/> property to test if sorting is supported before adding
        /// to SortDescriptions.
        /// </exception>
        public DataGridSortDescriptionCollection SortDescriptions
        {
            get
            {
                if (_sortDescriptions == null)
                {
                    SetSortDescriptions(new DataGridSortDescriptionCollection());
                }

                return _sortDescriptions;
            }
        }

        /// <summary>
        /// Gets the source of the IEnumerable collection we are using for our view.
        /// </summary>
        public IEnumerable SourceCollection
        {
            get { return _sourceCollection; }
        }

        /// <summary>
        /// Gets the total number of items in the view before paging is applied.
        /// </summary>
        public int TotalItemCount
        {
            get
            {
                return InternalList.Count;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we have a valid ItemConstructor of the correct type
        /// </summary>
        private bool CanConstructItem
        {
            get
            {
                if (!_itemConstructorIsValid)
                {
                    EnsureItemConstructor();
                }

                return _itemConstructor != null;
            }
        }

        /// <summary>
        /// Gets the private count without taking paging or
        /// placeholders into account
        /// </summary>
        private int InternalCount
        {
            get { return InternalList.Count; }
        }

        /// <summary>
        /// Gets the InternalList
        /// </summary>
        private IList InternalList
        {
            get { return _internalList; }
        }

        /// <summary>
        /// Gets a value indicating whether CurrentItem and CurrentPosition are
        /// up-to-date with the state and content of the collection.
        /// </summary>
        private bool IsCurrentInSync
        {
            get
            {
                if (IsCurrentInView)
                {
                    return GetItemAt(CurrentPosition).Equals(CurrentItem);
                }
                else
                {
                    return CurrentItem == null;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current item is in the view
        /// </summary>
        private bool IsCurrentInView
        {
            get
            {
                VerifyRefreshNotDeferred();

                // Calling IndexOf will check whether the specified currentItem
                // is within the (paged) view.
                return IndexOf(CurrentItem) >= 0;
            }
        }

        /// <summary>
        /// Gets a value indicating whether or not we have grouping 
        /// taking place in this collection.
        /// </summary>
        private bool IsGrouping
        {
            get { return _isGrouping; }
        }

        bool IDataGridCollectionView.IsGrouping => IsGrouping;

        /// <summary>
        /// Gets a value indicating whether there
        /// is still an outstanding DeferRefresh in
        /// use.  If at all possible, derived classes
        /// should not call Refresh if IsRefreshDeferred
        /// is true.
        /// </summary>
        private bool IsRefreshDeferred
        {
            get { return _deferLevel > 0; }
        }

        /// <summary>
        /// Gets whether the current page is empty and we need
        /// to move to a previous page.
        /// </summary>
        //TODO Paging
        private bool NeedToMoveToPreviousPage
        {
            get { return (PageSize > 0 && Count == 0 && PageIndex != 0 && PageCount == PageIndex); }
        }

        /// <summary>
        /// Gets a value indicating whether we are on the last local page
        /// </summary>
        //TODO Paging
        private bool OnLastLocalPage
        {
            get
            {
                if (PageSize == 0)
                {
                    return false;
                }

                Debug.Assert(PageCount > 0, "Unexpected PageCount <= 0");

                // if we have no items (PageCount==1) or there is just one page
                if (PageCount == 1)
                {
                    return true;
                }

                return (PageIndex == PageCount - 1);
            }
        }

        /// <summary>
        /// Gets the number of pages we currently have
        /// </summary>
        //TODO Paging
        private int PageCount
        {
            get { return (_pageSize > 0) ? Math.Max(1, (int)Math.Ceiling((double)ItemCount / _pageSize)) : 0; }
        }

        /// <summary>
        /// Gets the root of the Group that we expose to the user
        /// </summary>
        private CollectionViewGroupRoot RootGroup
        {
            get
            {
                return _isUsingTemporaryGroup ? _temporaryGroup : _group;
            }
        }

        /// <summary>
        /// Gets the SourceCollection as an IList
        /// </summary>
        private IList SourceList
        {
            get { return SourceCollection as IList; }
        }

        /// <summary>
        /// Gets Timestamp used by the NewItemAwareEnumerator to determine if a
        /// collection change has occurred since the enumerator began.  (If so,
        /// MoveNext should throw.)
        /// </summary>
        private int Timestamp
        {
            get { return _timestamp; }
        }

        /// <summary>
        /// Gets a value indicating whether a private copy of the data 
        /// is needed for sorting, filtering, and paging. We want any deriving 
        /// classes to also be able to access this value to see whether or not 
        /// to use the default source collection, or the internal list.
        /// </summary>
        //TODO Paging
        private bool UsesLocalArray
        {
            get { return SortDescriptions.Count > 0 || Filter != null || _pageSize > 0 || GroupDescriptions.Count > 0; }
        }

        /// <summary>
        /// Return the item at the specified index
        /// </summary>
        /// <param name="index">Index of the item we want to retrieve</param>
        /// <returns>The item at the specified index</returns>
        public object this[int index]
        {
            get { return GetItemAt(index); }
        }

        bool IList.IsFixedSize => SourceList?.IsFixedSize ?? true;
        bool IList.IsReadOnly => SourceList?.IsReadOnly ?? true;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;

        object IList.this[int index]
        {
            get => this[index];
            set
            {
                SourceList[index] = value;
                if (SourceList is not INotifyCollectionChanged)
                {
                    // TODO: implement Replace
                    ProcessCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset, value));
                }
            }
        }










        /// <summary>
        /// Interface Implementation for GetEnumerator()
        /// </summary>
        /// <returns>IEnumerator that we get from our internal collection</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }






































        /// <summary>
        /// Raises the CurrentChanging event
        /// </summary>
        /// <param name="args">
        ///     CancelEventArgs used by the consumer of the event.  args.Cancel will
        ///     be true after this call if the CurrentItem should not be changed for
        ///     any reason.
        /// </param>
        /// <exception cref="InvalidOperationException">
        ///     This CurrentChanging event cannot be canceled.
        /// </exception>
        private void OnCurrentChanging(DataGridCurrentChangingEventArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (_currentChangedMonitor.Busy)
            {
                if (args.IsCancelable)
                {
                    args.Cancel = true;
                }

                return;
            }

            CurrentChanging?.Invoke(this, args);
        }




        /// <summary>
        /// Helper to raise a PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">Property name for the property that changed</param>
        private void OnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }


















        /// <summary>
        /// Set CurrentItem and CurrentPosition, no questions asked!
        /// </summary>
        /// <remarks>
        /// This method can be called from a constructor - it does not call
        /// any virtuals. The 'count' parameter is substitute for the real Count,
        /// used only when newItem is null.
        /// In that case, this method sets IsCurrentAfterLast to true if and only
        /// if newPosition >= count.  This distinguishes between a null belonging
        /// to the view and the dummy null when CurrentPosition is past the end.
        /// </remarks>
        /// <param name="newItem">New CurrentItem</param>
        /// <param name="newPosition">New CurrentPosition</param>
        /// <param name="count">Numbers of items in the collection</param>
        private void SetCurrent(object newItem, int newPosition, int count)
        {
            if (newItem != null)
            {
                // non-null item implies position is within range.
                // We ignore count - it's just a placeholder
                SetFlag(CollectionViewFlags.IsCurrentBeforeFirst, false);
                SetFlag(CollectionViewFlags.IsCurrentAfterLast, false);
            }
            else if (count == 0)
            {
                // empty collection - by convention both flags are true and position is -1
                SetFlag(CollectionViewFlags.IsCurrentBeforeFirst, true);
                SetFlag(CollectionViewFlags.IsCurrentAfterLast, true);
                newPosition = -1;
            }
            else
            {
                // null item, possibly within range.
                SetFlag(CollectionViewFlags.IsCurrentBeforeFirst, newPosition < 0);
                SetFlag(CollectionViewFlags.IsCurrentAfterLast, newPosition >= count);
            }

            _currentItem = newItem;
            _currentPosition = newPosition;
        }







        int IList.Add(object value)
        {
            var index = SourceList.Add(value);
            if (SourceList is not INotifyCollectionChanged)
            {
                ProcessCollectionChanged(
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value));
            }
            return index;
        }

        void IList.Clear()
        {
            SourceList.Clear();
            if (SourceList is not INotifyCollectionChanged)
            {
                ProcessCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        void IList.Insert(int index, object value) 
        {
            SourceList.Insert(index, value);
            if (SourceList is not INotifyCollectionChanged)
            {
                // TODO: implement Insert
                ProcessCollectionChanged(
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset, value));
            }
        }
        void ICollection.CopyTo(Array array, int index) => InternalList.CopyTo(array, index);

        /// <summary>
        /// Creates a comparer class that takes in a CultureInfo as a parameter,
        /// which it will use when comparing strings.
        /// </summary>
        private class CultureSensitiveComparer : IComparer<object>
        {
            /// <summary>
            /// Private accessor for the CultureInfo of our comparer
            /// </summary>
            private CultureInfo _culture;

            /// <summary>
            /// Creates a comparer which will respect the CultureInfo
            /// that is passed in when comparing strings.
            /// </summary>
            /// <param name="culture">The CultureInfo to use in string comparisons</param>
            public CultureSensitiveComparer(CultureInfo culture)
                : base()
            {
                _culture = culture ?? CultureInfo.InvariantCulture;
            }

            /// <summary>
            /// Compares two objects and returns a value indicating whether one is less than, equal to or greater than the other.
            /// </summary>
            /// <param name="x">first item to compare</param>
            /// <param name="y">second item to compare</param>
            /// <returns>Negative number if x is less than y, zero if equal, and a positive number if x is greater than y</returns>
            /// <remarks>
            /// Compares the 2 items using the specified CultureInfo for string and using the default object comparer for all other objects.
            /// </remarks>
            public int Compare(object x, object y)
            {
                if (x == null)
                {
                    if (y != null)
                    {
                        return -1;
                    }
                    return 0;
                }
                if (y == null)
                {
                    return 1;
                }

                // at this point x and y are not null
                if (x.GetType() == typeof(string) && y.GetType() == typeof(string))
                {
                    return _culture.CompareInfo.Compare((string)x, (string)y);
                }
                else
                {
                    return Comparer<object>.Default.Compare(x, y);
                }
            }
        }

        /// <summary>
        /// Used to keep track of Defer calls on the DataGridCollectionView, which
        /// will prevent the user from calling Refresh() on the view. In order
        /// to allow refreshes again, the user will have to call IDisposable.Dispose,
        /// to end the Defer operation.
        /// </summary>
        private class DeferHelper : IDisposable
        {
            /// <summary>
            /// Private reference to the CollectionView that created this DeferHelper
            /// </summary>
            private DataGridCollectionView collectionView;

            /// <summary>
            /// Initializes a new instance of the DeferHelper class
            /// </summary>
            /// <param name="collectionView">CollectionView that created this DeferHelper</param>
            public DeferHelper(DataGridCollectionView collectionView)
            {
                this.collectionView = collectionView;
            }

            /// <summary>
            /// Cleanup method called when done using this class
            /// </summary>
            public void Dispose()
            {
                if (collectionView != null)
                {
                    collectionView.EndDefer();
                    collectionView = null;
                }
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// A simple monitor class to help prevent re-entrant calls
        /// </summary>
        private class SimpleMonitor : IDisposable
        {
            /// <summary>
            /// Whether the monitor is entered
            /// </summary>
            private bool entered;

            /// <summary>
            /// Gets a value indicating whether we have been entered or not
            /// </summary>
            public bool Busy
            {
                get { return entered; }
            }

            /// <summary>
            /// Sets a value indicating that we have been entered
            /// </summary>
            /// <returns>Boolean value indicating whether we were already entered</returns>
            public bool Enter()
            {
                if (entered)
                {
                    return false;
                }

                entered = true;
                return true;
            }

            /// <summary>
            /// Cleanup method called when done using this class
            /// </summary>
            public void Dispose()
            {
                entered = false;
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// IEnumerator generated using the new item taken into account
        /// </summary>
        private class NewItemAwareEnumerator : IEnumerator
        {
            private enum Position
            {
                /// <summary>
                /// Whether the position is before the new item
                /// </summary>
                BeforeNewItem,

                /// <summary>
                /// Whether the position is on the new item that is being created
                /// </summary>
                OnNewItem,

                /// <summary>
                /// Whether the position is after the new item
                /// </summary>
                AfterNewItem
            }

            /// <summary>
            /// Initializes a new instance of the NewItemAwareEnumerator class.
            /// </summary>
            /// <param name="collectionView">The DataGridCollectionView we are creating the enumerator for</param>
            /// <param name="baseEnumerator">The baseEnumerator that we pass in</param>
            /// <param name="newItem">The new item we are adding to the collection</param>
            public NewItemAwareEnumerator(DataGridCollectionView collectionView, IEnumerator baseEnumerator, object newItem)
            {
                _collectionView = collectionView;
                _timestamp = collectionView.Timestamp;
                _baseEnumerator = baseEnumerator;
                _newItem = newItem;
            }

            /// <summary>
            /// Implements the MoveNext function for IEnumerable
            /// </summary>
            /// <returns>Whether we can move to the next item</returns>
            public bool MoveNext()
            {
                if (_timestamp != _collectionView.Timestamp)
                {
                    throw new InvalidOperationException("Collection was modified; enumeration operation cannot execute.");
                }

                switch (_position)
                {
                    case Position.BeforeNewItem:
                        if (_baseEnumerator.MoveNext() &&
                                    (_newItem == null || _baseEnumerator.Current != _newItem
                                            || _baseEnumerator.MoveNext()))
                        {
                            // advance base, skipping the new item
                        }
                        else if (_newItem != null)
                        {
                            // if base has reached the end, move to new item
                            _position = Position.OnNewItem;
                        }
                        else
                        {
                            return false;
                        }
                        return true;
                }

                // in all other cases, simply advance base, skipping the new item
                _position = Position.AfterNewItem;
                return _baseEnumerator.MoveNext() &&
                    (_newItem == null
                        || _baseEnumerator.Current != _newItem
                        || _baseEnumerator.MoveNext());
            }

            /// <summary>
            /// Gets the Current value for IEnumerable
            /// </summary>
            public object Current
            {
                get
                {
                    return (_position == Position.OnNewItem) ? _newItem : _baseEnumerator.Current;
                }
            }

            /// <summary>
            /// Implements the Reset function for IEnumerable
            /// </summary>
            public void Reset()
            {
                _position = Position.BeforeNewItem;
                _baseEnumerator.Reset();
            }

            /// <summary>
            /// CollectionView that we are creating the enumerator for
            /// </summary>
            private DataGridCollectionView _collectionView;

            /// <summary>
            /// The Base Enumerator that we are passing in
            /// </summary>
            private IEnumerator _baseEnumerator;

            /// <summary>
            /// The position we are appending items to the enumerator
            /// </summary>
            private Position _position;

            /// <summary>
            /// Reference to any new item that we want to add to the collection
            /// </summary>
            private object _newItem;

            /// <summary>
            /// Timestamp to let us know whether there have been updates to the collection
            /// </summary>
            private int _timestamp;
        }

        internal class MergedComparer
        {
            private readonly IComparer<object>[] _comparers;

            public MergedComparer(DataGridSortDescriptionCollection coll)
            {
                _comparers = MakeComparerArray(coll);
            }
            public MergedComparer(DataGridCollectionView collectionView)
                : this(collectionView.SortDescriptions)
            { }

            private static IComparer<object>[] MakeComparerArray(DataGridSortDescriptionCollection coll)
            {
                return 
                    coll.Select(c => c.Comparer)
                        .ToArray();
            }

            /// <summary>
            /// Compares two objects and returns a value indicating whether one is less than, equal to or greater than the other.
            /// </summary>
            /// <param name="x">first item to compare</param>
            /// <param name="y">second item to compare</param>
            /// <returns>Negative number if x is less than y, zero if equal, and a positive number if x is greater than y</returns>
            /// <remarks>
            /// Compares the 2 items using the list of property names and directions.
            /// </remarks>
            public int Compare(object x, object y)
            {
                int result = 0;

                // compare both objects by each of the properties until property values don't match
                for (int k = 0; k < _comparers.Length; ++k)
                {
                    var comparer = _comparers[k];
                    result = comparer.Compare(x, y);

                    if (result != 0)
                    {
                        break;
                    }
                }

                return result;
            }

            /// <summary>
            /// Steps through the given list using the comparer to find where
            /// to insert the specified item to maintain sorted order
            /// </summary>
            /// <param name="x">Item to insert into the list</param>
            /// <param name="list">List where we want to insert the item</param>
            /// <returns>Index where we should insert into</returns>
            public int FindInsertIndex(object x, IList list)
            {
                int min = 0;
                int max = list.Count - 1;
                int index;

                // run a binary search to find the right index
                // to insert into.
                while (min <= max)
                {
                    index = (min + max) / 2;

                    int result = Compare(x, list[index]);
                    if (result == 0)
                    {
                        return index;
                    }
                    else if (result > 0)
                    {
                        min = index + 1;
                    }
                    else
                    {
                        max = index - 1;
                    }
                }

                return min;
            }
        }       
    }
}
