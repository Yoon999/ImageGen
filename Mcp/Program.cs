using ImageGen.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var api = new NovelAiApiService();
var tools = new ImageGen.Mcp.GenerationTools(AppContext.BaseDirectory, api.GenerateImageAsync);
var options = new McpServerOptions
{
    ServerInfo = new Implementation { Name = "ImageGen", Version = "1.0" },
    ToolCollection = [McpServerTool.Create(tools.Generate, new()
    {
        Name = "generate_t2i",
        Description = "Generate one NovelAI image with the user's saved settings. Use __name__ for macros listed at imagegen://macros. Returns the local PNG path and seed."
    })],
    ResourceCollection = [McpServerResource.Create(tools.ListMacros, new()
    {
        Name = "Prompt macros", UriTemplate = "imagegen://macros", MimeType = "application/json"
    })]
};
await using var server = McpServer.Create(new StdioServerTransport("ImageGen"), options);
await server.RunAsync();
