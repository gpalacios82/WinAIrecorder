using System.Threading;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using VoiceType.Helpers;
using VoiceType.Services;

namespace VoiceType;

public partial class App : Application
{
    private static Mutex? _mutex;
    private static bool _mutexOwned;
    private TaskbarIcon? _taskbarIcon;
    private MainWindow? _mainWindow;

    public SettingsService SettingsService { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance check
        _mutex = new Mutex(true, "Win AI Recorder_SingleInstance", out bool createdNew);
        _mutexOwned = createdNew;
        if (!createdNew)
        {
            MessageBox.Show("Win AI Recorder is already running.", "Win AI Recorder", MessageBoxButton.OK, MessageBoxImage.Information);
            _mutex.Dispose();
            _mutex = null;
            Shutdown();
            return;
        }

        // Load settings
        SettingsService.Load();

        // Apply theme
        var theme = ThemeHelper.ResolveTheme(SettingsService.Settings.Theme);
        ApplyDynamicTheme(theme);

        // Create main window (overlay)
        _mainWindow = new MainWindow(SettingsService);
        bool startMinimized = e.Args.Contains("--minimized");
        if (!startMinimized)
            _mainWindow.Show();

        // Create system tray icon
        InitializeTrayIcon();

        // First run notification is handled by MainWindow when user tries to record
    }

    private void InitializeTrayIcon()
    {
        _taskbarIcon = new TaskbarIcon();

        // Use generated icon
        var icon = IconHelper.CreateMicrophoneIcon(32);
        _taskbarIcon.Icon = icon;
        _taskbarIcon.ToolTipText = "Win AI Recorder — Listo";

        // Double-click to show/hide
        _taskbarIcon.TrayMouseDoubleClick += (s, e) => ToggleOverlay();

        // Context menu
        var contextMenu = new System.Windows.Controls.ContextMenu();

        var showHideItem = new System.Windows.Controls.MenuItem { Header = "Show / Hide" };
        showHideItem.Click += (s, e) => ToggleOverlay();
        contextMenu.Items.Add(showHideItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings..." };
        settingsItem.Click += (s, e) => OpenSettings();
        contextMenu.Items.Add(settingsItem);

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (s, e) => ExitApp();
        contextMenu.Items.Add(exitItem);

        _taskbarIcon.ContextMenu = contextMenu;

        // Expose to MainWindow for tooltip updates
        TrayIcon = _taskbarIcon;
    }

    public static TaskbarIcon? TrayIcon { get; private set; }

    public void ToggleOverlay()
    {
        if (_mainWindow == null) return;
        if (_mainWindow.Visibility == Visibility.Visible && _mainWindow.IsVisible)
            _mainWindow.Hide();
        else
            _mainWindow.Show();
    }

    public void OpenSettings()
    {
        if (_mainWindow == null) return;
        var config = new ConfigWindow(SettingsService, _mainWindow)
        {
            Owner = _mainWindow
        };
        config.ShowDialog();
    }

    public void ExitApp()
    {
        _taskbarIcon?.Dispose();
        _taskbarIcon = null;
        ReleaseMutexSafe();
        _mainWindow?.ForceClose();
        Shutdown();
    }

    private static void ReleaseMutexSafe()
    {
        if (_mutex != null && _mutexOwned)
        {
            try { _mutex.ReleaseMutex(); } catch { }
            _mutex.Dispose();
            _mutex = null;
            _mutexOwned = false;
        }
    }

    internal static void ApplyDynamicTheme(AppTheme theme)
    {
        var resources = Current.Resources;

        if (theme == AppTheme.Light)
        {
            resources["BackgroundColor"] = System.Windows.Media.Color.FromRgb(245, 247, 250);
            resources["SurfaceColor"] = System.Windows.Media.Color.FromRgb(255, 255, 255);
            resources["BorderColor"] = System.Windows.Media.Color.FromRgb(203, 213, 224);
            resources["AccentColor"] = System.Windows.Media.Color.FromRgb(49, 130, 206);
            resources["TextColor"] = System.Windows.Media.Color.FromRgb(26, 32, 44);
            resources["SubtextColor"] = System.Windows.Media.Color.FromRgb(113, 128, 150);
            resources["BackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 247, 250));
            resources["SurfaceBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
            resources["BorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 224));
            resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(49, 130, 206));
            resources["TextBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 32, 44));
            resources["SubtextBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(113, 128, 150));
        }
        else
        {
            resources["BackgroundColor"] = System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x2E);
            resources["SurfaceColor"] = System.Windows.Media.Color.FromRgb(0x2A, 0x2A, 0x3E);
            resources["BorderColor"] = System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x5C);
            resources["AccentColor"] = System.Windows.Media.Color.FromRgb(0x63, 0xB3, 0xED);
            resources["TextColor"] = System.Windows.Media.Color.FromRgb(0xE2, 0xE8, 0xF0);
            resources["SubtextColor"] = System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8);
            resources["BackgroundBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x2E));
            resources["SurfaceBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x2A, 0x3E));
            resources["BorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x5C));
            resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x63, 0xB3, 0xED));
            resources["TextBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE2, 0xE8, 0xF0));
            resources["SubtextBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _taskbarIcon?.Dispose();
        _taskbarIcon = null;
        ReleaseMutexSafe();
        base.OnExit(e);
    }
}
