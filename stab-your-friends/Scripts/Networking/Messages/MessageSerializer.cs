#nullable enable

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace StabYourFriends.Networking.Messages;

public static class MessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(IMessage message)
    {
        return message switch
        {
            WelcomeMessage m => JsonSerializer.Serialize(m, Options),
            LobbyStateMessage m => JsonSerializer.Serialize(m, Options),
            ErrorMessage m => JsonSerializer.Serialize(m, Options),
            GameStartMessage m => JsonSerializer.Serialize(m, Options),
            JoinMessage m => JsonSerializer.Serialize(m, Options),
            InputMessage m => JsonSerializer.Serialize(m, Options),
            ReadyMessage m => JsonSerializer.Serialize(m, Options),
            ShakeMessage m => JsonSerializer.Serialize(m, Options),
            GrappleStateMessage m => JsonSerializer.Serialize(m, Options),
            GameEndMessage m => JsonSerializer.Serialize(m, Options),
            _ => throw new ArgumentException($"Unknown message type: {message.GetType()}")
        };
    }

    public static IMessage? Deserialize(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node == null) return null;

            var type = node["type"]?.GetValue<string>();

            return type switch
            {
                "join" => JsonSerializer.Deserialize<JoinMessage>(json, Options),
                "input" => JsonSerializer.Deserialize<InputMessage>(json, Options),
                "ready" => JsonSerializer.Deserialize<ReadyMessage>(json, Options),
                "shake" => JsonSerializer.Deserialize<ShakeMessage>(json, Options),
                "welcome" => JsonSerializer.Deserialize<WelcomeMessage>(json, Options),
                "lobbyState" => JsonSerializer.Deserialize<LobbyStateMessage>(json, Options),
                "error" => JsonSerializer.Deserialize<ErrorMessage>(json, Options),
                _ => null
            };
        }
        catch (JsonException e)
        {
            GD.PrintErr($"Failed to deserialize message: {e.Message}");
            return null;
        }
    }
}
