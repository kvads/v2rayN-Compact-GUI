namespace ServiceLib.ViewModels;

public sealed class ConfigurationListViewModel : ReactiveObject, IDisposable
{
    private readonly ProfilesViewModel _profiles;
    private string _query = string.Empty;

    public BulkObservableCollection<ProfileItemModel> Cards { get; } = [];
    public bool IsEmpty => Cards.Count == 0;
    public MainWindowViewModel Main { get; }
    public ReactiveCommand<ProfileItemModel, RxVoid> Activate { get; }
    public ReactiveCommand<ProfileItemModel, RxVoid> Edit { get; }
    public ReactiveCommand<ProfileItemModel, RxVoid> TestTcp { get; }
    public ReactiveCommand<ProfileItemModel, RxVoid> TestReal { get; }
    public ReactiveCommand<ProfileItemModel, RxVoid> TestSpeed { get; }

    public string Query
    {
        get => _query;
        set
        {
            _query = value?.Trim() ?? string.Empty;
            RefreshCards();
        }
    }

    public ConfigurationListViewModel(MainWindowViewModel main)
    {
        Main = main;
        _profiles = main.ProfilesViewModel;
        Activate = ReactiveCommand.CreateFromTask<ProfileItemModel>(item => _profiles.SetDefaultServer(item.IndexId));
        Edit = ReactiveCommand.CreateFromTask<ProfileItemModel>(item => _profiles.EditServerAsync(item.IndexId));
        TestTcp = CreateTest(ESpeedActionType.Tcping);
        TestReal = CreateTest(ESpeedActionType.Realping);
        TestSpeed = CreateTest(ESpeedActionType.Speedtest);
        _profiles.ProfileItems.CollectionChanged += ProfilesChanged;
        // This view lists all profiles, including installs that last used a subscription tab.
        if (_profiles.SelectedSub?.Id.IsNotEmpty() == true)
        {
            _profiles.SelectedSub = _profiles.SubItems.FirstOrDefault();
        }
        RefreshCards();
    }

    private ReactiveCommand<ProfileItemModel, RxVoid> CreateTest(ESpeedActionType action) =>
        ReactiveCommand.CreateFromTask<ProfileItemModel>(item => _profiles.TestProfileAsync(item, action));

    private void ProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshCards();

    private void RefreshCards()
    {
        Cards.ReplaceRange(_profiles.ProfileItems.Where(item =>
            _query.Length == 0 || item.DisplayName.Contains(_query, StringComparison.CurrentCultureIgnoreCase)
            || item.Address?.Contains(_query, StringComparison.OrdinalIgnoreCase) == true));
        this.RaisePropertyChanged(nameof(IsEmpty));
    }

    public void Dispose()
    {
        _profiles.ProfileItems.CollectionChanged -= ProfilesChanged;
        Activate.Dispose();
        Edit.Dispose();
        TestTcp.Dispose();
        TestReal.Dispose();
        TestSpeed.Dispose();
    }
}
