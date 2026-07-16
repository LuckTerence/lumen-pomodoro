using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using LumenPomodoro.Services.Abstractions;
using Serilog;

namespace LumenPomodoro.Services;

public class SoundService : ISoundService
{
    private readonly Dictionary<string, MediaPlayer> _players;
    private readonly string _soundsDirectory;
    private double _volume = 1.0;
    private bool _isMuted;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0, 1.0);
            ApplyVolumeToAll();
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            ApplyVolumeToAll();
        }
    }

    public SoundService()
    {
        _players = new Dictionary<string, MediaPlayer>();
        _soundsDirectory = GetSoundsDirectory();
        Directory.CreateDirectory(_soundsDirectory);

        LoadDefaultSounds();
    }

    private void ApplyVolumeToAll()
    {
        double effectiveVolume = _isMuted ? 0.0 : _volume;
        foreach (var player in _players.Values)
        {
            try
            {
                player.Volume = effectiveVolume;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "设置音量失败");
            }
        }
    }

    private void LoadDefaultSounds()
    {
        var soundFiles = new Dictionary<string, string>
        {
            { "FocusComplete", Path.Combine(_soundsDirectory, "focus_complete.wav") },
            { "BreakComplete", Path.Combine(_soundsDirectory, "break_complete.wav") },
            { "Tick", Path.Combine(_soundsDirectory, "tick.wav") }
        };

        foreach (var kvp in soundFiles)
        {
            if (File.Exists(kvp.Value))
            {
                try
                {
                    var player = new MediaPlayer();
                    player.Open(new Uri(kvp.Value));
                    player.Volume = _volume;
                    _players[kvp.Key] = player;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "加载音效 {Name} 失败", kvp.Key);
                }
            }
        }
    }

    public void PlaySound(string soundName)
    {
        if (_isMuted) return;

        if (_players.TryGetValue(soundName, out var player))
        {
            try
            {
                player.Stop();
                player.Position = TimeSpan.Zero;
                player.Play();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "播放 {Name} 失败", soundName);
            }
        }
    }

    public void PlaySoundSync(string soundName)
    {
        if (_isMuted) return;

        if (_players.TryGetValue(soundName, out var player))
        {
            try
            {
                var frame = new DispatcherFrame();
                void OnMediaEnded(object? s, EventArgs e)
                {
                    player.MediaEnded -= OnMediaEnded;
                    frame.Continue = false;
                }
                player.MediaEnded += OnMediaEnded;

                player.Stop();
                player.Position = TimeSpan.Zero;
                player.Play();

                Dispatcher.PushFrame(frame);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "同步播放 {Name} 失败", soundName);
            }
        }
    }

    public void StopSound(string soundName)
    {
        if (_players.TryGetValue(soundName, out var player))
        {
            try
            {
                player.Stop();
                player.Position = TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "停止 {Name} 失败", soundName);
            }
        }
    }

    public void StopAllSounds()
    {
        foreach (var kvp in _players)
        {
            try
            {
                kvp.Value.Stop();
                kvp.Value.Position = TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "停止音效 {Name} 失败", kvp.Key);
            }
        }
    }

    public void LoadCustomSound(string soundName, string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Sound file not found: {filePath}");
        }

        try
        {
            if (_players.TryGetValue(soundName, out var oldPlayer))
            {
                oldPlayer.Stop();
                oldPlayer.Close();
            }

            var player = new MediaPlayer();
            player.Open(new Uri(filePath));
            player.Volume = _isMuted ? 0.0 : _volume;
            _players[soundName] = player;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load sound file: {ex.Message}", ex);
        }
    }

    public static void GenerateDefaultWavFiles()
    {
        var soundsDirectory = GetSoundsDirectory();
        Directory.CreateDirectory(soundsDirectory);

        var defaultSounds = new Dictionary<string, int[]>
        {
            {
                "focus_complete.wav",
                new int[] { 800, 1000, 1200 }
            },
            {
                "break_complete.wav",
                new int[] { 600, 800, 600 }
            }
        };

        foreach (var kvp in defaultSounds)
        {
            var filePath = Path.Combine(soundsDirectory, kvp.Key);
            if (!File.Exists(filePath))
            {
                GenerateSimpleWav(filePath, kvp.Value);
            }
        }
    }

    private static string GetSoundsDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LumenPomodoro",
            "Sounds");
    }

    private static void GenerateSimpleWav(string filePath, int[] frequencies)
    {
        int sampleRate = 44100;
        short bitsPerSample = 16;
        short numChannels = 1;
        int durationMs = 200;
        int samplesPerTone = (sampleRate * durationMs) / 1000;

        using (var fs = new FileStream(filePath, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            int totalSamples = samplesPerTone * frequencies.Length;
            int byteRate = sampleRate * numChannels * bitsPerSample / 8;
            short blockAlign = (short)(numChannels * bitsPerSample / 8);
            int dataSize = totalSamples * blockAlign;

            // WAV header
            bw.Write(new char[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + dataSize);
            bw.Write(new char[] { 'W', 'A', 'V', 'E' });
            bw.Write(new char[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1);
            bw.Write(numChannels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write(bitsPerSample);
            bw.Write(new char[] { 'd', 'a', 't', 'a' });
            bw.Write(dataSize);

            // Write audio data
            for (int i = 0; i < frequencies.Length; i++)
            {
                double frequency = frequencies[i];
                for (int j = 0; j < samplesPerTone; j++)
                {
                    double t = (double)j / sampleRate;
                    double envelope = 1.0 - (double)j / samplesPerTone;
                    short sample = (short)(32767 * 0.5 * envelope * Math.Sin(2 * Math.PI * frequency * t));
                    bw.Write(sample);
                }
            }
        }
    }

    public void Dispose()
    {
        foreach (var kvp in _players)
        {
            try
            {
                kvp.Value.Stop();
                kvp.Value.Close();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Dispose 音效 {Name} 失败", kvp.Key);
            }
        }
        _players.Clear();
    }
}
