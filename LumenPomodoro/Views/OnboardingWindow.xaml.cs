using System.Windows;
using LumenPomodoro.Models;

namespace LumenPomodoro.Views;

public partial class OnboardingWindow : Window
{
    private int _step;
    private readonly Settings _settings;

    /// <summary>用户是否完成（含跳过）引导。</summary>
    public bool Completed { get; private set; }

    public OnboardingWindow(Settings settings)
    {
        InitializeComponent();
        _settings = settings;
        ShowStep(0);
    }

    private void ShowStep(int step)
    {
        _step = step;
        StepWhy.Visibility = step == 0 ? Visibility.Visible : Visibility.Collapsed;
        StepPrivacy.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        StepPreset.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;

        BackButton.Visibility = step > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content = step >= 2 ? "开始使用" : "下一步";
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_step < 2)
        {
            ShowStep(_step + 1);
            return;
        }

        ApplySelectedPreset();
        Finish(markCompleted: true);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_step > 0)
            ShowStep(_step - 1);
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        // 跳过仍标记完成，避免反复弹；不改用户场景预设
        Finish(markCompleted: true);
    }

    private void ApplySelectedPreset()
    {
        if (PresetStrict.IsChecked == true)
            _settings.ApplyStrictFocusPreset();
        else if (PresetLight.IsChecked == true)
            _settings.ApplyLightFocusPreset();
        else
            _settings.ApplyStandardFocusPreset();

        // 标准/严格会开摄像头：视为已看过隐私页
        if (_settings.CameraAlertEnabled)
            _settings.HasShownCameraPrivacyNotice = true;
    }

    private void Finish(bool markCompleted)
    {
        if (markCompleted)
            _settings.HasCompletedOnboarding = true;
        Completed = true;
        DialogResult = true;
        Close();
    }
}
