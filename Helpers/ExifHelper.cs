﻿using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Collections.Generic;

namespace ImageGen.Helpers;

public static class ExifHelper
{
    // EXIF UserComment Tag ID (0x9286)
    private const int ExifUserCommentId = 0x9286;

    public static string ExtractMetadata(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return string.Empty;
        }

        try
        {
            using var image = Image.FromFile(filePath);

            if (image.PropertyIdList.Contains(ExifUserCommentId))
            {
                var propertyItem = image.GetPropertyItem(ExifUserCommentId);
                if (propertyItem?.Value == null)
                    return string.Empty;

                var data = propertyItem.Value;

                string metadata;
                // UserComment는 보통 처음 8바이트에 인코딩 헤더를 포함합니다.
                string header = data.Length > 8 ? Encoding.ASCII.GetString(data, 0, 8) : string.Empty;

                if (header.StartsWith("ASCII"))
                    metadata = Encoding.ASCII.GetString(data, 8, data.Length - 8).Trim('\0');
                else if (header.StartsWith("UNICODE"))
                    metadata = Encoding.Unicode.GetString(data, 8, data.Length - 8).Trim('\0');
                else
                    metadata = Encoding.UTF8.GetString(data).Trim('\0');

                return ParseNaiPrompt(metadata);
            }
        }
        catch
        {
            // 이미지 형식이 잘못되었거나 읽을 수 없는 경우 빈 문자열 반환
        }

        return string.Empty;
    }

    private static string ParseNaiPrompt(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
                return json;

            var node = JsonNode.Parse(json);
            if (node == null) return json;

            string ExtractText(JsonNode? captionNode)
            {
                if (captionNode == null) return string.Empty;
                var parts = new List<string>();

                string? baseCap = captionNode["base_caption"]?.ToString();
                if (!string.IsNullOrWhiteSpace(baseCap)) parts.Add(baseCap);

                if (captionNode["char_captions"] is JsonArray arr)
                {
                    int idx = 0;
                    foreach (var item in arr)
                    {
                        string? c = item?["char_caption"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(c))
                        {
                            parts.Add($"\n\n==[Character #{++idx}]==\n" + c);
                        }
                    }
                }
                return string.Join(", ", parts);
            }

            string positive = ExtractText(node["v4_prompt"]?["caption"]);
            string negative = ExtractText(node["v4_negative_prompt"]?["caption"]);

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(positive)) sb.Append(positive);
            if (!string.IsNullOrWhiteSpace(negative))
            {
                if (sb.Length > 0) sb.AppendLine().AppendLine("\n\n==[Negative Prompt]==");
                else sb.Append("\n\n==[Negative Prompt]==");
                sb.Append(negative);
            }

            return sb.Length > 0 ? sb.ToString() : json;
        }
        catch
        {
            return json;
        }
    }
}
