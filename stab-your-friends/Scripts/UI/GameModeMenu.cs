using Godot;
using StabYourFriends.Game;

namespace StabYourFriends.UI;

public partial class GameModeMenu : Control
{
    [Signal] public delegate void GameModeSelectedEventHandler(string gameMode);
    [Signal] public delegate void CancelledEventHandler();

    public GameSettings Settings { get; private set; } = new();

    // Settings controls
    private Label _timeLabel = null!;
    private CheckBox _victoryPointCheck = null!;
    private CheckBox _kungFuCheck = null!;
    private CheckBox _reverseGripCheck = null!;
    private CheckBox _smokeBombCheck = null!;
    private CheckBox _turboStabCheck = null!;
    private HSlider _grappleDamageSlider = null!;
    private Label _grappleDamageValueLabel = null!;
    private CheckBox _stabModeCheck = null!;
    private HSlider _speedSlider = null!;
    private Label _speedValueLabel = null!;
    private HSlider _bonusSpeedSlider = null!;
    private Label _bonusSpeedValueLabel = null!;
    private HSlider _powerUpSpawnSlider = null!;
    private Label _powerUpSpawnValueLabel = null!;
    private HSlider _vipSpawnSlider = null!;
    private Label _vipSpawnValueLabel = null!;
    private HSlider _worldSizeSlider = null!;
    private Label _worldSizeValueLabel = null!;
    private HSlider _totalEnemiesSlider = null!;
    private Label _totalEnemiesValueLabel = null!;

    // Bottom buttons
    private Button _launchButton = null!;
    private Button _cancelButton = null!;

    private float _gameDurationSeconds = 600f;

    public override void _Ready()
    {
        BuildUi();
    }

    private void BuildUi()
    {
        // Full-screen semi-transparent background
        var bg = new ColorRect();
        bg.Color = new Color(0, 0, 0, 0.85f);
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // Main panel - nearly full screen
        var panel = new Panel();
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        panel.OffsetLeft = 40;
        panel.OffsetRight = -40;
        panel.OffsetTop = 30;
        panel.OffsetBottom = -30;
        AddChild(panel);

        // Main vertical layout with padding
        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.OffsetLeft = 30;
        vbox.OffsetRight = -30;
        vbox.OffsetTop = 20;
        vbox.OffsetBottom = -20;
        vbox.AddThemeConstantOverride("separation", 6);
        panel.AddChild(vbox);

        // ── Title ──
        var title = new Label();
        title.Text = "GAME SETUP";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 32);
        vbox.AddChild(title);

        AddSeparator(vbox);

        // ── Top row: Game Time + Power-Ups side by side ──
        var topColumns = new HBoxContainer();
        topColumns.AddThemeConstantOverride("separation", 40);
        topColumns.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        vbox.AddChild(topColumns);

        // Left column: Game Time
        var timeCol = new VBoxContainer();
        timeCol.AddThemeConstantOverride("separation", 6);
        timeCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        topColumns.AddChild(timeCol);

        AddSectionHeader(timeCol, "Game Time");

        var timeRow = new HBoxContainer();
        timeRow.AddThemeConstantOverride("separation", 10);
        timeRow.Alignment = BoxContainer.AlignmentMode.Center;
        timeCol.AddChild(timeRow);

        var minusBtn = new Button();
        minusBtn.Text = "-30s";
        minusBtn.CustomMinimumSize = new Vector2(70, 35);
        minusBtn.Pressed += () => AdjustTime(-30);
        timeRow.AddChild(minusBtn);

        _timeLabel = new Label();
        _timeLabel.CustomMinimumSize = new Vector2(100, 0);
        _timeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _timeLabel.AddThemeFontSizeOverride("font_size", 22);
        timeRow.AddChild(_timeLabel);

        var plusBtn = new Button();
        plusBtn.Text = "+30s";
        plusBtn.CustomMinimumSize = new Vector2(70, 35);
        plusBtn.Pressed += () => AdjustTime(30);
        timeRow.AddChild(plusBtn);

        UpdateTimeLabel();

        // Right column: Power-Ups
        var powerCol = new VBoxContainer();
        powerCol.AddThemeConstantOverride("separation", 4);
        powerCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        topColumns.AddChild(powerCol);

        AddSectionHeader(powerCol, "Power-Ups");

        var powerUpGrid = new GridContainer();
        powerUpGrid.Columns = 3;
        powerUpGrid.AddThemeConstantOverride("h_separation", 20);
        powerUpGrid.AddThemeConstantOverride("v_separation", 4);
        powerCol.AddChild(powerUpGrid);

