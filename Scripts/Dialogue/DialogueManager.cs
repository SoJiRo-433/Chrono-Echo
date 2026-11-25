using System.Collections.Generic;
using Godot;

public partial class DialogueManager : Node
{
	#region Signals
	[Signal] public delegate void DialogueStartedEventHandler(string dialogueId);
	[Signal] public delegate void LineChangedEventHandler(string speaker, string text);
	[Signal] public delegate void ChoicesPresentedEventHandler(Godot.Collections.Array<string> choices);
	[Signal] public delegate void DialogueEndedEventHandler(string dialogueId);
	[Signal] public delegate void MemoryFragmentUnlockedEventHandler(string fragmentId, string title, string text);
	#endregion

	#region Private state
	private readonly Dictionary<string, DialogueData> _library = new();
	private readonly List<MemoryFragment> _fragments = new();
	private readonly List<MemoryFragment> _unlockedFragments = new();
	private readonly HashSet<string> _unlockedIds = new();
	private readonly List<DialogueChoice> _visibleChoices = new();

	private DialogueData _currentData;
	private DialogueNode _currentNode;
	private int _lineIndex;
	private bool _isActive;
	private bool _awaitingChoice;
	private float _choiceTimeout;
	private float _choiceTimer;

	// Dialogue UI - the frame, its speaker tab and the portrait plate are drawn from Assets/UI.
	private CanvasLayer _layer;
	private TextureRect _memoryImage;
	private TextureRect _portrait;
	private TextureRect _panel;
	private Label _speakerLabel;
	private Label _dialogueLabel;
	private Label _toastLabel;
	private SceneTreeTimer _toastTimer;

	private string _currentObjective = "";
	private string _platePath = "";
	#endregion

	private const string ObjectiveSpeaker = "Objective";
	private const string PortraitFolder = "res://Assets/Portraits/";

	// The one CG image the repo ships. The pendant's flash is drawn with it, and any node that
	// declares a plate of its own (the Atrium's window) draws it too - the gallery keeps the CGs
	// apart by flag even while they share a placeholder picture.
	private const string MemoryCgPath = "res://Assets/blue_light_memory.jpg";
	private const string PendantMemoryCgPath = "res://Assets/memoryPendant.PNG";

	// The dialogue frame is drawn whole, at the proportions it is drawn at, and pinned near the
	// bottom of the screen. The speaker tab, the text block and the portrait window are all placed
	// from the sheets' own rectangles scaled to these two sizes (see UiSkin), so the drawing keeps
	// its proportions whatever the frame is sized to.
	private static readonly Vector2 DialogueFrameSize = new(957f, 294f);
	private static readonly Vector2 DialogueFrameOrigin = new(20f, 316f);
	private static readonly Vector2 PortraitFigureSize = new(1320f, 1720f);
	private static readonly Vector2 PortraitFigureOrigin = new(160f, 75f);
	private static readonly Vector2 FragmentToastPosition = new(20f, 190f);
	private static readonly Vector2 FragmentToastSize = new(652f, 44f); /*original size: 852f, 44f*/
	private static readonly Dictionary<string, Vector2> PortraitOrigins = new()
	{
		["Aionia Avgerinos"] = PortraitFigureOrigin,
		["Mochizuki Yoruka"] = new(225f, 55f),
		["Adellaine Moriarty"] = PortraitFigureOrigin,
		["Hesiod Moriarty"] = PortraitFigureOrigin,
		["Felix Lasarewitch"] = PortraitFigureOrigin
	};

	#region Public properties
	[Export] public bool ShowDebugOverlay { get; set; } = true;
	public bool IsActive => _isActive;
	public IReadOnlyList<MemoryFragment> UnlockedFragments => _unlockedFragments;

	/// <summary>The last Objective line shown, kept on screen by the HUD until the next one
	/// replaces it. Empty until an Objective has been read in this run.</summary>
	public string CurrentObjective => _currentObjective;

	/// <summary>Sets the objective without a conversation - for a level whose goal changes when
	/// the player reaches a place, where no one is around to say it.</summary>
	public void SetObjective(string objective)
	{
		_currentObjective = objective ?? "";
	}

	public int FragmentCount => _fragments.Count;
	#endregion

	#region Lifecycle
	public override void _Ready()
	{
		BuildDemoContent();
		if (ShowDebugOverlay) BuildOverlay();

		if (GameStateManager.Instance != null)
		{
			GameStateManager.Instance.FlagChanged += OnFlagChanged;
			GameStateManager.Instance.FlagsReset += OnFlagsReset;
		}
	}

	public override void _ExitTree()
	{
		if (GameStateManager.Instance != null)
		{
			GameStateManager.Instance.FlagChanged -= OnFlagChanged;
			GameStateManager.Instance.FlagsReset -= OnFlagsReset;
		}
	}

	public override void _Process(double delta)
	{
		if (!_isActive || !_awaitingChoice || _choiceTimeout <= 0f) return;

		// A timed choice: when the clock runs out she takes the option that was left last.
		_choiceTimer -= (float)delta;
		if (_choiceTimer <= 0f)
		{
			SelectChoice(_visibleChoices.Count - 1);
			return;
		}
		RefreshOverlay();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_isActive || _currentNode == null) return;
		if (!(@event is InputEventKey key) || !key.Pressed || key.IsEcho()) return;

		bool advance = key.Keycode == Key.Enter || key.Keycode == Key.KpEnter || key.Keycode == Key.Space;
		bool choose = key.Keycode >= Key.Key1 && key.Keycode <= Key.Key9;
		if (!advance && !choose) return;

		// Claim the event BEFORE acting. Advancing the last line of a closing conversation
		// hands over to the next level, and the scene change frees this node - so touching
		// the viewport afterwards dereferences a node that no longer exists.
		GetViewport()?.SetInputAsHandled();

