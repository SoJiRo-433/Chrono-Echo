using Godot;

/// <summary>
/// Translates raw keyboard state into ONE PlayerAction per physics tick.
///
/// OOP role: ENCAPSULATION / separation of concerns. Player never touches the Godot Input
/// API directly - it asks this class "what is the player doing right now?". The same
/// PlayerAction enum is what the recorder stores and the Echo replays.
///
/// Input actions are registered in code so the project works without editing the
/// Input Map by hand. Controls: WASD/Arrows move, E interact, J attack, T capture,
/// R reset loop.
/// </summary>
public sealed class PlayerInput
{
    #region Input map registration
    /// <summary>Safe to call many times: an action is only created once.</summary>
    public static void RegisterInputActions()
    {
        AddKeyAction("move_up", Key.W, Key.Up);
        AddKeyAction("move_down", Key.S, Key.Down);
        AddKeyAction("move_left", Key.A, Key.Left);
        AddKeyAction("move_right", Key.D, Key.Right);
        AddKeyAction("interact", Key.E);
        AddKeyAction("attack", Key.J);
        AddKeyAction("toggle_capture", Key.T);
        AddKeyAction("reset_loop", Key.R);
    }

    private static void AddKeyAction(string action, params Key[] keys)
    {
        if (InputMap.HasAction(action)) return;
        InputMap.AddAction(action);
        foreach (Key key in keys)
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }
    #endregion

    #region Reading input
    /// <summary>
    /// One-shot actions (Interact, Attack) win over movement, because they must never be
    /// missed. Movement is 4-directional: only one direction per tick (simple + replayable).
    /// </summary>
    public PlayerAction ReadAction()
    {
        if (Input.IsActionJustPressed("interact")) return PlayerAction.Interact;
        if (Input.IsActionJustPressed("attack")) return PlayerAction.Attack;
        if (Input.IsActionPressed("move_up")) return PlayerAction.MoveUp;
        if (Input.IsActionPressed("move_down")) return PlayerAction.MoveDown;
        if (Input.IsActionPressed("move_left")) return PlayerAction.MoveLeft;
        if (Input.IsActionPressed("move_right")) return PlayerAction.MoveRight;
        return PlayerAction.Idle;
    }

    public bool IsResetPressed() => Input.IsActionJustPressed("reset_loop");

    /// <summary>True on the tick the player presses the capture key (T).</summary>
    public bool IsCaptureTogglePressed() => Input.IsActionJustPressed("toggle_capture");
    #endregion

    #region Helpers
    /// <summary>Unit direction for a movement action (zero for non-movement actions).</summary>
    public static Vector2 DirectionOf(PlayerAction action)
    {
        switch (action)
        {
            case PlayerAction.MoveUp: return Vector2.Up;
            case PlayerAction.MoveDown: return Vector2.Down;
            case PlayerAction.MoveLeft: return Vector2.Left;
            case PlayerAction.MoveRight: return Vector2.Right;
            default: return Vector2.Zero;
        }
    }
    #endregion
}
