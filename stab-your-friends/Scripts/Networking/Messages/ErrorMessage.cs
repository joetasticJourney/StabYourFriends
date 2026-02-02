using System.Text.Json.Serialization;

namespace StabYourFriends.Networking.Messages;

public class ErrorMessage : IMessage
{
    [JsonPropertyName("type")]
    public string Type => "error";

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}
