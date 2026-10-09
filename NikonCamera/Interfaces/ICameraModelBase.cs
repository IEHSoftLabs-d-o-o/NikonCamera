using System.Drawing;
using NikonCamera.Models;

namespace NikonCamera.Interfaces;

public interface ICameraModelBase
{
    double Angle { get; set; }
    int PreviewPercentage { get; set; }
    Image Rotate(Image image, double desiredAngle);
    void SendStatusMessage(string message);
    void SendLiveViewStatusMessage(bool isEnabled);
    void SendLiveViewMessage(Image image);
    void SendCameraAvailableMessage(string name);
    void SendCameraRemovedMessage();
    void SendCameraImageMessage(CameraImage cameraImage);
}