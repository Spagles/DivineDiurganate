#!/usr/bin/env bash
set -euxo pipefail

cd "$(dirname "$0")/.."

DOTNET_INSTALL_DIR="${HOME}/.dotnet"
DOTNET_INSTALL_SCRIPT="/tmp/dotnet-install.sh"

curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${DOTNET_INSTALL_SCRIPT}"
bash "${DOTNET_INSTALL_SCRIPT}" --version 8.0.416 --install-dir "${DOTNET_INSTALL_DIR}"
export PATH="${DOTNET_INSTALL_DIR}:$PATH"

dotnet --version

# 预热还原（避免首次构建慢/失败）
dotnet restore "Source/DivineDiurganate/DivineDiurganate.sln"
