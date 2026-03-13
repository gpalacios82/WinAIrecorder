using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WinAiRecorder.Helpers;

namespace WinAiRecorder.Services;

public class HotkeyService : IDisposable
{
    private const int HotkeyId = 9001;
    private HwndSource? _hwndSource;
    private bool _registered;

    public event Action? HotkeyPressed;

    public void Register(Window window, string hotkeyString)
    {
        Unregister();

        if (!TryParseHotkey(hotkeyString, out var modifiers, out var vk))
            throw new ArgumentException($"Invalid hotkey: {hotkeyString}");

        var helper = new WindowInteropHelper(window);
        _hwndSource = HwndSource.FromHwnd(helper.EnsureHandle());
        _hwndSource.AddHook(WndProc);

        if (!NativeMethods.RegisterHotKey(_hwndSource.Handle, HotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, vk))
        {
            int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to register hotkey '{hotkeyString}'. Error: {err}. The hotkey may already be in use by another application.");
        }
        _registered = true;
    }

    public void Unregister()
    {
        if (_registered && _hwndSource != null)
        {
            NativeMethods.UnregisterHotKey(_hwndSource.Handle, HotkeyId);
            _registered = false;
        }
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public static bool TryParseHotkey(string hotkey, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(hotkey)) return false;

        var parts = hotkey.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        foreach (var part in parts)
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= NativeMethods.MOD_CONTROL;
                    break;
                case "SHIFT":
                    modifiers |= NativeMethods.MOD_SHIFT;
                    break;
                case "ALT":
                    modifiers |= NativeMethods.MOD_ALT;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= NativeMethods.MOD_WIN;
                    break;
                default:
                    // Try to parse as a Key enum
                    if (Enum.TryParse<Key>(part, true, out var key))
                    {
                        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    }
                    else
                    {
                        // Single character
                        if (part.Length == 1)
                        {
                            vk = (uint)char.ToUpper(part[0]);
                        }
                        else
                        {
                            return false;
                        }
                    }
                    break;
            }
        }

        return vk != 0;
    }

    public void Dispose()
    {
        Unregister();
    }
}
