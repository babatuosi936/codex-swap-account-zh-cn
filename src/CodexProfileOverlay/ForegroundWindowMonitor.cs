using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
using CodexProfileOverlay.Core.Services;

namespace CodexProfileOverlay;

internal sealed class ForegroundWindowMonitor : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly Action refresh;
    private readonly SafeLogger logger;
    private readonly WinEventCallback callback;
    private readonly List<IntPtr> hooks = [];
    private int pending;
    private int disposed;

    public ForegroundWindowMonitor(Dispatcher dispatcher, Action refresh, SafeLogger logger)
    {
        this.dispatcher = dispatcher;
        this.refresh = refresh;
        this.logger = logger;
        callback = OnWindowEvent; // Keep the native callback alive until unhooked.
    }

    public void Start()
    {
        if (hooks.Count > 0 || Volatile.Read(ref disposed) != 0) return;
        // Foreground change and minimize end. OUTOFCONTEXT keeps the hook in
        // this helper; no injection or input interception in Codex processes.
        foreach (uint windowEvent in new uint[] { 0x0003, 0x0017 })
        {
            IntPtr hook = SetWinEventHook(windowEvent, windowEvent, IntPtr.Zero, callback, 0, 0, 0);
            if (hook != IntPtr.Zero) hooks.Add(hook);
            else logger.Info("Window event hook unavailable; periodic tracking remains active.");
        }
    }

    private void OnWindowEvent(IntPtr hook, uint eventType, IntPtr hwnd, int objectId, int childId, uint threadId, uint time)
    {
        if (Volatile.Read(ref disposed) != 0 || dispatcher.HasShutdownStarted
            || Interlocked.Exchange(ref pending, 1) != 0) return;
        try
        {
            dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                Interlocked.Exchange(ref pending, 0);
                if (Volatile.Read(ref disposed) == 0) refresh();
            }));
        }
        catch (InvalidOperationException) { Interlocked.Exchange(ref pending, 0); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        foreach (IntPtr hook in hooks) UnhookWinEvent(hook);
        hooks.Clear();
    }

    private delegate void WinEventCallback(IntPtr hook, uint eventType, IntPtr hwnd, int objectId, int childId, uint threadId, uint time);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEventCallback callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWinEvent(IntPtr hook);
}
