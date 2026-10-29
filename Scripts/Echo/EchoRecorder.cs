using System.Collections.Generic;
using Godot;

/// <summary>
/// Records what the player does during ONE time loop.
///
/// OOP role: a small single-purpose class (not a Node) that the Player HAS-A (composition).
///
/// WHY WE RECORD ONLY WHEN SOMETHING CHANGES (and not every frame):
///   The game runs at 60 physics ticks per second. A 30-second loop would be 1,800 snapshots
///   if we saved every tick - for EVERY echo, forever. But the player usually holds the same
///   key for many ticks ("walk right for 2 seconds" = 120 identical ticks). Those ticks carry
///   no new information: the Echo can work out the in-between positions itself because it
///   knows start position, end position and the time between them. So we save a frame only
///   when the action CHANGES (plus every one-shot Interact/Attack). Typical result: ~20-60
///   frames per loop instead of 1,800 - less memory, faster replay, easier to debug.
/// </summary>
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
    /// <summary>Begins a fresh recording (any previous frames are discarded).</summary>
    public void StartRecording()
    {
        _frames.Clear();
        _hasLastAction = false;
        _isRecording = true;
    }

    /// <summary>
    /// Called once per physics tick by the Player. Stores a frame ONLY if the action
    /// changed since last tick, or if it is a one-shot (Interact/Attack).
    /// </summary>
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

    /// <summary>
    /// Ends the capture window and returns a COPY of the frames. A final Idle frame is
    /// appended so the Echo knows where the player ended up and when the window closed.
    ///
    /// Safe to call once the window is already closed: the frozen frames come back unchanged
    /// and no second Idle frame is added. That is what lets the player close the window with
    /// the capture key and still project it with R without disturbing it.
    /// </summary>
    public IReadOnlyList<ActionFrame> StopRecording(float endTimestamp, Vector2 endPosition)
    {
        if (_isRecording)
        {
            _isRecording = false;
            _frames.Add(new ActionFrame(endTimestamp, endPosition, PlayerAction.Idle));
        }
        return new List<ActionFrame>(_frames);
    }

    /// <summary>
    /// Throws away the frozen frames and re-arms the recorder. The Player calls this once a
    /// window has been projected as an Echo, so the same window can never be projected twice.
    /// </summary>
    public void Clear()
    {
        _frames.Clear();
        _hasLastAction = false;
        _isRecording = false;
    }
    #endregion
}
