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

	// Grapple constants
	private const float GrappleRange = 80f;
	private const float GrappleOrbitDistance = 55f;
	private const float GrappleRotationSpeed = 3f;

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

	// Grapple state
	private bool _isGrappling;
	private bool _isGrappled;
	private StabCharacter? _grappleTarget;
	private StabCharacter? _grappledBy;
	private bool _action1WasPressed;
	private float _grappleAngle;

	// Reference to game world for character lookup
	private GameWorld? _gameWorld;

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

		// Get reference to GameWorld parent
		_gameWorld = GetParent<GameWorld>();

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
		// Check if grapple target was freed
		if (_isGrappling && !IsInstanceValid(_grappleTarget))
		{
			GD.Print($"[Grapple] {CharacterName}: Target was freed, clearing grapple state");
			ClearGrappleState();
		}
		if (_isGrappled && !IsInstanceValid(_grappledBy))
		{
			GD.Print($"[Grapple] {CharacterName}: Grappler was freed, clearing grappled state");
			ClearGrappledState();
		}

		// If grappled, cannot move
		if (_isGrappled)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		if (IsNpc)
		{
			ProcessNpcAi((float)delta);
		}
		else
		{
			ProcessPlayerInput((float)delta);
		}

		// Only call MoveAndSlide if not grappling (grappling uses direct position)
		if (!_isGrappling)
		{
			MoveAndSlide();
		}

		// Check if NPC hit a wall and needs to change direction
		CheckWallCollision();
	}

	private void ProcessPlayerInput(float delta)
	{
		if (_controller == null)
		{
			Velocity = Vector2.Zero;
			return;
		}

		var input = _controller.CurrentInput;

		// Handle action button toggle (detect press, not hold)
		bool action1Pressed = input.Action1 && !_action1WasPressed;
		_action1WasPressed = input.Action1;

		if (action1Pressed)
		{
			OnAction1();
		}
		if (input.Action2)
		{
			OnAction2();
		}

		// Grappling movement - orbit around target
		if (_isGrappling && _grappleTarget != null)
		{
			Vector2 inputVector = new Vector2(input.Movement.X, input.Movement.Y);
			float scaledOrbitDistance = GrappleOrbitDistance * _scaleFactor;

			if (inputVector.LengthSquared() > 0.01f)
			{
				// Target position is opposite side of target from input direction
				Vector2 inputNormalized = inputVector.Normalized();
				Vector2 targetOrbitPos = _grappleTarget.Position + inputNormalized * scaledOrbitDistance;

				// Calculate target angle from the desired position
				Vector2 toTargetPos = targetOrbitPos - _grappleTarget.Position;
				float targetAngle = Mathf.Atan2(toTargetPos.Y, toTargetPos.X);

				// Calculate angular velocity based on character speed and orbit circumference
				// Arc length = angle * radius, so angular speed = linear speed / radius
				float angularSpeed = _currentMoveSpeed / scaledOrbitDistance;

				// Find shortest rotation direction to target angle
				float angleDiff = targetAngle - _grappleAngle;
				// Normalize to -PI to PI
				while (angleDiff > Mathf.Pi) angleDiff -= Mathf.Tau;
				while (angleDiff < -Mathf.Pi) angleDiff += Mathf.Tau;

				// Move towards target angle at character speed
				float maxRotation = angularSpeed * delta;
				if (Mathf.Abs(angleDiff) <= maxRotation)
				{
					_grappleAngle = targetAngle;
				}
				else
				{
					_grappleAngle += Mathf.Sign(angleDiff) * maxRotation;
				}

				GD.Print($"[Grapple] {CharacterName}: Orbiting {_grappleTarget.CharacterName} - angle={Mathf.RadToDeg(_grappleAngle):F1}°, targetAngle={Mathf.RadToDeg(targetAngle):F1}°");
			}

			// Position self at fixed distance from target
			var offset = new Vector2(
				Mathf.Cos(_grappleAngle),
				Mathf.Sin(_grappleAngle)
			) * scaledOrbitDistance;

			Position = _grappleTarget.Position + offset;
			Velocity = Vector2.Zero;
			return;
		}

		// Normal movement
		Vector2 moveInput = new Vector2(input.Movement.X, input.Movement.Y);
		float inputMag = moveInput.Length();
		float playerspeed = _currentMoveSpeed;
		if (inputMag > 0.6f)
		{
			playerspeed += _currentMoveSpeed * (((inputMag > 1 ) ? 1 : inputMag) - 0.6f) / 0.4f;
		}

		Velocity = moveInput.Normalized() * playerspeed;
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
		GD.Print($"[Grapple] {CharacterName}: Action1 pressed (isGrappled={_isGrappled}, isGrappling={_isGrappling})");

		// Can't act while grappled
		if (_isGrappled)
		{
			GD.Print($"[Grapple] {CharacterName}: Cannot act - currently grappled by {_grappledBy?.CharacterName ?? "unknown"}");
			return;
		}

		if (_isGrappling)
		{
			ReleaseGrapple();
		}
		else
		{
			TryStartGrapple();
		}
	}

	private void TryStartGrapple()
	{
		GD.Print($"[Grapple] {CharacterName}: Attempting to find grapple target at position {Position}");
		var target = FindGrappleTarget();
		if (target != null)
		{
			GD.Print($"[Grapple] {CharacterName}: Found target {target.CharacterName} at position {target.Position}");
			StartGrapple(target);
		}
		else
		{
			GD.Print($"[Grapple] {CharacterName}: No valid target found within range {GrappleRange * _scaleFactor}");
		}
	}

	private StabCharacter? FindGrappleTarget()
	{
		if (_gameWorld == null)
		{
			GD.Print($"[Grapple] {CharacterName}: FindGrappleTarget - GameWorld is null!");
			return null;
		}

		float scaledRange = GrappleRange * _scaleFactor;
		var nearbyCharacters = _gameWorld.GetNearbyCharacters(Position, scaledRange);
		GD.Print($"[Grapple] {CharacterName}: Found {nearbyCharacters.Count} characters within range {scaledRange}");

		StabCharacter? closestPlayer = null;
		StabCharacter? closestNpc = null;
		float closestPlayerDist = float.MaxValue;
		float closestNpcDist = float.MaxValue;

		foreach (var character in nearbyCharacters)
		{
			// Skip self
			if (character == this)
			{
				GD.Print($"[Grapple] {CharacterName}: Skipping self");
				continue;
			}

			// Skip characters already being grappled
			if (character._isGrappled)
			{
				GD.Print($"[Grapple] {CharacterName}: Skipping {character.CharacterName} - already grappled");
				continue;
			}

			float distance = Position.DistanceTo(character.Position);
			GD.Print($"[Grapple] {CharacterName}: Checking {character.CharacterName} - distance={distance:F1}, isNpc={character.IsNpc}");

			if (!character.IsNpc)
			{
				// Player-controlled character
				if (distance < closestPlayerDist)
				{
					closestPlayerDist = distance;
					closestPlayer = character;
					GD.Print($"[Grapple] {CharacterName}: {character.CharacterName} is new closest player (dist={distance:F1})");
				}
			}
			else
			{
				// NPC
				if (distance < closestNpcDist)
				{
					closestNpcDist = distance;
					closestNpc = character;
					GD.Print($"[Grapple] {CharacterName}: {character.CharacterName} is new closest NPC (dist={distance:F1})");
				}
			}
		}

		// Prioritize player-controlled targets
		if (closestPlayer != null)
		{
			GD.Print($"[Grapple] {CharacterName}: Selected player target {closestPlayer.CharacterName}");
			return closestPlayer;
		}

		if (closestNpc != null)
		{
			GD.Print($"[Grapple] {CharacterName}: No players in range, selected NPC target {closestNpc.CharacterName}");
			return closestNpc;
		}

		GD.Print($"[Grapple] {CharacterName}: No valid targets found");
		return null;
	}

	private void StartGrapple(StabCharacter target)
	{
		_isGrappling = true;
		_grappleTarget = target;

		// Calculate initial angle from target to self
		Vector2 toSelf = Position - target.Position;
		_grappleAngle = Mathf.Atan2(toSelf.Y, toSelf.X);

		GD.Print($"[Grapple] {CharacterName}: Starting grapple on {target.CharacterName}");
		GD.Print($"[Grapple] {CharacterName}: Initial angle = {Mathf.RadToDeg(_grappleAngle):F1} degrees");
		GD.Print($"[Grapple] {CharacterName}: Orbit distance = {GrappleOrbitDistance * _scaleFactor}");

		// Tell target they're being grappled
		target.OnGrappled(this);

		GD.Print($"[Grapple] === {CharacterName} GRAPPLED {target.CharacterName} ===");
	}

	private void ReleaseGrapple()
	{
		GD.Print($"[Grapple] {CharacterName}: Releasing grapple");
		if (_grappleTarget != null && IsInstanceValid(_grappleTarget))
		{
			GD.Print($"[Grapple] === {CharacterName} RELEASED {_grappleTarget.CharacterName} ===");
			_grappleTarget.OnReleased();
		}
		else
		{
			GD.Print($"[Grapple] {CharacterName}: No valid target to release");
		}
		ClearGrappleState();
	}

	private void OnGrappled(StabCharacter grappler)
	{
		GD.Print($"[Grapple] {CharacterName}: Being grappled by {grappler.CharacterName}");
		_isGrappled = true;
		_grappledBy = grappler;
	}

	private void OnReleased()
	{
		GD.Print($"[Grapple] {CharacterName}: Released from grapple");
		ClearGrappledState();
	}

	private void ClearGrappleState()
	{
		GD.Print($"[Grapple] {CharacterName}: Clearing grapple state (was grappling: {_grappleTarget?.CharacterName ?? "none"})");
		_isGrappling = false;
		_grappleTarget = null;
	}

	private void ClearGrappledState()
	{
		GD.Print($"[Grapple] {CharacterName}: Clearing grappled state (was grappled by: {_grappledBy?.CharacterName ?? "none"})");
		_isGrappled = false;
		_grappledBy = null;
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

	/// <summary>
	/// Clean up grapple state before being removed from the game.
	/// Call this before QueueFree() to properly release any grapple relationships.
	/// </summary>
	public void CleanupGrapple()
	{
		GD.Print($"[Grapple] {CharacterName}: CleanupGrapple called (isGrappling={_isGrappling}, isGrappled={_isGrappled})");

		// If we're grappling someone, release them
		if (_isGrappling)
		{
			GD.Print($"[Grapple] {CharacterName}: Cleaning up - releasing grapple on {_grappleTarget?.CharacterName ?? "unknown"}");
			ReleaseGrapple();
		}

		// If we're being grappled, the grappler will detect via IsInstanceValid check
		// and clean up their own state in _PhysicsProcess
		if (_isGrappled)
		{
			GD.Print($"[Grapple] {CharacterName}: Cleaning up - was being grappled by {_grappledBy?.CharacterName ?? "unknown"}");
		}
	}
}
