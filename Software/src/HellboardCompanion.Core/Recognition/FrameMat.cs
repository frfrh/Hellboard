// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Platform;
using OpenCvSharp;

namespace HellboardCompanion.Core.Recognition;

/// <summary>Conversion between <see cref="Frame"/> and OpenCV images.</summary>
public static class FrameMat
{
    /// <summary>Copies <paramref name="area"/> (frame-relative, clipped) as BGR image.</summary>
    public static Mat CropBgr(Frame frame, PixelRect area)
    {
        var clipped = area.Intersect(new PixelRect(0, 0, frame.Width, frame.Height));
        if (clipped.IsEmpty)
        {
            throw new ArgumentOutOfRangeException(nameof(area), area, "The area lies outside the frame.");
        }

        var handle = GCHandle.Alloc(frame.Bgra, GCHandleType.Pinned);
        try
        {
            using var whole = Mat.FromPixelData(frame.Height, frame.Width, MatType.CV_8UC4, handle.AddrOfPinnedObject(), frame.Stride);
            using var region = new Mat(whole, new Rect(clipped.X, clipped.Y, clipped.Width, clipped.Height));
            var bgr = new Mat();
            Cv2.CvtColor(region, bgr, ColorConversionCodes.BGRA2BGR);
            return bgr;
        }
        finally
        {
            handle.Free();
        }
    }
}
