using Godot;

/// <summary>
/// A labelled patch of floor. Purely visual: it never blocks movement and cannot be
/// interacted with. Used to lay the room out like the blueprint - the wake-up spot, the
/// spatial anomaly, the Echo recording zone, and the piles of debris.
///
/// OOP role: the simplest Room part after Obstacle - data plus one drawing method. The Room
/// does not even need to know about it, because it neither collides nor reacts to input.
/// </summary>
public partial class FloorMarker : Node2D
{
    #region Private state
    private Vector2 _size = new Vector2(160f, 70f);
    private string _caption = "";
    private Color _fill = new Color(0.8f, 0.85f, 0.9f, 0.08f);
    private Color _outline = new Color(0.75f, 0.8f, 0.85f, 0.45f);
    private string _visibleFlag = "";
<<<<<<< Updated upstream
=======
    private bool _dottedRing;
    private bool _dashed;
    private bool _drawBox = true;
>>>>>>> Stashed changes
    #endregion

    #region Public properties
    [Export] public Vector2 Size { get => _size; set { _size = value; QueueRedraw(); } }
    [Export] public string Caption { get => _caption; set { _caption = value ?? ""; QueueRedraw(); } }
    [Export] public Color Fill { get => _fill; set { _fill = value; QueueRedraw(); } }
    [Export] public Color Outline { get => _outline; set { _outline = value; QueueRedraw(); } }

    /// <summary>
    /// When set, the marker only becomes visible once this GameStateManager flag is true.
    /// That is how the burnt photo "appears on the final page" of the report log.
    /// </summary>
    [Export] public string VisibleFlag { get => _visibleFlag; set => _visibleFlag = value ?? ""; }

<<<<<<< Updated upstream
    /// <summary>Rectangle in room coordinates (used only for documentation/debugging).</summary>
=======
    /// <summary>Draws a dotted ring instead of a box. Both blueprints mark an "Echo stand spot"
    /// that way, so a plate the Echo is meant to hold can say so on the floor.</summary>
    [Export] public bool DottedRing { get => _dottedRing; set { _dottedRing = value; QueueRedraw(); } }

    /// <summary>Dashes the outline. The Level 3 blueprint draws a missing step - the one a jump
    /// has to clear - as a dashed rectangle.</summary>
    [Export] public bool Dashed { get => _dashed; set { _dashed = value; QueueRedraw(); } }

    /// <summary>False leaves the marker as pure annotation: the caption stays, the box or ring is
    /// not drawn, because the room's painted backdrop already shows what is there.</summary>
    [Export] public bool DrawBox { get => _drawBox; set { _drawBox = value; QueueRedraw(); } }

>>>>>>> Stashed changes
    public Rect2 Bounds => new Rect2(Position, _size);
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
        RefreshVisibility();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_visibleFlag != "") RefreshVisibility();
    }

    /// <summary>Hidden until its flag is set; visible straight away when it has no flag.</summary>
    private void RefreshVisibility()
    {
        bool unlocked = _visibleFlag == "" || (GameStateManager.Instance?.GetFlag(_visibleFlag) ?? false);
        if (Visible != unlocked) Visible = unlocked;
    }
    #endregion

    #region Drawing
    public override void _Draw()
    {
<<<<<<< Updated upstream
        DrawRect(new Rect2(Vector2.Zero, _size), _fill);
        DrawRect(new Rect2(Vector2.Zero, _size), _outline, false, 1f);
=======
        if (!_drawBox) return;
        Rect2 box = new Rect2(Vector2.Zero, _size);

        if (_dottedRing)
        {
            Vector2 centre = _size * 0.5f;
            float radius = Mathf.Min(_size.X, _size.Y) * 0.5f;
            const int dots = 24;
            for (int i = 0; i < dots; i++)
            {
                float angle = Mathf.Tau * i / dots;
                DrawCircle(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, 2.5f, _outline);
            }
            return;
        }

        DrawRect(box, _fill);
        if (_dashed) DrawDashedOutline(box, _outline, 2f, 8f);
        else DrawRect(box, _outline, false, 2f);
    }

    private void DrawDashedOutline(Rect2 box, Color colour, float width, float dash)
    {
        Vector2 topLeft = box.Position;
        Vector2 topRight = new Vector2(box.End.X, box.Position.Y);
        Vector2 bottomRight = box.End;
        Vector2 bottomLeft = new Vector2(box.Position.X, box.End.Y);

        DrawDashedLine(topLeft, topRight, colour, width, dash);
        DrawDashedLine(topRight, bottomRight, colour, width, dash);
        DrawDashedLine(bottomRight, bottomLeft, colour, width, dash);
        DrawDashedLine(bottomLeft, topLeft, colour, width, dash);
    }

    private void DrawDashedLine(Vector2 from, Vector2 to, Color colour, float width, float dash)
    {
        float length = from.DistanceTo(to);
        if (length <= 0f || dash <= 0f) return;

        Vector2 step = (to - from) / length;
        for (float travelled = 0f; travelled < length; travelled += dash * 2f)
        {
            float end = Mathf.Min(travelled + dash, length);
            DrawLine(from + step * travelled, from + step * end, colour, width);
        }
>>>>>>> Stashed changes
    }
    #endregion
}
