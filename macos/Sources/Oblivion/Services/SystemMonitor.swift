import AppKit
import Darwin
import Foundation
import IOKit
import IOKit.ps

// MARK: - Snapshot types

struct ProcessRow: Identifiable, Hashable {
    var id: String { name }
    let name: String
    let count: Int
    /// Share of the whole machine's CPU (0–100).
    let cpu: Double
    let memory: Int64
    let pids: [pid_t]
    /// .app bundle (for the icon), when the process belongs to one.
    let bundlePath: String?
}

struct DriveRow: Identifiable, Hashable {
    var id: String { path }
    let name: String
    let path: String
    let total: Int64
    let free: Int64
    var usedFraction: Double { total > 0 ? Double(total - free) / Double(total) : 0 }
}

struct BatteryInfo: Hashable {
    let percent: Int
    let charging: Bool
    let onAC: Bool
    let minutesLeft: Int?
    let health: Int?
    let cycles: Int?
}

struct SystemSnapshot {
    var cpu: Double = 0
    var cpuHistory: [Double] = []
    var memUsed: UInt64 = 0
    var memTotal: UInt64 = ProcessInfo.processInfo.physicalMemory
    var memPercent: Double = 0
    var memHistory: [Double] = []
    /// °C when a sensor could be read (Apple Silicon HID sensors / Intel SMC), otherwise nil.
    var temperature: Double?
    var thermalState: ProcessInfo.ThermalState = .nominal
    var netDown: Double = 0
    var netUp: Double = 0
    var netDownHistory: [Double] = []
    var netUpHistory: [Double] = []
    var drives: [DriveRow] = []
    var battery: BatteryInfo?
    var uptime: TimeInterval = 0
    var topCPU: [ProcessRow] = []
    var topMemory: [ProcessRow] = []
    var sampled = false
}

struct StaticSystemInfo {
    var modelName = ""
    var modelID = ""
    var chip = SystemInfo.chip
    var cores = ""
    var memory = SystemInfo.memory
    var gpu = ""
    var os = SystemInfo.macOSVersion
    var architecture = ""
}

// MARK: - Monitor

/// Live system statistics. Sampling runs only while a view is subscribed and the window
/// is visible; on battery power it samples less often to save energy.
@MainActor
final class SystemMonitor: ObservableObject {
    @Published private(set) var snapshot = SystemSnapshot()
    @Published private(set) var info = StaticSystemInfo()

    private var subscribers: [String: Bool] = [:]
    private var timer: Timer?
    private var busy = false
    private var paused = false
    private var tick = 0
    private let sampler = Sampler()
    private var observers: [NSObjectProtocol] = []
    private var infoLoaded = false

