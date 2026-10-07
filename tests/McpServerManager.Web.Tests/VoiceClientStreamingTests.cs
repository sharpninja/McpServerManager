using System.Net;
using System.Net.Http;
using McpServer.Client;
using McpServer.Client.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace McpServerManager.Web.Tests;

public sealed class VoiceClientStreamingTests
{
    // Ceiling for waits on cross-process signals (Kestrel cold start, first connection).
    // The streaming property itself is proven by ordering, not by wall-clock: the server
    // holds the response open until the test releases it, so a first chunk observed before
    // the release can only have been yielded before the stream completed.
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task SubmitTurnStreamingAsync_YieldsChunkBeforeStreamCompletes()
    {
        var firstChunkWritten = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueStream = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var streamCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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
            await continueStream.Task.WaitAsync(SignalTimeout);
            await context.Response.WriteAsync("data: {\"type\":\"done\",\"turnId\":\"turn-1\",\"latencyMs\":123}\n\n");
            await context.Response.Body.FlushAsync();
            streamCompleted.TrySetResult(true);
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
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
                }).GetAsyncEnumerator(TestContext.Current.CancellationToken);

            try
            {
                var firstMove = enumerator.MoveNextAsync().AsTask();
                await firstChunkWritten.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken);

                Assert.True(await firstMove.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken));
                Assert.False(continueStream.Task.IsCompleted, "the server must still be holding the stream open");
                Assert.False(streamCompleted.Task.IsCompleted, "the first chunk must be yielded before the stream completes");
                Assert.Equal("chunk", enumerator.Current.Type);
                Assert.Equal("hello", enumerator.Current.Text);
                Assert.Equal("POST", await requestMethod.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken));
                Assert.Equal("/mcpserver/voice/session/session-123/turn/stream", await requestPath.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken));
                Assert.Equal("text/event-stream", await acceptHeader.Task.WaitAsync(SignalTimeout, TestContext.Current.CancellationToken));

                continueStream.TrySetResult(true);
                Assert.True(await enumerator.MoveNextAsync().AsTask().WaitAsync(SignalTimeout, TestContext.Current.CancellationToken));
                Assert.Equal("done", enumerator.Current.Type);
            }
            finally
            {
                // Release the server so no MoveNextAsync is pending when the enumerator is disposed;
                // disposing an async iterator mid-MoveNext throws NotSupportedException and would
                // mask the original assertion failure.
                continueStream.TrySetResult(true);
            }
        }
        finally
        {
            await app.StopAsync(CancellationToken.None);
        }
    }
}