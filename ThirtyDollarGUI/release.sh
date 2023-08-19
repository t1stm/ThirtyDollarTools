#/bin/sh

rm -rf ./bin/Release

platforms=("linux-x64" "win-x64" "osx-x64" "osx-arm64")

for platform in "${platforms[@]}"; do

dotnet publish -c Release -r "$platform" --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

done;

cd ./bin/Release/net7.0/ || exit

rm */publish/*.pdb

for i in *; 
do 

zip -r -j -9 "$i.zip" "$i/publish"

echo "$i"; 

done;