    init() {
        let center = NotificationCenter.default
        observers.append(center.addObserver(forName: NSWindow.didChangeOcclusionStateNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.updateVisibility() }
        })
        observers.append(center.addObserver(forName: NSApplication.didHideNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.updateVisibility() }
        })
        observers.append(center.addObserver(forName: NSApplication.didUnhideNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.updateVisibility() }
        })
    }

    func subscribe(_ id: String, detailed: Bool) {
        subscribers[id] = detailed
        if detailed { loadStaticInfo() }
        reschedule(immediately: true)
    }

    func unsubscribe(_ id: String) {
        subscribers[id] = nil
        reschedule(immediately: false)
    }

    private var detailed: Bool { subscribers.values.contains(true) }

    private func updateVisibility() {
        let visible = !NSApp.isHidden && NSApp.windows.contains {
            $0.isVisible && $0.frame.width > 600 && $0.occlusionState.contains(.visible)
        }
        if paused == !visible { return }
        paused = !visible
        reschedule(immediately: visible)
    }

    private func reschedule(immediately: Bool) {
        timer?.invalidate()
        timer = nil
        guard !subscribers.isEmpty, !paused else { return }
        let onBattery = snapshot.battery.map { !$0.onAC } ?? false
        let interval: TimeInterval = (detailed ? 1.0 : 2.0) * (onBattery ? 1.5 : 1.0)
        if immediately { sample() }
        let t = Timer(timeInterval: interval, repeats: true) { [weak self] _ in
            Task { @MainActor in self?.sample() }
        }
        t.tolerance = interval * 0.2
        RunLoop.main.add(t, forMode: .common)
        timer = t
    }

    private func sample() {
        guard !busy else { return }
        busy = true
        tick += 1
        let detailed = self.detailed
        let current = tick
        let sampler = self.sampler
        Task.detached(priority: .utility) {
            let snap = sampler.sample(detailed: detailed, tick: current)
            await MainActor.run {
                self.snapshot = snap
                self.busy = false
            }
        }
    }

    private func loadStaticInfo() {
        guard !infoLoaded else { return }
        infoLoaded = true
        Task.detached(priority: .utility) {
            let info = StaticInfoReader.read()
            await MainActor.run { self.info = info }
        }
    }

    // MARK: Actions

    /// Ends every process of a group (apps are asked to quit first).
    func end(_ row: ProcessRow) -> Bool {
        var ok = false
        for pid in row.pids {
            if let app = NSRunningApplication(processIdentifier: pid) {
                ok = app.terminate() || ok
            } else if kill(pid, SIGTERM) == 0 {
                ok = true
            }
        }
        return ok
    }

    static let protectedProcesses: Set<String> = [
        "kernel_task", "launchd", "WindowServer", "loginwindow", "Finder", "Dock", "SystemUIServer",
        "mds", "mds_stores", "mdworker", "coreaudiod", "bluetoothd", "configd", "logd", "opendirectoryd",
        "securityd", "trustd", "cfprefsd", "distnoted", "UserEventAgent", "ControlCenter", "Oblivion",
    ]
}

// MARK: - Sampler (runs off the main thread, one sample at a time)

final class Sampler: @unchecked Sendable {
    private let historyLength = 60
    private var prevTicks: (user: UInt32, system: UInt32, idle: UInt32, nice: UInt32)?
    private var prevNet: [String: (UInt32, UInt32)] = [:]
    private var prevNetTime: TimeInterval = 0
    private var cpuHistory: [Double] = []
    private var memHistory: [Double] = []
    private var downHistory: [Double] = []
    private var upHistory: [Double] = []
    private var temperature: Double?
    private var drives: [DriveRow] = []
    private var topCPU: [ProcessRow] = []
    private var topMemory: [ProcessRow] = []
    private var batteryHealth: (health: Int?, cycles: Int?)?
    private let thermal = ThermalReader()

    func sample(detailed: Bool, tick: Int) -> SystemSnapshot {
        var s = SystemSnapshot()
        s.cpu = cpuUsage()
        let (used, total) = memory()
        s.memUsed = used
        s.memTotal = total
        s.memPercent = total > 0 ? Double(used) / Double(total) * 100 : 0
        let (down, up) = network()
        s.netDown = down
        s.netUp = up

        if tick % 3 == 1 { temperature = thermal.cpuTemperature() }
        s.temperature = temperature
        s.thermalState = ProcessInfo.processInfo.thermalState

        if drives.isEmpty || tick % 10 == 1 { drives = Self.readDrives() }
        s.drives = drives
        s.battery = battery()
        s.uptime = Self.uptime()

        if detailed && (topCPU.isEmpty || tick % 2 == 1) { sampleProcesses() }
        s.topCPU = detailed ? topCPU : []
        s.topMemory = detailed ? topMemory : []

        push(&cpuHistory, s.cpu)
        push(&memHistory, s.memPercent)
        push(&downHistory, down)
        push(&upHistory, up)
        s.cpuHistory = cpuHistory
        s.memHistory = memHistory
        s.netDownHistory = downHistory
        s.netUpHistory = upHistory
        s.sampled = true
        return s
    }

    private func push(_ list: inout [Double], _ value: Double) {
        list.append(value.isFinite ? value : 0)
        if list.count > historyLength { list.removeFirst(list.count - historyLength) }
    }

    // MARK: CPU / memory

