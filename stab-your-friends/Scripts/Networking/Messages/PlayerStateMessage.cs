using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class PlayerStateMessage : IMessage
{
    [JsonPropertyName("type")]
    public string Type => "playerState";

    [JsonPropertyName("health")]
    public int Health { get; set; }

    [JsonPropertyName("maxHealth")]
    public int MaxHealth { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("kungFuCount")]
    public int KungFuCount { get; set; }

    [JsonPropertyName("reverseGripCount")]
    public int ReverseGripCount { get; set; }

    [JsonPropertyName("turboStabCount")]
    public int TurboStabCount { get; set; }

    [JsonPropertyName("smokeBombCount")]
    public int SmokeBombCount { get; set; }

    [JsonPropertyName("isDead")]
    public bool IsDead { get; set; }
}
