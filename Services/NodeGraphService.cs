using System.IO;
using System.Text.Json;
using ImageGen.Models;

namespace ImageGen.Services;

public class NodeGraphService
{
    public NodeGraphSaveData CreateSaveData(IEnumerable<GenerationNode> nodes)
    {
        var nodeList = nodes.ToList();
        var saveData = new NodeGraphSaveData
        {
            Nodes = nodeList
        };

        foreach (var node in nodeList)
        {
            if (node.NextNode != null)
            {
                saveData.Connections.Add(new NodeConnectionData
                {
                    SourceId = node.Id,
                    TargetId = node.NextNode.Id
                });
            }

            foreach (var target in node.NextNodes)
            {
                saveData.Connections.Add(new NodeConnectionData
                {
                    SourceId = node.Id,
                    TargetId = target.Id
                });
            }

            if (node.Type == NodeType.BaseConcat)
            {
                node.InputOrder = node.InputNodes.Select(n => n.Id).ToList();
            }
        }

        return saveData;
    }

    public NodeGraphSaveData CreateClipboardData(IEnumerable<GenerationNode> selectedNodes)
    {
        var nodeList = selectedNodes
            .Where(n => n.Type is not NodeType.Begin and not NodeType.End)
            .ToList();

        var saveData = new NodeGraphSaveData
        {
            Nodes = nodeList
        };

        foreach (var node in nodeList)
        {
            if (node.NextNode != null && nodeList.Contains(node.NextNode))
            {
                saveData.Connections.Add(new NodeConnectionData { SourceId = node.Id, TargetId = node.NextNode.Id });
            }

            foreach (var target in node.NextNodes.Where(nodeList.Contains))
            {
                saveData.Connections.Add(new NodeConnectionData { SourceId = node.Id, TargetId = target.Id });
            }
        }

        return saveData;
    }

    public GenerationNode CloneNode(GenerationNode node, double offset = 20)
    {
        return new GenerationNode
        {
            UiX = node.UiX + offset,
            UiY = node.UiY + offset,
            Type = node.Type,
            Title = node.Title,
            BasePrompt = node.BasePrompt,
            NegativePrompt = node.NegativePrompt,
            PresetName = node.PresetName,
            CharX = node.CharX,
            CharY = node.CharY,
            Width = node.Width,
            Height = node.Height,
            IsCollapsed = node.IsCollapsed,
            IsBypassed = node.IsBypassed
        };
    }

    public Dictionary<string, GenerationNode> CloneNodes(IEnumerable<GenerationNode> nodes, double offset = 20)
    {
        return nodes.ToDictionary(node => node.Id, node => CloneNode(node, offset));
    }

    public void RestoreConnections(IList<GenerationNode> nodes, IEnumerable<NodeConnectionData> connections)
    {
        foreach (var conn in connections)
        {
            var source = nodes.FirstOrDefault(n => n.Id == conn.SourceId);
            var target = nodes.FirstOrDefault(n => n.Id == conn.TargetId);

            if (source == null || target == null)
            {
                continue;
            }

            Connect(source, target);
        }
    }

    public async Task<NodeGraphSaveData?> LoadFromFileAsync(string fileName)
    {
        var json = await File.ReadAllTextAsync(fileName);
        return JsonSerializer.Deserialize<NodeGraphSaveData>(json);
    }

    public async Task SaveToFileAsync(string fileName, NodeGraphSaveData saveData)
    {
        var json = JsonSerializer.Serialize(saveData, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(fileName, json);
    }

    private static void Connect(GenerationNode source, GenerationNode target)
    {
        if (source.Type is NodeType.Character or NodeType.Base or NodeType.BaseConcat)
        {
            source.NextNodes.Add(target);
            return;
        }

        source.NextNode = target;
    }
}
