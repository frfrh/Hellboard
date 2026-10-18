// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.InteropServices;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging;
using static HellboardCompanion.Platform.Windows.Interop.NativeMethods;

namespace HellboardCompanion.Platform.Windows;

/// <summary>
/// Locates the game window via Win32 (implementation plan §6.1). Runs on its own thread with a
/// message loop: WinEvent hooks react immediately to moves, focus and minimize changes, a 1 s
/// timer covers process start/exit and display configuration changes.
/// Only process name, process id and window/monitor geometry are queried – the game process
/// itself is never opened.
/// </summary>
public sealed class Win32GameLocator : IGameLocator
{
    private const uint PollIntervalMs = 1000;

    private readonly GameLocatorOptions _options;
    private readonly ILogger<Win32GameLocator> _logger;
    private readonly WinEventProc _winEventProc;
    private readonly nint _winEventProcPtr;
    private readonly ManualResetEventSlim _threadReady = new();
    private readonly List<nint> _processHooks = [];

    private Thread? _thread;
    private uint _threadId;
    private nint _foregroundHook;
    private uint _hookedProcessId;
    private nint _window;
    private nint _lastForegroundLogged;
    private volatile GameSnapshot _current = GameSnapshot.NotRunning;

    public Win32GameLocator(GameLocatorOptions options, ILogger<Win32GameLocator> logger)
    {
        _options = options;
        _logger = logger;
        _winEventProc = OnWinEvent;
        _winEventProcPtr = Marshal.GetFunctionPointerForDelegate(_winEventProc);
    }

    public GameSnapshot Current => _current;

