// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace HellboardCompanion.Platform.Windows.Interop;

/// <summary>
/// Windows display configuration (QueryDisplayConfig / DisplayConfigGetDeviceInfo): the SDR white level of
/// a monitor in HDR mode ("SDR content brightness"), needed to convert the HDR desktop image back to the
/// colors the game shows in SDR.
/// </summary>
internal static unsafe partial class DisplayConfig
{
    private const uint QdcOnlyActivePaths = 0x2;
    private const int GetSourceName = 1;
    private const int GetSdrWhiteLevel = 11;

    /// <summary>
    /// Brightness of SDR white on <paramref name="gdiDeviceName"/> (e.g. <c>\\.\DISPLAY1</c>) in scRGB units
    /// (1.0 = 80 nits); <c>null</c> if Windows does not report it.
    /// </summary>
    public static double? SdrWhiteLevel(string gdiDeviceName)
    {
        if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount) != 0)
        {
            return null;
        }

        var paths = new PathInfo[pathCount];
        var modes = new ModeInfo[modeCount];
        fixed (PathInfo* pathPointer = paths)
        fixed (ModeInfo* modePointer = modes)
        {
            if (QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, pathPointer, ref modeCount, modePointer, 0) != 0)
            {
                return null;
            }
        }

        foreach (var path in paths.Take((int)pathCount))
        {
            var source = new SourceDeviceName
            {
                Header = new DeviceInfoHeader { Type = GetSourceName, Size = (uint)sizeof(SourceDeviceName), AdapterId = path.SourceAdapterId, Id = path.SourceId },
            };
            if (DisplayConfigGetDeviceInfo(&source.Header) != 0
                || !string.Equals(new string(source.ViewGdiDeviceName).TrimEnd('\0'), gdiDeviceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var white = new SdrWhiteLevelInfo
            {
                Header = new DeviceInfoHeader { Type = GetSdrWhiteLevel, Size = (uint)sizeof(SdrWhiteLevelInfo), AdapterId = path.TargetAdapterId, Id = path.TargetId },
            };
            return DisplayConfigGetDeviceInfo(&white.Header) == 0 && white.SdrWhiteLevel > 0 ? white.SdrWhiteLevel / 1000.0 : null;
        }

        return null;
    }

    [LibraryImport("user32.dll")]
    private static partial int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [LibraryImport("user32.dll")]
    private static partial int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, PathInfo* pathArray, ref uint numModeInfoArrayElements, ModeInfo* modeInfoArray, nint currentTopologyId);

    [LibraryImport("user32.dll")]
    private static partial int DisplayConfigGetDeviceInfo(DeviceInfoHeader* requestPacket);

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    /// <summary>DISPLAYCONFIG_PATH_INFO (72 bytes), only the ids are used.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PathInfo
    {
        public Luid SourceAdapterId;
        public uint SourceId;
        public uint SourceModeInfoIdx;
        public uint SourceStatusFlags;
        public Luid TargetAdapterId;
        public uint TargetId;
        public uint TargetModeInfoIdx;
        public int OutputTechnology;
        public int Rotation;
        public int Scaling;
        public uint RefreshNumerator;
        public uint RefreshDenominator;
        public int ScanLineOrdering;
        public int TargetAvailable;
        public uint TargetStatusFlags;
        public uint Flags;
    }

    /// <summary>DISPLAYCONFIG_MODE_INFO (64 bytes), not evaluated.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct ModeInfo
    {
        public int InfoType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfoHeader
    {
        public int Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SourceDeviceName
    {
        public DeviceInfoHeader Header;
        public fixed char ViewGdiDeviceName[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SdrWhiteLevelInfo
    {
        public DeviceInfoHeader Header;
        public uint SdrWhiteLevel;
    }
}
