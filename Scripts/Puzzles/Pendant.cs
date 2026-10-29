using Godot;

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
    [Export] public string DialogueId { get => _dialogueId; set => _dialogueId = value ?? ""; }

    [Export] public string FlagToSet { get => _flagToSet; set => _flagToSet = value ?? ""; }

    [Export] public string ItemName { get => _itemName; set => _itemName = value ?? ""; }

    public bool IsTaken => _taken;

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
