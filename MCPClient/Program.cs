using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using ModelContextProtocol.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

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

// Add logging services
kernelBuilder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.SetMinimumLevel(LogLevel.Trace);
});


List<IMcpClient> mcpClients = new List<IMcpClient>();

//Get the list of MCPServers from appsettings.json and create a remote MCP client for each server
var mcpServers = configuration.GetSection("MCPServers").GetChildren();
foreach (var mcpServer in mcpServers)
{
    //Get the server name, version and URL from mcpServer
    string type = mcpServer["Type"] ?? string.Empty;
    string serverName = mcpServer["Name"] ?? string.Empty;
    string serverVersion = mcpServer["Version"] ?? string.Empty;
    string serverEndpoint = mcpServer["Endpoint"] ?? string.Empty;

    // Get the Args array and convert it to a List<string>
    List<string> mcpArgs = mcpServer.GetSection("Args").Get<List<string>>() ?? new List<string>();

    // Get the Headers section and convert it to a Dictionary<string, string>
    Dictionary<string, string> headers = mcpServer.GetSection("Headers").Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();

    //Get the Environment variable section and convert it to a Dictionary<string, string>
    Dictionary<string, string> env = mcpServer.GetSection("Env").Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();

    if (type.ToLower() == "stdio")
    {
        IMcpClient localMCPClient = McpClientFactory.CreateAsync(new StdioClientTransport(new()
        {
            Name = serverName,
            // Point the client to the MCPServer server executable
            Command = serverEndpoint,
            Arguments = mcpArgs,
            EnvironmentVariables = env,
        })).GetAwaiter().GetResult();

        IList<McpClientTool> localTools = localMCPClient.ListToolsAsync().GetAwaiter().GetResult();
        kernelBuilder.Plugins.AddFromFunctions($"{serverName}", localTools.Select(mcpClientTool => mcpClientTool.AsKernelFunction()));
        mcpClients.Add(localMCPClient);
    }
    else if (type.ToLower()== "sse")
    {
        //Configure HTTP Headers
        HttpClient httpClient = new HttpClient();
        if (headers.Count > 0)
        {
            foreach (var header in headers)
            {
                httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        IMcpClient remoteMcpClient = McpClientFactory.CreateAsync(new SseClientTransport(httpClient: httpClient, transportOptions: new SseClientTransportOptions() { Endpoint = new Uri($"{serverEndpoint}/sse") }), new McpClientOptions() { ClientInfo = new() { Name = serverName, Version = serverVersion } }).GetAwaiter().GetResult();
        IList<McpClientTool> remoteTools = remoteMcpClient.ListToolsAsync().GetAwaiter().GetResult();
        kernelBuilder.Plugins.AddFromFunctions($"{serverName}", remoteTools.Select(mcpClientTool => mcpClientTool.AsKernelFunction()));
        mcpClients.Add(remoteMcpClient);
    }
    

}

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
var prompt = "Navigate to Bing.com and enter 'Semantic Kernel Documentation' into the search box and then press enter.";
var result = await kernel.InvokePromptAsync(prompt, new(executionSettings));
Console.WriteLine(result);

//cleanup mcpClients
foreach (var remoteMcpClient in mcpClients)
{
    await remoteMcpClient.DisposeAsync();
}

Console.ReadLine();