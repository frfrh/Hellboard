// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Platform;

/// <summary>Shift in pixels, e.g. the correction found by the screen detection.</summary>
public readonly record struct PixelOffset(int X, int Y);
