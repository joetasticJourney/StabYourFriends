using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class DeathMessage : IMessage
{
    [JsonPropertyName("type")]
    public string Type => "death";
}
