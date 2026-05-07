using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Views
{
    public partial class DevToolsStatusBarView : UserControl
    {
        public static readonly StyledProperty<DevToolsSession?> SessionProperty =
            AvaloniaProperty.Register<DevToolsStatusBarView, DevToolsSession?>(nameof(Session));

        private DevToolsSession? _subscribedSession;

        public DevToolsStatusBarView()
        {
            InitializeComponent();
        }

        public DevToolsSession? Session
        {
            get => GetValue(SessionProperty);
            set => SetValue(SessionProperty, value);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            SubscribeSession(Session);
            UpdateDataContext();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            SubscribeSession(null);
            DataContext = null;
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SessionProperty)
            {
                SubscribeSession(change.GetNewValue<DevToolsSession?>());
                UpdateDataContext();
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
            UpdateDataContext();
        }

        private void UpdateDataContext()
        {
            DataContext = Session?.ViewModel;
        }
    }
}
