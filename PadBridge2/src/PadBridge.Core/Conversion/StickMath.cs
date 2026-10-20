namespace PadBridge.Core.Conversion;

public static class StickMath
{
    public static (float X, float Y) ClampToUnitCircle(float x, float y)
    {
        var mag = MathF.Sqrt(x * x + y * y);
        if (mag > 1f)
        {
            x /= mag;
            y /= mag;
        }

        return (x, y);
    }

    public static float ApplyResponseCurve(float value, float exponent)
    {
        if (value == 0f || exponent <= 0f)
        {
            return value;
        }

        var sign = MathF.Sign(value);
        return sign * MathF.Pow(MathF.Abs(value), exponent);
    }

    public static (float X, float Y) ApplyRadialAntiDeadzone(float x, float y, float inner, float outer)
    {
        var mag = MathF.Sqrt(x * x + y * y);
        if (mag < 0.0001f)
        {
            return (0f, 0f);
        }

        inner = Math.Clamp(inner, 0f, 0.50f);
        outer = Math.Clamp(1f - outer, inner + 0.01f, 1f);
        var scaled = inner + (mag * (outer - inner));
        scaled = Math.Clamp(scaled, 0f, 1f);
        var factor = scaled / mag;
        return (x * factor, y * factor);
    }

    public static short ToAxis(float value)
    {
        var scaled = (int)MathF.Round(Math.Clamp(value, -1f, 1f) * 32767f);
        return (short)Math.Clamp(scaled, -32767, 32767);
    }

    public static byte ToTrigger(bool pressed) => pressed ? (byte)255 : (byte)0;

    public static float MoveTowards(float current, float target, float maxDelta)
    {
        var delta = target - current;
        if (MathF.Abs(delta) <= maxDelta)
        {
            return target;
        }

        return current + MathF.Sign(delta) * maxDelta;
    }
}
