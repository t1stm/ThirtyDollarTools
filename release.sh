#!/bin/bash

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
NET_FOLDER="net8.0"
platforms=("linux-x64" "win-x64" "osx-x64" "osx-arm64")

publish() {
  for platform in "${platforms[@]}"; do
  
  dotnet publish -c Release -r "$platform" --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
  
  done;
  
  cd ./bin/Release/$NET_FOLDER/ || exit
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
    cp -r "$SCRIPT_DIR/ThirtyDollarConverter.GUI/bin/Release/$NET_FOLDER/$platform/publish/" "$SCRIPT_DIR/bin/$platform" || exit
  done;
}

zip_releases() {
  for platform in "${platforms[@]}"; do
    cd "$SCRIPT_DIR/bin/$platform/publish" || exit
    rm -rf "./runtimes" || exit
    zip -r9 "$SCRIPT_DIR/bin/$platform.zip" "."
    cd - || exit
  done;
}

if [ "$#" -gt 0 ]; then
  for arg in "$@"; do
    if [ "$arg" == "--zip-only" ]; then
      zip_releases
      exit
    fi  
  done;
fi

rm -rf "${SCRIPT_DIR:?}/bin"
mkdir "$SCRIPT_DIR/bin"

cd ./ThirtyDollarConverter.GUI || exit
publish

cd "$SCRIPT_DIR" || exit

cd ./ThirtyDollarVisualizer || exit
publish

create_release_dirs
copy_releases

if [ "$#" -gt 0 ]; then
  for arg in "$@"; do
    if [ "$arg" == "-z" ]; then
      zip_releases
    fi  
  done;
fi
