#nullable enable

using Godot;

namespace StabYourFriends.Game;

public partial class VipCharacter : StabCharacter
{
	private static readonly Color VipColor = new(0.2f, 0.6f, 1.0f);

	public override void _Ready()
	{
		base._Ready();
		Health = 5;
		KillPointValue = 1;
		CharacterColor = VipColor;
		GetNode<AnimatedSprite2D>("AnimatedSprite2D").SelfModulate = VipColor;
	}
}
