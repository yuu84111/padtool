using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PadBridge.Core.Engine;
using PadBridge.Core.Input;
using PadBridge.Core.Mapping;
using PadBridge.Core.VirtualPad;
using CoreMouseButton = PadBridge.Core.Mapping.MouseButton;

namespace PadBridge.App;

public partial class MainWindow : Window
{
    private readonly string _profilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PadBridge",
        "profile.json");

    private readonly InputSnapshot _snapshot = new();
    private readonly DispatcherTimer _toggleTimer;
    private MappingProfile _profile = MappingProfile.CreateDefault();
    private LowLevelInputCapture? _capture;
    private ConverterEngine? _engine;
    private VigemXbox360Gamepad? _pad;
    private bool _updatingSliders;
    private bool _driverWarningShown;

    // ── Binding editor state ──
    private readonly Dictionary<GamepadTarget, TextBlock> _bindingLabels = new();
    private GamepadTarget? _capturingTarget;
    private Button? _captureButton;

    private static readonly GamepadTarget[] FaceTargets =
    {
        GamepadTarget.ButtonA, GamepadTarget.ButtonB,
        GamepadTarget.ButtonX, GamepadTarget.ButtonY
    };

    private static readonly GamepadTarget[] ShoulderTargets =
    {
        GamepadTarget.LeftShoulder, GamepadTarget.RightShoulder,
        GamepadTarget.LeftTrigger, GamepadTarget.RightTrigger
    };

    private static readonly GamepadTarget[] ThumbMenuTargets =
    {
        GamepadTarget.LeftThumb, GamepadTarget.RightThumb,
        GamepadTarget.Start, GamepadTarget.Back
    };

    private static readonly GamepadTarget[] DpadTargets =
    {
        GamepadTarget.DpadUp, GamepadTarget.DpadDown,
        GamepadTarget.DpadLeft, GamepadTarget.DpadRight
    };

    private static readonly GamepadTarget[] LStickTargets =
    {
        GamepadTarget.LeftStickUp, GamepadTarget.LeftStickDown,
        GamepadTarget.LeftStickLeft, GamepadTarget.LeftStickRight
    };

    public MainWindow()
    {
        InitializeComponent();
        try
        {
            _profile = MappingProfile.LoadOrCreate(_profilePath);
        }
        catch (Exception ex)
        {
            AppLog.Error("プロファイルの読み込みに失敗したため既定値を使います", ex);
            _profile = MappingProfile.CreateDefault();
        }

        ApplyProfileToSliders();
        PopulateAllBindingPanels();

        _toggleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _toggleTimer.Tick += (_, _) => PollToggle();
        _toggleTimer.Start();
        Loaded += (_, _) => CheckDriver();

        // Wire capture events
        PreviewKeyDown += OnWindowPreviewKeyDown;
        PreviewMouseDown += OnWindowPreviewMouseDown;
    }

    // ════════════════════════════════════════════════════════════════
    //  Driver check & virtual pad connect
    // ════════════════════════════════════════════════════════════════

    private void CheckDriver()
    {
        try
        {
            if (VigemXbox360Gamepad.TryProbeDriver(out var error))
            {
                StatusText.Text = "ViGEmBus を検出済み。仮想パッドを接続してください。";
                AppLog.Info("ViGEmBus を検出した");
                return;
            }

            ShowDriverMissing(error, showDialogOnce: true);
        }
        catch (Exception ex)
        {
            ShowDriverMissing(VigemXbox360Gamepad.FormatDriverError(ex), ex, showDialogOnce: true);
        }
    }

    private void OnConnectClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _pad?.Dispose();
            _pad = new VigemXbox360Gamepad();
            if (!_pad.TryConnect(out var error))
            {
                ShowDriverMissing(error, showDialogOnce: false);
                _pad.Dispose();
                _pad = null;
                return;
            }

            _engine?.Dispose();
            _engine = new ConverterEngine(_snapshot, _pad, _profile);
            _engine.Preview += OnPreview;
            _capture?.Dispose();
            _capture = new LowLevelInputCapture(_snapshot, () => _profile);
            _capture.Start();
            StartButton.IsEnabled = true;
            StatusText.Text = "仮想 Xbox 360 コントローラーを接続しました。Windows のコントローラー設定でも確認できます。";
            AppLog.Info("仮想パッドを接続した");
        }
        catch (Exception ex)
        {
            ShowDriverMissing(VigemXbox360Gamepad.FormatDriverError(ex), ex, showDialogOnce: false);
        }
    }

    private void ShowDriverMissing(string? message, Exception? exception = null, bool showDialogOnce = false)
    {
        var text = string.IsNullOrWhiteSpace(message)
            ? "ViGEmBusが見つかりません"
            : message;
        StatusText.Text = text;
        AppLog.Error(text, exception);
        if (showDialogOnce)
        {
            if (_driverWarningShown)
            {
                return;
            }

            _driverWarningShown = true;
        }

        MessageBox.Show(
            $"{text}\n\nアプリは起動したままです。ドライバ導入後に「仮想パッドを接続」を押してください。\nログ: {AppLog.LogFilePath}",
            "PadBridge",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    // ════════════════════════════════════════════════════════════════
    //  Conversion start / stop / toggle
    // ════════════════════════════════════════════════════════════════

    private void OnStartClick(object sender, RoutedEventArgs e) => SetConversion(true);

    private void OnStopClick(object sender, RoutedEventArgs e) => SetConversion(false);

    private void PollToggle()
    {
        if (_snapshot.ConsumeToggle())
        {
            SetConversion(!(_engine?.IsRunning ?? false));
        }
    }

    private void SetConversion(bool enabled)
    {
        if (_engine is null || _capture is null || _pad is null)
        {
            StatusText.Text = "先に仮想パッドを接続してください。";
            return;
        }

        if (enabled)
        {
            _profile = ReadProfileFromSliders();
            _profile.Save(_profilePath);
            _engine.Profile = _profile;
            _capture.ConversionActive = true;
            _engine.Start();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            StatusText.Text = "変換中。ゲームへフォーカスを移してください。停止は F8。";
        }
        else
        {
            _capture.ConversionActive = false;
            _engine.Stop();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            StatusText.Text = "変換を停止しました。仮想パッドは接続されたままです。";
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  Live preview
    // ════════════════════════════════════════════════════════════════

    private void OnPreview(GamepadState state, double hz)
    {
        Dispatcher.BeginInvoke(() =>
        {
            HzText.Text = $"ポーリング: {hz:0} Hz";
            LeftXBar.Value = state.LeftX;
            LeftYBar.Value = state.LeftY;
            RightXBar.Value = state.RightX;
            RightYBar.Value = state.RightY;
            var pressed = new List<string>();
            if (state.A) pressed.Add("A");
            if (state.B) pressed.Add("B");
            if (state.X) pressed.Add("X");
            if (state.Y) pressed.Add("Y");
            if (state.LeftShoulder) pressed.Add("LB");
            if (state.RightShoulder) pressed.Add("RB");
            if (state.LeftTrigger) pressed.Add("LT");
            if (state.RightTrigger) pressed.Add("RT");
            if (state.Start) pressed.Add("Start");
            if (state.Back) pressed.Add("Back");
            ButtonsText.Text = "ボタン: " + (pressed.Count == 0 ? "なし" : string.Join(" ", pressed));
        });
    }

    // ════════════════════════════════════════════════════════════════
    //  Settings sliders
    // ════════════════════════════════════════════════════════════════

    private void OnSettingChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || _updatingSliders)
        {
            return;
        }

        _profile = ReadProfileFromSliders();
        if (_engine is not null)
        {
            _engine.Profile = _profile;
        }

        UpdateSliderLabels();
    }

    private void ApplyProfileToSliders()
    {
        _updatingSliders = true;
        SensitivitySlider.Value = _profile.MouseSensitivity;
        CurveSlider.Value = _profile.ResponseCurveExponent;
        DeadzoneSlider.Value = _profile.AntiDeadzone;
        SmoothSlider.Value = _profile.MouseSmoothingSeconds;
        RampSlider.Value = _profile.WasdRampSeconds;
        _updatingSliders = false;
        UpdateSliderLabels();
    }

    private MappingProfile ReadProfileFromSliders()
    {
        _profile.MouseSensitivity = (float)SensitivitySlider.Value;
        _profile.ResponseCurveExponent = (float)CurveSlider.Value;
        _profile.AntiDeadzone = (float)DeadzoneSlider.Value;
        _profile.MouseSmoothingSeconds = (float)SmoothSlider.Value;
        _profile.WasdRampSeconds = (float)RampSlider.Value;
        return _profile;
    }

    private void UpdateSliderLabels()
    {
        SensitivityLabel.Text = $"感度 ({SensitivitySlider.Value:0.000})";
        CurveLabel.Text = $"レスポンスカーブ ({CurveSlider.Value:0.00})";
        DeadzoneLabel.Text = $"アンチデッドゾーン ({DeadzoneSlider.Value:0.00})";
        SmoothLabel.Text = $"スムージング ({SmoothSlider.Value * 1000:0} ms)";
        RampLabel.Text = $"WASD ランプ ({RampSlider.Value * 1000:0} ms)";
    }

    // ════════════════════════════════════════════════════════════════
    //  Profile Save / Load
    // ════════════════════════════════════════════════════════════════

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _profile = ReadProfileFromSliders();
            _profile.Save(_profilePath);
            StatusText.Text = $"プロファイルを保存しました: {_profilePath}";
            AppLog.Info("プロファイルを手動保存した");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"保存に失敗しました: {ex.Message}";
            AppLog.Error("プロファイル保存失敗", ex);
        }
    }

    private void OnLoadClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "プロファイルを読み込み",
                Filter = "JSON ファイル|*.json",
                InitialDirectory = Path.GetDirectoryName(_profilePath) ?? ""
            };
            if (dlg.ShowDialog() != true)
            {
                return;
            }

            var json = File.ReadAllText(dlg.FileName);
            var loaded = JsonSerializer.Deserialize(json, MappingJsonContext.Default.MappingProfile);
            if (loaded is null)
            {
                StatusText.Text = "プロファイルの解析に失敗しました。";
                return;
            }

            _profile = loaded;
            ApplyProfileToSliders();
            RefreshAllBindings();
            if (_engine is not null)
            {
                _engine.Profile = _profile;
            }

            StatusText.Text = $"プロファイルを読み込みました: {Path.GetFileName(dlg.FileName)}";
            AppLog.Info($"プロファイルを読み込んだ: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"読み込みに失敗しました: {ex.Message}";
            AppLog.Error("プロファイル読み込み失敗", ex);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  Binding editor — panel population
    // ════════════════════════════════════════════════════════════════

    private void PopulateAllBindingPanels()
    {
        _bindingLabels.Clear();
        PopulatePanel(FacePanel, FaceTargets);
        PopulatePanel(ShoulderPanel, ShoulderTargets);
        PopulatePanel(ThumbMenuPanel, ThumbMenuTargets);
        PopulatePanel(DpadPanel, DpadTargets);
        PopulatePanel(LStickPanel, LStickTargets);
    }

    private void PopulatePanel(UniformGrid panel, GamepadTarget[] targets)
    {
        panel.Children.Clear();
        foreach (var target in targets)
        {
            // Column 1: target display name
            var nameBlock = new TextBlock
            {
                Text = KeyNameHelper.TargetToName(target),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF8)),
                Margin = new Thickness(4, 4, 4, 4),
                FontWeight = FontWeights.SemiBold
            };

            // Column 2: current binding display
            var bindingBlock = new TextBlock
            {
                Text = GetBindingDisplayText(target),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0xC3, 0x4A)),
                Margin = new Thickness(4, 4, 4, 4),
                MinWidth = 80
            };
            _bindingLabels[target] = bindingBlock;

            // Column 3: assign + remove buttons
            var assignBtn = new Button
            {
                Content = "割り当て",
                Style = (Style)FindResource("BindBtn")
            };
            var removeBtn = new Button
            {
                Content = "解除",
                Style = (Style)FindResource("BindBtn")
            };

            var capturedTarget = target;
            assignBtn.Click += (_, _) => StartCapture(capturedTarget, assignBtn);
            removeBtn.Click += (_, _) =>
            {
                RemoveBinding(capturedTarget);
                RefreshBindingDisplay(capturedTarget);
                if (_engine is not null)
                {
                    _engine.Profile = _profile;
                }
            };

            var btnStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnStack.Children.Add(assignBtn);
            btnStack.Children.Add(removeBtn);

            panel.Children.Add(nameBlock);
            panel.Children.Add(bindingBlock);
            panel.Children.Add(btnStack);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  Binding editor — capture input
    // ════════════════════════════════════════════════════════════════

    private void StartCapture(GamepadTarget target, Button btn)
    {
        // Cancel any existing capture
        StopCapture();

        _capturingTarget = target;
        _captureButton = btn;
        btn.Content = "入力待ち…";
        btn.Background = new SolidColorBrush(Color.FromRgb(0xE1, 0x7A, 0x47));
        StatusText.Text = $"「{KeyNameHelper.TargetToName(target)}」に割り当てるキーまたはマウスボタンを押してください。Esc でキャンセル。";
    }

    private void StopCapture()
    {
        if (_captureButton is not null)
        {
            _captureButton.Content = "割り当て";
            _captureButton.Background = new SolidColorBrush(Color.FromRgb(0x35, 0x3B, 0x48));
        }

        _capturingTarget = null;
        _captureButton = null;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_capturingTarget is null)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Escape cancels capture
        if (key == Key.Escape)
        {
            StopCapture();
            StatusText.Text = "割り当てをキャンセルしました。";
            e.Handled = true;
            return;
        }

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
        {
            return;
        }

        var target = _capturingTarget.Value;
        AssignKeyboardBinding(vk, target);
        StopCapture();
        RefreshAllBindings();
        StatusText.Text = $"「{KeyNameHelper.TargetToName(target)}」に {KeyNameHelper.VirtualKeyToName(vk)} を割り当てました。";
        if (_engine is not null)
        {
            _engine.Profile = _profile;
        }

        e.Handled = true;
    }

    private void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_capturingTarget is null)
        {
            return;
        }

        CoreMouseButton? mb = e.ChangedButton switch
        {
            System.Windows.Input.MouseButton.Left => CoreMouseButton.Left,
            System.Windows.Input.MouseButton.Right => CoreMouseButton.Right,
            System.Windows.Input.MouseButton.Middle => CoreMouseButton.Middle,
            System.Windows.Input.MouseButton.XButton1 => CoreMouseButton.X1,
            System.Windows.Input.MouseButton.XButton2 => CoreMouseButton.X2,
            _ => null
        };

        if (mb is null)
        {
            return;
        }

        var target = _capturingTarget.Value;
        AssignMouseBinding(mb.Value, target);
        StopCapture();
        RefreshAllBindings();
        StatusText.Text = $"「{KeyNameHelper.TargetToName(target)}」に {KeyNameHelper.MouseButtonToName(mb.Value)} を割り当てました。";
        if (_engine is not null)
        {
            _engine.Profile = _profile;
        }

        e.Handled = true;
    }

    // ════════════════════════════════════════════════════════════════
    //  Binding editor — assign / remove / refresh
    // ════════════════════════════════════════════════════════════════

    private void AssignKeyboardBinding(int vk, GamepadTarget target)
    {
        // Remove all existing bindings for this target
        RemoveBinding(target);
        // Remove any prior mapping for the same key
        _profile.KeyboardBindings.Remove(vk);
        // Assign
        _profile.KeyboardBindings[vk] = target;
    }

    private void AssignMouseBinding(CoreMouseButton mb, GamepadTarget target)
    {
        // Remove all existing bindings for this target
        RemoveBinding(target);
        // Remove any prior mapping for the same mouse button
        _profile.MouseBindings.Remove(mb);
        // Assign
        _profile.MouseBindings[mb] = target;
    }

    private void RemoveBinding(GamepadTarget target)
    {
        var keysToRemove = _profile.KeyboardBindings
            .Where(kvp => kvp.Value == target)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var k in keysToRemove)
        {
            _profile.KeyboardBindings.Remove(k);
        }

        var mouseToRemove = _profile.MouseBindings
            .Where(kvp => kvp.Value == target)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var m in mouseToRemove)
        {
            _profile.MouseBindings.Remove(m);
        }
    }

    private string GetBindingDisplayText(GamepadTarget target)
    {
        var parts = new List<string>();
        foreach (var kvp in _profile.KeyboardBindings)
        {
            if (kvp.Value == target)
            {
                parts.Add(KeyNameHelper.VirtualKeyToName(kvp.Key));
            }
        }

        foreach (var kvp in _profile.MouseBindings)
        {
            if (kvp.Value == target)
            {
                parts.Add(KeyNameHelper.MouseButtonToName(kvp.Key));
            }
        }

        return parts.Count == 0 ? "未設定" : string.Join(", ", parts);
    }

    private void RefreshBindingDisplay(GamepadTarget target)
    {
        if (_bindingLabels.TryGetValue(target, out var block))
        {
            block.Text = GetBindingDisplayText(target);
        }
    }

    private void RefreshAllBindings()
    {
        foreach (var target in _bindingLabels.Keys)
        {
            RefreshBindingDisplay(target);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ════════════════════════════════════════════════════════════════

    private void OnClosed(object? sender, EventArgs e)
    {
        _toggleTimer.Stop();
        _capture?.Dispose();
        _engine?.Dispose();
        try
        {
            _profile.Save(_profilePath);
        }
        catch
        {
            // ignored
        }
    }
}
