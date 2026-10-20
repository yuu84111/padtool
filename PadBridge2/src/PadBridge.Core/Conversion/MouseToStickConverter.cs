using PadBridge.Core.Mapping;

namespace PadBridge.Core.Conversion;

/// <summary>
/// マウス移動量（ピクセル）を右スティック変位へ変換する。
/// 変位は速度比例なので、マウスを止めると視点も止まる（パッドの視点操作と同じ）。
///
/// 処理の流れ（すべて float。整数演算は一切使わない）:
///   1) dt / ピクセル量のガード
///   2) 速度 (px/s) に換算し、2 段の 1 次ローパスで平滑化
///   3) MouseSensitivity を速度に乗算して正規化（1.0 = フル傾き）
///   4) 応答カーブ → ソフトニー付きアンチデッドゾーン → 外周クリップ → 最終クランプ
///
/// StickMath には依存しない（微小入力の挙動をこのファイル内で完結させるため）。
/// </summary>
public sealed class MouseToStickConverter
{
    private const float MinDt = 0.0005f;
    private const float MaxDt = 0.050f;

    /// <summary>1 サンプルで受け付けるピクセル量の上限。超えた値は異常値として捨てる。</summary>
    private const float MaxPixelsPerSample = 300f;

    /// <summary>
    /// MouseSensitivity = 1.0 のときの「1 px/s あたりのスティック変位」。
    /// 0.00015 なら約 6700 px/s（素早い振り向き）でフル傾き。全体の速さを変えたいときはここだけ。
    /// </summary>
    private const float StickPerPxPerSecAtUnitSensitivity = 0.00015f;

    private const float MinSensitivity = 0.05f;
    private const float MaxSensitivity = 20f;

    private const float SecondStageRatio = 0.5f;

    /// <summary>
    /// アンチデッドゾーンを滑らかに立ち上げる幅（カーブ後の大きさ基準）。
    /// 微小入力でも 0 → AntiDeadzone へ連続的に増えるので、1〜2px の動きが 0.0x 台で出力される。
    /// </summary>
    private const float AntiDeadzoneKnee = 0.05f;

    /// <summary>フィルタ状態（正規化速度）がこれ未満なら完全停止とみなして状態を 0 に戻す。</summary>
    private const float SnapNormalizedSpeed = 0.0001f;

    /// <summary>出力がこの値未満なら 0 にスナップ。</summary>
    private const float SnapToZero = 0.00001f;

    private float _s1X, _s1Y; // 平滑化済み速度 (px/s) 1 段目
    private float _s2X, _s2Y; // 2 段目

    public (float X, float Y) Update(float pixelDx, float pixelDy, float dtSeconds, MappingProfile profile)
    {
        // --- 1) ガード ---------------------------------------------------
        if (!float.IsFinite(dtSeconds)) dtSeconds = MinDt;
        var dt = Math.Clamp(dtSeconds, MinDt, MaxDt);

        if (!IsSane(pixelDx)) pixelDx = 0f;
        if (!IsSane(pixelDy)) pixelDy = 0f;

        // --- 2) 速度 (px/s) + 2 段ローパス ----------------------------------
        float velX = pixelDx / dt;
        float velY = -pixelDy / dt; // 画面 Y は下向き正、スティックは上向き正

        float tau = MathF.Max(profile.MouseSmoothingSeconds, 0.001f);
        float a1 = 1f - MathF.Exp(-dt / tau);
        float a2 = 1f - MathF.Exp(-dt / (tau * SecondStageRatio));

        _s1X += (velX - _s1X) * a1;
        _s1Y += (velY - _s1Y) * a1;
        _s2X += (_s1X - _s2X) * a2;
        _s2Y += (_s1Y - _s2Y) * a2;

        // --- 3) 感度を速度に乗算して正規化（カーブより前） -------------------
        float sensitivity = Math.Clamp(profile.MouseSensitivity, MinSensitivity, MaxSensitivity);
        float scale = sensitivity * StickPerPxPerSecAtUnitSensitivity;

        float nx = _s2X * scale;
        float ny = _s2Y * scale;
        float mag = MathF.Sqrt(nx * nx + ny * ny);

        // 1 段目・2 段目とも十分小さければ完全停止。残りカスを消して状態をリセット。
        float mag1 = MathF.Sqrt(_s1X * _s1X + _s1Y * _s1Y) * scale;
        if (mag < SnapNormalizedSpeed && mag1 < SnapNormalizedSpeed)
        {
            Reset();
            return (0f, 0f);
        }

        // --- 4) 整形（大きさのみ加工し、方向は保持する） ---------------------
        float dirX = nx / mag;
        float dirY = ny / mag;

        // 応答カーブ。範囲外の値は 1.0 に頭打ち。
        float m = MathF.Min(mag, 1f);
        float exponent = Math.Clamp(profile.ResponseCurveExponent, 0.5f, 4f);
        m = MathF.Pow(m, exponent);

        // ソフトニー付きアンチデッドゾーン。
        //   m が十分大きい: ad + (1-ad)*m   （従来のアンチデッドゾーンと同じ）
        //   m が微小      : 0 から連続的に立ち上がる（0 に張り付かない）
        float ad = Math.Clamp(profile.AntiDeadzone, 0f, 0.6f);
        float s = 1f - MathF.Exp(-m / AntiDeadzoneKnee);
        float lift = ad * s;
        m = lift + (1f - lift) * m;

        // 外周デッドゾーン（フル傾きに届きやすくする）。
        float outer = Math.Clamp(profile.OuterDeadzone, 0f, 0.5f);
        m = MathF.Min(m / (1f - outer), 1f);

        float outX = dirX * m;
        float outY = dirY * m;

        // 最終クランプ（後段の short 変換でのオーバーフロー＝符号反転を防ぐ）。
        if (!float.IsFinite(outX)) outX = 0f;
        if (!float.IsFinite(outY)) outY = 0f;
        outX = Math.Clamp(outX, -1f, 1f);
        outY = Math.Clamp(outY, -1f, 1f);

        if (MathF.Abs(outX) < SnapToZero) outX = 0f;
        if (MathF.Abs(outY) < SnapToZero) outY = 0f;
        return (outX, outY);
    }

    public void Reset()
    {
        _s1X = _s1Y = 0f;
        _s2X = _s2Y = 0f;
    }

    private static bool IsSane(float pixels) =>
        float.IsFinite(pixels) && MathF.Abs(pixels) <= MaxPixelsPerSample;
}
