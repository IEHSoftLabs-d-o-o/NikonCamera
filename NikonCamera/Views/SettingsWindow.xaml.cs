using NikonCamera.Models;
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
    public string ArianaLabClientPath { get; private set; }
    public string ArianaLabClientExecutable { get; private set; }

    public SettingsWindow(
        List<CameraDefinition>? cameraDefinitions,
        CameraDefinition? cameraDefinition,
        string fotoFolder,
        string baseUrl,
        string auth,
        string arianaLabClientPath,
        string arianaLabClientExecutable)
    {
        CameraDefinitions = cameraDefinitions;
        CameraDefinition = cameraDefinition;
        FotoFolder = fotoFolder;
        BaseUrl = baseUrl;
        Auth = auth;
        ArianaLabClientPath = arianaLabClientPath;
        ArianaLabClientExecutable = arianaLabClientExecutable;
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
        TextBoxClientPath.Text = ArianaLabClientPath;
        TextBoxClientExecutable.Text = ArianaLabClientExecutable;
    }

    private void ButtonOk_OnClick(object sender, RoutedEventArgs e)
    {
        Accepted = true;
        CameraDefinition = ComboBoxCameraModels.SelectedItem as CameraDefinition;
        FotoFolder = TextBoxFotoFolder.Text.Trim();
        BaseUrl = TextBoxBaseUrl.Text.Trim();
        Auth = TextBoxAuth.Text;
        ArianaLabClientPath = TextBoxClientPath.Text.Trim();
        ArianaLabClientExecutable = TextBoxClientExecutable.Text.Trim();

        Close();
    }

    private void ButtonCancel_OnClick(object sender, RoutedEventArgs e)
    {
        Accepted = false;

        Close();
    }
}