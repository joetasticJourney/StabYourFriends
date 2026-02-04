using System.Collections.Generic;
using Godot;
using StabYourFriends.Autoload;
using StabYourFriends.Controllers;
using StabYourFriends.Game.PowerUps;

namespace StabYourFriends.Game;

public partial class GameWorld : Node2D
{
	[Export] public PackedScene StabCharacterScene { get; set; } = null!;
	[Export] public int NpcCount { get; set; } = 50;
	[Export] public float GameDurationSeconds { get; set; } = 600f;

	private PackedScene _npcCharacterScene = null!;
	private PackedScene _vipCharacterScene = null!;

	public string GameMode { get; set; } = "FreeForAll";

	// Universal character color (same for all players and NPCs)
	private static readonly Color CharacterColor = new Color(0.9f, 0.3f, 0.4f); // Red-pink

	// Reference resolution for 16:9 aspect ratio
	private const float ReferenceWidth = 1920f;
	private const float ReferenceHeight = 1080f;
	private const float AspectRatio = ReferenceWidth / ReferenceHeight; // 16:9
	private const float BaseWallThickness = 20f;
	private const float LeaderboardRefWidth = 125f;

	// Power-up spawn settings
	private const float MinSpawnInterval = 2f;
	private const float MaxSpawnInterval = 4f;

	// VIP spawn settings
	private const float MinVipSpawnInterval = 5f;
	private const float MaxVipSpawnInterval = 20f;

	// Separate character lists for players and NPCs
	private readonly Dictionary<string, StabCharacter> _playerCharacters = new();
	private readonly Dictionary<string, StabCharacter> _npcCharacters = new();
	private int _npcCounter = 0;
	private Vector2 _gameAreaSize;  // The actual game area size (maintains aspect ratio)
	private Vector2 _gameAreaOffset; // Offset for centering the game area
	private float _scaleFactor = 1f;

	// Power-up spawning
	private Vector2[] _spawnPoints = new Vector2[4];
	private readonly Dictionary<int, PowerUp> _activePowerUps = new();
	private readonly RandomNumberGenerator _powerUpRng = new();
	private Timer _powerUpTimer = null!;
	private SpawnPointMarkers _spawnPointMarkers = null!;

	// VIP spawning
	private readonly RandomNumberGenerator _vipRng = new();
	private Timer _vipTimer = null!;
	private int _vipCounter = 0;

	private Leaderboard _leaderboard = null!;
	private float _leaderboardWidth;

	private ColorRect _background = null!;
	private ColorRect _letterboxLeft = null!;
	private ColorRect _letterboxRight = null!;
	private ColorRect _letterboxTop = null!;
	private ColorRect _letterboxBottom = null!;
	private CollisionShape2D _topWall = null!;
	private CollisionShape2D _bottomWall = null!;
	private CollisionShape2D _leftWall = null!;
	private CollisionShape2D _rightWall = null!;

	// Game timer and end conditions
	private GameTimer _gameTimer = null!;
	private CanvasLayer _uiLayer = null!;
	private bool _gameEnded;

