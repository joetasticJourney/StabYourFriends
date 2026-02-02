using Godot;
using StabYourFriends.Controllers;

namespace StabYourFriends.UI;

public partial class PlayerSlot : PanelContainer
{
    private Label _nameLabel = null!;
    private ColorRect _colorIndicator = null!;

    public override void _Ready()
    {
        _nameLabel = GetNode<Label>("%NameLabel");
        _colorIndicator = GetNode<ColorRect>("%ColorIndicator");

        SetEmpty();
    }

    public void SetPlayer(PlayerController player)
    {
        _nameLabel.Text = player.PlayerName;
        _colorIndicator.Color = player.PlayerColor;
        _colorIndicator.Visible = true;
    }

    public void SetEmpty()
    {
        _nameLabel.Text = "Waiting for player...";
        _colorIndicator.Visible = false;
    }
}
