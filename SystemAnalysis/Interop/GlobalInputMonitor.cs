using SystemAnalysis.Config;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SystemAnalysis.Interop;

public sealed class GlobalInputMonitor : IDisposable
{
    private readonly int _moveTolerance;
    private readonly NativeMethods.HookProc _keyboardProc;
    private readonly NativeMethods.HookProc _mouseProc;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private bool _disposed;
    private NativeMethods.POINT? _lastHumanMousePoint;

    public GlobalInputMonitor(SafetyConfig safety)
    {
        _moveTolerance = Math.Max(0, safety.MouseMoveTolerancePx);
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
    }

    public bool IsArmed { get; set; }

    public event Action? StartRequested;
    public event Action? StopRequested;
    public event Action? CaptureSourceRequested;
    public event Action? CaptureSecondarySourceRequested;
    public event Action? CaptureTargetRequested;

    public void Start()
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        var moduleHandle = NativeMethods.GetModuleHandle(module.ModuleName);

        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);

        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            throw new InvalidOperationException("Global hook kurulamadı.");
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        if (message is not (NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN))
        {
            return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        var keyboardInfo = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
        var isInjected = (keyboardInfo.flags & NativeMethods.LLKHF_INJECTED) != 0;
        var key = (ushort)keyboardInfo.vkCode;

        if (!isInjected)
        {
            if (key == NativeMethods.VK_F8)
            {
                StartRequested?.Invoke();
            }
            else if (key == NativeMethods.VK_F9)
            {
                StopRequested?.Invoke();
            }
            else if (key == NativeMethods.VK_ESCAPE)
            {
                StopRequested?.Invoke();
            }
            else if (key == NativeMethods.VK_F6)
            {
                CaptureSourceRequested?.Invoke();
            }
            else if (key == NativeMethods.VK_F7)
            {
                CaptureTargetRequested?.Invoke();
            }
            else if (key == NativeMethods.VK_F10)
            {
                CaptureSecondarySourceRequested?.Invoke();
            }
            else if (IsArmed)
            {
                StopRequested?.Invoke();
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        var mouseInfo = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
        var isInjected = (mouseInfo.flags & NativeMethods.LLMHF_INJECTED) != 0;

        if (!isInjected)
        {
            var message = wParam.ToInt32();

            if (IsArmed)
            {
                if (message == NativeMethods.WM_MOUSEMOVE)
                {
                    if (_lastHumanMousePoint is { } lastPoint)
                    {
                        var dx = Math.Abs(lastPoint.X - mouseInfo.pt.X);
                        var dy = Math.Abs(lastPoint.Y - mouseInfo.pt.Y);

                        if (dx > _moveTolerance || dy > _moveTolerance)
                        {
                            StopRequested?.Invoke();
                        }
                    }

                    _lastHumanMousePoint = mouseInfo.pt;
                }
                else if (message is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN or NativeMethods.WM_MOUSEWHEEL)
                {
                    StopRequested?.Invoke();
                }
            }
            else
            {
                _lastHumanMousePoint = mouseInfo.pt;
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
        }

        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
        }

        _disposed = true;
    }
}
