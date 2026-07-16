import SwiftUI

/// 全屏休息遮罩：大号倒计时 +（非严格模式下）结束按钮
struct FullscreenBreakView: View {
    @ObservedObject var viewModel: AppViewModel

    var body: some View {
        ZStack {
            Color.black.opacity(0.88)
                .ignoresSafeArea()

            VStack(spacing: 20) {
                Text(viewModel.fullscreenBreakTitle)
                    .font(.title2.weight(.semibold))
                    .foregroundStyle(.white.opacity(0.9))

                Text("站起来走走，看看远处")
                    .font(.subheadline)
                    .foregroundStyle(.white.opacity(0.55))

                Text(viewModel.remainingTime)
                    .font(.system(size: 72, weight: .light, design: .monospaced))
                    .foregroundStyle(.white)
                    .padding(.vertical, 24)

                if viewModel.settings.effectiveAllowEndBreakEarly {
                    Button("结束休息") {
                        viewModel.skipBreak()
                    }
                    .buttonStyle(.borderedProminent)
                    .controlSize(.large)
                } else {
                    Text("严格模式：请等待休息自然结束")
                        .font(.caption)
                        .foregroundStyle(.white.opacity(0.45))
                }
            }
            .padding(40)
        }
    }
}
