using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class GrappleStateMessage : IMessage
{
    [JsonPropertyName("type")]
    public string Type => "grappleState";

    [JsonPropertyName("stabSpeed")]
    public float StabSpeed { get; set; } = 1.0f;
}
