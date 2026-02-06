using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class OofMessage : IMessage
{
    [JsonPropertyName("type")]
    public string Type => "oof";
}
