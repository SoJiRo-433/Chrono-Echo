/// <summary>
/// The complete list of things the player can be doing in a single physics tick.
/// Used by the input layer, the recorder and the Echo replay, so all three speak
/// the same "language".
/// </summary>
public enum PlayerAction
{
<<<<<<< Updated upstream
    Idle,
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Attack,
    Interact
=======
	Idle,
	MoveUp,
	MoveDown,
	MoveLeft,
	MoveRight,
	Attack,
	Interact,
	InteractHeld,
	Jump
>>>>>>> Stashed changes
}