		if (advance) Advance();
		else SelectChoice((int)key.Keycode - (int)Key.Key1);
	}
	#endregion

	#region Public API
	public void RegisterDialogue(DialogueData data) { _library[data.Id] = data; }

	public void RegisterFragment(MemoryFragment fragment) { _fragments.Add(fragment); }

	public bool StartDialogue(string dialogueId)
	{
		if (_isActive || !_library.TryGetValue(dialogueId, out DialogueData data)) return false;

		_currentData = data;
		_isActive = true;
		GameStateManager.Instance?.SetPaused(true);   // freeze gameplay
		EmitSignal(SignalName.DialogueStarted, dialogueId);
		EnterNode(data.StartNodeId);
		return true;
	}

	public void Advance()
	{
		if (!_isActive || _awaitingChoice) return;

		if (_lineIndex < _currentNode.Lines.Count - 1)
		{
			_lineIndex++;
			ShowCurrentLine();
			return;
		}

		if (!string.IsNullOrEmpty(_currentNode.NextNodeId)) EnterNode(_currentNode.NextNodeId);
		else EndDialogue();
	}

	public void SelectChoice(int index)
	{
		if (!_isActive || !_awaitingChoice || index < 0 || index >= _visibleChoices.Count) return;

		DialogueChoice choice = _visibleChoices[index];
		_awaitingChoice = false;

		// Apply the outcome to the single source of truth.
		if (!string.IsNullOrEmpty(choice.FlagToSet))
			GameStateManager.Instance?.SetFlag(choice.FlagToSet, choice.FlagValue);

		// Choices move the relationship counters as well as the flags.
		if (!string.IsNullOrEmpty(choice.AffectionCharacter))
			GameStateManager.Instance?.AddAffection(choice.AffectionCharacter, choice.AffectionDelta);
		foreach (KeyValuePair<string, float> change in choice.AdditionalAffectionChanges)
			GameStateManager.Instance?.AddAffection(change.Key, change.Value);

		if (!string.IsNullOrEmpty(choice.NextNodeId)) EnterNode(choice.NextNodeId);
		else EndDialogue();
	}
	#endregion

	#region Internal flow
	private void EnterNode(string nodeId)
	{
		DialogueNode node = _currentData.GetNode(nodeId);
		if (node == null || node.Lines.Count == 0) { EndDialogue(); return; }

		_currentNode = node;
		_lineIndex = 0;

		if (!string.IsNullOrEmpty(node.FlagOnEnter))
			GameStateManager.Instance?.SetFlag(node.FlagOnEnter, node.FlagOnEnterValue);

		ShowCurrentLine();
	}

	private void ShowCurrentLine()
	{
		EmitSignal(SignalName.LineChanged, _currentNode.Speaker, _currentNode.Lines[_lineIndex]);

		// An Objective line is also the HUD's "Current objective": it replaces the previous one
		// and stays on screen long after this dialogue box has closed.
		if (_currentNode.Speaker == ObjectiveSpeaker) _currentObjective = _currentNode.Lines[_lineIndex];

		_awaitingChoice = false;
		_visibleChoices.Clear();
		_choiceTimeout = 0f;
		_choiceTimer = 0f;

		// Choices appear together with the LAST line of the node.
		if (_lineIndex == _currentNode.Lines.Count - 1)
		{
			foreach (DialogueChoice choice in _currentNode.Choices)
				if (choice.IsAvailable()) _visibleChoices.Add(choice);

			if (_visibleChoices.Count > 0)
			{
				_awaitingChoice = true;
				_choiceTimeout = _currentNode.ChoiceTimeoutSeconds;
				_choiceTimer = _choiceTimeout;
				var texts = new Godot.Collections.Array<string>();
				foreach (DialogueChoice choice in _visibleChoices) texts.Add(choice.Text);
				EmitSignal(SignalName.ChoicesPresented, texts);
			}
		}
		RefreshOverlay();
	}

	private void EndDialogue()
	{
		string finishedId = _currentData?.Id ?? "";
		_isActive = false;
		_awaitingChoice = false;
		_currentData = null;
		_currentNode = null;
		GameStateManager.Instance?.SetPaused(false);   // resume gameplay
		RefreshOverlay();
		EmitSignal(SignalName.DialogueEnded, finishedId);
		CheckMemoryFragments();
	}
	#endregion

	#region Memory fragments
	private void OnFlagChanged(string flagName, bool value) { CheckMemoryFragments(); }

	private void OnFlagsReset()
	{
		_unlockedIds.Clear();
		_unlockedFragments.Clear();
	}

	private void CheckMemoryFragments()
	{
		GameStateManager state = GameStateManager.Instance;
		if (state == null) return;

		foreach (MemoryFragment fragment in _fragments)
		{
			if (_unlockedIds.Contains(fragment.Id) || !state.GetFlag(fragment.RequiredFlag)) continue;

			_unlockedIds.Add(fragment.Id);               // mark first: prevents re-entry via FlagChanged
			_unlockedFragments.Add(fragment);
			state.SetFlag("memory_" + fragment.Id, true);
			EmitSignal(SignalName.MemoryFragmentUnlocked, fragment.Id, fragment.Title, fragment.Text);
			ShowToast("MEMORY UNLOCKED - " + fragment.Title + "\n" + fragment.Text);
		}
	}
	#endregion

	#region Placeholder overlay (no external assets)
	private void BuildOverlay()
	{
		_layer = new CanvasLayer { Layer = 10 };

		_memoryImage = new TextureRect
		{
			Texture = GD.Load<Texture2D>(MemoryCgPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		_memoryImage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_layer.AddChild(_memoryImage);

		// Full-body character art sits to the left of the dialogue and extends behind its
		// translucent panel, like a visual-novel speaker illustration.
		_portrait = new TextureRect
		{
			Position = PortraitFigureOrigin,
			Size = PortraitFigureSize,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};

		_panel = UiSkin.DialoguePlate(DialogueFrameSize);
		_panel.Position = DialogueFrameOrigin;
		_panel.Visible = false;

		// The speaker's name goes in the tab the frame draws for it, which the sheet hangs over the
		// box's own top-left corner - not above the box, where it used to float.
		Rect2 tab = UiSkin.DialogueLocal(UiSkin.DialogueTabRegion, DialogueFrameSize);
		_speakerLabel = new Label
		{
			Position = tab.Position + new Vector2(18f, 10f),
			Size = tab.Size - new Vector2(36f, 20f),
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_speakerLabel.AddThemeFontSizeOverride("font_size", 21);
		_speakerLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_speakerLabel.AddThemeConstantOverride("outline_size", 5);
		_panel.AddChild(_speakerLabel);

		Rect2 text = UiSkin.DialogueLocal(UiSkin.DialogueTextRegion, DialogueFrameSize);
		_dialogueLabel = new Label
		{
			Position = text.Position,
			Size = text.Size,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_dialogueLabel.AddThemeFontSizeOverride("font_size", 20);
		_panel.AddChild(_dialogueLabel);

		// The toast sits above the frame, clear of the portrait plate beside it.
		_toastLabel = new Label { Position = FragmentToastPosition, Size = FragmentToastSize, HorizontalAlignment = HorizontalAlignment.Left, AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
		_toastLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.4f));
		_toastLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_toastLabel.AddThemeConstantOverride("outline_size", 4);

		_layer.AddChild(_portrait);
		_layer.AddChild(_panel);
		_layer.AddChild(_toastLabel);
		AddChild(_layer);
	}

	private void RefreshOverlay()
	{
		RefreshPlate();

		if (_panel == null) return;
		_panel.Visible = _isActive;
		if (!_isActive || _currentNode == null)
		{
			HidePortrait();
			return;
		}

		string speaker = _currentNode.Speaker;
		Color colour = SpeakerColor(speaker);

		_speakerLabel.Text = speaker;
		_speakerLabel.AddThemeColorOverride("font_color", colour);
		RefreshPortrait(speaker);

		string text = _currentNode.Lines[_lineIndex];
		if (_awaitingChoice)
		{
			text += "\n";
			for (int i = 0; i < _visibleChoices.Count; i++)
				text += "\n  [" + (i + 1) + "]  " + _visibleChoices[i].Text;

			if (_choiceTimeout > 0f)
				text += "\n\n  " + Mathf.Max(0f, _choiceTimer).ToString("0.0") + "s";
		}
		else
		{
			text += "\n\n  [Enter] continue";
		}
		_dialogueLabel.Text = text;
	}

	/// <summary>The CG plate behind the box. Two things can put a picture there: the pendant's
	/// flash, which is the memory the whole game opens with, and any node that declares one of its
	/// own - the Atrium's window shows its CG while she is remembering and drops it for the beats
	/// that are back in the room.</summary>
	private void RefreshPlate()
	{
		if (_memoryImage == null) return;

		bool flash = _isActive && _currentData?.Id == "pendant_flash"
					 && _currentNode?.Id != "headache";   // the headache is the pain, not the memory

		string declared = _isActive ? _currentNode?.PlateImagePath ?? "" : "";
		string path = declared != "" ? declared : flash ? PendantMemoryCgPath : "";

		if (path != _platePath)
		{
			_platePath = path;
			_memoryImage.Texture = path == "" || !ResourceLoader.Exists(path) ? null : GD.Load<Texture2D>(path);
		}

		_memoryImage.Visible = path != "";
	}

	/// <summary>Shows the speaker's full-body art from res://Assets/Portraits when present.
	/// Missing art is not an error.</summary>
	private void RefreshPortrait(string speaker)
	{
		if (_portrait == null) return;

		string path = PortraitFolder + speaker.Replace(' ', '_') + ".png";
		Texture2D texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

		_portrait.Position = PortraitOrigins.TryGetValue(speaker, out Vector2 origin) ? origin : PortraitFigureOrigin;
		_portrait.Texture = texture;
		_portrait.Visible = texture != null;
	}

	private void HidePortrait()
	{
		if (_portrait != null) _portrait.Visible = false;
	}

	/// <summary>One colour per speaker. The five named characters are eye/hair-coded; the
	/// Narrator, Instructions and Objective voices read as caption styles, not as people.
	/// (The doc's profile pages do not specify colours, so these are chosen to be distinct.)</summary>
	private static Color SpeakerColor(string speaker)
	{
		switch (speaker)
		{
			case "Aionia Avgerinos": return new Color(0.63f, 0.87f, 1f);     // cold blue of the pendant
			case "Mochizuki Yoruka": return new Color(1f, 0.66f, 0.82f);     // jirai-kei pink
			case "Felix Lasarewitch": return new Color(1f, 0.85f, 0.45f);    // the institute's golden boy
			case "Adellaine Moriarty": return new Color(0.66f, 1f, 0.78f);   // medicine green
			case "Hesiod Moriarty": return new Color(0.85f, 0.78f, 1f);      // photography violet
			case ObjectiveSpeaker: return new Color(1f, 0.86f, 0.45f);
			case "Instructions": return new Color(0.87f, 0.93f, 0.62f);
			case "Narrator": return new Color(0.72f, 0.76f, 0.84f);
			default: return new Color(0.9f, 0.9f, 0.9f);
		}
	}

	private void ShowToast(string message)
	{
		GD.Print(message);
		if (_toastLabel == null) return;
		_toastLabel.Text = message;
		_toastLabel.Visible = true;
		_toastTimer = GetTree().CreateTimer(6.0);
		_toastTimer.Timeout += () =>
		{
			if (IsInstanceValid(_toastLabel) && _toastTimer != null && !_toastTimer.TimeLeft.Equals(0f)) return;
			if (IsInstanceValid(_toastLabel)) _toastLabel.Visible = false;
		};
	}
	#endregion

	#region Story content (laid out like the OOP blueprint)
	// The narrative from the design doc, wired into the blueprint's floor plan.
	// Protagonist: Aionia Avgerinos. Setting: the ruined Aetherion Institute.
	// The room itself is built in Scenes/Rooms/Room01.tscn - this is only the words.
	private void BuildDemoContent()
	{
		// --- Scene 1: waking up at the west side of the room ---
		RegisterDialogue(new DialogueData("intro")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("W-what happened? Where am I?")
				.SetNext("wake"))
			.AddNode(new DialogueNode("wake", "Narrator")
				.AddLine("The space around you looks like it has been abandoned. You find scattered papers, broken machines, and broken glasses.")
				.AddLine("You should look around and find out what happened.")
				.SetNext("move"))
			.AddNode(new DialogueNode("move", "Instructions")
				.AddLine("Use W,A,S,D to move.")));

		// --- Scene 2-3: the report log, page by page, ending on the burnt photo ---
		RegisterDialogue(new DialogueData("report_log")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Project Hyperion, to traverse time and space…Interesting")
				.AddChoice(new DialogueChoice("Turn the page", "photo", "saw_burnt_photo")))
			.AddNode(new DialogueNode("photo", "Narrator")
				.AddLine("In the picture, it shows a figure of you and another person facing away, looking towards a machine in the same room that you’re standing in right now.")
				.SetNext("photo_reaction"))
			.AddNode(new DialogueNode("photo_reaction", "Aionia Avgerinos")
				.AddLine("That looks like me, and someone else…But why do I not remember who that is?")
				.AddLine("I should keep this.")
				.AddLine("I should check around, maybe that will help me find clues")));

		RegisterDialogue(new DialogueData("report_log_again")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("I should check around, maybe that will help me find clues")));

		// --- Scene 3: everything else worth examining ---
		RegisterDialogue(new DialogueData("drawers")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Folders, stack of papers, and nothing worth to note..")));

		RegisterDialogue(new DialogueData("time_machine")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Probably a machine to churn out data.")));

		RegisterDialogue(new DialogueData("cabinet")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Just some mechanical parts.")));

		RegisterDialogue(new DialogueData("broken_machinery")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Just some mechanical parts.")));

		// Scene 3 interactables straight from the doc: the boxes and the loose photos.
		RegisterDialogue(new DialogueData("boxes")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Just some mechanical parts.")));

		RegisterDialogue(new DialogueData("photos")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Pictures of the room, the students working on it, and mostly just documentation. You can’t seem to identify who these students are though.")));

		// --- Scene 4-5: the pendant, and the memory it drags up with it ---
		RegisterDialogue(new DialogueData("pendant_flash")
			.AddNode(new DialogueNode("pickup", "Narrator")
				.AddLine("After looking around the room, you notice a shining, blue light, on the floor. You picked it up.")
				.SetNext("pickup_question"))
			.AddNode(new DialogueNode("pickup_question", "Aionia Avgerinos")
				.AddLine("What is this?")
				.SetNext("buttons"))
			.AddNode(new DialogueNode("buttons", "Narrator")
				.AddLine("You tried pushing the buttons of the pendant.")
				.AddLine("It somehow reminded you of a memory from a long time ago. You remember talking to a blonde person, and he seems very important to you.")
				.SetNext("felix_offer"))
			.AddNode(new DialogueNode("felix_offer", "Felix Lasarewitch")
				.AddLine("Hey, I want to give something to you.")
				.SetNext("aionia_question"))
			.AddNode(new DialogueNode("aionia_question", "Aionia Avgerinos")
				.AddLine("And this is?")
				.SetNext("felix_explain"))
			.AddNode(new DialogueNode("felix_explain", "Felix Lasarewitch")
				.AddLine("Since our study—project, rather—is time. I figured you need a proper physical failsafe.")
				.AddLine("Consider it a specialized remote controller for the Hyperion core mainframe. If the spatial trajectory fluctuates while we're testing, use this override frequency to anchor your position and recalibrate the system.")
				.AddLine("Make sure you keep it close, alright? No matter where the data takes us... I need you to be safe.")
				.SetNext("headache"))
			.AddNode(new DialogueNode("headache", "Aionia Avgerinos")
				.AddLine("Argh! What just happened?!")
				.AddLine("Why did I suddenly remember someone?")
				.SetFlagOnEnter("saw_headache")
				.SetNext("echo_reveal"))
			.AddNode(new DialogueNode("echo_reveal", "Narrator")
				.AddLine("While trying to relive the memory, a blue light appeared in front of you.")
				.AddLine("It is a figure of you—a projection of yourself. It touched its head in pain, mimicking your action earlier.")
				.SetNext("echo_first_reaction"))
			.AddNode(new DialogueNode("echo_first_reaction", "Aionia Avgerinos")
				.AddLine("Wha…It copies my movements?")
				.AddLine("I need to test this out.")
				.SetNext("echo_instruction"))
			.AddNode(new DialogueNode("echo_instruction", "Narrator")
				.AddLine("Press R to start recording movement. Press R again to stop and project your Echo. (Show a 5-second recording bar.)")
				.SetNext("echo_reaction"))
			.AddNode(new DialogueNode("echo_reaction", "Aionia Avgerinos")
				.AddLine("Hah…Amazing")
				.AddLine("I can use this to get out of here.")));

		// Scene 7 opener: the line above used to be duplicated into the flash, so the beat where
		// she connects the Echo to the sealed door lives here instead - it fires the first time
		// the player walks up to the security door or its pressure plate (see Door.ApproachDialogueId).
		RegisterDialogue(new DialogueData("room01_plate")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("The security door is sealed, and this plate needs weight to stay down.")
				.AddLine("I can use the pendant to get out of here.")));

				// --- Scene 6-7: the Echo is what gets you through the security door ---
		RegisterDialogue(new DialogueData("room01_clear")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("You’ve made it out, with only the burnt photo on your hand and the pendant someone you knew gave to you.")
				.AddLine("You should check outside.")));

		// Level 2: the library bridgeway, archives and Yoruka encounter.
		RegisterDialogue(new DialogueData("room02_intro")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("I need to cross this hallway, but the doors are closed.")
				.AddLine("There should be a way to open the doors.")
				.AddLine("!!!!")
				.AddLine("Those plates can be a way to open those doors.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Use your Echo to find a way to open the door.")));

		// Level 2 Scene 1 ends here: the hallway is the last stretch before the Archives.
		RegisterDialogue(new DialogueData("room02_clear")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("The plates hold the last door open long enough for you to slip through.")
				.AddLine("Beyond it the corridor widens, and the smell of dust gives way to old paper.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Head for the Archives and look for anything that survived.")));

		// The Archives open on the line that used to sit in Room02's closing beat.
		RegisterDialogue(new DialogueData("library_intro")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Another student? Glad that I am not alone here, I should approach her.")));

		RegisterDialogue(new DialogueData("library_yoruka")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Uhm, excuse me?")
				.SetNext("question"))
			.AddNode(new DialogueNode("question", "Mochizuki Yoruka")
				.AddLine("!!")
				.AddLine("Senior!! Why are you here? Shouldn’t you be fixing this mess right now?")
				.AddChoice(new DialogueChoice("Fixing what? I don’t even know what is happening here.", "option_a", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1))
				.AddChoice(new DialogueChoice("Do I know you? My head is completely blank right now.", "option_b", "asked_identity", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1))
				.AddChoice(new DialogueChoice("You look terrified. Are you alright?", "option_c", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 2)))
			.AddNode(new DialogueNode("option_a", "Mochizuki Yoruka")
				.AddLine("I-I-I WHAT?! The entire temporal matrix in this sector is collapsing, how can you be so calm right now!")
				.SetNext("aionia_remembers"))
			.AddNode(new DialogueNode("aionia_remembers", "Aionia Avgerinos")
				.AddLine("I…don’t remember.")
				.SetNext("outburst"))
			.AddNode(new DialogueNode("option_b", "Mochizuki Yoruka")
				.AddLine("Don't joke around with me! This isn't the time for your games. If you're trying to run away from your responsibility because everything’s fried, it won't work—everyone's depending on you!")
				.SetNext("outburst"))
			.AddNode(new DialogueNode("option_c", "Mochizuki Yoruka")
				.AddLine("I-I am not terrified! I'm just... highly alert! Unlike some people who act completely unfazed while reality itself tears apart at the seams!")
				.SetNext("aionia_panicking"))
			.AddNode(new DialogueNode("aionia_panicking", "Aionia Avgerinos")
				.AddLine("Panicking doesn't seem very efficient.")
				.SetNext("outburst"))
			.AddNode(new DialogueNode("outburst", "Mochizuki Yoruka")
				.AddLine("Unbelievable! I hate you! I literally don't have time to deal with your amnesiac act right now! If you want to get yourself permanently erased by a localized time-loop and left as a forgotten corpse, go right ahead!")
				.AddLine("Just stay out of my sight!")
				.SetNext("runs"))
			.AddNode(new DialogueNode("runs", "Narrator")
				.AddLine("The student runs away, leaving behind an item.")
				.AddLine("Follow her and maybe ask her more questions. Kindly this time.")
				.AddLine("Objective: Run after the twin-pigtails girl and ask her for more information about yourself.")));

		RegisterDialogue(new DialogueData("library_yoruka_followup")
			.AddNode(new DialogueNode("start", "Mochizuki Yoruka")
				.AddLine("!!")
				.AddLine("Listen, I’m sorry. I was just, just confused and all.")
				.AddLine("I didn’t expect you to be here…")
				.AddLine("How did you even manage to get here?")
				.AddChoice(new DialogueChoice("I had this. Shows pendant.", "pendant_option", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 2))
				.AddChoice(new DialogueChoice("It’s a long story.", "story_option", affectionCharacter: GameStateManager.Yoruka, affectionDelta: -1)))
			.AddNode(new DialogueNode("pendant_option", "Mochizuki Yoruka")
				.AddLine("Okay, I don’t know what that is for—but can you help me with this?")
				.SetNext("after_options"))
			.AddNode(new DialogueNode("story_option", "Mochizuki Yoruka")
				.AddLine("Yeah right…")
				.SetNext("after_options"))
			.AddNode(new DialogueNode("after_options", "Mochizuki Yoruka")
				.AddLine("Anyway, let’s just fix this thing.")
				.AddLine("This terminal log helps us where to go next.")
				.AddLine("I am trying to find out what actually happened in this institute.")
				.AddLine("I figure out maybe something or someone is messing with it.")
				.AddLine("Let’s talk later.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Interact with the surviving terminal interface to extract the data log.")));

		// What she picks up where Yoruka was standing: the item the blueprint's scene 2 says she
		// leaves behind. One line, and the flag the room watches for comes from the node itself.
		RegisterDialogue(new DialogueData("library_item_taken")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("A student card, dropped in the dust. 'Mochizuki Yoruka - junior assistant.' So that's her name.")));

		RegisterDialogue(new DialogueData("library_cork_board")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Pictures of the reading hall, students working on research, and structural documentation. I can’t seem to identify who these people are, though.")));
		RegisterDialogue(new DialogueData("library_computer")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("They probably encode some book borrowers here.")));
		RegisterDialogue(new DialogueData("library_bookshelves")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Books sorted by subject. Can’t see why we need to look here.")));
		RegisterDialogue(new DialogueData("library_drawers")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Scattered folders and stacks of dusty papers. Nothing of real importance here.")));
		RegisterDialogue(new DialogueData("library_sealed_door")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("The service door back to the bridgeway. Whatever came down on the other side of it has jammed it solid.")));
		RegisterDialogue(new DialogueData("library_server_rack")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Racks of storage, still warm. Whatever the archives kept, this is where it was kept.")));
		RegisterDialogue(new DialogueData("library_bench")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("A bench of cut cable and empty spools. Somebody has been re-wiring this corridor by hand, and recently.")));
		RegisterDialogue(new DialogueData("library_conduit")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("The building's pipes, bundled and labelled. One of them is still ticking.")));
		RegisterDialogue(new DialogueData("library_workbench")
			.AddNode(new DialogueNode("start", "Mochizuki Yoruka")
				.AddLine("Don't touch anything down here, senior. Half of it is still live.")));
		RegisterDialogue(new DialogueData("library_spares")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Crates of spares, every one of them stamped with the Hyperion project number.")));
		RegisterDialogue(new DialogueData("library_collapsed_shelves")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("She moved fast... The main route is completely blocked by these fallen shelves. I'll need to bypass it.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Clear the collapsed path using the Echo to catch up to Yoruka.")));
		RegisterDialogue(new DialogueData("library_terminal")
			.AddNode(new DialogueNode("start", "Mochizuki Yoruka")
				.AddLine("Yay!! It’s fixed.")
				.AddLine("Let’s see here…")
				.AddLine("The next terminal log should be in the medical wing. But before we can go there, we need to go through the Atrium…")
				.AddLine("…What a headache…")
				.AddChoice(new DialogueChoice("It’s a long story.", "walk")))
			.AddNode(new DialogueNode("walk", "Aionia Avgerinos")
				.AddLine("We’ll talk on the way, is that good?")
				.SetNext("sure"))
			.AddNode(new DialogueNode("sure", "Mochizuki Yoruka")
				.AddLine("…Sure!")));

		// --- Scene 5: the walk to the Atrium (plays on leaving the Library) ---
		RegisterDialogue(new DialogueData("library_to_atrium")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("The heavy library doors slide open as You and Yoruka begin walking down the quiet, root-webbed corridor toward the Atrium.")
				.AddLine("You told her about what happened to you, how you woke up in a strange place—not remembering events that came before.")
				.AddLine("You showed her your pendant, to which she also doesn’t have a clue about.")
				.SetNext("legend"))
			.AddNode(new DialogueNode("legend", "Mochizuki Yoruka")
				.AddLine("I still can't believe you completely blanked on me. When you were heading Project Hyperion, you were practically a campus legend. Everyone wanted to be in your orbit... especially us junior assistants trying to learn actual chronology from the best.")
				.AddChoice(new DialogueChoice("Did I…treat you poorly?", "treated", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1)))
			.AddNode(new DialogueNode("treated", "Mochizuki Yoruka")
				.AddLine("N-No! Well—maybe a little! You never gave me dramatic responses no matter how much I freaked out. But... you never ignored me either.")
				.AddLine("When everyone else said I was too loud or hyper-fixated, you actually listened to my theories.")
				.AddChoice(new DialogueChoice("I'm sorry I forgot you, Yoruka. I'll make sure to remember everything.", "promise", "promised_yoruka", true, null, "promised_yoruka", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 3))
				.AddChoice(new DialogueChoice("What was Project Hyperion?", "hyperion_q", "asked_hyperion", true, null, "asked_hyperion")))
			.AddNode(new DialogueNode("promise", "Mochizuki Yoruka")
				.AddLine("W—What?! Don't just drop heavy, emotional things on me out of nowhere with that deadpan face! It's completely unfair! ...It’s not like I was sitting around waiting for you to validate me or anything, but... if you actually remember, you better keep your promise, okay?! Don't you dare forget me again!")
				// "Choose another option from the bottom": the branch not yet taken is offered here
				.AddChoice(new DialogueChoice("What was Project Hyperion?", "hyperion_q", "asked_hyperion", true, null, "asked_hyperion")))
			.AddNode(new DialogueNode("hyperion_q", "Mochizuki Yoruka")
				.AddLine("Eek—are you joking?")
				.AddLine("You were the lead researcher of the entire project…You and Felix-san are the one all of us junior assistants looked up to as our idols while trying to learn real chronology!")
				.AddLine("I worked so hard just to understand your equations...Hearing you ask me about it like it's nothing makes me feel like you've completely erased me from your memory.")
				.AddLine("Please... don't act like I never mattered to you...")
				.AddChoice(new DialogueChoice("Who is Felix?", "felix_q")))
			.AddNode(new DialogueNode("felix_q", "Narrator")
				.AddLine("You noticed Yoruka flinched slightly, surprised that you forgot about this Felix-person.")
				.SetNext("felix_w"))
			.AddNode(new DialogueNode("felix_w", "Mochizuki Yoruka")
				.AddLine("W-well…")
				.AddLine("Felix Lasarewitch, the golden-boy heir of the institute, the senior researcher you spent every waking hour locked in the lab with, and the most terrifyingly influential person on this entire campus!")
				.AddLine("Everyone, every single student and faculty member, walks on eggshells around him because he practically owned the board of directors!")
				.AddChoice(new DialogueChoice("I... still don't remember him.", "felix_after", affectionCharacter: GameStateManager.Yoruka, affectionDelta: -1)))
			// End of the Felix branch: the apology is still on the table if she has not offered it yet.
			.AddNode(new DialogueNode("felix_after", "Mochizuki Yoruka")
				.AddLine("That’s a shame, we could encounter him around here, since he may be also figuring out how to fix things here.")
				.AddLine("Let’s just carry on to the Atrium then, shall we go, senior?")
				.AddChoice(new DialogueChoice("I'm sorry I forgot you, Yoruka. I'll make sure to remember everything.", "promise", "promised_yoruka", true, null, "promised_yoruka", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 3))));

		// ======================================================================
		// Level 3 - The Atrium. Dialogue staged verbatim from the doc; the
		// RoomAtrium scene is not built yet, so these fire once that level exists.
		// ======================================================================
		RegisterDialogue(new DialogueData("atrium_intro")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("The floor looks like it is changing appearances. Brand new marble floor then broken the next. Be careful where you step on.")
				.SetNext("yoruka1"))
			.AddNode(new DialogueNode("yoruka1", "Mochizuki Yoruka")
				.AddLine("Seems like these tiles are fluctuating through time.")
				.AddLine("Don't step on the blue tiles. They're from a version of this room where the floor never got built.")
				.SetNext("aionia1"))
			.AddNode(new DialogueNode("aionia1", "Aionia Avgerinos")
				.AddLine("That sounds like a very specific problem.")
				.SetNext("yoruka2"))
			.AddNode(new DialogueNode("yoruka2", "Mochizuki Yoruka")
				.AddLine("It is a specific problem! Ugh, why do I have to explain this to you?")
				.SetNext("instr"))
			.AddNode(new DialogueNode("instr", "Instructions")
				.AddLine("Press R to record. Your Echo repeats your recording on a loop. (Show a 5-second recording bar.)")
				.SetNext("yoruka3"))
			.AddNode(new DialogueNode("yoruka3", "Mochizuki Yoruka")
				.AddLine("Just so we're clear, I'm not standing on the plate. The pendant copies you, remember?")
				.SetNext("aionia2"))
			.AddNode(new DialogueNode("aionia2", "Aionia Avgerinos")
				.AddLine("I wasn't going to ask.")
				.SetNext("yoruka4"))
			.AddNode(new DialogueNode("yoruka4", "Mochizuki Yoruka")
				.AddLine("...You were thinking about it. Your face did that thing.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Get across the entry hall.")));

		RegisterDialogue(new DialogueData("atrium_bridge")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("The bridge breathes in and out. There is a rhythm to it. Learn it.")
				.AddLine("Yoruka's panel collapses.")
				.SetNext("panic"))
			.AddNode(new DialogueNode("panic", "Mochizuki Yoruka")
				.AddLine("S-Senior!!")
				.SetChoiceTimeout(8f)   // the bridge is collapsing: not deciding is its own answer
				.AddChoice(new DialogueChoice("Grab her hand.", "a_hand", "bridge_rescued_yoruka", true, null, null, GameStateManager.Yoruka, 2))
				.AddChoice(new DialogueChoice("Tell her to jump onto your Echo's platform.", "b_jump", "bridge_rescued_yoruka", true, null, null, GameStateManager.Yoruka, 1))
				.AddChoice(new DialogueChoice("Keep moving. She'll catch up.", "c_go", "bridge_abandoned_yoruka", true, null, null, GameStateManager.Yoruka, -3)))
			.AddNode(new DialogueNode("a_hand", "Aionia Avgerinos")
				.AddLine("I've got you.")
				.SetNext("a_react"))
			.AddNode(new DialogueNode("a_react", "Mochizuki Yoruka")
				.AddLine("...You didn't hesitate. Not even a little.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("b_jump", "Aionia Avgerinos")
				.AddLine("Jump. Left. My Echo's holding it.")
				.SetNext("b_react"))
			.AddNode(new DialogueNode("b_react", "Mochizuki Yoruka")
				.AddLine("Are you out of your mind?! ...Fine! Fine, fine!")
				.AddLine("That actually worked. Don't look so smug.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("c_go", "Aionia Avgerinos")
				.AddLine("You can make it. Keep going.")
				.SetNext("c_react"))
			.AddNode(new DialogueNode("c_react", "Mochizuki Yoruka")
				.AddLine("Don't say that like it's easy!")
				.SetNext("c_fall"))
			.AddNode(new DialogueNode("c_fall", "Narrator")
				.AddLine("Yoruka falls below the abyss, but you're sure she'll catch up.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Cross the bridge before the panels phase out.")));

		// Scene 3's opening. Both answers are one conversation-ending beat each: the terminal only
		// opens once the puzzle is actually solved (Yoruka's flag, or the two consoles matched),
		// and the reveal is its own conversation so the blueprint's line has one copy, not two.
		RegisterDialogue(new DialogueData("atrium_terminal")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("In front of you is a terminal. You can solve this puzzle by using an Echo or let Yoruka solve it by herself.")
				.AddLine("*Either way, the terminal opens a path to the stairway.")
				.AddChoice(new DialogueChoice("Let Yoruka decode it.", "let_yoruka", "yoruka_solved_terminal", hiddenIfFlag: "yoruka_solved_terminal", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 2))
				.AddChoice(new DialogueChoice("Decode it by yourself.", "self", "atrium_terminal_self", hiddenIfFlag: "atrium_terminal_self", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 0)))
			.AddNode(new DialogueNode("let_yoruka", "Mochizuki Yoruka")
				.AddLine("Heh! The pattern isn't random. It's a Fibonacci offset with a time delay. Of course they hid a lock in my favorite kind of problem.")
				.SetNext("enjoy"))
			.AddNode(new DialogueNode("enjoy", "Aionia Avgerinos")
				.AddLine("You enjoy this.")
				.SetNext("do_i"))
			.AddNode(new DialogueNode("do_i", "Mochizuki Yoruka")
				.AddLine("I do!"))
			.AddNode(new DialogueNode("self", "Objective")
				.AddLine("Match the sequence on both consoles.")));

		// What the terminal says once it is open - played by RoomAtrium for either route, so the
		// blueprint's reveal lands whether Yoruka decoded it or the player matched the codes.
		RegisterDialogue(new DialogueData("atrium_terminal_reveal")
			.AddNode(new DialogueNode("unlock", "Narrator")
				.AddLine("The terminal unlocks. Text flickers across the screen for one second: SUBJECT A.A. ANCHOR STATUS: STABLE.")
				.SetNext("name_q"))
			.AddNode(new DialogueNode("name_q", "Mochizuki Yoruka")
				.AddLine("...Senior, why is your name on a system that's supposed to be broken?")
				.SetNext("warm"))
			.AddNode(new DialogueNode("warm", "Narrator")
				.AddLine("The pendant is warm in your palm. You don't remember putting your hand on it.")));

		RegisterDialogue(new DialogueData("atrium_stairway")
			.AddNode(new DialogueNode("start", "Mochizuki Yoruka")
				.AddLine("I'll just wait up here. Because I can climb. Like a normal person.")
				.SetNext("climb"))
			.AddNode(new DialogueNode("climb", "Narrator")
				.AddLine("You climb. She follows. Somewhere around the third landing, her voice drops.")
				.SetNext("family"))
			.AddNode(new DialogueNode("family", "Mochizuki Yoruka")
				.AddLine("...You know people said I only got into this place because of my family.")
				.AddChoice(new DialogueChoice("Do you believe that?", "believe", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 2))
				.AddChoice(new DialogueChoice("Say nothing and keep climbing.", "silent", affectionCharacter: GameStateManager.Yoruka, affectionDelta: -1)))
			.AddNode(new DialogueNode("believe", "Mochizuki Yoruka")
				.AddLine("Of course not! ...But sometimes, at night, I wonder.")
				.AddChoice(new DialogueChoice("Then why are you still here?", "why_still", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 2)))
			.AddNode(new DialogueNode("why_still", "Mochizuki Yoruka")
				.AddLine("Because I'm going to prove them wrong. Every single one of them.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("silent", "Mochizuki Yoruka")
				.AddLine("...Typical. You never answered that kind of thing before either.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Use your Echo to climb the broken stairs.")));

		// The window's memory carries the CG the blueprint asks for: "CG: Aionia and Felix". The
		// plate is declared on the memory's own nodes, so it lifts for the beats back in the room.
		RegisterDialogue(new DialogueData("atrium_window")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("In the glass, a version of you is standing next to someone. The glass won't show his face.")
				.SetPlate(MemoryCgPath)
				.SetNext("felix1"))
			.AddNode(new DialogueNode("felix1", "Felix Lasarewitch")
				.AddLine("You've been staring at that window for an hour.")
				.AddLine("If something ever happens to me, don't go looking.")
				.SetPlate(MemoryCgPath)
				.SetNext("a_again"))
			.AddNode(new DialogueNode("a_again", "Aionia Avgerinos")
				.AddLine("Say that again.")
				.SetPlate(MemoryCgPath)
				.SetNext("f_joke"))
			.AddNode(new DialogueNode("f_joke", "Felix Lasarewitch")
				.AddLine("It was a joke.")
				.SetPlate(MemoryCgPath)
				.SetNext("a_wasnt"))
			.AddNode(new DialogueNode("a_wasnt", "Aionia Avgerinos")
				.AddLine("It wasn't.")
				.SetPlate(MemoryCgPath)
				.SetNext("felt_afraid"))
			.AddNode(new DialogueNode("felt_afraid", "Aionia Avgerinos")
				.AddLine("Why did he sound so... afraid?")
				.SetPlate(MemoryCgPath)
				.SetNext("f3"))
			.AddNode(new DialogueNode("f3", "Narrator")
				.AddLine("FRAGMENT F3 ACQUIRED: The Window.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Align the reflections in the window.")));

		// --- Memory fragments: the pieces of Aionia's past she recovers ---
		RegisterFragment(new MemoryFragment("pendant", "Fragment I - The Pendant",
			"Cold blue light, and a promise to keep you safe. Felix. The name arrives before the face does.", "touched_pendant"));
		RegisterFragment(new MemoryFragment("hyperion", "Fragment II - Project Hyperion",
			"A machine built to traverse time and space. Every timeline in existence was spiralling toward collapse.", "room01_complete"));
		RegisterFragment(new MemoryFragment("name", "Fragment III - A Name",
			"Aionia Avgerinos. It is your own name, and it fits back into place like a key.", "asked_identity"));
		RegisterFragment(new MemoryFragment("corridor", "Fragment IV - The Fractured Campus",
			"The overgrown, shattered corridors of the Aetherion Institute. Every parallel timeline has collided into one unstable present.", "library_complete"));
		RegisterFragment(new MemoryFragment("window", "Fragment F3 - The Window",
			"Standing at the glass, next to someone whose face the glass won't show. A sunset that was never one sunset.", "atrium_window_seen"));

		RegisterFragment(new MemoryFragment("scan", "Fragment F4 - The Scan", "Your temporal signature is thinning across timelines. The pendant is an anchor.", "F4_scan"));
		RegisterFragment(new MemoryFragment("footage", "Fragment F5 - The Footage", "Aionia at the Hyperion prototype, the pendant falling, and a second shadow reaching for her.", "F5_footage"));
		RegisterFragment(new MemoryFragment("ledger", "Fragment F6 - The Ledger", "A hidden containment account and Hesiod's note about Lasarewitch money.", "F6_ledger"));
		RegisterFragment(new MemoryFragment("resonance", "Fragment F9 - Resonance", "Every timeline ends unless the variable that built the machine is absent. That variable is Aionia.", "F9_resonance"));
		RegisterFragment(new MemoryFragment("divergence", "Fragment F10 - The Divergence", "An override bearing Aionia's credentials was logged four minutes before the explosion, while she was somewhere else.", "F10_divergence"));
		RegisterLevelsFourToSix();
		RegisterLevelSeven();
		RegisterLevelEight();
		RegisterLevelNine();
		RegisterLevelTen();
	}

	// Level 8 dialogue is kept separate from the Atrium hub so its clues and choices remain local.
	private void RegisterLevelEight()
	{
		RegisterDialogue(new DialogueData("level8_intro").AddNode(new DialogueNode("start", "Narrator")
			.AddLine("The time research facility flickers between the day it was built and the night it fell silent.")
			.AddLine("Pale students walk the same paths through the classrooms, unaware of you.").SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective").AddLine("Explore the classroom hallway, then find a way into the record archive.")));
		RegisterDialogue(new DialogueData("level8_chalkboard").AddNode(new DialogueNode("start", "Aionia Avgerinos")
			.AddLine("Look at the board. Whoever wrote that equation, they were scared.").SetNext("hesiod"))
			.AddNode(new DialogueNode("hesiod", "Hesiod Moriarty").AddLine("Or very excited. In this field, they look the same.")));
		RegisterDialogue(new DialogueData("level8_yoruka").AddNode(new DialogueNode("start", "Mochizuki Yoruka")
			.AddLine("...You were so patient with me. I forgot that.")
			.AddChoice(new DialogueChoice("You were worth being patient with.", "warm", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1.5f))
			.AddChoice(new DialogueChoice("I wish I could remember it.", "remember", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1.5f))
			.AddChoice(new DialogueChoice("Keep moving. We can’t linger here.", "cold", affectionCharacter: GameStateManager.Yoruka, affectionDelta: -1.5f)))
			.AddNode(new DialogueNode("warm", "Mochizuki Yoruka").AddLine("Then let’s make a new memory.").SetFlagOnEnter("level8_yoruka_kind"))
			.AddNode(new DialogueNode("remember", "Mochizuki Yoruka").AddLine("Maybe this time, we can keep it.").SetFlagOnEnter("level8_yoruka_kind"))
			.AddNode(new DialogueNode("cold", "Mochizuki Yoruka").AddLine("Right. We should keep going.").SetFlagOnEnter("level8_yoruka_cold")));
		RegisterDialogue(new DialogueData("level8_classroom").AddNode(new DialogueNode("start", "Narrator")
			.AddLine("The pale outline turns toward you, then dissolves into the classroom's old light.")));
		RegisterDialogue(new DialogueData("level8_whiteboard").AddNode(new DialogueNode("start", "Narrator").AddLine("2. Hyperion safety margins. Someone has circled ‘unacceptable’ three times.")));
		RegisterDialogue(new DialogueData("level8_minutes").AddNode(new DialogueNode("start", "Narrator").AddLine("Board notes. Felix wanted the project shut down. The professors refused.")));
		RegisterDialogue(new DialogueData("level8_desk").AddNode(new DialogueNode("start", "Narrator").AddLine("A faculty desk. It belongs to someone named Sasha Ackerman.")));
		RegisterDialogue(new DialogueData("level8_locker").AddNode(new DialogueNode("start", "Narrator").AddLine("A faculty locker. It belongs to someone named Enialle Cetnavares.")));
		RegisterDialogue(new DialogueData("level8_envelope").AddNode(new DialogueNode("start", "Narrator").AddLine("An envelope addressed to Felix. Read it publicly or keep it private?")
			.AddChoice(new DialogueChoice("Read it aloud.", "public", affectionCharacter: GameStateManager.Yoruka, affectionDelta: -1).AddAffection(GameStateManager.Adellaine, -1).AddAffection(GameStateManager.Hesiod, 0.5f))
			.AddChoice(new DialogueChoice("Keep it private.", "private")))
			.AddNode(new DialogueNode("public", "Aionia Avgerinos").AddLine("By the time you read this, I’ll be gone from every timeline. Don’t look for me. I’m sorry I never told you. —A.").SetFlagOnEnter("READ_LETTER_PUBLIC"))
			.AddNode(new DialogueNode("private", "Narrator").AddLine("You fold the letter and keep its words to yourself.").SetFlagOnEnter("READ_LETTER_PRIVATE")));
		RegisterDialogue(new DialogueData("level8_resonance").AddNode(new DialogueNode("start", "Recording")
			.AddLine("If you’re hearing this, then it worked, and I’ve forgotten. I’m sorry. I found something in the data I couldn’t un-see.")
			.AddLine("Every timeline ends. Everyone. Unless the variable that built the machine isn’t there to build it. That variable is me.")
			.AddLine("Don’t let anyone stop you from remembering. And please... don’t let him hold on too tightly.").SetNext("fragment"))
			.AddNode(new DialogueNode("fragment", "Narrator").AddLine("FRAGMENT F9 ACQUIRED: Resonance.").SetNext("decision"))
			.AddNode(new DialogueNode("decision", "Aionia Avgerinos").AddLine("She wants to erase herself to save everyone. What do we do?")
				.AddChoice(new DialogueChoice("I’m not going to disappear. I’ll find another way.", "reject", affectionCharacter: GameStateManager.Felix, affectionDelta: 2)
					.AddAffection(GameStateManager.Hesiod, 1).AddAffection(GameStateManager.Adellaine, 1).AddAffection(GameStateManager.Yoruka, 1))
				.AddChoice(new DialogueChoice("If it saves everyone, maybe she was right.", "accept", affectionCharacter: GameStateManager.Felix, affectionDelta: -4)
					.AddAffection(GameStateManager.Hesiod, -4).AddAffection(GameStateManager.Adellaine, -4).AddAffection(GameStateManager.Yoruka, -4))
				.AddChoice(new DialogueChoice("I don’t know what to believe yet.", "uncertain")))
			.AddNode(new DialogueNode("reject", "Hesiod Moriarty").AddLine("Then we keep looking. We won’t give up on you.").SetFlagOnEnter("REJECT_ERASURE").SetNext("done"))
			.AddNode(new DialogueNode("accept", "Mochizuki Yoruka").AddLine("I understand why she chose it. I hate that she had to.").SetFlagOnEnter("ACCEPT_ERASURE").SetNext("done"))
			.AddNode(new DialogueNode("uncertain", "Adellaine Moriarty").AddLine("We can decide when we know more.").SetNext("done"))
			.AddNode(new DialogueNode("done", "Narrator").AddLine("The recording fades. The security mechanism waits ahead.")));
		RegisterDialogue(new DialogueData("level8_hesiod").AddNode(new DialogueNode("start", "Hesiod Moriarty").AddLine("You want me to trust the machine with your future? Then tell me what you learned in that recording.").SetNext("end"))
			.AddNode(new DialogueNode("end", "Hesiod Moriarty").AddLine("I trust you. I’ll stand with you.").SetFlagOnEnter("level8_hesiod_trusted")));
		RegisterDialogue(new DialogueData("level8_adellaine").AddNode(new DialogueNode("start", "Adellaine Moriarty").AddLine("I’m not stepping through unless we’re honest about what this could cost.").SetNext("end"))
			.AddNode(new DialogueNode("end", "Adellaine Moriarty").AddLine("All right. You have my trust.").SetFlagOnEnter("level8_adellaine_trusted")));
		RegisterDialogue(new DialogueData("level8_yoruka_trust").AddNode(new DialogueNode("start", "Mochizuki Yoruka").AddLine("I’m scared, but I believe you. Let’s face the next door together.").SetFlagOnEnter("level8_yoruka_trusted")));
		RegisterDialogue(new DialogueData("level8_clear").AddNode(new DialogueNode("start", "Narrator").AddLine("The security mechanism accepts the party. Sector Alpha opens beyond the time research facility.")));
	}

	// Level 9 follows the PDF's Sector Alpha sequence: chess lock, shared office, divergence
	// simulation, damaged prototype, then the confrontation at the inner core.
	private void RegisterLevelNine()
	{
		RegisterDialogue(new DialogueData("level9_intro").AddNode(new DialogueNode("start", "Narrator")
			.AddLine("Sector Alpha's inner door is a chess lock. Beyond it, the facility keeps the memories it was built to contain.")
			.AddLine("Felix's voice crackles over the intercom. Hesiod hears him and quietly photographs the speaker.").SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective").AddLine("Cross the chessboard to the opposing king. An Echo can draw the pieces away.")));
		RegisterDialogue(new DialogueData("level9_intercom").AddNode(new DialogueNode("start", "Felix Lasarewitch")
			.AddLine("You've come far. Faster than I expected. ...I'm glad. I would like you to do the puzzle for me, Aionia. You always won this one.")
			.AddChoice(new DialogueChoice("Stay there. We'll catch up to you.", "answer", affectionCharacter: GameStateManager.Felix, affectionDelta: 1))
			.AddChoice(new DialogueChoice("Ignore the intercom and solve the lock.", "quiet"))
			.AddChoice(new DialogueChoice("Stop watching and help us!", "answer_cold", affectionCharacter: GameStateManager.Felix, affectionDelta: -1)))
			.AddNode(new DialogueNode("answer", "Felix Lasarewitch").AddLine("Of course. I'll wait.").SetFlagOnEnter("level9_intercom_answered"))
			.AddNode(new DialogueNode("quiet", "Felix Lasarewitch").AddLine("You're quiet. I remember that.").SetFlagOnEnter("level9_intercom_ignored"))
			.AddNode(new DialogueNode("answer_cold", "Felix Lasarewitch").AddLine("No. You have to do this by yourself.").SetFlagOnEnter("level9_intercom_rebuffed")));
		RegisterDialogue(new DialogueData("level9_whiteboard").AddNode(new DialogueNode("start", "Narrator").AddLine("Two handwritings. His corrects mine. Mine corrects his. We were never alone in this equation.")));
		RegisterDialogue(new DialogueData("level9_mugs").AddNode(new DialogueNode("start", "Aionia Avgerinos").AddLine("One is chipped. I think I did that. He kept it anyway.")));
		RegisterDialogue(new DialogueData("level9_chair").AddNode(new DialogueNode("start", "Aionia Avgerinos").AddLine("It's still warm. How is it still warm?").SetNext("memory"))
			.AddNode(new DialogueNode("memory", "Felix Lasarewitch").AddLine("You keep working as if the sun is going to apologize for setting.").SetNext("answer"))
			.AddNode(new DialogueNode("answer", "Aionia Avgerinos").AddLine("Do you ever not follow me?").SetNext("felix"))
			.AddNode(new DialogueNode("felix", "Felix Lasarewitch").AddLine("I tried once. It lasted four minutes.").SetNext("narrator"))
			.AddNode(new DialogueNode("narrator", "Narrator").AddLine("They were so close. You envy both of them.")));
		RegisterDialogue(new DialogueData("level9_drawer").AddNode(new DialogueNode("start", "Narrator").AddLine("A dried flower. A ticket stub. A note: 'For when you remember.'").SetFlagOnEnter("level9_drawer_found")));
		RegisterDialogue(new DialogueData("level9_gear_wound").AddNode(new DialogueNode("start", "Narrator").AddLine("You force the gear forward. Its teeth catch with a dull metallic clunk. Record the motion, then send an Echo back to unwind it.")));
		RegisterDialogue(new DialogueData("level9_machine_restored").AddNode(new DialogueNode("start", "Narrator")
			.AddLine("The machine begins to hum. You remember this sound from somewhere that isn't here.")
			.AddLine("A memory returns: Felix stopped you from stepping into the prototype by triggering the explosion with your pendant.")
			.AddLine("The memory fades. You tell your companions what you remembered. There must be another way.").SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective").AddLine("Confront Felix at the inner core.")));
		RegisterDialogue(new DialogueData("level9_variable_one").AddNode(new DialogueNode("start", "Narrator").AddLine("The simulation lays out the timeline where you explored other possibilities for Project Hyperion.")));
		RegisterDialogue(new DialogueData("level9_variable_two").AddNode(new DialogueNode("start", "Narrator").AddLine("Your professors and investors kept the project moving despite its vanishingly small chance of success.")));
		RegisterDialogue(new DialogueData("level9_variable_three").AddNode(new DialogueNode("start", "Narrator").AddLine("OVERRIDE: A.A. // 4 MIN PRIOR. You were in another room. The signature is yours.").SetNext("aionia"))
			.AddNode(new DialogueNode("aionia", "Aionia Avgerinos").AddLine("Someone used my name. Or someone was me.")));
		RegisterDialogue(new DialogueData("level9_terminal_read").AddNode(new DialogueNode("start", "Narrator")
			.AddLine("Every line without you survives. Every line with you collapses.").SetNext("reaction"))
			.AddNode(new DialogueNode("reaction", "Hesiod Moriarty").AddLine("Convenient. That doesn't make it right.").SetNext("fragment"))
			.AddNode(new DialogueNode("fragment", "Narrator").AddLine("FRAGMENT F10 ACQUIRED: The Divergence.")));
		RegisterDialogue(new DialogueData("level9_felix").AddNode(new DialogueNode("start", "Hesiod Moriarty")
			.AddLine("Lasarewitch. Keep your camera pointed elsewhere. I have eleven photographs of you at the explosion.").SetNext("adellaine"))
			.AddNode(new DialogueNode("adellaine", "Adellaine Moriarty").AddLine("You hurt people, Felix. People died.").SetNext("felix"))
			.AddNode(new DialogueNode("felix", "Felix Lasarewitch").AddLine("I'm aware. I'm not here for them. I stopped time for you. Every second I'm not with you is wasted. Stay. Please. Don't go through that door.")
				.AddChoice(new DialogueChoice("I can't stay like this. You can come with us.", "reject", affectionCharacter: GameStateManager.Felix, affectionDelta: 1))
				.AddChoice(new DialogueChoice("Okay. I'll stay with you.", "fed", flagToSet: "FELIX_BAD_FED", affectionCharacter: GameStateManager.Felix, affectionDelta: 3))
				.AddChoice(new DialogueChoice("I'll stay only if you let time start again.", "bargain", flagToSet: "FELIX_GOOD_PATH", affectionCharacter: GameStateManager.Felix, affectionDelta: 2))
				.AddChoice(new DialogueChoice("Show him the letter.", "letter", flagToSet: "REJECT_ERASURE_CONFRONTED", requiredFlag: "REJECT_ERASURE")))
			.AddNode(new DialogueNode("reject", "Felix Lasarewitch").AddLine("Then let me come with you.").SetFlagOnEnter("level9_felix_choice").SetNext("end"))
			.AddNode(new DialogueNode("fed", "Felix Lasarewitch").AddLine("Thank you.").SetFlagOnEnter("level9_felix_choice").SetNext("end"))
			.AddNode(new DialogueNode("bargain", "Felix Lasarewitch").AddLine("That is a harder bargain than I'd like.").SetFlagOnEnter("level9_felix_choice").SetNext("end"))
			.AddNode(new DialogueNode("letter", "Felix Lasarewitch").AddLine("You meant to leave without telling me.").SetFlagOnEnter("level9_felix_choice").SetNext("end"))
			.AddNode(new DialogueNode("end", "Narrator").AddLine("The inner core door waits. The next step is yours.")));
		RegisterDialogue(new DialogueData("level9_clear").AddNode(new DialogueNode("start", "Narrator").AddLine("The threshold of Sector Alpha's inner core falls silent. The machine, the override, and Felix's choice are no longer hidden from you.")));
	}

	// Level 10 is the final rooftop: walk to the last Echo, hear the companions' answer, and make
	// the timeline choice at the central console.
	private void RegisterLevelTen()
	{
		RegisterDialogue(new DialogueData("level10_intro").AddNode(new DialogueNode("start", "Narrator")
			.AddLine("You step onto a rooftop beneath every sky at once. The companions appear around you as translucent echoes of themselves.")
			.AddLine("The rings of light converge on a console at the center of the roof.").SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective").AddLine("Reach the last Echo of Aionia at the console.")));
		RegisterDialogue(new DialogueData("level10_echo_many").AddNode(new DialogueNode("start", "??? Aionia")
			.AddLine("You made it. Good. I wasn't sure you would.").SetNext("memory"))
			.AddNode(new DialogueNode("memory", "??? Aionia").AddLine("You collected them all. ...Then you know what I know. And you still hesitate.").SetNext("plan"))
			.AddNode(new DialogueNode("plan", "??? Aionia").AddLine("One button, one sacrifice, one saved universe. That's what I believed when I planned to erase myself.").SetNext("end"))
			.AddNode(new DialogueNode("end", "Aionia Avgerinos").AddLine("I won't leave them without an answer.").SetFlagOnEnter("level10_echo_spoken")));
		RegisterDialogue(new DialogueData("level10_echo_standard").AddNode(new DialogueNode("start", "??? Aionia")
			.AddLine("You made it. Good. I wasn't sure you would.").SetNext("memory"))
			.AddNode(new DialogueNode("memory", "??? Aionia").AddLine("I was going to save them. It's the same thing, from where I stood.").SetNext("plan"))
			.AddNode(new DialogueNode("plan", "??? Aionia").AddLine("One button, one sacrifice, one saved universe. That was my plan.").SetNext("end"))
			.AddNode(new DialogueNode("end", "Aionia Avgerinos").AddLine("I won't leave them without an answer.").SetFlagOnEnter("level10_echo_spoken")));
		RegisterDialogue(new DialogueData("level10_echo_few").AddNode(new DialogueNode("start", "??? Aionia")
			.AddLine("You don't remember enough to choose. ...That's the saddest outcome. Not death. Not knowing.").SetNext("end"))
			.AddNode(new DialogueNode("end", "Aionia Avgerinos").AddLine("The answer stays beyond reach.").SetFlagOnEnter("level10_echo_spoken")));
		RegisterDialogue(new DialogueData("level10_echo_repeat").AddNode(new DialogueNode("start", "??? Aionia").AddLine("You've heard what I have to say. The choice is yours now.")));

		RegisterDialogue(new DialogueData("level10_companion_repeat").AddNode(new DialogueNode("start", "Narrator").AddLine("The echo fades. You have already heard what they needed to say.")));
		RegisterDialogue(new DialogueData("level10_companion_yoruka_short").AddNode(new DialogueNode("start", "Mochizuki Yoruka").AddLine("Whatever you choose, I want you to know that I'm here. I won't ask you to be someone else.")));
		RegisterDialogue(new DialogueData("level10_companion_adellaine_short").AddNode(new DialogueNode("start", "Adellaine Moriarty").AddLine("I want one thing that's mine: to stand beside you while you decide.")));
		RegisterDialogue(new DialogueData("level10_companion_hesiod_short").AddNode(new DialogueNode("start", "Hesiod Moriarty").AddLine("No matter what happens, I'll remember this moment clearly.")));
		RegisterDialogue(new DialogueData("level10_companion_felix_short").AddNode(new DialogueNode("start", "Felix Lasarewitch").AddLine("I need you, Aionia. But I won't speak for the others.")));

		RegisterDialogue(new DialogueData("level10_companion_yoruka_route").AddNode(new DialogueNode("start", "Mochizuki Yoruka")
			.AddLine("I used to need people to tell me I was enough. I just need you to stay.")
			.AddChoice(new DialogueChoice("You were always enough. Heal for yourself, not for me.", "good", flagToSet: "level10_yoruka_good", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1))
			.AddChoice(new DialogueChoice("Stay with me, always.", "bad", flagToSet: "level10_yoruka_bad", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1))
			.AddChoice(new DialogueChoice("Don't follow me.", "bad", flagToSet: "level10_yoruka_bad", affectionCharacter: GameStateManager.Yoruka, affectionDelta: -2)))
			.AddNode(new DialogueNode("good", "Mochizuki Yoruka").AddLine("Then I'll choose my own way forward.").SetNext("end"))
			.AddNode(new DialogueNode("bad", "Mochizuki Yoruka").AddLine("I don't know if I can let go.").SetNext("end"))
			.AddNode(new DialogueNode("end", "Narrator").AddLine("Yoruka returns to the circle of echoes.")));
		RegisterDialogue(new DialogueData("level10_companion_adellaine_route").AddNode(new DialogueNode("start", "Adellaine Moriarty")
			.AddLine("I've lived for other people's approval my entire life. I want one thing that's mine. I want you.")
			.AddChoice(new DialogueChoice("Let's build a life where you choose it.", "good", flagToSet: "level10_adellaine_good", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 1))
			.AddChoice(new DialogueChoice("I need you for the mission.", "bad", flagToSet: "level10_adellaine_bad", affectionCharacter: GameStateManager.Adellaine, affectionDelta: -1)))
			.AddNode(new DialogueNode("good", "Adellaine Moriarty").AddLine("Then I'll choose it for myself.").SetNext("end"))
			.AddNode(new DialogueNode("bad", "Adellaine Moriarty").AddLine("I thought you understood.").SetNext("end"))
			.AddNode(new DialogueNode("end", "Narrator").AddLine("Adellaine returns to the circle of echoes.")));
		RegisterDialogue(new DialogueData("level10_companion_hesiod_route").AddNode(new DialogueNode("start", "Hesiod Moriarty")
			.AddLine("I never let anyone take my photo. But I'd take yours every day for the rest of my life.")
			.AddChoice(new DialogueChoice("Then let's have that life.", "good", flagToSet: "level10_hesiod_good", affectionCharacter: GameStateManager.Hesiod, affectionDelta: 1))
			.AddChoice(new DialogueChoice("Use my fragments if you have to.", "bad", flagToSet: "level10_hesiod_bad", affectionCharacter: GameStateManager.Hesiod, affectionDelta: -1)))
			.AddNode(new DialogueNode("good", "Hesiod Moriarty").AddLine("I'll take care of them. And of you.").SetNext("end"))
			.AddNode(new DialogueNode("bad", "Hesiod Moriarty").AddLine("I hoped you'd trust me more than that.").SetNext("end"))
			.AddNode(new DialogueNode("end", "Narrator").AddLine("Hesiod returns to the circle of echoes.")));
		RegisterDialogue(new DialogueData("level10_companion_felix_route").AddNode(new DialogueNode("start", "Felix Lasarewitch")
			.AddLine("I need you, Aionia.")
			.AddChoice(new DialogueChoice("Let go, and I'll stay.", "good", flagToSet: "level10_felix_good", affectionCharacter: GameStateManager.Felix, affectionDelta: 1))
			.AddChoice(new DialogueChoice("Hold on. I'll stay forever.", "bad", flagToSet: "level10_felix_bad", affectionCharacter: GameStateManager.Felix, affectionDelta: 1))
			.AddChoice(new DialogueChoice("Then we go together.", "together", flagToSet: "level10_felix_together", affectionCharacter: GameStateManager.Felix, affectionDelta: 1)))
			.AddNode(new DialogueNode("good", "Felix Lasarewitch").AddLine("I'll try. I don't know how, but I'll try.").SetNext("end"))
			.AddNode(new DialogueNode("bad", "Felix Lasarewitch").AddLine("Then I never have to lose you again.").SetNext("end"))
			.AddNode(new DialogueNode("together", "Felix Lasarewitch").AddLine("Together, then. Always.").SetNext("end"))
			.AddNode(new DialogueNode("end", "Narrator").AddLine("Felix returns to the circle of echoes.")));

		RegisterDialogue(new DialogueData("level10_final_choice").AddNode(new DialogueNode("start", "Narrator")
			.AddLine("One button says ERASE. A second says REPAIR. You can also leave the console untouched.")
			.AddChoice(new DialogueChoice("ERASE. Complete the original plan.", "erase"))
			.AddChoice(new DialogueChoice("REPAIR. Fix the timeline and remain.", "repair"))
			.AddChoice(new DialogueChoice("Do nothing.", "wait")))
		.AddNode(new DialogueNode("erase", "Aionia Avgerinos").AddLine("Then let my absence be the thing that saves them.").SetFlagOnEnter("final_choice_erase"))
		.AddNode(new DialogueNode("repair", "Aionia Avgerinos").AddLine("There has to be another way. I'll repair the timeline and stay.").SetFlagOnEnter("final_choice_repair"))
			.AddNode(new DialogueNode("wait", "Narrator").AddLine("The console dims. The answer remains beyond reach.").SetFlagOnEnter("final_choice_do_nothing")));
		RegisterDialogue(new DialogueData("level10_final_choice_replay").AddNode(new DialogueNode("start", "Narrator")
			.AddLine("One button says ERASE. A second says REPAIR. You can also open the Project Theia Containment Vault.")
			.AddChoice(new DialogueChoice("ERASE. Complete the original plan.", "erase"))
			.AddChoice(new DialogueChoice("REPAIR. Fix the timeline and remain.", "repair"))
			.AddChoice(new DialogueChoice("Do nothing.", "wait"))
			.AddChoice(new DialogueChoice("Open the Project Theia Containment Vault.", "true_ending", requiredFlag: "true_ending_unlocked")))
		.AddNode(new DialogueNode("erase", "Aionia Avgerinos").AddLine("Then let my absence be the thing that saves them.").SetFlagOnEnter("final_choice_erase"))
		.AddNode(new DialogueNode("repair", "Aionia Avgerinos").AddLine("There has to be another way. I'll repair the timeline and stay.").SetFlagOnEnter("final_choice_repair"))
			.AddNode(new DialogueNode("wait", "Narrator").AddLine("The console dims. The answer remains beyond reach.").SetFlagOnEnter("final_choice_do_nothing"))
			.AddNode(new DialogueNode("true_ending", "Narrator").AddLine("A hidden door opens onto the Project Theia Containment Vault. A way toward the truth no one could reach before.").SetFlagOnEnter("final_choice_true_ending")));
		RegisterDialogue(new DialogueData("level10_clear").AddNode(new DialogueNode("start", "Narrator").AddLine("The rooftop light folds around you. What comes next is yours to carry.")));
	}

	private void RegisterLevelsFourToSix()
	{
		RegisterDialogue(new DialogueData("level4_heavy_door")
			.AddNode(new DialogueNode("start", "Narrator").AddLine("A door caught your eye, someone is in there. Maybe you should check it out.").SetNext("code"))
			.AddNode(new DialogueNode("code", "Mochizuki Yoruka").AddLine("The sign says 4, ?, 7, 3. The digits add up to 15. The missing digit is 1.").SetNext("instruction"))
			.AddNode(new DialogueNode("instruction", "Instructions").AddLine("Enter the code, record a path to the pressure plate, then project your Echo.").SetNext("dialogue"))
			.AddNode(new DialogueNode("dialogue", "Adellaine Moriarty").AddLine("Don't open that! Unless neither of you is phased. Are you phased?")
				.AddChoice(new DialogueChoice("I don’t think we are.", "answer")))
			.AddNode(new DialogueNode("answer", "Aionia Avgerinos").AddLine("Are we what ?!").SetNext("let_in"))
			.AddNode(new DialogueNode("let_in", "Adellaine Moriarty").AddLine("Alright…I’ll let you in, but I will scan you to make sure. Got it?").SetNext("yes"))
			.AddNode(new DialogueNode("yes", "Aionia Avgerinos").AddLine("Yes yes now come on!").SetNext("objective"))
			// An objective of its own, so the HUD banner carries it: the level's first job is
			// the quarantine door, and the banner is where the player looks for it.
			.AddNode(new DialogueNode("objective", "Objective").AddLine("Open the quarantine door.")));

		RegisterDialogue(new DialogueData("level4_triage_encounter")
			.AddNode(new DialogueNode("start", "Adellaine Moriarty").AddLine("Stop right there. Both of you.").SetNext("yoruka"))
			.AddNode(new DialogueNode("yoruka", "Mochizuki Yoruka").AddLine("Excuse me? Who do you think you are?").SetNext("adellaine"))
			.AddNode(new DialogueNode("adellaine", "Adellaine Moriarty").AddLine("Someone with a scanner. Hold still.").SetNext("scan"))
			.AddNode(new DialogueNode("scan", "Adellaine Moriarty").AddLine("(scans Yoruka) ...Normal. Why are you wearing platform shoes in a place where it can crumble any second?").SetNext("jirai"))
			.AddNode(new DialogueNode("jirai", "Mochizuki Yoruka").AddLine("It's jirai kei. Black and pink, ribbons, hearts. Look it up!").SetNext("library"))
			.AddNode(new DialogueNode("library", "Adellaine Moriarty").AddLine("I will, once there's a library again. ...Oh.").SetNext("what"))
			.AddNode(new DialogueNode("what", "Aionia Avgerinos").AddLine("What is it?").SetNext("recognize"))
			.AddNode(new DialogueNode("recognize", "Adellaine Moriarty").AddLine("Aionia, it’s surprising how you’re still here and not trying to fix your project right now.").SetNext("question"))
			.AddNode(new DialogueNode("question", "Mochizuki Yoruka").AddLine("Senior, do you remember her?")
				.AddChoice(new DialogueChoice("I don't remember her, but she seems to know me.", "amnesia"))
				.AddChoice(new DialogueChoice("She's a medical student. She'd know what's wrong with me.", "medical", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 1)))
			.AddNode(new DialogueNode("amnesia", "Adellaine Moriarty").AddLine("...Amnesia. Of course. I'd have been surprised if you didn't have something wrong with you.").SetNext("what_mean"))
			.AddNode(new DialogueNode("what_mean", "Aionia Avgerinos").AddLine("What do you mean?").SetNext("time_people"))
			.AddNode(new DialogueNode("time_people", "Adellaine Moriarty").AddLine("Well, the nature of you people that studies time…Ah, forget about it.").SetNext("yoruka_defends"))
			.AddNode(new DialogueNode("yoruka_defends", "Mochizuki Yoruka").AddLine("Hey! Don’t speak to her like that! She genuinely can’t remember anything!").SetNext("identify"))
			.AddNode(new DialogueNode("medical", "Adellaine Moriarty").AddLine("Don't flatter me. I'm still deciding if I should let you through.").SetNext("identify"))
			.AddNode(new DialogueNode("identify", "Adellaine Moriarty").AddLine("I'm Adellaine Moriarty. Third year, medicine. You can call me Ellaine if you stop glaring at me.").SetNext("scan_prompt"))
			.AddNode(new DialogueNode("scan_prompt", "Instructions").AddLine("Hold the scan ring over Aionia, then Yoruka, for three seconds each without letting it drift.")));

		RegisterDialogue(new DialogueData("level4_triage_scan_result")
			.AddNode(new DialogueNode("objective", "Objective").AddLine("Examine the room. Find a way to reach the diagnostic terminal.")));
		RegisterDialogue(new DialogueData("level4_cot").AddNode(new DialogueNode("start", "Aionia Avgerinos").AddLine("Someone's been here. The bed's still warm. But nobody's in it.")));
		RegisterDialogue(new DialogueData("level4_cabinet").AddNode(new DialogueNode("start", "Aionia Avgerinos").AddLine("Sealed vials, labelled in handwriting: 'Do not use after phase shift.' Reassuring.")));
		RegisterDialogue(new DialogueData("level4_notes").AddNode(new DialogueNode("start", "Aionia Avgerinos").AddLine("Charts on time sickness. Some of them have my name on them. I never agreed to be a case study.")));
		RegisterDialogue(new DialogueData("level4_whiteboard")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos").AddLine("A roster of names, with one circled: HESIOD M. Under it: 'Last seen: Business Dept. 2 days ago.'").SetNext("adellaine"))
			.AddNode(new DialogueNode("adellaine", "Adellaine Moriarty").AddLine("My brother's name is on that board. Don't touch it.").SetNext("yoruka"))
			.AddNode(new DialogueNode("yoruka", "Mochizuki Yoruka").AddLine("Your brother? Moriarty... Not Hesiod Moriarty? The business one? He's the one who...").SetNext("adellaine2"))
			.AddNode(new DialogueNode("adellaine2", "Adellaine Moriarty").AddLine("The one who what? ...Nothing. Never mind. Keep going. He's older, effortlessly brilliant, and insufferable. He gets better grades than me by skimming. I've spent my whole life trying to beat him. (her voice cracks) And now I can't find him. I'm fine. It's not like I haven't gone through this for years now.")
				.AddChoice(new DialogueChoice("You don't sound fine.", "not_fine", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 2))
				.AddChoice(new DialogueChoice("We can help look for him.", "help", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 2)))
			.AddNode(new DialogueNode("not_fine", "Adellaine Moriarty").AddLine("...Don't. Please. I have about four minutes of composure left.").SetNext("tell"))
			.AddNode(new DialogueNode("help", "Adellaine Moriarty").AddLine("You mean that? You don't even know him. ...Thank you.").SetNext("tell"))
			.AddNode(new DialogueNode("tell", "Adellaine Moriarty").AddLine("Please. Let me come with you.")));

		RegisterDialogue(new DialogueData("level4_diagnostic_terminal")
			.AddNode(new DialogueNode("start", "Narrator").AddLine("The scan loads. Your outline is there, but around the edges it's fading. As if someone were deleting you.").SetNext("adellaine"))
			.AddNode(new DialogueNode("adellaine", "Adellaine Moriarty").AddLine("That isn't damage. That's intentional.").SetNext("aionia"))
			.AddNode(new DialogueNode("aionia", "Aionia Avgerinos").AddLine("Intentional? Who would do that to her?").SetNext("question"))
			.AddNode(new DialogueNode("question", "Adellaine Moriarty").AddLine("...I don't think 'who' is the right question.")
				.AddChoice(new DialogueChoice("Show Adellaine the pendant.", "show", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 1))
				.AddChoice(new DialogueChoice("Keep the pendant hidden.", "hide", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1)))
			.AddNode(new DialogueNode("show", "Aionia Avgerinos").AddLine("A remote controller. For time.").SetNext("anchor"))
			.AddNode(new DialogueNode("anchor", "Adellaine Moriarty").AddLine("...It's a temporal anchor. It's keeping you in one place. Without it, your cells would scatter across the timeline.").SetNext("hesiod"))
			.AddNode(new DialogueNode("hide", "Adellaine Moriarty").AddLine("You're hiding something. I'd do the same.").SetNext("hesiod"))
			.AddNode(new DialogueNode("hesiod", "Adellaine Moriarty").AddLine("Hesiod went to the Business Department before the lockdown, to 'secure the assets.' That's him all over: he cares more about the company than about himself. I tried calling. Nothing. I want to beat him at everything. That doesn't mean I want him to be dead. Please. Let me come with you.")
				.AddChoice(new DialogueChoice("Of course. We need a doctor.", "doctor", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 1))
				.AddChoice(new DialogueChoice("Why should we trust you?", "trust", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1))
				.AddChoice(new DialogueChoice("Tell me about your brother.", "brother", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 2).AddAffection(GameStateManager.Hesiod, 1)))
			.AddNode(new DialogueNode("doctor", "Adellaine Moriarty").AddLine("...Thank you. I won't slow you down.").SetNext("f4"))
			.AddNode(new DialogueNode("trust", "Adellaine Moriarty").AddLine("Because I'm terrified, and people who are terrified don't lie well.").SetNext("f4"))
			.AddNode(new DialogueNode("brother", "Adellaine Moriarty").AddLine("He's tall, annoyingly charming, always carrying a camera. Everyone loves him. ...And he's my brother.").SetNext("hesiod_point"))
			.AddNode(new DialogueNode("hesiod_point", "Hesiod Moriarty").AddLine("She's kind of scary. I like her.").SetNext("f4"))
			.AddNode(new DialogueNode("f4", "Narrator").AddLine("FRAGMENT F4 ACQUIRED: The Scan.").SetFlagOnEnter("adellaine_joined").SetNext("objective"))
			// What is left of the level: the sealed case, and the gate sign in the hall.
			.AddNode(new DialogueNode("objective", "Objective").AddLine("Take the sealed case. Then read the gate sign.")));

		RegisterDialogue(new DialogueData("level4_yoruka")
			.AddNode(new DialogueNode("start", "Mochizuki Yoruka").AddLine("She stared at me earlier. Do you think she hates me?")
				.AddChoice(new DialogueChoice("I think she doesn't know what to do with you.", "end", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1)))
			.AddNode(new DialogueNode("end", "Mochizuki Yoruka").AddLine("...That's worse.")));
		RegisterDialogue(new DialogueData("level4_adellaine_walk")
			.AddNode(new DialogueNode("start", "Adellaine Moriarty").AddLine("Do you remember anything about the project? Even a feeling?")
				.AddChoice(new DialogueChoice("Being responsible for something I couldn't hold.", "end", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 1)))
			.AddNode(new DialogueNode("end", "Adellaine Moriarty").AddLine("...That's what my whole life feels like.")));
		RegisterDialogue(new DialogueData("level4_wait_up").AddNode(new DialogueNode("start", "Mochizuki Yoruka").AddLine("Hey wait up senior! Why are you in such a hurry?")));
		RegisterDialogue(new DialogueData("level4_business_gate")
			.AddNode(new DialogueNode("start", "Narrator").AddLine("The corridor to the Business Department ends at a thick steel gate. A sign: OBSERVATION WING: ALTERNATE ACCESS ONLY.").SetNext("aionia"))
			.AddNode(new DialogueNode("aionia", "Aionia Avgerinos").AddLine("...Then we go through the Observation Wing.").SetFlagOnEnter("medical_route_open")));

		RegisterDialogue(new DialogueData("level5_glass_lock")
			.AddNode(new DialogueNode("start", "Adellaine Moriarty").AddLine("My card worked yesterday. I mean, it worked in another yesterday.").SetNext("yoruka"))
			.AddNode(new DialogueNode("yoruka", "Mochizuki Yoruka").AddLine("So we're stuck outside a door because of paperwork from a timeline that doesn't exist?").SetNext("aionia"))
			.AddNode(new DialogueNode("aionia", "Aionia Avgerinos").AddLine("Maybe the door doesn't need a card. It needs a rhythm.").SetNext("goal"))
			.AddNode(new DialogueNode("goal", "Objective").AddLine("Get the scanner to read a valid sequence.").SetNext("instructions"))
			.AddNode(new DialogueNode("instructions", "Instructions").AddLine("Glass tiles crack after one step and shatter after two. Your Echo's steps count too.").SetNext("reply"))
			.AddNode(new DialogueNode("reply", "Mochizuki Yoruka").AddLine("...Okay, that was cool. Don't tell her I said that.").SetNext("heard"))
			.AddNode(new DialogueNode("heard", "Adellaine Moriarty").AddLine("I heard.")));

		// The gate in the gallery divider. E at a sealed door says nothing on its own, so the door
		// introduces itself the first time she walks up to it (Door.ApproachDialogueId).
		RegisterDialogue(new DialogueData("level5_gate_sealed")
			.AddNode(new DialogueNode("start", "Narrator").AddLine("The gallery gate is sealed. The scanners beside it want a sequence - hold A, then B, then C within a few seconds.")));

		RegisterDialogue(new DialogueData("level5_felix_window")
			.AddNode(new DialogueNode("start", "Objective").AddLine("Talk to the blonde student.").SetNext("felix"))
			.AddNode(new DialogueNode("felix", "Felix Lasarewitch").AddLine("...It’s you…").SetNext("aionia"))
			.AddNode(new DialogueNode("aionia", "Aionia Avgerinos").AddLine("...Felix-san Lasarewitch. I should have known you'd be standing in the one room that still has power.").SetNext("felix2"))
			.AddNode(new DialogueNode("felix2", "Felix Lasarewitch").AddLine("Moriarty. Mochizuki. ...You kept the pendant.")
				.AddChoice(new DialogueChoice("Did you make this pendant?", "made", affectionCharacter: GameStateManager.Felix, affectionDelta: 2))
				.AddChoice(new DialogueChoice("Who are you?", "who", affectionCharacter: GameStateManager.Felix, affectionDelta: 2))
				.AddChoice(new DialogueChoice("Stay back.", "back", affectionCharacter: GameStateManager.Felix, affectionDelta: -1)))
			.AddNode(new DialogueNode("made", "Felix Lasarewitch").AddLine("I kept it safe. …That's enough. That's more than I hoped for.").SetNext("join"))
			.AddNode(new DialogueNode("who", "Felix Lasarewitch").AddLine("A fair question. I'll answer it as many times as you need to ask.").SetNext("join"))
			.AddNode(new DialogueNode("back", "Felix Lasarewitch").AddLine("That's new. Fine. I'll stay exactly this far away.").SetNext("join"))
			.AddNode(new DialogueNode("join", "Aionia Avgerinos").AddLine("We're going to the Business Department. My brother is there.").SetNext("felix_join"))
			.AddNode(new DialogueNode("felix_join", "Felix Lasarewitch").AddLine("…I’ll come with you, can I?").AddChoice(new DialogueChoice("Let him come with you.", "come", affectionCharacter: GameStateManager.Felix, affectionDelta: 1)).AddChoice(new DialogueChoice("Refuse.", "refuse", affectionCharacter: GameStateManager.Felix, affectionDelta: -1).AddAffection(GameStateManager.Yoruka, 1).AddAffection(GameStateManager.Adellaine, 1)))
			.AddNode(new DialogueNode("come", "Felix Lasarewitch").AddLine("Thank you, Aionia.").SetFlagOnEnter("felix_joined"))
			.AddNode(new DialogueNode("refuse", "Mochizuki Yoruka").AddLine("I insist, really. I can help you with clearing out some things here.").SetNext("adellaine"))
			.AddNode(new DialogueNode("adellaine", "Adellaine Moriarty").AddLine("I think we should let him anyway. He could be helpful.").SetFlagOnEnter("felix_joined")));

		// Scene 3's tail. The fragment and the "did it show you anything?" beat both land at the end of
		// the footage conversation, so this is the door itself: the way on to the conduit, and the
		// line the player gets if she goes back and talks to him again.
		RegisterDialogue(new DialogueData("level5_felix_door")
			.AddNode(new DialogueNode("start", "Felix Lasarewitch").AddLine("The service conduit, then. It is the only way on to the Business Department from here.").SetNext("yoruka"))
			.AddNode(new DialogueNode("yoruka", "Mochizuki Yoruka").AddLine("Senior, you are not seriously crawling through that.").SetNext("aionia"))
			.AddNode(new DialogueNode("aionia", "Aionia Avgerinos").AddLine("Hesiod went that way. I am not taking the long route.")));

		RegisterDialogue(new DialogueData("level5_security_footage")
			.AddNode(new DialogueNode("start", "Objective").AddLine("Review the footage. Find where Hesiod went.").SetNext("narration"))
			.AddNode(new DialogueNode("narration", "Narrator").AddLine("The footage grinds forward, grey and grainy, as if the camera were watching from the ceiling.").AddLine("In the first clip, a tall figure with a camera smiles up at the lens and walks into the Business Department.").AddLine("The last clip is from a circular room you recognize without knowing why. A figure with short dark hair is bent over a machine, working fast. Aionia at the console of the Hyperion prototype, hands moving across the controls. A second shadow stretches across the floor from the edge of the frame.").AddLine("Audio crackles: two voices, words dropped. '...why would you...' '...the only way...' '...won't let you...' The shadow's hand closes on her arm. Something small and blue spins out of her hands and skids across the floor. The picture tears into static and cuts to white.").SetNext("talk"))
			.AddNode(new DialogueNode("talk", "Aionia Avgerinos").AddLine("...Was that you, senior?").SetNext("yoruka"))
			.AddNode(new DialogueNode("yoruka", "Mochizuki Yoruka").AddLine("Someone grabbed her. That's all I can tell.").SetNext("adellaine"))
			.AddNode(new DialogueNode("adellaine", "Adellaine Moriarty").AddLine("Two people, and one of them was her. Who? Why can't we see them?").SetNext("felix"))
			.AddNode(new DialogueNode("felix", "Felix Lasarewitch").AddLine("The camera lost them. It lost everything four minutes later.").SetNext("continue"))
			.AddNode(new DialogueNode("continue", "Aionia Avgerinos").AddLine("Let’s continue to the Business Department, we saw Hesiod go there.").SetNext("pendant"))
			.AddNode(new DialogueNode("pendant", "Narrator").AddLine("You look at the small blue thing on the floor. It's the pendant. It is in your hand right now. You feel it grow warm.").SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective").AddLine("Talk to Felix.").SetNext("felix_door"))
			.AddNode(new DialogueNode("felix_door", "Felix Lasarewitch").AddLine("Did it show you anything?").AddChoice(new DialogueChoice("Someone was fighting me. I dropped the pendant.", "someone", affectionCharacter: GameStateManager.Felix, affectionDelta: 1)).AddChoice(new DialogueChoice("Nothing useful.", "nothing")).AddChoice(new DialogueChoice("Were you there?", "there")))
			.AddNode(new DialogueNode("someone", "Felix Lasarewitch").AddLine("...Then you weren't alone.").SetNext("fragment"))
			.AddNode(new DialogueNode("nothing", "Felix Lasarewitch").AddLine("I see.").SetNext("fragment"))
			.AddNode(new DialogueNode("there", "Felix Lasarewitch").AddLine("I was in the building. So was everyone.").SetNext("fragment"))
			.AddNode(new DialogueNode("fragment", "Narrator").AddLine("FRAGMENT F5 ACQUIRED: The Footage.").SetFlagOnEnter("F5_footage")));

		RegisterDialogue(new DialogueData("level5_conduit")
			.AddNode(new DialogueNode("start", "Objective").AddLine("Crawl through the conduit to the Business Department.").SetNext("instructions"))
			.AddNode(new DialogueNode("instructions", "Instructions").AddLine("Hold E on each valve for six seconds. Your Echo keeps the section's steam shut for fifteen seconds.")));

		RegisterDialogue(new DialogueData("level6_lobby_gate")
			.AddNode(new DialogueNode("start", "Narrator").AddLine("Felix leaves at the start of this level. He says he has a solution, but he is lying: he is avoiding Hesiod.").SetNext("felix"))
			.AddNode(new DialogueNode("felix", "Felix Lasarewitch").AddLine("This is where I leave you.").SetNext("aionia"))
			.AddNode(new DialogueNode("aionia", "Aionia Avgerinos").AddLine("Already?").SetNext("facility"))
			.AddNode(new DialogueNode("facility", "Felix Lasarewitch").AddLine("The Time Research Facility. If I can reach the core before it fails, I may be able to stabilize this sector. I'll return when I have something. Don't wait for me.").SetNext("narrator"))
			.AddNode(new DialogueNode("narrator", "Narrator").AddLine("He says it very smoothly. He hasn't looked at the building once. You know what it looks like when someone is lying, even if you don't remember how you know.")
				.AddChoice(new DialogueChoice("Let him go.", "let_go", affectionCharacter: GameStateManager.Felix, affectionDelta: 1))
				.AddChoice(new DialogueChoice("Ask him to stay.", "stay", affectionCharacter: GameStateManager.Felix, affectionDelta: 1))
				.AddChoice(new DialogueChoice("Ask him what the solution is.", "solution"))
				.AddChoice(new DialogueChoice("Catch his sleeve.", "sleeve", affectionCharacter: GameStateManager.Felix, affectionDelta: 2)))
			.AddNode(new DialogueNode("let_go", "Aionia Avgerinos").AddLine("Alright…We’ll meet you at the facility.").SetNext("thank_you"))
			.AddNode(new DialogueNode("stay", "Aionia Avgerinos").AddLine("Can’t you stay? We might need you here.").SetNext("cant"))
			.AddNode(new DialogueNode("cant", "Felix Lasarewitch").AddLine("...I can't. Not here.").SetNext("leave"))
			.AddNode(new DialogueNode("solution", "Aionia Avgerinos").AddLine("Then tell us, what solution do you think is needed to fix this problem?").SetNext("works"))
			.AddNode(new DialogueNode("works", "Felix Lasarewitch").AddLine("I'll tell you when it works.").SetNext("leave"))
			.AddNode(new DialogueNode("sleeve", "Felix Lasarewitch").AddLine("...I'll be at the facility. Come when you're ready.").SetNext("leave"))
			.AddNode(new DialogueNode("thank_you", "Felix Lasarewitch").AddLine("Thank you. Keep the pendant close.").SetNext("leave"))
			.AddNode(new DialogueNode("leave", "Narrator").AddLine("He leaves through a side corridor, and he does not look back.").SetNext("question"))
			.AddNode(new DialogueNode("question", "Aionia Avgerinos").AddLine("That was a lie, wasn't it?").SetNext("yoruka"))
			.AddNode(new DialogueNode("yoruka", "Mochizuki Yoruka").AddLine("He didn't want to be here. I'd like to know why.").SetFlagOnEnter("felix_left")));

		RegisterDialogue(new DialogueData("level6_desk").AddNode(new DialogueNode("start", "Aionia Avgerinos").AddLine("Contracts for a time-travel business plan. Someone has been very optimistic.")));
		RegisterDialogue(new DialogueData("level6_office_objective").AddNode(new DialogueNode("objective", "Objective").AddLine("Look around the office. Find a way to the audit archive.")));
		RegisterDialogue(new DialogueData("level6_jacket").AddNode(new DialogueNode("start", "Aionia Avgerinos").AddLine("A camera strap, still warm. He left in a hurry.")));
		RegisterDialogue(new DialogueData("level6_poster").AddNode(new DialogueNode("start", "Aionia Avgerinos").AddLine("A tall man with an easy grin. Everyone on campus has probably seen him.")));
		RegisterDialogue(new DialogueData("level6_yoruka_poster")
			.AddNode(new DialogueNode("start", "Mochizuki Yoruka").AddLine("...That's. Him.").SetNext("aionia"))
			.AddNode(new DialogueNode("aionia", "Aionia Avgerinos").AddLine("Hesiod? He looks like him.").SetNext("yoruka"))
			.AddNode(new DialogueNode("yoruka", "Mochizuki Yoruka").AddLine("My oshi. Same hair, same eyes, same everything. Who gives someone permission to steal a face?!").SetNext("aionia2"))
			.AddNode(new DialogueNode("aionia2", "Aionia Avgerinos").AddLine("He didn't steal it, Yoruka. It's his face. He was born with it.").SetNext("yoruka2"))
			.AddNode(new DialogueNode("yoruka2", "Mochizuki Yoruka").AddLine("Don't be rational with me right now!")));
		RegisterDialogue(new DialogueData("level6_archive").AddNode(new DialogueNode("start", "Objective").AddLine("Find the hidden account in the ledgers.")));
		RegisterDialogue(new DialogueData("level6_ledger_found")
			.AddNode(new DialogueNode("start", "Narrator").AddLine("The numbers don't add up. Not because they're wrong, but because someone has been very careful to hide them.").SetNext("adellaine"))
			.AddNode(new DialogueNode("adellaine", "Adellaine Moriarty").AddLine("That's Hesiod's handwriting. He suspected Lasarewitch too.").SetNext("yoruka"))
			.AddNode(new DialogueNode("yoruka", "Mochizuki Yoruka").AddLine("So Hesiod-san thought Felix-san was suspicious, and thought he shouldn’t talk to anybody else about it?").SetNext("aionia"))
			.AddNode(new DialogueNode("aionia", "Aionia Avgerinos").AddLine("Hesiod never tells anyone anything. That's his talent.").SetNext("options"))
			.AddNode(new DialogueNode("options", "Adellaine Moriarty").AddLine("He suspected Felix.")
				.AddChoice(new DialogueChoice("Tell Adellaine her brother may be right about Felix.", "right", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 1))
				.AddChoice(new DialogueChoice("Tell her the note proves nothing yet.", "nothing", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 1).AddAffection(GameStateManager.Felix, 1)))
			.AddNode(new DialogueNode("right", "Adellaine Moriarty").AddLine("Maybe Hesiod was right, something is up with Felix.").SetNext("reaction"))
			.AddNode(new DialogueNode("reaction", "Aionia Avgerinos").AddLine("I'd hate that. I'd really hate that.").SetNext("he_might"))
			.AddNode(new DialogueNode("he_might", "Adellaine Moriarty").AddLine("...He might be.").SetNext("f6"))
			.AddNode(new DialogueNode("nothing", "Aionia Avgerinos").AddLine("We can’t suspect Felix yet, there is something that doesn’t add up.").SetNext("youre_right"))
			.AddNode(new DialogueNode("youre_right", "Adellaine Moriarty").AddLine("You're right. I'm panicking over handwriting.").SetNext("f6"))
			.AddNode(new DialogueNode("f6", "Narrator").AddLine("FRAGMENT F6 ACQUIRED: The Ledger.").SetFlagOnEnter("F6_ledger")));

		RegisterDialogue(new DialogueData("level6_hesiod_overlook")
			.AddNode(new DialogueNode("start", "Hesiod Moriarty").AddLine("You're late. I've been expecting someone to come looking.").SetNext("adellaine"))
			.AddNode(new DialogueNode("adellaine", "Adellaine Moriarty").AddLine("HESIOD! Ellaine. You look terrible. I was worried about you! Two days, no message, no word...").SetNext("hesiod"))
			.AddNode(new DialogueNode("hesiod", "Hesiod Moriarty").AddLine("I was working.").SetNext("adellaine2"))
			.AddNode(new DialogueNode("adellaine2", "Adellaine Moriarty").AddLine("You were taking pictures!").SetNext("hesiod2"))
			.AddNode(new DialogueNode("hesiod2", "Hesiod Moriarty").AddLine("It's the same thing.").SetNext("yoruka"))
			.AddNode(new DialogueNode("yoruka", "Mochizuki Yoruka").AddLine("Sigh...I'm so glad you're alive. Don't you dare say I said that.").SetNext("hesiod3"))
			.AddNode(new DialogueNode("hesiod3", "Hesiod Moriarty").AddLine("I didn't hear anything.").SetNext("yoruka2"))
			.AddNode(new DialogueNode("yoruka2", "Hesiod Moriarty").AddLine("...And you. The one who stared at me in the cafeteria for forty minutes.").SetNext("yoruka3"))
			.AddNode(new DialogueNode("yoruka3", "Mochizuki Yoruka").AddLine("I did not! It was a reasonable amount of time to look at someone!").SetNext("hesiod4"))
			.AddNode(new DialogueNode("hesiod4", "Hesiod Moriarty").AddLine("Forty minutes is not a reasonable amount of time.")
				.AddChoice(new DialogueChoice("Stand between Yoruka and Hesiod.", "between", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1))
				.AddChoice(new DialogueChoice("Ask Hesiod why he wrote that note about Lasarewitch.", "note", affectionCharacter: GameStateManager.Hesiod, affectionDelta: 1).AddAffection(GameStateManager.Adellaine, -0.5f)))
			.AddNode(new DialogueNode("between", "Mochizuki Yoruka").AddLine("...I didn't need your help. ...Thanks.").SetNext("where"))
			.AddNode(new DialogueNode("note", "Aionia Avgerinos").AddLine("Why did you write that note about Felix? Are you suspecting him?").SetNext("homework"))
			.AddNode(new DialogueNode("homework", "Hesiod Moriarty").AddLine("Ah. So you've been reading my homework.").SetNext("where"))
			.AddNode(new DialogueNode("where", "Aionia Avgerinos").AddLine("Where were you? Why are you not with Adellaine?")
				.AddChoice(new DialogueChoice("Ask him to explain where he's been.", "around", affectionCharacter: GameStateManager.Hesiod, affectionDelta: 0.5f)))
			.AddNode(new DialogueNode("around", "Hesiod Moriarty").AddLine("Around. Watching. I also wanted to figure out what had happened, luckily I have something to share.").SetNext("felix"))
			.AddNode(new DialogueNode("felix", "Hesiod Moriarty").AddLine("I saw a student leave you at the lobby. Pale hair. Walked like he owned the building.").SetNext("answer"))
			.AddNode(new DialogueNode("answer", "Aionia Avgerinos").AddLine("That was Felix Lasarewitch. He said he was going to the Time Research Facility.").SetNext("hesiod5"))
			.AddNode(new DialogueNode("hesiod5", "Hesiod Moriarty").AddLine("Of course he did. I hope he stays there.").SetFlagOnEnter("hesiod_joined")));

		RegisterDialogue(new DialogueData("level6_back_to_atrium")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos").AddLine("I take that we should go back to the Atrium?").SetNext("yoruka"))
			.AddNode(new DialogueNode("yoruka", "Mochizuki Yoruka").AddLine("Yes, Lasarewitch told us to meet us there, right?").SetNext("choice"))
			.AddNode(new DialogueNode("choice", "Hesiod Moriarty").AddChoice(new DialogueChoice("Yes.", "end")))
			.AddNode(new DialogueNode("end", "Hesiod Moriarty").AddLine("Excellent, lead the way ladies, I’ll watch our backs.").SetNext("yoruka2"))
			.AddNode(new DialogueNode("yoruka2", "Mochizuki Yoruka").AddLine("He talks like my oshi, too. Why does he talk like my oshi?").SetFlagOnEnter("level6_return_ready")));
	}

	// --- Level 7: the Atrium, revisited. The party comes back to it as a hub: three friends to
	// talk to, a camera roll to order, and a night watch that decides whose route this run takes.
	private void RegisterLevelSeven()
	{
		RegisterDialogue(new DialogueData("level7_intro")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("The party returns to the Atrium and finds it transformed. For the first time since Aionia woke up, nothing is trying to kill them.")
				.AddLine("Hesiod is at the north railing of the Glass Skybridge, his camera aimed at the dome.")
				.SetNext("objective"))
			.AddNode(new DialogueNode("objective", "Objective")
				.AddLine("Talk to Hesiod.")));

		RegisterDialogue(new DialogueData("level7_hesiod")
			.AddNode(new DialogueNode("start", "Hesiod Moriarty")
				.AddLine("I told you I had something to share. Every photograph I took shows the same spot at a different time. Help me put them in order.")
				.SetNext("picture"))
			.AddNode(new DialogueNode("picture", "Narrator")
				.AddLine("The photos fall into place. A single figure appears in several frames, always slightly turned away.")
				.SetNext("felix"))
			.AddNode(new DialogueNode("felix", "Hesiod Moriarty")
				.AddLine("I think that’s Felix,")
				.SetNext("fragment"))
			.AddNode(new DialogueNode("fragment", "Narrator")
				.AddLine("FRAGMENT F7 ACQUIRED: The Camera Roll.")
				.SetNext("choice"))
			.AddNode(new DialogueNode("choice", "Aionia Avgerinos")
				.AddChoice(new DialogueChoice("Why are you so sure?", "sure", affectionCharacter: GameStateManager.Hesiod, affectionDelta: 1))
				.AddChoice(new DialogueChoice("Show him the pendant.", "pendant", affectionCharacter: GameStateManager.Hesiod, affectionDelta: 0.5f))
				.AddChoice(new DialogueChoice("Don’t mention the pendant.", "silent", affectionCharacter: GameStateManager.Hesiod, affectionDelta: 0.2f)))
			.AddNode(new DialogueNode("sure", "Hesiod Moriarty")
				.AddLine("How are you so sure that Felix has something to do with this?")
				.SetNext("watched"))
			.AddNode(new DialogueNode("watched", "Hesiod Moriarty")
				.AddLine("Well…yes, I was partnered with the two of you to oversee the finance of project Hyperion.")
				.AddLine("Doesn’t ring a bell?")
				.SetNext("watched_choice"))
			.AddNode(new DialogueNode("watched_choice", "Aionia Avgerinos")
				.AddChoice(new DialogueChoice("No.", "no", affectionCharacter: GameStateManager.Hesiod, affectionDelta: 1)))
			.AddNode(new DialogueNode("no", "Hesiod Moriarty")
				.AddLine("No, unfortunately. It’s alright, you’ll remember in no time."))
			.AddNode(new DialogueNode("pendant", "Hesiod Moriarty")
				.AddLine("Where did you get it? It’s the first time I have seen something like that.")
				.AddLine("Is it another project of yours and Felix?")
				.SetNext("pendant_choice"))
			.AddNode(new DialogueNode("pendant_choice", "Aionia Avgerinos")
				.AddChoice(new DialogueChoice("I don’t actually remember.", "remember", affectionCharacter: GameStateManager.Hesiod, affectionDelta: 1)))
			.AddNode(new DialogueNode("remember", "Hesiod Moriarty")
				.AddLine("I am going to ask about it when we arrive at the facility. Sounds like a good plan."))
			.AddNode(new DialogueNode("silent", "Hesiod Moriarty")
				.AddLine("Something wrong?")
				.SetNext("silent_choice"))
			.AddNode(new DialogueNode("silent_choice", "Aionia Avgerinos")
				.AddChoice(new DialogueChoice("Nothing. I was just lost at the moment.", "lost", affectionCharacter: GameStateManager.Hesiod, affectionDelta: 1)))
			.AddNode(new DialogueNode("lost", "Hesiod Moriarty")
				.AddLine("Lost. Right.")));

		RegisterDialogue(new DialogueData("level7_camera_solved")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("The photos fall into place. A single figure appears in several frames, always slightly turned away.")
				.SetNext("felix"))
			.AddNode(new DialogueNode("felix", "Hesiod Moriarty")
				.AddLine("I think that’s Felix,")
				.SetNext("fragment"))
			.AddNode(new DialogueNode("fragment", "Narrator")
				.AddLine("FRAGMENT F7 ACQUIRED: The Camera Roll.")
				.SetNext("done"))
			.AddNode(new DialogueNode("done", "Hesiod Moriarty")
				.AddLine("Well? Any idea who else is in them?")));

		RegisterDialogue(new DialogueData("level7_adellaine")
			.AddNode(new DialogueNode("start", "Adellaine Moriarty")
				.AddLine("Hold still. …Slow response. You need sleep.")
				.SetNext("choice"))
			.AddNode(new DialogueNode("choice", "Aionia Avgerinos")
				.AddChoice(new DialogueChoice("You could tell him you’re glad.", "glad", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 1.2f))
				.AddChoice(new DialogueChoice("I’ll say it for you.", "for_you", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 2f)))
			.AddNode(new DialogueNode("glad", "Adellaine Moriarty")
				.AddLine("Absolutely not. He’d never let me forget it."))
			.AddNode(new DialogueNode("for_you", "Adellaine Moriarty")
				.AddLine("Don’t you dare… *she chuckled*…But thank you.")));

		RegisterDialogue(new DialogueData("level7_yoruka")
			.AddNode(new DialogueNode("start", "Mochizuki Yoruka")
				.AddLine("Don’t say anything about my hair. The conduit ate a bow. I lost a whole bow.")
				.SetNext("choice"))
			.AddNode(new DialogueNode("choice", "Aionia Avgerinos")
				.AddChoice(new DialogueChoice("I can help you fix it.", "help", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 2f))
				.AddChoice(new DialogueChoice("It still looks good.", "good", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1f)))
			.AddNode(new DialogueNode("help", "Mochizuki Yoruka")
				.AddLine("You? Sew? …Fine. Hold the thread. No, tighter. Don’t poke me with the needle."))
			.AddNode(new DialogueNode("good", "Mochizuki Yoruka")
				.AddLine("L-liar. …Thank you.")));

		RegisterDialogue(new DialogueData("level7_felix_deck")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("The deck. Three reflection spots glow faintly blue on the floor. Felix stands on the second one, with his back to her.")
				.SetNext("felix"))
			.AddNode(new DialogueNode("felix", "Felix Lasarewitch")
				.AddLine("You came back. I hoped you would.")
				.SetNext("choice"))
			.AddNode(new DialogueNode("choice", "Aionia Avgerinos")
				.AddChoice(new DialogueChoice("You said you were going to the Time Research Facility.", "facility", affectionCharacter: GameStateManager.Felix, affectionDelta: 1f))
				.AddChoice(new DialogueChoice("Why are you hiding?", "hiding", affectionCharacter: GameStateManager.Felix, affectionDelta: 2f))
				.AddChoice(new DialogueChoice("Who am I to you?", "who", affectionCharacter: GameStateManager.Felix, affectionDelta: 2f))
				.AddChoice(new DialogueChoice("Were you the one in the footage with me?", "footage", affectionCharacter: GameStateManager.Felix, affectionDelta: 0f)))
			.AddNode(new DialogueNode("facility", "Felix Lasarewitch")
				.AddLine("I said a great many things. The person I was avoiding is in this building. Moriarty doesn’t like me. I don’t blame him."))
			.AddNode(new DialogueNode("hiding", "Felix Lasarewitch")
				.AddLine("I’m not hiding. I’m standing where you saw me last."))
			.AddNode(new DialogueNode("who", "Felix Lasarewitch")
				.AddLine("Everything. I won’t say it again until you remember."))
			.AddNode(new DialogueNode("footage", "Felix Lasarewitch")
				.AddLine("…I’ll tell you everything when you remember. I can tell you I would never have hurt you.")));

		RegisterDialogue(new DialogueData("level7_piano_roll")
			.AddNode(new DialogueNode("start", "Felix Lasarewitch")
				.AddLine("He takes a folded sheet of staff paper from his coat and puts it in your palm. Handwritten notes, in a hand you almost know.")
				.AddLine("The song you used to hum when you worked. I wrote it down so I wouldn’t lose it.")
				.SetNext("fragment"))
			.AddNode(new DialogueNode("fragment", "Narrator")
				.AddLine("FRAGMENT F8 ACQUIRED: The Piano Roll.")
				.SetNext("end"))
			.AddNode(new DialogueNode("end", "Felix Lasarewitch")
				.AddLine("Go back to them. They’ll worry. I’ll be here.")));

		// The night watch. The character whose affection total is highest gets the scene, and the
		// route is locked to whoever says the right thing. The room picks the variant and sets
		// "level7_route_locked" to the character it belongs to.
		RegisterDialogue(new DialogueData("level7_nightwatch_yoruka")
			.AddNode(new DialogueNode("start", "Mochizuki Yoruka")
				.AddLine("I couldn’t sleep. …Can I sit with you? Whatever happens, promise you won’t disappear on me again.")
				.SetNext("choice"))
			.AddNode(new DialogueNode("choice", "Aionia Avgerinos")
				.AddChoice(new DialogueChoice("I promise.", "promise", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 2f))
				.AddChoice(new DialogueChoice("I’ll try.", "try", affectionCharacter: GameStateManager.Yoruka, affectionDelta: 1f))
				.AddChoice(new DialogueChoice("I can’t promise that.", "cant", affectionCharacter: GameStateManager.Yoruka, affectionDelta: -1f)))
			.AddNode(new DialogueNode("promise", "Mochizuki Yoruka")
				.AddLine("…Thank you.").SetFlagOnEnter("level7_route_locked_yoruka"))
			.AddNode(new DialogueNode("try", "Mochizuki Yoruka")
				.AddLine("…Okay."))
			.AddNode(new DialogueNode("cant", "Mochizuki Yoruka")
				.AddLine("That’s fair.")));

		RegisterDialogue(new DialogueData("level7_nightwatch_adellaine")
			.AddNode(new DialogueNode("start", "Adellaine Moriarty")
				.AddLine("Your pulse is steady now. …Would you mind if I just sat with you for a minute?")
				.SetNext("choice"))
			.AddNode(new DialogueNode("choice", "Aionia Avgerinos")
				.AddChoice(new DialogueChoice("Then feel it. I’m here.", "here", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 2f))
				.AddChoice(new DialogueChoice("You don’t have to carry it alone.", "alone", affectionCharacter: GameStateManager.Adellaine, affectionDelta: 1f))
				.AddChoice(new DialogueChoice("We need to focus on the mission.", "mission", affectionCharacter: GameStateManager.Adellaine, affectionDelta: -1f)))
			.AddNode(new DialogueNode("here", "Adellaine Moriarty")
				.AddLine("…Good.").SetFlagOnEnter("level7_route_locked_adellaine"))
			.AddNode(new DialogueNode("alone", "Adellaine Moriarty")
				.AddLine("…Thank you."))
			.AddNode(new DialogueNode("mission", "Adellaine Moriarty")
				.AddLine("Right. Of course.")));

		RegisterDialogue(new DialogueData("level7_nightwatch_hesiod")
			.AddNode(new DialogueNode("start", "Hesiod Moriarty")
				.AddLine("I never let anyone take my photo. But I’d take yours every day for the rest of my life.")
				.SetNext("choice"))
			.AddNode(new DialogueNode("choice", "Aionia Avgerinos")
				.AddChoice(new DialogueChoice("I’d like that.", "like", affectionCharacter: GameStateManager.Hesiod, affectionDelta: 2f))
				.AddChoice(new DialogueChoice("You’d be bored of me.", "bored", affectionCharacter: GameStateManager.Hesiod, affectionDelta: 1f))
				.AddChoice(new DialogueChoice("I only want your information.", "info", affectionCharacter: GameStateManager.Hesiod, affectionDelta: -1f)))
			.AddNode(new DialogueNode("like", "Hesiod Moriarty")
				.AddLine("…Say it again.").SetFlagOnEnter("level7_route_locked_hesiod"))
			.AddNode(new DialogueNode("bored", "Hesiod Moriarty")
				.AddLine("Bored? Never."))
			.AddNode(new DialogueNode("info", "Hesiod Moriarty")
				.AddLine("…Right. That’s all you ever wanted.")));

		RegisterDialogue(new DialogueData("level7_nightwatch_felix")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("They are all asleep. You hold the paper in your hand. You can hear a faint piano melody that wasn’t there before. It’s the song on the page.")
				.SetNext("end"))
			.AddNode(new DialogueNode("end", "Narrator")
				.AddLine("The Felix route is locked.").SetFlagOnEnter("level7_route_locked_felix")));

		RegisterDialogue(new DialogueData("level7_nightwatch_group")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("They are all asleep. Nobody comes to sit with you. The Atrium is quiet enough to hear the building settle.")
				.SetNext("end"))
			.AddNode(new DialogueNode("end", "Narrator")
				.AddLine("No route is locked.")));

		RegisterDialogue(new DialogueData("level7_clear")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("The hatch slides open. A narrow service lift waits below, humming in the dark.")
				.AddLine("Somewhere beneath you, the Time Research Facility is waiting.")));

		RegisterDialogue(new DialogueData("level7_rest")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("You lie on the floor and look at the ceiling. Every time you close your eyes, you see a window full of overlapping skies.")
				.SetNext("awake"))
			.AddNode(new DialogueNode("awake", "Narrator")
				.AddLine("You can't sleep. You get up and go for a walk.")));

		// --- Level 7's fragments ---
		RegisterFragment(new MemoryFragment("camera", "Fragment F7 - The Camera Roll",
			"One spot, photographed over and over at different times of day. A figure that is always slightly turned away.", "F7_camera_roll"));
		RegisterFragment(new MemoryFragment("piano", "Fragment F8 - The Piano Roll",
			"A song you used to hum at work, written out in a hand you almost know. Felix kept it so he would not lose it.", "F8_piano_roll"));
	}
	#endregion
}
