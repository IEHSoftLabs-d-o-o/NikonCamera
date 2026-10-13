using NikonCamera.Interfaces;
using NikonCamera.Tools.Misc;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using Timer = System.Timers.Timer;


namespace NikonCamera.Models;

public class DummyCameraModel : CameraModelBase, ICameraModel
{
    private readonly Timer _liveViewTimer;
    private readonly Lock _backgroundLock = new();

    private bool _isLiveViewEnabled;
    private bool _keepLiveViewBackground;
    private Bitmap? _liveViewBackground;

    public DummyCameraModel()
    {
        _isLiveViewEnabled = false;

        _liveViewTimer = new Timer
        {
            Interval = 500
        };
        _liveViewTimer.Elapsed += (_, _) => _liveViewTimer_Elapsed();

        Task.Factory.StartNew(() =>
        {
            Thread.Sleep(1000);

            SendCameraAvailableMessage("Dummy Camera");
        });
    }

    public void ToggleLiveView()
    {
        try
        {
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
            SendStatusMessage(ex.Message);
        }
    }

    public void EnableLiveView()
    {
        try
        {
            _isLiveViewEnabled = true;

            if (!_keepLiveViewBackground)
            {
                var background = CreateChocolateBarImage();
                lock (_backgroundLock)
                {
                    _liveViewBackground?.Dispose();
                    _liveViewBackground = background;
                }
            }

            _liveViewTimer.Enabled = _isLiveViewEnabled;
            _liveViewTimer.Start();

            SendLiveViewStatusMessage(_isLiveViewEnabled);
        }
        catch (Exception ex)
        {
            SendStatusMessage(ex.Message);
        }
    }

    public void DisableLiveView()
    {
        try
        {
            _isLiveViewEnabled = false;
            _liveViewTimer.Stop();

            _liveViewTimer.Enabled = _isLiveViewEnabled;

            SendLiveViewStatusMessage(_isLiveViewEnabled);
            SendLiveViewMessage(null);
        }
        catch (Exception ex)
        {
            SendStatusMessage(ex.Message);
        }
    }

    public void Capture(double desiredAngle)
    {
        Angle = desiredAngle;

        try
        {
            using var originalImage = CreateTimestampImage();

            using (var memoryStream = new MemoryStream())
            {
                originalImage.Save(memoryStream, ImageFormat.Jpeg);

                var bytes = memoryStream.ToArray();

                SendCameraImageMessage(new CameraImage
                {
                    Bytes = bytes,
                    ImageSource = Imaging.Convert(Imaging.ResizeImage(originalImage, PreviewPercentage), ImageFormat.Jpeg)
                });
            }

            _keepLiveViewBackground = true;
            ToggleLiveView();
        }
        catch (Exception ex)
        {
            SendStatusMessage(ex.Message);
        }
        finally
        {
            ToggleLiveView();
            _keepLiveViewBackground = false;
        }
    }

    public void Cleanup()
    {
        DisableLiveView();

        lock (_backgroundLock)
        {
            _liveViewBackground?.Dispose();
            _liveViewBackground = null;
        }
    }

    public void Setup(CameraDefinition cameraDefinition)
    {
            
    }

    private Bitmap CreateTimestampImage()
    {
        Bitmap frame;

        lock (_backgroundLock)
        {
            _liveViewBackground ??= CreateChocolateBarImage();
            frame = (Bitmap)_liveViewBackground.Clone();
        }

        Rotate(frame, Angle);
        DrawTimestamp(frame);
        return frame;
    }

    private static Bitmap CreateChocolateBarImage()
    {
        const int width = 1600;
        const int height = 1200;

        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            DrawTable(graphics, width, height);

            var columns = Random.Shared.Next(4, 7);
            var rows = Random.Shared.Next(3, 5);
            var barWidth = width * 0.72f;
            var barHeight = height * 0.52f;
            var cocoa = Random.Shared.Next(3) switch
            {
                0 => Color.FromArgb(118, 64, 32),
                1 => Color.FromArgb(74, 38, 22),
                _ => Color.FromArgb(96, 52, 28)
            };

            var state = graphics.Save();
            graphics.TranslateTransform(width / 2f, height / 2f);
            graphics.RotateTransform(Random.Shared.Next(-7, 8));
            DrawChocolateBar(graphics, barWidth, barHeight, columns, rows, cocoa);
            graphics.Restore(state);
        }

