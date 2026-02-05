#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using StabYourFriends.Autoload;

namespace StabYourFriends.Game;

public partial class VictoryScreen : Control
{
    public event Action? ReturnToLobbyRequested;
    public event Action? RestartRequested;

    private string _winnerPlayerId = "";
    private bool _prevAction1;
    private bool _prevAction2;
    private double _showTime;
    private bool _choiceMade;

    private const double GuardDelaySec = 1.0;

    public void Show(string winnerName, List<PlayerRanking> rankings, string winnerPlayerId)
    {
        _winnerPlayerId = winnerPlayerId;
        _showTime = 0.0;
        _choiceMade = false;

        // Snapshot current button state so we only trigger on false→true transitions
        if (!string.IsNullOrEmpty(_winnerPlayerId) &&
            GameManager.Instance.Players.TryGetValue(_winnerPlayerId, out var ctrl))
        {
            _prevAction1 = ctrl.CurrentInput.Action1;
            _prevAction2 = ctrl.CurrentInput.Action2;
        }

        BuildUi(winnerName, rankings);
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (_choiceMade || string.IsNullOrEmpty(_winnerPlayerId)) return;

        _showTime += delta;
        if (_showTime < GuardDelaySec) return;

        if (!GameManager.Instance.Players.TryGetValue(_winnerPlayerId, out var ctrl))
            return;

        bool curAction1 = ctrl.CurrentInput.Action1;
        bool curAction2 = ctrl.CurrentInput.Action2;

        // Edge detection: false → true
        if (curAction1 && !_prevAction1)
        {
            _choiceMade = true;
            RestartRequested?.Invoke();
        }
        else if (curAction2 && !_prevAction2)
        {
            _choiceMade = true;
            ReturnToLobbyRequested?.Invoke();
        }

        _prevAction1 = curAction1;
        _prevAction2 = curAction2;
    }

    private void BuildUi(string winnerName, List<PlayerRanking> rankings)
    {
        // Full-screen semi-transparent background
        var bg = new ColorRect();
        bg.Color = new Color(0, 0, 0, 0.75f);
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // Center container
        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.Center);
        vbox.GrowHorizontal = GrowDirection.Both;
        vbox.GrowVertical = GrowDirection.Both;
        vbox.OffsetLeft = -250;
        vbox.OffsetRight = 250;
        vbox.OffsetTop = -300;
        vbox.OffsetBottom = 300;
        vbox.AddThemeConstantOverride("separation", 12);
        AddChild(vbox);

        // "GAME OVER" title
        var titleLabel = new Label();
        titleLabel.Text = "GAME OVER";
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.AddThemeFontSizeOverride("font_size", 48);
        titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
        vbox.AddChild(titleLabel);

        // Winner name
        var winnerLabel = new Label();
        winnerLabel.Text = string.IsNullOrEmpty(winnerName) ? "No winner" : $"Winner: {winnerName}";
        winnerLabel.HorizontalAlignment = HorizontalAlignment.Center;
        winnerLabel.AddThemeFontSizeOverride("font_size", 32);
        winnerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
        vbox.AddChild(winnerLabel);

        // Separator
        var sep = new HSeparator();
        vbox.AddChild(sep);

        // Rankings header
        var headerLabel = new Label();
        headerLabel.Text = "Rankings";
        headerLabel.HorizontalAlignment = HorizontalAlignment.Center;
        headerLabel.AddThemeFontSizeOverride("font_size", 24);
        vbox.AddChild(headerLabel);

        // Rankings list
        for (int i = 0; i < rankings.Count; i++)
        {
            var r = rankings[i];
            var rankLabel = new Label();

            string status = "";
            if (r.WasDisconnected) status = " [DC]";
            else if (r.IsDead) status = " [Dead]";
            else if (r.WasLastAlive) status = " [Last Standing]";

            rankLabel.Text = $"#{i + 1}  {r.Name}  —  {r.Score} pts{status}";
            rankLabel.HorizontalAlignment = HorizontalAlignment.Center;
            rankLabel.AddThemeFontSizeOverride("font_size", 18);

            if (i == 0)
                rankLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));

            vbox.AddChild(rankLabel);
        }

        // Spacer
        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, 20);
        vbox.AddChild(spacer);

        // Winner's choice prompt
        var promptLabel = new Label();
        string displayName = string.IsNullOrEmpty(winnerName) ? "Winner" : winnerName;
        promptLabel.Text = $"{displayName}, what would you like to do?";
        promptLabel.HorizontalAlignment = HorizontalAlignment.Center;
        promptLabel.AddThemeFontSizeOverride("font_size", 22);
        promptLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
        vbox.AddChild(promptLabel);

        // Spacer
        var spacer2 = new Control();
        spacer2.CustomMinimumSize = new Vector2(0, 8);
        vbox.AddChild(spacer2);

        // --- Play Again row (green circle + label) ---
        var playAgainRow = BuildChoiceRow(
            new Color(0.298f, 0.686f, 0.314f), // #4caf50
            "Play Again"
        );
        vbox.AddChild(playAgainRow);

        // --- Main Menu row (blue circle + label) ---
        var mainMenuRow = BuildChoiceRow(
            new Color(0.129f, 0.588f, 0.953f), // #2196f3
            "Main Menu"
        );
        vbox.AddChild(mainMenuRow);
    }

    private static HBoxContainer BuildChoiceRow(Color circleColor, string text)
    {
        var row = new HBoxContainer();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddThemeConstantOverride("separation", 12);

        // Colored circle (drawn via a small Panel with a rounded StyleBox)
        var circlePanel = new Panel();
        circlePanel.CustomMinimumSize = new Vector2(32, 32);

        var style = new StyleBoxFlat();
        style.BgColor = circleColor;
        style.CornerRadiusTopLeft = 16;
        style.CornerRadiusTopRight = 16;
        style.CornerRadiusBottomLeft = 16;
        style.CornerRadiusBottomRight = 16;
        circlePanel.AddThemeStyleboxOverride("panel", style);
        row.AddChild(circlePanel);

        // Label
        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", Colors.White);
        row.AddChild(label);

        return row;
    }
}
