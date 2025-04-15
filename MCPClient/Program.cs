using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;

// Load configuration from appsettings.json
IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var azureOpenAIConfig = configuration.GetSection("AzureOpenAI");
string modelId = azureOpenAIConfig["ModelId"]??string.Empty;
string endpoint = azureOpenAIConfig["Endpoint"]?? string.Empty;
string apiKey = azureOpenAIConfig["ApiKey"] ?? string.Empty;

await using IMcpClient localMcpClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
{
    Name = "MCPServer",
    // Point the client to the MCPServer server executable
    Command = Path.Combine("..", "..", "..", "..", "MCPServer", "bin", "Debug", "net9.0", "MCPServer.exe")
}));


await using IMcpClient remoteMcpClient = await McpClientFactory.CreateAsync(new SseClientTransport(transportOptions: new SseClientTransportOptions() { Endpoint = new Uri("http://localhost:4949/sse") }), new McpClientOptions() { ClientInfo = new() { Name="MCPHttpServer", Version="1.0.0.0" } });

IList<McpClientTool> localTools = await localMcpClient.ListToolsAsync();
IList<McpClientTool> remoteTools = await remoteMcpClient.ListToolsAsync();

IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
kernelBuilder.Plugins.AddFromFunctions("LocalTools", localTools.Select(mcpClientTool => mcpClientTool.AsKernelFunction()));
kernelBuilder.Plugins.AddFromFunctions("RemoteTools", remoteTools.Select(mcpClientTool => mcpClientTool.AsKernelFunction()));
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
kernelBuilder.Services.AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey);

Kernel kernel = kernelBuilder.Build();

// Enable automatic function calling
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
AzureOpenAIPromptExecutionSettings executionSettings = new()
{
    Temperature = 0,
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
};
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

// Execute a prompt using the MCP localTools. The AI model will automatically call the appropriate MCP localTools to answer the prompt.
var prompt = "What is the time in EST and weather in Detroit?";
var result = await kernel.InvokePromptAsync(prompt, new(executionSettings));
Console.WriteLine(result);

prompt = "What is the current stock price for MSFT?";
result = await kernel.InvokePromptAsync(prompt, new(executionSettings));
Console.WriteLine(result);

Console.ReadLine();