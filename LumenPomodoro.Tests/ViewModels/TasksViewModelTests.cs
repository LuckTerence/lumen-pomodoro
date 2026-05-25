using LumenPomodoro.Models;
using LumenPomodoro.Services.Abstractions;
using LumenPomodoro.ViewModels;
using Moq;

namespace LumenPomodoro.Tests.ViewModels;

public class TasksViewModelTests
{
    private readonly Mock<IStorageService> _storageMock;
    private readonly TasksViewModel _viewModel;

    public TasksViewModelTests()
    {
        _storageMock = new Mock<IStorageService>();
        _storageMock.Setup(s => s.LoadTasks()).Returns(new List<TaskItem>());
        _viewModel = new TasksViewModel(_storageMock.Object);
    }

    [Fact]
    public void Constructor_InitializesDefaults()
    {
        Assert.NotNull(_viewModel.Tasks);
        Assert.Empty(_viewModel.Tasks);
        Assert.Equal(string.Empty, _viewModel.NewTaskName);
        Assert.Equal("#3B82F6", _viewModel.SelectedColor);
        Assert.Null(_viewModel.EditingTaskId);
    }

    [Fact]
    public void AddTask_WithValidName_AddsToCollection()
    {
        _viewModel.NewTaskName = "数学";
        _viewModel.NewTaskCategory = "理科";
        _viewModel.SelectedColor = "#FF0000";

        _viewModel.AddTask();

        Assert.Single(_viewModel.Tasks);
        Assert.Equal("数学", _viewModel.Tasks[0].Name);
        Assert.Equal("理科", _viewModel.Tasks[0].Category);
        Assert.Equal("#FF0000", _viewModel.Tasks[0].Color);
        Assert.Equal(string.Empty, _viewModel.NewTaskName);
        Assert.Equal(string.Empty, _viewModel.NewTaskCategory);
        _storageMock.Verify(s => s.SaveTasks(It.IsAny<List<TaskItem>>()), Times.Once);
    }

    [Fact]
    public void AddTask_WithEmptyName_DoesNotAdd()
    {
        _viewModel.NewTaskName = "   ";

        _viewModel.AddTask();

        Assert.Empty(_viewModel.Tasks);
        _storageMock.Verify(s => s.SaveTasks(It.IsAny<List<TaskItem>>()), Times.Never);
    }

    [Fact]
    public void DeleteTask_ExistingTask_RemovesFromCollection()
    {
        var task = new TaskItem { Name = "数学" };
        _viewModel.Tasks.Add(task);

        _viewModel.DeleteTask(task.Id);

        Assert.Empty(_viewModel.Tasks);
        _storageMock.Verify(s => s.SaveTasks(It.IsAny<List<TaskItem>>()), Times.Once);
    }

    [Fact]
    public void DeleteTask_NonExistentTask_DoesNothing()
    {
        var task = new TaskItem { Name = "数学" };
        _viewModel.Tasks.Add(task);

        _viewModel.DeleteTask("non-existent-id");

        Assert.Single(_viewModel.Tasks);
    }

    [Fact]
    public void SelectTask_ExistingTask_InvokesEvent()
    {
        var task = new TaskItem { Name = "数学" };
        _viewModel.Tasks.Add(task);
        TaskItem? selectedTask = null;
        _viewModel.TaskSelected += t => selectedTask = t;

        _viewModel.SelectTask(task.Id);

        Assert.Equal(task.Name, selectedTask?.Name);
    }

    [Fact]
    public void SelectTask_NonExistentTask_DoesNotInvokeEvent()
    {
        var invoked = false;
        _viewModel.TaskSelected += _ => invoked = true;

        _viewModel.SelectTask("non-existent-id");

        Assert.False(invoked);
    }

    [Fact]
    public void StartEdit_SetsEditingTaskId()
    {
        var task = new TaskItem { Name = "数学" };
        _viewModel.Tasks.Add(task);

        _viewModel.StartEdit(task);

        Assert.Equal(task.Id, _viewModel.EditingTaskId);
    }

    [Fact]
    public void FinishEdit_WithValidName_UpdatesTask()
    {
        var task = new TaskItem { Name = "数学" };
        _viewModel.Tasks.Add(task);
        _viewModel.StartEdit(task);

        _viewModel.FinishEdit(task.Id, "英语");

        Assert.Equal("英语", task.Name);
        Assert.Null(_viewModel.EditingTaskId);
        _storageMock.Verify(s => s.SaveTasks(It.IsAny<List<TaskItem>>()), Times.Once);
    }

    [Fact]
    public void FinishEdit_WithEmptyName_CancelsEdit()
    {
        var task = new TaskItem { Name = "数学" };
        _viewModel.Tasks.Add(task);
        _viewModel.StartEdit(task);

        _viewModel.FinishEdit(task.Id, "   ");

        Assert.Equal("数学", task.Name);
        Assert.Null(_viewModel.EditingTaskId);
        _storageMock.Verify(s => s.SaveTasks(It.IsAny<List<TaskItem>>()), Times.Never);
    }

    [Fact]
    public void LoadTasks_PopulatesCollection()
    {
        var tasks = new List<TaskItem>
        {
            new() { Name = "数学" },
            new() { Name = "英语" }
        };
        _storageMock.Setup(s => s.LoadTasks()).Returns(tasks);

        _viewModel.LoadTasks();

        Assert.Equal(2, _viewModel.Tasks.Count);
    }

    [Fact]
    public void SelectedColor_InvalidInput_FallsBackToDefault()
    {
        _viewModel.SelectedColor = "invalid";

        Assert.Equal("#6B7280", _viewModel.SelectedColor);
    }

    [Fact]
    public void SelectedColor_ShortHex_ExpandsToFull()
    {
        _viewModel.SelectedColor = "#ABC";

        Assert.Equal("#AABBCC", _viewModel.SelectedColor);
    }

    [Fact]
    public void TasksChanged_InvokedAfterAdd()
    {
        var invoked = false;
        _viewModel.TasksChanged += () => invoked = true;
        _viewModel.NewTaskName = "测试";

        _viewModel.AddTask();

        Assert.True(invoked);
    }
}
