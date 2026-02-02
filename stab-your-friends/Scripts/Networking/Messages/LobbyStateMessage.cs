using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class PlayerInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "";
}

public class LobbyStateMessage : IMessage
{
    [JsonPropertyName("type")]
    public string Type => "lobbyState";

    [JsonPropertyName("players")]
    public List<PlayerInfo> Players { get; set; } = new();

    [JsonPropertyName("canStart")]
    public bool CanStart { get; set; }
}
