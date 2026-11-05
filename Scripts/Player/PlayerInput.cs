using Godot;

public sealed class PlayerInput
{
    #region Input map registration
    public static void RegisterInputActions()
    {
        AddKeyAction("move_up", Key.W, Key.Up);
        AddKeyAction("move_down", Key.S, Key.Down);
        AddKeyAction("move_left", Key.A, Key.Left);
        AddKeyAction("move_right", Key.D, Key.Right);
        AddKeyAction("interact", Key.E);
        AddKeyAction("attack", Key.J);
        AddKeyAction("jump", Key.Space);
        AddKeyAction("toggle_capture", Key.T);
        AddKeyAction("project_echo", Key.R);
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
    public PlayerAction ReadAction()
    {
        // A jump is a single tick, so it takes the tick it was pressed on: the step she gives up
        // is one sixtieth of a second of walking, and the recording keeps the leap where it
        // happened. Dialogue is paused, so Space is free for this while a conversation is closed.
        if (Input.IsActionJustPressed("jump")) return PlayerAction.Jump;
        if (Input.IsActionJustPressed("interact")) return PlayerAction.Interact;
        if (Input.IsActionJustPressed("attack")) return PlayerAction.Attack;
        if (Input.IsActionPressed("move_up")) return PlayerAction.MoveUp;
        if (Input.IsActionPressed("move_down")) return PlayerAction.MoveDown;
        if (Input.IsActionPressed("move_left")) return PlayerAction.MoveLeft;
        if (Input.IsActionPressed("move_right")) return PlayerAction.MoveRight;
        return PlayerAction.Idle;
    }

    public bool IsProjectEchoPressed() => Input.IsActionJustPressed("project_echo");

    public bool IsCaptureTogglePressed() => Input.IsActionJustPressed("toggle_capture");
    #endregion

    #region Helpers
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
