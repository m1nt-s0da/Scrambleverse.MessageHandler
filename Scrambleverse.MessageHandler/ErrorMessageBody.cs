using System.Text.Json.Serialization;

namespace Scrambleverse.MessageHandler;

class ErrorMessageBody<T>
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "error";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("body")]
    public T? Body { get; set; } = default;
}

class ErrorMessageBody : ErrorMessageBody<object> { }
