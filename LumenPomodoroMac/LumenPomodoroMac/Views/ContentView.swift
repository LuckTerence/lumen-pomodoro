import SwiftUI

struct ContentView: View {
    @ObservedObject var viewModel: AppViewModel
    @Environment(\.scenePhase) private var scenePhase

    var body: some View {
        TabView {
            TimerView(viewModel: viewModel)
                .tabItem { Label("计时", systemImage: "timer") }

            TasksView(viewModel: viewModel)
                .tabItem { Label("任务", systemImage: "checklist") }

            StatsView(viewModel: viewModel)
                .tabItem { Label("统计", systemImage: "chart.bar") }

            SettingsView(viewModel: viewModel)
                .tabItem { Label("设置", systemImage: "gearshape") }
        }
        .frame(minWidth: 480, minHeight: 640)
        .overlay {
            // macOS 无 fullScreenCover API，用窗口级遮罩盖住全部 Tab
            if viewModel.showFullscreenBreak {
                FullscreenBreakView(viewModel: viewModel)
                    .transition(.opacity)
            }
        }
        .animation(.easeInOut(duration: 0.2), value: viewModel.showFullscreenBreak)
        .onChange(of: scenePhase) { _, phase in
            switch phase {
            case .active: viewModel.onAppBecameActive()
            case .inactive, .background: viewModel.onAppResignedActive()
            @unknown default: break
            }
        }
        .alert("摄像头错误", isPresented: Binding(
            get: { viewModel.cameraErrorMessage != nil },
            set: { if !$0 { viewModel.cameraErrorMessage = nil } }
        )) {
            Button("好") { viewModel.cameraErrorMessage = nil }
        } message: {
            Text(viewModel.cameraErrorMessage ?? "")
        }
        .sheet(isPresented: $viewModel.showOnboarding) {
            OnboardingView(viewModel: viewModel)
                .interactiveDismissDisabled(true)
        }
        .sheet(isPresented: $viewModel.showPrivacySheet) {
            PrivacyNoticeSheet(viewModel: viewModel)
        }
        .sheet(isPresented: Binding(
            get: { viewModel.showDailyReport && viewModel.settings.hasCompletedOnboarding },
            set: { viewModel.showDailyReport = $0 }
        )) {
            if let report = viewModel.dailyReport {
                DailyReportSheet(report: report) {
                    viewModel.dismissDailyReport()
                }
            }
        }
    }
}

private struct PrivacyNoticeSheet: View {
    @ObservedObject var viewModel: AppViewModel
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("摄像头隐私说明")
                .font(.title2.bold())

            Text("""
            Lumen Pomodoro 不把摄像头当作采集设备使用，只把它当作本地硬件提醒信号。

            • 不保存照片
            • 不录制视频
            • 不上传摄像头数据
            • 不展示摄像头画面
            • 仅在本机调用摄像头硬件
            • 连续运行超过 30 分钟会自动保护释放
            """)

            HStack {
                Spacer()
                Button("我已了解") {
                    viewModel.confirmPrivacyNotice()
                    dismiss()
                }
                .buttonStyle(.borderedProminent)
            }
        }
        .padding(24)
        .frame(width: 420)
    }
}
