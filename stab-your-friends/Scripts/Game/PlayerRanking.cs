namespace StabYourFriends.Game;

public class PlayerRanking
{
    public string Name { get; set; } = "";
    public int Score { get; set; }
    public bool IsDead { get; set; }
    public bool WasLastAlive { get; set; }
    public bool WasDisconnected { get; set; }
}
