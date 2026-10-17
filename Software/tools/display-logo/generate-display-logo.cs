// SPDX-License-Identifier: GPL-3.0-or-later
//
// Generates the start-up logo of the Hellboard display (decision F16): Firmware/libs/logo.py with the
// 128x64 bitmap (framebuf MONO_HLSB) and a colored preview PNG. The display shows rows 0-15 in yellow
// and rows 16-63 in blue (fixed by the hardware): the yellow band carries the "HELLBOARD" lettering,
// the blue part the hexagon with four keys from the Companion icon.
//
// Usage (from Software/):  dotnet run tools/display-logo/generate-display-logo.cs

#:property ManagePackageVersionsCentrally=false
#:package SkiaSharp@3.119.4
#:package SkiaSharp.NativeAssets.Win32@3.119.4

using System.Text;
using SkiaSharp;

const int Width = 128, Height = 64, YellowRows = 16;
var pixels = new bool[Height, Width];

void Rect(int x, int y, int w, int h, bool on = true)
{
    for (var yy = y; yy < y + h; yy++)
    {
        for (var xx = x; xx < x + w; xx++)
        {
            if (xx is >= 0 and < Width && yy is >= 0 and < Height)
            {
                pixels[yy, xx] = on;
            }
        }
    }
}

// ---- "HELLBOARD" in the yellow band: angular 12x12 letters, 3 px strokes, 2 px gaps -------------
const int S = 3, L = 12, Top = 2;

void Chamfer(int x, int y, bool left, bool top)
{
    // cut a 2 px corner for the angular look of the game UI
    var cx = left ? x : x + L - 1;
    var cy = top ? Top : Top + L - 1;
    var dx = left ? 1 : -1;
    var dy = top ? 1 : -1;
    pixels[cy, cx] = false;
    pixels[cy, cx + dx] = false;
    pixels[cy + dy, cx] = false;
}

var letters = new Dictionary<char, Action<int>>
{
    ['H'] = x => { Rect(x, Top, S, L); Rect(x + L - S, Top, S, L); Rect(x, Top + 5, L, 2); },
    ['E'] = x => { Rect(x, Top, S, L); Rect(x, Top, L, S); Rect(x, Top + 5, L - 3, 2); Rect(x, Top + L - S, L, S); Chamfer(x, 0, true, true); Chamfer(x, 0, true, false); },
    ['L'] = x => { Rect(x, Top, S, L); Rect(x, Top + L - S, L, S); Chamfer(x, 0, true, false); },
    ['B'] = x => { Rect(x, Top, S, L); Rect(x, Top, L - 2, S); Rect(x, Top + 5, L - 2, 2); Rect(x, Top + L - S, L - 2, S); Rect(x + L - S, Top + 1, S, 4); Rect(x + L - S, Top + 7, S, 4); },
    ['O'] = x => { Rect(x, Top, S, L); Rect(x + L - S, Top, S, L); Rect(x, Top, L, S); Rect(x, Top + L - S, L, S); Chamfer(x, 0, true, true); Chamfer(x, 0, false, true); Chamfer(x, 0, true, false); Chamfer(x, 0, false, false); },
    ['A'] = x => { Rect(x, Top, S, L); Rect(x + L - S, Top, S, L); Rect(x, Top, L, S); Rect(x, Top + 6, L, 2); Chamfer(x, 0, true, true); Chamfer(x, 0, false, true); },
    ['R'] = x => { Rect(x, Top, S, L); Rect(x, Top, L - 2, S); Rect(x + L - S, Top + 1, S, 5); Rect(x, Top + 5, L - 2, 2); Rect(x + L - S - 1, Top + 7, S, 2); Rect(x + L - S, Top + 9, S, 3); },
    ['D'] = x => { Rect(x, Top, S, L); Rect(x, Top, L - 2, S); Rect(x, Top + L - S, L - 2, S); Rect(x + L - S, Top + 2, S, L - 4); Rect(x + L - S - 1, Top + 1, 2, 2); Rect(x + L - S - 1, Top + L - 3, 2, 2); },
};

const string Word = "HELLBOARD";
var wordWidth = (Word.Length * L) + ((Word.Length - 1) * 2);
var start = (Width - wordWidth) / 2;
for (var i = 0; i < Word.Length; i++)
{
    letters[Word[i]](start + (i * (L + 2)));
}

