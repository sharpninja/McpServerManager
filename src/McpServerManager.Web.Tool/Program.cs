namespace McpServerManager.Web.Tool;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return WebToolLauncher.Run(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"mcp-web failed to start: {exception.Message}");
            return 1;
        }
    }
}
