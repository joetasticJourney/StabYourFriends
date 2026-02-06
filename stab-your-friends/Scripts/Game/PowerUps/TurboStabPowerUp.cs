using Godot;

namespace StabYourFriends.Game.PowerUps;

public partial class TurboStabPowerUp : PowerUp
{
	public TurboStabPowerUp()
	{
		Label = "TS";
		PowerUpColor = new Color(0.2f, 0.8f, 0.9f); // Cyan
		SoundPath = "res://Sounds/TurboStab.mp3";
	}

	protected override void Pickup(StabCharacter character)
	{
		base.Pickup(character);
		character.AddTurboStab();
	}
}
