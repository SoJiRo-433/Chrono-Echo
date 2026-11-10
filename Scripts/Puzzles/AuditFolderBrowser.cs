using Godot;

/// <summary>Code-built folder browser for the hidden audit ledger.</summary>
public partial class AuditFolderBrowser : Node2D, IInteractable
{
    [Export] public string FlagToSet { get; set; } = "F6_ledger";
    [Export] public Vector2 Size { get; set; } = new Vector2(44f, 34f);
    [Export] public string Caption { get; set; } = "LEDGERS (E)";
    private CanvasLayer _layer; private Label _label; private int _stage;
    public Rect2 InteractionBounds => new Rect2(Position - Size * 0.5f, Size);

    public float DistanceTo(Vector2 from) => InteractionBounds.DistanceToPoint(from);
    public void Interact(Entity interactor) { if (_layer == null) BuildUi(); _stage = 0; Refresh(); _layer.Visible = true; }

    /// <summary>The ledger cabinet draws itself, so the archive says which shelves to rifle through.</summary>
    public override void _Ready()
    {
        Label label = new Label
        {
            Text = Caption,
            Position = new Vector2(-Size.X * 0.5f, -Size.Y * 0.5f - 16f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", new Color(0.9f, 0.86f, 0.7f));
        AddChild(label);
        QueueRedraw();
    }

    public override void _Draw()
    {
        Rect2 box = new Rect2(-Size * 0.5f, Size);
        DrawRect(box, new Color(0.24f, 0.22f, 0.2f, 0.95f));
        DrawRect(box, new Color(0.86f, 0.79f, 0.62f, 0.9f), false, 2f);
        for (int i = 0; i < 3; i++)
        {
            Rect2 drawer = new Rect2(box.Position + new Vector2(4f, 4f + i * 9f), new Vector2(box.Size.X - 8f, 6f));
            DrawRect(drawer, new Color(0.34f, 0.31f, 0.27f, 0.95f));
            DrawRect(drawer, new Color(0.82f, 0.75f, 0.6f, 0.6f), false, 1f);
        }
    }
    private void BuildUi()
    {
        GameStateManager.Instance?.SetPaused(true);
        _layer = new CanvasLayer { Layer = 20, Visible = false };
        var panel = new PanelContainer { Position = new Vector2(300, 150), Size = new Vector2(560, 340) };
        var stack = new VBoxContainer(); _label = new Label { HorizontalAlignment = HorizontalAlignment.Center }; stack.AddChild(_label);
        foreach (string text in new[] { "Institute Accounts", "Project Hyperion", "Private Accounts", "████: CONTAINMENT - LG" })
        {
            string item = text; var button = new Button { Text = item, CustomMinimumSize = new Vector2(420, 48) };
            button.Pressed += () => { _stage++; Refresh(); if (item.Contains("LG") && _stage >= 3) { GameStateManager.Instance?.SetFlag(FlagToSet, true); _layer.Visible = false; GameStateManager.Instance?.SetPaused(false); (GetParent() as Room)?.Dialogue?.StartDialogue("level6_ledger_found"); } };
            stack.AddChild(button);
        }
        panel.AddChild(stack); _layer.AddChild(panel); AddChild(_layer);
    }
    private void Refresh() { if (_label != null) _label.Text = _stage >= 2 ? "AUDIT ARCHIVE / LEDGERS" : "AUDIT ARCHIVE / FOLDERS"; }
}
