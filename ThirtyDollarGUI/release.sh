#/bin/sh

rm -rf ./bin/Release

dotnet publish -c Release -r linux-x64
dotnet publish -c Release -r win-x64

dotnet publish -c Release -r osx-x64
dotnet publish -c Release -r osx-arm64

cd ./bin/Release/net7.0/ || exit

rm */publish/*.pdb

for i in *; 
do 

zip -r -j -9 "$i.zip" "$i/publish"

echo "$i"; 

done;
