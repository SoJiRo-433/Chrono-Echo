using System.Collections.Generic;
using Godot;

/// <summary>
/// SINGLETON registered as a Godot AUTOLOAD (see project.godot).
/// Single source of truth for narrative progress and "who/where is the player".
///
/// OOP roles:
///  - SINGLETON:     static Instance, only one exists for the whole game.
///  - ENCAPSULATION: the flag dictionary is private; use SetFlag/GetFlag/ResetFlags.
///  - Decoupling:    other systems react to its Godot signals instead of polling.
/// </summary>
public partial class GameStateManager : Node
{
    #region Singleton
    /// <summary>Global access point. Null only if the autoload is not configured.</summary>
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

    /// <summary>Read-only view of all flags (for debugging / HUD).</summary>
    public IReadOnlyDictionary<string, bool> Flags => _flags;
    #endregion

    #region Registration (called by Room and Player when they enter the scene)
    public void RegisterRoom(Room room) { CurrentRoom = room; }
    public void RegisterPlayer(Player player) { Player = player; }
    #endregion

    #region Narrative flags
    /// <summary>Sets a story flag and announces the change (only if the value changed).</summary>
    public void SetFlag(string flagName, bool value = true)
    {
        if (string.IsNullOrEmpty(flagName)) return;

        bool changed = !_flags.TryGetValue(flagName, out bool oldValue) || oldValue != value;
        _flags[flagName] = value;
        if (changed) EmitSignal(SignalName.FlagChanged, flagName, value);
    }

    /// <summary>Unknown flags are simply "false".</summary>
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

    /// <summary>1-based number of the level being played (the Room reports it in _Ready).</summary>
    public int CurrentLevelNumber => _currentLevelNumber;

    /// <summary>
    /// Lives left over from the previous level. 0 means "nothing carried over", so a brand
    /// new Player falls back to its own StartingLives.
    /// </summary>
    public int CarriedLives
    {
        get => _carriedLives;
        set => _carriedLives = Mathf.Max(0, value);
    }

    public bool HasCarriedLives => _carriedLives > 0;

    /// <summary>Called by Room._Ready() so the HUD and the carry-over logic know the level.</summary>
    public void RegisterLevel(int levelNumber) { _currentLevelNumber = Mathf.Max(1, levelNumber); }

    /// <summary>
    /// Loads 'scenePath' as the next level. This node is an AUTOLOAD, so it survives the
    /// scene change: flags, memory fragments and carried lives are all kept. Returns false
    /// when there is no next level to load.
    /// </summary>
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
