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

/// <summary>A link rendering specification parsed from a --link option value.</summary>
/// <param name="UrlProperty">The property whose value is used as the URL.</param>
/// <param name="TextProperty">
/// The property whose value is used as the link text.
/// When null, the URL value is used as both text and href.
/// </param>
record LinkSpec(string UrlProperty, string? TextProperty);

/// <summary>A horizontal rule separating sections.</summary>
record HorizontalRuleBlock : MarkdownBlock;

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
    /// <param name="linkProperties">
    /// Link specs of the form <c>"urlProp"</c> or <c>"urlProp,textProp"</c>.
    /// A single-property spec wraps the URL value as <c>[url](url)</c>.
    /// A two-property spec renders <c>[textPropValue](urlPropValue)</c> and consumes both properties.
    /// Non-URL values and missing properties fall through to default rendering.
    /// </param>
    /// <param name="depth">The starting heading level (1–6). Defaults to 1.</param>
    /// <returns>A Markdown string.</returns>
    public static string Process(
        string input,
        string? title = null,
        IReadOnlyList<string>? tableProperties = null,
        IReadOnlyList<string>? codeProperties = null,
        IReadOnlyList<string>? linkProperties = null,
        int depth = 1
    )
    {
        if (depth < 1 || depth > 6)
            throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be between 1 and 6.");

        using JsonDocument doc = JsonDocument.Parse(input);
        JsonElement root = doc.RootElement;

        HashSet<string> tableSet = tableProperties is null ? [] : [.. tableProperties];
        HashSet<string> codeSet = codeProperties is null ? [] : [.. codeProperties];
        List<LinkSpec> linkSpecs = linkProperties is null
            ? []
            : [.. linkProperties.Select(ParseLinkSpec)];

        if (root.ValueKind == JsonValueKind.Array)
        {
            List<MarkdownBlock> arrayBlocks = [];
            int objectCount = 0;
            foreach (JsonElement item in root.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                if (objectCount > 0)
                    arrayBlocks.Add(new HorizontalRuleBlock());
                arrayBlocks.AddRange(
                    CollectObjectBlocks(
                        item,
                        skipProperty: null,
                        headingDepth: 2,
                        tableSet,
                        codeSet,
                        linkSpecs
                    )
                );
                objectCount++;
            }
            StringBuilder arraySb = new();
            RenderBlocks(arraySb, arrayBlocks);
            return arraySb.ToString().TrimEnd();
        }

        if (root.ValueKind != JsonValueKind.Object)
            return root.ToString() ?? "";

        List<MarkdownBlock> blocks = [];

        if (title is not null && root.TryGetProperty(title, out JsonElement titleValue))
            blocks.Add(new SectionBlock(titleValue.ToString() ?? "", Depth: depth, Children: []));

        blocks.AddRange(
            CollectObjectBlocks(
                root,
                skipProperty: title,
                headingDepth: depth + 1,
                tableSet,
                codeSet,
                linkSpecs
            )
        );

        StringBuilder sb = new();
        RenderBlocks(sb, blocks);
        return sb.ToString().TrimEnd();
    }

    private static LinkSpec ParseLinkSpec(string spec)
    {
        int commaIndex = spec.IndexOf(',');
        return commaIndex < 0
            ? new LinkSpec(spec, null)
            : new LinkSpec(spec[..commaIndex], spec[(commaIndex + 1)..]);
    }

    private static List<MarkdownBlock> CollectObjectBlocks(
        JsonElement obj,
        string? skipProperty,
        int headingDepth,
        HashSet<string> tableProperties,
        HashSet<string> codeProperties,
        IReadOnlyList<LinkSpec> linkSpecs
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
                    codeProperties,
                    linkSpecs
                );
                complexBlocks.Add(new SectionBlock(prop.Name, headingDepth, children));
            }
            else if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                complexBlocks.Add(
                    CollectArraySection(
                        prop,
                        headingDepth,
                        tableProperties,
                        codeProperties,
                        linkSpecs
                    )
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

        if (linkSpecs.Count > 0)
            scalarItems = ApplyLinks(scalarItems, linkSpecs);

        List<MarkdownBlock> result = [];
        if (scalarItems.Count > 0)
            result.Add(new KeyValueListBlock(scalarItems));
        result.AddRange(complexBlocks);
        return result;
    }

    private static List<(string Key, string Value)> ApplyLinks(
        List<(string Key, string Value)> items,
        IReadOnlyList<LinkSpec> linkSpecs
    )
    {
        // Last spec for a given URL property wins, matching CLI override semantics.
        Dictionary<string, LinkSpec> urlPropToSpec = linkSpecs
            .GroupBy(s => s.UrlProperty)
            .ToDictionary(g => g.Key, g => g.Last());
        HashSet<string> textPropsToConsume =
        [
            .. linkSpecs.Select(s => s.TextProperty).OfType<string>(),
        ];
        Dictionary<string, string> valueByKey = items.ToDictionary(i => i.Key, i => i.Value);

        return
        [
            .. items
                .Where(item => !textPropsToConsume.Contains(item.Key))
                .Select(item =>
                {
                    if (!urlPropToSpec.TryGetValue(item.Key, out LinkSpec? spec))
                        return item;

                    if (!IsUrl(item.Value))
                        return item;

                    string linkText =
                        spec.TextProperty is not null
                        && valueByKey.TryGetValue(spec.TextProperty, out string? textValue)
                            ? textValue
                            : item.Value;

                    return (item.Key, $"[{linkText}]({item.Value})");
                }),
        ];
    }

    private static bool IsUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static SectionBlock CollectArraySection(
        JsonProperty prop,
        int headingDepth,
        HashSet<string> tableProperties,
        HashSet<string> codeProperties,
        IReadOnlyList<LinkSpec> linkSpecs
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
                    codeProperties,
                    linkSpecs
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

            case HorizontalRuleBlock:
                sb.AppendLine("---");
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
