using System.Text.Json;

namespace HackthonSpurs.Resources;

/// <summary>Share the exact key-free gateway envelope between local sizing and the sending adapter.</summary>
public static class SpurPayload
{
    /// <summary>Encode image bytes of mediaType for model, including prompt, base64 and JSON escaping.</summary>
    public static byte[] Encode(string model, byte[] image, string mediaType)
    {
        var content = new object[] {
            new { type = WireFields.Text, text = ResourceText.TranscriptionPrompt },
            new { type = WireFields.ImageUrl, image_url = new { url = $"data:{mediaType};base64,{Convert.ToBase64String(image)}" } }
        };
        return JsonSerializer.SerializeToUtf8Bytes(new { model, messages = new[] { new { role = WireFields.User, content } } }, JsonSerializerOptions.Web);
    }
}
