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
<<<<<<< Updated upstream
=======
    [Signal] public delegate void AffectionChangedEventHandler(string character, float value);
>>>>>>> Stashed changes
    #endregion

    #region State
    private readonly Dictionary<string, bool> _flags = new();
<<<<<<< Updated upstream
=======
    // Stored as integer tenths so old JSON remains integer-based while supporting PDF deltas.
    private readonly Dictionary<string, int> _affection = new();
>>>>>>> Stashed changes

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

<<<<<<< Updated upstream
=======
    #region Affection / route tracking
    // The four characters the doc gives routes to, under the names the dialogue data uses.
    public const string Yoruka = "yoruka";
    public const string Adellaine = "adellaine";
    public const string Hesiod = "hesiod";
    public const string Felix = "felix";

    // How much affection it takes before a route is the one being taken.
    public const float RomanceThreshold = 3f;

    public IReadOnlyDictionary<string, int> AffectionTenths => _affection;

    public float GetAffection(string character)
    {
        return !string.IsNullOrEmpty(character) && _affection.TryGetValue(character, out int value) ? value / 10f : 0f;
    }

    public void AddAffection(string character, float amount)
    {
        if (string.IsNullOrEmpty(character) || Mathf.IsZeroApprox(amount)) return;
        int units = Mathf.RoundToInt(amount * 10f);
        _affection[character] = GetStoredAffection(character) + units;
        EmitSignal(SignalName.AffectionChanged, character, GetAffection(character));
    }

    public void SetAffection(string character, float value)
    {
        if (string.IsNullOrEmpty(character)) return;

        int units = Mathf.RoundToInt(value * 10f);
        _affection[character] = units;
        EmitSignal(SignalName.AffectionChanged, character, units / 10f);
    }

    private int GetStoredAffection(string character) => _affection.TryGetValue(character, out int units) ? units : 0;

    public void ResetAffection()
    {
        _affection.Clear();
        foreach (string character in new[] { Yoruka, Adellaine, Hesiod, Felix })
            EmitSignal(SignalName.AffectionChanged, character, 0);
    }
    #endregion

>>>>>>> Stashed changes
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
<<<<<<< Updated upstream
=======

    #region Save / load handshake
    public override void _Ready()
    {
        // SaveManager owns the files; this singleton owns the state that goes into them.
        SaveManager.CaptureState = BuildSaveState;
        SaveManager.GameLoaded += OnGameLoaded;
        SaveManager.NewGameStarted += OnNewGame;
    }

    /// <summary>The slice of the singleton a save file holds: the narrative flags and the
    /// affection counters. Which scene the save is in is stored by SaveManager itself.</summary>
    private Godot.Collections.Dictionary BuildSaveState()
    {
        var flags = new Godot.Collections.Dictionary();
        foreach (KeyValuePair<string, bool> pair in _flags) flags[pair.Key] = pair.Value;

        var affection = new Godot.Collections.Dictionary();
        foreach (KeyValuePair<string, int> pair in _affection) affection[pair.Key] = pair.Value;

        return new Godot.Collections.Dictionary
        {
            { "flags", flags },
            { "affection", affection },
            { "affection_scale", 10 }
        };
    }

    private void OnGameLoaded(Godot.Collections.Dictionary state)
    {
        _flags.Clear();
        _affection.Clear();

        if (state != null && state.ContainsKey("flags"))
        {
            Godot.Collections.Dictionary saved = state["flags"].AsGodotDictionary();
            foreach (Variant key in saved.Keys) _flags[key.AsString()] = saved[key].AsBool();
        }

        if (state != null && state.ContainsKey("affection"))
        {
            Godot.Collections.Dictionary saved = state["affection"].AsGodotDictionary();
            bool valuesAreTenths = state.ContainsKey("affection_scale") && state["affection_scale"].AsInt32() == 10;
            foreach (Variant key in saved.Keys)
            {
                int value = saved[key].AsInt32();
                _affection[key.AsString()] = valuesAreTenths ? value : value * 10;
            }
        }

        // Every listener re-reads from scratch rather than trying to diff two worlds.
        EmitSignal(SignalName.FlagsReset);
        foreach (KeyValuePair<string, bool> pair in _flags)
            EmitSignal(SignalName.FlagChanged, pair.Key, pair.Value);
        foreach (KeyValuePair<string, int> pair in _affection)
            EmitSignal(SignalName.AffectionChanged, pair.Key, pair.Value / 10f);
    }

    private void OnNewGame()
    {
        ResetFlags();
        ResetAffection();
    }
    #endregion

    #region Ending resolution
    /// <summary>Reads the flags and the affection counters and returns the id of one of the
    /// endings the design doc lists. Nothing triggers it in gameplay yet - the levels that would
    /// are not built - but every ending rule lives here, in one place, so the later levels only
    /// have to send the player to the ending screen.</summary>
    public string ResolveEnding()
    {
        bool timelineFixed = GetFlag("atrium_complete");
        bool remembered = GetFlag("asked_identity");

        // Romance routes win once the affection is actually there.
        if (GetAffection(Felix) >= RomanceThreshold) return ResolveFelixEnding(timelineFixed);
        if (GetAffection(Yoruka) >= RomanceThreshold) return timelineFixed ? "yoruka_good" : "yoruka_bad";
        if (GetAffection(Adellaine) >= RomanceThreshold) return timelineFixed ? "adellaine_good" : "adellaine_bad";
        if (GetAffection(Hesiod) >= RomanceThreshold) return timelineFixed ? "hesiod_good" : "hesiod_bad";

        if (!remembered) return "no_more_time";            // she never pieced the truth together
        if (GetFlag("chose_sacrifice")) return "sacrificial";
        return timelineFixed ? "neutral_good" : "no_more_time";
    }

    private string ResolveFelixEnding(bool timelineFixed)
    {
        // Without the timeline fixed he takes them both down with him.
        if (!timelineFixed) return "felix_bad_together";
        return GetFlag("remembered_felix") ? "true_ending" : "felix_good";
    }
    #endregion
>>>>>>> Stashed changes
}
