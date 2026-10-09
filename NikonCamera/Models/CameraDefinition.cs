namespace NikonCamera.Models;

[Serializable]
public class CameraDefinition
{
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }

    public override string ToString()
    {
        return $"{Manufacturer} {Model}";
    }
}