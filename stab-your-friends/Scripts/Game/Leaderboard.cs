using System.Collections.Generic;
using System.Linq;
using Godot;

namespace StabYourFriends.Game;

public partial class Leaderboard : Control
{
	private const float RowHeight = 70f;
	private const float Padding = 8f;
	private const float HealthBarHeight = 6f;
	private const float HeaderFontSizeRef = 12f;
	private const float FontSizeRef = 20f;
	private const float PowerUpFontSizeRef = 16f;

	private static readonly Color BackgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f);
	private static readonly Color HealthBarBgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
	private static readonly Color HealthBarFgColor = new Color(0.8f, 0.15f, 0.15f, 0.9f);
	private static readonly Color TextColor = new Color(1f, 1f, 1f, 0.95f);
	private static readonly Color ScoreColor = new Color(1f, 0.85f, 0.3f, 0.95f);
	private static readonly Color HeaderColor = new Color(1f, 1f, 1f, 0.5f);
	private static readonly Color SeparatorColor = new Color(1f, 1f, 1f, 0.1f);

	// Power-up colors matching in-game pickups
	private static readonly Color SmokeBombColor = new Color(0.5f, 0.5f, 0.5f);
	private static readonly Color KungFuColor = new Color(1.0f, 0.4f, 0.2f);
	private static readonly Color ReverseGripColor = new Color(0.6f, 0.2f, 0.9f);
	private static readonly Color TurboStabColor = new Color(0.2f, 0.8f, 0.9f);

	private Dictionary<string, StabCharacter>? _playerCharacters;
	private float _scaleFactor = 1f;
	private Font? _font;

	public override void _Ready()
	{
		_font = ThemeDB.FallbackFont;
		ClipContents = true;
	}

	public void SetPlayerCharacters(Dictionary<string, StabCharacter> players)
	{
		_playerCharacters = players;
	}

	public void SetScaleFactor(float scaleFactor)
	{
		_scaleFactor = scaleFactor;
	}

	public override void _Process(double delta)
	{
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_font == null || _playerCharacters == null) return;

		float panelWidth = Size.X;
		float panelHeight = Size.Y;

		// Background
		DrawRect(new Rect2(0, 0, panelWidth, panelHeight), BackgroundColor);

		float scaled = _scaleFactor;
		float pad = Padding * scaled;
		int headerFont = Mathf.Max(Mathf.RoundToInt(HeaderFontSizeRef * scaled), 6);
		int fontSize = Mathf.Max(Mathf.RoundToInt(FontSizeRef * scaled), 8);
		int puFont = Mathf.Max(Mathf.RoundToInt(PowerUpFontSizeRef * scaled), 7);
		float rowH = RowHeight * scaled;
		float hpBarH = HealthBarHeight * scaled;

		// Header - smaller font so "LEADERBOARD" fits
		float headerY = pad + headerFont;
		DrawString(_font, new Vector2(pad, headerY), "LEADERBOARD",
			HorizontalAlignment.Left, panelWidth - pad * 2, headerFont, HeaderColor);

		float contentStart = headerY + pad;

		// Sort by score desc, name asc as tiebreak
		var sorted = _playerCharacters.Values
			.Where(c => IsInstanceValid(c))
			.OrderByDescending(c => c.Score)
			.ThenBy(c => c.CharacterName)
			.ToList();

		int maxRows = Mathf.FloorToInt((panelHeight - contentStart) / rowH);
		int count = Mathf.Min(sorted.Count, maxRows);
		float contentW = panelWidth - pad * 2;

		for (int i = 0; i < count; i++)
		{
			var p = sorted[i];
			float y = contentStart + i * rowH;

			// Separator
			DrawLine(new Vector2(pad, y), new Vector2(panelWidth - pad, y), SeparatorColor, 1f);

			// Row line 1: name + score
			float nameY = y + pad + fontSize;
			DrawString(_font, new Vector2(pad, nameY), p.CharacterName,
				HorizontalAlignment.Left, contentW * 0.7f, fontSize, TextColor);
			DrawString(_font, new Vector2(pad, nameY), p.Score.ToString(),
				HorizontalAlignment.Right, contentW, fontSize, ScoreColor);

			// Row line 2: health bar
			float barY = nameY + pad * 0.5f;
			float hpPct = (float)p.Health / StabCharacter.MaxHealthValue;
			DrawRect(new Rect2(pad, barY, contentW, hpBarH), HealthBarBgColor);
			if (hpPct > 0f)
			{
				DrawRect(new Rect2(pad, barY, contentW * hpPct, hpBarH), HealthBarFgColor);
			}

			// Row lines 3-4: power-up counts, 2 per line, only if > 0, each in its own color
			float puY = barY + hpBarH + pad * 0.5f + puFont;
			float halfW = contentW / 2f;
			int slot = 0;

			if (p.SmokeBombCount > 0)
			{
				DrawPowerUpText(pad, halfW, puY, puFont, slot, $"SB:{p.SmokeBombCount}", SmokeBombColor);
				slot++;
			}
			if (p.KungFuCount > 0)
			{
				DrawPowerUpText(pad, halfW, puY, puFont, slot, $"KF:{p.KungFuCount}", KungFuColor);
				slot++;
			}
			if (p.ReverseGripCount > 0)
			{
				DrawPowerUpText(pad, halfW, puY, puFont, slot, $"RG:{p.ReverseGripCount}", ReverseGripColor);
				slot++;
			}
			if (p.TurboStabCount > 0)
			{
				DrawPowerUpText(pad, halfW, puY, puFont, slot, $"TS:{p.TurboStabCount}", TurboStabColor);
				slot++;
			}
		}
	}

	private void DrawPowerUpText(float pad, float halfW, float firstLineY, int puFont, int slot, string text, Color color)
	{
		// slot 0,1 on first line; slot 2,3 on second line
		int row = slot / 2;
		int col = slot % 2;
		float lineHeight = puFont + pad * 0.3f;
		float x = pad + col * halfW;
		float y = firstLineY + row * lineHeight;
		DrawString(_font, new Vector2(x, y), text, HorizontalAlignment.Left, halfW, puFont, color);
	}
}
