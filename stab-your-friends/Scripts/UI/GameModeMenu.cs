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

    private Action _onFreeForAll = null!;
    private Action _onTeamBattle = null!;
    private Action _onSurvival = null!;

    public override void _Ready()
    {
        _freeForAllButton = GetNode<Button>("%FreeForAllButton");
        _teamBattleButton = GetNode<Button>("%TeamBattleButton");
        _survivalButton = GetNode<Button>("%SurvivalButton");
        _cancelButton = GetNode<Button>("%CancelButton");

        _onFreeForAll = () => OnGameModeSelected("FreeForAll");
        _onTeamBattle = () => OnGameModeSelected("TeamBattle");
        _onSurvival = () => OnGameModeSelected("Survival");

        _freeForAllButton.Pressed += _onFreeForAll;
        _teamBattleButton.Pressed += _onTeamBattle;
        _survivalButton.Pressed += _onSurvival;
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
        _freeForAllButton.Pressed -= _onFreeForAll;
        _teamBattleButton.Pressed -= _onTeamBattle;
        _survivalButton.Pressed -= _onSurvival;
        _cancelButton.Pressed -= OnCancelPressed;
    }
}