    public event EventHandler<GameSnapshot>? Changed;

    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "GameLocator" };
        _thread.Start();
        _threadReady.Wait();
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
        _threadReady.Dispose();
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();

        // Physical pixels on this thread even if the host has no per-monitor DPI manifest.
        SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        PeekMessage(out _, 0, 0, 0, PM_NOREMOVE); // creates the message queue before Start() returns
        _foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0, _winEventProcPtr, 0, 0, WINEVENT_OUTOFCONTEXT);
        var timer = SetTimer(0, 0, PollIntervalMs, 0);
        _threadReady.Set();

        Evaluate();

        while (GetMessage(out var msg, 0, 0, 0) > 0)
        {
            if (msg.Message == WM_TIMER)
            {
                Evaluate();
                continue;
            }

            TranslateMessage(in msg);
            DispatchMessage(in msg);
        }

        KillTimer(0, timer);
        UnhookWinEvent(_foregroundHook);
        HookProcess(0);
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint idEventThread, uint time)
    {
        var relevant = eventType switch
        {
            EVENT_SYSTEM_FOREGROUND => true,
            EVENT_OBJECT_LOCATIONCHANGE or EVENT_OBJECT_DESTROY => hwnd == _window && idObject == OBJID_WINDOW && idChild == 0,
            EVENT_SYSTEM_MINIMIZESTART or EVENT_SYSTEM_MINIMIZEEND => hwnd == _window,
            _ => false,
        };

        if (relevant)
        {
            Evaluate();
        }
    }

    private void Evaluate()
    {
        try
        {
            Publish(DetermineSnapshot());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Locating the game window failed");
        }
    }

    private GameSnapshot DetermineSnapshot()
    {
        // There can be several processes with the game's name (seen on a Windows 10 PC: the game was found
        // for a moment, then only "starting" for a quarter of an hour). The game is the one with the window.
        var processIds = FindProcessIds();
        if (processIds.Count == 0)
        {
            HookProcess(0);
            _window = 0;
            return GameSnapshot.NotRunning;
        }

        if (_window == 0 || !IsWindow(_window) || !(processIds.Contains(GetProcessIdOfWindow(_window)) || TitleOf(_window) == _options.WindowTitle))
        {
            _window = FindMainWindow(processIds);
            if (_window == 0)
            {
                // Seen on a Windows 10 PC: the game process had no enumerable window at all. Fall back to the
                // window title, then to the foreground window if it belongs to the game.
                _window = FindWindowByTitle(_options.WindowTitle);
                var foreground = GetForegroundWindow();
                if (_window == 0 && foreground != 0 && processIds.Contains(GetProcessIdOfWindow(foreground)))
                {
                    _window = foreground;
                }

                if (_window != 0)
                {
                    _logger.LogInformation("Game window found without the process list: {Window}", Describe(_window));
                }
            }
        }

        var processId = _window != 0 ? GetProcessIdOfWindow(_window) : processIds.Min();
        HookProcess(processId);

        if (_window == 0)
        {
            if (_current.Status != GameStatus.Starting || _lastForegroundLogged != GetForegroundWindow())
            {
                _lastForegroundLogged = GetForegroundWindow();
                _logger.LogInformation(
                    "Game process(es) {Processes} without a visible main window: {Windows}; foreground window: {Foreground}",
                    string.Join(", ", processIds), DescribeWindows(processIds), _lastForegroundLogged == 0 ? "none" : Describe(_lastForegroundLogged));
            }

            return GameSnapshot.Starting;
        }

        var monitors = Win32Monitors.GetAll();
        var isMinimized = IsIconic(_window);
        var clientBounds = isMinimized ? default : GetClientBounds(_window);

        MonitorInfo monitor;
        if (isMinimized)
        {
            // A minimized window has no useful geometry; Windows still knows its restore monitor.
            var handle = MonitorFromWindow(_window, MONITOR_DEFAULTTONEAREST);
            monitor = monitors.FirstOrDefault(m => m.Handle == handle).Info ?? monitors[0].Info;
        }
        else
        {
            monitor = GameLocationRules.SelectMonitor(clientBounds, monitors.Select(m => m.Info).ToList(), _options.MonitorOverride);
        }

        return GameSnapshot.Running(new GameLocation(
            (int)processId,
            _window,
            monitor,
            clientBounds,
            isMinimized ? WindowMode.Windowed : GameLocationRules.ClassifyWindowMode(clientBounds, monitor.Bounds),
            GetForegroundWindow() == _window,
            isMinimized));
    }

    private void Publish(GameSnapshot snapshot)
    {
        if (snapshot.Equals(_current))
        {
            return;
        }

        _current = snapshot;
        Changed?.Invoke(this, snapshot);

        if (snapshot.Location is { } location)
        {
            _logger.LogInformation(
                "Game {Status}: monitor {Monitor}, client {Client}, {Mode}, foreground={Foreground}, minimized={Minimized}",
                snapshot.Status, location.Monitor, location.ClientBounds, location.Mode, location.IsForeground, location.IsMinimized);
        }
        else
        {
            _logger.LogInformation("Game {Status}", snapshot.Status);
        }
    }

    private HashSet<uint> FindProcessIds()
    {
        var processes = Process.GetProcessesByName(_options.ProcessName);
        try
        {
            return processes.Select(p => (uint)p.Id).ToHashSet();
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    /// <summary>(Re-)registers the hooks that only concern the game process.</summary>
    private void HookProcess(uint processId)
    {
        if (processId == _hookedProcessId)
        {
            return;
        }

        foreach (var hook in _processHooks)
        {
            UnhookWinEvent(hook);
        }

        _processHooks.Clear();
        _hookedProcessId = processId;

        if (processId != 0)
        {
            _processHooks.Add(SetWinEventHook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_LOCATIONCHANGE, 0, _winEventProcPtr, processId, 0, WINEVENT_OUTOFCONTEXT));
            _processHooks.Add(SetWinEventHook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND, 0, _winEventProcPtr, processId, 0, WINEVENT_OUTOFCONTEXT));
        }
    }

    private static uint GetProcessIdOfWindow(nint window)
    {
        GetWindowThreadProcessId(window, out var processId);
        return processId;
    }

    /// <summary>The largest visible, unowned, non-tool window of the processes.</summary>
    private static nint FindMainWindow(HashSet<uint> processIds)
    {
        nint best = 0;
        long bestArea = 0;
        EnumWindowsProc callback = (hwnd, _) =>
        {
            if (processIds.Contains(GetProcessIdOfWindow(hwnd))
                && IsWindowVisible(hwnd)
                && GetWindow(hwnd, GW_OWNER) == 0
                && (GetWindowLongPtr(hwnd, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) == 0
                && GetWindowRect(hwnd, out var rect))
            {
                var area = PixelRect.FromEdges(rect.Left, rect.Top, rect.Right, rect.Bottom).Area;
                if (area > bestArea)
                {
                    best = hwnd;
                    bestArea = area;
                }
            }

            return true;
        };

        EnumWindows(Marshal.GetFunctionPointerForDelegate(callback), 0);
        GC.KeepAlive(callback);
        return best;
    }

    /// <summary>The largest visible top-level window with exactly this title.</summary>
    private static nint FindWindowByTitle(string title)
    {
        nint best = 0;
        long bestArea = 0;
        EnumWindowsProc callback = (hwnd, _) =>
        {
            if (IsWindowVisible(hwnd) && GetWindowRect(hwnd, out var rect) && TitleOf(hwnd) == title)
            {
                var area = PixelRect.FromEdges(rect.Left, rect.Top, rect.Right, rect.Bottom).Area;
                if (area > bestArea)
                {
                    best = hwnd;
                    bestArea = area;
                }
            }

            return true;
        };

        EnumWindows(Marshal.GetFunctionPointerForDelegate(callback), 0);
        GC.KeepAlive(callback);
        return best;
    }

    private static string TitleOf(nint window)
    {
        var buffer = new char[256];
        var length = GetWindowText(window, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    /// <summary>Title, class, process and size of a window, for the log.</summary>
    private static string Describe(nint window)
    {
        var className = new char[256];
        var classLength = GetClassName(window, className, className.Length);
        GetWindowRect(window, out var rect);
        return $"\"{TitleOf(window)}\" class {new string(className, 0, Math.Max(0, classLength))}, process {GetProcessIdOfWindow(window)}, "
            + $"{rect.Right - rect.Left}×{rect.Bottom - rect.Top}{(IsWindowVisible(window) ? string.Empty : " hidden")}";
    }

    /// <summary>All top-level windows of the processes with what excludes them, for the log (diagnosis on other PCs).</summary>
    private static string DescribeWindows(HashSet<uint> processIds)
    {
        var windows = new List<string>();
        EnumWindowsProc callback = (hwnd, _) =>
        {
            if (processIds.Contains(GetProcessIdOfWindow(hwnd)) && GetWindowRect(hwnd, out var rect))
            {
                windows.Add($"{GetProcessIdOfWindow(hwnd)}: {rect.Right - rect.Left}×{rect.Bottom - rect.Top}"
                    + (IsWindowVisible(hwnd) ? string.Empty : " hidden")
                    + (GetWindow(hwnd, GW_OWNER) != 0 ? " owned" : string.Empty)
                    + ((GetWindowLongPtr(hwnd, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0 ? " tool" : string.Empty));
            }

            return true;
        };

        EnumWindows(Marshal.GetFunctionPointerForDelegate(callback), 0);
        GC.KeepAlive(callback);
        return windows.Count == 0 ? "no windows" : string.Join("; ", windows.Take(10));
    }

    private static PixelRect GetClientBounds(nint window)
    {
        if (!GetClientRect(window, out var client))
        {
            return default;
        }

        var origin = new POINT();
        ClientToScreen(window, ref origin);
        return new PixelRect(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top);
    }
}
