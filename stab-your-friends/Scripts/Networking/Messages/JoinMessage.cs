using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class JoinMessage : IMessage
{
    [JsonPropertyName("type")]
    public string Type => "join";

    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; } = "";
}
