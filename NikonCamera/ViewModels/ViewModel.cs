using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Newtonsoft.Json.Linq;
using NikonCamera.Interfaces;
using NikonCamera.Messages;
using NikonCamera.Models;
using NikonCamera.Services;
using NikonCamera.Tools.Misc;
using NikonCamera.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace NikonCamera.ViewModels;

public partial class ViewModel : ObservableObject
{
    #region Fields

    private readonly IDialogProvider _dialogProvider;
    private readonly ICameraModel? _model;
    private readonly AppSettingsStore _settingsStore = new();

    private const string DefaultCameraInfo = "Kamera nicht verfügbar";
    private const string NoSampleText = "Sie haben keine Probe ausgewählt. Bitte scannen Sie einen Probenbarcode.";

    private const string LiveViewSwitchOnText = "Live View Ein";
    private const string LiveViewSwitchOffText = "Live View Aus";

    private const int PreviewPercentage = 100;

    private static readonly Lock LockObject = new();

    private string? _tagebuchnummer;
    private CancellationTokenSource? _barcodeScanCts;
    private bool _suppressBarcodeWatch;
    private string _arianaLabFotoFolder;
    private string _authString;
    private string _arianaLabBaseUrl;
    private string _arianaLabClientPath;
    private string _arianaLabClientExecutable;

    private CameraDefinition? _cameraDefinition;
    private List<CameraDefinition>? _availableCameraDefinitions;

    #endregion

    #region Constructors

    public ViewModel(IDialogProvider dialogProvider)
    {
        _dialogProvider = dialogProvider;

        var settings = _settingsStore.Load();
        _arianaLabFotoFolder = string.IsNullOrWhiteSpace(settings.FotoFolder) ? AppSettings.DefaultFotoFolder : settings.FotoFolder;
        _arianaLabBaseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? AppSettings.DefaultBaseUrl : settings.BaseUrl;
        _authString = string.IsNullOrWhiteSpace(settings.Auth) ? AppSettings.DefaultAuth : settings.Auth;
        _arianaLabClientPath = string.IsNullOrWhiteSpace(settings.ArianaLabClientPath) ? AppSettings.DefaultArianaLabClientPath : settings.ArianaLabClientPath;
        _arianaLabClientExecutable = string.IsNullOrWhiteSpace(settings.ArianaLabClientExecutable) ? AppSettings.DefaultArianaLabClientExecutable : settings.ArianaLabClientExecutable;
        Angle = settings.Angle;

        try
        {
            EnsureArianaLabClientIsRunning();
        }
        catch (Exception ex)
        {
            dialogProvider.ShowExceptionMessage(ex.Message);

            Application.Current.Shutdown();
        }

        IsCameraAvailable = false;

        GetCameras();

        StatusMessage = NoSampleText;
        SampleInfo = "?";
        CameraInfo = DefaultCameraInfo;

        ToggleButtonContent = LiveViewSwitchOnText;
        LiveViewVisibility = Visibility.Collapsed;
        PreviewCameraImage = null;
            
        _cameraDefinition = FindSavedCamera(settings) ?? _availableCameraDefinitions?.First();

        AppTitle = $"NikonCamera - {_cameraDefinition?.Manufacturer} {_cameraDefinition?.Model}";

        if (_cameraDefinition?.Manufacturer == Manufacturer.Nikon)
        {
            _model = new NikonCameraModel
            {
                Angle = Angle,
                PreviewPercentage = PreviewPercentage
            };

            _model.Setup(_cameraDefinition);
        }

        if (_cameraDefinition?.Manufacturer == Manufacturer.Dummy)
        {
            _model = new DummyCameraModel
            {
                Angle = Angle,
                PreviewPercentage = PreviewPercentage
            };
        }

        EnableEvents();

        CameraImages = [];
    }

