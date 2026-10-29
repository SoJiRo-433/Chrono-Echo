using System.Collections.Generic;
using Godot;

public sealed class EchoRecorder
{
    #region Private state
    private readonly List<ActionFrame> _frames = new();
    private PlayerAction _lastAction = PlayerAction.Idle;
    private bool _hasLastAction;
    private bool _isRecording;
    #endregion

    #region Public properties
    public bool IsRecording => _isRecording;
    public int FrameCount => _frames.Count;
    #endregion

    #region Recording
    public void StartRecording()
    {
        _frames.Clear();
        _hasLastAction = false;
        _isRecording = true;
    }

    /// <param name="timestamp">Seconds since the loop began (the Room's shared loop clock).</param>
    /// <param name="action">What the player actually did this tick.</param>
    /// <param name="position">Where the player stood at the start of this tick.</param>
    public void Tick(float timestamp, PlayerAction action, Vector2 position)
    {
        if (!_isRecording) return;

        bool actionChanged = !_hasLastAction || action != _lastAction;
        bool isOneShot = action == PlayerAction.Interact || action == PlayerAction.Attack;

        // The very first tick is always stored: it anchors the Echo's starting position.
        if (actionChanged || isOneShot)
            _frames.Add(new ActionFrame(timestamp, position, action));

        _lastAction = action;
        _hasLastAction = true;
    }

    public IReadOnlyList<ActionFrame> StopRecording(float endTimestamp, Vector2 endPosition)
    {
        if (_isRecording)
        {
            _isRecording = false;
            _frames.Add(new ActionFrame(endTimestamp, endPosition, PlayerAction.Idle));
        }
        return new List<ActionFrame>(_frames);
    }

    public void Clear()
    {
        _frames.Clear();
        _hasLastAction = false;
        _isRecording = false;
    }
    #endregion
}