        _victoryPointCheck = CreateCheckBox("Victory Points", true);
        _kungFuCheck = CreateCheckBox("Kung Fu", true);
        _reverseGripCheck = CreateCheckBox("Reverse Grip", true);
        _smokeBombCheck = CreateCheckBox("Smoke Bombs", true);
        _turboStabCheck = CreateCheckBox("Turbo Stab", true);

        powerUpGrid.AddChild(_victoryPointCheck);
        powerUpGrid.AddChild(_kungFuCheck);
        powerUpGrid.AddChild(_reverseGripCheck);
        powerUpGrid.AddChild(_smokeBombCheck);
        powerUpGrid.AddChild(_turboStabCheck);

        AddSeparator(vbox);

        // ── Sliders in two-column layout ──
        var sliderColumns = new HBoxContainer();
        sliderColumns.AddThemeConstantOverride("separation", 40);
        sliderColumns.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(sliderColumns);

        // Left slider column
        var leftCol = new VBoxContainer();
        leftCol.AddThemeConstantOverride("separation", 6);
        leftCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        sliderColumns.AddChild(leftCol);

        // Grapple Damage
        AddSliderRow(leftCol, "Grapple Damage", 0, 10, 1, 1,
            out _grappleDamageSlider, out _grappleDamageValueLabel, OnGrappleDamageChanged);

        // Player Move Speed
        AddSliderRow(leftCol, "Player Move Speed", 25, 200, 5, 100,
            out _speedSlider, out _speedValueLabel, OnSpeedChanged);

        // Player Bonus Speed
        AddSliderRow(leftCol, "Player Bonus Speed", 25, 200, 5, 100,
            out _bonusSpeedSlider, out _bonusSpeedValueLabel, OnBonusSpeedChanged);

        // Total Enemies
        AddSliderRow(leftCol, "Total Enemies", 10, 400, 10, 50,
            out _totalEnemiesSlider, out _totalEnemiesValueLabel, OnTotalEnemiesChanged);

        // Right slider column
        var rightCol = new VBoxContainer();
        rightCol.AddThemeConstantOverride("separation", 6);
        rightCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        sliderColumns.AddChild(rightCol);

        // Controller Mode
        _stabModeCheck = CreateCheckBox("Controller Mode", false);
        rightCol.AddChild(_stabModeCheck);

        // Power-Up Spawn Interval
        AddSliderRow(rightCol, "Power-Up Spawn (sec)", 1, 30, 1, 3,
            out _powerUpSpawnSlider, out _powerUpSpawnValueLabel, OnPowerUpSpawnChanged);

        // VIP Spawn Interval
        AddSliderRow(rightCol, "VIP Spawn (sec)", 5, 60, 1, 12,
            out _vipSpawnSlider, out _vipSpawnValueLabel, OnVipSpawnChanged);

        // World Size
        AddSliderRow(rightCol, "World Size", 0, 10, 0.5, 5,
            out _worldSizeSlider, out _worldSizeValueLabel, OnWorldSizeChanged);
        _worldSizeValueLabel.Text = WorldSizeScalerFromSlider(5).ToString("F1");

        AddSeparator(vbox);

        // ── Bottom Buttons ──
        var buttonRow = new HBoxContainer();
        buttonRow.AddThemeConstantOverride("separation", 20);
        buttonRow.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(buttonRow);

        _cancelButton = new Button();
        _cancelButton.Text = "Cancel";
        _cancelButton.CustomMinimumSize = new Vector2(200, 50);
        _cancelButton.Pressed += OnCancelPressed;
        buttonRow.AddChild(_cancelButton);

