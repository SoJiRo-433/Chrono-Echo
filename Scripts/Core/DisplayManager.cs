using Godot;

public partial class DisplayManager : Node
{
    #region Singleton
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
    [Signal] public delegate void FullscreenChangedEventHandler(bool isFullscreen);
    #endregion

    #region Input action
    public const string ToggleFullscreenAction = "toggle_fullscreen";

    public static void RegisterInputActions()
    {
        if (InputMap.HasAction(ToggleFullscreenAction)) return;
        InputMap.AddAction(ToggleFullscreenAction);
        InputMap.ActionAddEvent(ToggleFullscreenAction, new InputEventKey { PhysicalKeycode = Key.F11 });
    }
    #endregion

    #region State
    private bool _isFullscreen;

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

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!(@event is InputEventKey key) || !key.Pressed || key.IsEcho()) return;
        if (!InputMap.EventIsAction(@event, ToggleFullscreenAction)) return;

        GetViewport()?.SetInputAsHandled();
        ToggleFullscreen();
    }
    #endregion

    #region Public API
    public void ToggleFullscreen() => SetFullscreen(!_isFullscreen);

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
