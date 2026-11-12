using Godot;

public partial class PauseMenu : CanvasLayer
{
    // Where "Quit" sends the player. Adjust if main_menu.tscn lives in another folder.
    private const string MainMenuScene = "res://main_menu.tscn";

    private Control _root;
    private Button _btnResume;

    public override void _Ready()
    {
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always; // keep working while the game is paused
        BuildUi();
        Visible = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel")) return;

        var scene = GetTree().CurrentScene;
        if (scene == null || scene.IsInGroup("main_menu")) return;

        GetViewport().SetInputAsHandled();
        if (Visible) Resume();
        else Open();
    }

    private void Open()
    {
        Visible = true;
        GetTree().Paused = true;
        _btnResume.GrabFocus();
    }

    private void Resume()
    {
        Visible = false;
        GetTree().Paused = false;
    }

    private void BuildUi()
    {
        _root = new Control { Theme = UiHelper.GetTheme() };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = Control.MouseFilterEnum.Stop };
        _root.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var center = new CenterContainer();
        _root.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var panel = new PanelContainer();
        center.AddChild(panel);

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 32);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        margin.AddChild(box);

        box.AddChild(new Label { Text = "Paused", HorizontalAlignment = HorizontalAlignment.Center });

        _btnResume = AddButton(box, "Resume", Resume);
        AddButton(box, "Save Game", OnSavePressed);
        AddButton(box, "Load Game", OnLoadPressed);
        AddButton(box, "Quit", OnQuitPressed);
    }

    private static Button AddButton(Container parent, string text, System.Action onPressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(360, 60) };
        button.Pressed += onPressed;
        parent.AddChild(button);
        return button;
    }

    private void OnSavePressed()
    {
        var picker = SlotPicker.Open(_root, SlotPickerMode.Save);
        picker.TreeExited += RefocusIfVisible;
    }

    private void OnLoadPressed()
    {
        var picker = SlotPicker.Open(_root, SlotPickerMode.Load);
        picker.Chosen += () =>
        {
            Visible = false; // the save's scene is loading; SaveManager unpauses the tree
        };
        picker.TreeExited += RefocusIfVisible;
    }

    private void OnQuitPressed()
    {
        UiHelper.Confirm(
            _root,
            "Quit",
            "Quit to the main menu?\nAny unsaved progress will be lost.",
            QuitToMenu,
            "Quit");
    }

    private void QuitToMenu()
    {
        Visible = false;
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(MainMenuScene);
        // To quit the whole game instead, use: GetTree().Quit();
    }

    private void RefocusIfVisible()
    {
        if (Visible && IsInsideTree()) _btnResume.GrabFocus();
    }
}
