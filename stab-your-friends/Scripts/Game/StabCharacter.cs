#nullable enable

using Godot;
using StabYourFriends.Controllers;

namespace StabYourFriends.Game;

public partial class StabCharacter : CharacterBody2D
{
	private enum AiState { Pausing, Moving }

	[Export] public float BaseMoveSpeed { get; set; } = 100f;

	// Base sizes at 1080p reference
	private const float BaseRadius = 25f;
	private const float BaseFontSize = 14f;

	// NPC timing ranges
	private const float MinPauseTime = 0.5f;
	private const float MaxPauseTime = 3.0f;
	private const float MinMoveTime = 1.5f;
	private const float MaxMoveTime = 5.0f;
	private const float WallAvoidanceDistance = 50f;

	public string CharacterId { get; set; } = "";
	public string CharacterName { get; set; } = "";
	public Color CharacterColor { get; set; } = Colors.White;
	public bool IsNpc { get; private set; }

	private PlayerController? _controller;
	private ColorRect _colorRect = null!;
	private Label _nameLabel = null!;
	private CollisionShape2D _collisionShape = null!;
	private Sprite2D _sprite = null!;
	private float _scaleFactor = 1f;
	private float _currentMoveSpeed;

	// NPC AI state
	private AiState _aiState = AiState.Pausing;
	private float _stateTimer;
	private Vector2 _moveDirection = Vector2.Zero;
	private RandomNumberGenerator _rng = new();
	private Vector2 _gameAreaBounds = Vector2.Zero;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");
		_colorRect = GetNode<ColorRect>("Sprite2D/ColorRect");
		_nameLabel = GetNode<Label>("NameLabel");
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		_currentMoveSpeed = BaseMoveSpeed;

		_rng.Randomize();

