using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class GameStartMessage : IMessage
{
	[JsonPropertyName("type")]
	public string Type => "gameStart";

	[JsonPropertyName("gameMode")]
	public string GameMode { get; set; } = "";

	[JsonPropertyName("controllerMode")]
	public bool ControllerMode { get; set; } = true;
}
