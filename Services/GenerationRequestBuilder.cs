using System.Text.Json;
using ImageGen.Models;
using ImageGen.Models.Api;

namespace ImageGen.Services;

public static class GenerationRequestBuilder
{
    public static GenerationRequest BuildStandaloneRequest(
        GenerationRequest source,
        string prompt,
        string negativePrompt,
        IEnumerable<CharacterPromptSettings> characterPrompts,
        bool useRandomSeed)
    {
        var request = Clone(source);

        if (IsV4(request))
        {
            var v4Prompt = new V4ConditionInput
            {
                Caption = new V4ExternalCaption
                {
                    BaseCaption = prompt
                },
                UseCoords = true,
                UseOrder = true
            };

            var v4NegativePrompt = new V4ConditionInput
            {
                Caption = new V4ExternalCaption
                {
                    BaseCaption = negativePrompt
                },
                UseCoords = false,
                UseOrder = false
            };

            foreach (var characterPrompt in characterPrompts)
            {
                if (string.IsNullOrWhiteSpace(characterPrompt.Prompt))
                {
                    continue;
                }

                v4Prompt.Caption.CharCaptions.Add(CreateCharacterCaption(
                    characterPrompt.Prompt,
                    characterPrompt.X,
                    characterPrompt.Y));

                v4NegativePrompt.Caption.CharCaptions.Add(CreateCharacterCaption(
                    characterPrompt.NegativePrompt,
                    characterPrompt.X,
                    characterPrompt.Y));
            }

            request.parameters.V4Prompt = v4Prompt;
            request.parameters.V4NegativePrompt = v4NegativePrompt;
            request.parameters.noise_schedule = "karras";
            request.parameters.prefer_brownian = true;
            request.input = prompt;
        }
        else
        {
            request.input = prompt;
            request.parameters.V4Prompt = null;
            request.parameters.V4NegativePrompt = null;
        }

        ApplyRandomSeed(request, useRandomSeed);
        return request;
    }

    public static GenerationRequest BuildNodeRequest(
        GenerationRequest source,
        GenerationNode currentNode,
        IEnumerable<GenerationNode> incomingNodes,
        bool useRandomSeed)
    {
        var request = Clone(source);
        var effectiveIncoming = incomingNodes.Where(n => !n.IsBypassed).ToList();
        var baseNode = effectiveIncoming.FirstOrDefault(n => n.Type is NodeType.Base or NodeType.BaseConcat);
        var characterNodes = effectiveIncoming.Where(n => n.Type == NodeType.Character).ToList();

        if (IsV4(request))
        {
            request.parameters.V4Prompt ??= new V4ConditionInput
            {
                UseCoords = true,
                UseOrder = true
            };

            request.parameters.V4NegativePrompt ??= new V4ConditionInput
            {
                Caption = new V4ExternalCaption(),
                UseCoords = false,
                UseOrder = false
            };

            request.parameters.V4Prompt.Caption.BaseCaption = CombinePrompts(
                baseNode?.BasePrompt,
                currentNode.BasePrompt);
            request.parameters.V4NegativePrompt.Caption.BaseCaption = baseNode?.NegativePrompt ?? string.Empty;

            request.parameters.V4Prompt.Caption.CharCaptions.Clear();
            request.parameters.V4NegativePrompt.Caption.CharCaptions.Clear();
            foreach (var characterNode in characterNodes)
            {
                request.parameters.V4Prompt.Caption.CharCaptions.Add(CreateCharacterCaption(
                    characterNode.BasePrompt,
                    characterNode.CharX,
                    characterNode.CharY));

                request.parameters.V4NegativePrompt.Caption.CharCaptions.Add(CreateCharacterCaption(
                    characterNode.NegativePrompt,
                    characterNode.CharX,
                    characterNode.CharY));
            }

            request.input = request.parameters.V4Prompt.Caption.BaseCaption;
        }
        else
        {
            var prompts = new List<string>();
            var basePrompt = CombinePrompts(baseNode?.BasePrompt, currentNode.BasePrompt);
            if (!string.IsNullOrWhiteSpace(basePrompt))
            {
                prompts.Add(basePrompt);
            }

            prompts.AddRange(characterNodes
                .Select(n => n.BasePrompt)
                .Where(prompt => !string.IsNullOrWhiteSpace(prompt)));

            request.input = string.Join(", ", prompts);
        }

        ApplyRandomSeed(request, useRandomSeed);
        return request;
    }

    public static GenerationRequest Clone(GenerationRequest source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<GenerationRequest>(json) ?? new GenerationRequest();
    }

    private static bool IsV4(GenerationRequest request)
    {
        return request.model.Contains("nai-diffusion-4", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyRandomSeed(GenerationRequest request, bool useRandomSeed)
    {
        if (useRandomSeed)
        {
            request.parameters.seed = RandomSeedService.NextSeed();
        }
    }

    private static string CombinePrompts(params string?[] prompts)
    {
        return string.Join(", ", prompts.Where(prompt => !string.IsNullOrWhiteSpace(prompt)));
    }

    private static V4ExternalCharacterCaption CreateCharacterCaption(string prompt, double x, double y)
    {
        return new V4ExternalCharacterCaption
        {
            CharCaption = prompt,
            Centers = new List<Coordinates>
            {
                new Coordinates { x = x, y = y }
            }
        };
    }
}
