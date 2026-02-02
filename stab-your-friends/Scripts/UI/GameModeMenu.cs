using System;
using Godot;

namespace StabYourFriends.UI;

public partial class GameModeMenu : Control
{
    [Signal] public delegate void GameModeSelectedEventHandler(string gameMode);
    [Signal] public delegate void CancelledEventHandler();

    private Button _freeForAllButton = null!;
    private Button _teamBattleButton = null!;
    private Button _survivalButton = null!;
    private Button _cancelButton = null!;

    public override void _Ready()
    {
        _freeForAllButton = GetNode<Button>("%FreeForAllButton");
        _teamBattleButton = GetNode<Button>("%TeamBattleButton");
        _survivalButton = GetNode<Button>("%SurvivalButton");
        _cancelButton = GetNode<Button>("%CancelButton");

        _freeForAllButton.Pressed += () => OnGameModeSelected("FreeForAll");
        _teamBattleButton.Pressed += () => OnGameModeSelected("TeamBattle");
        _survivalButton.Pressed += () => OnGameModeSelected("Survival");
        _cancelButton.Pressed += OnCancelPressed;
    }

    private void OnGameModeSelected(string mode)
    {
        GD.Print($"Game mode selected: {mode}");
        EmitSignal(SignalName.GameModeSelected, mode);
    }

    private void OnCancelPressed()
    {
        EmitSignal(SignalName.Cancelled);
    }

    public override void _ExitTree()
    {
        _freeForAllButton.Pressed -= () => OnGameModeSelected("FreeForAll");
        _teamBattleButton.Pressed -= () => OnGameModeSelected("TeamBattle");
        _survivalButton.Pressed -= () => OnGameModeSelected("Survival");
        _cancelButton.Pressed -= OnCancelPressed;
    }
}