    private func cpuUsage() -> Double {
        var info = host_cpu_load_info()
        var count = mach_msg_type_number_t(MemoryLayout<host_cpu_load_info_data_t>.size / MemoryLayout<integer_t>.size)
        let result = withUnsafeMutablePointer(to: &info) {
            $0.withMemoryRebound(to: integer_t.self, capacity: Int(count)) {
                host_statistics(mach_host_self(), HOST_CPU_LOAD_INFO, $0, &count)
            }
        }
        guard result == KERN_SUCCESS else { return 0 }
        let ticks = (user: info.cpu_ticks.0, system: info.cpu_ticks.1, idle: info.cpu_ticks.2, nice: info.cpu_ticks.3)
        defer { prevTicks = ticks }
        guard let prev = prevTicks else { return 0 }
        let user = Double(ticks.user &- prev.user)
        let system = Double(ticks.system &- prev.system)
        let idle = Double(ticks.idle &- prev.idle)
        let nice = Double(ticks.nice &- prev.nice)
        let total = user + system + idle + nice
        return total > 0 ? min(100, max(0, (user + system + nice) / total * 100)) : 0
    }

    private func memory() -> (UInt64, UInt64) {
        let total = ProcessInfo.processInfo.physicalMemory
        var stats = vm_statistics64()
        var count = mach_msg_type_number_t(MemoryLayout<vm_statistics64_data_t>.size / MemoryLayout<integer_t>.size)
        let result = withUnsafeMutablePointer(to: &stats) {
            $0.withMemoryRebound(to: integer_t.self, capacity: Int(count)) {
                host_statistics64(mach_host_self(), HOST_VM_INFO64, $0, &count)
            }
        }
        guard result == KERN_SUCCESS else { return (0, total) }
        var pageSize: vm_size_t = 0
        host_page_size(mach_host_self(), &pageSize)
        // Same as Activity Monitor's "Memory Used": app memory + wired + compressed.
        let app = max(0, Int64(stats.internal_page_count) - Int64(stats.purgeable_count))
        let pages = app + Int64(stats.wire_count) + Int64(stats.compressor_page_count)
        let used = UInt64(max(0, pages)) * UInt64(pageSize)
        return (min(used, total), total)
    }

    // MARK: Network (32-bit interface counters; deltas handle wrap-around)

    private func network() -> (Double, Double) {
        var ifaddr: UnsafeMutablePointer<ifaddrs>?
        guard getifaddrs(&ifaddr) == 0, let first = ifaddr else { return (0, 0) }
        defer { freeifaddrs(ifaddr) }

        var current: [String: (UInt32, UInt32)] = [:]
        var cursor: UnsafeMutablePointer<ifaddrs>? = first
        while let ptr = cursor {
            let ifa = ptr.pointee
            cursor = ifa.ifa_next
            guard let addr = ifa.ifa_addr, addr.pointee.sa_family == UInt8(AF_LINK),
                  let data = ifa.ifa_data else { continue }
            let name = String(cString: ifa.ifa_name)
            if name.hasPrefix("lo") || name.hasPrefix("utun") || name.hasPrefix("awdl") || name.hasPrefix("llw") { continue }
            let d = data.assumingMemoryBound(to: if_data.self).pointee
            current[name] = (d.ifi_ibytes, d.ifi_obytes)
        }

        let now = Date().timeIntervalSinceReferenceDate
        defer {
            prevNet = current
            prevNetTime = now
        }
        guard prevNetTime > 0, now > prevNetTime else { return (0, 0) }
        var down: UInt64 = 0, up: UInt64 = 0
        for (name, value) in current {
            guard let prev = prevNet[name] else { continue }
            down += UInt64(value.0 &- prev.0)
            up += UInt64(value.1 &- prev.1)
        }
        let seconds = now - prevNetTime
        return (Double(down) / seconds, Double(up) / seconds)
    }

    // MARK: Drives / battery / uptime

