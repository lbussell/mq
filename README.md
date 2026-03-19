# MQ

A command line tool that converts JSON to Markdown.

## Usage

```bash
# Basic: pipe JSON and convert to Markdown
echo '{"name": "test", "stars": 100}' | mq

# Use a property as the H1 heading
echo '{"name": "test", "stars": 100}' | mq --title name

# Start headings at H3 instead of H1
echo '{"name": "test", "stars": 100}' | mq --title name --depth 3

# Root arrays: each object is separated by ---
echo '[{"name": "a", "stars": 1}, {"name": "b", "stars": 2}]' | mq

# Render an array of objects as a Markdown table
echo '{"name": "test", "items": [{"a": 1}, {"a": 2}]}' | mq --title name --table items

# Multiple --table flags
echo '{"x": [{"a": 1}], "y": [{"b": 2}]}' | mq --table x --table y

# Render a single-line property value as inline code
echo '{"name": "test", "sha": "abc123"}' | mq --title name --code sha

# Render a multi-line property value as a fenced code block
echo '{"name": "test", "diff": "line1\nline2\nline3"}' | mq --title name --code diff

# Multiple --code flags
echo '{"sha": "abc123", "tag": "v1.0.0"}' | mq --code sha --code tag

# Render a URL property as a clickable Markdown link
echo '{"name": "test", "url": "https://example.com"}' | mq --link url

# Pair a URL and a title property into a single link
echo '{"url": "https://example.com", "title": "Example", "stars": 5}' | mq --link url,title
```

### Options

| Option | Description |
| --- | --- |
| `--title <property>` | Use the named property's value as the title heading |
| `--depth <level>` | Starting heading level for output (1–6). Default is `1` |
| `--table <property>` | Render the named property's array of objects as a Markdown table (repeatable) |
| `--code <property>` | Render the named property's value as inline code (single-line) or a fenced code block (multi-line) (repeatable) |
| `--link <spec>` | Render a URL property as a clickable Markdown link (repeatable). Use `urlProp` to wrap the URL value, or `urlProp,textProp` to pair two properties into `[textPropValue](urlPropValue)`, consuming both from the output. Non-URL values and missing properties fall through to default rendering. |

## Automation scripts

The `scripts/` folder contains .NET file-based apps. They use the `LoganBussell.EasyScripting` NuGet package, so they can run without a local project reference.

Run them from the repository root:

```bash
dotnet run scripts/SetupRepository.cs
dotnet run scripts/SetupPublishing.cs
dotnet run scripts/Release.cs
```

These scripts assume the GitHub CLI is installed and authenticated with `gh auth login`.

### `SetupRepository.cs`

Configures a GitHub repository with the defaults used by this template:

- enables release immutability
- disables wikis
- disables discussions
- disables merge commits for pull requests

### `SetupPublishing.cs`

Bootstraps the GitHub-side publishing setup used by `.github/workflows/publish-nuget.yml`:

- creates the `production` environment
- sets the `NUGET_USER` environment secret
- prints the remaining values needed to configure NuGet Trusted Publishing

After the script finishes, go to <https://www.nuget.org/account/trustedpublishing> and create a Trusted Publisher that matches the repository, workflow file, and environment printed by the script.

### `Release.cs`

Creates a release by:

- verifying the working tree is clean
- creating and pushing a `v*` tag
- creating a GitHub Release with generated notes

Pushing the version tag triggers `.github/workflows/publish-nuget.yml`.

## How to add new projects

Projects go under the `src/` folder.
Projects should be named descriptively, but should not be too verbose.

Example project layout:

```text
- MyAwesomeProject.slnx
- src/
    - Console/
        Console.csproj
    - Library/
        Library.csproj
    - Library.Tests/
        Library.Tests.csproj
```

Test projects go in the `src/` folder too, and should be named the same as the project they are testing but with the `.Tests` suffix.

Projects should have limited namespace nesting.
Only add nesting when differentiation is required.
If all of the projects in the repo will have the same prefix (e.g. `MyProject.Library`, `MyProject.Tests`, `MyProject.Web`), then the prefix does not provide value and should be removed.

### Example: Add a new class library

From the repo root, run the following:

```bash
# Always run with dry-run first to verify correctness
dotnet new classlib --name MyClasslib --output src/MyClasslib --dry-run
# Create the project
dotnet new classlib --name MyClasslib --output src/MyClasslib --dry-run
# Always add projects to the root of the solution - no solution folders
dotnet sln *.slnx add src/MyClasslib --in-root
```
