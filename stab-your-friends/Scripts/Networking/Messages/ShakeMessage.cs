using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class ShakeMessage : IMessage
{
    [JsonPropertyName("type")]
    public string Type => "shake";
}
