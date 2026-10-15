// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Text.Json;
using HellboardCompanion.Core.Capture;
using HellboardCompanion.Core.Platform;
using OpenCvSharp;

namespace HellboardCompanion.Core.Tests.TestData;

/// <summary>Client area inside a window capture (window border excluded).</summary>
public sealed record SceneClient(int X, int Y, int Width, int Height);

/// <summary>One screenshot in <c>tests/testData/local/</c> with its expected recognition result.</summary>
public sealed record Scene(
    string File,
    string Description,
    bool LoadoutScreen,
    string? ReadyStatus,
    string[]? PlayerSlots,
    string[]? OptionalMissionSlots,
    SceneClient? Client)
{
    public override string ToString() => File;
}

/// <summary>
/// Access to the local screenshots (game content, never committed) and their expectations in
/// <c>tests/testData/scenes.json</c>. Tests using a missing image are skipped, so the suite also
/// runs on machines without the screenshots.
/// </summary>
public static class LocalScenes
{
    /// <summary>Output heights of the synthetic resolutions (implementation plan §6.3.1); 0 = native.</summary>
    public static readonly int[] Heights = [720, 900, 1080, 0, 2160];

    private static readonly Lazy<IReadOnlyList<Scene>> AllScenes = new(LoadScenes);

    public static string TestDataDirectory { get; } = FindTestDataDirectory();

    public static IReadOnlyList<Scene> All => AllScenes.Value;

    /// <summary>Loadout scenes × synthetic heights.</summary>
    public static TheoryData<string, int> LoadoutScenesAtAllHeights()
    {
        var data = new TheoryData<string, int>();
        foreach (var scene in All.Where(s => s.LoadoutScreen))
        {
            foreach (var height in Heights)
            {
                data.Add(scene.File, height);
            }
        }

        return data;
    }

    /// <summary>All scenes (loadout screens and negative examples) × synthetic heights.</summary>
    public static TheoryData<string, int> AllScenesAtAllHeights()
    {
        var data = new TheoryData<string, int>();
        foreach (var scene in All)
        {
            foreach (var height in Heights)
            {
                data.Add(scene.File, height);
            }
        }

        return data;
    }

    public static Scene Get(string file) => All.Single(s => s.File == file);

    /// <summary>
    /// Loads the client area of a scene as <see cref="Frame"/>, optionally rescaled to
    /// <paramref name="height"/> (aspect ratio kept; 0 = native).
    /// </summary>
    public static Frame Load(Scene scene, int height = 0)
    {
        var path = Path.Combine(TestDataDirectory, "local", scene.File);
        Assert.SkipUnless(System.IO.File.Exists(path), $"Local test image '{scene.File}' is not available.");

        using var image = Cv2.ImRead(path, ImreadModes.Color);
        using var client = scene.Client is { } c ? new Mat(image, new Rect(c.X, c.Y, c.Width, c.Height)) : image.Clone();

        using var scaled = new Mat();
        if (height > 0 && height != client.Rows)
        {
            var width = (int)Math.Round(client.Cols * (double)height / client.Rows);
            Cv2.Resize(client, scaled, new Size(width, height), interpolation: height < client.Rows ? InterpolationFlags.Area : InterpolationFlags.Cubic);
        }
        else
        {
            client.CopyTo(scaled);
        }

        using var bgra = new Mat();
        Cv2.CvtColor(scaled, bgra, ColorConversionCodes.BGR2BGRA);

        var (rows, cols) = (bgra.Rows, bgra.Cols);
        var rowBytes = cols * Frame.BytesPerPixel;
        var pixels = new byte[rows * rowBytes];
        for (var y = 0; y < rows; y++)
        {
            Marshal.Copy(bgra.Ptr(y), pixels, y * rowBytes, rowBytes);
        }

        return new Frame(new PixelRect(0, 0, cols, rows), pixels, DateTimeOffset.Now);
    }

    private static IReadOnlyList<Scene> LoadScenes()
    {
        using var stream = System.IO.File.OpenRead(Path.Combine(TestDataDirectory, "scenes.json"));
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("scenes")
            .Deserialize<Scene[]>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static string FindTestDataDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "testData");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("tests/testData not found above the test output directory.");
    }
}
