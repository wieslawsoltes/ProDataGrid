using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Diagnostics.Views;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics
{
    /// <summary>
    /// Hosts one standalone segment of a logical, visual, or combined diagnostics tree page.
    /// Multiple segment views can share one <see cref="DevToolsSession"/> so tree selection
    /// drives the properties and layout/style analyzer panels.
    /// </summary>
    public partial class DevToolsSegmentView : UserControl
    {
        public static readonly StyledProperty<DevToolsSession?> SessionProperty =
            AvaloniaProperty.Register<DevToolsSegmentView, DevToolsSession?>(nameof(Session));

        public static readonly StyledProperty<DevToolsViewKind> ViewKindProperty =
            AvaloniaProperty.Register<DevToolsSegmentView, DevToolsViewKind>(nameof(ViewKind), DevToolsViewKind.CombinedTree);

        public static readonly StyledProperty<DevToolsTreeSegmentKind> SegmentKindProperty =
            AvaloniaProperty.Register<DevToolsSegmentView, DevToolsTreeSegmentKind>(nameof(SegmentKind), DevToolsTreeSegmentKind.Tree);

        private readonly ContentControl _content;
        private readonly TextBlock _emptyView;
        private DevToolsSession? _subscribedSession;
        private TreePageViewModel? _treeViewModel;
        private bool _isAttached;

        public DevToolsSegmentView()
        {
            InitializeComponent();

            _content = this.GetControl<ContentControl>("PART_Content");
            _emptyView = new TextBlock
            {
                Text = "Run a sample to inspect diagnostics.",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Foreground = Avalonia.Media.Brushes.Gray
            };

            _content.Content = _emptyView;
        }

        public DevToolsSession? Session
        {
            get => GetValue(SessionProperty);
            set => SetValue(SessionProperty, value);
        }

        public DevToolsViewKind ViewKind
        {
            get => GetValue(ViewKindProperty);
            set => SetValue(ViewKindProperty, value);
        }

        public DevToolsTreeSegmentKind SegmentKind
        {
            get => GetValue(SegmentKindProperty);
            set => SetValue(SegmentKindProperty, value);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _isAttached = true;
            SubscribeSession(Session);
            Session?.AttachInput();
            UpdateContent();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isAttached = false;
            Session?.DetachInput();
            SubscribeSession(null);
            SetTreeViewModel(null);
            _content.Content = null;
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SessionProperty)
            {
                if (_isAttached)
                {
                    change.GetOldValue<DevToolsSession?>()?.DetachInput();
                    change.GetNewValue<DevToolsSession?>()?.AttachInput();
                }

                SubscribeSession(change.GetNewValue<DevToolsSession?>());
                UpdateContent();
            }
            else if (change.Property == ViewKindProperty ||
                     change.Property == SegmentKindProperty)
            {
                UpdateContent();
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SubscribeSession(DevToolsSession? session)
        {
            if (ReferenceEquals(_subscribedSession, session))
            {
                return;
            }

            if (_subscribedSession is not null)
            {
                _subscribedSession.ViewModelChanged -= OnSessionViewModelChanged;
            }

            _subscribedSession = session;

            if (_subscribedSession is not null)
            {
                _subscribedSession.ViewModelChanged += OnSessionViewModelChanged;
            }
        }

        private void OnSessionViewModelChanged(object? sender, System.EventArgs e)
        {
            UpdateContent();
        }

        private void UpdateContent()
        {
            if (Session?.ViewModel is not { } viewModel)
            {
                SetTreeViewModel(null);
                _content.Content = _emptyView;
                return;
            }

            if (viewModel.GetContent(ViewKind) is not TreePageViewModel treeViewModel)
            {
                SetTreeViewModel(null);
                _content.Content = _emptyView;
                return;
            }

            SetTreeViewModel(treeViewModel);

            switch (SegmentKind)
            {
                case DevToolsTreeSegmentKind.Tree:
                    _content.Content = new TreePageTreeView { DataContext = treeViewModel };
                    break;

                case DevToolsTreeSegmentKind.Properties:
                    UpdateDetailsContent(static details => new ControlPropertiesView { DataContext = details });
                    break;

                case DevToolsTreeSegmentKind.LayoutStyles:
                    UpdateDetailsContent(static details => new ControlLayoutStylesView { DataContext = details });
                    break;
            }
        }

        private void UpdateDetailsContent(System.Func<ControlDetailsViewModel, Control> createView)
        {
            if (_treeViewModel?.Details is { } details)
            {
                _content.Content = createView(details);
            }
            else
            {
                _content.Content = new TextBlock
                {
                    Text = "Select a tree item to inspect details.",
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Foreground = Avalonia.Media.Brushes.Gray
                };
            }
        }

        private void SetTreeViewModel(TreePageViewModel? treeViewModel)
        {
            if (ReferenceEquals(_treeViewModel, treeViewModel))
            {
                return;
            }

            if (_treeViewModel is not null)
            {
                _treeViewModel.PropertyChanged -= OnTreeViewModelPropertyChanged;
            }

            _treeViewModel = treeViewModel;

            if (_treeViewModel is not null)
            {
                _treeViewModel.PropertyChanged += OnTreeViewModelPropertyChanged;
            }
        }

        private void OnTreeViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TreePageViewModel.Details)
                && SegmentKind != DevToolsTreeSegmentKind.Tree)
            {
                UpdateContent();
            }
        }
    }
}
