using Godot;

namespace StabYourFriends.Game;

public partial class GameTimer : Label
{
    private float _remainingSeconds;
    private bool _running;
    private bool _oneMinuteWarningPlayed;
    private AudioStreamPlayer? _oneMinutePlayer;
    private static readonly Color NormalColor = Colors.White;
    private static readonly Color UrgentColor = new(1f, 0.2f, 0.2f);

    public bool IsExpired => _remainingSeconds <= 0f;

    public void Initialize(float totalSeconds)
    {
        _remainingSeconds = totalSeconds;
        _running = true;
        _oneMinuteWarningPlayed = totalSeconds <= 60f; // Don't play if starting at or below 60s

        _oneMinutePlayer = new AudioStreamPlayer();
        _oneMinutePlayer.Stream = GD.Load<AudioStream>("res://Sounds/One minute.mp3");
        AddChild(_oneMinutePlayer);

        VerticalAlignment = VerticalAlignment.Center;
        AddThemeColorOverride("font_color", NormalColor);

        UpdateDisplay();
    }

    public void Stop()
    {
        _running = false;
    }

    public override void _Process(double delta)
    {
        if (!_running) return;

        _remainingSeconds -= (float)delta;
        if (_remainingSeconds < 0f)
            _remainingSeconds = 0f;

        UpdateDisplay();

        // Play one minute warning when crossing 60 seconds
        if (!_oneMinuteWarningPlayed && _remainingSeconds <= 60f)
        {
            _oneMinuteWarningPlayed = true;
            _oneMinutePlayer?.Play();
        }

        if (_remainingSeconds <= 30f)
        {
            AddThemeColorOverride("font_color", UrgentColor);
        }
    }

    private void UpdateDisplay()
    {
        int totalSecs = Mathf.CeilToInt(_remainingSeconds);
        int minutes = totalSecs / 60;
        int seconds = totalSecs % 60;
        Text = $"{minutes:D2}:{seconds:D2}";
    }
}
