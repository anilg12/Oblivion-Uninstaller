// swift-tools-version:5.9
import PackageDescription

// Oblivion for macOS — native SwiftUI app (Apple Silicon + Intel universal).
// Built into a signed .app bundle and DMG by build-mac.sh.
let package = Package(
    name: "Oblivion",
    platforms: [.macOS(.v14)],
    products: [
        .executable(name: "Oblivion", targets: ["Oblivion"])
    ],
    targets: [
        .executableTarget(
            name: "Oblivion",
            path: "Sources/Oblivion"
        )
    ]
)
