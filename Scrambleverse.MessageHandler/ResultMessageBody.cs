using System.Text.Json.Serialization;

namespace Scrambleverse.MessageHandler;

class ResultMessageBody<T>
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "result";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("body")]
    public T? Body { get; set; } = default;
}

class ResultMessageBody : ResultMessageBody<object> { }
