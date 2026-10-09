using NikonCamera.Interfaces;
using NikonCamera.Tools.Misc;
using NikonCsWrapper;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Timers;
using Timer = System.Timers.Timer;

namespace NikonCamera.Models;

public class NikonCameraModel : CameraModelBase, ICameraModel
{
    private NikonManager? _manager;
    private NikonDevice? _nd;

    private readonly Timer _liveViewTimer;

    private bool _isLiveViewEnabled;
    private bool _restoreLiveViewAfterCapture;

    public NikonCameraModel()
    {
        _isLiveViewEnabled = false;

        _liveViewTimer = new Timer
        {
            Interval = 100
        };
        _liveViewTimer.Elapsed += (_, _) =>
        _liveViewTimer.Enabled = false;
    }

    public void Setup(CameraDefinition cameraDefinition)
    {
        var processType = Environment.Is64BitProcess ? "x64" : "x86";

        var md3Directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $@"SDK\{cameraDefinition.Manufacturer}\{cameraDefinition.Model}\{processType}");

        if (!Directory.Exists(md3Directory))
        {
            return;
        }

        var md3File = Directory.GetFiles(md3Directory, "*.md3")
            .FirstOrDefault();

        if (md3File == null)
        {
            return;
        }

        _manager = new NikonManager(md3File);

        _manager.DeviceAdded += ManagerDeviceAdded;
        _manager.DeviceRemoved += ManagerDeviceRemoved;
    }

    private void ManagerDeviceAdded(NikonManager sender, NikonDevice device)
    {
        try
        {
            _nd = device;
            _nd.LiveViewEnabled = false;

            ConfigureRamOnlyCapture();

            _nd.ImageReady += ImageReady;
            _nd.CaptureComplete += CaptureComplete;

            SendCameraAvailableMessage($"NIKON {device.Name}");
        }
        catch (Exception ex)
        {
            SendStatusMessage(ex.Message);
        }
    }

    private void ConfigureRamOnlyCapture()
    {
        if (_nd == null) return;

        _nd.SetUnsigned(
            eNkMAIDCapability.kNkMAIDCapability_SaveMedia,
            (uint)eNkMAIDSaveMedia.kNkMAIDSaveMedia_SDRAM);

        try
        {
            _nd.SetBoolean(eNkMAIDCapability.kNkMAIDCapability_ShootNoCard, true);
        }
        catch
        {
            try
            {
                _nd.SetUnsigned(eNkMAIDCapability.kNkMAIDCapability_ShootNoCard, 1);
            }
            catch
            {
                // Not all MD3 modules expose ShootNoCard.
            }
        }
    }

    private void ImageReady(NikonDevice sender, NikonImage nikonImage)
    {
        var originalImage = Imaging.ByteArrayToImage(nikonImage.Buffer);

        var rotatedImage = Rotate(originalImage, Angle);

        var rotatedBytes = Imaging.ImageToByteArray(rotatedImage);

        SendStatusMessage("OK");

        SendCameraImageMessage(new CameraImage
        {
            Bytes = rotatedBytes,
            ImageSource = Imaging.Convert(Imaging.ResizeImage(rotatedImage, PreviewPercentage), ImageFormat.Jpeg),
            IsSelected = false
        });
    }

    private void CaptureComplete(NikonDevice sender, int data)
    {
        if (_restoreLiveViewAfterCapture)
        {
            EnableLiveView();
        }
    }

    private void ManagerDeviceRemoved(NikonManager sender, NikonDevice device)
    {
        _nd?.ImageReady -= ImageReady;
        _nd?.CaptureComplete -= CaptureComplete;
        _nd = null;

        SendCameraRemovedMessage();
        SendLiveViewStatusMessage(false);
    }

    public void ToggleLiveView()
    {
        try
        {
            if (_nd == null) 
                return;

            if (_isLiveViewEnabled)
            {
                DisableLiveView();
            }
            else
            {
                EnableLiveView();
            }
        }
        catch (Exception ex)
        {
            if (ex is NikonException { ErrorCode: eNkMAIDResult.kNkMAIDResult_DeviceBusy }) return;

            SendStatusMessage(ex.Message);
        }
    }

    public void EnableLiveView()
    {
        if (_nd == null) return;

        try
        {
            _nd.LiveViewEnabled = true;

            _isLiveViewEnabled = true;
            _liveViewTimer.Start();

            _liveViewTimer.Enabled = _isLiveViewEnabled;

            SendLiveViewStatusMessage(true);
        }
        catch (Exception ex)
        {
            if (ex is NikonException { ErrorCode: eNkMAIDResult.kNkMAIDResult_DeviceBusy }) return;

            SendStatusMessage(ex.Message);
        }
    }

    public void DisableLiveView()
    {
        if (_nd == null) return;

        try
        {
            _nd.LiveViewEnabled = false;
                
            _isLiveViewEnabled = false;
            _liveViewTimer.Stop();

            _liveViewTimer.Enabled = _isLiveViewEnabled;

            SendLiveViewStatusMessage(false);
            SendLiveViewMessage(null);
        }
        catch (Exception ex)
        {
            if (ex is NikonException { ErrorCode: eNkMAIDResult.kNkMAIDResult_DeviceBusy }) return;

            SendStatusMessage(ex.Message);
        }
    }

    public void Capture(double desiredAngle)
    {
        Angle = desiredAngle;

        if(_nd == null) return;

        try
        {
            _restoreLiveViewAfterCapture = _isLiveViewEnabled;

            DisableLiveView();
            ConfigureRamOnlyCapture();

            _nd.Capture();
        }
        catch (Exception ex)
        {
            if (ex is NikonException { ErrorCode: eNkMAIDResult.kNkMAIDResult_DeviceBusy }) return;

            SendStatusMessage(ex.Message);
        }
    }

    public void Cleanup()
    {
        DisableLiveView();

        if (_nd != null)
        {
            _nd.LiveViewEnabled = false;
        }

        if (_manager != null)
        {
            _manager.DeviceAdded -= ManagerDeviceAdded;
            _manager.DeviceRemoved -= ManagerDeviceRemoved;

            _manager.Shutdown();
            _manager = null;
        }

        _nd = null;
    }

    private void LiveViewTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        NikonLiveViewImage? liveViewImage = null;

        try
        {
            liveViewImage = _nd?.GetLiveViewImage();
        }
        catch
        {
            _liveViewTimer.Stop();
        }

        if (liveViewImage == null) return;

        using var ms = new MemoryStream(liveViewImage.JpegBuffer);

        var image = Image.FromStream(ms);

        var rotatedImage = Rotate(image, Angle);

        SendLiveViewMessage(rotatedImage);
    }
}