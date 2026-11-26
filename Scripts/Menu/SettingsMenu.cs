using Godot;
using System;

/// <summary>Settings overlay opened from MainMenu, PauseMenu, or the dialogue bar.
/// Controls Music/SFX volume, Text Speed, Auto Text, Skip Mode (Read/All), and Display Mode.</summary>
public partial class SettingsMenu : Control
{
    private const int RowFontSize = 20;
    private const int TitleFontSize = 28;
    private const float LabelWidth = 220f;
    private const float ControlWidth = 340f;

    private static readonly Color ActiveModulate = new(0.65f, 0.9f, 1f);
    private static readonly Color InactiveModulate = new(0.75f, 0.75f, 0.78f);

    private HSlider _musicSlider;
    private Label _musicValueLabel;
    private HSlider _sfxSlider;
    private Label _sfxValueLabel;
    private HSlider _textSpeedSlider;
    private Label _textSpeedValueLabel;
    private Button _autoOffBtn;
    private Button _autoOnBtn;
    private HSlider _autoDelaySlider;
    private Label _autoDelayValueLabel;
    private Button _skipReadBtn;
    private Button _skipAllBtn;
    private Button _windowedBtn;
    private Button _fullscreenBtn;
    private Button _backBtn;

    private bool _updatingUi;

    public static SettingsMenu Open(Node parent)
    {
        var menu = new SettingsMenu();
        parent.AddChild(menu);
        return menu;
    }

    public override void _Ready()
    {
        Theme = UiHelper.GetTheme();
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        BuildUi();
        RefreshFromSettings();

        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SettingsChanged += RefreshFromSettings;
        if (DisplayManager.Instance != null)
            DisplayManager.Instance.FullscreenChanged += OnFullscreenChanged;

        _musicSlider.GrabFocus();
    }

