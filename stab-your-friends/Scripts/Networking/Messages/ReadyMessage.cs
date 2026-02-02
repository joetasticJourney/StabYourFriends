using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class ReadyMessage : IMessage
{
    [JsonPropertyName("type")]
    public string Type => "ready";

    [JsonPropertyName("isReady")]
    public bool IsReady { get; set; }
}
