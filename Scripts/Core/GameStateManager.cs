using System.Collections.Generic;
using Godot;

public partial class GameStateManager : Node
{
    private const string CompletionRecordPath = "user://chrono_echoes_progress.cfg";
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
    [Signal] public delegate void AffectionChangedEventHandler(string character, float value);
    #endregion

    public readonly struct DialogueLogEntry
    {
        public string Speaker { get; }
        public string Text { get; }

        public DialogueLogEntry(string speaker, string text)
        {
            Speaker = speaker ?? "";
            Text = text ?? "";
        }
    }

    private const int MaxDialogueLogEntries = 250;
    private readonly List<DialogueLogEntry> _dialogueLog = new();

    #region State
    private readonly Dictionary<string, bool> _flags = new();
    // Stored as integer tenths so old JSON remains integer-based while supporting PDF deltas.
    private readonly Dictionary<string, int> _affection = new();

    public Room CurrentRoom { get; private set; }
    public Player Player { get; private set; }
    public bool IsPaused { get; private set; }
    public bool CompletedRunBefore { get; private set; }

    public IReadOnlyDictionary<string, bool> Flags => _flags;
    public IReadOnlyList<DialogueLogEntry> DialogueLog => _dialogueLog;
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
        _dialogueLog.Clear();
        EmitSignal(SignalName.FlagsReset);
    }

    public void AddDialogueLog(string speaker, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (_dialogueLog.Count >= MaxDialogueLogEntries) _dialogueLog.RemoveAt(0);
        _dialogueLog.Add(new DialogueLogEntry(speaker, text));
    }

    public void ResetLevelFlags(int levelNumber, IEnumerable<string> additionalFlags = null)
    {
        if (levelNumber < 1) return;
        var prefixes = new List<string> { "level" + levelNumber + "_" };
        var exactFlags = new List<string>();
        switch (levelNumber)
        {
            case 2: prefixes.Add("library_"); break;
            case 3: exactFlags.Add("atrium_checkpoint"); break;
            case 4: prefixes.Add("medical_"); break;
            case 5: prefixes.Add("observation_"); break;
            case 6: exactFlags.Add("felix_left"); exactFlags.Add("hesiod_joined"); break;
        }
        var toRemove = new List<string>();
        foreach (string flag in _flags.Keys)
            foreach (string prefix in prefixes)
                if (flag.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) { toRemove.Add(flag); break; }
        toRemove.AddRange(exactFlags);
        if (additionalFlags != null)
            foreach (string flag in additionalFlags)
                if (!string.IsNullOrEmpty(flag)) toRemove.Add(flag);
        bool changedAny = false;
        foreach (string flag in toRemove)
            if (_flags.Remove(flag))
            {
                changedAny = true;
                EmitSignal(SignalName.FlagChanged, flag, false);
            }
        if (changedAny) EmitSignal(SignalName.FlagsReset);
    }
    #endregion

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
        var completion = new ConfigFile();
        if (completion.Load(CompletionRecordPath) == Error.Ok)
            CompletedRunBefore = completion.GetValue("progress", "completed_run", false).AsBool();

        // SaveManager owns the files; this singleton owns the state that goes into them.
        SaveManager.CaptureState = BuildSaveState;
        SaveManager.GameLoaded += OnGameLoaded;
        SaveManager.NewGameStarted += OnNewGame;
    }

    public void MarkRunCompleted()
    {
        CompletedRunBefore = true;
        var completion = new ConfigFile();
        completion.SetValue("progress", "completed_run", true);
        completion.Save(CompletionRecordPath);
    }

    private static readonly string[] KnownPuzzleFlags =
    {
        "touched_pendant",
        "room01_complete",
        "library_shelves_opened",
        "library_doors_opened",
        "library_complete",
        "atrium_gate_opened",
        "atrium_stairway_opened",
        "atrium_exit_opened",
        "atrium_complete",
        "medical_keypad_open",
        "medical_shutter_open",
        "medical_generator_active",
        "medical_case_taken",
        "F4_scan",
        "medical_complete",
        "level5_console_pressed",
        "observation_lock_open",
        "steam_1_shut",
        "steam_2_shut",
        "steam_3_shut",
        "observation_complete",
        "F6_ledger",
        "business_complete",
        "level7_shutter_held",
        "F7_camera_roll",
        "level7_hatch_open",
        "level7_complete",
        "level8_keypad_open",
        "level8_complete",
        "F9_resonance",
        "level9_complete",
        "F10_divergence",
        "level10_complete"
    };

    public static bool IsPuzzleFlag(string flagName)
    {
        if (string.IsNullOrEmpty(flagName)) return false;
        foreach (string known in KnownPuzzleFlags)
            if (flagName == known) return true;
        return flagName.EndsWith("_complete", System.StringComparison.Ordinal)
            || flagName.EndsWith("_opened", System.StringComparison.Ordinal)
            || flagName.EndsWith("_open", System.StringComparison.Ordinal)
            || flagName.StartsWith("F4_", System.StringComparison.Ordinal)
            || flagName.StartsWith("F6_", System.StringComparison.Ordinal)
            || flagName.StartsWith("F7_", System.StringComparison.Ordinal)
            || flagName.StartsWith("F8_", System.StringComparison.Ordinal)
            || flagName.StartsWith("F9_", System.StringComparison.Ordinal)
            || flagName.StartsWith("F10_", System.StringComparison.Ordinal);
    }

    public static int CountCompletedPuzzles(Godot.Collections.Dictionary flags)
    {
        if (flags == null) return 0;
        int count = 0;
        foreach (Variant key in flags.Keys)
            if (flags[key].AsBool() && IsPuzzleFlag(key.AsString()))
                count++;
        return count;
    }

    /// <summary>The slice of the singleton a save file holds: the narrative flags, completed
    /// puzzles, player position, objective, and affection counters. Which scene the save is in
    /// is stored by SaveManager itself.</summary>
    private Godot.Collections.Dictionary BuildSaveState()
    {
        var flags = new Godot.Collections.Dictionary();
        var completedPuzzles = new Godot.Collections.Array();
        foreach (KeyValuePair<string, bool> pair in _flags)
        {
            flags[pair.Key] = pair.Value;
            if (pair.Value && IsPuzzleFlag(pair.Key))
                completedPuzzles.Add(pair.Key);
        }

        var affection = new Godot.Collections.Dictionary();
        foreach (KeyValuePair<string, int> pair in _affection) affection[pair.Key] = pair.Value;

        var playerPosition = new Godot.Collections.Dictionary();
        if (Player != null)
        {
            playerPosition["x"] = Player.WorldPosition.X;
            playerPosition["y"] = Player.WorldPosition.Y;
        }

        return new Godot.Collections.Dictionary
        {
            { "flags", flags },
            { "completed_puzzles", completedPuzzles },
            { "affection", affection },
            { "affection_scale", 10 },
            { "player_position", playerPosition },
            { "objective", CurrentRoom?.Dialogue?.CurrentObjective ?? "" }
        };
    }

    private void OnGameLoaded(Godot.Collections.Dictionary state)
    {
        _flags.Clear();
        _affection.Clear();
        _dialogueLog.Clear();

        if (state != null && state.ContainsKey("flags"))
        {
            Godot.Collections.Dictionary saved = state["flags"].AsGodotDictionary();
            foreach (Variant key in saved.Keys) _flags[key.AsString()] = saved[key].AsBool();
        }

        if (state != null && state.ContainsKey("completed_puzzles") && state["completed_puzzles"].VariantType == Variant.Type.Array)
        {
            foreach (Variant entry in state["completed_puzzles"].AsGodotArray())
            {
                string puzzleFlag = entry.AsString();
                if (!string.IsNullOrEmpty(puzzleFlag)) _flags[puzzleFlag] = true;
            }
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

        if (Player != null && state != null && state.ContainsKey("player_position"))
        {
            Godot.Collections.Dictionary savedPosition = state["player_position"].AsGodotDictionary();
            if (savedPosition.ContainsKey("x") && savedPosition.ContainsKey("y"))
            {
                Player.WorldPosition = new Vector2(
                    (float)savedPosition["x"].AsDouble(),
                    (float)savedPosition["y"].AsDouble());
                CurrentRoom?.SnapCameraToPlayer();
            }
        }

        if (state != null && state.ContainsKey("objective"))
        {
            string objective = state["objective"].AsString();
            if (!string.IsNullOrEmpty(objective))
                CurrentRoom?.Dialogue?.SetObjective(objective);
        }
    }

    private void OnNewGame()
    {
        ResetFlags();
        ResetAffection();
    }
    #endregion

    #region Ending resolution
    /// <summary>Reads the flags and the affection counters and returns the id of one of the
    /// endings the design doc lists. Level 10 records its final choice here before sending the
    /// player to the ending screen.</summary>
    public string ResolveEnding()
    {
        if (GetFlag("final_choice_true_ending")) return "true_ending";
        if (GetFlag("final_choice_erase") || GetFlag("chose_sacrifice")) return "sacrificial";
        if (GetFlag("final_choice_do_nothing")) return "no_more_time";

        bool timelineFixed = GetFlag("final_choice_repair") || GetFlag("timeline_fixed") || GetFlag("atrium_complete");
        bool remembered = GetFlag("asked_identity") || GetFlag("F9_resonance") || GetFlag("F10_divergence");

        if (GetFlag("final_choice_repair"))
        {
            if (GetFlag("level10_felix_together")) return "felix_bad_together";
            if (GetFlag("level10_felix_bad")) return "felix_bad";
            if (GetFlag("level10_felix_good")) return "felix_good";
            if (GetFlag("level10_yoruka_bad")) return timelineFixed ? "yoruka_alternate" : "yoruka_bad";
            if (GetFlag("level10_yoruka_good")) return "yoruka_good";
            if (GetFlag("level10_adellaine_bad")) return "adellaine_bad";
            if (GetFlag("level10_adellaine_good")) return "adellaine_good";
            if (GetFlag("level10_hesiod_bad")) return "hesiod_bad";
            if (GetFlag("level10_hesiod_good")) return "hesiod_good";
        }

        // Romance routes win once the affection is actually there.
        if (GetAffection(Felix) >= RomanceThreshold) return ResolveFelixEnding(timelineFixed);
        if (GetAffection(Yoruka) >= RomanceThreshold)
            return GetFlag("level10_yoruka_bad") ? (timelineFixed ? "yoruka_alternate" : "yoruka_bad") : timelineFixed ? "yoruka_good" : "yoruka_bad";
        if (GetAffection(Adellaine) >= RomanceThreshold)
            return GetFlag("level10_adellaine_bad") ? "adellaine_bad" : timelineFixed ? "adellaine_good" : "adellaine_bad";
        if (GetAffection(Hesiod) >= RomanceThreshold)
            return GetFlag("level10_hesiod_bad") ? "hesiod_bad" : timelineFixed ? "hesiod_good" : "hesiod_bad";

        if (!remembered) return "no_more_time";            // she never pieced the truth together
        if (GetFlag("chose_sacrifice")) return "sacrificial";
        return timelineFixed ? "neutral_good" : "no_more_time";
    }

    private string ResolveFelixEnding(bool timelineFixed)
    {
        if (GetFlag("level10_felix_together")) return "felix_bad_together";
        if (GetFlag("level10_felix_good")) return "felix_good";
        if (GetFlag("level10_felix_bad") || GetFlag("FELIX_BAD_FED")) return "felix_bad";
        // Without the timeline fixed he takes them both down with him.
        if (!timelineFixed) return "felix_bad_together";
        return GetFlag("remembered_felix") ? "true_ending" : "felix_good";
    }
    #endregion
}



