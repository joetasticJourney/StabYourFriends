using Godot;

namespace StabYourFriends.Controllers;

public class PlayerInput
{
    public Vector2 Movement { get; set; } = Vector2.Zero;
    public bool Action1 { get; set; }
    public bool Action2 { get; set; }

    public void Reset()
    {
        Movement = Vector2.Zero;
        Action1 = false;
        Action2 = false;
    }
}
