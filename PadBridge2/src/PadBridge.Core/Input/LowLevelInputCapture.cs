using System.Runtime.InteropServices;
using PadBridge.Core.Mapping;
using PadBridge.Core.Native;

namespace PadBridge.Core.Input;

/// <summary>
/// 低レベルフックで KBM を取得し、変換中はゲームへ生入力が届かないよう消費する。
/// フックはメッセージループのあるスレッド（WPF UI スレッド）で開始すること。
/// </summary>
public sealed class LowLevelInputCapture : IDisposable
{
    private readonly InputSnapshot _snapshot;
    private readonly Func<MappingProfile> _profile;
    private readonly NativeMethods.HookProc _keyboardProc;
    private readonly NativeMethods.HookProc _mouseProc;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private int _centerX;
    private int _centerY;
    private int _cursorHidden;

    public LowLevelInputCapture(InputSnapshot snapshot, Func<MappingProfile> profile)
    {
        _snapshot = snapshot;
        _profile = profile;
        _keyboardProc = KeyboardHook;
        _mouseProc = MouseHook;
    }

    public bool IsCapturing { get; private set; }

    private bool _conversionActive;
    public bool ConversionActive
    {
        get => _conversionActive;
        set
        {
            _conversionActive = value;
            if (value)
            {
                RecenterCursor();
            }
            else
            {
                ShowCursorIfHidden();
                _snapshot.Reset();
            }
        }
    }

    public void Start()
    {
        if (IsCapturing)
        {
            return;
        }

        var module = NativeMethods.GetModuleHandle(null);
        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _keyboardProc, module, 0);
        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _mouseProc, module, 0);
        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            Stop();
            throw new InvalidOperationException("低レベル入力フックのインストールに失敗しました。");
        }

        RecenterCursor();
        IsCapturing = true;
    }

    public void Stop()
    {
        ConversionActive = false;
        IsCapturing = false;
        ShowCursorIfHidden();
        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        _snapshot.Reset();
    }

    public void Dispose() => Stop();

    private IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
            if ((info.Flags & NativeMethods.LlKhfInjected) == 0)
            {
                var vk = (int)info.VkCode;
                var message = wParam.ToInt32();
                var down = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;

                if (vk == _profile().ToggleVirtualKey && down)
                {
                    _snapshot.RequestToggle();
                    return 1;
                }

                if (ConversionActive)
                {
                    _snapshot.SetKey(vk, down);
                    if (_profile().KeyboardBindings.ContainsKey(vk))
                    {
                        return 1;
                    }
                }
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<NativeMethods.MsLlHookStruct>(lParam);
            if ((info.Flags & NativeMethods.LlMhfInjected) == 0 && ConversionActive)
            {
                var message = wParam.ToInt32();
                if (message == NativeMethods.WmMouseMove)
                {
                    // 返り値 1 でイベントを握りつぶしているため、システム上のカーソルは動かない。
                    // Pt は「現在のカーソル位置 + 今回の移動量」なので、移動量は現在位置との差で取る。
                    // （前回の Pt との差を取ると d_k - d_(k-1) になり、等速移動が 0、減速が逆向きの入力になる）
                    if (NativeMethods.GetCursorPos(out var cursor))
                    {
                        var dx = info.Pt.X - cursor.X;
                        var dy = info.Pt.Y - cursor.Y;
                        if (dx != 0 || dy != 0)
                        {
                            _snapshot.AddMouseDelta(dx, dy);
                        }

                        // 他アプリ等でカーソルが動かされた場合のみ中央へ戻す。
                        if (Math.Abs(cursor.X - _centerX) > 40 || Math.Abs(cursor.Y - _centerY) > 40)
                        {
                            NativeMethods.SetCursorPos(_centerX, _centerY);
                        }
                    }

                    HideCursorOnce();
                    return 1;
                }

                if (TryMapMouseButton(message, info.MouseData, out var button, out var down))
                {
                    _snapshot.SetMouseButton(button, down);
                    if (_profile().MouseBindings.ContainsKey(button))
                    {
                        return 1;
                    }
                }
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private static bool TryMapMouseButton(int message, uint mouseData, out MouseButton button, out bool down)
    {
        button = MouseButton.Left;
        down = false;
        switch (message)
        {
            case NativeMethods.WmLButtonDown:
                button = MouseButton.Left; down = true; return true;
            case NativeMethods.WmLButtonUp:
                button = MouseButton.Left; down = false; return true;
            case NativeMethods.WmRButtonDown:
                button = MouseButton.Right; down = true; return true;
            case NativeMethods.WmRButtonUp:
                button = MouseButton.Right; down = false; return true;
            case NativeMethods.WmMButtonDown:
                button = MouseButton.Middle; down = true; return true;
            case NativeMethods.WmMButtonUp:
                button = MouseButton.Middle; down = false; return true;
            case NativeMethods.WmXButtonDown:
            case NativeMethods.WmXButtonUp:
                down = message == NativeMethods.WmXButtonDown;
                button = ((mouseData >> 16) & 0xFFFF) == 2 ? MouseButton.X2 : MouseButton.X1;
                return true;
            default:
                return false;
        }
    }

    private void RecenterCursor()
    {
        _centerX = NativeMethods.GetSystemMetrics(0) / 2;
        _centerY = NativeMethods.GetSystemMetrics(1) / 2;
        NativeMethods.SetCursorPos(_centerX, _centerY);
    }

    private void HideCursorOnce()
    {
        if (_cursorHidden != 0)
        {
            return;
        }

        NativeMethods.ShowCursor(false);
        _cursorHidden = 1;
    }

    private void ShowCursorIfHidden()
    {
        if (_cursorHidden == 0)
        {
            return;
        }

        NativeMethods.ShowCursor(true);
        _cursorHidden = 0;
    }
}
