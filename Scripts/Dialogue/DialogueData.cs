using System.Collections.Generic;

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

    public bool IsAvailable()
    {
        return string.IsNullOrEmpty(RequiredFlag) || GameStateManager.Instance?.GetFlag(RequiredFlag) == true;
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

    public DialogueNode AddLine(string line) { _lines.Add(line); return this; }
    public DialogueNode AddChoice(DialogueChoice choice) { _choices.Add(choice); return this; }
    public DialogueNode SetNext(string nextNodeId) { NextNodeId = nextNodeId; return this; }
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
