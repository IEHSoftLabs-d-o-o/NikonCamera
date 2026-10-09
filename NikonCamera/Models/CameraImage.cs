using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace NikonCamera.Models;

public partial class CameraImage : ObservableObject
{
    public int? Id { get; set; }
    public string? Index { get; set; }
    public string? Tagebuchnummer { get; set; }
    public byte[]? Bytes { get; set; }
    public ImageSource? ImageSource { get; set; }

    [ObservableProperty] 
    private bool _isSelected;

    [ObservableProperty] 
    private string? _name;
}