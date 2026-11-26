using System.Collections.Generic;
using Godot;

public enum SkipMode
{
    ReadOnly = 0,
    All = 1
}

/// <summary>Holds and persists player-configurable audio, dialogue, and display preferences
/// across sessions, plus the set of dialogue lines the player has already seen.</summary>
public partial class SettingsManager : Node
{
    private const string SettingsPath = "user://settings.cfg";
    private const string ReadLinesPath = "user://read_dialogue.cfg";

    public const string MasterBus = "Master";
    public const string MusicBus = "Music";
    public const string SfxBus = "SFX";

    public const float DefaultMusicVolume = 1.0f;
    public const float DefaultSfxVolume = 1.0f;
    public const float DefaultTextSpeed = 0.5f;
    public const bool DefaultAutoText = false;
    public const float DefaultAutoTextDelay = 1.5f;
    public const SkipMode DefaultSkipMode = SkipMode.ReadOnly;
    public const bool DefaultFullscreen = false;

    #region Singleton
    public static SettingsManager Instance { get; private set; }

    public override void _EnterTree()
    {
        if (Instance != null && Instance != this) { QueueFree(); return; }
        Instance = this;
        EnsureAudioBuses();
    }

    public override void _ExitTree()
    {
        if (DisplayManager.Instance != null)
            DisplayManager.Instance.FullscreenChanged -= OnDisplayFullscreenChanged;
        FlushReadLines();
        if (Instance == this) Instance = null;
    }
    #endregion

    #region Signals
    [Signal] public delegate void SettingsChangedEventHandler();
    #endregion

    #region State
    private float _musicVolume = DefaultMusicVolume;
    private float _sfxVolume = DefaultSfxVolume;
    private float _textSpeed = DefaultTextSpeed;
    private bool _autoText = DefaultAutoText;
    private float _autoTextDelay = DefaultAutoTextDelay;
    private SkipMode _skipMode = DefaultSkipMode;
    private bool _isFullscreen = DefaultFullscreen;
    private bool _hasSavedDisplaySetting;

    private readonly HashSet<string> _readLines = new();
    private bool _readLinesDirty;

    public float MusicVolume => _musicVolume;
    public float SfxVolume => _sfxVolume;
    public float TextSpeed => _textSpeed;
    public bool AutoText => _autoText;
    public float AutoTextDelay => _autoTextDelay;
    public SkipMode SkipMode => _skipMode;
    public bool IsFullscreen => _isFullscreen;

    /// <summary>Characters per second for the dialogue typewriter effect, or PositiveInfinity for instant.</summary>
    public float CharactersPerSecond => _textSpeed >= 0.99f ? float.PositiveInfinity : Mathf.Lerp(20f, 100f, _textSpeed);
    #endregion

    #region Lifecycle
    public override void _Ready()
    {
        if (Instance != this) return;

        ProcessMode = ProcessModeEnum.Always;
        LoadSettings();
        LoadReadLines();
        ApplyAudioSettings();

        if (_hasSavedDisplaySetting)
            ApplyDisplayMode(_isFullscreen);
        else
            _isFullscreen = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;

        Callable.From(ConnectDisplayManager).CallDeferred();
    }

    private void ConnectDisplayManager()
    {
        if (DisplayManager.Instance == null) return;
        DisplayManager.Instance.FullscreenChanged -= OnDisplayFullscreenChanged;
        DisplayManager.Instance.FullscreenChanged += OnDisplayFullscreenChanged;
        if (_hasSavedDisplaySetting && DisplayManager.Instance.IsFullscreen != _isFullscreen)
            DisplayManager.Instance.SetFullscreen(_isFullscreen);
        else
            _isFullscreen = DisplayManager.Instance.IsFullscreen;
    }
    #endregion

