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
    private CheckBox _colorBlindCheck = null!;
    private HSlider _speedSlider = null!;
    private Label _speedValueLabel = null!;
    private HSlider _bonusSpeedSlider = null!;
    private Label _bonusSpeedValueLabel = null!;
    private HSlider _powerUpSpawnSlider = null!;
    private Label _powerUpSpawnValueLabel = null!;
    private HSlider _vipSpawnSlider = null!;
    private Label _vipSpawnValueLabel = null!;

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

        // Main panel - large and opaque
        var panel = new Panel();
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.GrowHorizontal = GrowDirection.Both;
        panel.GrowVertical = GrowDirection.Both;
        panel.OffsetLeft = -350;
        panel.OffsetRight = 350;
        panel.OffsetTop = -350;
        panel.OffsetBottom = 350;
        AddChild(panel);

        // ScrollContainer to handle overflow
        var scroll = new ScrollContainer();
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        scroll.OffsetLeft = 25;
        scroll.OffsetTop = 25;
        scroll.OffsetRight = -25;
        scroll.OffsetBottom = -25;
        panel.AddChild(scroll);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(vbox);

        // ── Title ──
        var title = new Label();
        title.Text = "GAME SETUP";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 32);
        vbox.AddChild(title);

        AddSeparator(vbox);

        // ── Game Time Section ──
        AddSectionHeader(vbox, "Game Time");

        var timeRow = new HBoxContainer();
        timeRow.AddThemeConstantOverride("separation", 10);
        timeRow.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(timeRow);

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

        AddSeparator(vbox);

        // ── Power-Ups Section ──
        AddSectionHeader(vbox, "Power-Ups");

        var powerUpGrid = new GridContainer();
        powerUpGrid.Columns = 2;
        powerUpGrid.AddThemeConstantOverride("h_separation", 30);
        powerUpGrid.AddThemeConstantOverride("v_separation", 6);
        vbox.AddChild(powerUpGrid);

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

        // ── Game Options Section ──
        AddSectionHeader(vbox, "Game Options");

        AddSectionHeader(vbox, "Grapple Damage");

        var grappleDamageRow = new HBoxContainer();
        grappleDamageRow.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(grappleDamageRow);

        _grappleDamageSlider = new HSlider();
        _grappleDamageSlider.MinValue = 0;
        _grappleDamageSlider.MaxValue = 10;
        _grappleDamageSlider.Step = 1;
        _grappleDamageSlider.Value = 1;
        _grappleDamageSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _grappleDamageSlider.CustomMinimumSize = new Vector2(0, 30);
        _grappleDamageSlider.ValueChanged += OnGrappleDamageChanged;
        grappleDamageRow.AddChild(_grappleDamageSlider);

        _grappleDamageValueLabel = new Label();
        _grappleDamageValueLabel.Text = "1";
        _grappleDamageValueLabel.CustomMinimumSize = new Vector2(40, 0);
        _grappleDamageValueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        grappleDamageRow.AddChild(_grappleDamageValueLabel);

        _colorBlindCheck = CreateCheckBox("Color Blind Mode", false);
        vbox.AddChild(_colorBlindCheck);

        AddSeparator(vbox);

        // ── Player Speed Section ──
        AddSectionHeader(vbox, "Player Move Speed");

        var speedRow = new HBoxContainer();
        speedRow.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(speedRow);

        _speedSlider = new HSlider();
        _speedSlider.MinValue = 25;
        _speedSlider.MaxValue = 200;
        _speedSlider.Step = 5;
        _speedSlider.Value = 100;
        _speedSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _speedSlider.CustomMinimumSize = new Vector2(0, 30);
        _speedSlider.ValueChanged += OnSpeedChanged;
        speedRow.AddChild(_speedSlider);

        _speedValueLabel = new Label();
        _speedValueLabel.Text = "100";
        _speedValueLabel.CustomMinimumSize = new Vector2(40, 0);
        _speedValueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        speedRow.AddChild(_speedValueLabel);

        AddSeparator(vbox);

        // ── Player Bonus Speed Section ──
        AddSectionHeader(vbox, "Player Bonus Speed");

        var bonusSpeedRow = new HBoxContainer();
        bonusSpeedRow.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(bonusSpeedRow);

        _bonusSpeedSlider = new HSlider();
        _bonusSpeedSlider.MinValue = 25;
        _bonusSpeedSlider.MaxValue = 200;
        _bonusSpeedSlider.Step = 5;
        _bonusSpeedSlider.Value = 100;
        _bonusSpeedSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _bonusSpeedSlider.CustomMinimumSize = new Vector2(0, 30);
        _bonusSpeedSlider.ValueChanged += OnBonusSpeedChanged;
        bonusSpeedRow.AddChild(_bonusSpeedSlider);

        _bonusSpeedValueLabel = new Label();
        _bonusSpeedValueLabel.Text = "100";
        _bonusSpeedValueLabel.CustomMinimumSize = new Vector2(40, 0);
        _bonusSpeedValueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        bonusSpeedRow.AddChild(_bonusSpeedValueLabel);

        AddSeparator(vbox);

        // ── Spawn Intervals Section ──
        AddSectionHeader(vbox, "Power-Up Spawn Interval (seconds)");

        var powerUpSpawnRow = new HBoxContainer();
        powerUpSpawnRow.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(powerUpSpawnRow);

        _powerUpSpawnSlider = new HSlider();
        _powerUpSpawnSlider.MinValue = 1;
        _powerUpSpawnSlider.MaxValue = 30;
        _powerUpSpawnSlider.Step = 1;
        _powerUpSpawnSlider.Value = 3;
        _powerUpSpawnSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _powerUpSpawnSlider.CustomMinimumSize = new Vector2(0, 30);
        _powerUpSpawnSlider.ValueChanged += OnPowerUpSpawnChanged;
        powerUpSpawnRow.AddChild(_powerUpSpawnSlider);

        _powerUpSpawnValueLabel = new Label();
        _powerUpSpawnValueLabel.Text = "3";
        _powerUpSpawnValueLabel.CustomMinimumSize = new Vector2(40, 0);
        _powerUpSpawnValueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        powerUpSpawnRow.AddChild(_powerUpSpawnValueLabel);

        AddSectionHeader(vbox, "VIP Spawn Interval (seconds)");

        var vipSpawnRow = new HBoxContainer();
        vipSpawnRow.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(vipSpawnRow);

        _vipSpawnSlider = new HSlider();
        _vipSpawnSlider.MinValue = 5;
        _vipSpawnSlider.MaxValue = 60;
        _vipSpawnSlider.Step = 1;
        _vipSpawnSlider.Value = 12;
        _vipSpawnSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _vipSpawnSlider.CustomMinimumSize = new Vector2(0, 30);
        _vipSpawnSlider.ValueChanged += OnVipSpawnChanged;
        vipSpawnRow.AddChild(_vipSpawnSlider);

        _vipSpawnValueLabel = new Label();
        _vipSpawnValueLabel.Text = "12";
        _vipSpawnValueLabel.CustomMinimumSize = new Vector2(40, 0);
        _vipSpawnValueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        vipSpawnRow.AddChild(_vipSpawnValueLabel);

        AddSeparator(vbox);

        // ── Bottom Buttons ──
        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, 5);
        vbox.AddChild(spacer);

        _launchButton = new Button();
        _launchButton.Text = "Launch Game";
        _launchButton.CustomMinimumSize = new Vector2(0, 50);
        _launchButton.AddThemeFontSizeOverride("font_size", 22);
        _launchButton.Pressed += OnLaunchPressed;
        vbox.AddChild(_launchButton);

        _cancelButton = new Button();
        _cancelButton.Text = "Cancel";
        _cancelButton.CustomMinimumSize = new Vector2(0, 40);
        _cancelButton.Pressed += OnCancelPressed;
        vbox.AddChild(_cancelButton);
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

    private void OnPowerUpSpawnChanged(double value)
    {
        _powerUpSpawnValueLabel.Text = ((int)value).ToString();
    }

    private void OnVipSpawnChanged(double value)
    {
        _vipSpawnValueLabel.Text = ((int)value).ToString();
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
            ColorBlindMode = _colorBlindCheck.ButtonPressed,
            PlayerMoveSpeed = (float)_speedSlider.Value,
            PlayerBonusSpeed = (float)_bonusSpeedSlider.Value,
            PowerUpSpawnInterval = (float)_powerUpSpawnSlider.Value,
            VipSpawnInterval = (float)_vipSpawnSlider.Value,
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
        _powerUpSpawnSlider.ValueChanged -= OnPowerUpSpawnChanged;
        _vipSpawnSlider.ValueChanged -= OnVipSpawnChanged;
    }
}
