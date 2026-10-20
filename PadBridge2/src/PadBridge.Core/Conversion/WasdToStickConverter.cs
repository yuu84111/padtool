using PadBridge.Core.Mapping;

namespace PadBridge.Core.Conversion;

public sealed class WasdToStickConverter
{
    private float _x;
    private float _y;

    public (float X, float Y) Update(bool up, bool down, bool left, bool right, float dtSeconds, MappingProfile profile)
    {
        var targetX = 0f;
        var targetY = 0f;
        if (right) targetX += 1f;
        if (left) targetX -= 1f;
        if (up) targetY += 1f;
        if (down) targetY -= 1f;
        (targetX, targetY) = StickMath.ClampToUnitCircle(targetX, targetY);

        var ramp = MathF.Max(profile.WasdRampSeconds, 0.001f);
        var maxDelta = dtSeconds / ramp;
        _x = StickMath.MoveTowards(_x, targetX, maxDelta);
        _y = StickMath.MoveTowards(_y, targetY, maxDelta);

        var (x, y) = StickMath.ClampToUnitCircle(_x, _y);
        if (MathF.Abs(x) < 0.0001f && MathF.Abs(y) < 0.0001f)
        {
            return (0f, 0f);
        }

        return StickMath.ApplyRadialAntiDeadzone(x, y, profile.AntiDeadzone, profile.OuterDeadzone);
    }

    public void Reset()
    {
        _x = 0f;
        _y = 0f;
    }
}
