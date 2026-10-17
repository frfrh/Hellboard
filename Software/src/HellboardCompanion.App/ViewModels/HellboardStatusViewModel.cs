// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellboardCompanion.Core.Hellboard;

namespace HellboardCompanion.App.ViewModels;

/// <summary>Connection and transfer state of the Hellboard in the status window (implementation plan §6.6).</summary>
public sealed partial class HellboardStatusViewModel : ObservableObject, IDisposable
{
    private readonly HellboardLink? _link;

    [ObservableProperty]
    private string _connection = "–";

    [ObservableProperty]
    private string _firmware = "–";

    [ObservableProperty]
    private string _lastSent = "–";

    [ObservableProperty]
    private string? _warning;

    [ObservableProperty]
    private string? _defaultResult;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetAsDefaultCommand))]
    private bool _canSetDefault;

    public HellboardStatusViewModel(HellboardLink? link)
    {
        _link = link;
        if (_link is not null)
        {
            _link.StateChanged += OnStateChanged;
            Apply(_link.State);
        }
    }

    public void Dispose()
    {
        if (_link is not null)
        {
            _link.StateChanged -= OnStateChanged;
        }
    }

    /// <summary>"Set as default" (F3): the only way the board's flash is written.</summary>
    [RelayCommand(CanExecute = nameof(CanSetDefault))]
    private async Task SetAsDefaultAsync()
    {
        if (_link is null)
        {
            return;
        }

        DefaultResult = "Saving …";
        try
        {
            await _link.SetAsDefaultAsync();
            DefaultResult = $"{DateTime.Now:HH:mm:ss} – saved as default on the Hellboard";
        }
        catch (Exception ex)
        {
            DefaultResult = $"Not saved: {ex.Message}";
        }
    }

    private void OnStateChanged(object? sender, HellboardState state) => Dispatcher.UIThread.Post(() => Apply(state));

    private void Apply(HellboardState state)
    {
        Connection = state.Status switch
        {
            HellboardStatus.DryRun => "Dry run – nothing is sent",
            HellboardStatus.NotFound => "No Hellboard found",
            HellboardStatus.NoProtocol => $"Hellboard on {state.Port} – {state.Message}",
            HellboardStatus.Connected => $"Connected on {state.Port}",
            HellboardStatus.Updating => $"Installing firmware on {state.Port} …",
            _ => state.Status.ToString(),
        };
        Firmware = state.Info is { } info ? $"{info.Firmware} (protocol {info.Protocol}, MicroPython {info.MicroPython})" : "–";
        LastSent = state.LastSent is { } loadout
            ? $"{state.LastSentAt?.LocalDateTime:HH:mm:ss} – {string.Join(", ", loadout.Stratagems.Select(s => s?.Name ?? "-"))}{Environment.NewLine}Multi-function: {(loadout.MissionStratagems.Count == 0 ? "–" : string.Join(", ", loadout.MissionStratagems.Select(s => s.Name)))}"
            : state.Pending is not null ? "Waiting for the Hellboard …" : "–";
        Warning = state.NotTransferable.Count > 0
            ? $"Not transferable (no input sequence known yet): {string.Join(", ", state.NotTransferable)}"
            : null;
        CanSetDefault = state.Status == HellboardStatus.Connected;
    }
}
