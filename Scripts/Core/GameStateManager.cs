using System.Collections.Generic;
using Godot;

public partial class GameStateManager : Node
{
    #region Singleton
    public static GameStateManager Instance { get; private set; }

    public override void _EnterTree()
    {
        // Guard: if a second copy ever appears, remove it so there is always exactly one.
        if (Instance != null && Instance != this) { QueueFree(); return; }
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }
    #endregion

    #region Signals
    [Signal] public delegate void FlagChangedEventHandler(string flagName, bool value);
    [Signal] public delegate void FlagsResetEventHandler();
    [Signal] public delegate void PlayerDiedEventHandler();
    [Signal] public delegate void GameOverEventHandler();
    [Signal] public delegate void PauseChangedEventHandler(bool isPaused);
    #endregion

    #region State
    private readonly Dictionary<string, bool> _flags = new();

    public Room CurrentRoom { get; private set; }
    public Player Player { get; private set; }
    public bool IsPaused { get; private set; }

    public IReadOnlyDictionary<string, bool> Flags => _flags;
    #endregion

    #region Registration (called by Room and Player when they enter the scene)
    public void RegisterRoom(Room room) { CurrentRoom = room; }
    public void RegisterPlayer(Player player) { Player = player; }
    #endregion

    #region Narrative flags
    public void SetFlag(string flagName, bool value = true)
    {
        if (string.IsNullOrEmpty(flagName)) return;

        bool changed = !_flags.TryGetValue(flagName, out bool oldValue) || oldValue != value;
        _flags[flagName] = value;
        if (changed) EmitSignal(SignalName.FlagChanged, flagName, value);
    }

    public bool GetFlag(string flagName)
    {
        return !string.IsNullOrEmpty(flagName) && _flags.TryGetValue(flagName, out bool value) && value;
    }

    public void ResetFlags()
    {
        _flags.Clear();
        EmitSignal(SignalName.FlagsReset);
    }
    #endregion

    #region Level progression
    private int _currentLevelNumber = 1;
    private int _carriedLives;

    public int CurrentLevelNumber => _currentLevelNumber;

    public int CarriedLives
    {
        get => _carriedLives;
        set => _carriedLives = Mathf.Max(0, value);
    }

    public bool HasCarriedLives => _carriedLives > 0;

    public void RegisterLevel(int levelNumber) { _currentLevelNumber = Mathf.Max(1, levelNumber); }

    public bool GoToNextLevel(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return false;

        Error error = GetTree().ChangeSceneToFile(scenePath);
        if (error != Error.Ok)
        {
            GD.PushError("GameStateManager: could not load next level '" + scenePath + "' (" + error + ").");
            return false;
        }

        // The old level is on its way out: forget its Player and Room so nothing can reach
        // into a freed node while the new scene is being built. The new Player registers
        // itself again in its own _Ready().
        Player = null;
        CurrentRoom = null;
        return true;
    }
    #endregion

    #region Game events and pause
    public void NotifyPlayerDied() { EmitSignal(SignalName.PlayerDied); }
    public void TriggerGameOver() { EmitSignal(SignalName.GameOver); }

    public void SetPaused(bool paused)
    {
        if (IsPaused == paused) return;
        IsPaused = paused;
        EmitSignal(SignalName.PauseChanged, paused);
    }
    #endregion
}
