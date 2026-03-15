// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.Text.Json;

namespace Mq.Core;

/// <summary>Core processing logic for mq.</summary>
public static class MqProcessor
{
    /// <summary>
    /// Parses the input as JSON and returns "OK" on success.
    /// </summary>
    /// <param name="input">A JSON string to validate.</param>
    /// <returns>"OK" if the input is valid JSON.</returns>
    /// <exception cref="JsonException">Thrown when the input is not valid JSON.</exception>
    public static string Process(string input)
    {
        using JsonDocument _ = JsonDocument.Parse(input);
        return "OK";
    }
}
