#nullable enable

using System;
using System.Collections.Generic;
using Godot;

namespace StabYourFriends.Game;

public partial class VictoryScreen : Control
{
    public event Action? ReturnToLobbyRequested;

    public void Show(string winnerName, List<PlayerRanking> rankings)
    {
        BuildUi(winnerName, rankings);
        Visible = true;
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
        vbox.OffsetTop = -250;
        vbox.OffsetBottom = 250;
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
        spacer.CustomMinimumSize = new Vector2(0, 16);
        vbox.AddChild(spacer);

        // Return to lobby button
        var button = new Button();
        button.Text = "Return to Lobby";
        button.CustomMinimumSize = new Vector2(200, 40);
        button.Pressed += () =>
        {
            ReturnToLobbyRequested?.Invoke();
        };
        vbox.AddChild(button);
    }
}
