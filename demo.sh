#!/usr/bin/env bash

if ! command -v gh &>/dev/null; then
        echo "Error: GitHub CLI ('gh') is not installed. Install it from https://cli.github.com/" >&2
        exit 1
fi

gh pr view --repo dotnet/docker-tools 2030 --json title,number,author,statusCheckRollup |
        jq '.author |= .login | .statusCheckRollup |= [.[] | {name, conclusion, status}]' |
        just run --title title --table statusCheckRollup
