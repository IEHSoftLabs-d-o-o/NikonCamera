using NikonCamera.Models;

namespace NikonCamera.Messages;

public class CameraImageMessage
{
    public required CameraImage CameraImage { get; set; }
}