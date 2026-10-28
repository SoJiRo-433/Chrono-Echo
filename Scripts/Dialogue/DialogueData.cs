using System.Collections.Generic;

/// <summary>
/// One selectable answer in a dialogue. Immutable (get-only properties).
/// Choosing it can jump to another node and/or set a story flag in the GameStateManager.
/// </summary>
public sealed class DialogueChoice
{
    public string Text { get; }
    public string NextNodeId { get; }       // null/empty = end the dialogue
    public string FlagToSet { get; }        // null/empty = no flag
    public bool FlagValue { get; }
    public string RequiredFlag { get; }     // null/empty = always available

    public DialogueChoice(string text, string nextNodeId = null, string flagToSet = null,
                          bool flagValue = true, string requiredFlag = null)
    {
        Text = text;
        NextNodeId = nextNodeId;
        FlagToSet = flagToSet;
        FlagValue = flagValue;
        RequiredFlag = requiredFlag;
    }

    /// <summary>Hidden unless its RequiredFlag (if any) is set.</summary>
    public bool IsAvailable()
    {
        return string.IsNullOrEmpty(RequiredFlag) || GameStateManager.Instance?.GetFlag(RequiredFlag) == true;
    }
}

/// <summary>
/// One "screen" of dialogue: a speaker, several lines of text and then EITHER choices
/// OR a single next-node reference (or nothing = end). Uses a fluent builder style.
/// </summary>
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

    public DialogueNode AddLine(string line) { _lines.Add(line); return this; }
    public DialogueNode AddChoice(DialogueChoice choice) { _choices.Add(choice); return this; }
    public DialogueNode SetNext(string nextNodeId) { NextNodeId = nextNodeId; return this; }
}

/// <summary>
/// A whole conversation = a dictionary of nodes + the id of the first one.
/// Deliberately tiny: no editor, no scripting language, just objects.
/// </summary>
public sealed class DialogueData
{
    private readonly Dictionary<string, DialogueNode> _nodes = new();

    public string Id { get; }
    public string StartNodeId { get; private set; }

    public DialogueData(string id) { Id = id; }

    /// <summary>The first node added becomes the start node.</summary>
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

/// <summary>
/// A memory the protagonist regains. It unlocks automatically when its RequiredFlag
/// becomes true in the GameStateManager (see DialogueManager.CheckMemoryFragments).
/// </summary>
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
