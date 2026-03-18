// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text;
using System.Text.Json;

namespace Mq.Core;

/// <summary>A single unit of markdown output.</summary>
abstract record MarkdownBlock;

/// <summary>A group of scalar properties rendered as a bullet list with bold keys.</summary>
record KeyValueListBlock(IReadOnlyList<(string Key, string Value)> Items) : MarkdownBlock;

/// <summary>A heading with child blocks underneath it.</summary>
record SectionBlock(string Heading, int Depth, IReadOnlyList<MarkdownBlock> Children)
    : MarkdownBlock;

/// <summary>A bullet list of scalar items.</summary>
record BulletListBlock(IReadOnlyList<string> Items) : MarkdownBlock;

/// <summary>A markdown table.</summary>
record TableBlock(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows)
    : MarkdownBlock;

/// <summary>A fenced code block.</summary>
record FencedCodeBlock(string Content) : MarkdownBlock;

/// <summary>Core processing logic for mq.</summary>
public static class MqProcessor
{
    /// <summary>
    /// Converts JSON input to a Markdown document.
    /// </summary>
    /// <param name="input">A JSON string.</param>
    /// <param name="title">The JSON property name to use as the title heading.</param>
    /// <param name="tableProperties">Property names whose arrays should render as Markdown tables.</param>
    /// <param name="codeProperties">Property names whose values should render as code.</param>
    /// <param name="depth">The starting heading level (1–6). Defaults to 1.</param>
    /// <returns>A Markdown string.</returns>
    public static string Process(
        string input,
        string? title = null,
        IReadOnlyList<string>? tableProperties = null,
        IReadOnlyList<string>? codeProperties = null,
        int depth = 1
    )
    {
        if (depth < 1 || depth > 6)
            throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be between 1 and 6.");

        using JsonDocument doc = JsonDocument.Parse(input);
        JsonElement root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            return root.ToString() ?? "";

        HashSet<string> tableSet = tableProperties is null ? [] : [.. tableProperties];
        HashSet<string> codeSet = codeProperties is null ? [] : [.. codeProperties];

        List<MarkdownBlock> blocks = [];

        if (title is not null && root.TryGetProperty(title, out JsonElement titleValue))
            blocks.Add(new SectionBlock(titleValue.ToString() ?? "", Depth: depth, Children: []));

        blocks.AddRange(
            CollectObjectBlocks(
                root,
                skipProperty: title,
                headingDepth: depth + 1,
                tableSet,
                codeSet
            )
        );

        StringBuilder sb = new();
        RenderBlocks(sb, blocks);
        return sb.ToString().TrimEnd();
    }

    private static List<MarkdownBlock> CollectObjectBlocks(
        JsonElement obj,
        string? skipProperty,
        int headingDepth,
        HashSet<string> tableProperties,
        HashSet<string> codeProperties
    )
    {
        List<(string Key, string Value)> scalarItems = [];
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
                    tableProperties,
                    codeProperties
                );
                complexBlocks.Add(new SectionBlock(prop.Name, headingDepth, children));
            }
            else if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                complexBlocks.Add(
                    CollectArraySection(prop, headingDepth, tableProperties, codeProperties)
                );
            }
            else
            {
                string value = prop.Value.ToString() ?? "";
                if (codeProperties.Contains(prop.Name))
                {
                    if (value.Contains('\n'))
                        complexBlocks.Add(
                            new SectionBlock(prop.Name, headingDepth, [new FencedCodeBlock(value)])
                        );
                    else
                        scalarItems.Add((prop.Name, FormatInlineCode(value)));
                }
                else
                {
                    scalarItems.Add((prop.Name, value));
                }
            }
        }

        List<MarkdownBlock> result = [];
        if (scalarItems.Count > 0)
            result.Add(new KeyValueListBlock(scalarItems));
        result.AddRange(complexBlocks);
        return result;
    }

    private static SectionBlock CollectArraySection(
        JsonProperty prop,
        int headingDepth,
        HashSet<string> tableProperties,
        HashSet<string> codeProperties
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
                    tableProperties,
                    codeProperties
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
            case KeyValueListBlock kvList:
                foreach ((string key, string value) in kvList.Items)
                    sb.AppendLine($"- **{key}**: {value}");
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
                sb.AppendLine(string.Join(" | ", table.Columns));
                sb.AppendLine(string.Join(" | ", table.Columns.Select(_ => "---")));
                foreach (IReadOnlyList<string> row in table.Rows)
                    sb.AppendLine(string.Join(" | ", row));
                sb.AppendLine();
                break;

            case FencedCodeBlock code:
                string fence = CodeFence(code.Content);
                sb.AppendLine(fence);
                sb.AppendLine(code.Content);
                sb.AppendLine(fence);
                sb.AppendLine();
                break;
        }
    }

    private static string EscapeTableCell(string value) =>
        value.Replace("|", "\\|").Replace("\n", "<br>");

    /// <summary>
    /// Wraps a value in inline code backticks, using enough backticks to avoid
    /// conflicts with any backticks present in the value.
    /// </summary>
    private static string FormatInlineCode(string value)
    {
        int max = MaxConsecutiveBackticks(value);
        string delimiters = new('`', max + 1);
        // Add padding spaces when value starts or ends with a backtick to avoid
        // the delimiter run merging with the value.
        bool needsPadding = value.StartsWith('`') || value.EndsWith('`');
        return needsPadding
            ? $"{delimiters} {value} {delimiters}"
            : $"{delimiters}{value}{delimiters}";
    }

    /// <summary>
    /// Returns the fence string (``` or longer) needed to safely wrap the content
    /// without prematurely closing the fenced code block.
    /// </summary>
    private static string CodeFence(string content)
    {
        int max = MaxConsecutiveBackticks(content);
        return new('`', Math.Max(3, max + 1));
    }

    private static int MaxConsecutiveBackticks(string value)
    {
        int max = 0;
        int run = 0;
        foreach (char c in value)
        {
            if (c == '`')
            {
                run++;
                if (run > max)
                    max = run;
            }
            else
            {
                run = 0;
            }
        }
        return max;
    }
}
