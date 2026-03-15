// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;

ConsoleApp.ConsoleAppBuilder consoleApp = ConsoleApp.Create();
consoleApp.Add<Commands>();
consoleApp.Run(args);

/// <summary>CLI commands for mq.</summary>
public class Commands
{
    /// <summary>Default command.</summary>
    [Command("")]
    public void Root()
    {
        Console.WriteLine("Hello world");
    }
}
