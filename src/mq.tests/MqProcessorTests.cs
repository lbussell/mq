// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mq.Core;

namespace Mq.Core.Tests;

[TestClass]
public class MqProcessorTests
{
    [TestMethod]
    public void Process_ValidJsonObject_ReturnsOk()
    {
        string result = MqProcessor.Process("""{"key": "value"}""");
        Assert.AreEqual("OK", result);
    }

    [TestMethod]
    public void Process_ValidJsonArray_ReturnsOk()
    {
        string result = MqProcessor.Process("""[1, 2, 3]""");
        Assert.AreEqual("OK", result);
    }

    [TestMethod]
    public void Process_ValidJsonString_ReturnsOk()
    {
        string result = MqProcessor.Process(
            """
            "hello"
            """
        );
        Assert.AreEqual("OK", result);
    }

    [TestMethod]
    public void Process_ValidJsonNumber_ReturnsOk()
    {
        string result = MqProcessor.Process("42");
        Assert.AreEqual("OK", result);
    }

    [TestMethod]
    [ExpectedException(typeof(JsonException), AllowDerivedTypes = true)]
    public void Process_InvalidJson_ThrowsJsonException()
    {
        MqProcessor.Process("not json");
    }

    [TestMethod]
    [ExpectedException(typeof(JsonException), AllowDerivedTypes = true)]
    public void Process_EmptyString_ThrowsJsonException()
    {
        MqProcessor.Process("");
    }
}
