namespace WinAiRecorder.Models;

public class AppSettings
{
    public string Model { get; set; } = "gpt-4o-mini-transcribe";
    public string Hotkey { get; set; } = "Ctrl+Shift+Space";
    public bool UseClipboardFallback { get; set; } = false;
    public int RefreshStatusMinutes { get; set; } = 5;
    public WindowPosition WindowPosition { get; set; } = new WindowPosition();
}

public class WindowPosition
{
    public double X { get; set; } = -1;
    public double Y { get; set; } = -1;
}