    static func readDrives() -> [DriveRow] {
        let keys: [URLResourceKey] = [.volumeNameKey, .volumeTotalCapacityKey, .volumeAvailableCapacityForImportantUsageKey,
                                      .volumeIsBrowsableKey, .volumeIsLocalKey]
        let urls = FileManager.default.mountedVolumeURLs(includingResourceValuesForKeys: keys, options: [.skipHiddenVolumes]) ?? []
        var rows: [DriveRow] = []
        for url in urls {
            guard let v = try? url.resourceValues(forKeys: Set(keys)),
                  v.volumeIsBrowsable == true, v.volumeIsLocal == true,
                  let total = v.volumeTotalCapacity, total > 1_000_000_000 else { continue }
            let free = v.volumeAvailableCapacityForImportantUsage ?? 0
            rows.append(DriveRow(name: v.volumeName ?? url.lastPathComponent, path: url.path, total: Int64(total), free: free))
        }
        return rows.sorted { ($0.path == "/" ? 0 : 1, $0.name) < ($1.path == "/" ? 0 : 1, $1.name) }
    }

    private func battery() -> BatteryInfo? {
        guard let blob = IOPSCopyPowerSourcesInfo()?.takeRetainedValue(),
              let list = IOPSCopyPowerSourcesList(blob)?.takeRetainedValue() as? [CFTypeRef] else { return nil }
        for source in list {
            guard let desc = IOPSGetPowerSourceDescription(blob, source)?.takeUnretainedValue() as? [String: Any],
                  (desc["Type"] as? String) == "InternalBattery" else { continue }
            let current = desc["Current Capacity"] as? Int ?? 0
            let maximum = max(1, desc["Max Capacity"] as? Int ?? 100)
            let charging = desc["Is Charging"] as? Bool ?? false
            let onAC = (desc["Power Source State"] as? String) == "AC Power"
            let minutes = desc["Time to Empty"] as? Int
            if batteryHealth == nil { batteryHealth = Self.batteryHealth() }
            return BatteryInfo(percent: min(100, current * 100 / maximum), charging: charging, onAC: onAC,
                               minutesLeft: (minutes ?? -1) > 0 ? minutes : nil,
                               health: batteryHealth?.health, cycles: batteryHealth?.cycles)
        }
        return nil
    }

    private static func batteryHealth() -> (health: Int?, cycles: Int?) {
        let service = IOServiceGetMatchingService(kIOMainPortDefault, IOServiceMatching("AppleSmartBattery"))
        guard service != 0 else { return (nil, nil) }
        defer { IOObjectRelease(service) }
        func prop(_ key: String) -> Int? {
            (IORegistryEntryCreateCFProperty(service, key as CFString, kCFAllocatorDefault, 0)?.takeRetainedValue() as? NSNumber)?.intValue
        }
        let design = prop("DesignCapacity") ?? 0
        let full = prop("AppleRawMaxCapacity") ?? prop("NominalChargeCapacity") ?? prop("MaxCapacity") ?? 0
        let health = design > 0 && full > 100 ? min(100, Int((Double(full) / Double(design) * 100).rounded())) : nil
        return (health, prop("CycleCount"))
    }

    static func uptime() -> TimeInterval {
        var boot = timeval()
        var size = MemoryLayout<timeval>.stride
        var mib: [Int32] = [CTL_KERN, KERN_BOOTTIME]
        guard sysctl(&mib, 2, &boot, &size, nil, 0) == 0, boot.tv_sec > 0 else { return ProcessInfo.processInfo.systemUptime }
        return Date().timeIntervalSince1970 - Double(boot.tv_sec)
    }

    // MARK: Processes (ps: works for every process without special rights)

