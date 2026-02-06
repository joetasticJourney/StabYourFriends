using Godot;

namespace StabYourFriends.Game.PowerUps;

public partial class PowerUp : Node2D
{
	private const float BasePickupRadius = 30f;
	private const float BaseBoxSize = 40f;
	private const float GlowPulseSpeed = 3f;
	private const float BaseFontSize = 26f;

	public string Label { get; protected set; } = "??";
	protected Color PowerUpColor { get; set; } = new Color(0.2f, 0.8f, 0.9f);
	protected string? SoundPath { get; set; }

	private float _scaleFactor = 1f;
	private float _time;
	private GameWorld _gameWorld = null!;

	public void Initialize(Vector2 position, float scaleFactor, GameWorld gameWorld)
	{
		Position = position;
		_scaleFactor = scaleFactor;
		_gameWorld = gameWorld;
		ZIndex = 10;
	}

	public void UpdateScale(float scaleFactor)
	{
		_scaleFactor = scaleFactor;
	}

	public override void _PhysicsProcess(double delta)
	{
		_time += (float)delta;

		float pickupRadius = 2.0f * BasePickupRadius * _scaleFactor;
		var nearbyPlayers = _gameWorld.GetNearbyPlayerCharacters(Position, pickupRadius);

		if (nearbyPlayers.Count > 0)
		{
			Pickup(nearbyPlayers[0]);
			QueueFree();
		}
	}

	public override void _Process(double delta)
	{
		QueueRedraw();
	}

	protected virtual void Pickup(StabCharacter character)
	{
		GD.Print($"[PowerUp] {Label} picked up by {character.CharacterName}");
		PlayPickupSound();
	}

	private void PlayPickupSound()
	{
		if (string.IsNullOrEmpty(SoundPath)) return;

		var sound = GD.Load<AudioStream>(SoundPath);
		if (sound == null) return;

		var player = new AudioStreamPlayer();
		player.Stream = sound;
		player.Bus = "Master";
		player.VolumeDb = -12f; // 25% volume
		_gameWorld.AddChild(player);
		player.Play();
		player.Finished += () => player.QueueFree();
	}

	public override void _Draw()
	{
		float boxSize = BaseBoxSize * _scaleFactor;
		float half = boxSize / 2f;

		// Pulsing glow alpha
		float glowAlpha = 0.3f + 0.2f * Mathf.Sin(_time * GlowPulseSpeed);

		// Draw glow (larger box behind)
		float glowPad = 6f * _scaleFactor;
		var glowRect = new Rect2(-half - glowPad, -half - glowPad, boxSize + glowPad * 2, boxSize + glowPad * 2);
		var glowColor = new Color(PowerUpColor.R, PowerUpColor.G, PowerUpColor.B, glowAlpha);
		DrawRect(glowRect, glowColor);

		// Draw filled box
		var boxRect = new Rect2(-half, -half, boxSize, boxSize);
		var boxColor = new Color(PowerUpColor.R, PowerUpColor.G, PowerUpColor.B, 0.85f);
		DrawRect(boxRect, boxColor);

		// Draw border
		var borderColor = new Color(1f, 1f, 1f, 0.9f);
		DrawRect(boxRect, borderColor, false, 2f * _scaleFactor);

		// Draw label text (bold, larger)
		int fontSize = Mathf.RoundToInt(BaseFontSize * _scaleFactor);
		var font = ThemeDB.FallbackFont;
		if (font == null) return;

		var textSize = font.GetStringSize(Label, HorizontalAlignment.Center, -1, fontSize);
		var textPos = new Vector2(-textSize.X / 2f, textSize.Y / 4f);
		DrawString(font, textPos, Label, HorizontalAlignment.Center, -1, fontSize, Colors.White);
	}
}
