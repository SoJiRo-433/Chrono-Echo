using Godot;

/// <summary>
/// The glowing pendant on the floor: Aionia's key story item and the source of the Echo
/// ability. Implements IInteractable, so picking it up goes through the same
/// Entity.Interact() path as a switch or a door.
///
/// Picking it up puts it in the Player's inventory, sets the story flag, plays the memory
/// flash and then removes it from the floor. It pulses so the player can spot it.
/// </summary>
public partial class Pendant : Node2D, IInteractable
{
    #region Private state
    private string _dialogueId = "pendant_flash";
    private string _flagToSet = "touched_pendant";
    private string _itemName = "pendant";
    private bool _taken;
    private float _pulse;
    #endregion

    #region Public properties
    /// <summary>Conversation played when the pendant is picked up (the memory flash).</summary>
    [Export] public string DialogueId { get => _dialogueId; set => _dialogueId = value ?? ""; }

    /// <summary>Story flag set when the pendant is taken.</summary>
    [Export] public string FlagToSet { get => _flagToSet; set => _flagToSet = value ?? ""; }

    /// <summary>Name it is stored under in the Player's inventory.</summary>
    [Export] public string ItemName { get => _itemName; set => _itemName = value ?? ""; }

    public bool IsTaken => _taken;

    /// <summary>The pendant is a small circle lying on the floor.</summary>
    public float DistanceTo(Vector2 from) => from.DistanceTo(Position);
    #endregion

    #region Lifecycle
    public override void _Ready() { QueueRedraw(); }

    public override void _Process(double delta)
    {
        if (_taken) return;
        _pulse += (float)delta;
        QueueRedraw();
    }
    #endregion

    #region IInteractable
    public void Interact(Entity interactor)
    {
        if (_taken) return;
        _taken = true;

        if (interactor is Player player) player.AddItem(_itemName);
        GameStateManager.Instance?.SetFlag(_flagToSet, true);

        DialogueManager dialogue = (GetParent() as Room)?.Dialogue;
        if (dialogue != null && _dialogueId != "") dialogue.StartDialogue(_dialogueId);

        Hide();
    }
    #endregion

    #region Drawing
    public override void _Draw()
    {
        // The glow breathes, so the eye finds it in the middle of the rubble.
        float breath = 1f + 0.22f * Mathf.Sin(_pulse * 2.5f);
        DrawCircle(Vector2.Zero, 24f * breath, new Color(0.2f, 0.75f, 1f, 0.16f));
        DrawCircle(Vector2.Zero, 13f, new Color(0.65f, 0.93f, 1f));
        DrawArc(Vector2.Zero, 13f, 0f, Mathf.Tau, 24, new Color(0f, 0.65f, 0.85f), 2f);
    }
    #endregion
}
