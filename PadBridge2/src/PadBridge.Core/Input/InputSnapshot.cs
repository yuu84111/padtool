using PadBridge.Core.Mapping;

namespace PadBridge.Core.Input;

public sealed class InputSnapshot
{
    private readonly object _sync = new();
    private readonly HashSet<int> _keys = [];
    private readonly HashSet<MouseButton> _mouseButtons = [];
    private float _mouseDx;
    private float _mouseDy;
    private volatile bool _toggleRequested;

    public void SetKey(int vk, bool down)
    {
        lock (_sync)
        {
            if (down) _keys.Add(vk);
            else _keys.Remove(vk);
        }
    }

    public void SetMouseButton(MouseButton button, bool down)
    {
        lock (_sync)
        {
            if (down) _mouseButtons.Add(button);
            else _mouseButtons.Remove(button);
        }
    }

    public void AddMouseDelta(int dx, int dy)
    {
        lock (_sync)
        {
            _mouseDx += dx;
            _mouseDy += dy;
        }
    }

    public void RequestToggle() => _toggleRequested = true;

    public bool ConsumeToggle()
    {
        if (!_toggleRequested)
        {
            return false;
        }

        _toggleRequested = false;
        return true;
    }

    public Frame ConsumeFrame()
    {
        lock (_sync)
        {
            var keys = _keys.ToHashSet();
            var mouse = _mouseButtons.ToHashSet();
            var dx = _mouseDx;
            var dy = _mouseDy;
            _mouseDx = 0f;
            _mouseDy = 0f;
            return new Frame(keys, mouse, dx, dy);
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _keys.Clear();
            _mouseButtons.Clear();
            _mouseDx = 0f;
            _mouseDy = 0f;
        }
    }

    public readonly record struct Frame(
        HashSet<int> Keys,
        HashSet<MouseButton> MouseButtons,
        float MouseDx,
        float MouseDy);
}
