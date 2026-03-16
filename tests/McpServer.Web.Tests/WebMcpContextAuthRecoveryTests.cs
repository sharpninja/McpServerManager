using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using McpServer.Client;
using McpServer.UI.Core.Messages;
using McpServer.UI.Core.ViewModels;
using McpServer.Web.Adapters;
using McpServer.Web.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace McpServer.Web.Tests;

public sealed class WebMcpContextAuthRecoveryTests
{
    [Fact]
    public async Task TodoAdapter_RefreshesStaleApiKey_AfterUnauthorized()
    {
        var config = BuildConfig(apiKey: "stale-api-key");
        var workspaceContext = new WorkspaceContextViewModel();
        var bearerTokenAccessor = new BearerTokenAccessor(BuildAccessor(context: null));
        using var handler = new RefreshingTodoHandler();
        var webContext = new WebMcpContext(
            config,
            workspaceContext,
            bearerTokenAccessor,
            clientFactory: options => CreateClient(handler, options));

        var adapter = new TodoApiClientAdapter(webContext);

        var result = await adapter.ListTodosAsync(new ListTodosQuery { Done = false });

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("TODO-001", result.Items[0].Id);
        Assert.Equal(1, handler.ApiKeyRequestCount);
        Assert.Equal(["stale-api-key", "fresh-api-key"], handler.TodoApiKeys);

        var client = await webContext.GetRequiredActiveWorkspaceApiClientAsync();
        Assert.Equal("fresh-api-key", client.ApiKey);
    }

    [Fact]
    public async Task TodoAdapter_UsesWorkspaceMarkerKey_BeforeFallingBackToDefaultApiKey()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"webmcp-marker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);

