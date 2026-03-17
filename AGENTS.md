# MQ

`mq` is a command line tool that converts json to markdown.

## Build & Test

Run `just validate` to format, build, and test the project.

## Tech Stack

- .NET 10 + Native AOT
- MSTest + CsCheck for testing

## When Making Changes

- Follow red-green TDD
- Run `just validate`
- Invoke two code-reviewer sub-agents: one to review code, and one to make sure that documentation is up-to-date.

## Code Conventions

- Composition over inheritance.
- Guard early, fail fast, return early to avoid nesting.
- Use LINQ instead of loops for manipulating collections.
- Use string interpolation instead of `string.Format` or concatenation.
- Use `"""triple-quoted strings"""` for multi-line string literals.
- Use `record` instead of `class` for DTOs.
- Avoid abbreviations or acronyms in names.
- Comments explain "why," not "what".
