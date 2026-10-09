using NikonCamera.Models;

namespace NikonCamera.Interfaces;

public interface ICameraModel : ICameraModelBase
{
    void Setup(CameraDefinition cameraDefinition);
    void ToggleLiveView();
    void EnableLiveView();
    void DisableLiveView();
    void Capture(double desiredAngle);
    void Cleanup();
}