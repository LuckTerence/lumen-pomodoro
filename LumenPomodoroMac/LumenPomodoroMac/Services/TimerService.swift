import LumenPomodoroMacCore
import Foundation

@MainActor
final class TimerService: ObservableObject {
    @Published private(set) var mode: TimerMode = .idle
    @Published private(set) var remainingSeconds: Int = 25 * 60
    @Published private(set) var totalSeconds: Int = 25 * 60

    var onTick: ((Int, Int) -> Void)?
    var onFocusCompleted: (() -> Void)?
    var onBreakCompleted: (() -> Void)?
    var onModeChanged: ((TimerMode) -> Void)?

    private var timer: Timer?
    private var endDate: Date?
    private var pausedRemaining: Int?
    private var pausedFromMode: TimerMode?

    var progress: Double {
        guard totalSeconds > 0 else { return 0 }
        return Double(remainingSeconds) / Double(totalSeconds)
    }

    var formattedTime: String {
        let minutes = remainingSeconds / 60
        let seconds = remainingSeconds % 60
        return String(format: "%02d:%02d", minutes, seconds)
    }

    func configureIdle(minutes: Int) {
        guard mode == .idle else { return }
        let clamped = min(max(minutes, 1), 120)
        totalSeconds = clamped * 60
        remainingSeconds = totalSeconds
        onTick?(remainingSeconds, totalSeconds)
    }

    func startFocus(minutes: Int) {
        stopTimer()
        let clamped = min(max(minutes, 1), 120)
        totalSeconds = clamped * 60
        remainingSeconds = totalSeconds
        endDate = Date().addingTimeInterval(TimeInterval(totalSeconds))
        setMode(.focus)
        startTimer()
    }

    func startBreak(minutes: Int) {
        stopTimer()
        let clamped = min(max(minutes, 1), 120)
        totalSeconds = clamped * 60
        remainingSeconds = totalSeconds
        endDate = Date().addingTimeInterval(TimeInterval(totalSeconds))
        setMode(.break)
        startTimer()
    }

    func pause() {
        guard mode == .focus || mode == .break else { return }
        pausedRemaining = remainingSeconds
        pausedFromMode = mode
        endDate = nil
        stopTimer()
        setMode(.paused)
    }

    func resume() {
        guard mode == .paused, let remaining = pausedRemaining else { return }
        remainingSeconds = remaining
        pausedRemaining = nil
        let resumeMode = pausedFromMode ?? .focus
        pausedFromMode = nil
        endDate = Date().addingTimeInterval(TimeInterval(remaining))
        setMode(resumeMode)
        startTimer()
    }

    func reset() {
        stopTimer()
        endDate = nil
        pausedRemaining = nil
        pausedFromMode = nil
        setMode(.idle)
    }

    func correctAfterWake() {
        guard let endDate, mode == .focus || mode == .break else { return }
        let remaining = max(0, Int(endDate.timeIntervalSinceNow))
        remainingSeconds = remaining
        onTick?(remainingSeconds, totalSeconds)
        if remaining <= 0 {
            handleCompletion()
        }
    }

    private func startTimer() {
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in
            Task { @MainActor [weak self] in
                self?.tick()
            }
        }
    }

    private func tick() {
        guard let endDate else { return }
        remainingSeconds = max(0, Int(endDate.timeIntervalSinceNow))
        onTick?(remainingSeconds, totalSeconds)
        if remainingSeconds <= 0 {
            handleCompletion()
        }
    }

    private func handleCompletion() {
        let completedMode = mode
        stopTimer()
        self.endDate = nil
        setMode(.idle)

        switch completedMode {
        case .focus:
            onFocusCompleted?()
        case .break:
            onBreakCompleted?()
        default:
            break
        }
    }

    private func stopTimer() {
        timer?.invalidate()
        timer = nil
    }

    private func setMode(_ newMode: TimerMode) {
        guard mode != newMode else { return }
        mode = newMode
        onModeChanged?(newMode)
    }
}
