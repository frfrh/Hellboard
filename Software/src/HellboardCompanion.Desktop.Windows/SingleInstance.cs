// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Desktop.Windows;

/// <summary>
/// Ensures that only one Hellboard Companion runs per user session. A second start asks the running
/// instance to show its window and then exits (it would otherwise compete for the hotkey, the screen
/// capture and later the Hellboard's serial port).
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\HellboardCompanion.SingleInstance";
    private const string ShowEventName = @"Local\HellboardCompanion.ShowWindow";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showRequested;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle showRequested)
    {
        _mutex = mutex;
        _showRequested = showRequested;
    }

    /// <summary>
    /// Returns the instance lock, or <c>null</c> if another instance is already running – in that
    /// case the running instance has been asked to show its window.
    /// </summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            if (EventWaitHandle.TryOpenExisting(ShowEventName, out var showRequested))
            {
                showRequested.Set();
                showRequested.Dispose();
            }

            return null;
        }

        return new SingleInstance(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName));
    }

    /// <summary>Runs <paramref name="show"/> (on a thread-pool thread) whenever another start asks for the window.</summary>
    public void OnShowRequested(Action show) =>
        _registration = ThreadPool.RegisterWaitForSingleObject(_showRequested, (_, _) => show(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _registration?.Unregister(null);
        _showRequested.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
