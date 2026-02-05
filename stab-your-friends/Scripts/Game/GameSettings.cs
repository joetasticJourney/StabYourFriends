namespace StabYourFriends.Game;

public class GameSettings
{
    public float GameDurationSeconds { get; set; } = 600f;
    public bool EnableVictoryPoints { get; set; } = true;
    public bool EnableKungFu { get; set; } = true;
    public bool EnableReverseGrip { get; set; } = true;
    public bool EnableSmokeBombs { get; set; } = true;
    public bool EnableTurboStab { get; set; } = true;
    public int GrappleDamage { get; set; } = 1;
    public bool ColorBlindMode { get; set; }
    public bool ControllerMode { get; set; } = true;
    public float PlayerMoveSpeed { get; set; } = 100f;
    public float PlayerBonusSpeed { get; set; } = 100f;
    public float PowerUpSpawnInterval { get; set; } = 3f;
    public float VipSpawnInterval { get; set; } = 12f;
}