    public override void _ExitTree()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SettingsChanged -= RefreshFromSettings;
        if (DisplayManager.Instance != null)
            DisplayManager.Instance.FullscreenChanged -= OnFullscreenChanged;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            QueueFree();
        }
    }

    private void BuildUi()
    {
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.78f), MouseFilter = MouseFilterEnum.Stop };
        AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer();
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var panel = new PanelContainer();
        center.AddChild(panel);

        var margin = new MarginContainer();
        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 28);
        panel.AddChild(margin);

        var rootBox = new VBoxContainer();
        rootBox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(rootBox);

        var title = new Label { Text = "Settings", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", TitleFontSize);
        rootBox.AddChild(title);

        (_musicSlider, _musicValueLabel) = AddSliderRow(
            rootBox, "Music Volume", 0, 100, 1,
            v => SettingsManager.Instance?.SetMusicVolume((float)(v / 100.0)));

        (_sfxSlider, _sfxValueLabel) = AddSliderRow(
            rootBox, "SFX Volume", 0, 100, 1,
            v => SettingsManager.Instance?.SetSfxVolume((float)(v / 100.0)));

        (_textSpeedSlider, _textSpeedValueLabel) = AddSliderRow(
            rootBox, "Text Speed", 0, 100, 5,
            v => SettingsManager.Instance?.SetTextSpeed((float)(v / 100.0)));

        (_autoOffBtn, _autoOnBtn) = AddChoiceRow(
            rootBox, "Auto Text",
            "Off", () => SettingsManager.Instance?.SetAutoText(false),
            "On", () => SettingsManager.Instance?.SetAutoText(true));

        (_autoDelaySlider, _autoDelayValueLabel) = AddSliderRow(
            rootBox, "Auto Delay", 0.5, 5.0, 0.25,
            v => SettingsManager.Instance?.SetAutoTextDelay((float)v));

        (_skipReadBtn, _skipAllBtn) = AddChoiceRow(
            rootBox, "Skip Option",
            "Read Messages", () => SettingsManager.Instance?.SetSkipMode(SkipMode.ReadOnly),
            "All Messages", () => SettingsManager.Instance?.SetSkipMode(SkipMode.All));

        (_windowedBtn, _fullscreenBtn) = AddChoiceRow(
            rootBox, "Display",
            "Windowed", () => SetFullscreenMode(false),
            "Fullscreen", () => SetFullscreenMode(true));

        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        footer.AddThemeConstantOverride("separation", 16);
        rootBox.AddChild(footer);

        var resetBtn = new Button { Text = "Reset Defaults", CustomMinimumSize = new Vector2(200, 48) };
        resetBtn.AddThemeFontSizeOverride("font_size", RowFontSize);
        resetBtn.Pressed += () => SettingsManager.Instance?.ResetDefaults();
        footer.AddChild(resetBtn);

        _backBtn = new Button { Text = "Back", CustomMinimumSize = new Vector2(200, 48) };
        _backBtn.AddThemeFontSizeOverride("font_size", RowFontSize);
        _backBtn.Pressed += QueueFree;
        footer.AddChild(_backBtn);
    }

    private static (HSlider Slider, Label ValueLabel) AddSliderRow(
        Container parent, string labelText, double min, double max, double step, Action<double> onChanged)
    {
        var row = CreateRow(parent, labelText);

        var box = new HBoxContainer { CustomMinimumSize = new Vector2(ControlWidth, 36) };
        box.AddThemeConstantOverride("separation", 12);
        row.AddChild(box);

        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        slider.ValueChanged += v => onChanged?.Invoke(v);
        box.AddChild(slider);

        var valueLabel = new Label
        {
            CustomMinimumSize = new Vector2(86, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        valueLabel.AddThemeFontSizeOverride("font_size", RowFontSize);
        box.AddChild(valueLabel);

        return (slider, valueLabel);
    }

    private static (Button First, Button Second) AddChoiceRow(
        Container parent, string labelText,
        string firstText, Action onFirst,
        string secondText, Action onSecond)
    {
        var row = CreateRow(parent, labelText);

        var box = new HBoxContainer { CustomMinimumSize = new Vector2(ControlWidth, 40) };
        box.AddThemeConstantOverride("separation", 10);
        row.AddChild(box);

        var first = new Button
        {
            Text = firstText,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 40)
        };
        first.AddThemeFontSizeOverride("font_size", RowFontSize);
        first.Pressed += () => onFirst?.Invoke();
        box.AddChild(first);

        var second = new Button
        {
            Text = secondText,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 40)
        };
        second.AddThemeFontSizeOverride("font_size", RowFontSize);
        second.Pressed += () => onSecond?.Invoke();
        box.AddChild(second);

        return (first, second);
    }

    private static HBoxContainer CreateRow(Container parent, string labelText)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        parent.AddChild(row);

        var label = new Label
        {
            Text = labelText,
            CustomMinimumSize = new Vector2(LabelWidth, 36),
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", RowFontSize);
        row.AddChild(label);
        return row;
    }

    private void RefreshFromSettings()
    {
        if (_updatingUi) return;
        _updatingUi = true;
        try
        {
            SettingsManager settings = SettingsManager.Instance;
            float music = settings?.MusicVolume ?? SettingsManager.DefaultMusicVolume;
            float sfx = settings?.SfxVolume ?? SettingsManager.DefaultSfxVolume;
            float textSpeed = settings?.TextSpeed ?? SettingsManager.DefaultTextSpeed;
            bool autoText = settings?.AutoText ?? SettingsManager.DefaultAutoText;
            float autoDelay = settings?.AutoTextDelay ?? SettingsManager.DefaultAutoTextDelay;
            SkipMode skipMode = settings?.SkipMode ?? SettingsManager.DefaultSkipMode;
            bool fullscreen = DisplayManager.Instance?.IsFullscreen
                              ?? settings?.IsFullscreen
                              ?? SettingsManager.DefaultFullscreen;

            _musicSlider.SetValueNoSignal(Mathf.Round(music * 100f));
            _musicValueLabel.Text = $"{Mathf.RoundToInt(music * 100f)}%";

            _sfxSlider.SetValueNoSignal(Mathf.Round(sfx * 100f));
            _sfxValueLabel.Text = $"{Mathf.RoundToInt(sfx * 100f)}%";

            _textSpeedSlider.SetValueNoSignal(Mathf.Round(textSpeed * 100f));
            _textSpeedValueLabel.Text = DescribeTextSpeed(textSpeed);

            _autoDelaySlider.SetValueNoSignal(autoDelay);
            _autoDelaySlider.Editable = autoText;
            _autoDelayValueLabel.Text = $"{autoDelay:0.00}s";
            _autoDelayValueLabel.Modulate = autoText ? Colors.White : new Color(1f, 1f, 1f, 0.45f);

            ApplyChoicePairState(_autoOffBtn, _autoOnBtn, !autoText);
            ApplyChoicePairState(_skipReadBtn, _skipAllBtn, skipMode == SkipMode.ReadOnly);
            ApplyChoicePairState(_windowedBtn, _fullscreenBtn, !fullscreen);
        }
        finally
        {
            _updatingUi = false;
        }
    }

    private static void ApplyChoicePairState(Button first, Button second, bool firstSelected)
    {
        first.Modulate = firstSelected ? ActiveModulate : InactiveModulate;
        second.Modulate = !firstSelected ? ActiveModulate : InactiveModulate;
        first.Flat = !firstSelected;
        second.Flat = firstSelected;
    }

    private static string DescribeTextSpeed(float speed)
    {
        if (speed >= 0.99f) return "Instant";
        if (speed >= 0.67f) return "Fast";
        if (speed >= 0.34f) return "Normal";
        return "Slow";
    }

    private static void SetFullscreenMode(bool fullscreen)
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SetFullscreen(fullscreen);
        else
            DisplayManager.Instance?.SetFullscreen(fullscreen);
    }

    private void OnFullscreenChanged(bool _)
    {
        RefreshFromSettings();
    }
}
