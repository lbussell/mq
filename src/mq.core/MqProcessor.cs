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
    /// <param name="tableProperties">Property names whose arrays should render as Markdown tables.</param>
    /// <returns>A Markdown string.</returns>
    public static string Process(
        string input,
        string? title = null,
        IReadOnlyList<string>? tableProperties = null
    )
    {
        using JsonDocument doc = JsonDocument.Parse(input);
        JsonElement root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            return root.ToString() ?? "";

        StringBuilder sb = new StringBuilder();

        if (title is not null && root.TryGetProperty(title, out JsonElement titleValue))
        {
            sb.AppendLine($"# {titleValue}");
            sb.AppendLine();
        }

        HashSet<string> tableSet = tableProperties is null ? [] : [.. tableProperties];

        WriteObjectProperties(sb, root, title, headingDepth: 2, tableSet);

        return sb.ToString().TrimEnd();
    }

    private static void WriteObjectProperties(
        StringBuilder sb,
        JsonElement obj,
        string? skipProperty,
        int headingDepth,
        HashSet<string> tableProperties
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
                sb.AppendLine();
                WriteObjectProperties(
                    sb,
                    prop.Value,
                    skipProperty: null,
                    headingDepth + 1,
                    tableProperties
                );
            }
            else if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                bool isObjectArray = prop
                    .Value.EnumerateArray()
                    .Any(item => item.ValueKind == JsonValueKind.Object);

                if (tableProperties.Contains(prop.Name) && isObjectArray)
                {
                    sb.AppendLine($"{hashes} {prop.Name}");
                    sb.AppendLine();
                    WriteTable(sb, prop.Value);
                }
                else
                {
                    sb.AppendLine($"{hashes} {prop.Name}");
                    sb.AppendLine();
                    WriteArray(sb, prop.Value, headingDepth, tableProperties);
                }
            }
            else
            {
                sb.AppendLine($"{prop.Name}: {prop.Value}");
                sb.AppendLine();
            }
        }
    }

    private static void WriteArray(
        StringBuilder sb,
        JsonElement array,
        int headingDepth,
        HashSet<string> tableProperties
    )
    {
        int index = 0;
        bool hasScalarItems = false;
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                string subHashes = new('#', headingDepth + 1);
                sb.AppendLine($"{subHashes} {index}");
                sb.AppendLine();
                WriteObjectProperties(
                    sb,
                    item,
                    skipProperty: null,
                    headingDepth + 2,
                    tableProperties
                );
            }
            else
            {
                sb.AppendLine($"- {item}");
                hasScalarItems = true;
            }

            index++;
        }

        if (hasScalarItems)
            sb.AppendLine();
    }

    private static void WriteTable(StringBuilder sb, JsonElement array)
    {
        List<JsonElement> rows = [.. array.EnumerateArray()];

        // Collect the union of all keys in first-seen order.
        List<string> columns =
        [
            .. rows.Where(row => row.ValueKind == JsonValueKind.Object)
                .SelectMany(row => row.EnumerateObject().Select(p => p.Name))
                .Distinct(),
        ];

        // Header row
        sb.AppendLine($"| {string.Join(" | ", columns)} |");

        // Separator row
        sb.AppendLine($"| {string.Join(" | ", columns.Select(_ => "---"))} |");

        // Data rows
        foreach (JsonElement row in rows)
        {
            IEnumerable<string> cells = columns.Select(col =>
            {
                if (
                    row.ValueKind != JsonValueKind.Object
                    || !row.TryGetProperty(col, out JsonElement value)
                )
                    return "";

                string raw = value.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                    ? value.GetRawText()
                    : value.ToString() ?? "";

                return EscapeTableCell(raw);
            });

            sb.AppendLine($"| {string.Join(" | ", cells)} |");
        }

        sb.AppendLine();
    }

    private static string EscapeTableCell(string value) =>
        value.Replace("|", "\\|").Replace("\n", "<br>");
}
