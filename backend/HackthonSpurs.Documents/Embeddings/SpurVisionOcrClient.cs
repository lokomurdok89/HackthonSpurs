using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace HackthonSpurs.Documents.Embeddings;

public sealed record SpurVisionSettings(string BaseUrl, string ApiKey, string Model)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(Model);
}

public sealed class SpurVisionOcrClient(HttpClient http, SpurVisionSettings settings)
{
    private const string OcrPrompt =
        "Transcribe all visible text, tables, part numbers, certificate numbers, heat numbers, dates, addresses, and stamps from this document image faithfully in its original language (English or French). " +
        "Preserve row/column structure for tables using plain text or pipe-separated columns. Do not invent or guess illegible text; mark unreadable regions as [illegible]. Return only the transcribed content.";

    public bool IsConfigured => settings.IsConfigured;
    public string Model => settings.Model;

    public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string mediaType, CancellationToken ct)
    {
        if (!settings.IsConfigured || imageBytes.Length == 0)
            return string.Empty;

        var endpoint = $"{settings.BaseUrl.TrimEnd('/')}/chat/completions";
        var dataUrl = $"data:{mediaType};base64,{Convert.ToBase64String(imageBytes)}";

        var body = new
        {
            model = settings.Model,
            temperature = 0.0,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = OcrPrompt },
                        new { type = "image_url", image_url = new { url = dataUrl } }
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            if (detail.Length > 300) detail = detail[..300];
            throw new SpurEmbeddingException(
                "vision_ocr_error",
                $"SPUR Vision OCR returned {(int)response.StatusCode}: {detail}",
                502);
        }

        var payload = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: ct);
        return payload?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? string.Empty;
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
