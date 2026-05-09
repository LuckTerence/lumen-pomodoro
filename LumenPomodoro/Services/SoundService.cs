using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;

namespace LumenPomodoro.Services;

public class SoundService : IDisposable
{
    private readonly Dictionary<string, SoundPlayer> _players;
    private readonly string _soundsDirectory;
    private double _volume = 1.0;
    private bool _isMuted;

    /// <summary>
    /// 占位属性 — System.Media.SoundPlayer 不支持音量控制，此属性无实际效果。
    /// </summary>
    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0, 1.0);
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set => _isMuted = value;
    }

    public SoundService()
    {
        _players = new Dictionary<string, SoundPlayer>();
        _soundsDirectory = GetSoundsDirectory();
        Directory.CreateDirectory(_soundsDirectory);

        LoadDefaultSounds();
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
                    var player = new SoundPlayer(kvp.Value);
                    player.Load();
                    _players[kvp.Key] = player;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SoundService] 加载音效 {kvp.Key} 失败: {ex.Message}");
                }
            }
        }
    }

    public void PlaySound(string soundName)
    {
        if (_isMuted) return;

        if (_players.ContainsKey(soundName))
        {
            try
            {
                _players[soundName].Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundService] 播放 {soundName} 失败: {ex.Message}");
            }
        }
    }

    public void PlaySoundSync(string soundName)
    {
        if (_isMuted) return;

        if (_players.ContainsKey(soundName))
        {
            try
            {
                _players[soundName].PlaySync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundService] 同步播放 {soundName} 失败: {ex.Message}");
            }
        }
    }

    public void StopSound(string soundName)
    {
        if (_players.ContainsKey(soundName))
        {
            try
            {
                _players[soundName].Stop();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundService] 停止 {soundName} 失败: {ex.Message}");
            }
        }
    }

    public void StopAllSounds()
    {
        foreach (var player in _players.Values)
        {
            try
            {
                player.Stop();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundService] 停止音效失败: {ex.Message}");
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
            var player = new SoundPlayer(filePath);
            player.Load();
            _players[soundName] = player;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load sound file: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        StopAllSounds();
        foreach (var player in _players.Values)
        {
            try
            {
                player.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundService] Dispose 音效失败: {ex.Message}");
            }
        }
        _players.Clear();
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
}
