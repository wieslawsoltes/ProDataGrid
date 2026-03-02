using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Diagnostics.Models;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Views
{
    partial class EventsPageView : UserControl
    {
        private readonly ListBox _events;
        private IDisposable? _adorner;
        private ObservableCollection<FiredEvent>? _recordedEvents;
        private Visual? _adornedVisual;
        private MainViewModel? _mainView;

        public EventsPageView()
        {
            InitializeComponent();
            _events = this.GetControl<ListBox>("EventsList");
        }

        public void NavigateTo(object sender, TappedEventArgs e)
        {
            if (DataContext is EventsPageViewModel vm && sender is Control control)
            {
                switch (control.Tag)
                {
                    case EventChainLink chainLink:
                    {
                        vm.RequestTreeNavigateTo(chainLink);
                        break;
                    }
                    case RoutedEvent evt:
                    {
                        vm.SelectEventByType(evt);

                        break;
                    }
                }
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (_recordedEvents != null)
            {
                _recordedEvents.CollectionChanged -= OnRecordedEventsChanged;
                _recordedEvents = null;
            }

            if (_mainView is not null)
            {
                _mainView.PropertyChanged -= OnMainViewPropertyChanged;
                _mainView = null;
            }

            if (DataContext is EventsPageViewModel vm)
            {
                _recordedEvents = vm.RecordedEvents;
                _recordedEvents.CollectionChanged += OnRecordedEventsChanged;
                _mainView = vm.MainView;
                if (_mainView is not null)
                {
                    _mainView.PropertyChanged += OnMainViewPropertyChanged;
                }
            }
            else
            {
                _adornedVisual = null;
                _adorner?.Dispose();
                _adorner = null;
            }
        }

        private void OnRecordedEventsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (DataContext is not EventsPageViewModel vm || !vm.AutoScrollToLatest)
            {
                return;
            }

            if (sender is ObservableCollection<FiredEvent>)
            {
                var evt = vm.RecordedEventsView.Cast<FiredEvent>().LastOrDefault();

                if (evt is null)
                {
                    return;
                }

                Dispatcher.UIThread.Post(() => _events.ScrollIntoView(evt));
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_recordedEvents != null)
            {
                _recordedEvents.CollectionChanged -= OnRecordedEventsChanged;
                _recordedEvents = null;
            }

            _adorner?.Dispose();
            _adorner = null;
            _adornedVisual = null;
            if (_mainView is not null)
            {
                _mainView.PropertyChanged -= OnMainViewPropertyChanged;
                _mainView = null;
            }
            base.OnDetachedFromVisualTree(e);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ListBoxItem_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (DataContext is EventsPageViewModel vm 
                && sender is Control control 
                && control.DataContext is EventChainLink chainLink
                && chainLink.Handler is Visual visual
                && vm.MainView?.HighlightElements != false)
            {
                _adornedVisual = visual;
                _adorner?.Dispose();
                _adorner = Controls.ControlHighlightAdorner.Add(
                    visual,
                    vm.MainView?.OverlayDisplayOptions ?? Controls.OverlayDisplayOptions.Default);
            }
        }

        private void ListBoxItem_PointerExited(object? sender, PointerEventArgs e)
        {
            _adorner?.Dispose();
            _adorner = null;
            _adornedVisual = null;
        }

        private void OnMainViewPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainViewModel.HighlightElements)
                or nameof(MainViewModel.ShouldVisualizeMarginPadding)
                or nameof(MainViewModel.ShowOverlayInfo)
                or nameof(MainViewModel.ShowOverlayRulers)
                or nameof(MainViewModel.ShowOverlayExtensionLines))
            {
                RefreshAdornerFromCurrentVisual();
            }
        }

        private void RefreshAdornerFromCurrentVisual()
        {
            _adorner?.Dispose();
            _adorner = null;

            if (_adornedVisual is null || _mainView is not { HighlightElements: true })
            {
                return;
            }

            _adorner = Controls.ControlHighlightAdorner.Add(_adornedVisual, _mainView.OverlayDisplayOptions);
        }
    }
}