	public override void _Ready()
	{
		// Load derived character scenes
		_npcCharacterScene = GD.Load<PackedScene>("res://Scenes/Game/NpcCharacter.tscn");
		_vipCharacterScene = GD.Load<PackedScene>("res://Scenes/Game/VipCharacter.tscn");

		// Get references to scene elements
		_background = GetNode<ColorRect>("Background");
		_topWall = GetNode<CollisionShape2D>("Walls/TopWall/CollisionShape2D");
		_bottomWall = GetNode<CollisionShape2D>("Walls/BottomWall/CollisionShape2D");
		_leftWall = GetNode<CollisionShape2D>("Walls/LeftWall/CollisionShape2D");
		_rightWall = GetNode<CollisionShape2D>("Walls/RightWall/CollisionShape2D");

		// Create letterbox bars for aspect ratio preservation
		CreateLetterboxBars();

		// Create leaderboard panel
		_leaderboard = new Leaderboard();
		_leaderboard.ZIndex = 101;
		_leaderboard.SetPlayerCharacters(_playerCharacters);
		AddChild(_leaderboard);

		// Initial size update
		UpdateWorldSize();

		// Connect to viewport size changed signal
		GetTree().Root.SizeChanged += OnViewportSizeChanged;

		GD.Print($"GameWorld started - Mode: {GameMode}");
		GD.Print($"Game area size: {_gameAreaSize}");

		SpawnAllPlayers();
		SpawnNpcs();

		// Create spawn point markers
		_spawnPointMarkers = new SpawnPointMarkers();
		AddChild(_spawnPointMarkers);
		_spawnPointMarkers.Update(_spawnPoints, _scaleFactor);

		// Set up power-up spawn timer
		_powerUpRng.Randomize();
		_powerUpTimer = new Timer();
		_powerUpTimer.OneShot = true;
		_powerUpTimer.Timeout += OnPowerUpTimerTimeout;
		AddChild(_powerUpTimer);
		StartPowerUpTimer();

		// Set up VIP spawn timer
		_vipRng.Randomize();
		_vipTimer = new Timer();
		_vipTimer.OneShot = true;
		_vipTimer.Timeout += OnVipTimerTimeout;
		AddChild(_vipTimer);
		StartVipTimer();

		// Set up game timer on the UI layer
		_uiLayer = GetNode<CanvasLayer>("UI");
		_gameTimer = new GameTimer();
		_uiLayer.AddChild(_gameTimer);
		_gameTimer.Initialize(GameDurationSeconds);

		// Subscribe to player events for mid-game joins/leaves
		GameManager.Instance.PlayerJoined += OnPlayerJoined;
		GameManager.Instance.PlayerLeft += OnPlayerLeft;
		GameManager.Instance.PlayerShake += OnPlayerShake;
		GameManager.Instance.PlayerDisconnected += OnPlayerDisconnected;
		GameManager.Instance.PlayerReconnected += OnPlayerReconnected;
	}

	public override void _Process(double delta)
	{
		if (_gameEnded) return;

		// Check timer expiry
		if (_gameTimer.IsExpired)
		{
			EndGame(false);
			return;
		}

		// Check last-player-standing (only when more than 1 player existed)
		if (_playerCharacters.Count > 1)
		{
			int aliveCount = 0;
			foreach (var character in _playerCharacters.Values)
			{
				if (!character.IsDead && character.Visible)
					aliveCount++;
			}

			if (aliveCount <= 1)
			{
				EndGame(true);
				return;
			}
		}
	}

	private void SpawnNpcs()
	{
		for (int i = 0; i < NpcCount; i++)
		{
			var name = $"Bot {i + 1}";
			SpawnNpc(name, CharacterColor);
		}
	}

	private void CreateLetterboxBars()
	{
		var letterboxColor = new Color(0, 0, 0, 1); // Black bars

		_letterboxLeft = new ColorRect { Color = letterboxColor, ZIndex = 100 };
		_letterboxRight = new ColorRect { Color = letterboxColor, ZIndex = 100 };
		_letterboxTop = new ColorRect { Color = letterboxColor, ZIndex = 100 };
		_letterboxBottom = new ColorRect { Color = letterboxColor, ZIndex = 100 };

		AddChild(_letterboxLeft);
		AddChild(_letterboxRight);
		AddChild(_letterboxTop);
		AddChild(_letterboxBottom);
	}

	private void OnViewportSizeChanged()
	{
		UpdateWorldSize();
	}

