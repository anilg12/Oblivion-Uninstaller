// swift-tools-version:5.9
import PackageDescription

// macOS version (SwiftUI), universal arm64 + x86_64.
// build-mac.sh turns it into a signed .app + dmg
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
