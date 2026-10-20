using System.Text.Json;
using System.Text.Json.Serialization;

namespace PadBridge.Core.Mapping;

public sealed class MappingProfile
{
    public string Name { get; set; } = "Default";

    /// <summary>プロファイル形式のバージョン。古い設定ファイルの自動マイグレーションに使う。</summary>
    public const int CurrentVersion = 4;

    public int Version { get; set; }

    /// <summary>
    /// マウス感度の倍率（1.0 が基準）。0.5 で半分、2.0 で倍の速さ。実用範囲は 0.25〜4.0 程度。
    /// 内部で 0.05〜20 にクランプされる。
    /// </summary>
    public float MouseSensitivity { get; set; } = 1.0f;

    /// <summary>スティックの応答カーブ。1=線形、2前後で微調整しやすくなる。</summary>
    public float ResponseCurveExponent { get; set; } = 1.4f;

    /// <summary>ゲーム側デッドゾーンを打ち消す最小出力（0〜0.4 程度）。</summary>
    public float AntiDeadzone { get; set; } = 0.18f;

    /// <summary>外周でクリップする割合。</summary>
    public float OuterDeadzone { get; set; } = 0.02f;

    /// <summary>マウス停止後に右スティックが中立へ戻る時定数（秒）。0 に近いほど即時。</summary>
    public float MouseSmoothingSeconds { get; set; } = 0.018f;

    /// <summary>WASD が目標方向へ到達する時間（秒）。瞬時 0/1 を避ける。</summary>
    public float WasdRampSeconds { get; set; } = 0.035f;

    public int PollRateHz { get; set; } = 1000;

    public int ToggleVirtualKey { get; set; } = 0x77; // F8

    public Dictionary<int, GamepadTarget> KeyboardBindings { get; set; } = DefaultKeyboardBindings();

    public Dictionary<MouseButton, GamepadTarget> MouseBindings { get; set; } = DefaultMouseBindings();

    public static MappingProfile CreateDefault() => new() { Version = CurrentVersion };

    public static Dictionary<int, GamepadTarget> DefaultKeyboardBindings() => new()
    {
        [0x57] = GamepadTarget.LeftStickUp,    // W
        [0x41] = GamepadTarget.LeftStickLeft,  // A
        [0x53] = GamepadTarget.LeftStickDown,  // S
        [0x44] = GamepadTarget.LeftStickRight, // D
        [0x20] = GamepadTarget.ButtonA,        // Space
        [0x43] = GamepadTarget.ButtonB,        // C
        [0x52] = GamepadTarget.ButtonX,        // R
        [0x46] = GamepadTarget.ButtonY,        // F
        [0x10] = GamepadTarget.LeftShoulder,   // Shift
        [0x11] = GamepadTarget.LeftThumb,      // Ctrl
        [0x51] = GamepadTarget.LeftShoulder,   // Q
        [0x45] = GamepadTarget.RightShoulder,  // E
        [0x09] = GamepadTarget.Back,           // Tab
        [0x0D] = GamepadTarget.Start,          // Enter
        [0x26] = GamepadTarget.DpadUp,
        [0x28] = GamepadTarget.DpadDown,
        [0x25] = GamepadTarget.DpadLeft,
        [0x27] = GamepadTarget.DpadRight
    };

    public static Dictionary<MouseButton, GamepadTarget> DefaultMouseBindings() => new()
    {
        [MouseButton.Left] = GamepadTarget.RightTrigger,
        [MouseButton.Right] = GamepadTarget.LeftTrigger,
        [MouseButton.Middle] = GamepadTarget.RightThumb
    };

    public static MappingProfile LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var created = CreateDefault();
            created.Save(path);
            return created;
        }

        var json = File.ReadAllText(path);
        var profile = JsonSerializer.Deserialize(json, MappingJsonContext.Default.MappingProfile) ?? CreateDefault();

        // v4: マウス差分の取得バグ修正に伴い感度のスケールを再定義した。旧値は引き継がず 1.0 に戻す。
        if (profile.Version < CurrentVersion)
        {
            profile.MouseSensitivity = new MappingProfile().MouseSensitivity;
            profile.Version = CurrentVersion;
            profile.Save(path);
        }

        return profile;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(this, MappingJsonContext.Default.MappingProfile);
        File.WriteAllText(path, json);
    }
}

public enum MouseButton
{
    Left,
    Right,
    Middle,
    X1,
    X2
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(MappingProfile))]
public partial class MappingJsonContext : JsonSerializerContext;
