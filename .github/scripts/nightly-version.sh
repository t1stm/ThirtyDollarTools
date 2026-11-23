#!/usr/bin/env bash

branch=$1
# Nightly tags are reused, so the desktop update check compares build dates.
jq -n --arg tag "nightly-$branch" --arg title "Nightly Build of [$branch]" \
  --arg date "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
  '{tag: $tag, releaseTitle: $title, nightly: true, prerelease: true, date: $date}'