        try
        {
            File.WriteAllText(
                Path.Combine(workspacePath, "AGENTS-README-FIRST.yaml"),
                """
                baseUrl: http://localhost:7147
                apiKey: marker-api-key
                workspacePath: E:\github\RequestTracker
                """);

            var config = BuildConfig(apiKey: "stale-api-key", workspacePath: workspacePath);
            var workspaceContext = new WorkspaceContextViewModel();
            var bearerTokenAccessor = new BearerTokenAccessor(BuildAccessor(context: null));
            using var handler = new MarkerPreferredTodoHandler();
            var webContext = new WebMcpContext(
                config,
                workspaceContext,
                bearerTokenAccessor,
                clientFactory: options => CreateClient(handler, options));

            var adapter = new TodoApiClientAdapter(webContext);

            var result = await adapter.ListTodosAsync(new ListTodosQuery { Done = false });

            Assert.Equal(1, result.TotalCount);
            Assert.Single(result.Items);
            Assert.Equal("TODO-002", result.Items[0].Id);
            Assert.Equal(["marker-api-key"], handler.TodoApiKeys);
            Assert.Equal(0, handler.ApiKeyRequestCount);
        }
        finally
        {
            if (Directory.Exists(workspacePath))
                Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public async Task TodoAdapter_DoesNotFallbackToApiKey_WhenBearerTokenUnauthorized()
    {
        var config = BuildConfig(apiKey: null);
        var workspaceContext = new WorkspaceContextViewModel();
        var bearerTokenAccessor = new BearerTokenAccessor(BuildAccessor(BuildAuthenticatedContext("user-access-token")));
        using var handler = new BearerUnauthorizedTodoHandler();
        var webContext = new WebMcpContext(
            config,
            workspaceContext,
            bearerTokenAccessor,
            clientFactory: options => CreateClient(handler, options));

        var adapter = new TodoApiClientAdapter(webContext);

        var exception = await Assert.ThrowsAsync<McpUnauthorizedException>(
            () => adapter.ListTodosAsync(new ListTodosQuery { Done = false }));

        Assert.Contains("Invalid or missing bearer token", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, handler.ApiKeyRequestCount);
        Assert.Single(handler.AuthorizationHeaders);
        Assert.Equal("Bearer user-access-token", handler.AuthorizationHeaders[0]);
    }

    private static IConfiguration BuildConfig(string? apiKey, string? workspacePath = "E:/ws/default")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpServer:BaseUrl"] = "http://localhost:7147",
                ["McpServer:ApiKey"] = apiKey,
                ["McpServer:WorkspacePath"] = workspacePath,
            })
            .Build();
    }

    private static IHttpContextAccessor BuildAccessor(HttpContext? context)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        return accessor;
    }

    private static DefaultHttpContext BuildAuthenticatedContext(string accessToken)
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "Test");
        context.User = new ClaimsPrincipal(identity);

        var props = new AuthenticationProperties();
        props.StoreTokens([new AuthenticationToken { Name = "access_token", Value = accessToken }]);

        var authResult = AuthenticateResult.Success(
            new AuthenticationTicket(context.User, props, "Test"));

        var authService = Substitute.For<IAuthenticationService>();
        authService
            .AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string?>())
            .Returns(authResult);

        var services = new ServiceCollection();
        services.AddSingleton(authService);
        context.RequestServices = services.BuildServiceProvider();

        return context;
    }

    private static McpServerClient CreateClient(HttpMessageHandler handler, McpServerClientOptions options)
    {
        var httpClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = options.BaseUrl,
            Timeout = options.Timeout,
        };
        return McpServerClientFactory.Create(httpClient, options);
    }

    private sealed class RefreshingTodoHandler : HttpMessageHandler
    {
        public List<string?> TodoApiKeys { get; } = [];
        public int ApiKeyRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Equals("/mcpserver/todo", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.TryGetValues("X-Api-Key", out var keyValues);
                var apiKey = keyValues?.SingleOrDefault();
                TodoApiKeys.Add(apiKey);

                if (string.Equals(apiKey, "stale-api-key", StringComparison.Ordinal))
                    return Task.FromResult(JsonResponse(HttpStatusCode.Unauthorized, "{\"message\":\"Invalid or missing API key.\"}"));

                if (string.Equals(apiKey, "fresh-api-key", StringComparison.Ordinal))
                {
                    return Task.FromResult(JsonResponse(
                        HttpStatusCode.OK,
                        """
                        {
                          "items": [
                            {
                              "id": "TODO-001",
                              "title": "Recovered",
                              "section": "Architecture",
                              "priority": "high",
                              "done": false,
                              "estimate": "1h"
                            }
                          ],
                          "totalCount": 1
                        }
                        """));
                }

                return Task.FromResult(JsonResponse(HttpStatusCode.Unauthorized, "{\"message\":\"Unexpected API key.\"}"));
            }

            if (path.Equals("/api-key", StringComparison.OrdinalIgnoreCase))
            {
                ApiKeyRequestCount++;
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"apiKey\":\"fresh-api-key\"}"));
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.NotFound, "{\"message\":\"Not found.\"}"));
        }
    }

    private sealed class BearerUnauthorizedTodoHandler : HttpMessageHandler
    {
        public List<string?> AuthorizationHeaders { get; } = [];
        public int ApiKeyRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Equals("/mcpserver/todo", StringComparison.OrdinalIgnoreCase))
            {
                AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());
                return Task.FromResult(JsonResponse(HttpStatusCode.Unauthorized, "{\"message\":\"Invalid or missing bearer token.\"}"));
            }

            if (path.Equals("/api-key", StringComparison.OrdinalIgnoreCase))
            {
                ApiKeyRequestCount++;
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"apiKey\":\"unexpected\"}"));
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.NotFound, "{\"message\":\"Not found.\"}"));
        }
    }

    private sealed class MarkerPreferredTodoHandler : HttpMessageHandler
    {
        public List<string?> TodoApiKeys { get; } = [];
        public int ApiKeyRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Equals("/mcpserver/todo", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.TryGetValues("X-Api-Key", out var keyValues);
                var apiKey = keyValues?.SingleOrDefault();
                TodoApiKeys.Add(apiKey);

                if (string.Equals(apiKey, "marker-api-key", StringComparison.Ordinal))
                {
                    return Task.FromResult(JsonResponse(
                        HttpStatusCode.OK,
                        """
                        {
                          "items": [
                            {
                              "id": "TODO-002",
                              "title": "Marker key",
                              "section": "Runtime",
                              "priority": "high",
                              "done": false,
                              "estimate": "30m"
                            }
                          ],
                          "totalCount": 1
                        }
                        """));
                }

                return Task.FromResult(JsonResponse(HttpStatusCode.Unauthorized, "{\"message\":\"Invalid or missing API key.\"}"));
            }

            if (path.Equals("/api-key", StringComparison.OrdinalIgnoreCase))
            {
                ApiKeyRequestCount++;
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"apiKey\":\"default-api-key\"}"));
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.NotFound, "{\"message\":\"Not found.\"}"));
        }
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }
}
