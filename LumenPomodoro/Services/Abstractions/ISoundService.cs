namespace LumenPomodoro.Services.Abstractions;

public interface ISoundService : IDisposable
{
    bool IsMuted { get; set; }
    double Volume { get; set; }

    void PlaySound(string soundName);
    void PlaySoundSync(string soundName);
    void StopSound(string soundName);
    void StopAllSounds();
    void LoadCustomSound(string soundName, string filePath);
}
