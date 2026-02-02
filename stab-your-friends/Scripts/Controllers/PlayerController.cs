using Godot;

namespace StabYourFriends.Controllers;

public class PlayerController
{
    public string PlayerId { get; }
    public string PlayerName { get; set; }
    public Color PlayerColor { get; }
    public PlayerInput CurrentInput { get; } = new();

    private static readonly Color[] AvailableColors =
    {
        new(0.9f, 0.2f, 0.2f),  // Red
        new(0.2f, 0.6f, 0.9f),  // Blue
        new(0.2f, 0.8f, 0.3f),  // Green
        new(0.9f, 0.8f, 0.2f),  // Yellow
        new(0.8f, 0.3f, 0.8f),  // Purple
        new(0.9f, 0.5f, 0.2f),  // Orange
        new(0.3f, 0.8f, 0.8f),  // Cyan
        new(0.9f, 0.4f, 0.6f),  // Pink
    };

    private static int _colorIndex;

    public PlayerController(string playerId, string playerName)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        PlayerColor = GetNextColor();
    }

    private static Color GetNextColor()
    {
        var color = AvailableColors[_colorIndex % AvailableColors.Length];
        _colorIndex++;
        return color;
    }

    public static void ResetColorIndex()
    {
        _colorIndex = 0;
    }

    public string GetColorHex()
    {
        return PlayerColor.ToHtml(false);
    }
}
