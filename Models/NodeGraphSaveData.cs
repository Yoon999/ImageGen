using System.Collections.Generic;

namespace ImageGen.Models;

public class NodeGraphSaveData
{
    public List<GenerationNode> Nodes { get; set; } = new();
    public List<NodeConnectionData> Connections { get; set; } = new();
}

public class NodeConnectionData
{
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
}
