using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class InputMessage : IMessage
{
    [JsonPropertyName("type")]
    public string Type => "input";

    [JsonPropertyName("moveX")]
    public float MoveX { get; set; }

    [JsonPropertyName("moveY")]
    public float MoveY { get; set; }

    [JsonPropertyName("action1")]
    public bool Action1 { get; set; }

    [JsonPropertyName("action2")]
    public bool Action2 { get; set; }

    [JsonPropertyName("orientAlpha")]
    public float OrientAlpha { get; set; }
}
