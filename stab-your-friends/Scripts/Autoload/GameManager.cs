using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using StabYourFriends.Controllers;
using StabYourFriends.Networking;
using StabYourFriends.Networking.Messages;

namespace StabYourFriends.Autoload;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; } = null!;

    [Export] public int MinPlayersToStart { get; set; } = 2;

    private WebSocketServer _server = null!;
    private HttpFileServer _httpServer = null!;
    private readonly Dictionary<string, PlayerController> _players = new();

    public event Action? LobbyStateChanged;
    public event Action<PlayerController>? PlayerJoined;
    public event Action<PlayerController>? PlayerLeft;
    public event Action<string>? GameStarted;

    public IReadOnlyDictionary<string, PlayerController> Players => _players;
    public string ServerIpAddress => WebSocketServer.GetLocalIpAddress();
    public int ServerPort => _server?.Port ?? 9080;
    public string CurrentGameMode { get; private set; } = "";

    public bool CanStartGame => _players.Count >= MinPlayersToStart;

    public override void _EnterTree()
    {
        GD.Print("=== GameManager _EnterTree ===");
        Instance = this;
    }

    public override void _Ready()
    {
        GD.Print("=== GameManager _Ready ===");

        // Start WebSocket server for game communication
        _server = new WebSocketServer();
        AddChild(_server);

        _server.ClientConnected += OnClientConnected;
        _server.ClientDisconnected += OnClientDisconnected;
        _server.MessageReceived += OnMessageReceived;

        // Start HTTP server to serve web client files
        _httpServer = new HttpFileServer();
        AddChild(_httpServer);

        GD.Print("=== Servers started ===");
    }

    private void OnClientConnected(ClientConnection client)
    {
        GD.Print($"New client connected: {client.Id}");
    }

    private void OnClientDisconnected(ClientConnection client)
    {
        if (_players.TryGetValue(client.Id, out var player))
        {
            _players.Remove(client.Id);
            GD.Print($"Player left: {player.PlayerName}");
            PlayerLeft?.Invoke(player);
            BroadcastLobbyState();
        }
    }

    private void OnMessageReceived(ClientConnection client, IMessage message)
    {
        switch (message)
        {
            case JoinMessage joinMsg:
                HandleJoin(client, joinMsg);
                break;
            case InputMessage inputMsg:
                HandleInput(client, inputMsg);
                break;
        }
    }

    private void HandleJoin(ClientConnection client, JoinMessage message)
    {
        if (_players.ContainsKey(client.Id))
        {
            client.Send(new ErrorMessage
            {
                Code = "ALREADY_JOINED",
                Message = "You have already joined"
            });
            return;
        }

        var playerName = string.IsNullOrWhiteSpace(message.PlayerName)
            ? $"Player {_players.Count + 1}"
            : message.PlayerName.Trim();

        if (playerName.Length > 20)
        {
            playerName = playerName[..20];
        }

        var player = new PlayerController(client.Id, playerName);
        _players[client.Id] = player;

        GD.Print($"Player joined: {player.PlayerName} ({player.PlayerId})");

        client.Send(new WelcomeMessage
        {
            PlayerId = player.PlayerId,
            PlayerColor = player.GetColorHex()
        });

        PlayerJoined?.Invoke(player);
        BroadcastLobbyState();
    }

    private void HandleInput(ClientConnection client, InputMessage message)
    {
        if (_players.TryGetValue(client.Id, out var player))
        {
            player.CurrentInput.Movement = new Vector2(message.MoveX, message.MoveY);
            player.CurrentInput.Action1 = message.Action1;
            player.CurrentInput.Action2 = message.Action2;
        }
    }

    private void BroadcastLobbyState()
    {
        var lobbyState = new LobbyStateMessage
        {
            CanStart = CanStartGame,
            Players = _players.Values.Select(p => new PlayerInfo
            {
                Id = p.PlayerId,
                Name = p.PlayerName,
                Color = p.GetColorHex()
            }).ToList()
        };

        _server.Broadcast(lobbyState);
        LobbyStateChanged?.Invoke();
    }

    public void ResetLobby()
    {
        _players.Clear();
        PlayerController.ResetColorIndex();
        BroadcastLobbyState();
    }

    public void StartGame(string gameMode)
    {
        if (!CanStartGame)
        {
            GD.PrintErr("Cannot start game - not enough players");
            return;
        }

        CurrentGameMode = gameMode;
        GD.Print($"Starting game with mode: {gameMode}");

        // Notify all clients that game is starting
        _server.Broadcast(new GameStartMessage { GameMode = gameMode });

        // Emit signal for scene transition
        GameStarted?.Invoke(gameMode);
    }

    public override void _ExitTree()
    {
        if (_server != null)
        {
            _server.ClientConnected -= OnClientConnected;
            _server.ClientDisconnected -= OnClientDisconnected;
            _server.MessageReceived -= OnMessageReceived;
        }
    }
}
