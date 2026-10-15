// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging;
using static HellboardCompanion.Platform.Windows.Interop.NativeMethods;

namespace HellboardCompanion.Platform.Windows;

/// <summary>
/// Global hotkeys via <c>RegisterHotKey</c> (implementation plan §6.13): no keyboard hook, no input
/// injection. Hotkeys belong to the thread that registered them, so registration and the
/// <c>WM_HOTKEY</c> messages are handled on a dedicated message-loop thread.
/// </summary>
public sealed class Win32GlobalHotkeys(ILogger<Win32GlobalHotkeys> logger) : IGlobalHotkeys
{
    private readonly ConcurrentQueue<Action> _work = new();
    private readonly ConcurrentDictionary<int, Action> _callbacks = new();
    private readonly Lock _startLock = new();
    private Thread? _thread;
    private uint _threadId;
    private int _nextId;

    public IDisposable Register(Hotkey hotkey, Action pressed)
    {
        var id = Interlocked.Increment(ref _nextId);
        var registered = RunOnHotkeyThread(() =>
        {
            if (!RegisterHotKey(0, id, ToModifiers(hotkey.Modifiers), ToVirtualKey(hotkey.Key)))
            {
                return Marshal.GetLastPInvokeError();
            }

            _callbacks[id] = pressed;
            return 0;
        });

        if (registered != 0)
        {
            throw new InvalidOperationException($"The hotkey {hotkey} could not be registered (error {registered}); it is probably used by another application.");
        }

        return new Registration(() => RunOnHotkeyThread(() =>
        {
            _callbacks.TryRemove(id, out _);
            UnregisterHotKey(0, id);
            return 0;
        }));
    }

    public void Dispose()
    {
        if (_thread is null)
        {
            return;
        }

        PostThreadMessage(_threadId, WM_QUIT, 0, 0);
        _thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    private int RunOnHotkeyThread(Func<int> action)
    {
        EnsureThread();
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Enqueue(() => completion.SetResult(action()));
        PostThreadMessage(_threadId, WM_APP, 0, 0);
        return completion.Task.Wait(TimeSpan.FromSeconds(5)) ? completion.Task.Result : -1;
    }

    private void EnsureThread()
    {
        lock (_startLock)
        {
            if (_thread is not null)
            {
                return;
            }

            using var ready = new ManualResetEventSlim();
            _thread = new Thread(() =>
            {
                _threadId = GetCurrentThreadId();
                PeekMessage(out _, 0, 0, 0, PM_NOREMOVE);
                ready.Set();
                MessageLoop();
            })
            { IsBackground = true, Name = "GlobalHotkeys" };
            _thread.Start();
            ready.Wait();
        }
    }

    private void MessageLoop()
    {
        while (GetMessage(out var msg, 0, 0, 0) > 0)
        {
            if (msg.Message == WM_APP)
            {
                while (_work.TryDequeue(out var work))
                {
                    work();
                }
            }
            else if (msg.Message == WM_HOTKEY && _callbacks.TryGetValue((int)msg.WParam, out var callback))
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        callback();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Hotkey handler failed");
                    }
                });
            }
        }

        foreach (var id in _callbacks.Keys)
        {
            UnregisterHotKey(0, id);
        }
    }

    private static uint ToModifiers(HotkeyModifiers modifiers) => (uint)modifiers | MOD_NOREPEAT;   // same bit values as MOD_*

    private static uint ToVirtualKey(string key) =>
        key.Length == 1
            ? char.ToUpperInvariant(key[0])                           // 'A'–'Z', '0'–'9' equal their VK codes
            : 0x70u + (uint)(int.Parse(key[1..]) - 1);                 // VK_F1 = 0x70

    private sealed class Registration(Func<int> unregister) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                unregister();
            }
        }
    }
}
