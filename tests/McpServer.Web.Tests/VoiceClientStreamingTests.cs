using System.Net;
using System.Net.Http;
using McpServer.Client;
using McpServer.Client.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace McpServer.Web.Tests;

public sealed class VoiceClientStreamingTests
{
    [Fact]
    public async Task SubmitTurnStreamingAsync_YieldsChunkBeforeStreamCompletes()
    {
        var firstChunkWritten = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueStream = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestMethod = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestPath = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var acceptHeader = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        await using var app = builder.Build();
        app.MapPost("/mcpserver/voice/session/{sessionId}/turn/stream", async context =>
        {
            requestMethod.TrySetResult(context.Request.Method);
            requestPath.TrySetResult(context.Request.Path.Value ?? string.Empty);
            acceptHeader.TrySetResult(context.Request.Headers.Accept.ToString());

            context.Response.ContentType = "text/event-stream";
            await context.Response.WriteAsync("data: {\"type\":\"chunk\",\"text\":\"hello\"}\n\n");
            await context.Response.Body.FlushAsync();
            firstChunkWritten.TrySetResult(true);
            await continueStream.Task.WaitAsync(TimeSpan.FromSeconds(1));
            await context.Response.WriteAsync("data: {\"type\":\"done\",\"turnId\":\"turn-1\",\"latencyMs\":123}\n\n");
            await context.Response.Body.FlushAsync();
        });

        await app.StartAsync();
        using var httpClient = new HttpClient();
        var client = new VoiceClient(httpClient, new McpServerClientOptions
        {
            BaseUrl = new Uri(app.Urls.Single()),
            ApiKey = "test-api-key",
            WorkspacePath = @"E:\repo"
        });

        await using var enumerator = client.SubmitTurnStreamingAsync(
            "session-123",
            new VoiceTurnRequest
            {
                UserTranscriptText = "hello",
                Language = "en-US",
                ClientTimestampUtc = "2026-03-18T00:00:00Z"
            }).GetAsyncEnumerator();

        var firstMoveTask = enumerator.MoveNextAsync().AsTask();
        await firstChunkWritten.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var completed = await Task.WhenAny(firstMoveTask, Task.Delay(500)) == firstMoveTask;

        Assert.True(completed);
        Assert.True(await firstMoveTask);
        Assert.Equal("chunk", enumerator.Current.Type);
        Assert.Equal("hello", enumerator.Current.Text);
        Assert.Equal("POST", await requestMethod.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal("/mcpserver/voice/session/session-123/turn/stream", await requestPath.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal("text/event-stream", await acceptHeader.Task.WaitAsync(TimeSpan.FromSeconds(1)));

        continueStream.TrySetResult(true);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("done", enumerator.Current.Type);
        await app.StopAsync();
    }
}
