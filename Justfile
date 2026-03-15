# show available recipes
[private]
default:
    @just --list

# clean, format/lint, build, and test
validate:
    dotnet clean
    dotnet format
    dotnet build --no-restore
    dotnet test --no-build

# run the app, passing any extra arguments through
run *args:
    dotnet run --project src/mq -- {{ args }}

# run the demo script
demo:
    bash demo.sh
