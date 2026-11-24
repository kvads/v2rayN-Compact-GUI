namespace v2rayN.Views;

public partial class ConfigurationsWindow
{
    private readonly ConfigurationListViewModel _list;

    public ConfigurationsWindow(MainWindowViewModel main)
    {
        InitializeComponent();
        _list = new ConfigurationListViewModel(main);
        DataContext = _list;
        Closed += (_, _) => _list.Dispose();
    }

    private void AddManually_Click(object sender, RoutedEventArgs e)
    {
        new AddConfigurationWindow(_list.Main) { Owner = this }.ShowDialog();
    }
}