	private void UpdateWorldSize()
	{
		var viewportSize = GetViewportRect().Size;

		// Reserve leaderboard width based on viewport height scale
		float heightScale = viewportSize.Y / ReferenceHeight;
		_leaderboardWidth = LeaderboardRefWidth * heightScale;
		float availableWidth = viewportSize.X - _leaderboardWidth;

		// Calculate game area size to fit in remaining space while maintaining 16:9
		float availableAspect = availableWidth / viewportSize.Y;
		if (availableAspect > AspectRatio)
		{
			_gameAreaSize.Y = viewportSize.Y;
			_gameAreaSize.X = viewportSize.Y * AspectRatio;
		}
		else
		{
			_gameAreaSize.X = availableWidth;
			_gameAreaSize.Y = availableWidth / AspectRatio;
		}

		// Center game area vertically, horizontally within the non-leaderboard space
		_gameAreaOffset = new Vector2(
			(availableWidth - _gameAreaSize.X) / 2f,
			(viewportSize.Y - _gameAreaSize.Y) / 2f
		);

		// Calculate scale factor based on game area height vs reference
		_scaleFactor = _gameAreaSize.Y / ReferenceHeight;

		// Position this node to offset the game area
		Position = _gameAreaOffset;

		// Update background size to match game area
		_background.Size = _gameAreaSize;
		_background.Position = Vector2.Zero;

		// Update letterbox bars
		UpdateLetterboxBars(viewportSize);

		// Scale wall thickness
		float wallThickness = BaseWallThickness * _scaleFactor;

		// Update wall shapes
		var horizontalShape = new RectangleShape2D { Size = new Vector2(_gameAreaSize.X, wallThickness) };
		var verticalShape = new RectangleShape2D { Size = new Vector2(wallThickness, _gameAreaSize.Y) };

		// Top wall
		_topWall.Shape = horizontalShape;
		_topWall.Position = new Vector2(_gameAreaSize.X / 2, -wallThickness / 2);

		// Bottom wall
		_bottomWall.Shape = horizontalShape;
		_bottomWall.Position = new Vector2(_gameAreaSize.X / 2, _gameAreaSize.Y + wallThickness / 2);

		// Left wall
		_leftWall.Shape = verticalShape;
		_leftWall.Position = new Vector2(-wallThickness / 2, _gameAreaSize.Y / 2);

		// Right wall
		_rightWall.Shape = verticalShape;
		_rightWall.Position = new Vector2(_gameAreaSize.X + wallThickness / 2, _gameAreaSize.Y / 2);

		// Update power-up spawn points
		UpdateSpawnPoints();

		// Update active power-up scales
		UpdateAllPowerUpScales();

		// Update all player character scales
		UpdateAllPlayerScales();

		// Position leaderboard to the right edge of viewport (coords relative to this node at _gameAreaOffset)
		if (IsInstanceValid(_leaderboard))
		{
			_leaderboard.Position = new Vector2(
				viewportSize.X - _leaderboardWidth - _gameAreaOffset.X,
				-_gameAreaOffset.Y
			);
			_leaderboard.Size = new Vector2(_leaderboardWidth, viewportSize.Y);
			_leaderboard.SetScaleFactor(_scaleFactor);
		}
	}

	private void UpdateLetterboxBars(Vector2 viewportSize)
	{
		// Position letterbox bars relative to game area (accounting for node offset)
		float leftWidth = _gameAreaOffset.X;
		float rightWidth = _gameAreaOffset.X;
		float topHeight = _gameAreaOffset.Y;
		float bottomHeight = _gameAreaOffset.Y;

		// Left bar
		_letterboxLeft.Position = new Vector2(-_gameAreaOffset.X, -_gameAreaOffset.Y);
		_letterboxLeft.Size = new Vector2(leftWidth, viewportSize.Y);
		_letterboxLeft.Visible = leftWidth > 0;

		// Right bar
		_letterboxRight.Position = new Vector2(_gameAreaSize.X, -_gameAreaOffset.Y);
		_letterboxRight.Size = new Vector2(rightWidth, viewportSize.Y);
		_letterboxRight.Visible = rightWidth > 0;

		// Top bar
		_letterboxTop.Position = new Vector2(-_gameAreaOffset.X, -_gameAreaOffset.Y);
		_letterboxTop.Size = new Vector2(viewportSize.X, topHeight);
		_letterboxTop.Visible = topHeight > 0;

		// Bottom bar
		_letterboxBottom.Position = new Vector2(-_gameAreaOffset.X, _gameAreaSize.Y);
		_letterboxBottom.Size = new Vector2(viewportSize.X, bottomHeight);
		_letterboxBottom.Visible = bottomHeight > 0;
	}

	private void UpdateAllPlayerScales()
	{
		foreach (var character in _playerCharacters.Values)
		{
			character.SetScale(_scaleFactor);
			character.SetGameBounds(_gameAreaSize);
		}
		foreach (var character in _npcCharacters.Values)
		{
			character.SetScale(_scaleFactor);
			character.SetGameBounds(_gameAreaSize);
		}
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
		if (_playerCharacters.ContainsKey(controller.PlayerId)) return;

		var character = StabCharacterScene.Instantiate<StabCharacter>();
		character.InitializeAsPlayer(controller);

		// Override color to match all characters
		character.SetColor(CharacterColor);

		// Set initial scale and game bounds
		character.SetScale(_scaleFactor);
		character.SetGameBounds(_gameAreaSize);

		// Spawn in a circle around the center of the game area
		var spawnPos = GetSpawnPosition(index, totalPlayers);
		character.Position = spawnPos;

		AddChild(character);
		_playerCharacters[controller.PlayerId] = character;

		GD.Print($"Spawned player: {controller.PlayerName} at {spawnPos}");
	}

