using Godot;

/// <summary>
/// One recorded moment of a time loop: WHEN it happened, WHERE the player was,
/// and WHAT the player was doing.
///
/// It is an immutable (readonly) struct: once recorded, a frame can never change,
/// which is part of what makes the replay deterministic. A struct is a value type,
/// so thousands of frames do not create thousands of heap objects.
/// </summary>
public readonly struct ActionFrame
{
    public ActionFrame(float timestamp, Vector2 position, PlayerAction action)
    {
        Timestamp = timestamp;
        Position = position;
        Action = action;
    }

    /// <summary>Seconds since the loop started (the room's shared loop clock).</summary>
    public float Timestamp { get; }

    /// <summary>Player position at the START of the tick this frame was captured.</summary>
    public Vector2 Position { get; }

    public PlayerAction Action { get; }

    /// <summary>True for actions that change the world (the only ones an Echo re-performs).</summary>
    public bool IsWorldAffecting => Action == PlayerAction.Interact || Action == PlayerAction.Attack;
}
