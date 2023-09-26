#!/bin/bash

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
NET_FOLDER="net7.0"

rm -rf "${SCRIPT_DIR:?}/bin"
mkdir "$SCRIPT_DIR/bin"

platforms=("linux-x64" "win-x64" "osx-x64" "osx-arm64")

publish() {
  for platform in "${platforms[@]}"; do
  
  dotnet publish -c Release -r "$platform" --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
  
  done;
  
  cd ./bin/Release/net7.0/ || exit
  rm ./*/publish/*.pdb
}

create_release_dirs() {
  for platform in "${platforms[@]}"; do
    mkdir "$SCRIPT_DIR/bin/$platform";
  done;
}

copy_releases() {
  for platform in "${platforms[@]}"; do
    cp -r "$SCRIPT_DIR/ThirtyDollarVisualizer/bin/Release/$NET_FOLDER/$platform/publish/" "$SCRIPT_DIR/bin/$platform" || exit
    cp -r "$SCRIPT_DIR/ThirtyDollarGUI/bin/Release/$NET_FOLDER/$platform/publish/" "$SCRIPT_DIR/bin/$platform" || exit
  done;
}

zip_releases() {
  for platform in "${platforms[@]}"; do
    zip -rj9 "$SCRIPT_DIR/bin/$platform.zip" "$SCRIPT_DIR/bin/$platform/"
  done;
}

cd ./ThirtyDollarGUI || exit
publish

cd "$SCRIPT_DIR" || exit

cd ./ThirtyDollarVisualizer || exit
publish

create_release_dirs
copy_releases
zip_releases