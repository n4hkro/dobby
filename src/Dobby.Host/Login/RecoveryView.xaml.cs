using System.IO;
using System.Windows.Controls;
using Dobby.Core;
using Dobby.Localization;

namespace Dobby.Host.Login;

public partial class RecoveryView : UserControl
{
    private readonly RecoveryViewModel viewModel;
    private readonly HostStringCatalog strings = new();

    public RecoveryView()
        : this(new RecoveryViewModel(
            new WindowsRecoveryFilePicker(),
            new WindowsRecoveryExportFolderOpener(),
            new WindowsRecoveryExportPathClipboard()))
    {
    }

    public RecoveryView(RecoveryViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        ApplyLocalizedText();
    }

    private void Export_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var result = viewModel.ExportRequest(DefaultRecoveryDirectory(), CorrelationId.New());
        if (!result.IsSuccess)
        {
            return;
        }

        ExportPathText.Text = result.Value.FilePath;
        ExportPathText.Visibility = System.Windows.Visibility.Visible;
        OpenFolderButton.Visibility = System.Windows.Visibility.Visible;
        CopyPathButton.Visibility = System.Windows.Visibility.Visible;
    }

    private void Import_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var result = viewModel.ImportResponse(CorrelationId.New());
        if (viewModel.SelectedImportPath is null)
        {
            return;
        }

        ImportPathText.Text = viewModel.SelectedImportPath;
        ImportPathText.Visibility = System.Windows.Visibility.Visible;
        if (!result.IsSuccess)
        {
            ImportPathText.Text = $"{viewModel.SelectedImportPath} ({result.ErrorCode})";
        }
    }

    private void OpenFolder_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (viewModel.LastExportPath is null)
        {
            return;
        }

        viewModel.OpenExportFolder(Path.GetDirectoryName(viewModel.LastExportPath) ?? DefaultRecoveryDirectory());
    }

    private void CopyPath_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (viewModel.LastExportPath is null)
        {
            return;
        }

        viewModel.CopyExportPath(viewModel.LastExportPath);
    }

    private static string DefaultRecoveryDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Dobby", "recovery");
    }

    private void ApplyLocalizedText()
    {
        RecoveryTitleText.Text = strings.GetString(HostResourceKeys.RecoveryTitle);
        ExportRequestButton.Content = strings.GetString(HostResourceKeys.LoginRecoveryExport);
        OpenFolderButton.Content = strings.GetString(HostResourceKeys.RecoveryOpenFolder);
        CopyPathButton.Content = strings.GetString(HostResourceKeys.RecoveryCopyPath);
        ImportResponseButton.Content = strings.GetString(HostResourceKeys.LoginRecoveryImport);
    }
}
