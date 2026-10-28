using Godot;

/// <summary>
/// Scenery the player can examine with E: the report log, the destroyed time machine, the
/// broken cabinet, the pile of broken machinery.
///
/// OOP roles:
///  - INTERFACE implementation (IInteractable), so Entity.Interact() reaches it through
///    Room.FindNearestInteractable without knowing what concrete thing it is.
///  - ENCAPSULATION: whether it has been examined yet is private; outside code reads
///    HasBeenExamined.
///  - DECOUPLING: it does not hold any story text. It only knows WHICH registered dialogue
///    to start and WHICH flag to set - the words live in the DialogueManager.
/// </summary>
public partial class Inspectable : Node2D, IInteractable
{
    #region Private state
    private Vector2 _size = new Vector2(180f, 80f);
    private string _caption = "OBJECT";
    private Color _fill = new Color(0.45f, 0.42f, 0.38f, 0.85f);
    private Color _outline = new Color(0.8f, 0.76f, 0.7f);
    private string _firstDialogueId = "";
    private string _repeatDialogueId = "";
    private string _flagToSet = "";
    private bool _solid;
    private bool _examined;
    #endregion

    #region Public properties
    [Export] public Vector2 Size { get => _size; set { _size = value; QueueRedraw(); } }
    [Export] public string Caption { get => _caption; set { _caption = value ?? ""; QueueRedraw(); } }
    [Export] public Color Fill { get => _fill; set { _fill = value; QueueRedraw(); } }
    [Export] public Color Outline { get => _outline; set { _outline = value; QueueRedraw(); } }

    /// <summary>Conversation started the first time the player examines this object.</summary>
    [Export] public string FirstDialogueId { get => _firstDialogueId; set => _firstDialogueId = value ?? ""; }

    /// <summary>Conversation started on later examinations. Empty = play the first one again.</summary>
    [Export] public string RepeatDialogueId { get => _repeatDialogueId; set => _repeatDialogueId = value ?? ""; }

    /// <summary>Story flag set the first time this object is examined (empty = sets nothing).</summary>
    [Export] public string FlagToSet { get => _flagToSet; set => _flagToSet = value ?? ""; }

    /// <summary>Solid objects block movement, exactly like an Obstacle.</summary>
    [Export] public bool Solid { get => _solid; set => _solid = value; }

    public bool HasBeenExamined => _examined;
    public Rect2 Bounds => new Rect2(Position, _size);

    /// <summary>Measured to the nearest edge, so a big desk is usable from right beside it.</summary>
    public float DistanceTo(Vector2 from) => Bounds.DistanceToPoint(from);
    #endregion

    #region Lifecycle
    public override void _Ready()
    {
        if (_caption != "")
        {
            Label label = new Label
            {
                Text = _caption,
                Position = new Vector2(6f, 4f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 12);
            label.AddThemeColorOverride("font_color", _outline);
            AddChild(label);
        }
        QueueRedraw();
    }
    #endregion

    #region IInteractable
    public void Interact(Entity interactor)
    {
        bool firstTime = !_examined;
        _examined = true;

        if (firstTime && _flagToSet != "") GameStateManager.Instance?.SetFlag(_flagToSet, true);

        string dialogueId = firstTime || _repeatDialogueId == "" ? _firstDialogueId : _repeatDialogueId;
        DialogueManager dialogue = (GetParent() as Room)?.Dialogue;
        if (dialogue != null && dialogueId != "") dialogue.StartDialogue(dialogueId);
    }
    #endregion

    #region Drawing
    public override void _Draw()
    {
        Rect2 box = new Rect2(Vector2.Zero, _size);
        DrawRect(box, _fill);
        DrawRect(box, _outline, false, 2f);
    }
    #endregion
}
