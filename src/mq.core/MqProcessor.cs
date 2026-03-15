// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text;
using System.Text.Json;

namespace Mq.Core;

/// <summary>Core processing logic for mq.</summary>
public static class MqProcessor
{
    /// <summary>
    /// Converts JSON input to a Markdown document.
    /// </summary>
    /// <param name="input">A JSON string.</param>
    /// <param name="title">The JSON property name to use as the H1 heading.</param>
    /// <returns>A Markdown string.</returns>
    public static string Process(string input, string? title = null)
    {
        using JsonDocument doc = JsonDocument.Parse(input);
        JsonElement root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            return root.ToString() ?? "";

        StringBuilder sb = new StringBuilder();

        if (title is not null && root.TryGetProperty(title, out JsonElement titleValue))
            sb.AppendLine($"# {titleValue}");

        WriteObjectProperties(sb, root, title, headingDepth: 2);

        return sb.ToString().TrimEnd();
    }

    private static void WriteObjectProperties(
        StringBuilder sb,
        JsonElement obj,
        string? skipProperty,
        int headingDepth
    )
    {
        string hashes = new('#', headingDepth);

        foreach (JsonProperty prop in obj.EnumerateObject())
        {
            if (prop.Name == skipProperty)
                continue;

            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                sb.AppendLine($"{hashes} {prop.Name}");
                WriteObjectProperties(sb, prop.Value, skipProperty: null, headingDepth + 1);
            }
            else if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine($"{hashes} {prop.Name}");
                int index = 0;
                foreach (JsonElement item in prop.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        string subHashes = new('#', headingDepth + 1);
                        sb.AppendLine($"{subHashes} {index}");
                        WriteObjectProperties(sb, item, skipProperty: null, headingDepth + 2);
                    }
                    else
                    {
                        sb.AppendLine($"- {item}");
                    }

                    index++;
                }
            }
            else
            {
                sb.AppendLine($"{prop.Name}: {prop.Value}");
            }
        }
    }
}
