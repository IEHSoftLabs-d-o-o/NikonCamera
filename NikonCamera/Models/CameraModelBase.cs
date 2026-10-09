using CommunityToolkit.Mvvm.Messaging;
using System.Drawing;
using NikonCamera.Interfaces;
using NikonCamera.Messages;

namespace NikonCamera.Models;

public class CameraModelBase : ICameraModelBase
{
    public double Angle { get; set; }
    public int PreviewPercentage { get; set; }

    public Image Rotate(Image image, double desiredAngle)
    {
        switch ((int)desiredAngle)
        {
            case 0:
                break;

            case 180:
                image.RotateFlip(RotateFlipType.Rotate180FlipNone);
                break;
        }

        return image;
    }

    public void SendStatusMessage(string message)
    {
        WeakReferenceMessenger.Default.Send(new StatusMessage
        {
            Text = message
        });
    }

    public void SendLiveViewStatusMessage(bool isEnabled)
    {
        WeakReferenceMessenger.Default.Send(new LiveViewStatusMessage
        {
            IsEnabled = isEnabled
        });
    }

    public void SendLiveViewMessage(Image? image)
    {
        WeakReferenceMessenger.Default.Send(new LiveViewMessage
        {
            Image = image
        });
    }

    public void SendCameraAvailableMessage(string name)
    {
        WeakReferenceMessenger.Default.Send(new CameraAvailableMessage
        {
            Name = name
        });
    }

    public void SendCameraRemovedMessage()
    {
        WeakReferenceMessenger.Default.Send(new CameraRemovedMessage());
    }

    public void SendCameraImageMessage(CameraImage cameraImage)
    {
        WeakReferenceMessenger.Default.Send(new CameraImageMessage
        {
            CameraImage = cameraImage
        });
    }
}