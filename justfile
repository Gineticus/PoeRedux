set windows-shell := ["cmd.exe", "/c"]

app := "PoeRedux/PoeRedux.csproj"

default:
    @just --list

# Hot-reload run, Debug, console attached. Ctrl-C to stop.
build_dev:
    dotnet watch --project {{app}} run --configuration Debug

# Release single-file exe in dist/
build:
    dotnet publish {{app}} -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
    @echo dist/PoeRedux.exe

run:
    dotnet run --project {{app}} -c Debug

check:
    dotnet build {{app}} -c Debug --nologo

clean:
    dotnet clean {{app}}
    if exist dist rmdir /s /q dist

# Is the game running, which would block any write
game-running:
    tasklist /nh /fi "imagename eq PathOfExile_x64.exe"

backup-status:
    @dir "%LOCALAPPDATA%\PoeRedux\Backups" 2>nul || echo no backup zip yet
