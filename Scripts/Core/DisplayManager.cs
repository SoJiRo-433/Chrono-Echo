using Godot;

/// <summary>
/// SINGLETON registered as a Godot AUTOLOAD (see project.godot). Owns the WINDOW/display
/// concerns, kept apart from GameStateManager (which owns narrative + player state): a
/// display setting is a different responsibility from story progress.
///
/// Right now that is exactly one feature: <b>F11 toggles full screen</b>.
///
/// OOP roles:
///  - SINGLETON:              static Instance; survives every scene change, so the shortcut
///                           works in any room / level (and while gameplay is frozen).
///  - Separation of concerns: gameplay never calls the DisplayServer directly - it goes
///                           through this manager, or listens to FullscreenChanged.
///  - Encapsulation:          the window mode is private state; use IsFullscreen / SetFullscreen.
///
/// The toggle uses a proper INPUT ACTION ("toggle_fullscreen") registered in code, so it can
/// be rebound in the Input Map later - the same convention PlayerInput uses for gameplay keys.
/// </summary>
public partial class DisplayManager : Node
{
    #region Singleton
    /// <summary>Global access point. Null only if the autoload is not configured.</summary>
    public static DisplayManager Instance { get; private set; }

    public override void _EnterTree()
    {
        // Guard: if a second copy ever appears, remove it so there is always exactly one.
        if (Instance != null && Instance != this) { QueueFree(); return; }
        Instance = this;
        RegisterInputActions();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }
    #endregion

    #region Signals
    /// <summary>Raised whenever the window enters or leaves full screen (true = full screen).</summary>
    [Signal] public delegate void FullscreenChangedEventHandler(bool isFullscreen);
    #endregion

    #region Input action
    /// <summary>Name of the input action that toggles full screen (rebindable via the Input Map).</summary>
    public const string ToggleFullscreenAction = "toggle_fullscreen";

    /// <summary>Safe to call many times: the action is only created once.</summary>
    public static void RegisterInputActions()
    {
        if (InputMap.HasAction(ToggleFullscreenAction)) return;
        InputMap.AddAction(ToggleFullscreenAction);
        InputMap.ActionAddEvent(ToggleFullscreenAction, new InputEventKey { PhysicalKeycode = Key.F11 });
    }
    #endregion

    #region State
    private bool _isFullscreen;

    /// <summary>True while the window is full screen.</summary>
    public bool IsFullscreen => _isFullscreen;
    #endregion

    #region Lifecycle
    public override void _Ready()
    {
        if (Instance != this) return;   // a doomed duplicate from the singleton guard

        // Keep the shortcut alive even if the tree is ever genuinely paused (dialogue / game over).
        ProcessMode = ProcessModeEnum.Always;

        // Adopt whatever mode the window started in, so the first F11 press is never a no-op
        // in the wrong direction (e.g. a build configured to launch full screen).
        _isFullscreen = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
    }

    /// <summary>
    /// F11 arrives here because nothing else consumes it (the Room only handles F3, the
    /// DialogueManager only handles Enter/Space/1-9). Marking it handled stops the key from
    /// also reaching anything behind the overlay.
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!(@event is InputEventKey key) || !key.Pressed || key.IsEcho()) return;
        if (!InputMap.EventIsAction(@event, ToggleFullscreenAction)) return;

        GetViewport()?.SetInputAsHandled();
        ToggleFullscreen();
    }
    #endregion

    #region Public API
    /// <summary>Flips between windowed and full screen.</summary>
    public void ToggleFullscreen() => SetFullscreen(!_isFullscreen);

    /// <summary>
    /// Sets the window mode explicitly. Full screen here is Godot's borderless "Fullscreen"
    /// mode (not exclusive): it fills the screen with no border and alt-tabs cleanly.
    /// </summary>
    public void SetFullscreen(bool fullscreen)
    {
        if (_isFullscreen == fullscreen) return;

        _isFullscreen = fullscreen;
        DisplayServer.WindowSetMode(fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);

        EmitSignal(SignalName.FullscreenChanged, fullscreen);
    }
    #endregion
}
