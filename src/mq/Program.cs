// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using Mq.Core;

ConsoleApp.ConsoleAppBuilder consoleApp = ConsoleApp.Create();
consoleApp.Add<MqCommand>();
consoleApp.Run(args);

class MqCommand
{
    /// <summary>Convert JSON to Markdown.</summary>
    /// <param name="input">JSON string. Reads from stdin if omitted.</param>
    /// <param name="title">JSON property to use as the H1 heading.</param>
    [Command("")]
    public void Execute([Argument] string? input = null, string? title = null)
    {
        input ??= Console.In.ReadToEnd();
        string result = MqProcessor.Process(input, title);
        Console.WriteLine(result);
    }
}
