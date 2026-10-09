using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NikonCamera.Tools.Misc;

public static class Imaging
{
    public static BitmapImage? Convert(Image? img, ImageFormat imageFormat)
    {
        if (img == null) 
            return null;

        using var memory = new MemoryStream();
        img.Save(memory, imageFormat);
        memory.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memory;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
    }

    public static Image? ResizeImage(Image image, int percentage)
    {
        var originalWidth = image.Width;
        var originalHeight = image.Height;

        var newWidth = originalWidth * percentage / 100;
        var newHeight = originalHeight * percentage / 100;

        using var newImage = new Bitmap(newWidth, newHeight);
        using var graphicsHandle = Graphics.FromImage(newImage);
        
        {
            graphicsHandle.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphicsHandle.DrawImage(image, 0, 0, newWidth, newHeight);

            return newImage.Clone() as Image;
        }
    }

    public static byte[]? ImageSourceToBytes(BitmapEncoder encoder, ImageSource imageSource)
    {
        if (imageSource is not BitmapSource bitmapSource) 
            return null;

        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        var bytes = stream.ToArray();

        return bytes;
    }

    public static Image ByteArrayToImage(byte[] bytes)
    {
        var ms = new MemoryStream(bytes);
        var returnImage = Image.FromStream(ms);

        return returnImage;
    }

    public static byte[] ImageToByteArray(Image imageIn)
    {
        var ms = new MemoryStream();
        imageIn.Save(ms, ImageFormat.Jpeg);

        return ms.ToArray();
    }
}