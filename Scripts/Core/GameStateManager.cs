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
    [Signal] public delegate void AffectionChangedEventHandler(string character, int value);
    #endregion

    #region State
    private readonly Dictionary<string, bool> _flags = new();
    private readonly Dictionary<string, int> _affection = new();

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

    #region Affection / route tracking
    // The four characters the doc gives routes to, under the names the dialogue data uses.
    public const string Yoruka = "yoruka";
    public const string Adellaine = "adellaine";
    public const string Hesiod = "hesiod";
    public const string Felix = "felix";

    // How much affection it takes before a route is the one being taken.
    public const int RomanceThreshold = 3;

    public IReadOnlyDictionary<string, int> Affection => _affection;

    public int GetAffection(string character)
    {
        return !string.IsNullOrEmpty(character) && _affection.TryGetValue(character, out int value) ? value : 0;
    }

    public void AddAffection(string character, int amount)
    {
        if (string.IsNullOrEmpty(character) || amount == 0) return;
        SetAffection(character, GetAffection(character) + amount);
    }

    public void SetAffection(string character, int value)
    {
        if (string.IsNullOrEmpty(character)) return;

        _affection[character] = value;
        EmitSignal(SignalName.AffectionChanged, character, value);
    }

    public void ResetAffection()
    {
        _affection.Clear();
        foreach (string character in new[] { Yoruka, Adellaine, Hesiod, Felix })
            EmitSignal(SignalName.AffectionChanged, character, 0);
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
            { "affection", affection }
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
            foreach (Variant key in saved.Keys) _affection[key.AsString()] = saved[key].AsInt32();
        }

        // Every listener re-reads from scratch rather than trying to diff two worlds.
        EmitSignal(SignalName.FlagsReset);
        foreach (KeyValuePair<string, bool> pair in _flags)
            EmitSignal(SignalName.FlagChanged, pair.Key, pair.Value);
        foreach (KeyValuePair<string, int> pair in _affection)
            EmitSignal(SignalName.AffectionChanged, pair.Key, pair.Value);
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
}
