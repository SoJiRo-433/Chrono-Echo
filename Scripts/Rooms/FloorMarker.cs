using Godot;

public partial class FloorMarker : Node2D
{
    #region Private state
    private Vector2 _size = new Vector2(160f, 70f);
    private string _caption = "";
    private Color _fill = new Color(0.8f, 0.85f, 0.9f, 0.08f);
    private Color _outline = new Color(0.75f, 0.8f, 0.85f, 0.45f);
    private string _visibleFlag = "";
    #endregion

    #region Public properties
    [Export] public Vector2 Size { get => _size; set { _size = value; QueueRedraw(); } }
    [Export] public string Caption { get => _caption; set { _caption = value ?? ""; QueueRedraw(); } }
    [Export] public Color Fill { get => _fill; set { _fill = value; QueueRedraw(); } }
    [Export] public Color Outline { get => _outline; set { _outline = value; QueueRedraw(); } }

    [Export] public string VisibleFlag { get => _visibleFlag; set => _visibleFlag = value ?? ""; }

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

    private void RefreshVisibility()
    {
        bool unlocked = _visibleFlag == "" || (GameStateManager.Instance?.GetFlag(_visibleFlag) ?? false);
        if (Visible != unlocked) Visible = unlocked;
    }
    #endregion

    #region Drawing
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, _size), _fill);
        DrawRect(new Rect2(Vector2.Zero, _size), _outline, false, 1f);
    }
    #endregion
}