	/// <summary>
	/// Spawn an NPC character
	/// </summary>
	public StabCharacter SpawnNpc(string name, Color color)
	{
		_npcCounter++;
		string npcId = $"npc_{_npcCounter}";

		var character = _npcCharacterScene.Instantiate<NpcCharacter>();
		character.InitializeAsNpc(npcId, name, color);

		// Set initial scale and game bounds
		character.SetScale(_scaleFactor);
		character.SetGameBounds(_gameAreaSize);

		// Spawn at random position within game area
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		var margin = 100f * _scaleFactor;
		var spawnPos = new Vector2(
			rng.RandfRange(margin, _gameAreaSize.X - margin),
			rng.RandfRange(margin, _gameAreaSize.Y - margin)
		);
		character.Position = spawnPos;

		AddChild(character);
		_npcCharacters[npcId] = character;

		GD.Print($"Spawned NPC: {name} at {spawnPos}");
		return character;
	}

	private Vector2 GetSpawnPosition(int index, int totalEntities)
	{
		var center = _gameAreaSize / 2;
		var radius = _gameAreaSize.Y * 0.3f;
		var angle = (index * Mathf.Tau) / Mathf.Max(totalEntities, 1);
		return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
	}

	private void OnPlayerJoined(PlayerController controller)
	{
		if (_gameEnded) return;

		// Spawn mid-game joiners
		SpawnPlayer(controller, _playerCharacters.Count, _playerCharacters.Count + 1);
	}

	private void OnPlayerLeft(PlayerController controller)
	{
		if (_playerCharacters.TryGetValue(controller.PlayerId, out var character))
		{
			character.CleanupGrapple();
			character.QueueFree();
			_playerCharacters.Remove(controller.PlayerId);
			GD.Print($"Removed character: {character.CharacterName}");
		}
	}

	private void OnPlayerDisconnected(PlayerController controller)
	{
		// PlayerId hasn't been remapped yet — it still has the old connection ID
		if (_playerCharacters.TryGetValue(controller.PlayerId, out var character))
		{
			character.CleanupGrapple();
			character.Visible = false;
			character.SetPhysicsProcess(false);
			GD.Print($"Player disconnected, hiding character: {character.CharacterName}");
		}
	}

	private void OnPlayerReconnected(PlayerController controller, string oldPlayerId)
	{
		// The controller's PlayerId has already been updated to the new connection ID.
		// The character is keyed under oldPlayerId in _playerCharacters.
		if (_playerCharacters.TryGetValue(oldPlayerId, out var character))
		{
			// Remap the dictionary key
			_playerCharacters.Remove(oldPlayerId);
			_playerCharacters[controller.PlayerId] = character;

			// Reassign the controller so input routes correctly
			character.ReassignController(controller);

			// Unhide
			character.Visible = true;
			character.SetPhysicsProcess(true);

			GD.Print($"Player reconnected, unhiding character: {character.CharacterName} (old key={oldPlayerId}, new key={controller.PlayerId})");
		}
		else
		{
			// Character not found — spawn as new player
			GD.Print($"Player reconnected but no character found, spawning new: {controller.PlayerName}");
			SpawnPlayer(controller, _playerCharacters.Count, _playerCharacters.Count + 1);
		}
	}

	private void OnPlayerShake(PlayerController controller)
	{
		if (_playerCharacters.TryGetValue(controller.PlayerId, out var character))
		{
			character.OnPlayerStab();
		}
	}

	/// <summary>
	/// Remove an NPC by its ID
	/// </summary>
	public void RemoveNpc(string npcId)
	{
		if (_npcCharacters.TryGetValue(npcId, out var character))
		{
			character.CleanupGrapple();
			character.QueueFree();
			_npcCharacters.Remove(npcId);
			GD.Print($"Removed NPC: {character.CharacterName}");
		}
	}

	/// <summary>
	/// Get all characters (players and NPCs) within a radius of a position
	/// </summary>
	public List<StabCharacter> GetNearbyCharacters(Vector2 position, float radius)
	{
		var result = new List<StabCharacter>();
		float radiusSquared = radius * radius;

		foreach (var character in _playerCharacters.Values)
		{
			if (!IsInstanceValid(character)) continue;

			float distSquared = (character.Position - position).LengthSquared();
			if (distSquared <= radiusSquared)
			{
				result.Add(character);
			}
		}

		foreach (var character in _npcCharacters.Values)
		{
			if (!IsInstanceValid(character)) continue;

			float distSquared = (character.Position - position).LengthSquared();
			if (distSquared <= radiusSquared)
			{
				result.Add(character);
			}
		}

		return result;
	}

