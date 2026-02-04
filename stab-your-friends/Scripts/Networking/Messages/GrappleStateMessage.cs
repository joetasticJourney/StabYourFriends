using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class GrappleStateMessage : IMessage
{
    [JsonPropertyName("type")]
    public string Type => "grappleState";

    [JsonPropertyName("isGrappling")]
    public bool IsGrappling { get; set; }
}
