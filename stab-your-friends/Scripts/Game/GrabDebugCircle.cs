using Godot;

namespace StabYourFriends.Game;

public partial class GrabDebugCircle : Node2D
{
    private const float Duration = 0.3f;
    private float _radius;
    private float _elapsed;

    public GrabDebugCircle(float radius)
    {
        _radius = radius;
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        if (_elapsed >= Duration)
        {
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float alpha = 1f - (_elapsed / Duration);
        var color = new Color(1f, 1f, 0f, alpha * 0.5f);
        DrawArc(Vector2.Zero, _radius, 0, Mathf.Tau, 32, color, 2f);
    }
}
