using System.Collections.Generic;

public sealed class DialogueChoice
{
	public string Text { get; }
	public string NextNodeId { get; }       // null/empty = end the dialogue
	public string FlagToSet { get; }        // null/empty = no flag
	public bool FlagValue { get; }
	public string RequiredFlag { get; }        // null/empty = always available
	public string HiddenIfFlag { get; }        // null/empty = never hidden; set = vanish once chosen
	public string AffectionCharacter { get; }  // null/empty = this choice moves no relationship
	public int AffectionDelta { get; }

	public DialogueChoice(string text, string nextNodeId = null, string flagToSet = null,
						  bool flagValue = true, string requiredFlag = null, string hiddenIfFlag = null,
						  string affectionCharacter = null, int affectionDelta = 0)
	{
		Text = text;
		NextNodeId = nextNodeId;
		FlagToSet = flagToSet;
		FlagValue = flagValue;
		RequiredFlag = requiredFlag;
		HiddenIfFlag = hiddenIfFlag;
		AffectionCharacter = affectionCharacter;
		AffectionDelta = affectionDelta;
	}

	public bool IsAvailable()
	{
		if (!string.IsNullOrEmpty(RequiredFlag) && GameStateManager.Instance?.GetFlag(RequiredFlag) != true)
			return false;

		// "hidden if" is the mirror of "required": a branch the player already took
		// disappears from the list when the conversation loops back to the same choice.
		if (!string.IsNullOrEmpty(HiddenIfFlag) && GameStateManager.Instance?.GetFlag(HiddenIfFlag) == true)
			return false;

		return true;
	}
}

public sealed class DialogueNode
{
	private readonly List<string> _lines = new();
	private readonly List<DialogueChoice> _choices = new();

	public string Id { get; }
	public string Speaker { get; }
	public string NextNodeId { get; private set; }

	public IReadOnlyList<string> Lines => _lines;
	public IReadOnlyList<DialogueChoice> Choices => _choices;

	public DialogueNode(string id, string speaker)
	{
		Id = id;
		Speaker = speaker;
	}

	public string FlagOnEnter { get; private set; }
	public bool FlagOnEnterValue { get; private set; } = true;

	/// <summary>Seconds the player has to answer this node's choices. 0 = no clock. When it runs
	/// out the last option is taken, so the "do nothing" answer belongs last.</summary>
	public float ChoiceTimeoutSeconds { get; private set; }

	/// <summary>The CG this node shows behind the dialogue box; empty for none. A CG belongs to a
	/// beat of a conversation rather than to the conversation, so it is declared node by node -
	/// the Atrium's window shows its picture while she is remembering and drops it afterwards.</summary>
	public string PlateImagePath { get; private set; } = "";

	public DialogueNode SetPlate(string imagePath)
	{
		PlateImagePath = imagePath ?? "";
		return this;
	}

	public DialogueNode SetChoiceTimeout(float seconds)
	{
		ChoiceTimeoutSeconds = seconds > 0f ? seconds : 0f;
		return this;
	}

	public DialogueNode AddLine(string line) { _lines.Add(line); return this; }
	public DialogueNode AddChoice(DialogueChoice choice) { _choices.Add(choice); return this; }
	public DialogueNode SetNext(string nextNodeId) { NextNodeId = nextNodeId; return this; }

	/// <summary>Entering this node records a flag. Some beats are their own trigger - the headache
	/// the pendant's flash gives her is what unlocks a CG, and nothing else has to notice it.</summary>
	public DialogueNode SetFlagOnEnter(string flagName, bool value = true)
	{
		FlagOnEnter = flagName;
		FlagOnEnterValue = value;
		return this;
	}
}

public sealed class DialogueData
{
	private readonly Dictionary<string, DialogueNode> _nodes = new();

	public string Id { get; }
	public string StartNodeId { get; private set; }

	public DialogueData(string id) { Id = id; }

	public DialogueData AddNode(DialogueNode node)
	{
		if (_nodes.Count == 0) StartNodeId = node.Id;
		_nodes[node.Id] = node;
		return this;
	}

	public DialogueNode GetNode(string id)
	{
		return !string.IsNullOrEmpty(id) && _nodes.TryGetValue(id, out DialogueNode node) ? node : null;
	}
}

public sealed class MemoryFragment
{
	public string Id { get; }
	public string Title { get; }
	public string Text { get; }
	public string RequiredFlag { get; }

	public MemoryFragment(string id, string title, string text, string requiredFlag)
	{
		Id = id;
		Title = title;
		Text = text;
		RequiredFlag = requiredFlag;
	}
}
