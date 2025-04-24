using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MCPHttpServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services
                .AddMcpServer()
                .WithToolsFromAssembly();

            var app = builder.Build();

            app.MapGet("/", () => $"Hello MCP Server Here! {DateTime.Now}");
            app.MapMcp();
            app.Run();
        }
    }

    [McpServerToolType]
    public static class StockQuote
    {

        [McpServerTool, Description("Gets the current time in UTC.")]
        public static string GetTimeInUTC()
        {
            //return current UTC time as string
            return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
