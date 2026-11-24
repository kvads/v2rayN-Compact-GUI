namespace v2rayN.Views;

public partial class AddConfigurationWindow
{
    private readonly MainWindowViewModel _viewModel;

    public AddConfigurationWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        Loaded += (_, _) => ConfigurationText.Focus();
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var value = ConfigurationText.Text?.Trim();
        if (value.IsNullOrEmpty())
        {
            ConfigurationText.Focus();
            return;
        }

        await _viewModel.AddServerViaClipboardAsync(value);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