    private func sampleProcesses() {
        let out = Shell.run("/bin/ps", ["-Ao", "pid=,pcpu=,rss=,comm="]).out
        let cores = Double(max(1, ProcessInfo.processInfo.activeProcessorCount))
        var groups: [String: (count: Int, cpu: Double, mem: Int64, pids: [pid_t], bundle: String?)] = [:]
        for line in out.split(separator: "\n") {
            let parts = line.split(separator: " ", maxSplits: 3, omittingEmptySubsequences: true)
            guard parts.count == 4, let pid = pid_t(parts[0]), let cpu = Double(parts[1]), let rss = Int64(parts[2]) else { continue }
            let command = parts[3].trimmingCharacters(in: .whitespaces)
            var name = (command as NSString).lastPathComponent
            var bundle: String?
            if let range = command.range(of: ".app/") {
                bundle = String(command[..<range.lowerBound]) + ".app"
                if let b = bundle, !command.contains("Helper") {
                    name = ((b as NSString).lastPathComponent as NSString).deletingPathExtension
                }
            }
            if name.isEmpty { continue }
            var g = groups[name] ?? (0, 0, 0, [], bundle)
            g.count += 1
            g.cpu += cpu / cores
            g.mem += rss * 1024
            g.pids.append(pid)
            if g.bundle == nil { g.bundle = bundle }
            groups[name] = g
        }
        let rows = groups.map { ProcessRow(name: $0.key, count: $0.value.count, cpu: min(100, $0.value.cpu), memory: $0.value.mem,
                                           pids: $0.value.pids, bundlePath: $0.value.bundle) }
        topCPU = Array(rows.sorted { $0.cpu > $1.cpu }.prefix(7))
        topMemory = Array(rows.sorted { $0.memory > $1.memory }.prefix(7))
    }
}

// MARK: - Static information

enum StaticInfoReader {
    static func read() -> StaticSystemInfo {
        var info = StaticSystemInfo()
        info.modelID = sysctlString("hw.model") ?? ""
        let physical = sysctlInt("hw.physicalcpu") ?? 0
        let logical = sysctlInt("hw.logicalcpu") ?? 0
        if let perf = sysctlInt("hw.perflevel0.physicalcpu"), let eff = sysctlInt("hw.perflevel1.physicalcpu"), perf > 0, eff > 0 {
            info.cores = "\(perf)P + \(eff)E"
        } else {
            info.cores = physical == logical || logical == 0 ? "\(physical)" : "\(physical) / \(logical)"
        }
        info.architecture = architecture()

        // One-time hardware query (marketing name and graphics), off the main thread.
        let json = Shell.run("/usr/sbin/system_profiler", ["SPHardwareDataType", "SPDisplaysDataType", "-json", "-detailLevel", "mini"]).out
        if let data = json.data(using: .utf8),
           let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
            if let hw = (root["SPHardwareDataType"] as? [[String: Any]])?.first {
                info.modelName = hw["machine_name"] as? String ?? ""
                if let chip = hw["chip_type"] as? String, !chip.isEmpty { info.chip = chip }
                if let mem = hw["physical_memory"] as? String, !mem.isEmpty { info.memory = mem }
            }
            if let gpu = (root["SPDisplaysDataType"] as? [[String: Any]])?.first {
                let name = gpu["sppci_model"] as? String ?? gpu["_name"] as? String ?? ""
                let cores = gpu["sppci_cores"] as? String
                info.gpu = cores.map { "\(name) · \($0) GPU" } ?? name
            }
        }
        return info
    }

    static func architecture() -> String {
        #if arch(arm64)
        return "Apple Silicon (arm64)"
        #else
        return (sysctlInt("sysctl.proc_translated") ?? 0) == 1 ? "Intel (Rosetta)" : "Intel (x86_64)"
        #endif
    }

    static func sysctlInt(_ name: String) -> Int? {
        var value: Int32 = 0
        var size = MemoryLayout<Int32>.size
        guard sysctlbyname(name, &value, &size, nil, 0) == 0 else { return nil }
        return Int(value)
    }

    static func sysctlString(_ name: String) -> String? {
        var size = 0
        guard sysctlbyname(name, nil, &size, nil, 0) == 0, size > 0 else { return nil }
        var buffer = [CChar](repeating: 0, count: size)
        guard sysctlbyname(name, &buffer, &size, nil, 0) == 0 else { return nil }
        return String(cString: buffer)
    }
}

// MARK: - CPU temperature

/// Reads the CPU temperature: Apple Silicon exposes its die sensors through the HID event
/// system, Intel Macs through the SMC. Each architecture tries its native source first.
final class ThermalReader: @unchecked Sendable {
    private let hid = HIDTemperature()
    private let smc = SMCTemperature()

    func cpuTemperature() -> Double? {
        #if arch(arm64)
        return hid.read() ?? smc.read(keys: SMCTemperature.appleSiliconKeys)
        #else
        return smc.read(keys: SMCTemperature.intelKeys) ?? hid.read()
        #endif
    }
}

