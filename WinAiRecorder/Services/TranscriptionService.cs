using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace WinAiRecorder.Services;

public class TranscriptionService
{
    private const string BaseUrl = "https://api.openai.com/v1";

    private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(30) };

    private static readonly string[] FallbackModels =
    [
        "gpt-4o-mini-transcribe",
        "gpt-4o-transcribe",
        "whisper-1"
    ];

    private static string? GetApiKey()
    {
        return Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User)
               ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    }

    public async Task<string> TranscribeAsync(MemoryStream audioStream, string model, CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set. Please configure it in Settings.");

        audioStream.Position = 0;

        using var content = new MultipartFormDataContent();

        // Use a copy of the stream so disposing StreamContent/content doesn't affect the caller's stream
        var audioContent = new StreamContent(new MemoryStream(audioStream.ToArray()));
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "file", "recording.wav");
        content.Add(new StringContent(model), "model");
        content.Add(new StringContent("json"), "response_format");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = content;

        HttpResponseMessage response;
        try
        {
            response = await _client.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Transcription request timed out after 30 seconds.");
        }

        using (response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException("Invalid API key. Please update it in Settings.");

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Transcription failed ({(int)response.StatusCode}): {errorBody}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("text", out var textElement))
                return textElement.GetString() ?? string.Empty;

            throw new InvalidOperationException("Unexpected response format from transcription API.");
        }
    }

    public async Task<List<string>> FetchModelsAsync()
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return FallbackModels.ToList();

        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _client.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
                return FallbackModels.ToList();

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);

            var models = new List<string>();
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                foreach (var model in data.EnumerateArray())
                {
                    if (model.TryGetProperty("id", out var idProp))
                    {
                        var id = idProp.GetString() ?? "";
                        if (id.Contains("transcri", StringComparison.OrdinalIgnoreCase) ||
                            id.Contains("whisper", StringComparison.OrdinalIgnoreCase) ||
                            id.Contains("audio", StringComparison.OrdinalIgnoreCase))
                        {
                            models.Add(id);
                        }
                    }
                }
            }

            return models.Count > 0 ? models : FallbackModels.ToList();
        }
        catch
        {
            return FallbackModels.ToList();
        }
    }
}