        AddGrain(bitmap);
        return bitmap;
    }

    private static void DrawTable(Graphics graphics, int width, int height)
    {
        var bounds = new Rectangle(0, 0, width, height);
        using (var table = new LinearGradientBrush(bounds, Color.FromArgb(92, 68, 48), Color.FromArgb(48, 34, 24), 65f))
        {
            graphics.FillRectangle(table, bounds);
        }

        using var grain = new Pen(Color.FromArgb(28, 120, 82, 48), 3);
        for (var y = 0; y < height; y += Random.Shared.Next(7, 18))
        {
            graphics.DrawLine(grain, 0, y, width, y + Random.Shared.Next(-8, 9));
        }

        using var vignettePath = new GraphicsPath();
        vignettePath.AddEllipse(-width * 0.15f, -height * 0.2f, width * 1.3f, height * 1.4f);
        using (var vignette = new PathGradientBrush(vignettePath))
        {
            vignette.CenterColor = Color.FromArgb(0, 0, 0, 0);
            vignette.SurroundColors = [Color.FromArgb(150, 12, 8, 4)];
            graphics.FillRectangle(vignette, bounds);
        }
    }

    private static void DrawChocolateBar(Graphics graphics, float barWidth, float barHeight, int columns, int rows, Color cocoa)
    {
        using (var shadow = new SolidBrush(Color.FromArgb(70, 20, 10, 4)))
        {
            graphics.FillEllipse(shadow, -barWidth / 2f + 12, barHeight / 2f - 28, barWidth, 70);
        }

        var groove = Color.FromArgb(Math.Max(0, cocoa.R - 38), Math.Max(0, cocoa.G - 24), Math.Max(0, cocoa.B - 16));
        using (var body = RoundedRectangle(-barWidth / 2f, -barHeight / 2f, barWidth, barHeight, 28))
        using (var bodyBrush = new SolidBrush(groove))
        {
            graphics.FillPath(bodyBrush, body);
        }

        const float gap = 14f;
        var cellWidth = (barWidth - gap * (columns + 1)) / columns;
        var cellHeight = (barHeight - gap * (rows + 1)) / rows;

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var x = -barWidth / 2f + gap + column * (cellWidth + gap);
                var y = -barHeight / 2f + gap + row * (cellHeight + gap);
                var shade = Color.FromArgb(
                    ClampChannel(cocoa.R + Random.Shared.Next(-10, 14)),
                    ClampChannel(cocoa.G + Random.Shared.Next(-8, 10)),
                    ClampChannel(cocoa.B + Random.Shared.Next(-6, 8)));

                using var piece = RoundedRectangle(x, y, cellWidth, cellHeight, 16);
                using var pieceBrush = new LinearGradientBrush(
                    new RectangleF(x, y, cellWidth, cellHeight),
                    Lighten(shade, 28),
                    shade,
                    55f);
                graphics.FillPath(pieceBrush, piece);

                using var highlight = new Pen(Color.FromArgb(110, 255, 214, 170), 3);
                graphics.DrawLine(highlight, x + 12, y + 8, x + cellWidth - 16, y + 8);
                using var edge = new Pen(Color.FromArgb(90, 40, 16, 6), 3);
                graphics.DrawLine(edge, x + 12, y + cellHeight - 8, x + cellWidth - 14, y + cellHeight - 8);
            }
        }

        using var gloss = new LinearGradientBrush(
            new RectangleF(-barWidth * 0.32f, -barHeight * 0.46f, barWidth * 0.64f, barHeight * 0.28f),
            Color.FromArgb(75, 255, 236, 214),
            Color.FromArgb(0, 255, 236, 214),
            LinearGradientMode.Vertical);
        graphics.FillEllipse(gloss, -barWidth * 0.32f, -barHeight * 0.46f, barWidth * 0.64f, barHeight * 0.22f);
    }

    private static void DrawTimestamp(Bitmap bitmap)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        using var graphics = Graphics.FromImage(bitmap);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Arial", 108, FontStyle.Bold);
        var size = graphics.MeasureString(timestamp, font);
        var x = (bitmap.Width - size.Width) / 2f;
        var y = bitmap.Height - size.Height - 36f;

        using var outline = new SolidBrush(Color.FromArgb(230, 24, 12, 8));
        for (var dx = -4; dx <= 4; dx += 2)
        {
            for (var dy = -4; dy <= 4; dy += 2)
            {
                if (dx == 0 && dy == 0) continue;
                graphics.DrawString(timestamp, font, outline, x + dx, y + dy);
            }
        }

        using var text = new SolidBrush(Color.White);
        graphics.DrawString(timestamp, font, text, x, y);
    }

    private static GraphicsPath RoundedRectangle(float x, float y, float width, float height, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void AddGrain(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

        try
        {
            var length = Math.Abs(data.Stride) * bitmap.Height;
            var buffer = new byte[length];
            Marshal.Copy(data.Scan0, buffer, 0, length);

            for (var y = 0; y < bitmap.Height; y++)
            {
                var row = y * data.Stride;
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var noise = Random.Shared.Next(-12, 13);
                    var index = row + x * 3;
                    buffer[index] = ClampChannel(buffer[index] + noise);
                    buffer[index + 1] = ClampChannel(buffer[index + 1] + noise);
                    buffer[index + 2] = ClampChannel(buffer[index + 2] + noise);
                }
            }

            Marshal.Copy(buffer, 0, data.Scan0, length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static Color Lighten(Color color, int amount)
    {
        return Color.FromArgb(ClampChannel(color.R + amount), ClampChannel(color.G + amount), ClampChannel(color.B + amount));
    }

    private static byte ClampChannel(int value)
    {
        return (byte)Math.Clamp(value, 0, 255);
    }

    private void _liveViewTimer_Elapsed()
    {
        if (!_isLiveViewEnabled) return;

        try
        {
            using var originalImage = CreateTimestampImage();

            SendLiveViewMessage(originalImage);
        }
        catch (Exception ex)
        {
            _liveViewTimer.Stop();
            SendStatusMessage(ex.Message);
        }
    }
}