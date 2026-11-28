using System.Collections.Generic;
using Godot;

public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; }

    private const string MusicBusName = "Music";
    private const string SfxBusName = "SFX";
    private readonly Dictionary<string, AudioStream> _streams = new();
    private AudioStreamPlayer _music;
    private AudioStreamPlayer _walking;
    private AudioStreamPlayer[] _effects;
    private int _nextEffectPlayer;

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        _music = CreatePlayer("Music", MusicBusName);
        _walking = CreatePlayer("Walking", SfxBusName);
        _effects = new[]
        {
            CreatePlayer("EffectsA", SfxBusName),
            CreatePlayer("EffectsB", SfxBusName),
            CreatePlayer("EffectsC", SfxBusName)
        };
    }

    private AudioStreamPlayer CreatePlayer(string name, string bus)
    {
        var player = new AudioStreamPlayer { Name = name, Bus = bus };
        AddChild(player);
        return player;
    }

    private AudioStream LoadStream(string path)
    {
        if (!_streams.TryGetValue(path, out AudioStream stream))
        {
            stream = ResourceLoader.Load<AudioStream>(path);
            if (stream == null) GD.PushWarning($"AudioManager: could not load {path}");
            else _streams[path] = stream;
        }
        return stream;
    }

    public void PlayMusic(string path)
    {
        AudioStream stream = LoadStream(path);
        if (stream == null || _music.Stream == stream && _music.Playing) return;
        _music.Stop();
        _music.Stream = stream;
        _music.Play();
    }

    public void PlayEffect(string path)
    {
        AudioStream stream = LoadStream(path);
        if (stream == null) return;
        AudioStreamPlayer player = _effects[_nextEffectPlayer];
        _nextEffectPlayer = (_nextEffectPlayer + 1) % _effects.Length;
        player.Stream = stream;
        player.Play();
    }

    public void SetWalking(bool walking)
    {
        if (walking)
        {
            if (_walking.Stream == null) _walking.Stream = LoadStream("res://Assets/Audio/SFX/Character/charWalking.mp3");
            if (_walking.Stream is AudioStreamMP3 mp3) mp3.Loop = true;
            if (_walking.Stream is AudioStreamWav wav) wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            if (_walking.Stream != null && !_walking.Playing) _walking.Play();
        }
        else if (_walking.Playing) _walking.Stop();
    }

    public void StopWalking() => SetWalking(false);
}
