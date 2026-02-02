using Godot;
using StabYourFriends.Controllers;

namespace StabYourFriends.Game;

public partial class PlayerCharacter : CharacterBody2D
{
    [Export] public float MoveSpeed { get; set; } = 300f;

    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public Color PlayerColor { get; set; } = Colors.White;

    private PlayerController? _controller;
    private ColorRect _colorRect = null!;
    private Label _nameLabel = null!;

    public override void _Ready()
    {
        _colorRect = GetNode<ColorRect>("Sprite2D/ColorRect");
        _nameLabel = GetNode<Label>("NameLabel");

        UpdateVisuals();
    }

    public void Initialize(PlayerController controller)
    {
        _controller = controller;
        PlayerId = controller.PlayerId;
        PlayerName = controller.PlayerName;
        PlayerColor = controller.PlayerColor;

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (_colorRect != null)
        {
            _colorRect.Color = PlayerColor;
        }

        if (_nameLabel != null)
        {
            _nameLabel.Text = PlayerName;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_controller == null) return;

        var input = _controller.CurrentInput;
        var velocity = new Vector2(input.Movement.X, input.Movement.Y) * MoveSpeed;

        Velocity = velocity;
        MoveAndSlide();

        // Handle action buttons
        if (input.Action1)
        {
            OnAction1();
        }
        if (input.Action2)
        {
            OnAction2();
        }
    }

    private void OnAction1()
    {
        // Primary action (e.g., attack/stab)
        // TODO: Implement game-specific action
    }

    private void OnAction2()
    {
        // Secondary action (e.g., dash/block)
        // TODO: Implement game-specific action
    }
}
