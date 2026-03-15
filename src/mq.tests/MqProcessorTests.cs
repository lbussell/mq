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

    private static string Dedent(string text)
    {
        string[] lines = text.Split('\n');
        string[] trimmed = [.. lines.Where(l => l.Trim().Length > 0).Select(l => l.TrimStart())];
        return string.Join("\n", trimmed);
    }
}
