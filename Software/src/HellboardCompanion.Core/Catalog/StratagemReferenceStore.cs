// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace HellboardCompanion.Core.Catalog;

/// <summary>Kind of slot a reference image was taken from; player and mission slots look different.</summary>
public enum ReferenceSlot
{
    Player,
    Mission,
}

/// <summary>A slot image from the game that the user assigned to a stratagem.</summary>
/// <param name="Path">PNG of the slot plus a margin of 1/8 of the slot on each side (as saved for the history).</param>
public sealed record StratagemReference(string Id, ReferenceSlot Slot, string Path)
{
    /// <summary>The slot inside a saved image of <paramref name="width"/> pixels (slot + 2 × slot/8).</summary>
    public static (int Offset, int Size) SlotIn(int width)
    {
        var size = Enumerable.Range(1, width).First(s => s + (2 * (s / 8)) >= width);
        return (size / 8, size);
    }
}

/// <summary>
/// Reference images of stratagems whose icon in the game differs from the SVG of the icon repository
/// (e.g. the badges of EAT-17 and EAT-411, or a wrong icon). Assigned by the user from a problem in the
/// history and stored only locally in <c>%LOCALAPPDATA%\HellboardCompanion\references\&lt;slot&gt;\&lt;id&gt;\</c>
/// – the images are game content and never leave the PC.
/// </summary>
public sealed partial class StratagemReferenceStore(IPlatformPaths paths, ILogger<StratagemReferenceStore> logger)
{
    /// <summary>Oldest images are removed above this number per stratagem and slot kind.</summary>
    public const int MaxPerStratagem = 5;

    /// <summary>Mean absolute difference (gray, 32×32) below which a new image counts as already stored.</summary>
    private const double DuplicateThreshold = 4.0;

    private volatile IReadOnlyList<StratagemReference> _references = [];

    public string Directory { get; } = System.IO.Path.Combine(paths.DataDirectory, "references");

    public IReadOnlyList<StratagemReference> References => _references;

    /// <summary>Raised after images were added or removed (the recognizer is rebuilt).</summary>
    public event EventHandler? Changed;

    public static ReferenceSlot? SlotOfProblemImage(string path) => ProblemImagePattern().Match(System.IO.Path.GetFileNameWithoutExtension(path)) switch
    {
        { Success: true } m when m.Groups[1].Value == "slot" => ReferenceSlot.Player,
        { Success: true } => ReferenceSlot.Mission,
        _ => null,
    };

    public void Load()
    {
        var references = new List<StratagemReference>();
        foreach (var slot in Enum.GetValues<ReferenceSlot>())
        {
            var root = SlotDirectory(slot);
            if (!System.IO.Directory.Exists(root))
            {
                continue;
            }

            foreach (var folder in System.IO.Directory.EnumerateDirectories(root))
            {
                var id = System.IO.Path.GetFileName(folder);
                references.AddRange(System.IO.Directory.EnumerateFiles(folder, "*.png").Order(StringComparer.Ordinal)
                    .Select(file => new StratagemReference(id, slot, file)));
            }
        }

        _references = references;
        logger.LogInformation("Stratagem reference images: {Count}", references.Count);
    }

    public int Count(string id) => _references.Count(r => r.Id == id);

    /// <summary>Stores a copy of <paramref name="imagePath"/> as reference of <paramref name="id"/>.</summary>
    /// <returns><c>false</c> if the same image is already stored.</returns>
    public bool Add(string imagePath, string id, ReferenceSlot slot)
    {
        if (!StratagemIcon.ToId(id).Equals(id, StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{id}' is not a stratagem id.", nameof(id));
        }

        var folder = System.IO.Path.Combine(SlotDirectory(slot), id);
        var existing = _references.Where(r => r.Id == id && r.Slot == slot).ToList();
        if (existing.Any(r => IsSameImage(r.Path, imagePath)))
        {
            return false;
        }

        System.IO.Directory.CreateDirectory(folder);
        var name = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}";
        var target = System.IO.Path.Combine(folder, $"{name}.png");
        for (var n = 2; File.Exists(target); n++)
        {
            target = System.IO.Path.Combine(folder, $"{name}-{n}.png");
        }

        File.Copy(imagePath, target);
        foreach (var old in existing.Take(Math.Max(0, existing.Count + 1 - MaxPerStratagem)))
        {
            File.Delete(old.Path);
        }

        logger.LogInformation("Reference image for {Id} ({Slot}) added from {Image}", id, slot, imagePath);
        Reload();
        return true;
    }

    /// <summary>Removes all reference images of <paramref name="id"/>.</summary>
    public void Remove(string id)
    {
        foreach (var slot in Enum.GetValues<ReferenceSlot>())
        {
            var folder = System.IO.Path.Combine(SlotDirectory(slot), id);
            if (System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.Delete(folder, recursive: true);
            }
        }

        logger.LogInformation("Reference images of {Id} removed", id);
        Reload();
    }

    private void Reload()
    {
        Load();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private string SlotDirectory(ReferenceSlot slot) => System.IO.Path.Combine(Directory, slot == ReferenceSlot.Player ? "player" : "mission");

    private static bool IsSameImage(string a, string b)
    {
        using var first = Small(a);
        using var second = Small(b);
        if (first.Empty() || second.Empty())
        {
            return false;
        }

        using var difference = new Mat();
        Cv2.Absdiff(first, second, difference);
        return Cv2.Mean(difference).Val0 < DuplicateThreshold;
    }

    private static Mat Small(string path)
    {
        using var image = Cv2.ImRead(path, ImreadModes.Grayscale);
        var small = new Mat();
        if (!image.Empty())
        {
            Cv2.Resize(image, small, new Size(32, 32), interpolation: InterpolationFlags.Area);
        }

        return small;
    }

    /// <summary>File names of LoadoutPipeline: problem-…-slot2.png (player) or problem-…-mission1.png.</summary>
    [GeneratedRegex(@"-(slot|mission)\d+$")]
    private static partial Regex ProblemImagePattern();
}
