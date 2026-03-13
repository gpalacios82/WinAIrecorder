using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinAiRecorder.Services;

namespace WinAiRecorder;

public partial class ConfigWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly MainWindow _mainWindow;
    private readonly TranscriptionService _transcriptionService;
    private string _capturedHotkey = "";
    private bool _capturingHotkey;
    private bool _apiKeyIsPlaceholder;

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

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            CurrentKeyText.Text = "sin configurar";
        }
        else
        {
            // Show masked placeholder so user sees there's a key saved
            CurrentKeyText.Text = apiKey.Length > 8 ? apiKey[..4] + "…" + apiKey[^4..] : "sk-…";
            ApiKeyBox.Password = new string('●', Math.Min(apiKey.Length, 32));
            _apiKeyIsPlaceholder = true;
        }

        // Hotkey
        _capturedHotkey = settings.Hotkey;
        HotkeyBox.Text = settings.Hotkey;

        // Options
        ClipboardFallbackCheck.IsChecked = settings.UseClipboardFallback;
        RefreshMinutesBox.Text = settings.RefreshStatusMinutes.ToString();
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _apiKeyIsPlaceholder = false;
        ValidateKeyBtn.Content = "✓ Validar";
        ValidateKeyBtn.Foreground = (System.Windows.Media.Brush)FindResource("SubtextBrush");
    }

    private async void ValidateKey_Click(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Password;
        if (string.IsNullOrWhiteSpace(key))
        {
            // Try the saved key
            key = Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User)
                  ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            ValidateKeyBtn.Content = "sin key";
            ValidateKeyBtn.Foreground = System.Windows.Media.Brushes.OrangeRed;
            return;
        }

        ValidateKeyBtn.Content = "…";
        ValidateKeyBtn.IsEnabled = false;

        try
        {
            // Temporarily set the key in-process so FetchModelsAsync picks it up
            var previous = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", key);

            var models = await _transcriptionService.FetchModelsAsync();
            bool valid = models.Count > 0;

            Environment.SetEnvironmentVariable("OPENAI_API_KEY", previous);

            if (valid)
            {
                ValidateKeyBtn.Content = "✓ OK";
                ValidateKeyBtn.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
                // Refresh models with the new key
                await RefreshModelsWithKeyAsync(key);
            }
            else
            {
                ValidateKeyBtn.Content = "✗ Error";
                ValidateKeyBtn.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }
        catch
        {
            ValidateKeyBtn.Content = "✗ Error";
            ValidateKeyBtn.Foreground = System.Windows.Media.Brushes.OrangeRed;
        }
        finally
        {
            ValidateKeyBtn.IsEnabled = true;
        }
    }

    private void RefreshModels_Click(object sender, RoutedEventArgs e) => LoadModelsAsync();

    private async void LoadModelsAsync()
    {
        var key = ApiKeyBox.Password;
        if (!string.IsNullOrWhiteSpace(key))
        {
            var previous = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", key);
            await RefreshModelsWithKeyAsync(key);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", previous);
        }
        else
        {
            await RefreshModelsWithKeyAsync(null);
        }
    }

    private async Task RefreshModelsWithKeyAsync(string? keyOverride)
    {
        ModelLoadingText.Visibility = Visibility.Visible;
        ModelComboBox.IsEnabled = false;

        if (keyOverride != null)
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", keyOverride);

        try
        {
            var models = await _transcriptionService.FetchModelsAsync();
            ModelComboBox.Items.Clear();

            foreach (var model in models)
            {
                var displayName = model == "gpt-4o-mini-transcribe"
                    ? $"{model}  ★"
                    : model;
                ModelComboBox.Items.Add(new ModelItem { Id = model, DisplayName = displayName });
            }

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
        catch
        {
            ModelLoadingText.Text = "error al cargar";
            ModelLoadingText.Visibility = Visibility.Visible;
        }
        finally
        {
            if (ModelLoadingText.Text != "error al cargar")
                ModelLoadingText.Visibility = Visibility.Collapsed;
            ModelComboBox.IsEnabled = true;
        }
    }

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = true;
        HotkeyBox.Text = "pulsa la combinación…";
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

        if (parts.Count == 0) return;

        parts.Add(key.ToString());
        _capturedHotkey = string.Join("+", parts);
        HotkeyBox.Text = _capturedHotkey;
        _capturingHotkey = false;

        StatusText.Focus();
    }

    private void ResetHotkey_Click(object sender, RoutedEventArgs e)
    {
        _capturedHotkey = "Ctrl+Shift+Space";
        HotkeyBox.Text = _capturedHotkey;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Guardando…";

        var newKey = _apiKeyIsPlaceholder ? "" : ApiKeyBox.Password;
        if (!string.IsNullOrWhiteSpace(newKey))
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", newKey, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", newKey);
        }

        if (ModelComboBox.SelectedItem is ModelItem selectedModel)
            _settingsService.Settings.Model = selectedModel.Id;

        var newHotkey = _capturedHotkey;
        if (string.IsNullOrWhiteSpace(newHotkey))
            newHotkey = "Ctrl+Shift+Space";

        bool hotkeyChanged = newHotkey != _settingsService.Settings.Hotkey;
        _settingsService.Settings.Hotkey = newHotkey;

        _settingsService.Settings.UseClipboardFallback = ClipboardFallbackCheck.IsChecked == true;

        if (int.TryParse(RefreshMinutesBox.Text, out int mins) && mins > 0)
            _settingsService.Settings.RefreshStatusMinutes = mins;

        _settingsService.Save();
        _mainWindow.UpdateRefreshTimer();

        if (hotkeyChanged)
        {
            try
            {
                _mainWindow.ReRegisterHotkey();
            }
            catch (Exception ex)
            {
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                StatusText.Text = $"Hotkey: {ex.Message}";
                await Task.Delay(2000);
                StatusText.Text = "";
                StatusText.Foreground = (System.Windows.Media.Brush)FindResource("SubtextBrush");
                return;
            }
        }

        StatusText.Text = "Guardado";
        await Task.Delay(600);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

}

public class ModelItem
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public override string ToString() => DisplayName;
}
