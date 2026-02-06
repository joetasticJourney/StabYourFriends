#nullable enable

using Godot;

namespace StabYourFriends.Game;

public partial class VipCharacter : StabCharacter
{
	private static readonly Color VipColor = new(0.2f, 0.6f, 1.0f);

	private AudioStreamPlayer2D? _vipDeathPlayer1;
	private AudioStreamPlayer2D? _vipDeathPlayer2;
	private RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		base._Ready();
		Health = 5;
		KillPointValue = 1;
		CharacterColor = VipColor;
		GetNode<AnimatedSprite2D>("AnimatedSprite2D").SelfModulate = VipColor;

		_vipDeathPlayer1 = new AudioStreamPlayer2D();
		_vipDeathPlayer1.Stream = GD.Load<AudioStream>("res://Sounds/VIP Death 1.mp3");
		_vipDeathPlayer1.MaxDistance = 2000;
		AddChild(_vipDeathPlayer1);

		_vipDeathPlayer2 = new AudioStreamPlayer2D();
		_vipDeathPlayer2.Stream = GD.Load<AudioStream>("res://Sounds/VIP Death 2.mp3");
		_vipDeathPlayer2.MaxDistance = 2000;
		AddChild(_vipDeathPlayer2);

		_rng.Randomize();
	}

	protected override void PlayDeathSound()
	{
		if (_rng.RandiRange(0, 1) == 0)
			_vipDeathPlayer1?.Play();
		else
			_vipDeathPlayer2?.Play();
	}
}
