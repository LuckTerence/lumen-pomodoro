@preconcurrency import AVFoundation
import Foundation

enum CameraError: LocalizedError {
    case permissionDenied
    case noDevice
    case startFailed(String)

    var errorDescription: String? {
        switch self {
        case .permissionDenied:
            return "摄像头权限被拒绝，请前往「系统设置 → 隐私与安全性 → 摄像头」开启 Lumen Pomodoro 的权限。"
        case .noDevice:
            return "未检测到可用的摄像头设备。"
        case .startFailed(let message):
            return "摄像头打开失败：\(message)"
        }
    }
}

@MainActor
final class CameraService: ObservableObject {
    @Published private(set) var isRunning = false
    @Published private(set) var statusMessage = ""

    private var captureSession: AVCaptureSession?
    private var autoStopTask: Task<Void, Never>?
    private static let maxRunMinutes = 30

    func start() async throws {
        if isRunning { return }

        let authorized = await requestAuthorization()
        guard authorized else { throw CameraError.permissionDenied }

        let session = AVCaptureSession()
        session.sessionPreset = .medium

        guard let device = selectDevice() else {
            throw CameraError.noDevice
        }

        let input = try AVCaptureDeviceInput(device: device)
        guard session.canAddInput(input) else {
            throw CameraError.startFailed("无法添加摄像头输入")
        }
        session.addInput(input)

        captureSession = session

        await withCheckedContinuation { continuation in
            DispatchQueue.global(qos: .userInitiated).async {
                session.startRunning()
                continuation.resume()
            }
        }

        isRunning = true
        statusMessage = "摄像头提醒中：当前摄像头被用于点亮指示灯，不会保存或上传画面。"

        autoStopTask?.cancel()
        autoStopTask = Task {
            try? await Task.sleep(for: .seconds(Self.maxRunMinutes * 60))
            guard !Task.isCancelled else { return }
            stop()
            statusMessage = "摄像头已运行超过 \(Self.maxRunMinutes) 分钟，自动保护释放"
        }
    }

    func startForDuration(seconds: Int) async {
        do {
            try await start()
            guard isRunning else { return }
            try? await Task.sleep(for: .seconds(seconds))
            stop()
        } catch {
            statusMessage = error.localizedDescription
        }
    }

    func stop() {
        autoStopTask?.cancel()
        autoStopTask = nil

        if let session = captureSession {
            DispatchQueue.global(qos: .userInitiated).async {
                session.stopRunning()
            }
        }
        captureSession = nil
        isRunning = false
        if statusMessage.contains("提醒中") {
            statusMessage = "摄像头已关闭"
        }
    }

    func listCameraNames() -> [String] {
        let devices = AVCaptureDevice.DiscoverySession(
            deviceTypes: [.builtInWideAngleCamera, .external],
            mediaType: .video,
            position: .unspecified
        ).devices
        let names = devices.map { $0.localizedName }
        return names.isEmpty ? ["默认摄像头"] : names
    }

    private func requestAuthorization() async -> Bool {
        switch AVCaptureDevice.authorizationStatus(for: .video) {
        case .authorized:
            return true
        case .notDetermined:
            return await AVCaptureDevice.requestAccess(for: .video)
        default:
            return false
        }
    }

    private func selectDevice() -> AVCaptureDevice? {
        let devices = AVCaptureDevice.DiscoverySession(
            deviceTypes: [.builtInWideAngleCamera, .external],
            mediaType: .video,
            position: .unspecified
        ).devices
        return devices.first ?? AVCaptureDevice.default(for: .video)
    }
}
