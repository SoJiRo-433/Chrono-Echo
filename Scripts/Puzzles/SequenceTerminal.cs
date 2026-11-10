using Godot;
using System.Collections.Generic;

/// <summary>Five increasingly long input rounds for the medical diagnostic terminal.</summary>
public partial class SequenceTerminal : Node2D, IInteractable
{
    [Export] public string FlagToSet { get; set; } = "F4_scan";
    [Export] public string ContinueDialogueId { get; set; } = "level4_diagnostic_terminal";
    [Export] public Vector2 Size { get; set; } = new Vector2(44f, 30f);
    [Export] public string Caption { get; set; } = "DIAGNOSTIC TERMINAL (E)";
    private CanvasLayer _layer;
    private Label _prompt;
    private readonly List<int> _input = new();
    private int _round = 1;
    public Rect2 InteractionBounds => new Rect2(Position - Size * 0.5f, Size);

    public float DistanceTo(Vector2 from) => InteractionBounds.DistanceToPoint(from);
    public void Interact(Entity interactor) { if (_layer == null) BuildUi(); _layer.Visible = true; _round = 1; _input.Clear(); Refresh(); }

    /// <summary>The terminal draws itself now, so the room does not need a separate marker beside it.</summary>
    public override void _Ready()
    {
        Label label = new Label
        {
            Text = Caption,
            Position = new Vector2(-Size.X * 0.5f, -Size.Y * 0.5f - 16f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", new Color(0.65f, 0.9f, 1f));
        AddChild(label);
        QueueRedraw();
    }

    public override void _Draw()
    {
        Rect2 box = new Rect2(-Size * 0.5f, Size);
        DrawRect(box, new Color(0.1f, 0.14f, 0.18f, 0.95f));
        DrawRect(box, new Color(0.55f, 0.9f, 1f, 0.9f), false, 2f);
        DrawRect(new Rect2(box.Position + new Vector2(4f, 4f), new Vector2(box.Size.X - 8f, 10f)), new Color(0.22f, 0.6f, 0.75f, 0.95f));
        DrawRect(new Rect2(box.Position + new Vector2(4f, 18f), new Vector2(box.Size.X - 8f, 4f)), new Color(0.4f, 0.85f, 1f, 0.6f));
    }
    private void BuildUi()
    {
        GameStateManager.Instance?.SetPaused(true);
        _layer = new CanvasLayer { Layer = 20, Visible = false };
        var panel = new PanelContainer { Position = new Vector2(320, 170), Size = new Vector2(520, 300) };
        var stack = new VBoxContainer(); _prompt = new Label { HorizontalAlignment = HorizontalAlignment.Center }; stack.AddChild(_prompt);
        var row = new HBoxContainer();
        for (int i = 1; i <= 5; i++) { int value = i; var b = new Button { Text = i.ToString(), CustomMinimumSize = new Vector2(72, 62) }; b.Pressed += () => Press(value); row.AddChild(b); }
        stack.AddChild(row); panel.AddChild(stack); _layer.AddChild(panel); AddChild(_layer);
    }
    private void Refresh() => _prompt.Text = $"DIAGNOSTIC SEQUENCE - ROUND {_round}/5   REPEAT: {string.Join(" ", System.Linq.Enumerable.Range(1, _round))}   INPUT: {string.Join(" ", _input)}";
    private void Press(int value)
    {
        int expected = _input.Count + 1;
        if (value != expected) { _input.Clear(); Refresh(); return; }
        _input.Add(value);
        if (_input.Count < _round) { Refresh(); return; }
        if (_round == 5) { GameStateManager.Instance?.SetFlag(FlagToSet, true); _layer.Visible = false; GameStateManager.Instance?.SetPaused(false); (GetParent() as Room)?.Dialogue?.StartDialogue(ContinueDialogueId); return; }
        _round++; _input.Clear(); Refresh();
    }
}
