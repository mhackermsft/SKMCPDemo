# Model Context Protocol Demo
## using Semantic Kernel

The is a simple Model Context Protocol demo showing how you can quickly build MCP clients and servers using the ModelContextProtocol Nuget package and Semantic Kernel.

The MCPClient is a C# console application that utilizes Azure Open AI and MCP Servers to respond to questions. Configuration for the client is stored in the appsettings.json file described below:

```json
{
  "AzureOpenAI": {
    "ModelId": "[Azure OpenAI Deployment Name]",
    "Endpoint": "[Azure OpenAI Endpoint]",
    "ApiKey": "[Azure OpenAI API Key]"
  },
  "MCPServers": [
    {
      "Type": "stdio",
      "Name": "playwright",
      "Version": "1.0.0.0",
      "Endpoint": "npx",
      "Args": [
        "-y",
        "@executeautomation/playwright-mcp-server"
      ],
      "Headers": {
        "x-functions-key": ""
      },
      "Env": {
      }
    }
  ]
}
```
The MCPClient will attempt to start and use any of the MCPServers defined in the appsettings.json. It supports both stdio and sse servers.

The MCPServer is a C# Console application that implements a MCP server that simulates tools for getting the current time and weather. This uses the STDIO transport. Both the client and server must be running on the same machine.

The MCPHttpServer is a C# web application that implements a MCP server that simulates tools for getting the current time and stock price. This uses the HTTPs transport. This enables a remote MCP client to access the tools. You will need to run this application prior to launching the MCPClient. Ensure that the URL for the MCPHttpServer is listed correctly in the MCPClient's appsettings.json file.

