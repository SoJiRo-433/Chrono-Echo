using Godot;
using System;

public enum SlotPickerMode { Load, Save }

public partial class SlotPicker : Control
{
    public event Action Chosen;

    private SlotPickerMode _mode = SlotPickerMode.Load;
    private readonly Button[] _slotButtons = new Button[SaveManager.SlotCount];
    private Button _back;

    public static SlotPicker Open(Node parent, SlotPickerMode mode)
    {
        var picker = new SlotPicker { _mode = mode };
        parent.AddChild(picker);
        return picker;
    }

    public override void _Ready()
    {
        Theme = UiHelper.GetTheme();
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Dark backdrop that also blocks clicks on whatever is behind
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.75f), MouseFilter = MouseFilterEnum.Stop };
        AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer();
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var panel = new PanelContainer();
        center.AddChild(panel);

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 32);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        margin.AddChild(box);

        box.AddChild(new Label
        {
            Text = _mode == SlotPickerMode.Load ? "Load Game" : "Save Game",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            int slot = i; // capture a copy for the lambda
            var button = new Button { CustomMinimumSize = new Vector2(560, 72) };
            button.Pressed += () => OnSlotPressed(slot);
            box.AddChild(button);
            _slotButtons[i] = button;
        }

        _back = new Button { Text = "Back", CustomMinimumSize = new Vector2(560, 56) };
        _back.Pressed += QueueFree;
        box.AddChild(_back);

        Refresh();
        FocusFirst();
    }

    public override void _Input(InputEvent @event)
    {
        // Esc closes the picker (and stops the pause menu from also reacting)
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            QueueFree();
        }
    }

    private void Refresh()
    {
        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            _slotButtons[i].Text = SaveManager.Describe(i);
            // Can't load an empty slot
            _slotButtons[i].Disabled = _mode == SlotPickerMode.Load && !SaveManager.SlotExists(i);
        }
    }

    private void FocusFirst()
    {
        foreach (var button in _slotButtons)
        {
            if (!button.Disabled)
            {
                button.GrabFocus();
                return;
            }
        }
        _back.GrabFocus();
    }

    private void OnSlotPressed(int slot)
    {
        if (_mode == SlotPickerMode.Load)
        {
            SaveManager.LoadSlot(GetTree(), slot);
            Chosen?.Invoke();
            QueueFree();
            return;
        }

        // Save mode: warn before overwriting an existing file
        if (SaveManager.SlotExists(slot))
        {
            UiHelper.Confirm(
                this,
                "Overwrite Save",
                $"Slot {slot + 1} already has a save.\nSaving here will permanently overwrite it. Continue?",
                () => DoSave(slot),
                "Overwrite");
        }
        else
        {
            DoSave(slot);
        }
    }

    private void DoSave(int slot)
    {
        bool ok = SaveManager.Save(GetTree(), slot);
        Refresh();
        UiHelper.Notify(this,
            ok ? "Saved" : "Error",
            ok ? $"Game saved to Slot {slot + 1}." : "Could not save the game.");
    }
}
