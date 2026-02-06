#nullable enable

using System.Collections.Generic;
using Godot;
using StabYourFriends.Autoload;
using StabYourFriends.Controllers;
using StabYourFriends.Game;

namespace StabYourFriends.UI;

public partial class LobbyScreen : Control
{
	[Export] public PackedScene PlayerSlotScene { get; set; } = null!;
	[Export] public PackedScene GameModeMenuScene { get; set; } = null!;

	private TextureRect _qrCode = null!;
	private Label _urlLabel = null!;
	private GridContainer _playerGrid = null!;
	private Button _startButton = null!;
	private Button _fullscreenButton = null!;
	private Label _statusLabel = null!;
	private GameModeMenu? _gameModeMenu;

	private readonly List<PlayerSlot> _playerSlots = new();

	public override void _Ready()
	{
		_qrCode = GetNode<TextureRect>("%QRCode");
		_urlLabel = GetNode<Label>("%UrlLabel");
		_playerGrid = GetNode<GridContainer>("%PlayerGrid");
		_startButton = GetNode<Button>("%StartButton");
		_fullscreenButton = GetNode<Button>("%FullscreenButton");
		_statusLabel = GetNode<Label>("%StatusLabel");

		_startButton.Pressed += OnStartPressed;
		_fullscreenButton.Pressed += OnFullscreenPressed;
		UpdateFullscreenButtonText();

		GenerateQRCode();

		GameManager.Instance.LobbyStateChanged += OnLobbyStateChanged;
		GameManager.Instance.PlayerJoined += OnPlayerJoined;
		GameManager.Instance.PlayerLeft += OnPlayerLeft;
		GameManager.Instance.GameStarted += OnGameStarted;

		UpdateUI();
	}

	private void GenerateQRCode()
	{
		var ip = GameManager.Instance.ServerIpAddress;
		var port = GameManager.Instance.ServerPort;
		var url = $"https://{ip}:{port}";

		_urlLabel.Text = url;

		// Generate QR code
		var qrTexture = QRCodeGenerator.Generate(url, 8);
		_qrCode.Texture = qrTexture;
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

	private void OnFullscreenPressed()
	{
		var currentMode = DisplayServer.WindowGetMode();
		if (currentMode == DisplayServer.WindowMode.Fullscreen)
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		}
		else
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
		}
		UpdateFullscreenButtonText();
	}

	private void UpdateFullscreenButtonText()
	{
		var currentMode = DisplayServer.WindowGetMode();
		_fullscreenButton.Text = currentMode == DisplayServer.WindowMode.Fullscreen ? "Windowed" : "Fullscreen";
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
		if (_gameModeMenu != null)
		{
			GameManager.Instance.CurrentSettings = _gameModeMenu.Settings;
		}
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
		GetTree().ChangeSceneToFile("res://Scenes/Game/SYFScene.tscn");
	}

	public override void _ExitTree()
	{
		_startButton.Pressed -= OnStartPressed;
		_fullscreenButton.Pressed -= OnFullscreenPressed;
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
