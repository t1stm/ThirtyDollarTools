#/bin/sh

rm -rf ./bin/Release

dotnet publish -c Release -r linux-x64
dotnet publish -c Release -r win-x64

dotnet publish -c Release -r osx-x64
dotnet publish -c Release -r osx-arm64
