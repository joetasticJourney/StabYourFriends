using Godot;

namespace StabYourFriends.Game.PowerUps;

public partial class VictoryPointPowerUp : PowerUp
{
	public VictoryPointPowerUp()
	{
		Label = "VP";
		PowerUpColor = new Color(1.0f, 0.84f, 0.0f); // Gold
	}

	protected override void Pickup(StabCharacter character)
	{
		base.Pickup(character);
		character.AddScore(1);
	}
}
