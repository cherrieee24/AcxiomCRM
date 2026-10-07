#!/usr/bin/env bash
# Builds a Release, framework-dependent publish of AcxiomCRM into ./artifacts/publish
# Usage: scripts/publish.sh
set -euo pipefail
cd "$(dirname "$0")/.."

OUT=artifacts/publish
rm -rf "$OUT"
dotnet publish src/AcxiomCRM/AcxiomCRM.csproj -c Release -o "$OUT"

echo
echo "Published to $OUT"
echo "Run with:  ASPNETCORE_ENVIRONMENT=Production dotnet $OUT/AcxiomCRM.dll"