// ---- hexagon with four keys in the blue part (like the Companion icon) --------------------------
const double Cx = 63.5, Cy = 39.5, Rx = 26, Ry = 20.5;   // points left and right, flat top and bottom
(double X, double Y)[] Hexagon(double rx, double ry) =>
[
    (Cx - rx, Cy), (Cx - (rx / 2), Cy - ry), (Cx + (rx / 2), Cy - ry),
    (Cx + rx, Cy), (Cx + (rx / 2), Cy + ry), (Cx - (rx / 2), Cy + ry),
];

bool Inside((double X, double Y)[] polygon, double x, double y)
{
    var inside = false;
    for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
    {
        if ((polygon[i].Y > y) != (polygon[j].Y > y)
            && x < ((polygon[j].X - polygon[i].X) * (y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y)) + polygon[i].X)
        {
            inside = !inside;
        }
    }

    return inside;
}

var outer = Hexagon(Rx, Ry);
var inner = Hexagon(Rx - 4.5, Ry - 3.5);
for (var y = YellowRows + 1; y < Height; y++)
{
    for (var x = 0; x < Width; x++)
    {
        if (Inside(outer, x + 0.5, y + 0.5) && !Inside(inner, x + 0.5, y + 0.5))
        {
            pixels[y, x] = true;
        }
    }
}

const int Key = 9, Gap = 3;
var keysLeft = (int)Math.Round(Cx + 0.5 - Key - (Gap / 2.0));
var keysTop = (int)Math.Round(Cy + 0.5 - Key - (Gap / 2.0));
foreach (var (kx, ky) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
{
    var x = keysLeft + (kx * (Key + Gap));
    var y = keysTop + (ky * (Key + Gap));
    Rect(x, y, Key, Key);
    pixels[y, x] = pixels[y, x + Key - 1] = pixels[y + Key - 1, x] = pixels[y + Key - 1, x + Key - 1] = false;   // rounded corners
}

// ---- output -------------------------------------------------------------------------------------
var dir = Path.GetDirectoryName(Path.GetFullPath(AppContext.GetData("EntryPointFilePath") as string ?? "."))!;
var repo = Path.GetFullPath(Path.Combine(dir, "..", "..", ".."));

var bytes = new byte[Width * Height / 8];
for (var y = 0; y < Height; y++)
{
    for (var x = 0; x < Width; x++)
    {
        if (pixels[y, x])
        {
            bytes[(y * Width / 8) + (x / 8)] |= (byte)(0x80 >> (x % 8));
        }
    }
}

var py = new StringBuilder();
py.Append("# Start-up logo of the display (decision F16), 128x64, framebuf.MONO_HLSB. Rows 0-15 appear yellow,\n");
py.Append("# rows 16-63 blue (hardware). Generated by Software/tools/display-logo/generate-display-logo.cs - do not edit.\n\n");
py.Append("WIDTH = 128\nHEIGHT = 64\n\nBITMAP = (\n");
for (var i = 0; i < bytes.Length; i += 32)
{
    py.Append("    b'").Append(string.Concat(bytes.Skip(i).Take(32).Select(b => $"\\x{b:x2}"))).Append("'\n");
}

py.Append(")\n");
File.WriteAllText(Path.Combine(repo, "Firmware", "libs", "logo.py"), py.ToString().ReplaceLineEndings("\r\n"));

// Preview: 4x scaled, yellow/blue like the real display.
const int Scale = 4;
using var preview = new SKBitmap(Width * Scale, Height * Scale);
using (var canvas = new SKCanvas(preview))
{
    canvas.Clear(new SKColor(8, 8, 10));
    using var yellow = new SKPaint { Color = new SKColor(0xF5, 0xD3, 0x3B) };
    using var blue = new SKPaint { Color = new SKColor(0x4F, 0xB8, 0xF0) };
    for (var y = 0; y < Height; y++)
    {
        for (var x = 0; x < Width; x++)
        {
            if (pixels[y, x])
            {
                canvas.DrawRect(x * Scale, y * Scale, Scale - 1, Scale - 1, y < YellowRows ? yellow : blue);
            }
        }
    }
}

var previewPath = Path.Combine(dir, "display-logo-preview.png");
using (var png = preview.Encode(SKEncodedImageFormat.Png, 100))
{
    File.WriteAllBytes(previewPath, png.ToArray());
}

Console.WriteLine($"Firmware/libs/logo.py ({bytes.Length} bytes) and {previewPath} written.");
