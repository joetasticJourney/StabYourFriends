using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class WelcomeMessage : IMessage
{
    [JsonPropertyName("type")]
    public string Type => "welcome";

    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("playerColor")]
    public string PlayerColor { get; set; } = "";
}
