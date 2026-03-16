// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

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
}
