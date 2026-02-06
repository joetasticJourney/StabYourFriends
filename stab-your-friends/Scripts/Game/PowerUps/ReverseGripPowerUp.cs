using Godot;

namespace StabYourFriends.Game.PowerUps;

public partial class ReverseGripPowerUp : PowerUp
{
	public ReverseGripPowerUp()
	{
		Label = "RG";
		PowerUpColor = new Color(0.6f, 0.2f, 0.9f); // Purple
		SoundPath = "res://Sounds/ReverseGrip.mp3";
	}

	protected override void Pickup(StabCharacter character)
	{
		base.Pickup(character);
		character.AddReverseGrip();
	}
}