	/// <summary>
	/// Get all player-controlled characters within a radius of a position
	/// </summary>
	public List<StabCharacter> GetNearbyPlayerCharacters(Vector2 position, float radius)
	{
		var result = new List<StabCharacter>();
		float radiusSquared = radius * radius;

		foreach (var character in _playerCharacters.Values)
		{
			if (!IsInstanceValid(character)) continue;

			float distSquared = (character.Position - position).LengthSquared();
			if (distSquared <= radiusSquared)
			{
				result.Add(character);
			}
		}

		return result;
	}

	private void UpdateSpawnPoints()
	{
		float x1 = 0.35f * _gameAreaSize.X;
		float x2 = 0.65f * _gameAreaSize.X;
		float y1 = 0.35f * _gameAreaSize.Y;
		float y2 = 0.65f * _gameAreaSize.Y;

		_spawnPoints[0] = new Vector2(x1, y1);
		_spawnPoints[1] = new Vector2(x2, y1);
		_spawnPoints[2] = new Vector2(x1, y2);
		_spawnPoints[3] = new Vector2(x2, y2);

		// Update spawn point markers
		if (IsInstanceValid(_spawnPointMarkers))
		{
			_spawnPointMarkers.Update(_spawnPoints, _scaleFactor);
		}

		// Reposition any active power-ups to updated spawn points
		foreach (var kvp in _activePowerUps)
		{
			if (IsInstanceValid(kvp.Value))
			{
				kvp.Value.Position = _spawnPoints[kvp.Key];
			}
		}
	}

	private void UpdateAllPowerUpScales()
	{
		foreach (var kvp in _activePowerUps)
		{
			if (IsInstanceValid(kvp.Value))
			{
				kvp.Value.UpdateScale(_scaleFactor);
			}
		}
	}

	private void StartPowerUpTimer()
	{
		float interval = _powerUpRng.RandfRange(MinSpawnInterval, MaxSpawnInterval);
		_powerUpTimer.WaitTime = interval;
		_powerUpTimer.Start();
	}

	private void OnPowerUpTimerTimeout()
	{
		SpawnRandomPowerUp();
		StartPowerUpTimer();
	}

	private void SpawnRandomPowerUp()
	{
		// Clean up freed power-ups
		var toRemove = new List<int>();
		foreach (var kvp in _activePowerUps)
		{
			if (!IsInstanceValid(kvp.Value))
			{
				toRemove.Add(kvp.Key);
			}
		}
		foreach (var key in toRemove)
		{
			_activePowerUps.Remove(key);
		}

		// Find available spawn points (not occupied)
		var available = new List<int>();
		for (int i = 0; i < _spawnPoints.Length; i++)
		{
			if (!_activePowerUps.ContainsKey(i))
			{
				available.Add(i);
			}
		}

		if (available.Count == 0) return;

		// Pick a random available spot
		int spotIndex = available[_powerUpRng.RandiRange(0, available.Count - 1)];

		// Pick a random power-up type
		int typeIndex = _powerUpRng.RandiRange(0, 4);
		PowerUp powerUp = typeIndex switch
		{
			0 => new VictoryPointPowerUp(),
			1 => new KungFuPowerUp(),
			2 => new ReverseGripPowerUp(),
			3 => new SmokeBombPowerUp(),
			4 => new TurboStabPowerUp(),
			_ => new VictoryPointPowerUp()
		};

		powerUp.Initialize(_spawnPoints[spotIndex], _scaleFactor, this);
		AddChild(powerUp);
		_activePowerUps[spotIndex] = powerUp;

		GD.Print($"[PowerUp] Spawned {powerUp.Label} at spot {spotIndex} ({_spawnPoints[spotIndex]})");
	}

	private void StartVipTimer()
	{
		float interval = _vipRng.RandfRange(MinVipSpawnInterval, MaxVipSpawnInterval);
		_vipTimer.WaitTime = interval;
		_vipTimer.Start();
	}

	private void OnVipTimerTimeout()
	{
		SpawnVip();
		StartVipTimer();
	}