/// IOHIDEventSystemClient (exported by IOKit, used by Activity-Monitor-like tools).
final class HIDTemperature: @unchecked Sendable {
    private typealias CreateFn = @convention(c) (CFAllocator?) -> OpaquePointer?
    private typealias SetMatchingFn = @convention(c) (OpaquePointer, CFDictionary) -> Int32
    private typealias CopyServicesFn = @convention(c) (OpaquePointer) -> UnsafeRawPointer?
    private typealias CopyPropertyFn = @convention(c) (OpaquePointer, CFString) -> UnsafeRawPointer?
    private typealias CopyEventFn = @convention(c) (OpaquePointer, Int64, Int32, Int64) -> OpaquePointer?
    private typealias GetFloatFn = @convention(c) (OpaquePointer, Int32) -> Double

    private var client: OpaquePointer?
    private var copyServices: CopyServicesFn?
    private var copyProperty: CopyPropertyFn?
    private var copyEvent: CopyEventFn?
    private var getFloat: GetFloatFn?
    private var available = true

    private static let temperatureType: Int64 = 15   // kIOHIDEventTypeTemperature

    init() {
        guard let handle = dlopen("/System/Library/Frameworks/IOKit.framework/IOKit", RTLD_NOW),
              let create = dlsym(handle, "IOHIDEventSystemClientCreate"),
              let setMatching = dlsym(handle, "IOHIDEventSystemClientSetMatching"),
              let services = dlsym(handle, "IOHIDEventSystemClientCopyServices"),
              let property = dlsym(handle, "IOHIDServiceClientCopyProperty"),
              let event = dlsym(handle, "IOHIDServiceClientCopyEvent"),
              let float = dlsym(handle, "IOHIDEventGetFloatValue") else {
            available = false
            return
        }
        copyServices = unsafeBitCast(services, to: CopyServicesFn.self)
        copyProperty = unsafeBitCast(property, to: CopyPropertyFn.self)
        copyEvent = unsafeBitCast(event, to: CopyEventFn.self)
        getFloat = unsafeBitCast(float, to: GetFloatFn.self)
        guard let c = unsafeBitCast(create, to: CreateFn.self)(kCFAllocatorDefault) else {
            available = false
            return
        }
        client = c
        // Apple vendor usage page 0xff00, usage 5 = temperature sensor.
        let matching = ["PrimaryUsagePage": 0xff00, "PrimaryUsage": 5] as CFDictionary
        _ = unsafeBitCast(setMatching, to: SetMatchingFn.self)(c, matching)
    }

    func read() -> Double? {
        guard available, let client, let copyServices, let copyProperty, let copyEvent, let getFloat,
              let servicesPtr = copyServices(client) else { return nil }
        let services = Unmanaged<CFArray>.fromOpaque(servicesPtr).takeRetainedValue()
        var cpu: [Double] = []
        var other: [Double] = []
        let field = Int32(Self.temperatureType << 16)
        for i in 0..<CFArrayGetCount(services) {
            guard let raw = CFArrayGetValueAtIndex(services, i) else { continue }
            let service = OpaquePointer(raw)
            var name = ""
            if let ptr = copyProperty(service, "Product" as CFString) {
                name = (Unmanaged<AnyObject>.fromOpaque(ptr).takeRetainedValue() as? String) ?? ""
            }
            guard let event = copyEvent(service, Self.temperatureType, 0, 0) else { continue }
            let value = getFloat(event, field)
            Unmanaged<AnyObject>.fromOpaque(UnsafeRawPointer(event)).release()
            guard value > 5, value < 125 else { continue }
            let lower = name.lowercased()
            if lower.contains("tdie") || lower.contains("mtr temp") || lower.contains("cpu") || lower.contains("soc") {
                cpu.append(value)
            } else if !lower.contains("battery") && !lower.contains("nand") && !lower.contains("gas gauge") {
                other.append(value)
            }
        }
        if !cpu.isEmpty { return cpu.reduce(0, +) / Double(cpu.count) }
        return other.max()
    }
}

