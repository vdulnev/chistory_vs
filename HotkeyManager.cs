using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace CHistory_VS;

public class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9001;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _hwndSource;
    private IntPtr _hwnd;

    public event EventHandler? HotkeyPressed;

    public void Attach(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.Handle;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource.AddHook(WndProc);
    }

    public bool Register(ModifierKeys modifiers, Key key)
    {
        if (_hwnd == IntPtr.Zero) return false;
        UnregisterHotKey(_hwnd, HOTKEY_ID);
        uint mod = ToWin32Modifiers(modifiers) | MOD_NOREPEAT;
        int vk = KeyInterop.VirtualKeyFromKey(key);
        return RegisterHotKey(_hwnd, HOTKEY_ID, mod, (uint)vk);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static uint ToWin32Modifiers(ModifierKeys modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= 0x0001;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= 0x0002;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= 0x0004;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= 0x0008;
        return result;
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
            UnregisterHotKey(_hwnd, HOTKEY_ID);
        _hwndSource?.RemoveHook(WndProc);
    }
}
