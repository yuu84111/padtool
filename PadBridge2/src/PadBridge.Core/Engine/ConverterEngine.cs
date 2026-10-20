using System.Diagnostics;
using PadBridge.Core.Conversion;
using PadBridge.Core.Input;
using PadBridge.Core.Mapping;
using PadBridge.Core.Native;
using PadBridge.Core.VirtualPad;

namespace PadBridge.Core.Engine;

public sealed class ConverterEngine : IDisposable
{
    private readonly InputSnapshot _snapshot;
    private readonly IVirtualGamepad _pad;
    private readonly MouseToStickConverter _mouse = new();
    private readonly WasdToStickConverter _wasd = new();
    private readonly Stopwatch _clock = new();
    private MappingProfile _profile;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private GamepadState _lastState;

    public ConverterEngine(InputSnapshot snapshot, IVirtualGamepad pad, MappingProfile profile)
    {
        _snapshot = snapshot;
        _pad = pad;
        _profile = profile;
    }

    public bool IsRunning { get; private set; }
    public double ActualHz { get; private set; }
    public GamepadState LastState => _lastState;
    public event Action<GamepadState, double>? Preview;

    public MappingProfile Profile
    {
        get => _profile;
        set => _profile = value;
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        if (!_pad.IsConnected)
        {
            _pad.Connect();
        }

        _mouse.Reset();
        _wasd.Reset();
        _cts = new CancellationTokenSource();
        IsRunning = true;
        NativeMethods.timeBeginPeriod(1);
        _loop = Task.Factory.StartNew(() => Run(_cts.Token), _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public void Stop()
    {
        IsRunning = false;
        _cts?.Cancel();
        try
        {
            _loop?.Wait(500);
        }
        catch (AggregateException)
        {
            // cancelled
        }

        NativeMethods.timeEndPeriod(1);
        _mouse.Reset();
        _wasd.Reset();
        _snapshot.Reset();
        if (_pad.IsConnected)
        {
            _pad.Apply(default);
        }
    }

    public void Dispose()
    {
        Stop();
        _pad.Dispose();
        _cts?.Dispose();
    }

    private void Run(CancellationToken token)
    {
        var interval = Stopwatch.Frequency / Math.Max(1, _profile.PollRateHz);
        _clock.Restart();
        var next = _clock.ElapsedTicks;
        var lastTick = next;
        var lastPreview = 0L;
        var ticks = 0;
        var hzWindowStart = _clock.ElapsedTicks;

        while (!token.IsCancellationRequested)
        {
            var now = _clock.ElapsedTicks;
            if (now < next)
            {
                var remainingMs = (next - now) * 1000.0 / Stopwatch.Frequency;
                if (remainingMs > 0.4)
                {
                    Thread.Sleep(0);
                }
                else
                {
                    Thread.SpinWait(8);
                }

                continue;
            }

            // 公称値ではなく実経過時間を使う。遅延したティックで溜まった移動量を 1ms 相当で割ると速度が跳ねる。
            var dt = (now - lastTick) / (double)Stopwatch.Frequency;
            lastTick = now;
            next += interval;
            if (now - next > interval * 4)
            {
                next = now;
            }

            Tick((float)dt);
            ticks++;

            if (now - lastPreview > Stopwatch.Frequency / 30)
            {
                lastPreview = now;
                Preview?.Invoke(_lastState, ActualHz);
            }

            if (now - hzWindowStart >= Stopwatch.Frequency)
            {
                ActualHz = ticks / ((now - hzWindowStart) / (double)Stopwatch.Frequency);
                ticks = 0;
                hzWindowStart = now;
            }
        }
    }

    private void Tick(float dt)
    {
        var profile = _profile;
        var frame = _snapshot.ConsumeFrame();
        var (lx, ly) = _wasd.Update(
            IsBound(frame, GamepadTarget.LeftStickUp),
            IsBound(frame, GamepadTarget.LeftStickDown),
            IsBound(frame, GamepadTarget.LeftStickLeft),
            IsBound(frame, GamepadTarget.LeftStickRight),
            dt,
            profile);
        var (rx, ry) = _mouse.Update(frame.MouseDx, frame.MouseDy, dt, profile);

        var state = new GamepadState(
            lx,
            ly,
            rx,
            ry,
            IsBound(frame, GamepadTarget.ButtonA),
            IsBound(frame, GamepadTarget.ButtonB),
            IsBound(frame, GamepadTarget.ButtonX),
            IsBound(frame, GamepadTarget.ButtonY),
            IsBound(frame, GamepadTarget.LeftShoulder),
            IsBound(frame, GamepadTarget.RightShoulder),
            IsBound(frame, GamepadTarget.LeftThumb),
            IsBound(frame, GamepadTarget.RightThumb),
            IsBound(frame, GamepadTarget.Start),
            IsBound(frame, GamepadTarget.Back),
            IsBound(frame, GamepadTarget.DpadUp),
            IsBound(frame, GamepadTarget.DpadDown),
            IsBound(frame, GamepadTarget.DpadLeft),
            IsBound(frame, GamepadTarget.DpadRight),
            IsBound(frame, GamepadTarget.LeftTrigger),
            IsBound(frame, GamepadTarget.RightTrigger));

        _lastState = state;
        _pad.Apply(state);
    }

    private bool IsBound(InputSnapshot.Frame frame, GamepadTarget target)
    {
        foreach (var pair in _profile.KeyboardBindings)
        {
            if (pair.Value == target && frame.Keys.Contains(pair.Key))
            {
                return true;
            }
        }

        foreach (var pair in _profile.MouseBindings)
        {
            if (pair.Value == target && frame.MouseButtons.Contains(pair.Key))
            {
                return true;
            }
        }

        return false;
    }
}
