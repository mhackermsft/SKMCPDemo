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
        [McpServerTool, Description("Gets the latest price for a stock symbol.")]
        public static string GetStockQuote(string symbol)
        {
            var random = new Random();
            var amount = random.Next(10000, 60001) / 100.0; // Generate random amount between 100.00 and 600.00
            return $"${amount:F2}"; // Format as USD with two decimal places
        }
    }
}
