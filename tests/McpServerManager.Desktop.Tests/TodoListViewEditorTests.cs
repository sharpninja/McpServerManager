using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using AvaloniaEdit;
using McpServerManager.Core.Services;
using McpServerManager.Core.ViewModels;
using McpServerManager.Desktop.Views;
using Xunit;

namespace McpServerManager.Desktop.Tests;

public sealed class TodoListViewEditorTests
{
    [AvaloniaFact]
    public async Task TodoListView_LoadingDifferentTodoText_ResetsEditorCaretAndScroll()
    {
        await EnsureHeadlessAppResourcesAsync();

        var mainVm = new MainWindowViewModel(
            new TestClipboardService(),
            mcpBaseUrl: "http://127.0.0.1:1",
            mcpApiKey: null,
            bearerToken: null,
            systemNotificationService: NoOpSystemNotificationService.Instance)
        {
            SaveWorkspaceKey = _ => { },
            LoadWorkspaceKey = () => null
        };

        var view = new TodoListView { DataContext = mainVm.TodoViewModel };
        var window = new Window
        {
            Width = 960,
            Height = 720,
            Content = view
        };

        try
        {
            window.Show();
            await PumpUiAsync();

            var editor = view.FindControl<TextEditor>("Editor");
            Assert.NotNull(editor);

            editor!.Text = BuildLongTodoText("OLD-TODO");
            editor.CaretOffset = editor.Text.Length;
            editor.ScrollToLine(40);
            await PumpUiAsync();

            mainVm.TodoViewModel.EditorText = BuildLongTodoText("NEW-TODO");
            await PumpUiAsync();

            Assert.Equal(0, editor.CaretOffset);
            Assert.Equal(1, editor.TextArea.Caret.Line);
            Assert.Equal(1, editor.TextArea.Caret.Column);
            Assert.True(editor.TextArea.TextView.ScrollOffset.Y <= 1.0);
        }
        finally
        {
            window.Close();
        }
    }

    private static string BuildLongTodoText(string id)
        => "# " + id + Environment.NewLine + string.Join(Environment.NewLine, Enumerable.Range(1, 60).Select(i => "Line " + i));

    private static Task EnsureHeadlessAppResourcesAsync()
    {
        return Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current == null)
            {
                AppBuilder.Configure<TestApp>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                    .SetupWithoutStarting();
            }

            if (!Application.Current!.Styles.OfType<FluentTheme>().Any())
            {
                Application.Current.Styles.Insert(0, new FluentTheme());
            }
        }).GetTask();
    }

    private static async Task PumpUiAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
    }

    private sealed class TestClipboardService : IClipboardService
    {
        public Task SetTextAsync(string text) => Task.CompletedTask;
    }

    private sealed class TestApp : Application
    {
    }
}
