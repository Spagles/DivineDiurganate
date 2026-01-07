#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${1:-Debug}"

cd "$(dirname "$0")/.."

dotnet --version
dotnet restore "Source/DivineDiurganate/DivineDiurganate.sln"
dotnet build "Source/DivineDiurganate/DivineDiurganate.sln" -c "$CONFIGURATION"

