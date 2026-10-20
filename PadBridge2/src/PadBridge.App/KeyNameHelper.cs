using PadBridge.Core.Mapping;

namespace PadBridge.App;

/// <summary>
/// 仮想キーコード→表示名、GamepadTarget→表示名の変換ヘルパー。
/// </summary>
public static class KeyNameHelper
{
    private static readonly Dictionary<int, string> VkNames = new()
    {
        [0x01] = "左クリック", [0x02] = "右クリック", [0x04] = "中クリック",
        [0x05] = "X1", [0x06] = "X2",
        [0x08] = "Backspace", [0x09] = "Tab", [0x0D] = "Enter", [0x10] = "Shift",
        [0x11] = "Ctrl", [0x12] = "Alt", [0x13] = "Pause", [0x14] = "CapsLock",
        [0x1B] = "Esc", [0x20] = "Space",
        [0x21] = "PageUp", [0x22] = "PageDown", [0x23] = "End", [0x24] = "Home",
        [0x25] = "←", [0x26] = "↑", [0x27] = "→", [0x28] = "↓",
        [0x2C] = "PrintScreen", [0x2D] = "Insert", [0x2E] = "Delete",
        [0x30] = "0", [0x31] = "1", [0x32] = "2", [0x33] = "3", [0x34] = "4",
        [0x35] = "5", [0x36] = "6", [0x37] = "7", [0x38] = "8", [0x39] = "9",
        [0x41] = "A", [0x42] = "B", [0x43] = "C", [0x44] = "D", [0x45] = "E",
        [0x46] = "F", [0x47] = "G", [0x48] = "H", [0x49] = "I", [0x4A] = "J",
        [0x4B] = "K", [0x4C] = "L", [0x4D] = "M", [0x4E] = "N", [0x4F] = "O",
        [0x50] = "P", [0x51] = "Q", [0x52] = "R", [0x53] = "S", [0x54] = "T",
        [0x55] = "U", [0x56] = "V", [0x57] = "W", [0x58] = "X", [0x59] = "Y",
        [0x5A] = "Z",
        [0x60] = "Num0", [0x61] = "Num1", [0x62] = "Num2", [0x63] = "Num3",
        [0x64] = "Num4", [0x65] = "Num5", [0x66] = "Num6", [0x67] = "Num7",
        [0x68] = "Num8", [0x69] = "Num9",
        [0x6A] = "Num*", [0x6B] = "Num+", [0x6D] = "Num-", [0x6E] = "Num.", [0x6F] = "Num/",
        [0x70] = "F1", [0x71] = "F2", [0x72] = "F3", [0x73] = "F4",
        [0x74] = "F5", [0x75] = "F6", [0x76] = "F7", [0x77] = "F8",
        [0x78] = "F9", [0x79] = "F10", [0x7A] = "F11", [0x7B] = "F12",
        [0xA0] = "LShift", [0xA1] = "RShift", [0xA2] = "LCtrl", [0xA3] = "RCtrl",
        [0xA4] = "LAlt", [0xA5] = "RAlt",
        [0xBA] = ";", [0xBB] = "=", [0xBC] = ",", [0xBD] = "-", [0xBE] = ".", [0xBF] = "/",
        [0xC0] = "`", [0xDB] = "[", [0xDC] = "\\", [0xDD] = "]", [0xDE] = "'"
    };

    public static string VirtualKeyToName(int vk)
        => VkNames.TryGetValue(vk, out var name) ? name : $"0x{vk:X2}";

    public static string MouseButtonToName(MouseButton mb) => mb switch
    {
        MouseButton.Left   => "マウス左",
        MouseButton.Right  => "マウス右",
        MouseButton.Middle => "マウス中",
        MouseButton.X1     => "マウスX1",
        MouseButton.X2     => "マウスX2",
        _                  => mb.ToString()
    };

    /// <summary>キー入力またはマウスボタンを統一した表示名で返す。</summary>
    public static string InputToName(int? vk, MouseButton? mb)
    {
        if (mb.HasValue) return MouseButtonToName(mb.Value);
        if (vk.HasValue) return VirtualKeyToName(vk.Value);
        return "未設定";
    }

    private static readonly Dictionary<GamepadTarget, string> TargetNames = new()
    {
        [GamepadTarget.None]           = "なし",
        [GamepadTarget.ButtonA]        = "A ボタン",
        [GamepadTarget.ButtonB]        = "B ボタン",
        [GamepadTarget.ButtonX]        = "X ボタン",
        [GamepadTarget.ButtonY]        = "Y ボタン",
        [GamepadTarget.LeftShoulder]   = "LB",
        [GamepadTarget.RightShoulder]  = "RB",
        [GamepadTarget.LeftTrigger]    = "LT",
        [GamepadTarget.RightTrigger]   = "RT",
        [GamepadTarget.LeftThumb]      = "左スティック押込",
        [GamepadTarget.RightThumb]     = "右スティック押込",
        [GamepadTarget.Start]          = "Start",
        [GamepadTarget.Back]           = "Back",
        [GamepadTarget.DpadUp]         = "D-Pad ↑",
        [GamepadTarget.DpadDown]       = "D-Pad ↓",
        [GamepadTarget.DpadLeft]       = "D-Pad ←",
        [GamepadTarget.DpadRight]      = "D-Pad →",
        [GamepadTarget.LeftStickUp]    = "左スティック↑",
        [GamepadTarget.LeftStickDown]  = "左スティック↓",
        [GamepadTarget.LeftStickLeft]  = "左スティック←",
        [GamepadTarget.LeftStickRight] = "左スティック→"
    };

    public static string TargetToName(GamepadTarget t)
        => TargetNames.TryGetValue(t, out var n) ? n : t.ToString();
}