	private void SpawnVip()
	{
		_vipCounter++;
		_npcCounter++;
		string vipId = $"vip_{_vipCounter}";
		string vipName = $"VIP {_vipCounter}";

		var character = _vipCharacterScene.Instantiate<VipCharacter>();
		character.InitializeAsNpc(vipId, vipName, Colors.White);

		character.SetScale(_scaleFactor);
		character.SetGameBounds(_gameAreaSize);

		// Pick a random edge and position just off-screen
		var spawnPos = GetOffScreenSpawnPosition();
		character.Position = spawnPos;

		// Disable wall collision so it can walk onto the map
		character.CollisionMask = 0;

		AddChild(character);
		_npcCharacters[vipId] = character;

		// Start returning state so it walks toward the play area
		character.StartReturning();

		GD.Print($"Spawned VIP: {vipName} at {spawnPos}");
	}

	private Vector2 GetOffScreenSpawnPosition()
	{
		float offset = 150f * _scaleFactor;
		// 0=top, 1=bottom, 2=left, 3=right
		int edge = _vipRng.RandiRange(0, 3);

		return edge switch
		{
			0 => new Vector2(_vipRng.RandfRange(0, _gameAreaSize.X), -offset),
			1 => new Vector2(_vipRng.RandfRange(0, _gameAreaSize.X), _gameAreaSize.Y + offset),
			2 => new Vector2(-offset, _vipRng.RandfRange(0, _gameAreaSize.Y)),
			_ => new Vector2(_gameAreaSize.X + offset, _vipRng.RandfRange(0, _gameAreaSize.Y)),
		};
	}

	private void EndGame(bool lastPlayerStanding)
	{
		if (_gameEnded) return;
		_gameEnded = true;

		GD.Print($"Game ended! LastPlayerStanding={lastPlayerStanding}");

		// Stop timer
		_gameTimer.Stop();

		// Stop power-up and VIP spawning
		_powerUpTimer.Stop();
		_vipTimer.Stop();

		// Freeze all characters
		foreach (var character in _playerCharacters.Values)
		{
			if (IsInstanceValid(character))
				character.SetPhysicsProcess(false);
		}
		foreach (var character in _npcCharacters.Values)
		{
			if (IsInstanceValid(character))
				character.SetPhysicsProcess(false);
		}

		// Build rankings and show victory screen
		var rankings = BuildRankings(lastPlayerStanding);
		string winnerName = rankings.Count > 0 ? rankings[0].Name : "";

		var victoryScreen = new VictoryScreen();
		victoryScreen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_uiLayer.AddChild(victoryScreen);
		victoryScreen.ReturnToLobbyRequested += OnReturnToLobby;
		victoryScreen.Show(winnerName, rankings);
	}

	private List<PlayerRanking> BuildRankings(bool lastPlayerStanding)
	{
		var rankings = new List<PlayerRanking>();

		foreach (var character in _playerCharacters.Values)
		{
			if (!IsInstanceValid(character)) continue;

			bool isAlive = !character.IsDead && character.Visible;
			rankings.Add(new PlayerRanking
			{
				Name = character.CharacterName,
				Score = character.Score,
				IsDead = character.IsDead,
				WasLastAlive = lastPlayerStanding && isAlive,
				WasDisconnected = !character.Visible && !character.IsDead,
			});
		}

		// Sort: last alive first, then score desc, then alive before dead, then name
		rankings.Sort((a, b) =>
		{
			// Last alive always first
			if (a.WasLastAlive != b.WasLastAlive)
				return a.WasLastAlive ? -1 : 1;

			// Then by score descending
			if (a.Score != b.Score)
				return b.Score.CompareTo(a.Score);

			// Then alive before dead
			bool aAlive = !a.IsDead && !a.WasDisconnected;
			bool bAlive = !b.IsDead && !b.WasDisconnected;
			if (aAlive != bAlive)
				return aAlive ? -1 : 1;

			// Then alphabetically
			return string.Compare(a.Name, b.Name, System.StringComparison.Ordinal);
		});

		return rankings;
	}

	private void OnReturnToLobby()
	{
		GameManager.Instance.EndGame();
		GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
	}

	public override void _ExitTree()
	{
		GetTree().Root.SizeChanged -= OnViewportSizeChanged;

		_vipTimer.Timeout -= OnVipTimerTimeout;

		if (GameManager.Instance != null)
		{
			GameManager.Instance.PlayerJoined -= OnPlayerJoined;
			GameManager.Instance.PlayerLeft -= OnPlayerLeft;
			GameManager.Instance.PlayerShake -= OnPlayerShake;
			GameManager.Instance.PlayerDisconnected -= OnPlayerDisconnected;
			GameManager.Instance.PlayerReconnected -= OnPlayerReconnected;
		}
	}
}
