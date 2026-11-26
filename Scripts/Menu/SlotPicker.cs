using Godot;
using System;

public enum SlotPickerMode { Load, Save }

public partial class SlotPicker : Control
{
    public event Action Chosen;

    private static readonly Vector2 ThumbSize = new(128f, 70f);
    private static readonly Vector2 HeaderThumbSize = new(80f, 44f);
    private static readonly Color CurrentCardTint = new(0.75f, 0.95f, 1f);
    private static readonly Color LastCardTint = new(1f, 0.94f, 0.78f);

    private SlotPickerMode _mode = SlotPickerMode.Load;
    private readonly Button[] _slotButtons = new Button[SaveManager.SlotCount];
    private readonly MarginContainer[] _slotBodies = new MarginContainer[SaveManager.SlotCount];
    private HBoxContainer _currentBanner;
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

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.8f), MouseFilter = MouseFilterEnum.Stop };
        AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer();
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var panel = new PanelContainer();
        center.AddChild(panel);

        var margin = new MarginContainer();
        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 22);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        margin.AddChild(box);

        var title = new Label
        {
            Text = _mode == SlotPickerMode.Load ? "Load Game" : "Save Game",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 26);
        box.AddChild(title);

        _currentBanner = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _currentBanner.AddThemeConstantOverride("separation", 12);
        box.AddChild(_currentBanner);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(968f, 412f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        box.AddChild(scroll);

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 10);
        scroll.AddChild(grid);

        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            int slot = i;
            var button = new Button
            {
                CustomMinimumSize = new Vector2(468f, 84f),
                ClipContents = true
            };
            button.Pressed += () => OnSlotPressed(slot);
            grid.AddChild(button);
            _slotButtons[i] = button;

            var body = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                body.AddThemeConstantOverride(side, 7);
            button.AddChild(body);
            _slotBodies[i] = body;
        }

        var backRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddChild(backRow);

        _back = new Button { Text = "Back", CustomMinimumSize = new Vector2(240f, 44f) };
        _back.AddThemeFontSizeOverride("font_size", 20);
        _back.Pressed += QueueFree;
        backRow.AddChild(_back);

        Refresh();
        FocusFirst();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            QueueFree();
        }
    }

    private void Refresh()
    {
        RefreshCurrentBanner();

        bool inGame = GameStateManager.Instance?.CurrentRoom != null;
        int activeSlot = inGame ? SaveManager.CurrentSlot : SaveManager.LastSlot;

        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            SaveSlotInfo info = SaveManager.GetSlotInfo(i);
            Button button = _slotButtons[i];
            button.Disabled = _mode == SlotPickerMode.Load && !info.Exists;

            bool isCurrent = inGame && SaveManager.CurrentSlot == i;
            bool isLast = !isCurrent && activeSlot == i && info.Exists;

            button.SelfModulate = isCurrent
                ? CurrentCardTint
                : isLast ? LastCardTint : Colors.White;

            RebuildSlotBody(_slotBodies[i], i, info, isCurrent, isLast);
        }
    }

    private void RefreshCurrentBanner()
    {
        foreach (Node child in _currentBanner.GetChildren())
            child.QueueFree();

        Room room = GameStateManager.Instance?.CurrentRoom;
        if (room != null)
        {
            string scene = GetTree().CurrentScene?.SceneFilePath ?? "";
            SaveLocationInfo location = SaveManager.GetLocationInfo(scene, room.LevelNumber);
            Player player = GameStateManager.Instance?.Player ?? room.Player;
            bool hasPos = player != null;
            Vector2 pos = player?.WorldPosition ?? Vector2.Zero;

            Control thumb = BuildLocationThumbnail(
                location.PicturePath,
                scene,
                room.RoomSize,
                hasPos,
                pos,
                HeaderThumbSize,
                location.LevelNumber);
            _currentBanner.AddChild(thumb);

            string slotTag = SaveManager.CurrentSlot >= 0 ? $"Slot {SaveManager.CurrentSlot + 1}  |  " : "";
            var label = new Label
            {
                Text = $"Current Location: {slotTag}{location.DisplayTitle}",
                VerticalAlignment = VerticalAlignment.Center
            };
            label.AddThemeFontSizeOverride("font_size", 18);
            label.AddThemeColorOverride("font_color", new Color(0.65f, 0.92f, 1f));
            _currentBanner.AddChild(label);
            return;
        }

        if (SaveManager.HasGame)
        {
            SaveSlotInfo recent = SaveManager.GetSlotInfo(SaveManager.LastSlot);
            var label = new Label
            {
                Text = $"Currently Saved: Slot {recent.Slot + 1}  |  {recent.DisplayLevel}  |  {recent.SavedAt}",
                VerticalAlignment = VerticalAlignment.Center
            };
            label.AddThemeFontSizeOverride("font_size", 17);
            label.AddThemeColorOverride("font_color", new Color(0.85f, 0.9f, 0.96f));
            _currentBanner.AddChild(label);
        }
    }

    private static void RebuildSlotBody(MarginContainer body, int slot, SaveSlotInfo info, bool isCurrent, bool isLast)
    {
        foreach (Node child in body.GetChildren())
            child.QueueFree();

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 12);
        body.AddChild(row);

        Control thumb = info.Exists
            ? BuildLocationThumbnail(info.PicturePath, info.ScenePath, info.RoomSize, info.HasPlayerPosition, info.PlayerPosition, ThumbSize, info.LevelNumber)
            : BuildEmptyThumbnail(ThumbSize);
        row.AddChild(thumb);

        var textCol = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        textCol.AddThemeConstantOverride("separation", 2);
        row.AddChild(textCol);

        var topRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        textCol.AddChild(topRow);

        var slotLabel = new Label
        {
            Text = $"Slot {slot + 1}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        slotLabel.AddThemeFontSizeOverride("font_size", 18);
        slotLabel.AddThemeColorOverride("font_color", info.Exists ? Colors.White : new Color(0.65f, 0.68f, 0.75f));
        topRow.AddChild(slotLabel);

        if (isCurrent || isLast)
        {
            var badge = new Label
            {
                Text = isCurrent ? "CURRENT" : "LAST",
                HorizontalAlignment = HorizontalAlignment.Right,
                MouseFilter = MouseFilterEnum.Ignore
            };
            badge.AddThemeFontSizeOverride("font_size", 13);
            badge.AddThemeColorOverride("font_color", isCurrent ? new Color(0.55f, 0.92f, 1f) : new Color(1f, 0.86f, 0.45f));
            topRow.AddChild(badge);
        }

        var levelLabel = new Label
        {
            Text = info.Exists ? info.DisplayLevel : "Empty Slot",
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = MouseFilterEnum.Ignore
        };
        levelLabel.AddThemeFontSizeOverride("font_size", 16);
        levelLabel.AddThemeColorOverride("font_color", info.Exists ? new Color(0.92f, 0.94f, 0.98f) : new Color(0.52f, 0.55f, 0.62f));
        textCol.AddChild(levelLabel);

        var timeLabel = new Label
        {
            Text = info.Exists && !string.IsNullOrEmpty(info.SavedAt) ? info.SavedAt : (info.Exists ? "Saved" : "—"),
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = MouseFilterEnum.Ignore
        };
        timeLabel.AddThemeFontSizeOverride("font_size", 14);
        timeLabel.AddThemeColorOverride("font_color", info.Exists ? new Color(0.72f, 0.78f, 0.86f) : new Color(0.42f, 0.45f, 0.5f));
        textCol.AddChild(timeLabel);
    }

    private static Control BuildEmptyThumbnail(Vector2 size)
    {
        var frame = new Control
        {
            CustomMinimumSize = size,
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        var bg = new ColorRect
        {
            Color = new Color(0.08f, 0.09f, 0.13f, 0.95f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        frame.AddChild(bg);

        var label = new Label
        {
            Text = "Empty",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(0.45f, 0.48f, 0.55f));
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        frame.AddChild(label);
        return frame;
    }

    private static Control BuildLocationThumbnail(
        string picturePath, string scenePath, Vector2 roomSize,
        bool hasPlayerPos, Vector2 playerPos, Vector2 size, int levelNumber)
    {
        var frame = new Control
        {
            CustomMinimumSize = size,
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore
        };

        var bg = new ColorRect
        {
            Color = new Color(0.08f, 0.1f, 0.14f, 1f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        frame.AddChild(bg);

        if (!string.IsNullOrEmpty(picturePath) && ResourceLoader.Exists(picturePath))
        {
            var tex = new TextureRect
            {
                Texture = GD.Load<Texture2D>(picturePath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore
            };
            tex.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            frame.AddChild(tex);
        }
        else
        {
            var preview = new BlueprintPreview(scenePath, levelNumber)
            {
                MouseFilter = MouseFilterEnum.Ignore
            };
            preview.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            frame.AddChild(preview);
        }

        if (hasPlayerPos && roomSize.X > 1f && roomSize.Y > 1f)
        {
            float nx = Mathf.Clamp(playerPos.X / roomSize.X, 0.04f, 0.96f);
            float ny = Mathf.Clamp(playerPos.Y / roomSize.Y, 0.06f, 0.94f);
            Vector2 dotPos = new(nx * size.X, ny * size.Y);

            var halo = new ColorRect
            {
                Color = new Color(0.05f, 0.1f, 0.18f, 0.9f),
                Position = dotPos - new Vector2(4f, 4f),
                Size = new Vector2(8f, 8f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            var dot = new ColorRect
            {
                Color = new Color(0.45f, 0.95f, 1f, 1f),
                Position = dotPos - new Vector2(2.5f, 2.5f),
                Size = new Vector2(5f, 5f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            frame.AddChild(halo);
            frame.AddChild(dot);
        }

        return frame;
    }

    private sealed partial class BlueprintPreview : Control
    {
        private readonly string _scenePath;
        private readonly int _levelNumber;

        public BlueprintPreview(string scenePath, int levelNumber)
        {
            _scenePath = scenePath ?? "";
            _levelNumber = levelNumber;
        }

        public override void _Draw()
        {
            Vector2 s = Size;
            DrawRect(new Rect2(Vector2.Zero, s), new Color(0.09f, 0.11f, 0.16f));

            if (_scenePath.Contains("Room06Business", StringComparison.OrdinalIgnoreCase))
            {
                // Scaled miniature of RoomBusiness layout (667x747)
                DrawScaledRoom(new Rect2(42, 443, 302, 211), new Vector2(667f, 747f), new Color(0.45f, 0.5f, 0.58f));
                DrawScaledRoom(new Rect2(103, 222, 252, 142), new Vector2(667f, 747f), new Color(0.4f, 0.46f, 0.55f));
                DrawScaledRoom(new Rect2(443, 222, 190, 142), new Vector2(667f, 747f), new Color(0.4f, 0.46f, 0.55f));
                DrawScaledRoom(new Rect2(190, 82, 362, 92), new Vector2(667f, 747f), new Color(0.35f, 0.48f, 0.62f));
            }
            else if (_scenePath.Contains("Room02", StringComparison.OrdinalIgnoreCase))
            {
                DrawScaledRoom(new Rect2(40, 80, 1060, 480), new Vector2(1152f, 648f), new Color(0.35f, 0.44f, 0.58f));
            }
            else
            {
                DrawRect(new Rect2(s * 0.12f, s * 0.76f), new Color(0.28f, 0.34f, 0.45f), false, 1.5f);
            }

            if (_levelNumber > 0)
            {
                Font font = ThemeDB.FallbackFont;
                if (font != null)
                    DrawString(font, new Vector2(6f, 16f), $"L{_levelNumber}", HorizontalAlignment.Left, -1, 12, new Color(0.8f, 0.88f, 1f, 0.8f));
            }
        }

        private void DrawScaledRoom(Rect2 rect, Vector2 worldSize, Color color)
        {
            Vector2 scale = new(Size.X / worldSize.X, Size.Y / worldSize.Y);
            Rect2 scaled = new(rect.Position * scale, rect.Size * scale);
            DrawRect(scaled, color);
            DrawRect(scaled, new Color(0.75f, 0.82f, 0.92f, 0.65f), false, 1f);
        }
    }

    private void FocusFirst()
    {
        foreach (Button button in _slotButtons)
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
