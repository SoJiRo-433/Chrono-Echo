using Godot;

public readonly struct ActionFrame
{
    public ActionFrame(float timestamp, Vector2 position, PlayerAction action)
    {
        Timestamp = timestamp;
        Position = position;
        Action = action;
    }

    public float Timestamp { get; }

    public Vector2 Position { get; }

    public PlayerAction Action { get; }

    public bool IsWorldAffecting => Action == PlayerAction.Interact || Action == PlayerAction.Attack;
}
