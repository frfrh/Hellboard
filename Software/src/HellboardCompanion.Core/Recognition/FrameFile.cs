// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Platform;
using OpenCvSharp;

namespace HellboardCompanion.Core.Recognition;

/// <summary>Reads and writes frames as image files (PNG), e.g. for offline analysis and test data.</summary>
public static class FrameFile
{
    /// <summary>Loads an image; the whole image is treated as the game's client area.</summary>
    public static Frame Load(string path)
    {
        using var image = Cv2.ImRead(path, ImreadModes.Color);
        if (image.Empty())
        {
            throw new InvalidDataException($"'{path}' is not a readable image.");
        }

        using var bgra = new Mat();
        Cv2.CvtColor(image, bgra, ColorConversionCodes.BGR2BGRA);
        var (rows, cols) = (bgra.Rows, bgra.Cols);
        var rowBytes = cols * Frame.BytesPerPixel;
        var pixels = new byte[rows * rowBytes];
        for (var y = 0; y < rows; y++)
        {
            Marshal.Copy(bgra.Ptr(y), pixels, y * rowBytes, rowBytes);
        }

        return new Frame(new PixelRect(0, 0, cols, rows), pixels, File.GetLastWriteTime(path));
    }

    public static void Save(Frame frame, string path)
    {
        using var bgr = FrameMat.CropBgr(frame, new PixelRect(0, 0, frame.Width, frame.Height));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (!Cv2.ImWrite(path, bgr))
        {
            throw new IOException($"Could not write '{path}'.");
        }
    }
}
