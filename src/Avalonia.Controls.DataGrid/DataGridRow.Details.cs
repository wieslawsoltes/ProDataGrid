// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Automation.Peers;
using Avalonia.Automation;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using System.Diagnostics;
using System;

namespace Avalonia.Controls
{
    partial class DataGridRow
    {
        private void OnRowDetailsChanged()
        {
            OwningGrid?.OnRowDetailsChanged();
        }

        private void UnloadDetailsTemplate(bool recycle)
        {
            if (_detailsElement != null)
            {
                if (_detailsContent != null)
                {
                    if (_detailsLoaded)
                    {
                        OwningGrid.OnUnloadingRowDetails(this, _detailsContent);
                    }
                    // A recycled row keeps its loaded details bound to their item, so reusing the row
                    // for the same item does not rebuild them. ClearRecyclingState releases them when
                    // the row is reused for another item.
                    _hasRecycledDetailsContent = recycle && _detailsLoaded;
                    if (!_hasRecycledDetailsContent)
                    {
                        _detailsContent.DataContext = null;
                    }
                    ClearDetailsContentSizeSubscription();
                    if (!recycle)
                    {
                        _detailsContent = null;
                    }
                }

                if (!recycle)
                {
                    _detailsElement.Children.Clear();
                }
                _detailsElement.ContentHeight = 0;
            }
            if (!recycle)
            {
                _appliedDetailsTemplate = null;
                SetValueNoCallback(DetailsTemplateProperty, null);
            }

            _detailsLoaded = false;
            _appliedDetailsVisibility = null;
            SetValueNoCallback(AreDetailsVisibleProperty, false);
        }

        internal bool HasLoadedDetails => _detailsLoaded;

        internal bool HasRecycledDetailsContent => _hasRecycledDetailsContent;

        internal void ReleaseRecycledDetailsContent()
        {
            if (_hasRecycledDetailsContent)
            {
                _hasRecycledDetailsContent = false;
                _detailsContent.DataContext = null;
            }
        }

        //TODO Animation
        internal void EnsureDetailsContentHeight()
        {
            if ((_detailsElement != null)
            && (_detailsContent != null)
            && (double.IsNaN(_detailsContent.Height))
            && (AreDetailsVisible)
            && (!double.IsNaN(_detailsDesiredHeight))
            && !MathUtilities.AreClose(_detailsContent.Bounds.Inflate(_detailsContent.Margin).Height, _detailsDesiredHeight)
            && Slot != -1)
            {
                _detailsDesiredHeight = _detailsContent.Bounds.Inflate(_detailsContent.Margin).Height;

                if (true)
                {
                    _detailsElement.ContentHeight = _detailsDesiredHeight;
                }
            }
        }

        // Makes sure the _detailsDesiredHeight is initialized.  We need to measure it to know what
        // height we want to animate to.  Subsequently, we just update that height in response to SizeChanged
        private void EnsureDetailsDesiredHeight()
        {
            Debug.Assert(_detailsElement != null && OwningGrid != null);

            if (_detailsContent != null)
            {
                Debug.Assert(_detailsElement.Children.Contains(_detailsContent));

                _detailsContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                _detailsDesiredHeight = _detailsContent.DesiredSize.Height;
            }
            else
            {
                _detailsDesiredHeight = 0;
            }
        }

        //TODO Animation
        private void DetailsContent_HeightChanged(double newValue)
        {
            if (_previousDetailsHeight.HasValue)
            {
                var oldValue = _previousDetailsHeight.Value;
                _previousDetailsHeight = newValue;
                if (newValue != oldValue && newValue != _detailsDesiredHeight)
                {

                    if (AreDetailsVisible && _appliedDetailsTemplate != null)
                    {
                        // Update the new desired height for RowDetails
                        _detailsDesiredHeight = newValue;

                        _detailsElement.ContentHeight = newValue;

                        // Calling this when details are not visible invalidates during layout when we have no work
                        // to do.  In certain scenarios, this could cause a layout cycle
                        OnRowDetailsChanged();
                    }
                }
            }
            else
            {
                _previousDetailsHeight = newValue;
            }
        }

        private void DetailsContent_SizeChanged(Rect newValue)
        {
            DetailsContent_HeightChanged(newValue.Height);
        }

        private void DetailsContent_MarginChanged(Thickness newValue)
        {
            if (_detailsContent != null)
            DetailsContent_SizeChanged(_detailsContent.Bounds.Inflate(newValue));
        }

