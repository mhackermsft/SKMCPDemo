using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace MCPServerFunction
{
    public class MCPServer
    {
        private readonly ILogger<MCPServer> _logger;

        public MCPServer(ILogger<MCPServer> logger)
        {
            _logger = logger;
        }

        [Function(nameof(GetDateTimeUTCString))]
        public string GetDateTimeUTCString([McpToolTrigger("GetDateTimeUTCString","Get the current date and time in UTC string format")] ToolInvocationContext context)
        {
            _logger.LogInformation("GetDateTimeUTCString called");
            return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
