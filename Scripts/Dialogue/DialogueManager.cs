using System.Collections.Generic;
using Godot;

/// <summary>
/// Plays DialogueData: shows lines one by one, presents choices, applies each choice's
/// outcome to the GameStateManager flags, and unlocks memory fragments when their flags
/// are set. Pauses gameplay while a conversation is active.
///
/// OOP roles: ENCAPSULATION (all state private; public API = StartDialogue/Advance/
/// SelectChoice), SIGNALS (a real UI can listen to LineChanged / ChoicesPresented...),
/// loose coupling to the story through GameStateManager flags only.
///
/// A tiny placeholder overlay is built in code so the system is testable without any
/// UI scenes. Controls: Enter/Space = next line, 1/2/3 = choose.
/// </summary>
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

	// placeholder UI
	private CanvasLayer _layer;
	private ColorRect _panel;
	private Label _dialogueLabel;
	private Label _toastLabel;
	#endregion

	#region Public properties
	[Export] public bool ShowDebugOverlay { get; set; } = true;
	public bool IsActive => _isActive;
	public IReadOnlyList<MemoryFragment> UnlockedFragments => _unlockedFragments;

	/// <summary>How many memory fragments exist in total, so the HUD can show "2/4".</summary>
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
	/// <summary>Adds a conversation so it can be started by id.</summary>
	public void RegisterDialogue(DialogueData data) { _library[data.Id] = data; }

	/// <summary>Adds a memory fragment that unlocks when its flag becomes true.</summary>
	public void RegisterFragment(MemoryFragment fragment) { _fragments.Add(fragment); }

	/// <summary>Starts a conversation. Returns false if one is already running or the id is unknown.</summary>
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

	/// <summary>Next line, or next node / end when the lines run out. Ignored while a choice is pending.</summary>
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

	/// <summary>Picks a visible choice (0-based), applies its flag outcome and follows its link.</summary>
	public void SelectChoice(int index)
	{
		if (!_isActive || !_awaitingChoice || index < 0 || index >= _visibleChoices.Count) return;

		DialogueChoice choice = _visibleChoices[index];
		_awaitingChoice = false;

		// Apply the outcome to the single source of truth.
		if (!string.IsNullOrEmpty(choice.FlagToSet))
			GameStateManager.Instance?.SetFlag(choice.FlagToSet, choice.FlagValue);

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
		ShowCurrentLine();
	}

	private void ShowCurrentLine()
	{
		EmitSignal(SignalName.LineChanged, _currentNode.Speaker, _currentNode.Lines[_lineIndex]);

		_awaitingChoice = false;
		_visibleChoices.Clear();

		// Choices appear together with the LAST line of the node.
		if (_lineIndex == _currentNode.Lines.Count - 1)
		{
			foreach (DialogueChoice choice in _currentNode.Choices)
				if (choice.IsAvailable()) _visibleChoices.Add(choice);

			if (_visibleChoices.Count > 0)
			{
				_awaitingChoice = true;
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

	/// <summary>Unlocks every fragment whose required flag is set and that is not unlocked yet.</summary>
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

		_panel = new ColorRect { Color = new Color(0f, 0f, 0f, 0.78f), Position = new Vector2(20f, 450f), Size = new Vector2(1112f, 180f), Visible = false };
		_dialogueLabel = new Label { Position = new Vector2(16f, 12f), Size = new Vector2(1080f, 156f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_dialogueLabel.AddThemeFontSizeOverride("font_size", 20);
		_panel.AddChild(_dialogueLabel);

		_toastLabel = new Label { Position = new Vector2(20f, 380f), Size = new Vector2(1100f, 60f), AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
		_toastLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.4f));
		_toastLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_toastLabel.AddThemeConstantOverride("outline_size", 4);

		_layer.AddChild(_panel);
		_layer.AddChild(_toastLabel);
		AddChild(_layer);
	}

	private void RefreshOverlay()
	{
		if (_panel == null) return;
		_panel.Visible = _isActive;
		if (!_isActive || _currentNode == null) return;

		string text = _currentNode.Speaker + ":  " + _currentNode.Lines[_lineIndex];
		if (_awaitingChoice)
		{
			text += "\n";
			for (int i = 0; i < _visibleChoices.Count; i++)
				text += "\n  [" + (i + 1) + "]  " + _visibleChoices[i].Text;
		}
		else
		{
			text += "\n\n  [Enter] continue";
		}
		_dialogueLabel.Text = text;
	}

	private void ShowToast(string message)
	{
		GD.Print(message);
		if (_toastLabel == null) return;
		_toastLabel.Text = message;
		_toastLabel.Visible = true;
		GetTree().CreateTimer(6.0).Timeout += () => { if (IsInstanceValid(_toastLabel)) _toastLabel.Visible = false; };
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
				.AddLine("(Move with WASD, examine things with E. T opens a capture window for your Echo; R ends the loop.)")));

		// --- Scene 2-3: the report log, page by page, ending on the burnt photo ---
		RegisterDialogue(new DialogueData("report_log")
			.AddNode(new DialogueNode("p1", "Aionia Avgerinos")
				.AddLine("A dusty report log, half-buried under the debris.")
				.AddLine("Project Hyperion, to traverse time and space... Interesting.")
				.AddChoice(new DialogueChoice("Turn the page", "p2")))
			.AddNode(new DialogueNode("p2", "Narrator")
				.AddLine("Dense engineering notes. Spatial trajectories, override frequencies, and a schedule of tests that never stopped.")
				.AddChoice(new DialogueChoice("Turn the page", "p3")))
			.AddNode(new DialogueNode("p3", "Narrator")
				.AddLine("The last page is different. It was written by hand, quickly: \"Anchor her position. Do not let the trajectory take her.\"")
				.AddChoice(new DialogueChoice("Turn the page", "photo", "saw_burnt_photo")))
			.AddNode(new DialogueNode("photo", "Narrator")
				.AddLine("Tucked into the final page: a half-burned photo. A figure of you, and another person facing away, looking towards a machine in the same room you are standing in right now.")
				.SetNext("photo2"))
			.AddNode(new DialogueNode("photo2", "Aionia Avgerinos")
				.AddLine("That looks like me, and someone else... But why do I not remember who that is?")
				.AddLine("I should keep this.")));

		RegisterDialogue(new DialogueData("report_log_again")
			.AddNode(new DialogueNode("start", "Narrator")
				.AddLine("The rest of the log is ash and water damage. The burnt photo is still folded into the last page.")));

		// --- Scene 3: everything else worth examining ---
		RegisterDialogue(new DialogueData("drawers")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Papers, a stack of papers, and nothing worth noting...")));

		RegisterDialogue(new DialogueData("time_machine")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Probably the machine that is supposed to traverse time.")
				.AddLine("It is in pieces. Whatever happened here, it happened fast.")));

		RegisterDialogue(new DialogueData("cabinet")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("A broken cabinet. Empty shelves, and the smell of scorched wiring.")));

		RegisterDialogue(new DialogueData("broken_machinery")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("Just some mechanical parts.")));

		// --- Scene 4-5: the pendant, and the memory it drags up with it ---
		RegisterDialogue(new DialogueData("pendant_flash")
			.AddNode(new DialogueNode("start", "Aionia Avgerinos")
				.AddLine("What is this?")
				.SetNext("react"))
			.AddNode(new DialogueNode("react", "Narrator")
				.AddLine("The moment your fingers brush against it, the blue glow intensifies - a chime, a distortion in the air, and then a memory that is not yours to keep yet.")
				.SetNext("felix"))
			.AddNode(new DialogueNode("felix", "Felix")
				.AddLine("Hey, I want to give something to you.")
				.AddLine("Since our study project is time, I figured you need a proper physical failsafe.")
				.AddLine("Consider it a specialized remote controller for the Hyperion core mainframe. If the spatial trajectory fluctuates while we are testing, use this override frequency to anchor your position and recalibrate the system.")
				.AddLine("Make sure you keep it close, alright? No matter where the data takes us... I need you to be safe.")
				.SetNext("after"))
			.AddNode(new DialogueNode("after", "Aionia Avgerinos")
				.AddLine("Argh! What just happened?!")
				.AddLine("Who is that person who gave me this?")
				.SetNext("hint"))
			.AddNode(new DialogueNode("hint", "Narrator")
				.AddLine("(Hint: press T to open a capture window, walk onto the pressure switch, then press R. Your Echo replays exactly that window.)")));

		// --- Scene 6-7: the Echo is what gets you through the security door ---
		RegisterDialogue(new DialogueData("room01_clear")
			.AddNode(new DialogueNode("start", "Pendant")
				.AddLine("The door yielded because a version of you held it open.")
				.AddLine("You are not alone in this loop.")
				.AddChoice(new DialogueChoice("Ask the pendant who you are", "who", "asked_identity", true, "touched_pendant"))
				.AddChoice(new DialogueChoice("Move on", null)))
			.AddNode(new DialogueNode("who", "Pendant")
				.AddLine("Not yet. But you left something behind in the earlier loops.")));

		// --- Room02: deeper into the institute ---
		RegisterDialogue(new DialogueData("room02_intro")
			.AddNode(new DialogueNode("start", "Pendant")
				.AddLine("Deeper into the Aetherion Institute. Two doors this time, and the far one is the way out.")
				.AddLine("(Hint: one Echo can only hold one plate. Capture on the first plate with T, press R, then capture on the second plate and press R again.)")));

		RegisterDialogue(new DialogueData("room02_clear")
			.AddNode(new DialogueNode("start", "Pendant")
				.AddLine("Two of them now, standing exactly where you once stood.")
				.AddLine("The corridor remembers a little more of you each time.")
				.AddChoice(new DialogueChoice("Ask what happens when it remembers everything", "full", "asked_corridor"))
				.AddChoice(new DialogueChoice("Say nothing", null)))
			.AddNode(new DialogueNode("full", "Pendant")
				.AddLine("Then there will be no room left in it for you.")));

		// --- Memory fragments: the pieces of Aionia's past she recovers ---
		RegisterFragment(new MemoryFragment("pendant", "Fragment I - The Pendant",
			"Cold blue light, and a promise to keep you safe. Felix. The name arrives before the face does.", "touched_pendant"));
		RegisterFragment(new MemoryFragment("hyperion", "Fragment II - Project Hyperion",
			"A machine built to traverse time and space. Every timeline in existence was spiralling toward collapse.", "room01_complete"));
		RegisterFragment(new MemoryFragment("name", "Fragment III - A Name",
			"Aionia Avgerinos. It is your own name, and it fits back into place like a key.", "asked_identity"));
		RegisterFragment(new MemoryFragment("corridor", "Fragment IV - The Fractured Campus",
			"The overgrown, shattered corridors of the Aetherion Institute. Every parallel timeline has collided into one unstable present.", "room02_complete"));
	}
	#endregion
}
