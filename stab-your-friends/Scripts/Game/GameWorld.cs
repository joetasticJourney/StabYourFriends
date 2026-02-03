using System.Collections.Generic;
using Godot;
using StabYourFriends.Autoload;
using StabYourFriends.Controllers;

namespace StabYourFriends.Game;

public partial class GameWorld : Node2D
{
	[Export] public PackedScene StabCharacterScene { get; set; } = null!;
	[Export] public int NpcCount { get; set; } = 50;

	public string GameMode { get; set; } = "FreeForAll";

	// Universal character color (same for all players and NPCs)
	private static readonly Color CharacterColor = new Color(0.9f, 0.3f, 0.4f); // Red-pink

	// Reference resolution for 16:9 aspect ratio
	private const float ReferenceWidth = 1920f;
	private const float ReferenceHeight = 1080f;
	private const float AspectRatio = ReferenceWidth / ReferenceHeight; // 16:9
	private const float BaseWallThickness = 20f;

	// All characters (players and NPCs)
	private readonly Dictionary<string, StabCharacter> _characters = new();
	private int _npcCounter = 0;
	private Vector2 _gameAreaSize;  // The actual game area size (maintains aspect ratio)
	private Vector2 _gameAreaOffset; // Offset for centering the game area
	private float _scaleFactor = 1f;

	private ColorRect _background = null!;
	private ColorRect _letterboxLeft = null!;
	private ColorRect _letterboxRight = null!;
	private ColorRect _letterboxTop = null!;
	private ColorRect _letterboxBottom = null!;
	private CollisionShape2D _topWall = null!;
	private CollisionShape2D _bottomWall = null!;
	private CollisionShape2D _leftWall = null!;
	private CollisionShape2D _rightWall = null!;

	public override void _Ready()
	{
		// Get references to scene elements
		_background = GetNode<ColorRect>("Background");
		_topWall = GetNode<CollisionShape2D>("Walls/TopWall/CollisionShape2D");
		_bottomWall = GetNode<CollisionShape2D>("Walls/BottomWall/CollisionShape2D");
		_leftWall = GetNode<CollisionShape2D>("Walls/LeftWall/CollisionShape2D");
		_rightWall = GetNode<CollisionShape2D>("Walls/RightWall/CollisionShape2D");

		// Create letterbox bars for aspect ratio preservation
		CreateLetterboxBars();

		// Initial size update
		UpdateWorldSize();

		// Connect to viewport size changed signal
		GetTree().Root.SizeChanged += OnViewportSizeChanged;

		GD.Print($"GameWorld started - Mode: {GameMode}");
		GD.Print($"Game area size: {_gameAreaSize}");

		SpawnAllPlayers();
		SpawnNpcs();

		// Subscribe to player events for mid-game joins/leaves
		GameManager.Instance.PlayerJoined += OnPlayerJoined;
		GameManager.Instance.PlayerLeft += OnPlayerLeft;
		GameManager.Instance.PlayerShake += OnPlayerShake;
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
		float viewportAspect = viewportSize.X / viewportSize.Y;

		// Calculate game area size to fit within viewport while maintaining aspect ratio
		if (viewportAspect > AspectRatio)
		{
			// Window is wider than 16:9 - fit to height, letterbox sides
			_gameAreaSize.Y = viewportSize.Y;
			_gameAreaSize.X = viewportSize.Y * AspectRatio;
		}
		else
		{
			// Window is taller than 16:9 - fit to width, letterbox top/bottom
			_gameAreaSize.X = viewportSize.X;
			_gameAreaSize.Y = viewportSize.X / AspectRatio;
		}

		// Calculate offset to center the game area
		_gameAreaOffset = (viewportSize - _gameAreaSize) / 2;

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

		// Update all player character scales
		UpdateAllPlayerScales();
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
		foreach (var character in _characters.Values)
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
		if (_characters.ContainsKey(controller.PlayerId)) return;

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
		_characters[controller.PlayerId] = character;

		GD.Print($"Spawned player: {controller.PlayerName} at {spawnPos}");
	}

	/// <summary>
	/// Spawn an NPC character
	/// </summary>
	public StabCharacter SpawnNpc(string name, Color color)
	{
		_npcCounter++;
		string npcId = $"npc_{_npcCounter}";

		var character = StabCharacterScene.Instantiate<StabCharacter>();
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
		_characters[npcId] = character;

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
		// Spawn mid-game joiners
		SpawnPlayer(controller, _characters.Count, _characters.Count + 1);
	}

	private void OnPlayerLeft(PlayerController controller)
	{
		if (_characters.TryGetValue(controller.PlayerId, out var character))
		{
			character.CleanupGrapple();
			character.QueueFree();
			_characters.Remove(controller.PlayerId);
			GD.Print($"Removed character: {character.CharacterName}");
		}
	}

	private void OnPlayerShake(PlayerController controller)
	{
		if (_characters.TryGetValue(controller.PlayerId, out var character))
		{
			character.OnPlayerStab();
		}
	}

	/// <summary>
	/// Remove an NPC by its ID
	/// </summary>
	public void RemoveNpc(string npcId)
	{
		if (_characters.TryGetValue(npcId, out var character))
		{
			character.CleanupGrapple();
			character.QueueFree();
			_characters.Remove(npcId);
			GD.Print($"Removed NPC: {character.CharacterName}");
		}
	}

	/// <summary>
	/// Get all characters within a radius of a position
	/// </summary>
	public List<StabCharacter> GetNearbyCharacters(Vector2 position, float radius)
	{
		var result = new List<StabCharacter>();
		float radiusSquared = radius * radius;

		foreach (var character in _characters.Values)
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

	public override void _ExitTree()
	{
		GetTree().Root.SizeChanged -= OnViewportSizeChanged;

		if (GameManager.Instance != null)
		{
			GameManager.Instance.PlayerJoined -= OnPlayerJoined;
			GameManager.Instance.PlayerLeft -= OnPlayerLeft;
			GameManager.Instance.PlayerShake -= OnPlayerShake;
		}
	}
}
