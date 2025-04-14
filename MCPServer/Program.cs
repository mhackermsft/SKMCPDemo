using MCPServer.KernelFunctions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using MCPServer;


var builder = Host.CreateEmptyApplicationBuilder(settings: null);

IKernelBuilder kernelBuilder = builder.Services.AddKernel();
kernelBuilder.Plugins.AddFromType<DateTimeUtils>();
kernelBuilder.Plugins.AddFromType<CurrentWeather>();
builder.Services
.AddMcpServer()
.WithStdioServerTransport()
.WithTools();

await builder.Build().RunAsync();
