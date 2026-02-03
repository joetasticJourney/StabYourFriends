#nullable enable

using Godot;
using StabYourFriends.Controllers;

namespace StabYourFriends.Game;

public partial class StabCharacter : CharacterBody2D
{
	private enum AiState { Pausing, Moving }
	private enum FacingDirection { Down, Up, Left, Right }

	[Export] public float BaseMoveSpeed { get; set; } = 100f;

	// Base sizes at 1080p reference
	private const float BaseRadius = 25f;
	private const float BaseFontSize = 14f;

	// Sprite constants
	private const int SpriteFrameCount = 8;
	private const int SpriteFrameWidth = 96;
	private const int SpriteFrameHeight = 80;
	private const float AnimationFps = 10f;

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

	// Health and death constants
	private const int MaxHealth = 10;
	private const int StabDamage = 1;
	private const float BodyFadeDelay = 5f;
	private const float BloodFadeDelay = 10f;
	private const float FadeDuration = 1f;
	private const float BloodPoolMaxScale = 2f;
	private const float BloodPoolExpandDuration = 0.5f;

	// Stab particle constants
	private const float StabParticleDistance = BaseRadius * 10f;
	private const float StabParticleSpreadAngle = Mathf.Pi / 4f; // 45 degrees
	private const float StabParticleLifetime = 0.6f;
	private const int StabParticleCount = 120;

	public string CharacterId { get; set; } = "";
	public string CharacterName { get; set; } = "";
	public Color CharacterColor { get; set; } = Colors.White;
	public bool IsNpc { get; private set; }
	public int Health { get; private set; } = MaxHealth;
	public bool IsDead { get; private set; }

	private PlayerController? _controller;
	private AnimatedSprite2D _animatedSprite = null!;
	private Label _nameLabel = null!;
	private CollisionShape2D _collisionShape = null!;
	private float _scaleFactor = 1f;
	private float _currentMoveSpeed;
	private FacingDirection _facing = FacingDirection.Down;
	private bool _isMoving;

	// Grapple state
	private bool _isGrappling;
	private bool _isGrappled;
	private StabCharacter? _grappleTarget;
	private StabCharacter? _grappledBy;
	private bool _action1WasPressed;
	private float _grappleAngle;

	// Reference to game world for character lookup
	private GameWorld? _gameWorld;

	// Death state
	private float _deathTimer;
	private float _bloodTimer;
	private bool _bodyFading;
	private bool _bloodFading;
	private ColorRect? _skullOverlay;
	private ColorRect? _bloodPool;
	private float _bloodExpandTimer;

	// NPC AI state
	private AiState _aiState = AiState.Pausing;
	private float _stateTimer;
	private Vector2 _moveDirection = Vector2.Zero;
	private RandomNumberGenerator _rng = new();
	private Vector2 _gameAreaBounds = Vector2.Zero;

	public override void _Ready()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_nameLabel = GetNode<Label>("NameLabel");
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		_currentMoveSpeed = BaseMoveSpeed;

		_rng.Randomize();

		// Get reference to GameWorld parent
		_gameWorld = GetParent<GameWorld>();

		BuildSpriteFrames();
		UpdateVisuals();
		PlayAnimation();

