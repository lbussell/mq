// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using Mq.Core;

ConsoleApp.ConsoleAppBuilder consoleApp = ConsoleApp.Create();
consoleApp.Add<MqCommand>();
consoleApp.Run(args);

class MqCommand
{
    /// <summary>Parse JSON from an argument or standard input.</summary>
    /// <param name="input">JSON string to parse. Reads from stdin if omitted.</param>
    [Command("")]
    public void Execute([Argument] string? input = null)
    {
        input ??= Console.In.ReadToEnd();
        string result = MqProcessor.Process(input);
        Console.WriteLine(result);
    }
}
