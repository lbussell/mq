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

            stars: 100

            url: https://example.com
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

            totalCount: 7878
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

            a: 1

            ### 1

            a: 2
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

            value: 42
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
            // UNLESS they are both bullet-list items.
            if (previousIsContent && nextIsContent && !currentIsEmpty)
            {
                bool bothAreBullets =
                    lines[i].TrimStart().StartsWith('-')
                    && lines[i - 1].TrimStart().StartsWith('-');
                Assert.IsTrue(
                    bothAreBullets,
                    $"Lines {i - 1} and {i} are adjacent content lines without a blank line separator:\n"
                        + $"  [{i - 1}] \"{lines[i - 1]}\"\n"
                        + $"  [{i}] \"{lines[i]}\""
                );
            }
        }
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
