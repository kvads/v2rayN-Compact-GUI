namespace v2rayN.Desktop.Views;

public partial class AddConfigurationWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public AddConfigurationWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        Opened += (_, _) => ConfigurationText.Focus();
    }

    private async void Add_Click(object? sender, RoutedEventArgs e)
    {
        var value = ConfigurationText.Text?.Trim();
        if (value.IsNullOrEmpty())
        {
            ConfigurationText.Focus();
            return;
        }

        await _viewModel.AddServerViaClipboardAsync(value);
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
