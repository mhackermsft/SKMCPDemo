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

await using IMcpClient mcpClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
{
    Name = "MCPServer",
    // Point the client to the MCPServer server executable
    Command = Path.Combine("..", "..", "..", "..", "MCPServer", "bin", "Debug", "net9.0", "MCPServer.exe")
}));

IList<McpClientTool> tools = await mcpClient.ListToolsAsync();

IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
kernelBuilder.Plugins.AddFromFunctions("Tools", tools.Select(mcpClientTool => mcpClientTool.AsKernelFunction()));
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

// Execute a prompt using the MCP tools. The AI model will automatically call the appropriate MCP tools to answer the prompt.
var prompt = "What is the time in EST and weather in Detroit?";
var result = await kernel.InvokePromptAsync(prompt, new(executionSettings));
Console.WriteLine(result);