using Godot;
using System;
using GDict = Godot.Collections.Dictionary;

public static class SaveManager
{
    public const int SlotCount = 3;

    // The first scene of the actual game (your friend's scene). Change if needed.
    public const string StartScene = "res://Scenes/Rooms/Room01.tscn";

    private const string MetaPath = "user://save_meta.json";

    public static Func<GDict> CaptureState;

    public static event Action NewGameStarted;

    public static event Action<GDict> GameLoaded;

    public static int CurrentSlot { get; private set; } = -1;

    // ---- "Current game" = the most recently used slot ----
    private static int _lastSlot = -2;

    public static int LastSlot
    {
        get
        {
            if (_lastSlot == -2) LoadMeta();
            return _lastSlot;
        }
        private set
        {
            _lastSlot = value;
            SaveMeta();
        }
    }

    public static bool HasGame => LastSlot >= 0 && SlotExists(LastSlot);

    // ---- Slot helpers ----
    private static string SlotPath(int slot) => $"user://save_slot_{slot + 1}.json";

    public static bool SlotExists(int slot) => ReadSlot(slot) != null;

    public static GDict ReadSlot(int slot)
    {
        string path = SlotPath(slot);
        if (!FileAccess.FileExists(path)) return null;

        var parsed = Json.ParseString(FileAccess.GetFileAsString(path));
        if (parsed.VariantType != Variant.Type.Dictionary) return null;

        var data = parsed.AsGodotDictionary();
        return data.ContainsKey("scene") ? data : null;
    }

    public static string Describe(int slot)
    {
        string label = $"Slot {slot + 1}";
        var data = ReadSlot(slot);
        if (data == null) return $"{label} - Empty";

        string time = data.ContainsKey("saved_at") ? data["saved_at"].AsString() : "";
        if (time.Length > 16) time = time.Substring(0, 16);

        string scene = System.IO.Path.GetFileNameWithoutExtension(data["scene"].AsString());
        return $"{label} - {time}  ({scene})";
    }

    private static int FirstEmptySlot()
    {
        for (int i = 0; i < SlotCount; i++)
            if (!SlotExists(i)) return i;
        return 0;
    }

    // ---- Actions ----

    public static async void NewGame(SceneTree tree)
    {
        int slot = HasGame ? LastSlot : FirstEmptySlot();

        var data = new GDict
        {
            { "scene", StartScene },
            { "saved_at", Time.GetDatetimeStringFromSystem(false, true) },
            { "state", new GDict() }
        };
        WriteFile(SlotPath(slot), data);

        CurrentSlot = slot;
        LastSlot = slot;

        tree.Paused = false;
        tree.ChangeSceneToFile(StartScene);

        await WaitForSceneToLoad(tree);
        NewGameStarted?.Invoke();
    }

    public static bool Save(SceneTree tree, int slot)
    {
        var state = CaptureState?.Invoke() ?? new GDict();
        string scene = tree.CurrentScene?.SceneFilePath;
        if (string.IsNullOrEmpty(scene)) scene = StartScene;

        var data = new GDict
        {
            { "scene", scene },
            { "saved_at", Time.GetDatetimeStringFromSystem(false, true) },
            { "state", state }
        };

        if (!WriteFile(SlotPath(slot), data)) return false;

        CurrentSlot = slot;
        LastSlot = slot;
        return true;
    }

    public static async void LoadSlot(SceneTree tree, int slot)
    {
        var data = ReadSlot(slot);
        if (data == null)
        {
            GD.PushWarning($"Slot {slot + 1} is empty or unreadable.");
            return;
        }

        CurrentSlot = slot;
        LastSlot = slot;

        string scene = data["scene"].AsString();
        if (!ResourceLoader.Exists(scene)) scene = StartScene;

        var state = data.ContainsKey("state") ? data["state"].AsGodotDictionary() : new GDict();

        tree.Paused = false;
        tree.ChangeSceneToFile(scene);

        await WaitForSceneToLoad(tree);
        GameLoaded?.Invoke(state);
    }

    // ---- Internals ----
    private static async System.Threading.Tasks.Task WaitForSceneToLoad(SceneTree tree)
    {
        // ChangeSceneToFile happens at the end of the frame; wait for the new scene to be ready.
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static bool WriteFile(string path, GDict data)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError($"Could not write {path}: {FileAccess.GetOpenError()}");
            return false;
        }
        file.StoreString(Json.Stringify(data, "\t"));
        return true;
    }

    private static void LoadMeta()
    {
        _lastSlot = -1;
        if (!FileAccess.FileExists(MetaPath)) return;

        var parsed = Json.ParseString(FileAccess.GetFileAsString(MetaPath));
        if (parsed.VariantType != Variant.Type.Dictionary) return;

        var meta = parsed.AsGodotDictionary();
        if (meta.ContainsKey("last_slot")) _lastSlot = meta["last_slot"].AsInt32();
    }

    private static void SaveMeta()
    {
        WriteFile(MetaPath, new GDict { { "last_slot", _lastSlot } });
    }
}
