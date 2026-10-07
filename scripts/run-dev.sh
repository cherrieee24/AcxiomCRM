#!/usr/bin/env bash
# Starts AcxiomCRM in Development (demo users + sample data) on http://localhost:5063
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project src/AcxiomCRM/AcxiomCRM.csproj --launch-profile http
