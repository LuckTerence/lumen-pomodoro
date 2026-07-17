// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "LumenPomodoroMac",
    platforms: [.macOS(.v14)],
    products: [
        .executable(name: "LumenPomodoroMac", targets: ["LumenPomodoroMac"])
    ],
    targets: [
        // 纯逻辑核心（Foundation 层，无 SwiftUI）：模型、存储、洞察，供 App 与测试共用
        .target(
            name: "LumenPomodoroMacCore",
            path: "LumenPomodoroMacCore"
        ),
        .executableTarget(
            name: "LumenPomodoroMac",
            dependencies: ["LumenPomodoroMacCore"],
            path: "LumenPomodoroMac",
            exclude: ["Info.plist", "LumenPomodoroMac.entitlements"]
        ),
        .testTarget(
            name: "LumenPomodoroMacTests",
            dependencies: ["LumenPomodoroMacCore"],
            path: "Tests"
        )
    ]
)
