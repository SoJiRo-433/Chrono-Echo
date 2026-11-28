using Godot;
using System;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;

public sealed class SaveLocationInfo
{
	public int LevelNumber { get; }
	public string LevelName { get; }
	public string PicturePath { get; }
	public Vector2 RoomSize { get; }

	public SaveLocationInfo(int levelNumber, string levelName, string picturePath, Vector2 roomSize)
	{
		LevelNumber = levelNumber;
		LevelName = levelName;
		PicturePath = picturePath;
		RoomSize = roomSize;
	}

	public string DisplayTitle => LevelNumber > 0 ? $"Level {LevelNumber} - {LevelName}" : LevelName;
}

public sealed class SaveSlotInfo
{
	public int Slot { get; init; }
	public bool Exists { get; init; }
	public string ScenePath { get; init; } = "";
	public int LevelNumber { get; init; }
	public string LevelName { get; init; } = "";
	public string SavedAt { get; init; } = "";
	public string PicturePath { get; init; } = "";
	public Vector2 RoomSize { get; init; } = new(1280f, 720f);
	public bool HasPlayerPosition { get; init; }
	public Vector2 PlayerPosition { get; init; }
	public int CompletedPuzzleCount { get; init; }

	public string DisplayLevel => LevelNumber > 0 ? $"Level {LevelNumber} - {LevelName}" : LevelName;
}

public static class SaveManager
{
	public const int SlotCount = 10;

	// The first scene of the actual game. Change if needed.
	public const string StartScene = "res://Scenes/Rooms/Room01.tscn";

	private const string MetaPath = "user://save_meta.json";

	private static readonly Dictionary<string, SaveLocationInfo> LocationsByScene = new()
	{
		["res://Scenes/Rooms/Room01.tscn"] = new SaveLocationInfo(1, "Tutorial Room", "res://Assets/level1_tutorial_room.png", new Vector2(1152f, 648f)),
		["res://Scenes/Rooms/RoomLibrary.tscn"] = new SaveLocationInfo(2, "The Archives", "res://Assets/level2_archives_map.png", new Vector2(1280f, 720f)),
		["res://Scenes/Rooms/Room02.tscn"] = new SaveLocationInfo(2, "The Bridgeway", "", new Vector2(1152f, 648f)),
		["res://Scenes/Rooms/RoomAtrium.tscn"] = new SaveLocationInfo(3, "The Atrium", "res://Assets/level3_atrium_map.png", new Vector2(1280f, 720f)),
		["res://Scenes/Rooms/Room04Medical.tscn"] = new SaveLocationInfo(4, "Medical Wing", "res://Assets/level4_medical_map.png", new Vector2(1280f, 720f)),
		["res://Scenes/Rooms/Room05Observation.tscn"] = new SaveLocationInfo(5, "Observation Wing", "res://Assets/level5_observation_map.png", new Vector2(1280f, 720f)),
		["res://Scenes/Rooms/Room06Business.tscn"] = new SaveLocationInfo(6, "Business Department", "", new Vector2(667f, 747f)),
		["res://Scenes/Rooms/Room07Atrium.tscn"] = new SaveLocationInfo(7, "Atrium Hub", "res://Assets/level3_atrium_map.png", new Vector2(1280f, 720f)),
		["res://Scenes/Rooms/Room08Facility.tscn"] = new SaveLocationInfo(8, "Time Research Facility", "res://Assets/level8_time_research_map.png", new Vector2(1280f, 720f)),
		["res://Scenes/Rooms/Room09SectorAlpha.tscn"] = new SaveLocationInfo(9, "Sector Alpha", "res://Assets/level9_sector_alpha_map.png", new Vector2(1280f, 720f)),
		["res://Scenes/Rooms/Room10Rooftop.tscn"] = new SaveLocationInfo(10, "Rooftop", "res://Assets/level10_rooftop_map.png", new Vector2(1280f, 720f)),
		["res://Scenes/EndingScreen.tscn"] = new SaveLocationInfo(10, "Ending", "res://Assets/level10_rooftop_map.png", new Vector2(1280f, 720f))
	};

	public static Func<GDict> CaptureState;

	public static event Action NewGameStarted;

	public static event Action<GDict> GameLoaded;

	public static int CurrentSlot { get; private set; } = -1;
	public static bool IsLoadingSave { get; private set; }

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

	public static bool HasGame => LastSlot >= 0 && LastSlot < SlotCount && SlotExists(LastSlot);

	// ---- Slot helpers ----
	private static string SlotPath(int slot) => $"user://save_slot_{slot + 1}.json";

	public static bool SlotExists(int slot) => ReadSlot(slot) != null;