        private void DetailsContent_LayoutUpdated(object sender, EventArgs e)
        {
            if (_detailsContent != null)
            {
                var margin = _detailsContent.Margin;
                var height = _detailsContent.DesiredSize.Height + margin.Top + margin.Bottom;

                DetailsContent_HeightChanged(height);
            }
        }

        //TODO Animation
        // Sets AreDetailsVisible on the row and animates if necessary
        internal void SetDetailsVisibilityInternal(bool isVisible, bool raiseNotification, bool animate)
        {
            Debug.Assert(OwningGrid != null);
            Debug.Assert(Index != -1);

            if (_appliedDetailsVisibility != isVisible)
            {
                if (_detailsElement == null)
                {
                    if (raiseNotification)
                    {
                        _detailsVisibilityNotificationPending = true;
                    }
                    return;
                }

                _appliedDetailsVisibility = isVisible;
                SetValueNoCallback(AreDetailsVisibleProperty, isVisible);

                // Applies a new DetailsTemplate only if it has changed either here or at the DataGrid level
                ApplyDetailsTemplate(initializeDetailsPreferredHeight: true);

                // no template to show
                if (_appliedDetailsTemplate == null)
                {
                    if (_detailsElement.ContentHeight > 0)
                    {
                        _detailsElement.ContentHeight = 0;
                    }
                    return;
                }

                if (AreDetailsVisible)
                {
                    // Set the details height directly
                    _detailsElement.ContentHeight = _detailsDesiredHeight;
                    _checkDetailsContentHeight = true;
                }
                else
                {
                    _detailsElement.ContentHeight = 0;
                }

                OnRowDetailsChanged();

                if (raiseNotification)
                {
                    OwningGrid.OnRowDetailsVisibilityChanged(new DataGridRowDetailsEventArgs(this, _detailsContent));
                }
            }
        }

        internal void ApplyDetailsTemplate(bool initializeDetailsPreferredHeight)
        {
            if (_detailsElement != null && AreDetailsVisible)
            {
                IDataTemplate oldDetailsTemplate = _appliedDetailsTemplate;
                if (ActualDetailsTemplate != null && ActualDetailsTemplate != _appliedDetailsTemplate)
                {
                    if (_detailsContent != null)
                    {
                        ClearDetailsContentSizeSubscription();
                        if (_detailsLoaded)
                        {
                            OwningGrid.OnUnloadingRowDetails(this, _detailsContent);
                            _detailsLoaded = false;
                        }
                    }
                    _detailsElement.Children.Clear();

                    _detailsContent = ActualDetailsTemplate.Build(DataContext);
                    _appliedDetailsTemplate = ActualDetailsTemplate;

                    if (_detailsContent != null)
                    {
                        _detailsElement.Children.Add(_detailsContent);
                        EnsureDetailsContentSizeSubscription();
                    }
                }
                else if (_detailsContent != null)
                {
                    EnsureDetailsContentSizeSubscription();
                }

                if (_detailsContent != null && !_detailsLoaded)
                {
                    _detailsLoaded = true;
                    _detailsContent.DataContext = DataContext;
                    OwningGrid.OnLoadingRowDetails(this, _detailsContent);
                }
                if (initializeDetailsPreferredHeight && double.IsNaN(_detailsDesiredHeight) &&
                _appliedDetailsTemplate != null && _detailsElement.Children.Count > 0)
                {
                    EnsureDetailsDesiredHeight();
                }
                else if (oldDetailsTemplate == null)
                {
                    _detailsDesiredHeight = double.NaN;
                    EnsureDetailsDesiredHeight();
                    _detailsElement.ContentHeight = _detailsDesiredHeight;
                }
            }
        }

        private void ClearDetailsContentSizeSubscription()
        {
            _detailsContentSizeSubscription?.Dispose();
            _detailsContentSizeSubscription = null;
        }

        private void EnsureDetailsContentSizeSubscription()
        {
            if (_detailsContent == null || _detailsContentSizeSubscription != null)
            {
                return;
            }

            if (_detailsContent is Layout.Layoutable layoutableContent)
            {
                layoutableContent.LayoutUpdated += DetailsContent_LayoutUpdated;

                _detailsContentSizeSubscription = new CompositeDisposable(2)
                {
                    Disposable.Create(() => layoutableContent.LayoutUpdated -= DetailsContent_LayoutUpdated),
                    _detailsContent.GetObservable(MarginProperty).Subscribe(DetailsContent_MarginChanged)
                };
            }
            else
            {
                _detailsContentSizeSubscription = _detailsContent
                    .GetObservable(MarginProperty)
                    .Subscribe(DetailsContent_MarginChanged);
            }
        }

    }
}
