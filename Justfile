# show available recipes
[private]
default:
    @just --list

# clean and rebuild to check for errors
validate:
    dotnet clean
    dotnet build --no-restore
    dotnet test --no-build

# run the app, passing any extra arguments through
run *args:
    dotnet run --project src/mq -- {{ args }}