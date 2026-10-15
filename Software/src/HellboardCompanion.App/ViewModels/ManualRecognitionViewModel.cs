// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellboardCompanion.Core.Pipeline;

namespace HellboardCompanion.App.ViewModels;

/// <summary>Manual trigger and offline image analysis in the status window (implementation plan §6.13).</summary>
public sealed partial class ManualRecognitionViewModel : ObservableObject, IDisposable
{
    private readonly ManualTriggerService? _trigger;
    private readonly LoadoutPipeline? _pipeline;

    [ObservableProperty]
    private string _result = "–";

    public ManualRecognitionViewModel(ManualTriggerService? trigger, LoadoutPipeline? pipeline, RecognitionOptions? options)
    {
        _trigger = trigger;
        _pipeline = pipeline;
        HotkeyInfo = trigger?.RegisteredHotkey is { } hotkey
            ? $"Hotkey: {hotkey}{(options?.ManualTransfer == false ? " (test mode – nothing is locked in)" : string.Empty)}"
            : trigger?.HotkeyProblem ?? "Hotkey disabled (automatic mode)";

        if (_trigger is not null)
        {
            _trigger.Completed += OnCompleted;
        }
    }

    public string HotkeyInfo { get; }

    public void Dispose()
    {
        if (_trigger is not null)
        {
            _trigger.Completed -= OnCompleted;
        }
    }

    /// <summary>Called by the view after the user picked an image file.</summary>
    public async Task AnalyzeFileAsync(string path)
    {
        if (_pipeline is null)
        {
            return;
        }

        Result = $"Analyzing {Path.GetFileName(path)} …";
        try
        {
            var observation = await _pipeline.AnalyzeFileAsync(path);
            Result = $"{Path.GetFileName(path)}{Environment.NewLine}{ManualRecognitionText.Describe(observation)}";
        }
        catch (Exception ex)
        {
            Result = $"Analysis of {Path.GetFileName(path)} failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RecognizeNowAsync()
    {
        if (_trigger is not null)
        {
            Result = "Recognizing …";
            await _trigger.TriggerAsync();
        }
    }

    private void OnCompleted(object? sender, ManualRecognitionResult result) => Dispatcher.UIThread.Post(() =>
    {
        var (title, body) = ManualRecognitionText.Describe(result);
        Result = $"{result.Timestamp.LocalDateTime:HH:mm:ss} – {title}{Environment.NewLine}{body}";
    });
}
