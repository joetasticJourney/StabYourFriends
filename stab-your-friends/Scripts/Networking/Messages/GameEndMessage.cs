using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class GameEndMessage : IMessage
{
    [JsonPropertyName("type")]
    public string Type => "gameEnd";
}
