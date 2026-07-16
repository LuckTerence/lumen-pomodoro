import AppKit
import Foundation

@MainActor
final class SoundService {
    static let shared = SoundService()

    private init() {}

    func playCompletion(enabled: Bool) {
        guard enabled else { return }
        NSSound.beep()
    }
}
