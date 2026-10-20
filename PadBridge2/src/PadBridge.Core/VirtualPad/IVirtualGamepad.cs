namespace PadBridge.Core.VirtualPad;

public interface IVirtualGamepad : IDisposable
{
    bool IsConnected { get; }
    void Connect();
    void Disconnect();
    void Apply(in GamepadState state);
}

public readonly record struct GamepadState(
    float LeftX,
    float LeftY,
    float RightX,
    float RightY,
    bool A,
    bool B,
    bool X,
    bool Y,
    bool LeftShoulder,
    bool RightShoulder,
    bool LeftThumb,
    bool RightThumb,
    bool Start,
    bool Back,
    bool DpadUp,
    bool DpadDown,
    bool DpadLeft,
    bool DpadRight,
    bool LeftTrigger,
    bool RightTrigger);
