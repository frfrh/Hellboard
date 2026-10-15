// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging;

namespace HellboardCompanion.Core.Pipeline;

/// <summary>
/// Connects the global hotkey (and the UI) with the manual recognition (implementation plan §6.13).
/// A second trigger within one second is ignored.
/// </summary>
public sealed class ManualTriggerService(
    LoadoutPipeline pipeline,
    RecognitionOptions options,
    ILogger<ManualTriggerService> logger,
    IGlobalHotkeys? hotkeys = null) : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(1);

    private IDisposable? _registration;
    private DateTimeOffset _lastTrigger = DateTimeOffset.MinValue;
    private int _running;

    /// <summary>Hotkey that is registered, or <c>null</c> (automatic mode or registration failed).</summary>
    public Hotkey? RegisteredHotkey { get; private set; }

    /// <summary>Reason why the hotkey could not be registered.</summary>
    public string? HotkeyProblem { get; private set; }

    /// <summary>Raised on a background thread after every manual recognition.</summary>
    public event EventHandler<ManualRecognitionResult>? Completed;

    public void Start()
    {
        if (options.Mode == RecognitionMode.Automatic || hotkeys is null)
        {
            return;
        }

        try
        {
            var hotkey = Hotkey.Parse(options.TriggerHotkey);
            _registration = hotkeys.Register(hotkey, () => _ = TriggerAsync());
            RegisteredHotkey = hotkey;
            logger.LogInformation("Manual trigger hotkey {Hotkey} registered", hotkey.ToString());
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            HotkeyProblem = ex.Message;
            logger.LogWarning(ex, "Manual trigger hotkey '{Hotkey}' could not be registered", options.TriggerHotkey);
        }
    }

    /// <summary>Runs a manual recognition; returns <c>null</c> if ignored by the debounce.</summary>
    public async Task<ManualRecognitionResult?> TriggerAsync()
    {
        var now = DateTimeOffset.Now;
        if (now - _lastTrigger < Debounce || Interlocked.Exchange(ref _running, 1) == 1)
        {
            return null;
        }

        _lastTrigger = now;
        try
        {
            var result = await pipeline.RecognizeManuallyAsync().ConfigureAwait(false);
            Completed?.Invoke(this, result);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Manual recognition failed");
            return null;
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    public void Dispose() => _registration?.Dispose();
}
