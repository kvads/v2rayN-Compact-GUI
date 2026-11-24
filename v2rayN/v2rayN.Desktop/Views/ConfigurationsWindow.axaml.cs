namespace v2rayN.Desktop.Views;

public partial class ConfigurationsWindow : Window
{
    private readonly ConfigurationListViewModel _list;

    public ConfigurationsWindow(MainWindowViewModel main)
    {
        InitializeComponent();
        _list = new ConfigurationListViewModel(main);
        DataContext = _list;
        Closed += (_, _) => _list.Dispose();
    }

    private async void AddManually_Click(object? sender, RoutedEventArgs e)
    {
        await new AddConfigurationWindow(_list.Main).ShowDialog(this);
    }
}
