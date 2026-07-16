// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "LumenPomodoroMac",
    platforms: [.macOS(.v14)],
    products: [
        .executable(name: "LumenPomodoroMac", targets: ["LumenPomodoroMac"])
    ],
    targets: [
        .executableTarget(
            name: "LumenPomodoroMac",
            path: "LumenPomodoroMac",
            exclude: ["Info.plist", "LumenPomodoroMac.entitlements"]
        )
    ]
)
