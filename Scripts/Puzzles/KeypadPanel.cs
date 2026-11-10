using Godot;

/// <summary>Code-built four-digit keypad for the quarantine door.</summary>
public partial class KeypadPanel : Node2D, IInteractable
{
    [Export] public string Code { get; set; } = "4173";
    [Export] public string FlagToSet { get; set; } = "medical_keypad_open";
    [Export] public Vector2 Size { get; set; } = new Vector2(28f, 28f);
    [Export] public string Caption { get; set; } = "KEYPAD (E)";
    private CanvasLayer _layer;
    private Label _display;
    private string _entry = "";
    public Rect2 InteractionBounds => new Rect2(Position - Size * 0.5f, Size);

    public float DistanceTo(Vector2 from) => InteractionBounds.DistanceToPoint(from);
    public void Interact(Entity interactor) { if (_layer == null) BuildUi(); _layer.Visible = true; }

    /// <summary>The keypad is a thing in the room, not thin air: the niche it sits in has to read as
    /// somewhere to press E, or the quarantine corridor looks like a wall with no way past it.</summary>
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
        DrawRect(box, new Color(0.11f, 0.15f, 0.19f, 0.95f));
        DrawRect(box, new Color(0.65f, 0.88f, 1f, 0.9f), false, 2f);

        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
                DrawRect(new Rect2(box.Position + new Vector2(4f + col * 7f, 4f + row * 7f), new Vector2(4f, 4f)),
                         new Color(0.78f, 0.92f, 1f, 0.9f));
        DrawRect(new Rect2(box.Position + new Vector2(4f, 24f), new Vector2(18f, 3f)), new Color(0.4f, 0.85f, 1f, 0.9f));
    }
    private void BuildUi()
    {
        GameStateManager.Instance?.SetPaused(true);
        _layer = new CanvasLayer { Layer = 20, Visible = false };
        var panel = new PanelContainer { Position = new Vector2(350, 120), Size = new Vector2(450, 400) };
        var stack = new VBoxContainer();
        _display = new Label { Text = "ENTER CODE: ____", HorizontalAlignment = HorizontalAlignment.Center };
        _display.AddThemeFontSizeOverride("font_size", 24);
        stack.AddChild(_display);
        for (int row = 0; row < 3; row++)
        {
            var line = new HBoxContainer();
            for (int col = 0; col < 3; col++)
            {
                int digit = row * 3 + col + 1;
                Button button = new() { Text = digit.ToString(), CustomMinimumSize = new Vector2(90, 58) };
                button.Pressed += () => AddDigit(digit.ToString()); line.AddChild(button);
            }
            stack.AddChild(line);
        }
        var actions = new HBoxContainer();
        Button zero = new() { Text = "0", CustomMinimumSize = new Vector2(90, 58) }; zero.Pressed += () => AddDigit("0"); actions.AddChild(zero);
        Button clear = new() { Text = "Clear", CustomMinimumSize = new Vector2(90, 58) }; clear.Pressed += () => { _entry = ""; Refresh(); }; actions.AddChild(clear);
        Button enter = new() { Text = "Enter", CustomMinimumSize = new Vector2(90, 58) }; enter.Pressed += Submit; actions.AddChild(enter);
        stack.AddChild(actions);
        Button close = new() { Text = "Close" }; close.Pressed += () => { _layer.Visible = false; GameStateManager.Instance?.SetPaused(false); }; stack.AddChild(close);
        panel.AddChild(stack); _layer.AddChild(panel); AddChild(_layer);
    }
    private void AddDigit(string digit) { if (_entry.Length < 4) _entry += digit; Refresh(); }
    private void Refresh() { if (_display != null) _display.Text = "ENTER CODE: " + _entry.PadRight(4, '_'); }
    private void Submit()
    {
        if (_entry == Code) { GameStateManager.Instance?.SetFlag(FlagToSet, true); _layer.Visible = false; GameStateManager.Instance?.SetPaused(false); }
        else { _entry = ""; Refresh(); }
    }
}
