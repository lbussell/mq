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
}