		// Re-apply scale in case SetScale was called before _Ready
		if (_scaleFactor != 1f)
		{
			SetScale(_scaleFactor);
		}
	}

	private void BuildSpriteFrames()
	{
		var spriteFrames = new SpriteFrames();

		// Remove the default animation
		if (spriteFrames.HasAnimation("default"))
		{
			spriteFrames.RemoveAnimation("default");
		}

		// Define all animations to load
		var animations = new (string animName, string path)[]
		{
			("idle_down", "res://Sprites/IDLE/idle_down.png"),
			("idle_up", "res://Sprites/IDLE/idle_up.png"),
			("idle_left", "res://Sprites/IDLE/idle_left.png"),
			("idle_right", "res://Sprites/IDLE/idle_right.png"),
			("run_down", "res://Sprites/RUN/run_down.png"),
			("run_up", "res://Sprites/RUN/run_up.png"),
			("run_left", "res://Sprites/RUN/run_left.png"),
			("run_right", "res://Sprites/RUN/run_right.png"),
		};

		foreach (var (animName, path) in animations)
		{
			spriteFrames.AddAnimation(animName);
			spriteFrames.SetAnimationSpeed(animName, AnimationFps);
			spriteFrames.SetAnimationLoop(animName, true);

			var sheetTexture = GD.Load<Texture2D>(path);
			if (sheetTexture == null)
			{
				GD.PrintErr($"Failed to load sprite: {path}");
				continue;
			}

			for (int i = 0; i < SpriteFrameCount; i++)
			{
				var atlas = new AtlasTexture();
				atlas.Atlas = sheetTexture;
				atlas.Region = new Rect2(
					i * SpriteFrameWidth, 0,
					SpriteFrameWidth, SpriteFrameHeight
				);
				spriteFrames.AddFrame(animName, atlas);
			}
		}

		_animatedSprite.SpriteFrames = spriteFrames;
	}

	private void PlayAnimation()
	{
		string dirSuffix = _facing switch
		{
			FacingDirection.Up => "up",
			FacingDirection.Down => "down",
			FacingDirection.Left => "left",
			FacingDirection.Right => "right",
			_ => "down"
		};

		string animName = (_isMoving ? "run_" : "idle_") + dirSuffix;

		if (_animatedSprite.Animation != animName)
		{
			_animatedSprite.Play(animName);
		}
	}

	private void UpdateFacingFromVelocity(Vector2 velocity)
	{
		if (velocity.LengthSquared() < 0.01f)
		{
			_isMoving = false;
			PlayAnimation();
			return;
		}

		_isMoving = true;

		// Pick direction based on which axis has the larger component
		if (Mathf.Abs(velocity.X) >= Mathf.Abs(velocity.Y))
		{
			_facing = velocity.X >= 0 ? FacingDirection.Right : FacingDirection.Left;
		}
		else
		{
			_facing = velocity.Y >= 0 ? FacingDirection.Down : FacingDirection.Up;
		}

		PlayAnimation();
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
		if (_nameLabel != null)
		{
			_nameLabel.Text = CharacterName;
		}
	}

	public void SetColor(Color color)
	{
		CharacterColor = color;
	}

	public void SetScale(float scaleFactor)
	{
		_scaleFactor = scaleFactor;

		// Scale movement speed
		_currentMoveSpeed = BaseMoveSpeed * scaleFactor;

		// Scale the animated sprite
		if (_animatedSprite != null)
		{
			_animatedSprite.Scale = new Vector2(scaleFactor * 3f, scaleFactor * 3f);
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
		// Handle death state
		if (IsDead)
		{
			ProcessDeathState((float)delta);
			return;
		}

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
			UpdateFacingFromVelocity(Vector2.Zero);
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

		// Update animation based on current velocity
		UpdateFacingFromVelocity(Velocity);

		// Check if NPC hit a wall and needs to change direction
		CheckWallCollision();
	}

	private void ProcessDeathState(float delta)
	{
		// Expand blood pool
		if (_bloodPool != null && _bloodExpandTimer < BloodPoolExpandDuration)
		{
			_bloodExpandTimer += delta;
			float t = Mathf.Min(_bloodExpandTimer / BloodPoolExpandDuration, 1f);
			float scale = Mathf.Lerp(0.1f, BloodPoolMaxScale, t) * _scaleFactor;
			_bloodPool.Scale = new Vector2(scale, scale);
		}

		// Body fade timer
		_deathTimer += delta;
		if (!_bodyFading && _deathTimer >= BodyFadeDelay)
		{
			_bodyFading = true;
		}

		// Fade body
		if (_bodyFading)
		{
			float fadeProgress = (_deathTimer - BodyFadeDelay) / FadeDuration;
			if (fadeProgress >= 1f)
			{
				if (_animatedSprite != null) _animatedSprite.Visible = false;
				if (_nameLabel != null) _nameLabel.Visible = false;
				if (_skullOverlay != null) _skullOverlay.Visible = false;
			}
			else
			{
				float alpha = 1f - fadeProgress;
				if (_animatedSprite != null) _animatedSprite.Modulate = new Color(1, 1, 1, alpha);
				if (_nameLabel != null) _nameLabel.Modulate = new Color(1, 1, 1, alpha);
				if (_skullOverlay != null) _skullOverlay.Modulate = new Color(1, 1, 1, alpha * 0.7f);
			}
		}

		// Blood fade timer
		_bloodTimer += delta;
		if (!_bloodFading && _bloodTimer >= BloodFadeDelay)
		{
			_bloodFading = true;
		}

		// Fade blood
		if (_bloodFading && _bloodPool != null)
		{
			float fadeProgress = (_bloodTimer - BloodFadeDelay) / FadeDuration;
			if (fadeProgress >= 1f)
			{
				_bloodPool.Visible = false;
			}
			else
			{
				float alpha = 1f - fadeProgress;
				_bloodPool.Modulate = new Color(1, 1, 1, alpha);
			}
		}
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
			playerspeed += _currentMoveSpeed * (((inputMag > 1 ) ? 1 : inputMag) - 0.6f) / 0.2f;
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
		GD.Print($"[Grapple] {CharacterName}: Action1 pressed (isGrappled={_isGrappled}, isGrappling={_isGrappling}, isDead={IsDead})");

		// Can't act while dead
		if (IsDead)
		{
			GD.Print($"[Grapple] {CharacterName}: Cannot act - dead");
			return;
		}

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

			// Skip dead characters
			if (character.IsDead)
			{
				GD.Print($"[Grapple] {CharacterName}: Skipping {character.CharacterName} - dead");
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
		if (IsDead)
		{
			GD.Print($"[Stab] {CharacterName} cannot stab - dead");
			return;
		}

		GD.Print($"[Stab] {CharacterName} is stabbing!");

		// If grappling someone, deal damage and spawn particles
		if (_isGrappling && _grappleTarget != null && IsInstanceValid(_grappleTarget))
		{
			GD.Print($"[Stab] {CharacterName} stabs {_grappleTarget.CharacterName} for {StabDamage} damage!");

			// Spawn stab particle effect at the grappled target
			Vector2 stabDirection = (_grappleTarget.Position - Position).Normalized();
			SpawnStabParticles(_grappleTarget.GlobalPosition, stabDirection);

			_grappleTarget.TakeDamage(StabDamage, this);
		}
	}

	/// <summary>
	/// Spawns a 45-degree cone of particles at the given position in the given direction.
	/// </summary>
	private void SpawnStabParticles(Vector2 spawnPosition, Vector2 direction)
	{
		float scaledDistance = StabParticleDistance * _scaleFactor;
		float angle = Mathf.Atan2(direction.Y, direction.X);

		var particles = new GpuParticles2D();
		particles.Emitting = true;
		particles.Amount = StabParticleCount;
		particles.Lifetime = StabParticleLifetime;
		particles.OneShot = true;
		particles.Explosiveness = 0.9f;
		particles.GlobalPosition = spawnPosition;

		var material = new ParticleProcessMaterial();

		// Direction: angle of stabber -> victim, converted to Godot's particle direction system
		// ParticleProcessMaterial uses 3D direction, with spread for the cone
		material.Direction = new Vector3(1, 0, 0);
		material.Spread = Mathf.RadToDeg(StabParticleSpreadAngle / 2f); // spread is half-angle

		// Speed = distance / lifetime so particles travel the full distance
		float speed = scaledDistance / StabParticleLifetime;
		material.InitialVelocityMin = speed * 0.8f;
		material.InitialVelocityMax = speed * 1.2f;

		// Particle size
		float particleSize = 3f * _scaleFactor;
		material.ScaleMin = particleSize;
		material.ScaleMax = particleSize * 1.5f;

		// Fade out over lifetime
		material.Color = new Color(0.8f, 0f, 0f, 1f); // Red blood color

		// Rotate the emission to match the stab direction
		particles.Rotation = angle;

		// Light damping so particles slow near the end
		material.DampingMin = speed * 0.05f;
		material.DampingMax = speed * 0.1f;

		// Gravity off
		material.Gravity = Vector3.Zero;

		particles.ProcessMaterial = material;

		// Add to the game world so it stays in world space
		if (_gameWorld != null)
		{
			_gameWorld.AddChild(particles);
		}
		else
		{
			GetTree().CurrentScene.AddChild(particles);
		}

		// Auto-free after particles finish
		var timer = GetTree().CreateTimer(StabParticleLifetime + 0.5f);
		timer.Timeout += () =>
		{
			if (IsInstanceValid(particles))
			{
				particles.QueueFree();
			}
		};

		// Draw debug triangle for the cone
		SpawnDebugCone(spawnPosition, angle, scaledDistance);

		GD.Print($"[Stab] Spawned particle effect at {spawnPosition}, direction={direction}, angle={Mathf.RadToDeg(angle):F1}°");
	}

	private void SpawnDebugCone(Vector2 origin, float angle, float distance)
	{
		float halfSpread = StabParticleSpreadAngle / 2f;

		// Calculate the three points of the triangle
		Vector2 tip1 = origin + new Vector2(
			Mathf.Cos(angle - halfSpread),
			Mathf.Sin(angle - halfSpread)
		) * distance;

		Vector2 tip2 = origin + new Vector2(
			Mathf.Cos(angle + halfSpread),
			Mathf.Sin(angle + halfSpread)
		) * distance;

		var line = new Line2D();
		line.Points = new Vector2[] { origin, tip1, tip2, origin };
		line.Width = 2f;
		line.DefaultColor = new Color(1f, 1f, 0f, 0.8f); // Yellow

		if (_gameWorld != null)
		{
			_gameWorld.AddChild(line);
		}
		else
		{
			GetTree().CurrentScene.AddChild(line);
		}

		// Remove after particles finish
		var timer = GetTree().CreateTimer(StabParticleLifetime + 0.5f);
		timer.Timeout += () =>
		{
			if (IsInstanceValid(line))
			{
				line.QueueFree();
			}
		};
	}

	/// <summary>
	/// Deal damage to this character
	/// </summary>
	public void TakeDamage(int amount, StabCharacter? attacker = null)
	{
		if (IsDead) return;

		Health -= amount;
		GD.Print($"[Health] {CharacterName} took {amount} damage from {attacker?.CharacterName ?? "unknown"}. Health: {Health}/{MaxHealth}");

		if (Health <= 0)
		{
			Health = 0;
			Die(attacker);
		}
	}

	private void Die(StabCharacter? killer = null)
	{
		if (IsDead) return;

		IsDead = true;
		GD.Print($"[Death] {CharacterName} was killed by {killer?.CharacterName ?? "unknown"}!");

		// Release any grapple relationships
		if (_isGrappling)
		{
			ReleaseGrapple();
		}
		if (_isGrappled && _grappledBy != null && IsInstanceValid(_grappledBy))
		{
			_grappledBy.ClearGrappleState();
		}
		ClearGrappledState();

		// Stop movement
		Velocity = Vector2.Zero;

		// Stop animation
		if (_animatedSprite != null)
		{
			_animatedSprite.Stop();
		}

		// Create death visuals
		CreateDeathVisuals();
	}

	private void CreateDeathVisuals()
	{
		float baseSize = BaseRadius * 2 * _scaleFactor;

		// Create blood pool (behind character)
		_bloodPool = new ColorRect();
		_bloodPool.Color = new Color(0.5f, 0f, 0f, 0.8f); // Dark red
		_bloodPool.Size = new Vector2(baseSize, baseSize);
		_bloodPool.Position = new Vector2(-baseSize / 2, -baseSize / 2);
		_bloodPool.Scale = new Vector2(0.1f, 0.1f);
		_bloodPool.ZIndex = -1;
		AddChild(_bloodPool);

		// Create skull overlay (on top of character)
		_skullOverlay = new ColorRect();
		_skullOverlay.Color = new Color(1f, 1f, 1f, 0.7f); // White, semi-transparent
		float skullSize = baseSize * 0.6f;
		_skullOverlay.Size = new Vector2(skullSize, skullSize);
		_skullOverlay.Position = new Vector2(-skullSize / 2, -skullSize / 2);
		_skullOverlay.ZIndex = 10;
		AddChild(_skullOverlay);

		// Add skull "X" eyes using labels
		var skullLabel = new Label();
		skullLabel.Text = "X X";
		skullLabel.HorizontalAlignment = HorizontalAlignment.Center;
		skullLabel.VerticalAlignment = VerticalAlignment.Center;
		skullLabel.Size = _skullOverlay.Size;
		skullLabel.AddThemeColorOverride("font_color", Colors.Black);
		int fontSize = Mathf.RoundToInt(BaseFontSize * _scaleFactor * 1.5f);
		skullLabel.AddThemeFontSizeOverride("font_size", fontSize);
		_skullOverlay.AddChild(skullLabel);

		GD.Print($"[Death] Created death visuals for {CharacterName}");
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
