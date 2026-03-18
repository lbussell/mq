// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text;
using System.Text.Json;

namespace Mq.Core;

/// <summary>A single unit of markdown output.</summary>
abstract record MarkdownBlock;

/// <summary>A scalar property rendered as "key: value".</summary>
record KeyValueBlock(string Key, string Value) : MarkdownBlock;

/// <summary>A heading with child blocks underneath it.</summary>
record SectionBlock(string Heading, int Depth, IReadOnlyList<MarkdownBlock> Children)
    : MarkdownBlock;

/// <summary>A bullet list of scalar items.</summary>
record BulletListBlock(IReadOnlyList<string> Items) : MarkdownBlock;

/// <summary>A markdown table.</summary>
record TableBlock(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows)
    : MarkdownBlock;

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

        HashSet<string> tableSet = tableProperties is null ? [] : [.. tableProperties];

        List<MarkdownBlock> blocks = [];

        if (title is not null && root.TryGetProperty(title, out JsonElement titleValue))
            blocks.Add(new SectionBlock(titleValue.ToString() ?? "", Depth: 1, Children: []));

        blocks.AddRange(CollectObjectBlocks(root, skipProperty: title, headingDepth: 2, tableSet));

        StringBuilder sb = new();
        RenderBlocks(sb, blocks);
        return sb.ToString().TrimEnd();
    }

    private static List<MarkdownBlock> CollectObjectBlocks(
        JsonElement obj,
        string? skipProperty,
        int headingDepth,
        HashSet<string> tableProperties
    )
    {
        List<MarkdownBlock> scalarBlocks = [];
        List<MarkdownBlock> complexBlocks = [];

        foreach (JsonProperty prop in obj.EnumerateObject())
        {
            if (prop.Name == skipProperty)
                continue;

            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                List<MarkdownBlock> children = CollectObjectBlocks(
                    prop.Value,
                    skipProperty: null,
                    headingDepth + 1,
                    tableProperties
                );
                complexBlocks.Add(new SectionBlock(prop.Name, headingDepth, children));
            }
            else if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                complexBlocks.Add(CollectArraySection(prop, headingDepth, tableProperties));
            }
            else
            {
                scalarBlocks.Add(new KeyValueBlock(prop.Name, prop.Value.ToString() ?? ""));
            }
        }

        return [.. scalarBlocks, .. complexBlocks];
    }

    private static SectionBlock CollectArraySection(
        JsonProperty prop,
        int headingDepth,
        HashSet<string> tableProperties
    )
    {
        bool isObjectArray = prop
            .Value.EnumerateArray()
            .Any(item => item.ValueKind == JsonValueKind.Object);

        if (tableProperties.Contains(prop.Name) && isObjectArray)
            return new SectionBlock(prop.Name, headingDepth, [CollectTable(prop.Value)]);

        List<MarkdownBlock> children = [];
        List<string> scalarItems = [];
        int index = 0;

        foreach (JsonElement item in prop.Value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                List<MarkdownBlock> objChildren = CollectObjectBlocks(
                    item,
                    skipProperty: null,
                    headingDepth + 2,
                    tableProperties
                );
                children.Add(new SectionBlock(index.ToString(), headingDepth + 1, objChildren));
            }
            else
            {
                scalarItems.Add(item.ToString() ?? "");
            }

            index++;
        }

        if (scalarItems.Count > 0)
            children.Insert(0, new BulletListBlock(scalarItems));

        return new SectionBlock(prop.Name, headingDepth, children);
    }

    private static TableBlock CollectTable(JsonElement array)
    {
        List<JsonElement> rows = [.. array.EnumerateArray()];

        List<string> columns =
        [
            .. rows.Where(row => row.ValueKind == JsonValueKind.Object)
                .SelectMany(row => row.EnumerateObject().Select(p => p.Name))
                .Distinct(),
        ];

        List<IReadOnlyList<string>> dataRows =
        [
            .. rows.Select(row =>
                (IReadOnlyList<string>)
                    [
                        .. columns.Select(col =>
                        {
                            if (
                                row.ValueKind != JsonValueKind.Object
                                || !row.TryGetProperty(col, out JsonElement value)
                            )
                                return "";

                            string raw = value.ValueKind
                                is JsonValueKind.Object
                                    or JsonValueKind.Array
                                ? value.GetRawText()
                                : value.ToString() ?? "";

                            return EscapeTableCell(raw);
                        }),
                    ]
            ),
        ];

        return new TableBlock(columns, dataRows);
    }

    private static void RenderBlocks(StringBuilder sb, IReadOnlyList<MarkdownBlock> blocks)
    {
        foreach (MarkdownBlock block in blocks)
            RenderBlock(sb, block);
    }

    private static void RenderBlock(StringBuilder sb, MarkdownBlock block)
    {
        switch (block)
        {
            case KeyValueBlock kv:
                sb.AppendLine($"{kv.Key}: {kv.Value}");
                sb.AppendLine();
                break;

            case SectionBlock section:
                string hashes = new('#', section.Depth);
                sb.AppendLine($"{hashes} {section.Heading}");
                sb.AppendLine();
                RenderBlocks(sb, section.Children);
                break;

            case BulletListBlock list:
                foreach (string item in list.Items)
                    sb.AppendLine($"- {item}");
                sb.AppendLine();
                break;

            case TableBlock table:
                sb.AppendLine($"| {string.Join(" | ", table.Columns)} |");
                sb.AppendLine($"| {string.Join(" | ", table.Columns.Select(_ => "---"))} |");
                foreach (IReadOnlyList<string> row in table.Rows)
                    sb.AppendLine($"| {string.Join(" | ", row)} |");
                sb.AppendLine();
                break;
        }
    }

    private static string EscapeTableCell(string value) =>
        value.Replace("|", "\\|").Replace("\n", "<br>");
}
