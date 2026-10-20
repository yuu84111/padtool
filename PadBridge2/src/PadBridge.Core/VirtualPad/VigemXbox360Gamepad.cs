using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using PadBridge.Core.Conversion;

namespace PadBridge.Core.VirtualPad;

public sealed class VigemXbox360Gamepad : IVirtualGamepad
{
    private ViGEmClient? _client;
    private IXbox360Controller? _controller;

    public bool IsConnected => _controller is not null;

    public static bool TryProbeDriver(out string? error)
    {
        try
        {
            using var client = new ViGEmClient();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = FormatDriverError(ex);
            return false;
        }
    }

    public bool TryConnect(out string? error)
    {
        error = null;
        if (_controller is not null)
        {
            return true;
        }

        try
        {
            _client = new ViGEmClient();
            _controller = _client.CreateXbox360Controller();
            _controller.AutoSubmitReport = false;
            _controller.Connect();
            return true;
        }
        catch (Exception ex)
        {
            Dispose();
            error = FormatDriverError(ex);
            return false;
        }
    }

    public void Connect()
    {
        if (!TryConnect(out var error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public static string FormatDriverError(Exception ex)
    {
        if (ex is VigemBusNotFoundException
            || ex is DllNotFoundException
            || ex is TypeInitializationException
            || LooksLikeMissingBus(ex))
        {
            return "ViGEmBusが見つかりません。ドライバをインストールしてから再接続してください。";
        }

        return $"仮想コントローラーの初期化に失敗しました: {ex.Message}";
    }

    private static bool LooksLikeMissingBus(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is VigemBusNotFoundException || current is DllNotFoundException)
            {
                return true;
            }

            var text = current.Message ?? string.Empty;
            if (text.Contains("ViGEm", StringComparison.OrdinalIgnoreCase)
                || text.Contains("VigemBus", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void Disconnect() => Dispose();

    public void Apply(in GamepadState state)
    {
        if (_controller is null)
        {
            return;
        }

        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, StickMath.ToAxis(state.LeftX));
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, StickMath.ToAxis(state.LeftY));
        _controller.SetAxisValue(Xbox360Axis.RightThumbX, StickMath.ToAxis(state.RightX));
        _controller.SetAxisValue(Xbox360Axis.RightThumbY, StickMath.ToAxis(state.RightY));
        _controller.SetButtonState(Xbox360Button.A, state.A);
        _controller.SetButtonState(Xbox360Button.B, state.B);
        _controller.SetButtonState(Xbox360Button.X, state.X);
        _controller.SetButtonState(Xbox360Button.Y, state.Y);
        _controller.SetButtonState(Xbox360Button.LeftShoulder, state.LeftShoulder);
        _controller.SetButtonState(Xbox360Button.RightShoulder, state.RightShoulder);
        _controller.SetButtonState(Xbox360Button.LeftThumb, state.LeftThumb);
        _controller.SetButtonState(Xbox360Button.RightThumb, state.RightThumb);
        _controller.SetButtonState(Xbox360Button.Start, state.Start);
        _controller.SetButtonState(Xbox360Button.Back, state.Back);
        _controller.SetButtonState(Xbox360Button.Up, state.DpadUp);
        _controller.SetButtonState(Xbox360Button.Down, state.DpadDown);
        _controller.SetButtonState(Xbox360Button.Left, state.DpadLeft);
        _controller.SetButtonState(Xbox360Button.Right, state.DpadRight);
        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, StickMath.ToTrigger(state.LeftTrigger));
        _controller.SetSliderValue(Xbox360Slider.RightTrigger, StickMath.ToTrigger(state.RightTrigger));
        _controller.SubmitReport();
    }

    public void Dispose()
    {
        try
        {
            _controller?.Disconnect();
        }
        catch
        {
            // ignored
        }

        _controller = null;
        _client?.Dispose();
        _client = null;
    }
}
