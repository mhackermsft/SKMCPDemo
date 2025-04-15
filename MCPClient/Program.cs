using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
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
IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

await using IMcpClient localMcpClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
{
    Name = "MCPServer",
    // Point the client to the MCPServer server executable
    Command = Path.Combine("..", "..", "..", "..", "MCPServer", "bin", "Debug", "net9.0", "MCPServer.exe")
}));

IList<McpClientTool> localTools = await localMcpClient.ListToolsAsync();
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
kernelBuilder.Plugins.AddFromFunctions("LocalTools", localTools.Select(mcpClientTool => mcpClientTool.AsKernelFunction()));

List<IMcpClient> remoteMcpClients = new List<IMcpClient>();

//Get the list of MCPServers from appsettings.json and create a remote MCP client for each server
var mcpServers = configuration.GetSection("MCPServers").GetChildren();
foreach (var mcpServer in mcpServers)
{
    //Get the server name, version and URL from mcpServer
    string serverName = mcpServer["Name"] ?? string.Empty;
    string serverVersion = mcpServer["Version"] ?? string.Empty;
    string serverUrl = mcpServer["Url"] ?? string.Empty;

    IMcpClient remoteMcpClient = await McpClientFactory.CreateAsync(new SseClientTransport(transportOptions: new SseClientTransportOptions() { Endpoint = new Uri($"{serverUrl}/sse") }), new McpClientOptions() { ClientInfo = new() { Name = serverName, Version = serverVersion } });
    IList<McpClientTool> remoteTools = await remoteMcpClient.ListToolsAsync();
    kernelBuilder.Plugins.AddFromFunctions($"{serverName}", remoteTools.Select(mcpClientTool => mcpClientTool.AsKernelFunction()));
    remoteMcpClients.Add(remoteMcpClient);
}

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

//cleanup remoteMcpClients
foreach (var remoteMcpClient in remoteMcpClients)
{
    await remoteMcpClient.DisposeAsync();
}

Console.ReadLine();