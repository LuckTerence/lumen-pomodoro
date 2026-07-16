import SwiftUI

struct ArcProgressView: View {
    var progress: Double
    var lineWidth: CGFloat = 4
    var accentColor: Color = .accentColor
    var trackColor: Color = Color.white.opacity(0.08)

    var body: some View {
        ZStack {
            Circle()
                .stroke(trackColor, lineWidth: lineWidth)
            Circle()
                .trim(from: 0, to: max(0, min(1, progress)))
                .stroke(
                    accentColor,
                    style: StrokeStyle(lineWidth: lineWidth, lineCap: .round)
                )
                .rotationEffect(.degrees(-90))
                .animation(.easeInOut(duration: 0.25), value: progress)
        }
    }
}