/// Minimal AppleSMC reader for temperature keys (sp78 / flt values).
final class SMCTemperature: @unchecked Sendable {
    static let intelKeys = ["TC0P", "TC0D", "TC0E", "TC0F", "TC0H", "TCXC", "TC1C"]
    static let appleSiliconKeys = ["Tp09", "Tp0T", "Tp01", "Tp05", "Tp0D", "Tp0H", "Tp0L", "Tp0P", "Tp0X", "Tp0b", "Tf04", "Tf09"]

    private struct KeyDataVersion {
        var major: UInt8 = 0, minor: UInt8 = 0, build: UInt8 = 0, reserved: UInt8 = 0
        var release: UInt16 = 0
    }

    private struct KeyDataLimit {
        var version: UInt16 = 0, length: UInt16 = 0
        var cpuPLimit: UInt32 = 0, gpuPLimit: UInt32 = 0, memPLimit: UInt32 = 0
    }

    /// 12 bytes like the C struct (explicit tail padding keeps the outer layout identical).
    private struct KeyInfo {
        var dataSize: UInt32 = 0
        var dataType: UInt32 = 0
        var dataAttributes: UInt8 = 0
        var pad0: UInt8 = 0, pad1: UInt8 = 0, pad2: UInt8 = 0
    }

    private typealias Bytes32 = (UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8,
                                 UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8,
                                 UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8,
                                 UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8)

    private struct KeyData {
        var key: UInt32 = 0
        var vers = KeyDataVersion()
        var pLimitData = KeyDataLimit()
        var keyInfo = KeyInfo()
        var result: UInt8 = 0
        var status: UInt8 = 0
        var data8: UInt8 = 0
        var data32: UInt32 = 0
        var bytes: Bytes32 = (0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                              0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
    }

    private var connection: io_connect_t = 0
    private var workingKey: String?

    init() {
        let service = IOServiceGetMatchingService(kIOMainPortDefault, IOServiceMatching("AppleSMC"))
        guard service != 0 else { return }
        defer { IOObjectRelease(service) }
        var conn: io_connect_t = 0
        if IOServiceOpen(service, mach_task_self_, 0, &conn) == KERN_SUCCESS { connection = conn }
    }

    deinit {
        if connection != 0 { IOServiceClose(connection) }
    }

    func read(keys: [String]) -> Double? {
        guard connection != 0 else { return nil }
        if let key = workingKey, let value = value(for: key) { return value }
        for key in keys {
            if let value = value(for: key) {
                workingKey = key
                return value
            }
        }
        return nil
    }

    private func fourCC(_ s: String) -> UInt32 {
        s.utf8.prefix(4).reduce(UInt32(0)) { ($0 << 8) | UInt32($1) }
    }

    private func call(_ input: inout KeyData) -> KeyData? {
        var output = KeyData()
        var outputSize = MemoryLayout<KeyData>.size
        let result = IOConnectCallStructMethod(connection, 2, &input, MemoryLayout<KeyData>.size, &output, &outputSize)
        guard result == KERN_SUCCESS, output.result == 0 else { return nil }
        return output
    }

    private func value(for key: String) -> Double? {
        var input = KeyData()
        input.key = fourCC(key)
        input.data8 = 9   // kSMCGetKeyInfo
        guard let info = call(&input), info.keyInfo.dataSize > 0 else { return nil }
        input.keyInfo.dataSize = info.keyInfo.dataSize
        input.data8 = 5   // kSMCReadKey
        guard let out = call(&input) else { return nil }
        let b = out.bytes
        let type = info.keyInfo.dataType
        var celsius: Double?
        if type == fourCC("sp78") {
            celsius = Double(Int16(bitPattern: UInt16(b.0) << 8 | UInt16(b.1))) / 256.0
        } else if type == fourCC("flt ") {
            let bits = UInt32(b.0) | UInt32(b.1) << 8 | UInt32(b.2) << 16 | UInt32(b.3) << 24
            celsius = Double(Float(bitPattern: bits))
        }
        guard let c = celsius, c > 5, c < 125 else { return nil }
        return c
    }
}
