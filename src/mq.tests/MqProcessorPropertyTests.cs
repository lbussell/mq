// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text;
using CsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mq.Core.Tests;

[TestClass]
public class MqProcessorPropertyTests
{
    private static readonly Gen<string> AlphaNumString = Gen
        .Char.AlphaNumeric.Array[1, 20]
        .Select(chars => new string(chars));

    [TestMethod]
    public void Process_ScalarValues_AppearVerbatimInOutput()
    {
        Gen.Select(AlphaNumString, AlphaNumString, AlphaNumString)
            .Where((key1, key2, _) => key1 != key2)
            .Sample(
                (key1, key2, value) =>
                {
                    string json = $$"""{"{{key1}}": "{{value}}", "{{key2}}": 42}""";
                    string result = MqProcessor.Process(json);

                    return result.Contains(value) && result.Contains("42");
                },
                iter: 1000
            );
    }

    [TestMethod]
    public void Process_TopLevelKeys_AppearInOutput()
    {
        Gen.Select(AlphaNumString, AlphaNumString, AlphaNumString)
            .Where((key1, key2, _) => key1 != key2)
            .Sample(
                (key1, key2, value) =>
                {
                    string json = $$"""{"{{key1}}": "{{value}}", "{{key2}}": 99}""";
                    string result = MqProcessor.Process(json);

                    return result.Contains(key1) && result.Contains(key2);
                },
                iter: 1000
            );
    }

    [TestMethod]
    public void Process_WithTitle_OutputStartsWithH1()
    {
        Gen.Select(AlphaNumString, AlphaNumString)
            .Where((key, value) => key != value)
            .Sample(
                (key, value) =>
                {
                    string json = $$"""{"{{key}}": "{{value}}"}""";
                    string result = MqProcessor.Process(json, title: key);

                    return result.StartsWith($"# {value}");
                },
                iter: 1000
            );
    }

    [TestMethod]
    public void Process_Output_HasNoTrailingWhitespace()
    {
        Gen.Select(AlphaNumString, AlphaNumString)
            .Where((key, value) => key != value)
            .Sample(
                (key, value) =>
                {
                    string json = $$"""{"{{key}}": "{{value}}"}""";
                    string result = MqProcessor.Process(json);

                    return result == result.TrimEnd();
                },
                iter: 1000
            );
    }

    [TestMethod]
    public void Process_HeadingDepth_NeverExceedsNestingDepthPlusOne()
    {
        // Generate a random nesting depth between 1 and 5
        Gen.Int[1, 5]
            .Sample(
                depth =>
                {
                    string json = BuildNestedJson(depth);
                    string result = MqProcessor.Process(json);

                    int maxHashes = result
                        .Split('\n')
                        .Where(line => line.StartsWith('#'))
                        .Select(line => line.TakeWhile(c => c == '#').Count())
                        .DefaultIfEmpty(0)
                        .Max();

                    // Top-level properties start at ## (depth 2), each nesting adds 1
                    return maxHashes <= depth + 1;
                },
                iter: 1000
            );
    }

    /// <summary>
    /// Builds a JSON object nested to the specified depth.
    /// Depth 1: {"a": {"value": 1}}
    /// Depth 2: {"a": {"b": {"value": 1}}}
    /// </summary>
    private static string BuildNestedJson(int depth)
    {
        StringBuilder sb = new();
        for (int i = 0; i < depth; i++)
            sb.Append($$"""{"level{{i}}": """);

        sb.Append("{\"value\": 1}");

        for (int i = 0; i < depth; i++)
            sb.Append('}');

        return sb.ToString();
    }
}
