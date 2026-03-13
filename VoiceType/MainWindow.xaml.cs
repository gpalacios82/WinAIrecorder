using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VoiceType.Helpers;
using VoiceType.Services;

namespace VoiceType;

public enum OverlayState { Idle, Recording, Processing, Success, Error }

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly AudioRecorderService _audioRecorder;
    private readonly TranscriptionService _transcriptionService;
    private readonly HotkeyService _hotkeyService;
    private readonly PasteService _pasteService;

    private OverlayState _state = OverlayState.Idle;
    private string? _lastError;
    private System.Threading.CancellationTokenSource? _transcriptionCts;
    private double _levelBarMaxWidth;
    private bool _forceClose;

    // Storyboards
    private Storyboard? _pulseAnimation;
    private Storyboard? _spinAnimation;
    private Storyboard? _successFadeAnimation;
    private Storyboard? _warnFlashAnimation;

    public MainWindow(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _audioRecorder = new AudioRecorderService();
        _transcriptionService = new TranscriptionService();
        _hotkeyService = new HotkeyService();
        _pasteService = new PasteService();

        InitializeComponent();
        SetupEventHandlers();
        SetupPosition();

        // Handle WM_SETTINGCHANGE for theme
        SourceInitialized += (s, e) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(ThemeWndProc);
        };
    }

    private void SetupEventHandlers()
    {
        _audioRecorder.AudioLevelChanged += OnAudioLevelChanged;
        _audioRecorder.RecordingCompleted += OnRecordingCompleted;
        _audioRecorder.RecordingError += OnRecordingError;
        _audioRecorder.MaxDurationWarning += OnMaxDurationWarning;
        _audioRecorder.MaxDurationReached += OnMaxDurationReached;

        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
        SizeChanged += (s, e) => _levelBarMaxWidth = LevelBar.ActualWidth > 0 ? LevelBar.ActualWidth : 76;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Get storyboards from resources
        _pulseAnimation = (Storyboard)Resources["PulseAnimation"];
        _spinAnimation = (Storyboard)Resources["SpinAnimation"];
        _successFadeAnimation = (Storyboard)Resources["SuccessFade"];
        _warnFlashAnimation = (Storyboard)Resources["WarnFlash"];

        _levelBarMaxWidth = 76;

        // Subscribe to success animation completion (once)
        if (_successFadeAnimation != null)
        {
            _successFadeAnimation.Completed += (s, e) =>
            {
                if (_state == OverlayState.Success)
                    SetState(OverlayState.Idle);
            };
        }

        // Register hotkey
        RegisterHotkey();

        // Apply always-on-top setting
        Topmost = _settingsService.Settings.AlwaysOnTop;
        UpdatePinMenuItem();
    }

    private void RegisterHotkey()
    {
        try
        {
            _hotkeyService.Register(this, _settingsService.Settings.Hotkey);
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        }
        catch (Exception ex)
        {
            SetState(OverlayState.Error, $"Hotkey: {ex.Message}");
        }
    }

    public void ReRegisterHotkey()
    {
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        try
        {
            _hotkeyService.Register(this, _settingsService.Settings.Hotkey);
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        }
        catch (Exception ex)
        {
            SetState(OverlayState.Error, $"Hotkey: {ex.Message}");
        }
    }

    private void SetupPosition()
    {
        var settings = _settingsService.Settings;
        var workArea = SystemParameters.WorkArea;

        if (settings.WindowPosition.X < 0 || settings.WindowPosition.Y < 0 ||
            settings.WindowPosition.X > workArea.Right - 50 ||
            settings.WindowPosition.Y > workArea.Bottom - 50)
        {
            // Default: bottom-right corner with 24px margin
            Left = workArea.Right - Width - 24;
            Top = workArea.Bottom - Height - 24;
        }
        else
        {
            Left = settings.WindowPosition.X;
            Top = settings.WindowPosition.Y;
        }
    }

    private void SetState(OverlayState newState, string? errorMessage = null)
    {
        _state = newState;
        _lastError = errorMessage;

        // Stop all animations
        try { _pulseAnimation?.Stop(this); } catch { }
        try { _spinAnimation?.Stop(this); } catch { }
        try { _warnFlashAnimation?.Stop(this); } catch { }

        // Reset visibility
        MicButton.Visibility = Visibility.Visible;
        SpinnerGrid.Visibility = Visibility.Collapsed;
        SuccessIcon.Visibility = Visibility.Collapsed;
        ErrorIcon.Visibility = Visibility.Collapsed;
        LevelBar.Visibility = Visibility.Collapsed;
        LevelBarTrack.Visibility = Visibility.Collapsed;
        StatusText.Text = "";

        // Update tray tooltip
        var tray = App.TrayIcon;

        switch (newState)
        {
            case OverlayState.Idle:
                MicPath.Fill = (Brush)FindResource("AccentBrush");
                if (tray != null) tray.ToolTipText = "VoiceType — Listo";
                break;

            case OverlayState.Recording:
                MicPath.Fill = (Brush)FindResource("RecordingBrush");
                LevelBarTrack.Visibility = Visibility.Visible;
                LevelBar.Visibility = Visibility.Visible;
                _pulseAnimation?.Begin(this, true);
                if (tray != null) tray.ToolTipText = "VoiceType — Grabando…";
                break;

            case OverlayState.Processing:
                MicButton.Visibility = Visibility.Collapsed;
                SpinnerGrid.Visibility = Visibility.Visible;
                _spinAnimation?.Begin(this, true);
                if (tray != null) tray.ToolTipText = "VoiceType — Procesando…";
                break;

            case OverlayState.Success:
                MicButton.Visibility = Visibility.Collapsed;
                SuccessIcon.Visibility = Visibility.Visible;
                SuccessIcon.Opacity = 1.0;
                _successFadeAnimation?.Begin(this, true);
                if (tray != null) tray.ToolTipText = "VoiceType — Listo";
                break;

            case OverlayState.Error:
                MicButton.Visibility = Visibility.Collapsed;
                ErrorIcon.Visibility = Visibility.Visible;
                ErrorPath.ToolTip = errorMessage ?? "An error occurred";
                StatusText.Text = "Error";
                if (tray != null) tray.ToolTipText = "VoiceType — Error";
                // Auto-clear after 5 seconds
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    if (_state == OverlayState.Error)
                        SetState(OverlayState.Idle);
                };
                timer.Start();
                break;
        }
    }

    private void OnHotkeyPressed()
    {
        if (!HasApiKey())
        {
            Application.Current?.Dispatcher.BeginInvoke(() => ((App)Application.Current).OpenSettings());
            return;
        }
        // Capture foreground window before we take focus
        _pasteService.CaptureForegroundWindow();
        ToggleRecording();
    }

    private void MicButton_Click(object sender, RoutedEventArgs e)
    {
        // For button click, the overlay already has focus - capture before showing
        // Actually for button click we need to capture the PREVIOUS window
        // This is already handled since overlay may not have taken focus yet
        if (_state == OverlayState.Idle)
            _pasteService.CaptureForegroundWindow();
        ToggleRecording();
    }

    private static bool HasApiKey() =>
        !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

    private void ToggleRecording()
    {
        switch (_state)
        {
            case OverlayState.Idle:
                if (!HasApiKey())
                {
                    ((App)Application.Current).OpenSettings();
                    return;
                }
                StartRecording();
                break;
            case OverlayState.Recording:
                StopRecording();
                break;
            case OverlayState.Processing:
                // Cancel processing
                _transcriptionCts?.Cancel();
                SetState(OverlayState.Idle);
                break;
            case OverlayState.Error:
                SetState(OverlayState.Idle);
                break;
        }
    }

    private void StartRecording()
    {
        _audioRecorder.StartRecording();
        SetState(OverlayState.Recording);
    }

    private void StopRecording()
    {
        _audioRecorder.StopRecording();
        SetState(OverlayState.Processing);
    }

    private void OnAudioLevelChanged(float level)
    {
        if (_state != OverlayState.Recording) return;

        double width = _levelBarMaxWidth * level;
        LevelBar.Width = Math.Max(0, Math.Min(width, _levelBarMaxWidth));
    }

    private async void OnRecordingCompleted(MemoryStream audioStream)
    {
        if (audioStream.Length <= 44) // Effectively empty (WAV header only)
        {
            audioStream.Dispose();
            SetState(OverlayState.Idle);
            return;
        }

        SetState(OverlayState.Processing);

        _transcriptionCts = new System.Threading.CancellationTokenSource();
        try
        {
            var text = await _transcriptionService.TranscribeAsync(
                audioStream,
                _settingsService.Settings.Model,
                _transcriptionCts.Token);

            if (string.IsNullOrWhiteSpace(text))
            {
                SetState(OverlayState.Idle);
                return;
            }

            SetState(OverlayState.Success);

            await _pasteService.PasteTextAsync(text, _settingsService.Settings.UseClipboardFallback);
        }
        catch (OperationCanceledException)
        {
            SetState(OverlayState.Idle);
        }
        catch (Exception ex)
        {
            SetState(OverlayState.Error, ex.Message);
        }
        finally
        {
            audioStream.Dispose();
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;
        }
    }

    private void OnRecordingError(string message)
    {
        SetState(OverlayState.Error, message);
    }

    private void OnMaxDurationWarning()
    {
        try { _warnFlashAnimation?.Begin(this, true); } catch { }
    }

    private void OnMaxDurationReached()
    {
        StopRecording();
    }

    private IntPtr ThemeWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_SETTINGCHANGE)
        {
            if (_settingsService.Settings.Theme == "auto")
            {
                var theme = ThemeHelper.GetWindowsTheme();
                App.ApplyDynamicTheme(theme);
            }
        }
        return IntPtr.Zero;
    }

    // Dragging
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        OverlayContextMenu.IsOpen = true;
    }

    // Context menu
    private void PinMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        _settingsService.Settings.AlwaysOnTop = Topmost;
        _settingsService.Save();
        UpdatePinMenuItem();
    }

    private void UpdatePinMenuItem()
    {
        PinMenuItem.Header = Topmost ? "✓ Pin (Always on top)" : "Pin (Always on top)";
    }

    private void HideMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).OpenSettings();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).ExitApp();
    }

    // Save position on move/close
    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        SavePosition();
    }

    private void SavePosition()
    {
        if (Left > -9999 && Top > -9999)
        {
            _settingsService.Settings.WindowPosition.X = Left;
            _settingsService.Settings.WindowPosition.Y = Top;
            _settingsService.Save();
        }
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_forceClose)
        {
            // Hide to tray instead of closing
            e.Cancel = true;
            Hide();
        }
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _audioRecorder.Dispose();
        _hotkeyService.Dispose();
        base.OnClosed(e);
    }
}
