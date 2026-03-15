// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using ConsoleAppFramework;
using Mq;

ConsoleApp.ConsoleAppBuilder consoleApp = ConsoleApp.Create();
consoleApp.Add<MqCommand>();
consoleApp.Run(args);