    #region Public API - Settings
    public void SetMusicVolume(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, 1f);
        if (Mathf.IsEqualApprox(_musicVolume, clamped)) return;
        _musicVolume = clamped;
        ApplyBusVolume(MusicBus, _musicVolume);
        SaveSettings();
        EmitSignal(SignalName.SettingsChanged);
    }

    public void SetSfxVolume(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, 1f);
        if (Mathf.IsEqualApprox(_sfxVolume, clamped)) return;
        _sfxVolume = clamped;
        ApplyBusVolume(SfxBus, _sfxVolume);
        SaveSettings();
        EmitSignal(SignalName.SettingsChanged);
    }

    public void SetTextSpeed(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, 1f);
        if (Mathf.IsEqualApprox(_textSpeed, clamped)) return;
        _textSpeed = clamped;
        SaveSettings();
        EmitSignal(SignalName.SettingsChanged);
    }

    public void SetAutoText(bool enabled)
    {
        if (_autoText == enabled) return;
        _autoText = enabled;
        SaveSettings();
        EmitSignal(SignalName.SettingsChanged);
    }

    public void SetAutoTextDelay(float seconds)
    {
        float clamped = Mathf.Clamp(seconds, 0.5f, 5.0f);
        if (Mathf.IsEqualApprox(_autoTextDelay, clamped)) return;
        _autoTextDelay = clamped;
        SaveSettings();
        EmitSignal(SignalName.SettingsChanged);
    }

    public void SetSkipMode(SkipMode mode)
    {
        if (_skipMode == mode) return;
        _skipMode = mode;
        SaveSettings();
        EmitSignal(SignalName.SettingsChanged);
    }

    public void SetFullscreen(bool fullscreen)
    {
        _hasSavedDisplaySetting = true;
        if (DisplayManager.Instance != null)
        {
            if (DisplayManager.Instance.IsFullscreen != fullscreen)
            {
                DisplayManager.Instance.SetFullscreen(fullscreen);
                return;
            }
        }
        else
        {
            ApplyDisplayMode(fullscreen);
        }

        if (_isFullscreen == fullscreen) return;
        _isFullscreen = fullscreen;
        SaveSettings();
        EmitSignal(SignalName.SettingsChanged);
    }

    public void ResetDefaults()
    {
        _musicVolume = DefaultMusicVolume;
        _sfxVolume = DefaultSfxVolume;
        _textSpeed = DefaultTextSpeed;
        _autoText = DefaultAutoText;
        _autoTextDelay = DefaultAutoTextDelay;
        _skipMode = DefaultSkipMode;
        ApplyAudioSettings();
        SetFullscreen(DefaultFullscreen);
        SaveSettings();
        EmitSignal(SignalName.SettingsChanged);
    }
    #endregion

    #region Public API - Read dialogue tracking
    public static string MakeLineKey(string dialogueId, string nodeId, int lineIndex) =>
        $"{dialogueId ?? ""}|{nodeId ?? ""}|{lineIndex}";

    public bool IsLineRead(string key) => !string.IsNullOrEmpty(key) && _readLines.Contains(key);

    public void MarkLineRead(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (_readLines.Add(key)) _readLinesDirty = true;
    }

    public void FlushReadLines()
    {
        if (!_readLinesDirty) return;
        SaveReadLines();
        _readLinesDirty = false;
    }
    #endregion

    #region Audio buses
    public static void EnsureAudioBuses()
    {
        EnsureBus(MusicBus);
        EnsureBus(SfxBus);
    }

    private static void EnsureBus(string busName)
    {
        if (AudioServer.GetBusIndex(busName) >= 0) return;
        int index = AudioServer.BusCount;
        AudioServer.AddBus(index);
        AudioServer.SetBusName(index, busName);
        AudioServer.SetBusSend(index, MasterBus);
    }

    private void ApplyAudioSettings()
    {
        EnsureAudioBuses();
        ApplyBusVolume(MusicBus, _musicVolume);
        ApplyBusVolume(SfxBus, _sfxVolume);
    }

    private static void ApplyBusVolume(string busName, float linear)
    {
        int idx = AudioServer.GetBusIndex(busName);
        if (idx < 0) return;
        bool mute = linear <= 0.001f;
        AudioServer.SetBusMute(idx, mute);
        if (!mute) AudioServer.SetBusVolumeDb(idx, Mathf.LinearToDb(Mathf.Clamp(linear, 0.001f, 1f)));
    }
    #endregion

    #region Display sync
    private static void ApplyDisplayMode(bool fullscreen)
    {
        DisplayServer.WindowMode target = fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed;
        if (DisplayServer.WindowGetMode() != target)
            DisplayServer.WindowSetMode(target);
    }

    private void OnDisplayFullscreenChanged(bool isFullscreen)
    {
        _hasSavedDisplaySetting = true;
        if (_isFullscreen == isFullscreen) return;
        _isFullscreen = isFullscreen;
        SaveSettings();
        EmitSignal(SignalName.SettingsChanged);
    }
    #endregion

    #region Persistence
    private void LoadSettings()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SettingsPath) != Error.Ok) return;

        _musicVolume = Mathf.Clamp((float)cfg.GetValue("audio", "music_volume", DefaultMusicVolume).AsDouble(), 0f, 1f);
        _sfxVolume = Mathf.Clamp((float)cfg.GetValue("audio", "sfx_volume", DefaultSfxVolume).AsDouble(), 0f, 1f);
        _textSpeed = Mathf.Clamp((float)cfg.GetValue("dialogue", "text_speed", DefaultTextSpeed).AsDouble(), 0f, 1f);
        _autoText = cfg.GetValue("dialogue", "auto_text", DefaultAutoText).AsBool();
        _autoTextDelay = Mathf.Clamp((float)cfg.GetValue("dialogue", "auto_text_delay", DefaultAutoTextDelay).AsDouble(), 0.5f, 5.0f);

        int rawSkip = cfg.GetValue("dialogue", "skip_mode", (int)DefaultSkipMode).AsInt32();
        _skipMode = rawSkip == (int)SkipMode.All ? SkipMode.All : SkipMode.ReadOnly;

        if (cfg.HasSectionKey("display", "fullscreen"))
        {
            _hasSavedDisplaySetting = true;
            _isFullscreen = cfg.GetValue("display", "fullscreen", DefaultFullscreen).AsBool();
        }
    }

    private void SaveSettings()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("audio", "music_volume", _musicVolume);
        cfg.SetValue("audio", "sfx_volume", _sfxVolume);
        cfg.SetValue("dialogue", "text_speed", _textSpeed);
        cfg.SetValue("dialogue", "auto_text", _autoText);
        cfg.SetValue("dialogue", "auto_text_delay", _autoTextDelay);
        cfg.SetValue("dialogue", "skip_mode", (int)_skipMode);
        cfg.SetValue("display", "fullscreen", _isFullscreen);
        cfg.Save(SettingsPath);
    }

    private void LoadReadLines()
    {
        _readLines.Clear();
        var cfg = new ConfigFile();
        if (cfg.Load(ReadLinesPath) != Error.Ok) return;

        Variant raw = cfg.GetValue("dialogue", "read_keys", new Godot.Collections.Array<string>());
        foreach (string key in raw.AsStringArray())
            if (!string.IsNullOrEmpty(key)) _readLines.Add(key);
    }

    private void SaveReadLines()
    {
        var arr = new string[_readLines.Count];
        _readLines.CopyTo(arr);
        var cfg = new ConfigFile();
        cfg.SetValue("dialogue", "read_keys", arr);
        cfg.Save(ReadLinesPath);
    }
    #endregion
}
