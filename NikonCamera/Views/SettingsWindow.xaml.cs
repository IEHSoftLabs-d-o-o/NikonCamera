using Microsoft.Win32;
using NikonCamera.Models;
using NikonCamera.Services;
using System.IO;
using System.Windows;

namespace NikonCamera.Views;

public partial class SettingsWindow
{
    public bool Accepted { get; private set; }
    public List<CameraDefinition>? CameraDefinitions { get; set; }
    public CameraDefinition? CameraDefinition { get; set; }
    public string FotoFolder { get; private set; }
    public string BaseUrl { get; private set; }
    public string Auth { get; private set; }
    public string ArianaLabClient { get; private set; }

    public SettingsWindow(
        List<CameraDefinition>? cameraDefinitions,
        CameraDefinition? cameraDefinition,
        string fotoFolder,
        string baseUrl,
        string auth,
        string arianaLabClient)
    {
        CameraDefinitions = cameraDefinitions;
        CameraDefinition = cameraDefinition;
        FotoFolder = fotoFolder;
        BaseUrl = baseUrl;
        Auth = auth;
        ArianaLabClient = arianaLabClient;
        InitializeComponent();
    }

    private void SettingsWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        ComboBoxCameraModels.ItemsSource = CameraDefinitions;
        ComboBoxCameraModels.SelectedValue = CameraDefinitions?.FirstOrDefault(
            x => x.Manufacturer == CameraDefinition?.Manufacturer
                 && x.Model == CameraDefinition?.Model);
        TextBoxFotoFolder.Text = FotoFolder;
        TextBoxBaseUrl.Text = BaseUrl;
        TextBoxAuth.Text = Auth;
        TextBoxArianaLabClient.Text = ArianaLabClient;
    }

    private void ButtonBrowseFotoFolder_OnClick(object sender, RoutedEventArgs e)
    {
        var currentPath = TextBoxFotoFolder.Text.Trim();
        var dialog = new OpenFolderDialog();

        if (Directory.Exists(currentPath))
            dialog.InitialDirectory = currentPath;

        if (dialog.ShowDialog() == true)
            TextBoxFotoFolder.Text = dialog.FolderName;
    }

    private void ButtonBrowseArianaLabClient_OnClick(object sender, RoutedEventArgs e)
    {
        var currentPath = TextBoxArianaLabClient.Text.Trim();
        var dialog = new OpenFileDialog
        {
            Filter = "Programme (*.exe)|*.exe",
            FileName = Path.GetFileName(currentPath)
        };

        var directory = Path.GetDirectoryName(currentPath);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            dialog.InitialDirectory = directory;

        if (dialog.ShowDialog() == true)
            TextBoxArianaLabClient.Text = dialog.FileName;
    }

    private void ButtonOk_OnClick(object sender, RoutedEventArgs e)
    {
        Accepted = true;
        CameraDefinition = ComboBoxCameraModels.SelectedItem as CameraDefinition;
        var fotoFolder = TextBoxFotoFolder.Text.Trim();
        FotoFolder = string.IsNullOrWhiteSpace(fotoFolder)
            ? AppSettings.DefaultFotoFolder
            : fotoFolder;
        BaseUrl = TextBoxBaseUrl.Text.Trim();
        Auth = TextBoxAuth.Text;
        var arianaLabClient = TextBoxArianaLabClient.Text.Trim();
        ArianaLabClient = string.IsNullOrWhiteSpace(arianaLabClient)
            ? AppSettings.DefaultArianaLabClient
            : arianaLabClient;

        Close();
    }

    private void ButtonCancel_OnClick(object sender, RoutedEventArgs e)
    {
        Accepted = false;

        Close();
    }
}