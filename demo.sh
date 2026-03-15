#!/usr/bin/env bash

if ! command -v gh &> /dev/null; then
    echo "Error: GitHub CLI ('gh') is not installed. Install it from https://cli.github.com/" >&2
    exit 1
fi

if ! command -v glow &> /dev/null; then
    echo "Error: 'glow' is not installed (used to prettify Markdown output). Install it from https://github.com/charmbracelet/glow" >&2
    exit 1
fi

gh repo view dotnet/runtime \
    --json nameWithOwner,url,licenseInfo,issues,pullRequests,primaryLanguage,updatedAt,stargazerCount,watchers,latestRelease \
    | dotnet run --project src/mq -- --title nameWithOwner \
    | glow -
