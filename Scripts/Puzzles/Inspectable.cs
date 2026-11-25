using Godot;

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
    private string _requiredFlag = "";
    private string _characterSheetPath = "";
    private bool _solid;
    private bool _drawBox = true;
    private bool _examined;
    #endregion

    #region Public properties
    [Export] public Vector2 Size { get => _size; set { _size = value; QueueRedraw(); } }
    [Export] public string Caption { get => _caption; set { _caption = value ?? ""; QueueRedraw(); } }
    [Export] public Color Fill { get => _fill; set { _fill = value; QueueRedraw(); } }
    [Export] public Color Outline { get => _outline; set { _outline = value; QueueRedraw(); } }

    [Export] public string FirstDialogueId { get => _firstDialogueId; set => _firstDialogueId = value ?? ""; }

    [Export] public string RepeatDialogueId { get => _repeatDialogueId; set => _repeatDialogueId = value ?? ""; }

    [Export] public string FlagToSet { get => _flagToSet; set => _flagToSet = value ?? ""; }
    [Export] public string RequiredFlag { get => _requiredFlag; set => _requiredFlag = value ?? ""; }

    /// <summary>When set, the object uses a static, front-facing character sprite.
    /// Empty = the plain captioned box, which is what every piece of scenery uses.</summary>
    [Export] public string CharacterSheetPath { get => _characterSheetPath; set => _characterSheetPath = value ?? ""; }

    [Export] public bool Solid { get => _solid; set => _solid = value; }

    // For a room whose art comes from its own painted backdrop: the fill and outline are not drawn,
    // so the painting shows the object, but the caption and the interaction stay. Visible is left
    // alone on purpose - Room.FindNearestInteractable skips invisible nodes.
    [Export] public bool DrawBox { get => _drawBox; set { _drawBox = value; QueueRedraw(); } }

    public bool HasBeenExamined => _examined;
    public Rect2 Bounds => new Rect2(Position, _size);

    public float DistanceTo(Vector2 from) => Bounds.DistanceToPoint(from);
    #endregion

    #region Lifecycle
    public override void _Ready()
    {
        CharacterSprite character = null;
        if (_characterSheetPath != "")
        {
            character = new CharacterSprite { SheetPath = _characterSheetPath, FrameCount = 1, Look = CharacterSprite.Facing.South };
            AddChild(character);
            // The character stands on the bottom edge of the box, centred on it, so the feet land
            // on the floor the room laid out rather than in the middle of the interaction area.
            character.Position = new Vector2(_size.X * 0.5f, _size.Y);
        }

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

            // A character is a picture of the thing the caption names, so the caption moves under
            // their feet instead of sitting on top of their head.
            if (character != null)
            {
                float width = Mathf.Max(_size.X, 90f);
                label.Size = new Vector2(width, 16f);
                label.Position = new Vector2((_size.X - width) * 0.5f, _size.Y + 4f);
                label.HorizontalAlignment = HorizontalAlignment.Center;
            }

            AddChild(label);
        }
        QueueRedraw();
    }
    #endregion

    #region IInteractable
    public void Interact(Entity interactor)
    {
        if (_requiredFlag != "" && GameStateManager.Instance?.GetFlag(_requiredFlag) != true) return;
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
        if (!_drawBox) return;
        Rect2 box = new Rect2(Vector2.Zero, _size);
        DrawRect(box, _fill);
        DrawRect(box, _outline, false, 2f);
    }
    #endregion
}