		UpdateVisuals();
	}

	/// <summary>
	/// Initialize as a player-controlled character
	/// </summary>
	public void InitializeAsPlayer(PlayerController controller)
	{
		IsNpc = false;
		_controller = controller;
		CharacterId = controller.PlayerId;
		CharacterName = controller.PlayerName;
		CharacterColor = controller.PlayerColor;

		UpdateVisuals();
	}

	/// <summary>
	/// Initialize as an NPC
	/// </summary>
	public void InitializeAsNpc(string id, string name, Color color)
	{
		IsNpc = true;
		_controller = null;
		CharacterId = id;
		CharacterName = name;
		CharacterColor = color;

		// Start in pausing state
		_aiState = AiState.Pausing;
		_stateTimer = _rng.RandfRange(MinPauseTime, MaxPauseTime);

		UpdateVisuals();
	}

	private void UpdateVisuals()
	{
		if (_colorRect != null)
		{
			_colorRect.Color = CharacterColor;
		}

		if (_nameLabel != null)
		{
			_nameLabel.Text = CharacterName;
		}
	}

	public void SetColor(Color color)
	{
		CharacterColor = color;
		if (_colorRect != null)
		{
			_colorRect.Color = color;
		}
	}

	public void SetScale(float scaleFactor)
	{
		_scaleFactor = scaleFactor;

		// Scale movement speed
		_currentMoveSpeed = BaseMoveSpeed * scaleFactor;

		// Scale the sprite (visual)
		if (_sprite != null)
		{
			_sprite.Scale = new Vector2(scaleFactor, scaleFactor);
		}

		// Scale the collision shape
		if (_collisionShape != null && _collisionShape.Shape is CircleShape2D circleShape)
		{
			circleShape.Radius = BaseRadius * scaleFactor;
		}

		// Scale and reposition the name label
		if (_nameLabel != null)
		{
			int fontSize = Mathf.RoundToInt(BaseFontSize * scaleFactor);
			_nameLabel.AddThemeFontSizeOverride("font_size", fontSize);

			float labelOffset = (BaseRadius + 20f) * scaleFactor;
			_nameLabel.Position = new Vector2(-50f * scaleFactor, -labelOffset);
			_nameLabel.Size = new Vector2(100f * scaleFactor, 20f * scaleFactor);
		}
	}

	public void SetGameBounds(Vector2 bounds)
	{
		_gameAreaBounds = bounds;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsNpc)
		{
			ProcessNpcAi((float)delta);
		}
		else
		{
			ProcessPlayerInput();
		}

		MoveAndSlide();

		// Check if NPC hit a wall and needs to change direction
		CheckWallCollision();
	}

	private void ProcessPlayerInput()
	{
		if (_controller == null)
		{
			Velocity = Vector2.Zero;
			return;
		}

		var input = _controller.CurrentInput;
		Vector2 inputVector = new Vector2(input.Movement.X, input.Movement.Y);
		float inputMag = inputVector.Length();
		float playerspeed = _currentMoveSpeed;
		if (inputMag > 0.6f)
		{
			playerspeed += _currentMoveSpeed * (((inputMag > 1 ) ? 1 : inputMag) - 0.6f) / 0.4f;

		}

		Velocity = new Vector2(input.Movement.X, input.Movement.Y).Normalized() * playerspeed;

		// Handle action buttons
		if (input.Action1)
		{
			OnAction1();
		}
		if (input.Action2)
		{
			OnAction2();
		}
	}

	private void ProcessNpcAi(float delta)
	{
		_stateTimer -= delta;

		if (_stateTimer <= 0)
		{
			// Switch state
			if (_aiState == AiState.Pausing)
			{
				// Start moving in a random direction
				_aiState = AiState.Moving;
				_stateTimer = _rng.RandfRange(MinMoveTime, MaxMoveTime);
				PickNewDirection();
			}
			else
			{
				// Start pausing
				_aiState = AiState.Pausing;
				_stateTimer = _rng.RandfRange(MinPauseTime, MaxPauseTime);
				_moveDirection = Vector2.Zero;
			}
		}

		// Apply velocity based on current state
		if (_aiState == AiState.Moving)
		{
			Velocity = _moveDirection * _currentMoveSpeed;
		}
		else
		{
			Velocity = Vector2.Zero;
		}
	}

	private void CheckWallCollision()
	{
		if (!IsNpc || _aiState != AiState.Moving) return;

		// Check if we hit something
		if (GetSlideCollisionCount() > 0)
		{
			// Stop and switch to pausing state
			_aiState = AiState.Pausing;
			_stateTimer = _rng.RandfRange(MinPauseTime, MaxPauseTime);
			_moveDirection = Vector2.Zero;
			Velocity = Vector2.Zero;
		}
	}

	private void PickNewDirection()
	{
		// Try up to 10 times to find a valid direction
		for (int i = 0; i < 10; i++)
		{
			float angle = _rng.RandfRange(0, Mathf.Tau);
			var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

			if (IsDirectionValid(direction))
			{
				_moveDirection = direction;
				return;
			}
		}

		// Fallback: move toward center
		var center = _gameAreaBounds / 2;
		_moveDirection = (center - Position).Normalized();
	}

	private bool IsDirectionValid(Vector2 direction)
	{
		if (_gameAreaBounds == Vector2.Zero) return true;

		float margin = WallAvoidanceDistance * _scaleFactor;

		// Check if near left wall and trying to go left
		if (Position.X < margin && direction.X < 0) return false;

		// Check if near right wall and trying to go right
		if (Position.X > _gameAreaBounds.X - margin && direction.X > 0) return false;

		// Check if near top wall and trying to go up
		if (Position.Y < margin && direction.Y < 0) return false;

		// Check if near bottom wall and trying to go down
		if (Position.Y > _gameAreaBounds.Y - margin && direction.Y > 0) return false;

		return true;
	}

	private void OnAction1()
	{
		// Primary action (e.g., attack/stab)
		// TODO: Implement game-specific action
	}

	private void OnAction2()
	{
		// Secondary action (e.g., dash/block)
		// TODO: Implement game-specific action
	}

	/// <summary>
	/// Called when the player shakes their phone to perform a stab action
	/// </summary>
	public void OnPlayerStab()
	{
		GD.Print($"{CharacterName} is stabbing!");
		// TODO: Implement stab animation/effect
	}
}
