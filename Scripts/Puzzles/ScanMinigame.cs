using Godot;

/// <summary>Cursor-following scan ring: hold over both targets for three steady seconds.</summary>
public partial class ScanMinigame : Node2D, IInteractable
{
    [Export] public string ContinueDialogueId { get; set; } = "level4_triage_scan_result";
    [Export] public Vector2 Size { get; set; } = new Vector2(30f, 30f);
    [Export] public string Caption { get; set; } = "SCAN RING (E)";
    private CanvasLayer _layer;
    private Control _root;
    private Control _ring;
    private Control[] _targets;
    private int _targetIndex;
    private float _steadySeconds;
    private Label _hint;
    public Rect2 InteractionBounds => new Rect2(Position - Size * 0.5f, Size);

    public float DistanceTo(Vector2 from) => InteractionBounds.DistanceToPoint(from);
    public void Interact(Entity interactor) { if (_layer == null) BuildUi(); _targetIndex = 0; _steadySeconds = 0; _layer.Visible = true; }

    /// <summary>Adellaine's handheld scanner, drawn so the triage room says where her scan starts.</summary>
    public override void _Ready()
    {
        Label label = new Label
        {
            Text = Caption,
            Position = new Vector2(-Size.X * 0.5f, -Size.Y * 0.5f - 16f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", new Color(0.75f, 1f, 0.85f));
        AddChild(label);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float radius = Size.X * 0.5f - 2f;
        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 24, new Color(0.55f, 0.98f, 1f, 0.95f), 2f);
        DrawLine(new Vector2(-radius * 0.55f, 0f), new Vector2(radius * 0.55f, 0f), new Color(0.55f, 0.98f, 1f, 0.5f), 1f);
        DrawLine(new Vector2(0f, -radius * 0.55f), new Vector2(0f, radius * 0.55f), new Color(0.55f, 0.98f, 1f, 0.5f), 1f);
    }
    public override void _Process(double delta)
    {
        if (_layer == null || !_layer.Visible) return;
        Vector2 cursor = _root.GetGlobalMousePosition();
        _ring.Position = cursor - new Vector2(22, 22);
        if (_targets[_targetIndex].GetGlobalRect().HasPoint(cursor)) _steadySeconds += (float)delta; else _steadySeconds = 0f;
        _hint.Text = $"Hold the scan ring steady over {(_targetIndex == 0 ? "Aionia" : "Yoruka")}: {Mathf.Max(0f, 3f - _steadySeconds):0.0}s";
        if (_steadySeconds < 3f) return;
        _steadySeconds = 0f;
        _targetIndex++;
        if (_targetIndex < 2) return;
        _layer.Visible = false;
        GameStateManager.Instance?.SetPaused(false);
        (GetParent() as Room)?.Dialogue?.StartDialogue(ContinueDialogueId);
    }
    private void BuildUi()
    {
        GameStateManager.Instance?.SetPaused(true);
        _layer = new CanvasLayer { Layer = 20, Visible = false };
        _root = new Control(); _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); _layer.AddChild(_root);
        var shade = new ColorRect { Color = new Color(0.02f, 0.08f, 0.14f, 0.9f) }; shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); _root.AddChild(shade);
        _hint = new Label { Position = new Vector2(300, 80), Size = new Vector2(560, 44), HorizontalAlignment = HorizontalAlignment.Center };
        _hint.AddThemeFontSizeOverride("font_size", 20); _root.AddChild(_hint);
        _targets = new Control[2];
        _targets[0] = new Panel { Position = new Vector2(360, 220), Size = new Vector2(100, 100) };
        _targets[1] = new Panel { Position = new Vector2(650, 220), Size = new Vector2(100, 100) };
        for (int i = 0; i < 2; i++) { var label = new Label { Text = i == 0 ? "AIONIA" : "YORUKA", Position = new Vector2(10, 38) }; _targets[i].AddChild(label); _root.AddChild(_targets[i]); }
        _ring = new Panel { Size = new Vector2(44, 44), MouseFilter = Control.MouseFilterEnum.Ignore };
        _ring.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.3f, 0.9f, 1f, 0.16f), BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3, BorderColor = new Color(0.5f, 1f, 1f) });
        _root.AddChild(_ring); AddChild(_layer);
    }
}
