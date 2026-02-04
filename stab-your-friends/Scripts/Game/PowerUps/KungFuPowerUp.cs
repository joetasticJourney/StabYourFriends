using Godot;

namespace StabYourFriends.Game.PowerUps;

public partial class KungFuPowerUp : PowerUp
{
	public KungFuPowerUp()
	{
		Label = "KF";
		PowerUpColor = new Color(1.0f, 0.4f, 0.2f); // Orange-red
	}

	protected override void Pickup(StabCharacter character)
	{
		base.Pickup(character);
		character.AddKungFu();
	}
}