        _launchButton = new Button();
        _launchButton.Text = "Launch Game";
        _launchButton.CustomMinimumSize = new Vector2(300, 50);
        _launchButton.AddThemeFontSizeOverride("font_size", 22);
        _launchButton.Pressed += OnLaunchPressed;
        buttonRow.AddChild(_launchButton);
    }

    private void AddSliderRow(VBoxContainer parent, string label,
        double min, double max, double step, double defaultVal,
        out HSlider slider, out Label valueLabel,
        HSlider.ValueChangedEventHandler onChanged)
    {
        var header = new Label();
        header.Text = label;
        header.AddThemeFontSizeOverride("font_size", 16);
        header.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.5f));
        parent.AddChild(header);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        parent.AddChild(row);

        slider = new HSlider();
        slider.MinValue = min;
        slider.MaxValue = max;
        slider.Step = step;
        slider.Value = defaultVal;
        slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        slider.CustomMinimumSize = new Vector2(0, 25);
        slider.ValueChanged += onChanged;
        row.AddChild(slider);

        valueLabel = new Label();
        valueLabel.Text = ((int)defaultVal).ToString();
        valueLabel.CustomMinimumSize = new Vector2(40, 0);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(valueLabel);
    }

    private CheckBox CreateCheckBox(string text, bool defaultOn)
    {
        var cb = new CheckBox();
        cb.Text = text;
        cb.ButtonPressed = defaultOn;
        cb.AddThemeFontSizeOverride("font_size", 16);
        return cb;
    }

    private void AddSectionHeader(VBoxContainer parent, string text)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", 20);
        label.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.5f));
        parent.AddChild(label);
    }

    private void AddSeparator(VBoxContainer parent)
    {
        var sep = new HSeparator();
        parent.AddChild(sep);
    }

    private void AdjustTime(float deltaSeconds)
    {
        _gameDurationSeconds = Mathf.Clamp(_gameDurationSeconds + deltaSeconds, 60f, 600f);
        UpdateTimeLabel();
    }

    private void UpdateTimeLabel()
    {
        int totalSecs = (int)_gameDurationSeconds;
        int minutes = totalSecs / 60;
        int seconds = totalSecs % 60;
        _timeLabel.Text = seconds == 0 ? $"{minutes}:00" : $"{minutes}:{seconds:D2}";
    }

    private void OnGrappleDamageChanged(double value)
    {
        _grappleDamageValueLabel.Text = ((int)value).ToString();
    }

    private void OnSpeedChanged(double value)
    {
        _speedValueLabel.Text = ((int)value).ToString();
    }

    private void OnBonusSpeedChanged(double value)
    {
        _bonusSpeedValueLabel.Text = ((int)value).ToString();
    }

    private void OnTotalEnemiesChanged(double value)
    {
        _totalEnemiesValueLabel.Text = ((int)value).ToString();
    }

    private void OnPowerUpSpawnChanged(double value)
    {
        _powerUpSpawnValueLabel.Text = ((int)value).ToString();
    }

    private void OnVipSpawnChanged(double value)
    {
        _vipSpawnValueLabel.Text = ((int)value).ToString();
    }

    private void OnWorldSizeChanged(double value)
    {
        _worldSizeValueLabel.Text = WorldSizeScalerFromSlider(value).ToString("F1");
    }

    private static float WorldSizeScalerFromSlider(double sliderValue)
    {
        // 0 → 0.2f, 10 → 5.0f (linear interpolation)
        if(sliderValue == 5)
        {
            return 1;
        }
        if( sliderValue < 5)
        {
            return (1.0f / Mathf.Lerp( 1f, 2f, (float) Mathf.Abs(sliderValue - 5f) / 5f ));
        }
        else
        {
            return (Mathf.Lerp(1f, 2.5f,(float) (sliderValue - 5f) / 5f));
        }

    }

    private void OnLaunchPressed()
    {
        Settings = new GameSettings
        {
            GameDurationSeconds = _gameDurationSeconds,
            EnableVictoryPoints = _victoryPointCheck.ButtonPressed,
            EnableKungFu = _kungFuCheck.ButtonPressed,
            EnableReverseGrip = _reverseGripCheck.ButtonPressed,
            EnableSmokeBombs = _smokeBombCheck.ButtonPressed,
            EnableTurboStab = _turboStabCheck.ButtonPressed,
            GrappleDamage = (int)_grappleDamageSlider.Value,
            ControllerMode = _stabModeCheck.ButtonPressed,
            PlayerMoveSpeed = (float)_speedSlider.Value,
            PlayerBonusSpeed = (float)_bonusSpeedSlider.Value,
            PowerUpSpawnInterval = (float)_powerUpSpawnSlider.Value,
            VipSpawnInterval = (float)_vipSpawnSlider.Value,
            WorldSizeScaler = WorldSizeScalerFromSlider(_worldSizeSlider.Value),
            TotalEnemies = (int)_totalEnemiesSlider.Value,
        };

        GD.Print($"Game launching: duration={_gameDurationSeconds}s, grappleDmg={Settings.GrappleDamage}, colorBlind={Settings.ColorBlindMode}, speed={Settings.PlayerMoveSpeed}");
        EmitSignal(SignalName.GameModeSelected, "FreeForAll");
    }

    private void OnCancelPressed()
    {
        EmitSignal(SignalName.Cancelled);
    }

    public override void _ExitTree()
    {
        _launchButton.Pressed -= OnLaunchPressed;
        _cancelButton.Pressed -= OnCancelPressed;
        _grappleDamageSlider.ValueChanged -= OnGrappleDamageChanged;
        _speedSlider.ValueChanged -= OnSpeedChanged;
        _bonusSpeedSlider.ValueChanged -= OnBonusSpeedChanged;
        _totalEnemiesSlider.ValueChanged -= OnTotalEnemiesChanged;
        _powerUpSpawnSlider.ValueChanged -= OnPowerUpSpawnChanged;
        _vipSpawnSlider.ValueChanged -= OnVipSpawnChanged;
        _worldSizeSlider.ValueChanged -= OnWorldSizeChanged;
    }
}
