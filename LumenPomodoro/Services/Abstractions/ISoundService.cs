namespace LumenPomodoro.Services.Abstractions;

public interface ISoundService : IDisposable
{
    void PlayFocusComplete();
    void PlayBreakComplete();
    void PlayTick();
    
    bool IsMuted { get; set; }
    double Volume { get; set; }
    
    Task LoadCustomSoundAsync(string soundName, string filePath);
    bool HasCustomSound(string soundName);
    void GenerateDefaultWavFiles();
}
