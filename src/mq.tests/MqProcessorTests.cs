// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mq.Core.Tests;

[TestClass]
public class MqProcessorTests
{
    [TestMethod]
    public void Process_TitleProperty_BecomesH1()
    {
        string json = """{"nameWithOwner": "dotnet/runtime", "stars": 100}""";
        string result = MqProcessor.Process(json, title: "nameWithOwner");
        StringAssert.StartsWith(result, "# dotnet/runtime");
    }

    [TestMethod]
    public void Process_ScalarProperties_BecomeKeyValueLines()
    {
        string json =
            """{"nameWithOwner": "dotnet/runtime", "stars": 100, "url": "https://example.com"}""";
        string result = MqProcessor.Process(json, title: "nameWithOwner");
        string expected = """
            # dotnet/runtime

            - **stars**: 100
            - **url**: https://example.com
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_NestedObject_BecomesSectionWithFields()
    {
        string json = """{"name": "test", "issues": {"totalCount": 7878}}""";
        string result = MqProcessor.Process(json, title: "name");
        string expected = """
            # test

            ## issues

            - **totalCount**: 7878
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_ScalarArray_BecomesBulletList()
    {
        string json = """{"name": "test", "tags": ["alpha", "beta", "gamma"]}""";
        string result = MqProcessor.Process(json, title: "name");
        string expected = """
            # test

            ## tags

            - alpha
            - beta
            - gamma
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_ObjectArray_BecomesSubHeadingsPerElement()
    {
        string json = """{"name": "test", "items": [{"a": 1}, {"a": 2}]}""";
        string result = MqProcessor.Process(json, title: "name");
        string expected = """
            # test

            ## items

            ### 0

            - **a**: 1

            ### 1

            - **a**: 2
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_DeeplyNested_IncrementsHeadingDepth()
    {
        string json = """{"name": "root", "level1": {"level2": {"value": 42}}}""";
        string result = MqProcessor.Process(json, title: "name");
        string expected = """
            # root

            ## level1

            ### level2

            - **value**: 42
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_AdjacentBlocks_AreSeparatedByBlankLine()
    {
        string json = """{"name": "test", "stars": 100, "tags": ["a", "b"], "info": {"x": 1}}""";
        string result = MqProcessor.Process(json, title: "name");

        // Split into blocks: groups of consecutive non-empty lines.
        string[] lines = result.Split('\n');
        for (int i = 1; i < lines.Length - 1; i++)
        {
            bool previousIsContent = lines[i - 1].Trim().Length > 0;
            bool currentIsEmpty = lines[i].Trim().Length == 0;
            bool nextIsContent = lines[i + 1].Trim().Length > 0;

            // A blank line should only appear between two content lines (separator).
            // Two content lines should never be adjacent without a blank line,
            // UNLESS they are both bullet-list items or both table rows.
            if (previousIsContent && nextIsContent && !currentIsEmpty)
            {
                bool bothAreBullets =
                    lines[i].TrimStart().StartsWith('-')
                    && lines[i - 1].TrimStart().StartsWith('-');
                bool bothAreTableRows =
                    lines[i].TrimStart().StartsWith('|')
                    && lines[i - 1].TrimStart().StartsWith('|');
                Assert.IsTrue(
                    bothAreBullets || bothAreTableRows,
                    $"Lines {i - 1} and {i} are adjacent content lines without a blank line separator:\n"
                        + $"  [{i - 1}] \"{lines[i - 1]}\"\n"
                        + $"  [{i}] \"{lines[i]}\""
                );
            }
        }
    }

    [TestMethod]
    public void Process_ScalarProperties_RenderedBeforeComplexProperties()
    {
        string json = """{"name": "test", "info": {"x": 1}, "count": 42}""";
        string result = MqProcessor.Process(json, title: "name");
        string expected = """
            # test

            - **count**: 42

            ## info

            - **x**: 1
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_TableProperty_RendersMarkdownTable()
    {
        string json = """{"name": "test", "items": [{"a": 1, "b": "x"}, {"a": 2, "b": "y"}]}""";
        string result = MqProcessor.Process(json, title: "name", tableProperties: ["items"]);
        string expected = """
            # test

            ## items

            a | b
            --- | ---
            1 | x
            2 | y
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_TableProperty_MissingKeysRenderAsEmptyCells()
    {
        string json = """{"items": [{"a": 1}, {"b": 2}]}""";
        string result = MqProcessor.Process(json, tableProperties: ["items"]);
        string expected = """
            ## items

            a | b
            --- | ---
            1 | 
             | 2
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_TableProperty_NestedObjectsRenderAsInlineJson()
    {
        string json = """{"items": [{"name": "a", "meta": {"x": 1}}]}""";
        string result = MqProcessor.Process(json, tableProperties: ["items"]);
        string expected = """
            ## items

            name | meta
            --- | ---
            a | {"x": 1}
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_TableProperty_ScalarArrayFallsBackToBulletList()
    {
        string json = """{"tags": ["alpha", "beta"]}""";
        string result = MqProcessor.Process(json, tableProperties: ["tags"]);
        string expected = """
            ## tags

            - alpha
            - beta
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_TableProperty_NonArrayPropertyFallsBackToDefault()
    {
        string json = """{"name": "test", "count": 42}""";
        string result = MqProcessor.Process(json, tableProperties: ["count"]);
        string expected = """
            - **name**: test
            - **count**: 42
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_MultipleTableProperties_AllRenderAsTables()
    {
        string json = """{"a": [{"x": 1}], "b": [{"y": 2}]}""";
        string result = MqProcessor.Process(json, tableProperties: ["a", "b"]);
        string expected = """
            ## a

            x
            ---
            1

            ## b

            y
            ---
            2
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_TableWithTitleCombined_WorksTogether()
    {
        string json = """{"name": "root", "items": [{"k": "v"}]}""";
        string result = MqProcessor.Process(json, title: "name", tableProperties: ["items"]);
        string expected = """
            # root

            ## items

            k
            ---
            v
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_TableProperty_PipeCharactersInValuesAreEscaped()
    {
        string json = """{"items": [{"col": "a|b|c"}]}""";
        string result = MqProcessor.Process(json, tableProperties: ["items"]);
        string expected = """
            ## items

            col
            ---
            a\|b\|c
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_TableProperty_NewlinesInValuesAreReplacedWithBreaks()
    {
        string json = """{"items": [{"col": "line1\nline2"}]}""";
        string result = MqProcessor.Process(json, tableProperties: ["items"]);
        string expected = """
            ## items

            col
            ---
            line1<br>line2
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_TableProperty_EmptyArrayRendersOnlyHeading()
    {
        string json = """{"items": []}""";
        string result = MqProcessor.Process(json, tableProperties: ["items"]);
        string expected = """
            ## items
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_CodeProperty_SingleLineRendersAsInlineCode()
    {
        string json = """{"name": "test", "sha": "abc123"}""";
        string result = MqProcessor.Process(json, title: "name", codeProperties: ["sha"]);
        string expected = """
            # test

            - **sha**: `abc123`
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_CodeProperty_MultiLineRendersAsFencedCodeBlock()
    {
        string json = """{"name": "test", "config": "line1\nline2\nline3"}""";
        string result = MqProcessor.Process(json, title: "name", codeProperties: ["config"]);
        string expected = """
            # test

            ## config

            ```
            line1
            line2
            line3
            ```
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_MultipleCodeProperties_AllRenderAsCode()
    {
        string json = """{"sha": "abc123", "tag": "v1.0.0"}""";
        string result = MqProcessor.Process(json, codeProperties: ["sha", "tag"]);
        string expected = """
            - **sha**: `abc123`
            - **tag**: `v1.0.0`
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_CodeProperty_NonScalarPropertyFallsBackToDefault()
    {
        string json = """{"name": "test", "info": {"x": 1}}""";
        string result = MqProcessor.Process(json, title: "name", codeProperties: ["info"]);
        string expected = """
            # test

            ## info

            - **x**: 1
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_CodeWithTitleAndTableCombined_WorksTogether()
    {
        string json = """{"name": "root", "sha": "abc123", "items": [{"k": "v"}]}""";
        string result = MqProcessor.Process(
            json,
            title: "name",
            tableProperties: ["items"],
            codeProperties: ["sha"]
        );
        string expected = """
            # root

            - **sha**: `abc123`

            ## items

            k
            ---
            v
            """;
        Assert.AreEqual(Dedent(expected), result);
    }

    [TestMethod]
    public void Process_CodeProperty_ValueWithBackticksUsesLongerDelimiters()
    {
        string json = """{"cmd": "echo `hello`"}""";
        string result = MqProcessor.Process(json, codeProperties: ["cmd"]);
        Assert.IsTrue(
            result.Contains("``"),
            "Should use double backticks when value contains a backtick"
        );
        Assert.IsTrue(result.Contains("echo `hello`"), "Value should appear verbatim");
    }

    [TestMethod]
    public void Process_CodeProperty_MultiLineValueWithTripleBackticksUsesFourBacktickFence()
    {
        string json = """{"code": "```\nfenced\n```"}""";
        string result = MqProcessor.Process(json, codeProperties: ["code"]);
        Assert.IsTrue(
            result.Contains("````"),
            "Should use 4 backticks when content contains triple backticks"
        );
    }

    private static string Dedent(string text)
    {
        string[] lines = text.Split('\n');

        int leading = lines
            .Where(l => l.Trim().Length > 0)
            .Select(l => l.Length - l.TrimStart().Length)
            .DefaultIfEmpty(0)
            .Min();

        string[] trimmed =
        [
            .. lines
                .SkipWhile(l => l.Trim().Length == 0)
                .Select(l => l.Length >= leading ? l[leading..] : l),
        ];

        return string.Join("\n", trimmed).TrimEnd();
    }
}