    private void EnsureArianaLabClientIsRunning()
    {
        var clientProcessName = Path.GetFileNameWithoutExtension(_arianaLabClientExecutable);
        var processes = Process.GetProcessesByName(clientProcessName);

        if (processes.Length != 0) 
            return;

        var fileName = Path.Combine(_arianaLabClientPath, _arianaLabClientExecutable);

        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException($"ArianaLab Client '{fileName}' nicht verfügbar.");
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(_arianaLabClientPath, _arianaLabClientExecutable),
            WorkingDirectory = _arianaLabClientPath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized
        });

        if (process != null)
            _ = MinimizeClientWhenReady(process);
    }

    private static async Task MinimizeClientWhenReady(Process process)
    {
        const int swMinimize = 6;

        try
        {
            for (var attempt = 0; attempt < 25; attempt++)
            {
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(process.MainWindowHandle, swMinimize);
                    await Task.Delay(300);
                    process.Refresh();
                    ShowWindow(process.MainWindowHandle, swMinimize);
                    return;
                }

                await Task.Delay(200);
            }
        }
        catch (InvalidOperationException)
        {
            // The client process has already exited.
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    #endregion

    #region Properties

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleLiveViewCommand))]
    [NotifyCanExecuteChangedFor(nameof(CaptureCommand))]
    [NotifyCanExecuteChangedFor(nameof(RotateCommand))]
    private bool _isCameraAvailable;

    [ObservableProperty] 
    private string? _info;

    [ObservableProperty]
    private double _angle;

    [ObservableProperty]
    private Visibility? _liveViewVisibility;

    [ObservableProperty] 
    private string? _appTitle;

    [ObservableProperty] 
    private string? _cameraInfo;

    [ObservableProperty] 
    private string? _sampleInfo;

    [ObservableProperty] 
    private string? _toggleButtonContent;

    [ObservableProperty] 
    private string? _statusMessage;

    [ObservableProperty] 
    private ImageSource? _previewCameraImage;

    [ObservableProperty]
    private ObservableCollection<CameraImage>? _cameraImages;

    [ObservableProperty] 
    private CameraImage? _selectedCameraImage;

    [ObservableProperty]
    private ObservableCollection<SampleModel>? _samples;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddPictureCommand))]
    [NotifyCanExecuteChangedFor(nameof(CaptureCommand))]
    private SampleModel? _selectedSample;

    [ObservableProperty] 
    private string? _searchText;

    [ObservableProperty]
    private bool? _isBusy;

    #endregion

    #region Methods

    private void EnableEvents()
    {
        WeakReferenceMessenger.Default.Register<CameraAvailableMessage>(this, (_, m) =>
        {
            OnCameraAvailable(m);
        });

        WeakReferenceMessenger.Default.Register<CameraRemovedMessage>(this, (_, m) =>
        {
            OnCameraRemoved(m);
        });

        WeakReferenceMessenger.Default.Register<LiveViewMessage>(this, (_, m) =>
        {
            OnLiveView(m);
        });

        WeakReferenceMessenger.Default.Register<LiveViewStatusMessage>(this, (_, m) =>
        {
            OnLiveViewStatus(m);
        });

        WeakReferenceMessenger.Default.Register<StatusMessage>(this, (_, m) =>
        {
            ShowStatusMessage(m);
        });

        WeakReferenceMessenger.Default.Register<CameraImageMessage>(this, (_, m) =>
        {
            AppendNewImage(m);
        });
    }

    public void DisableEvents()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private void GetCameras()
    {
        _availableCameraDefinitions = [];

        AddCameras(Manufacturer.Nikon, true);

        if (Environment.MachineName.Equals("lbdev01", StringComparison.OrdinalIgnoreCase))
            AddCameras(Manufacturer.Dummy);
    }

    private void AddCameras(string manufacturer, bool isMandatory = false)
    {
        try
        {
            if (manufacturer == "Dummy")
            {
                _availableCameraDefinitions?.Add(new CameraDefinition
                {
                    Manufacturer = manufacturer,
                    Model = "Camera"
                });
            }

            var sdkDirectory = Path.Combine(Directory.GetCurrentDirectory(), "SDK", manufacturer);

            if (!Directory.Exists(sdkDirectory))
            {
                if (isMandatory)
                {
                    _dialogProvider.ShowExclamationMessage($"Verzeichnis '{sdkDirectory}' existiert nicht!");
                }

                return;
            }

            var directories = Directory.GetDirectories(sdkDirectory);

            foreach (var nikonSubDirectory in directories)
            {
                _availableCameraDefinitions?.Add(new CameraDefinition
                {
                    Manufacturer = manufacturer,
                    Model = Path.GetFileName(nikonSubDirectory)
                });
            }
        }
        catch
        {
            // ignored
        }
    }

    private string? GetProbenbezeichnung(string responseString)
    {
        return JObject.Parse(responseString)["Probenbezeichnung"]?.ToString();
    }

    private void LoadPicturesBySampleById(int sampleId)
    {
        try
        {
            Task.Factory.StartNew(() =>
            {
                lock (LockObject)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = true;
                        StatusMessage = string.Empty;
                        PreviewCameraImage = null;
                        CameraImages = [];
                    });

                    if (sampleId == 0)
                    {
                        var cleanedTagebuchnummer = _tagebuchnummer?.Replace("/", "~");

                        var requestUri = $"{_arianaLabBaseUrl}/Rest/Opd/Proben/{cleanedTagebuchnummer}";
                        var client = new HttpClient();
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(_authString)));
                        var task = Task.Run(() => client.GetAsync(requestUri));
                        task.Wait();

                        if (task.Result.StatusCode != HttpStatusCode.OK)
                        {
                            _dialogProvider.ShowExceptionMessage($"ArianaLab Bilder laden gescheitert: Status Code: {task.Result.StatusCode}");

                            return;
                        }

                        var receiveStream = task.Result.Content.ReadAsStreamAsync();
                        var readStream = new StreamReader(receiveStream.Result, Encoding.UTF8);
                        var response = readStream.ReadToEnd();

                        var anlagen = GetAnlagen(response);
                        var probenBezeichnung = GetProbenbezeichnung(response);

                        if (Samples?.Count == 1)
                        {
                            Samples[0].Description = probenBezeichnung;

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                SampleInfo = $"{SelectedSample?.Number} - {probenBezeichnung}";
                            });
                        }

                        foreach (var anlage in anlagen)
                        {
                            if(anlage?.Dateipfad == null || !anlage.Dateipfad.EndsWith(".jpg")) continue;

                            try
                            {
                                var bytes = File.ReadAllBytes(anlage.Dateipfad.Replace("E$", "E"));

                                var image = Imaging.ByteArrayToImage(bytes);

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    CameraImages?.Add(new CameraImage
                                    {
                                        Tagebuchnummer = anlage.Tagebuchnummer,
                                        Index = anlage.Index,
                                        ImageSource = Imaging.Convert(Imaging.ResizeImage(image, PreviewPercentage), ImageFormat.Jpeg),
                                        Name = anlage.Name,
                                        IsSelected = anlage.Drucken
                                    });
                                });
                            }
                            catch (Exception ex)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    IsBusy = false;
                                });

                                _dialogProvider.ShowExceptionMessage(ex.Message);

                                return;
                            }
                        }
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;

                        if (LiveViewVisibility != Visibility.Collapsed) 
                            return;

                        _model?.DisableLiveView();
                        SelectedCameraImage = CameraImages?.FirstOrDefault();
                        ImageMouseUpAsync();
                    });
                }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            IsBusy = false;
        }
    }

    private IList<HmAnlage?> GetAnlagen(string responseString)
    {
        var anlagen = new List<HmAnlage?>();

        try
        {
            var content = JObject.Parse(responseString);
            var tokens = content["Anlagen"]?.Children().ToList();

            if (tokens == null) 
                return new List<HmAnlage?>();

            anlagen.AddRange(tokens.Select(token => token.ToObject<HmAnlage>()));
        }
        catch
        {
            // ignored
        }

        return anlagen;
    }

    private bool CanExecuteDeviceCommand()
    {
        return IsCameraAvailable;
    }

    public bool CanCapture => IsCameraAvailable && SelectedSample != null;

    partial void OnIsCameraAvailableChanged(bool value) => OnPropertyChanged(nameof(CanCapture));

    partial void OnSelectedSampleChanged(SampleModel? value) => OnPropertyChanged(nameof(CanCapture));

    private bool CanAddPicture()
    {
        return SelectedSample != null;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteDeviceCommand))]
    private Task ToggleLiveViewAsync()
    {
        _model?.ToggleLiveView();

        PreviewCameraImage = null;

        StatusMessage = null;

        return Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanCapture))]
    private Task Capture()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatusMessage = null;
        });

        _model?.Capture(Angle);

        return Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteDeviceCommand))]
    private Task RotateAsync()
    {
        switch ((int)Angle)
        {
            case 180:
                Angle = 0;
                break;

            default:
                Angle += 180;
                break;                    
        }

        _model?.Angle = Angle;

        return Task.CompletedTask;
    }

    public void OnClosing()
    {
        _model?.Cleanup();

        SaveSettings();
    }

    [RelayCommand]
    private Task ImageMouseUpAsync()
    {
        if (SelectedCameraImage == null) 
            return Task.CompletedTask;

        PreviewCameraImage = null;

        StatusMessage = null;

        _model?.DisableLiveView();

        PreviewCameraImage = SelectedCameraImage.ImageSource;

        LiveViewVisibility = Visibility.Collapsed;

        StatusMessage = SelectedCameraImage.Name;

        return Task.CompletedTask;
    }

    private CameraDefinition? FindSavedCamera(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Manufacturer) || string.IsNullOrWhiteSpace(settings.Model))
            return null;

        return _availableCameraDefinitions?.FirstOrDefault(x =>
            x.Manufacturer == settings.Manufacturer && x.Model == settings.Model);
    }

    private void SaveSettings()
    {
        var settings = _settingsStore.Load();
        settings.Angle = Angle;
        settings.Manufacturer = _cameraDefinition?.Manufacturer;
        settings.Model = _cameraDefinition?.Model;
        _settingsStore.Save(settings);
    }

    private void OnCameraAvailable(CameraAvailableMessage cameraAvailableMessage)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsCameraAvailable = true;
            CameraInfo = $"{cameraAvailableMessage.Name}";
        });
    }

    private void OnCameraRemoved(CameraRemovedMessage cameraRemovedMessage)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsCameraAvailable = false;
            CameraInfo = DefaultCameraInfo;
        });
    }

    public void OnLiveView(LiveViewMessage liveViewMessage)
    {
        if(Application.Current == null) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            PreviewCameraImage = Imaging.Convert(liveViewMessage.Image, ImageFormat.Jpeg);
        });            
    }

    public void OnLiveViewStatus(LiveViewStatusMessage liveViewStatusMessage)
    {
        LiveViewVisibility = liveViewStatusMessage.IsEnabled ? Visibility.Visible : Visibility.Collapsed;

        ToggleButtonContent = liveViewStatusMessage.IsEnabled ? LiveViewSwitchOffText : LiveViewSwitchOnText;
    }

    public void ShowStatusMessage(StatusMessage message)
    {
        StatusMessage = message.Text;
    }

    public void AppendNewImage(CameraImageMessage cameraImageMessage)
    {
        if (SelectedSample == null)
        {
            StatusMessage = NoSampleText;

            return;
        }

        Application.Current.Dispatcher.Invoke(async () =>
        {
            var newCount = CameraImages?.Count + 1;

            cameraImageMessage.CameraImage.Name = $"{SelectedSample.Number}_{newCount:00}";

            CameraImages?.Add(cameraImageMessage.CameraImage);

            SelectedCameraImage = CameraImages?.Last();

            await UploadPictureToArianaLab(cameraImageMessage);
        });
    }

    private async Task UploadPictureToArianaLab(CameraImageMessage cameraImageMessage)
    {
        try
        {
            if (cameraImageMessage.CameraImage.Bytes == null)
            {
                return;
            }

            if (!Directory.Exists(_arianaLabFotoFolder))
            {
                Directory.CreateDirectory(_arianaLabFotoFolder);
            }
            else
            {
                var filenames = Directory.GetFiles(_arianaLabFotoFolder);

                foreach (var filename in filenames)
                {
                    File.Delete(filename);
                }

                await File.WriteAllBytesAsync(Path.Combine(_arianaLabFotoFolder, "temp.jpg"), cameraImageMessage.CameraImage.Bytes);

                var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(_authString)));
                var response = await client.PostAsync(new Uri($"{_arianaLabBaseUrl}/Rest/Opd/Attachments/Proben/{_tagebuchnummer?.Replace("/", "~")}/AttachClientPhotos"), new StringContent(""));

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"ArianaLab Upload gescheitert: Status Code: {response.StatusCode}");
                }
            }
        }
        catch (Exception ex)
        {
            _dialogProvider.ShowExceptionMessage(ex.Message);
        }
    }

    [RelayCommand]
    private Task DeletePictureAsync()
    {
        if (SelectedCameraImage == null
            || !_dialogProvider.ShowQuestionMessage(
                $"Wollen Sie das Bild '{SelectedCameraImage.Name}' wirlich löschen?"))
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(SelectedCameraImage.Index))
        {
            _dialogProvider.ShowExclamationMessage(
                "Das Bild kann nicht gelöscht werden, da der Index unbekannt ist.\nBitte Bilder zuerst neu laden!");
            return Task.CompletedTask;
        }

        // e.g. HTTPDELETE https://arianalab.drberns.de/Rest/Opd/Attachments/Proben/B21-060220/74f3cf19-0922-4ed2-8f26-00a50d6aea3c

        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes(_authString)));

        var task = Task.Run(() =>
            client.DeleteAsync(new Uri(
                $"{_arianaLabBaseUrl}/Rest/Opd/Attachments/Proben/{SelectedCameraImage.Tagebuchnummer}/{SelectedCameraImage.Index}")));

        task.Wait();

        if (task.Result.StatusCode != HttpStatusCode.OK)
            _dialogProvider.ShowExceptionMessage(
                $"ArianaLab Bild löschen gescheitert: Status Code: {task.Result.StatusCode}");
        else
            CameraImages?.Remove(SelectedCameraImage);

        return Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanAddPicture))]
    private Task AddPictureAsync()
    {
        var filenames = _dialogProvider.ShowOpenMultipleFileDialog("jpg", "Bilder");

        if (filenames == null) 
            return Task.CompletedTask;

        try
        {
            foreach (var filename in filenames)
            {
                if (!File.Exists(filename))
                {
                    _dialogProvider.ShowExceptionMessage($"{filename} konnte nicht gefunden werden.");

                    return Task.CompletedTask;
                }

                var image = System.Drawing.Image.FromFile(filename);

                WeakReferenceMessenger.Default.Send(new CameraImageMessage
                {
                    CameraImage = new CameraImage
                    {
                        Bytes = Imaging.ImageToByteArray(image),
                        ImageSource = Imaging.Convert(Imaging.ResizeImage(image, PreviewPercentage),
                            ImageFormat.Jpeg),
                        IsSelected = true
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _dialogProvider.ShowExceptionMessage(ex.Message);
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task SelectedSampleChangedAsync()
    {
        if (SelectedSample == null) 
            return Task.CompletedTask;

        Application.Current.Dispatcher.Invoke(() =>
        {
            SampleInfo = $"{SelectedSample.Number} - {SelectedSample.Description}";
        });

        LoadPicturesBySampleById(SelectedSample.Id);

        return Task.CompletedTask;
    }

    partial void OnSearchTextChanged(string? value)
    {
        _barcodeScanCts?.Cancel();

        if (_suppressBarcodeWatch || string.IsNullOrWhiteSpace(value) || !IsScannedOrderBarcode(value))
            return;

        var scan = value.Trim();
        var cts = new CancellationTokenSource();
        _barcodeScanCts = cts;
        _ = CompleteBarcodeScanAsync(scan, cts.Token);
    }

    private async Task CompleteBarcodeScanAsync(string scan, CancellationToken token)
    {
        try
        {
            await Task.Delay(250, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (Application.Current == null)
            return;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (token.IsCancellationRequested)
                return;

            if (!string.Equals(SearchText?.Trim(), scan, StringComparison.OrdinalIgnoreCase))
                return;

            LoadSamplesCommand.Execute(null);
        });
    }

    private static bool IsScannedOrderBarcode(string value)
    {
        var scan = value.Trim();
        return scan.Length is 10 or 13 && Barcode.IsArianaLabBarcode(scan);
    }

    [RelayCommand]
    private Task LoadSamplesAsync()
    {
        _barcodeScanCts?.Cancel();
        _tagebuchnummer = string.Empty;

        if(string.IsNullOrWhiteSpace(SearchText)) 
            return Task.CompletedTask;

        var orderNumber = SearchText.Trim();

        if (!orderNumber.ToUpper().StartsWith("B")) 
            return Task.CompletedTask;

        var chunks = orderNumber.Split('-');

        _tagebuchnummer = string.Join("-", chunks.Take(2));

        _suppressBarcodeWatch = true;
        SearchText = _tagebuchnummer;
        _suppressBarcodeWatch = false;

        GetSamplesByTagebuchnummer(_tagebuchnummer);

        return Task.CompletedTask;
    }

    private void GetSamplesByTagebuchnummer(string tagebuchNumber)
    {
        const int length = 10;

        if (tagebuchNumber.Length < length)
        {
            _dialogProvider.ShowExclamationMessage($"{tagebuchNumber} ist zu kurz!");

            return;
        }

        _tagebuchnummer = tagebuchNumber;

        Samples =
        [
            new SampleModel
            {
                Description = "ArianaLab Probe",
                Id = 0,
                Number = _tagebuchnummer
            }
        ];

        SelectedSample = Samples.FirstOrDefault();
    }

    [RelayCommand]
    private Task SettingsAsync()
    {
        var w = new SettingsWindow(
            _availableCameraDefinitions,
            _cameraDefinition,
            _arianaLabFotoFolder,
            _arianaLabBaseUrl,
            _authString,
            _arianaLabClientPath,
            _arianaLabClientExecutable);

        w.ShowDialog();

        if (!w.Accepted)
            return Task.CompletedTask;

        _arianaLabFotoFolder = string.IsNullOrWhiteSpace(w.FotoFolder) ? AppSettings.DefaultFotoFolder : w.FotoFolder;
        _arianaLabBaseUrl = string.IsNullOrWhiteSpace(w.BaseUrl) ? AppSettings.DefaultBaseUrl : w.BaseUrl;
        _authString = string.IsNullOrWhiteSpace(w.Auth) ? AppSettings.DefaultAuth : w.Auth;
        _arianaLabClientPath = string.IsNullOrWhiteSpace(w.ArianaLabClientPath) ? AppSettings.DefaultArianaLabClientPath : w.ArianaLabClientPath;
        _arianaLabClientExecutable = string.IsNullOrWhiteSpace(w.ArianaLabClientExecutable) ? AppSettings.DefaultArianaLabClientExecutable : w.ArianaLabClientExecutable;

        var settings = _settingsStore.Load();
        settings.FotoFolder = _arianaLabFotoFolder;
        settings.BaseUrl = _arianaLabBaseUrl;
        settings.Auth = _authString;
        settings.ArianaLabClientPath = _arianaLabClientPath;
        settings.ArianaLabClientExecutable = _arianaLabClientExecutable;
        _settingsStore.Save(settings);

        if (w.CameraDefinition == null
            || (w.CameraDefinition.Manufacturer == _cameraDefinition?.Manufacturer
                && w.CameraDefinition.Model == _cameraDefinition?.Model))
            return Task.CompletedTask;

        _cameraDefinition = w.CameraDefinition;

        _dialogProvider.ShowInformationMessage("Für einen Wechsel der Kamera muss das Kameramodul neu gestartet werden.");

        WeakReferenceMessenger.Default.Send(new RestartMessage());

        return Task.CompletedTask;
    }

    #endregion
}