using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TotalFractal.Infrastructure;

/// <summary>
/// Saves raw framebuffer pixels to a PNG. Input is expected to be tightly packed BGRA,
/// bottom-up rows (exactly what <c>GL.ReadPixels</c> returns when read as
/// <c>PixelFormat.Bgra / PixelType.UnsignedByte</c>). The rows are flipped so the PNG is
/// the right way up.
/// </summary>
public static class ScreenshotWriter
{
    public static void Save(byte[] bgraBottomUp, int width, int height, string path)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, width, height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = width * 4;
            for (int y = 0; y < height; y++)
            {
                // GL row 0 is the bottom of the image; bitmap row 0 is the top -> flip.
                int srcOffset = (height - 1 - y) * rowBytes;
                IntPtr dstRow = data.Scan0 + y * data.Stride;
                Marshal.Copy(bgraBottomUp, srcOffset, dstRow, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        bitmap.Save(path, ImageFormat.Png);
    }
}
