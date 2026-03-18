// SPDX-FileCopyrightText: Copyright (c) 2026 Logan Bussell
// SPDX-License-Identifier: MIT

using System.CommandLine;
using Mq.Core;

Argument<string?> inputArgument = new("input")
{
    Description = "JSON string. Reads from stdin if omitted.",
    Arity = ArgumentArity.ZeroOrOne,
};

Option<string?> titleOption = new("--title")
{
    Description = "JSON property to use as the H1 heading.",
};

Option<string[]> tableOption = new("--table")
{
    Description = "JSON properties whose arrays should render as Markdown tables.",
    AllowMultipleArgumentsPerToken = true,
};

Option<string[]> codeOption = new("--code")
{
    Description = "JSON properties whose values should render as code.",
    AllowMultipleArgumentsPerToken = true,
};

RootCommand rootCommand = new("Convert JSON to Markdown.")
{
    inputArgument,
    titleOption,
    tableOption,
    codeOption,
};

rootCommand.SetAction(result =>
{
    string? input = result.GetValue(inputArgument);
    string? title = result.GetValue(titleOption);
    string[]? table = result.GetValue(tableOption);
    string[]? code = result.GetValue(codeOption);

    input ??= Console.In.ReadToEnd();
    string output = MqProcessor.Process(input, title, table, code);
    Console.WriteLine(output);
});

rootCommand.Parse(args).Invoke();
