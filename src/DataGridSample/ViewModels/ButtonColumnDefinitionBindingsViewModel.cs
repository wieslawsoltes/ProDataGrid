using System.Collections.ObjectModel;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(
    typeof(ButtonColumnDefinitionBindingsItem),
    ProviderName = "ButtonColumnDefinitionBindingsSchema")]
[GenerateDataGridView(
    typeof(ButtonColumnDefinitionBindingsItem),
    ViewName = "ButtonColumnDefinitionBindingsPage",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.Explorer,
    Title = "Generated row actions and toggles",
    AutomationId = "generated-row-actions-grid")]
public sealed partial class ButtonColumnDefinitionBindingsViewModel : ReactiveObject
{
    public ButtonColumnDefinitionBindingsViewModel()
    {
        Items = new ObservableCollection<ButtonColumnDefinitionBindingsItem>
        {
            new("Alpha", 0, isFavorite: false, isOnline: true),
            new("Beta", 2, isFavorite: true, isOnline: false),
            new("Gamma", 5, isFavorite: false, isOnline: true)
        };
    }

    public ObservableCollection<ButtonColumnDefinitionBindingsItem> Items { get; }
}

[GenerateDataGridColumns(
    ProviderName = "ButtonColumnDefinitionBindingsSchema",
    Discovery = DataGridColumnDiscovery.AttributedOnly)]
public sealed class ButtonColumnDefinitionBindingsItem : ReactiveObject
{
    private string _actionLabel = "Run";
    private int _clickCount;
    private bool _isFavorite;
    private bool _isOnline;
    private string _lastEvent = "Ready";

    public ButtonColumnDefinitionBindingsItem(
        string name,
        int clickCount,
        bool isFavorite,
        bool isOnline)
    {
        Name = name;
        _clickCount = clickCount;
        _isFavorite = isFavorite;
        _isOnline = isOnline;
        RunActionCommand = ReactiveCommand.Create<string>(RunAction);
        ClearClicksCommand = ReactiveCommand.Create<ButtonColumnDefinitionBindingsItem>(ClearClicks);
        FavoriteChangedCommand = ReactiveCommand.Create<string>(OnFavoriteChanged);
        OnlineChangedCommand = ReactiveCommand.Create<string>(OnOnlineChanged);
    }

    [DataGridColumn(Header = "Name", Order = 0, Width = "1.2*", IsReadOnly = true)]
    public string Name { get; }

    [DataGridColumn(Header = "Clicks", Order = 1, Width = "90", IsReadOnly = true)]
    public int ClickCount
    {
        get => _clickCount;
        private set => this.RaiseAndSetIfChanged(ref _clickCount, value);
    }

    [DataGridColumn(
        Kind = DataGridColumnKind.Button,
        Header = "Row action",
        Order = 2,
        Width = "130",
        ContentMember = nameof(ActionLabel),
        CommandMember = nameof(RunActionCommand),
        CommandParameterMember = nameof(Name))]
    public string Action => Name;

    [DataGridColumn(
        Kind = DataGridColumnKind.Button,
        Header = "Fallback row",
        Order = 3,
        Width = "130",
        Content = "Clear clicks",
        CommandMember = nameof(ClearClicksCommand))]
    public string ClearAction => Name;

    [DataGridColumn(
        Kind = DataGridColumnKind.ToggleButton,
        Header = "Favorite",
        Order = 4,
        Width = "130",
        CheckedContentMember = nameof(FavoriteOnLabel),
        UncheckedContentMember = nameof(FavoriteOffLabel),
        CommandMember = nameof(FavoriteChangedCommand),
        CommandParameterMember = nameof(Name))]
    public bool IsFavorite
    {
        get => _isFavorite;
        set => this.RaiseAndSetIfChanged(ref _isFavorite, value);
    }

    [DataGridColumn(
        Kind = DataGridColumnKind.ToggleSwitch,
        Header = "Presence",
        Order = 5,
        Width = "140",
        OnContentMember = nameof(OnlineLabel),
        OffContentMember = nameof(OfflineLabel),
        CommandMember = nameof(OnlineChangedCommand),
        CommandParameterMember = nameof(Name))]
    public bool IsOnline
    {
        get => _isOnline;
        set => this.RaiseAndSetIfChanged(ref _isOnline, value);
    }

    [DataGridColumn(Header = "Last event", Order = 6, Width = "2*", IsReadOnly = true)]
    public string LastEvent
    {
        get => _lastEvent;
        private set => this.RaiseAndSetIfChanged(ref _lastEvent, value);
    }

    public string ActionLabel
    {
        get => _actionLabel;
        private set => this.RaiseAndSetIfChanged(ref _actionLabel, value);
    }

    public string FavoriteOnLabel => "★ Favorite";

    public string FavoriteOffLabel => "☆ Favorite";

    public string OnlineLabel => "Online";

    public string OfflineLabel => "Offline";

    public ReactiveCommand<string, RxVoid> RunActionCommand { get; }

    public ReactiveCommand<ButtonColumnDefinitionBindingsItem, RxVoid> ClearClicksCommand { get; }

    public ReactiveCommand<string, RxVoid> FavoriteChangedCommand { get; }

    public ReactiveCommand<string, RxVoid> OnlineChangedCommand { get; }

    private void RunAction(string name)
    {
        ClickCount++;
        ActionLabel = ClickCount % 2 == 0 ? "Run" : "Pause";
        LastEvent = $"Action executed for {name} ({ClickCount})";
    }

    private static void ClearClicks(ButtonColumnDefinitionBindingsItem item)
    {
        item.ClickCount = 0;
        item.ActionLabel = "Run";
        item.LastEvent = $"Cleared {item.Name} using the default row parameter";
    }

    private void OnFavoriteChanged(string name) =>
        LastEvent = $"{name} favorite is now {IsFavorite}";

    private void OnOnlineChanged(string name) =>
        LastEvent = $"{name} presence is now {(IsOnline ? "online" : "offline")}";
}
