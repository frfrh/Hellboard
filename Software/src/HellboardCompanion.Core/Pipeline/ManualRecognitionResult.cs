// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Recognition;

namespace HellboardCompanion.Core.Pipeline;

/// <summary>Outcome of a manual trigger (implementation plan §6.13).</summary>
/// <param name="ScreenDetected">Whether the loadout screen was detected in the majority of the frames.</param>
/// <param name="Loadout">Majority result per slot; <c>null</c> if no frame could be captured.</param>
/// <param name="Locked">Whether the result was locked in (and will be transferred).</param>
/// <param name="TestMode">Manual transfer disabled: the result is only shown.</param>
/// <param name="Problem">Reason why nothing was locked in, for the user.</param>
public sealed record ManualRecognitionResult(
    DateTimeOffset Timestamp,
    int Frames,
    bool ScreenDetected,
    ReadyStatus Status,
    LoadoutSnapshot? Loadout,
    LoadoutScreenResult? Screen,
    bool Locked,
    bool TestMode,
    IReadOnlyList<string> SavedFiles,
    string? Problem)
{
    /// <summary>Most frequent value per slot and most frequent status over several observations.</summary>
    public static (LoadoutSnapshot Loadout, ReadyStatus Status) Majority(IReadOnlyList<LoadoutObservation> observations)
    {
        var snapshots = observations.Select(o => o.Loadout).OfType<LoadoutSnapshot>().ToList();
        if (snapshots.Count == 0)
        {
            throw new ArgumentException("At least one observation with a loadout is required.", nameof(observations));
        }

        static IReadOnlyList<SlotValue> Vote(IEnumerable<IReadOnlyList<SlotValue>> rows)
        {
            var list = rows.ToList();
            return Enumerable.Range(0, list[0].Count)
                .Select(i => list.Select(r => r[i]).GroupBy(v => v).OrderByDescending(g => g.Count()).First().Key)
                .ToList();
        }

        var loadout = new LoadoutSnapshot(Vote(snapshots.Select(s => s.PlayerSlots)), Vote(snapshots.Select(s => s.MissionSlots)));
        var status = observations.Select(o => o.Status).Where(s => s != ReadyStatus.Unknown)
            .GroupBy(s => s).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        return (loadout, status);
    }
}
