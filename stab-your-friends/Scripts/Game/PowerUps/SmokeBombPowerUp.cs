using Godot;

namespace StabYourFriends.Game.PowerUps;

public partial class SmokeBombPowerUp : PowerUp
{
	public SmokeBombPowerUp()
	{
		Label = "SB";
		PowerUpColor = new Color(0.5f, 0.5f, 0.5f); // Gray
	}

	protected override void Pickup(StabCharacter character)
	{
		base.Pickup(character);
		character.AddSmokeBombs(1);
	}
}
