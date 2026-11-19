#!/usr/bin/env bash
# Switch the solution beside this script from NuGet to a sibling Sundex checkout.
# Use --restore to put back the solution and project files saved before switching.
set -euo pipefail

repo=$(git -C "$(dirname -- "$0")" rev-parse --show-toplevel)
cd -- "$(dirname -- "$0")"
backup=.sundex-local
if [[ "${1:-}" = --restore ]]; then
    if [[ ! -d "$backup" ]]; then
        printf '%s\n' 'No saved package references to restore.' >&2
        exit 1
    fi
    while IFS= read -r -d '' file; do
        cp -p -- "$file" "${file#"$backup"/}"
    done < <(find "$backup" -type f -print0)
    rm -r -- "$backup"
    exit 0
fi

if [[ -d "$backup" ]]; then
    printf '%s\n' 'A source switch already exists. Run UseLocalSundex.sh --restore first.' >&2
    exit 1
fi

checkout=$(realpath -e -- "${SUNDEX:-$repo/../Sundex}")
shopt -s nullglob
solutions=(*.slnx)
if (( ${#solutions[@]} != 1 )); then
    printf 'Expected one .slnx alongside %s.\n' "$0" >&2
    exit 1
fi
solution=${solutions[0]}

engine_props=$checkout/Sundex.Engine/buildTransitive/Sundex.Engine.props
generator_props=$checkout/Sundex.Markup.Generator/buildTransitive/Sundex.Markup.Generator.props
generator_project=$checkout/Sundex.Markup.Generator/Sundex.Markup.Generator.csproj
projects=("$checkout"/Sundex.*/*.csproj)
if [[ ! -f "$engine_props" || ! -f "$generator_props" || ! -f "$generator_project" ]]; then
    printf 'Sundex checkout at %s is missing its build files.\n' "$checkout" >&2
    exit 1
fi

append() {
    local content=${1//&/\\&}
    content=${content//#/\\#}
    sed -i "s#</Project>#  $content\\n</Project>#" "$2"
}

project_list=$(dotnet sln "$solution" list)
mkdir -- "$backup"
cp -p -- "$solution" "$backup/"
while IFS= read -r project; do
    [[ "$project" = *.csproj ]] || continue
    mapfile -t packages < <(sed -n 's/.*<PackageReference Include="\(Sundex\.[A-Za-z.]*\)".*/\1/p' "$project" | sort -u)
    (( ${#packages[@]} )) || continue
    for package in "${packages[@]}"; do
        if [[ ! -f "$checkout/$package/$package.csproj" ]]; then
            printf 'Missing source project for %s in %s.\n' "$package" "$checkout" >&2
            exit 1
        fi
    done

    cp -p --parents -- "$project" "$backup/"
    uses_generator=false
    dir=$(dirname -- "$project")
    for package in "${packages[@]}"; do
        dotnet remove "$project" package "$package"
        if [[ "$package" = Sundex.Markup.Generator ]]; then
            uses_generator=true
            continue
        fi
        dotnet add "$project" reference "$checkout/$package/$package.csproj"
        if [[ "$package" = Sundex.Markup || "$package" = Sundex.Markup.Legacy ]]; then
            uses_generator=true
        fi
    done

    if $uses_generator; then
        generator=$(realpath --relative-to="$dir" -- "$generator_project")
        props=$(realpath --relative-to="$dir" -- "$generator_props")
        append "<ItemGroup><ProjectReference Include=\"$generator\" OutputItemType=\"Analyzer\" ReferenceOutputAssembly=\"false\"/></ItemGroup>" "$project"
        append "<Import Project=\"\$(MSBuildThisFileDirectory)$props\"/>" "$project"
    fi
    props=$(realpath --relative-to="$dir" -- "$engine_props")
    append "<Import Project=\"\$(MSBuildThisFileDirectory)$props\"/>" "$project"
done <<< "$project_list"

dotnet sln "$solution" add --solution-folder Sundex "${projects[@]}"
