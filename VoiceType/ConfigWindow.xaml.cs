using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using VoiceType.Services;

namespace VoiceType;

public partial class ConfigWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly MainWindow _mainWindow;
    private readonly TranscriptionService _transcriptionService;
    private string _capturedHotkey = "";
    private bool _capturingHotkey;

    public ConfigWindow(SettingsService settingsService, MainWindow mainWindow)
    {
        _settingsService = settingsService;
        _mainWindow = mainWindow;
        _transcriptionService = new TranscriptionService();

        InitializeComponent();
        LoadCurrentSettings();
        LoadModelsAsync();
    }

    private void LoadCurrentSettings()
    {
        var settings = _settingsService.Settings;

        // API key status
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User)
                     ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var masked = apiKey.Length > 8
                ? apiKey[..4] + "…" + apiKey[^4..]
                : "sk-…";
            CurrentKeyText.Text = $"Current: {masked}";
        }
        else
        {
            CurrentKeyText.Text = "No API key configured";
        }

        // Hotkey
        _capturedHotkey = settings.Hotkey;
        HotkeyBox.Text = settings.Hotkey;

        // Options
        ClipboardFallbackCheck.IsChecked = settings.UseClipboardFallback;
        AutoStartCheck.IsChecked = settings.AutoStart;
    }

    private async void LoadModelsAsync()
    {
        ModelLoadingText.Visibility = Visibility.Visible;
        ModelComboBox.IsEnabled = false;

        try
        {
            var models = await _transcriptionService.FetchModelsAsync();
            ModelComboBox.Items.Clear();

            foreach (var model in models)
            {
                var displayName = model == "gpt-4o-mini-transcribe"
                    ? $"{model}  ⭐ Recommended"
                    : model;
                ModelComboBox.Items.Add(new ModelItem { Id = model, DisplayName = displayName });
            }

            // Select current model
            var currentModel = _settingsService.Settings.Model;
            foreach (ModelItem item in ModelComboBox.Items)
            {
                if (item.Id == currentModel)
                {
                    ModelComboBox.SelectedItem = item;
                    break;
                }
            }

            if (ModelComboBox.SelectedItem == null && ModelComboBox.Items.Count > 0)
                ModelComboBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            ModelLoadingText.Text = $"Failed to load models: {ex.Message}";
        }
        finally
        {
            ModelLoadingText.Visibility = Visibility.Collapsed;
            ModelComboBox.IsEnabled = true;
        }
    }

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = true;
        HotkeyBox.Text = "Press key combination...";
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = false;
        if (!string.IsNullOrEmpty(_capturedHotkey))
            HotkeyBox.Text = _capturedHotkey;
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingHotkey) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore modifier-only presses
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LWin || key == Key.RWin)
            return;

        var parts = new List<string>();
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            parts.Add("Ctrl");
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            parts.Add("Alt");
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            parts.Add("Shift");

        // Need at least one modifier
        if (parts.Count == 0) return;

        parts.Add(key.ToString());
        _capturedHotkey = string.Join("+", parts);
        HotkeyBox.Text = _capturedHotkey;
        _capturingHotkey = false;

        // Move focus away
        StatusText.Focus();
    }

    private void ResetHotkey_Click(object sender, RoutedEventArgs e)
    {
        _capturedHotkey = "Ctrl+Shift+Space";
        HotkeyBox.Text = _capturedHotkey;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Saving...";

        // Save API key if provided
        var newKey = ApiKeyBox.Password;
        if (!string.IsNullOrWhiteSpace(newKey))
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", newKey, EnvironmentVariableTarget.User);
            // Also set for current process
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", newKey);
        }

        // Save model
        if (ModelComboBox.SelectedItem is ModelItem selectedModel)
            _settingsService.Settings.Model = selectedModel.Id;

        // Save hotkey
        var newHotkey = _capturedHotkey;
        if (string.IsNullOrWhiteSpace(newHotkey))
            newHotkey = "Ctrl+Shift+Space";

        bool hotkeyChanged = newHotkey != _settingsService.Settings.Hotkey;
        _settingsService.Settings.Hotkey = newHotkey;

        // Save options
        _settingsService.Settings.UseClipboardFallback = ClipboardFallbackCheck.IsChecked == true;
        _settingsService.Settings.AutoStart = AutoStartCheck.IsChecked == true;

        // Auto-start registry
        SetAutoStart(_settingsService.Settings.AutoStart);

        // Save to file
        _settingsService.Save();

        // Re-register hotkey if changed
        if (hotkeyChanged)
        {
            try
            {
                _mainWindow.ReRegisterHotkey();
            }
            catch (Exception ex)
            {
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                StatusText.Text = $"Hotkey error: {ex.Message}";
                await Task.Delay(2000);
                StatusText.Text = "";
                StatusText.Foreground = (System.Windows.Media.Brush)FindResource("SubtextBrush");
                return;
            }
        }

        StatusText.Text = "Saved!";
        await Task.Delay(800);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static void SetAutoStart(bool enable)
    {
        const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "VoiceType";

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            if (key == null) return;

            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(valueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
        catch { /* Silently fail */ }
    }
}

public class ModelItem
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public override string ToString() => DisplayName;
}
