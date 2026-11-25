using Godot;

/// <summary>Six-symbol archive keypad used by the Time Research Facility.</summary>
public partial class FacilityKeypad : Node2D, IInteractable
{
    [Export] public string Code { get; set; } = "2ECSAD";
    [Export] public string FlagToSet { get; set; } = "level8_keypad_open";
    [Export] public Vector2 Size { get; set; } = new(36f, 28f);
    [Export] public string Caption { get; set; } = "ARCHIVE KEYPAD (E)";

    private CanvasLayer _layer;
    private Label _display;
    private string _entry = "";

    public Rect2 InteractionBounds => new(Position - Size * 0.5f, Size);
    public float DistanceTo(Vector2 from) => InteractionBounds.DistanceToPoint(from);

    public override void _Ready()
    {
        var label = new Label
        {
            Text = Caption,
            Position = new Vector2(-Size.X * 0.5f, -Size.Y * 0.5f - 18f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", new Color(0.65f, 0.9f, 1f));
        AddChild(label);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var box = new Rect2(-Size * 0.5f, Size);
        DrawRect(box, new Color(0.08f, 0.12f, 0.16f, 0.96f));
        DrawRect(box, new Color(0.45f, 0.8f, 1f), false, 2f);
        DrawRect(new Rect2(box.Position + new Vector2(4f, 5f), new Vector2(box.Size.X - 8f, 8f)),
            new Color(0.3f, 0.7f, 0.9f, 0.85f));
    }

    public void Interact(Entity interactor)
    {
        if (GameStateManager.Instance?.GetFlag(FlagToSet) == true) return;
        if (_layer == null) BuildUi();
        _entry = "";
        Refresh();
        _layer.Visible = true;
        GameStateManager.Instance?.SetPaused(true);
    }

    private void BuildUi()
    {
        _layer = new CanvasLayer { Layer = 20, Visible = false };
        var panel = new PanelContainer { Position = new Vector2(310, 180), Size = new Vector2(530, 250) };
        var stack = new VBoxContainer();
        stack.AddChild(new Label
        {
            Text = "RECORD ARCHIVE ACCESS CODE",
            HorizontalAlignment = HorizontalAlignment.Center
        });
        _display = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _display.AddThemeFontSizeOverride("font_size", 22);
        stack.AddChild(_display);

        var symbols = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        foreach (string symbol in new[] { "2", "E", "C", "S", "A", "D" })
        {
            string selected = symbol;
            var button = new Button { Text = symbol, CustomMinimumSize = new Vector2(56, 52) };
            button.Pressed += () => AddSymbol(selected);
            symbols.AddChild(button);
        }
        stack.AddChild(symbols);

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        var clear = new Button { Text = "CLEAR", CustomMinimumSize = new Vector2(120, 38) };
        clear.Pressed += () => { _entry = ""; Refresh(); };
        var enter = new Button { Text = "ENTER", CustomMinimumSize = new Vector2(120, 38) };
        enter.Pressed += Submit;
        var close = new Button { Text = "CLOSE", CustomMinimumSize = new Vector2(120, 38) };
        close.Pressed += Close;
        actions.AddChild(clear); actions.AddChild(enter); actions.AddChild(close);
        stack.AddChild(actions);

        panel.AddChild(stack);
        _layer.AddChild(panel);
        AddChild(_layer);
    }

    private void AddSymbol(string symbol)
    {
        if (_entry.Length >= Code.Length) return;
        _entry += symbol;
        Refresh();
    }

    private void Refresh()
    {
        if (_display != null)
            _display.Text = _entry + (_entry.Length < Code.Length ? "  " + new string('.', Code.Length - _entry.Length) : "");
    }

    private void Submit()
    {
        if (_entry == Code)
        {
            GameStateManager.Instance?.SetFlag(FlagToSet, true);
            Close();
        }
        else
        {
            _entry = "";
            Refresh();
        }
    }

    private void Close()
    {
        if (_layer != null) _layer.Visible = false;
        GameStateManager.Instance?.SetPaused(false);
    }
}
