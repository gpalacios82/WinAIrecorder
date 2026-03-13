using System.Runtime.InteropServices;
using System.Windows;
using WinAiRecorder.Helpers;

namespace WinAiRecorder.Services;

public class PasteService
{
    private IntPtr _capturedWindow = IntPtr.Zero;

    /// <summary>
    /// Capture the current foreground window BEFORE the overlay takes focus.
    /// Call this immediately when the hotkey/button is pressed.
    /// </summary>
    public void CaptureForegroundWindow()
    {
        _capturedWindow = NativeMethods.GetForegroundWindow();
    }

    public IntPtr CapturedWindow => _capturedWindow;

    public async Task PasteTextAsync(string text, bool useClipboard)
    {
        if (string.IsNullOrEmpty(text)) return;

        var hwnd = _capturedWindow;
        if (hwnd == IntPtr.Zero) return;

        // Restore focus to the original window
        NativeMethods.SetForegroundWindow(hwnd);

        // Attach input threads to ensure focus
        uint targetThread = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        uint currentThread = NativeMethods.GetCurrentThreadId();
        bool attached = false;
        if (targetThread != currentThread)
        {
            attached = NativeMethods.AttachThreadInput(currentThread, targetThread, true);
        }

        await Task.Delay(150);

        try
        {
            if (useClipboard)
            {
                await PasteViaClipboard(text);
            }
            else
            {
                SendUnicodeText(text);
            }
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThread, targetThread, false);
            }
        }
    }

    private static async Task PasteViaClipboard(string text)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null)
            await dispatcher.InvokeAsync(() => Clipboard.SetText(text));

        await Task.Delay(50);

        // Send Ctrl+V
        SendCtrlV();
    }

    private static void SendCtrlV()
    {
        var inputs = new NativeMethods.INPUT[]
        {
            // Ctrl down
            new() { type = NativeMethods.INPUT_KEYBOARD, union = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_CONTROL }
            }},
            // V down
            new() { type = NativeMethods.INPUT_KEYBOARD, union = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_V }
            }},
            // V up
            new() { type = NativeMethods.INPUT_KEYBOARD, union = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_V, dwFlags = NativeMethods.KEYEVENTF_KEYUP }
            }},
            // Ctrl up
            new() { type = NativeMethods.INPUT_KEYBOARD, union = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_CONTROL, dwFlags = NativeMethods.KEYEVENTF_KEYUP }
            }},
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static void SendUnicodeText(string text)
    {
        var inputs = new List<NativeMethods.INPUT>();

        foreach (char c in text)
        {
            // Key down
            inputs.Add(new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                union = new NativeMethods.INPUTUNION
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = NativeMethods.KEYEVENTF_UNICODE
                    }
                }
            });

            // Key up
            inputs.Add(new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                union = new NativeMethods.INPUTUNION
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP
                    }
                }
            });
        }

        var inputArray = inputs.ToArray();
        NativeMethods.SendInput((uint)inputArray.Length, inputArray, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
