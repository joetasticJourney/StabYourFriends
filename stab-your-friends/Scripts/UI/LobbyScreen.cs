using System.Collections.Generic;
using Godot;
using StabYourFriends.Autoload;
using StabYourFriends.Controllers;

namespace StabYourFriends.UI;

public partial class LobbyScreen : Control
{
    [Export] public PackedScene PlayerSlotScene { get; set; } = null!;
    [Export] public PackedScene GameModeMenuScene { get; set; } = null!;

    private Label _ipLabel = null!;
    private Label _portLabel = null!;
    private GridContainer _playerGrid = null!;
    private Button _startButton = null!;
    private Label _statusLabel = null!;
    private GameModeMenu? _gameModeMenu;

    private readonly List<PlayerSlot> _playerSlots = new();

    public override void _Ready()
    {
        _ipLabel = GetNode<Label>("%IpLabel");
        _portLabel = GetNode<Label>("%PortLabel");
        _playerGrid = GetNode<GridContainer>("%PlayerGrid");
        _startButton = GetNode<Button>("%StartButton");
        _statusLabel = GetNode<Label>("%StatusLabel");

        _startButton.Pressed += OnStartPressed;

        UpdateServerInfo();

        GameManager.Instance.LobbyStateChanged += OnLobbyStateChanged;
        GameManager.Instance.PlayerJoined += OnPlayerJoined;
        GameManager.Instance.PlayerLeft += OnPlayerLeft;
        GameManager.Instance.GameStarted += OnGameStarted;

        UpdateUI();
    }

    private void UpdateServerInfo()
    {
        _ipLabel.Text = $"IP: {GameManager.Instance.ServerIpAddress}";
        _portLabel.Text = $"Port: {GameManager.Instance.ServerPort}";
    }

    private void OnLobbyStateChanged()
    {
        CallDeferred(nameof(UpdateUI));
    }

    private void OnPlayerJoined(PlayerController player)
    {
        GD.Print($"UI: Player joined - {player.PlayerName}");
    }

    private void OnPlayerLeft(PlayerController player)
    {
        GD.Print($"UI: Player left - {player.PlayerName}");
    }

    private void UpdateUI()
    {
        var players = GameManager.Instance.Players.Values;
        var playerCount = GameManager.Instance.Players.Count;

        // Add more slots if needed
        while (_playerSlots.Count < playerCount)
        {
            var slot = PlayerSlotScene.Instantiate<PlayerSlot>();
            _playerGrid.AddChild(slot);
            _playerSlots.Add(slot);
        }

        // Update existing slots with player data
        int slotIndex = 0;
        foreach (var player in players)
        {
            _playerSlots[slotIndex].SetPlayer(player);
            slotIndex++;
        }

        // Hide unused slots
        for (int i = slotIndex; i < _playerSlots.Count; i++)
        {
            _playerSlots[i].SetEmpty();
            _playerSlots[i].Visible = false;
        }

        // Show used slots
        for (int i = 0; i < slotIndex; i++)
        {
            _playerSlots[i].Visible = true;
        }

        var canStart = GameManager.Instance.CanStartGame;
        _startButton.Disabled = !canStart;

        var minPlayers = GameManager.Instance.MinPlayersToStart;

        if (playerCount < minPlayers)
        {
            _statusLabel.Text = $"Waiting for players... ({playerCount}/{minPlayers} minimum)";
        }
        else
        {
            _statusLabel.Text = $"{playerCount} player(s) connected. Press Start to begin!";
        }
    }

    private void OnStartPressed()
    {
        if (!GameManager.Instance.CanStartGame) return;

        // Show game mode selection menu
        ShowGameModeMenu();
    }

    private void ShowGameModeMenu()
    {
        if (_gameModeMenu != null) return;

        _gameModeMenu = GameModeMenuScene.Instantiate<GameModeMenu>();
        _gameModeMenu.GameModeSelected += OnGameModeSelected;
        _gameModeMenu.Cancelled += OnGameModeCancelled;
        AddChild(_gameModeMenu);
    }

    private void HideGameModeMenu()
    {
        if (_gameModeMenu == null) return;

        _gameModeMenu.GameModeSelected -= OnGameModeSelected;
        _gameModeMenu.Cancelled -= OnGameModeCancelled;
        _gameModeMenu.QueueFree();
        _gameModeMenu = null;
    }

    private void OnGameModeSelected(string gameMode)
    {
        HideGameModeMenu();
        GameManager.Instance.StartGame(gameMode);
    }

    private void OnGameModeCancelled()
    {
        HideGameModeMenu();
    }

    private void OnGameStarted(string gameMode)
    {
        // Load the game world scene
        GetTree().ChangeSceneToFile("res://Scenes/Game/GameWorld.tscn");
    }

    public override void _ExitTree()
    {
        _startButton.Pressed -= OnStartPressed;
        HideGameModeMenu();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LobbyStateChanged -= OnLobbyStateChanged;
            GameManager.Instance.PlayerJoined -= OnPlayerJoined;
            GameManager.Instance.PlayerLeft -= OnPlayerLeft;
            GameManager.Instance.GameStarted -= OnGameStarted;
        }
    }
}
