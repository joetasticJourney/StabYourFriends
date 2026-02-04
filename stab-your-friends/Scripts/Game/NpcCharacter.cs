#nullable enable

namespace StabYourFriends.Game;

public partial class NpcCharacter : StabCharacter
{
	public override void _Ready()
	{
		base._Ready();
		Health = 1;
		KillPointValue = 0;
	}
}
