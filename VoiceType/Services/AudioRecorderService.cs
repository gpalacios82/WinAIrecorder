using System.IO;
using System.Windows;
using NAudio.Wave;

namespace VoiceType.Services;

public class AudioRecorderService : IDisposable
{
    private const int SampleRate = 16000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;
    private const int MaxDurationSeconds = 600; // 10 minutes
    private const int WarnDurationSeconds = 540; // 9 minutes

    private readonly object _lock = new();

    private WaveInEvent? _waveIn;
    private MemoryStream? _memoryStream;
    private WaveFileWriter? _writer;
    private bool _isRecording;
    private DateTime _recordingStart;
    private System.Threading.Timer? _durationTimer;

    public event Action<float>? AudioLevelChanged;
    public event Action<MemoryStream>? RecordingCompleted;
    public event Action? MaxDurationWarning;
    public event Action? MaxDurationReached;
    public event Action<string>? RecordingError;

    public bool IsRecording => _isRecording;

    public void StartRecording()
    {
        if (_isRecording) return;

        try
        {
            _memoryStream = new MemoryStream();
            var waveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels);
            _writer = new WaveFileWriter(_memoryStream, waveFormat);

            _waveIn = new WaveInEvent
            {
                WaveFormat = waveFormat,
                BufferMilliseconds = 50
            };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();

            _isRecording = true;
            _recordingStart = DateTime.UtcNow;

            // Set up duration timer
            _durationTimer = new System.Threading.Timer(OnDurationTick, null, 1000, 1000);
        }
        catch (Exception ex)
        {
            CleanupRecording();
            RecordingError?.Invoke($"Failed to start recording: {ex.Message}");
        }
    }

    public void StopRecording()
    {
        if (!_isRecording) return;
        _waveIn?.StopRecording();
        // Completion is handled in OnRecordingStopped
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_lock)
        {
            if (!_isRecording || _writer == null) return;
            _writer.Write(e.Buffer, 0, e.BytesRecorded);
        }

        // Calculate RMS level
        float level = CalculateRmsLevel(e.Buffer, e.BytesRecorded);
        Application.Current?.Dispatcher.BeginInvoke(() => AudioLevelChanged?.Invoke(level));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _isRecording = false;
        _durationTimer?.Dispose();
        _durationTimer = null;

        if (e.Exception != null)
        {
            CleanupRecording();
            Application.Current?.Dispatcher.BeginInvoke(() =>
                RecordingError?.Invoke($"Recording error: {e.Exception.Message}"));
            return;
        }

        try
        {
            lock (_lock)
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }

            if (_memoryStream != null)
            {
                var elapsed = DateTime.UtcNow - _recordingStart;
                if (elapsed.TotalSeconds < 0.5)
                {
                    // Discard recordings shorter than 0.5s
                    _memoryStream.Dispose();
                    _memoryStream = null;
                    Application.Current?.Dispatcher.BeginInvoke(() => RecordingCompleted?.Invoke(new MemoryStream()));
                    return;
                }

                // Copy bytes to a new stream — WaveFileWriter.Dispose() closes the original
                var resultStream = new MemoryStream(_memoryStream.ToArray());
                _memoryStream.Dispose();
                _memoryStream = null;
                Application.Current?.Dispatcher.BeginInvoke(() => RecordingCompleted?.Invoke(resultStream));
            }
        }
        catch (Exception ex)
        {
            CleanupRecording();
            Application.Current?.Dispatcher.BeginInvoke(() =>
                RecordingError?.Invoke($"Error finalizing recording: {ex.Message}"));
        }
    }

    private void OnDurationTick(object? state)
    {
        if (!_isRecording) return;

        var elapsed = DateTime.UtcNow - _recordingStart;
        if (elapsed.TotalSeconds >= MaxDurationSeconds)
        {
            Application.Current?.Dispatcher.BeginInvoke(() => MaxDurationReached?.Invoke());
            StopRecording();
        }
        else if (elapsed.TotalSeconds >= WarnDurationSeconds && elapsed.TotalSeconds < WarnDurationSeconds + 1)
        {
            Application.Current?.Dispatcher.BeginInvoke(() => MaxDurationWarning?.Invoke());
        }
    }

    private static float CalculateRmsLevel(byte[] buffer, int bytesRecorded)
    {
        if (bytesRecorded < 2) return 0f;

        double sum = 0;
        int sampleCount = bytesRecorded / 2;
        for (int i = 0; i < bytesRecorded - 1; i += 2)
        {
            short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
            double normalized = sample / 32768.0;
            sum += normalized * normalized;
        }

        float rms = (float)Math.Sqrt(sum / sampleCount);
        // Amplify and clamp to 0-1
        return Math.Min(1f, rms * 5f);
    }

    private void CleanupRecording()
    {
        _isRecording = false;
        _durationTimer?.Dispose();
        _durationTimer = null;
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
        _memoryStream?.Dispose();
        _memoryStream = null;
        _waveIn?.Dispose();
        _waveIn = null;
    }

    public void Dispose()
    {
        _durationTimer?.Dispose();
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            if (_isRecording)
                _waveIn.StopRecording();
            _waveIn.Dispose();
            _waveIn = null;
        }
        lock (_lock)
        {
            _writer?.Dispose();
        }
        _memoryStream?.Dispose();
    }
}
