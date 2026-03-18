using System.Runtime.InteropServices;

namespace SystemAnalysis.Interop;

public static class InputController
{
    public static (int X, int Y) GetMousePosition()
    {
        NativeMethods.GetCursorPos(out var point);
        return (point.X, point.Y);
    }

    public static void MoveMouse(int x, int y)
    {
        NativeMethods.SetCursorPos(x, y);
    }

    public static void LeftClick()
    {
        SendMouse(NativeMethods.MOUSEEVENTF_LEFTDOWN);
        Thread.Sleep(35);
        SendMouse(NativeMethods.MOUSEEVENTF_LEFTUP);
    }

    public static void RightClick()
    {
        SendMouse(NativeMethods.MOUSEEVENTF_RIGHTDOWN);
        Thread.Sleep(35);
        SendMouse(NativeMethods.MOUSEEVENTF_RIGHTUP);
    }

    public static void KeyDown(ushort virtualKey)
    {
        SendKey(virtualKey, 0);
    }

    public static void KeyUp(ushort virtualKey)
    {
        SendKey(virtualKey, NativeMethods.KEYEVENTF_KEYUP);
    }

    public static void PressShortcut(params ushort[] virtualKeys)
    {
        foreach (var key in virtualKeys)
        {
            KeyDown(key);
            Thread.Sleep(20);
        }

        for (var i = virtualKeys.Length - 1; i >= 0; i--)
        {
            Thread.Sleep(20);
            KeyUp(virtualKeys[i]);
        }
    }

    public static void ReleaseCommonModifiers()
    {
        // Release the most common modifier keys defensively in case a run is
        // interrupted while one of them is logically held down.
        KeyUp(NativeMethods.VK_CONTROL);
        Thread.Sleep(10);
        KeyUp(NativeMethods.VK_SHIFT);
        Thread.Sleep(10);
        KeyUp(NativeMethods.VK_MENU);
    }

    private static void SendMouse(uint flags)
    {
        var inputs = new[]
        {
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_MOUSE,
                U = new NativeMethods.InputUnion
                {
                    mi = new NativeMethods.MOUSEINPUT
                    {
                        dwFlags = flags
                    }
                }
            }
        };

        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static void SendKey(ushort virtualKey, uint flags)
    {
        var inputs = new[]
        {
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = virtualKey,
                        dwFlags = flags
                    }
                }
            }
        };

        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
