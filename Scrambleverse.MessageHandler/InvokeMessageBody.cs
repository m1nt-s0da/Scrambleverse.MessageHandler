using System.Text.Json.Serialization;

namespace Scrambleverse.MessageHandler;

class InvokeMessageBody<T>
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "invoke";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("body")]
    public T? Body { get; set; } = default;
}

class InvokeMessageBody : InvokeMessageBody<object> { }
