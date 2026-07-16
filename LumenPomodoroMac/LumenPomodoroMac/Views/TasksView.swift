import SwiftUI

struct TasksView: View {
    @ObservedObject var viewModel: AppViewModel
    @State private var newTaskName = ""
    @State private var newCategory = "考研"
    @State private var newColor = "#3B82F6"

    private let palette = ["#3B82F6", "#10B981", "#F59E0B", "#8B5CF6", "#EF4444", "#EC4899"]

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("任务管理")
                .font(.title2.bold())

            List {
                ForEach(viewModel.tasks) { task in
                    HStack {
                        Circle().fill(Color(hex: task.color)).frame(width: 10, height: 10)
                        VStack(alignment: .leading) {
                            Text(task.name).font(.headline)
                            Text(task.category).font(.caption).foregroundStyle(.secondary)
                        }
                        Spacer()
                        if viewModel.selectedTask?.id == task.id {
                            Image(systemName: "checkmark.circle.fill")
                                .foregroundStyle(Color.accentColor)
                        }
                    }
                    .contentShape(Rectangle())
                    .onTapGesture { viewModel.selectTask(task) }
                    .contextMenu {
                        Button("设为当前任务") { viewModel.selectTask(task) }
                        Button("删除", role: .destructive) { viewModel.deleteTask(task) }
                    }
                }
            }

            GroupBox("添加任务") {
                VStack(alignment: .leading, spacing: 10) {
                    TextField("任务名称", text: $newTaskName)
                    TextField("分类", text: $newCategory)
                    HStack {
                        Text("颜色")
                        ForEach(palette, id: \.self) { color in
                            Circle()
                                .fill(Color(hex: color))
                                .frame(width: 18, height: 18)
                                .overlay(
                                    Circle().stroke(Color.primary, lineWidth: newColor == color ? 2 : 0)
                                )
                                .onTapGesture { newColor = color }
                        }
                    }
                    Button("添加") {
                        let name = newTaskName.trimmingCharacters(in: .whitespacesAndNewlines)
                        guard !name.isEmpty else { return }
                        viewModel.addTask(name: name, category: newCategory, color: newColor)
                        newTaskName = ""
                    }
                    .disabled(newTaskName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
                .padding(4)
            }
        }
        .padding(24)
    }
}
