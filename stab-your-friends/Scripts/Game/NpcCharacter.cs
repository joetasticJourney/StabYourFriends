#nullable enable

using Godot;

namespace StabYourFriends.Game;

public partial class NpcCharacter : StabCharacter
{
	private AudioStreamPlayer2D?[] _npcDeathPlayers = new AudioStreamPlayer2D?[5];
	private RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		base._Ready();
		Health = 1;
		KillPointValue = 0;

		for (int i = 0; i < 5; i++)
		{
			_npcDeathPlayers[i] = new AudioStreamPlayer2D();
			_npcDeathPlayers[i]!.Stream = GD.Load<AudioStream>($"res://Sounds/NPC Death {i + 1}.mp3");
			_npcDeathPlayers[i]!.MaxDistance = 2000;
			AddChild(_npcDeathPlayers[i]);
		}

		_rng.Randomize();
	}

	protected override void PlayDeathSound()
	{
		int index = _rng.RandiRange(0, 4);
		_npcDeathPlayers[index]?.Play();
	}
}
