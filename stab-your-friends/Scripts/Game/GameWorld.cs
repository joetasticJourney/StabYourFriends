using System.Collections.Generic;
using Godot;
using StabYourFriends.Autoload;
using StabYourFriends.Controllers;

namespace StabYourFriends.Game;

public partial class GameWorld : Node2D
{
    [Export] public PackedScene PlayerCharacterScene { get; set; } = null!;

    public string GameMode { get; set; } = "FreeForAll";

    private readonly Dictionary<string, PlayerCharacter> _characters = new();
    private Vector2 _worldSize;

    public override void _Ready()
    {
        _worldSize = GetViewportRect().Size;

        GD.Print($"GameWorld started - Mode: {GameMode}");
        GD.Print($"World size: {_worldSize}");

        SpawnAllPlayers();

        // Subscribe to player events for mid-game joins/leaves
        GameManager.Instance.PlayerJoined += OnPlayerJoined;
        GameManager.Instance.PlayerLeft += OnPlayerLeft;
    }

    private void SpawnAllPlayers()
    {
        var players = GameManager.Instance.Players;
        int index = 0;
        int totalPlayers = players.Count;

        foreach (var kvp in players)
        {
            SpawnPlayer(kvp.Value, index, totalPlayers);
            index++;
        }
    }

    private void SpawnPlayer(PlayerController controller, int index, int totalPlayers)
    {
        if (_characters.ContainsKey(controller.PlayerId)) return;

        var character = PlayerCharacterScene.Instantiate<PlayerCharacter>();
        character.Initialize(controller);

        // Spawn in a circle around the center
        var center = _worldSize / 2;
        var radius = Mathf.Min(_worldSize.X, _worldSize.Y) * 0.3f;
        var angle = (index * Mathf.Tau) / Mathf.Max(totalPlayers, 1);
        var spawnPos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

        character.Position = spawnPos;

        AddChild(character);
        _characters[controller.PlayerId] = character;

        GD.Print($"Spawned player: {controller.PlayerName} at {spawnPos}");
    }

    private void OnPlayerJoined(PlayerController controller)
    {
        // Spawn mid-game joiners
        SpawnPlayer(controller, _characters.Count, _characters.Count + 1);
    }

    private void OnPlayerLeft(PlayerController controller)
    {
        if (_characters.TryGetValue(controller.PlayerId, out var character))
        {
            character.QueueFree();
            _characters.Remove(controller.PlayerId);
            GD.Print($"Removed player: {controller.PlayerName}");
        }
    }

    public override void _ExitTree()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayerJoined -= OnPlayerJoined;
            GameManager.Instance.PlayerLeft -= OnPlayerLeft;
        }
    }
}