	public static GDict ReadSlot(int slot)
	{
		if (slot < 0 || slot >= SlotCount) return null;
		string path = SlotPath(slot);
		if (!FileAccess.FileExists(path)) return null;

		var parsed = Json.ParseString(FileAccess.GetFileAsString(path));
		if (parsed.VariantType != Variant.Type.Dictionary) return null;

		var data = parsed.AsGodotDictionary();
		return data.ContainsKey("scene") ? data : null;
	}

	public static SaveLocationInfo GetLocationInfo(string scenePath, int fallbackLevel = 0)
	{
		if (!string.IsNullOrEmpty(scenePath) && LocationsByScene.TryGetValue(scenePath, out SaveLocationInfo info))
			return info;

		string fallbackName = string.IsNullOrEmpty(scenePath)
			? "Unknown Location"
			: System.IO.Path.GetFileNameWithoutExtension(scenePath);
		return new SaveLocationInfo(fallbackLevel, fallbackName, "", new Vector2(1280f, 720f));
	}

	public static SaveSlotInfo GetSlotInfo(int slot)
	{
		var data = ReadSlot(slot);
		if (data == null)
			return new SaveSlotInfo { Slot = slot, Exists = false };

		string scene = data["scene"].AsString();
		int savedLevel = data.ContainsKey("level_number") ? data["level_number"].AsInt32() : 0;
		SaveLocationInfo location = GetLocationInfo(scene, savedLevel);

		int levelNumber = savedLevel > 0 ? savedLevel : location.LevelNumber;
		string levelName = data.ContainsKey("level_name") && !string.IsNullOrEmpty(data["level_name"].AsString())
			? data["level_name"].AsString()
			: location.LevelName;

		string savedAt = data.ContainsKey("saved_at") ? FormatTimestamp(data["saved_at"].AsString()) : "";

		bool hasPos = false;
		Vector2 playerPos = Vector2.Zero;
		int completedPuzzles = 0;
		if (data.ContainsKey("state") && data["state"].VariantType == Variant.Type.Dictionary)
		{
			GDict state = data["state"].AsGodotDictionary();
			if (state.ContainsKey("player_position") && state["player_position"].VariantType == Variant.Type.Dictionary)
			{
				GDict pos = state["player_position"].AsGodotDictionary();
				if (pos.ContainsKey("x") && pos.ContainsKey("y"))
				{
					hasPos = true;
					playerPos = new Vector2((float)pos["x"].AsDouble(), (float)pos["y"].AsDouble());
				}
			}
			if (state.ContainsKey("completed_puzzles") && state["completed_puzzles"].VariantType == Variant.Type.Array)
			{
				completedPuzzles = state["completed_puzzles"].AsGodotArray().Count;
			}
			else if (state.ContainsKey("flags") && state["flags"].VariantType == Variant.Type.Dictionary)
			{
				completedPuzzles = GameStateManager.CountCompletedPuzzles(state["flags"].AsGodotDictionary());
			}
		}

		return new SaveSlotInfo
		{
			Slot = slot,
			Exists = true,
			ScenePath = scene,
			LevelNumber = levelNumber,
			LevelName = levelName,
			SavedAt = savedAt,
			PicturePath = location.PicturePath,
			RoomSize = location.RoomSize,
			HasPlayerPosition = hasPos,
			PlayerPosition = playerPos,
			CompletedPuzzleCount = completedPuzzles
		};
	}

	public static string Describe(int slot)
	{
		string label = $"Slot {slot + 1}";
		SaveSlotInfo info = GetSlotInfo(slot);
		if (!info.Exists) return $"{label} - Empty";

		return $"{label} - {info.SavedAt}  ({info.DisplayLevel})";
	}

	private static string FormatTimestamp(string raw)
	{
		if (string.IsNullOrEmpty(raw)) return "";
		return raw.Replace('T', ' ');
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
		SaveLocationInfo location = GetLocationInfo(StartScene, 1);

		var data = new GDict
		{
			{ "scene", StartScene },
			{ "level_number", location.LevelNumber },
			{ "level_name", location.LevelName },
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
		if (slot < 0 || slot >= SlotCount) return false;

		var state = CaptureState?.Invoke() ?? new GDict();
		string scene = tree.CurrentScene?.SceneFilePath;
		if (string.IsNullOrEmpty(scene)) scene = StartScene;

		int liveLevel = GameStateManager.Instance?.CurrentLevelNumber ?? 0;
		SaveLocationInfo location = GetLocationInfo(scene, liveLevel);

		var data = new GDict
		{
			{ "scene", scene },
			{ "level_number", location.LevelNumber },
			{ "level_name", location.LevelName },
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

		IsLoadingSave = true;
		tree.Paused = false;
		tree.ChangeSceneToFile(scene);

		await WaitForSceneToLoad(tree);
		IsLoadingSave = false;
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
