// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;

namespace Mq;

/// <summary>CLI commands for mq.</summary>
public class MqCommand
{
    /// <summary>Default command.</summary>
    [Command("")]
    public void Execute()
    {
        Console.WriteLine("Hello world");
    }
}
