#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
DOTNET="${DOTNET:-dotnet}"
PYTHON="${PYTHON:-python3}"
"$PYTHON" Tests/Regression/extract_config.py
# Both configurations write Output/. Keep builds sequential and leave Release as the deliverable.
"$DOTNET" build Light/LightPluginMain.csproj -c Debug --nologo
"$DOTNET" build Light/LightPluginMain.csproj -c Release --nologo
"$DOTNET" build VerificationServer/VerificationServer.csproj -c Release --nologo
"$DOTNET" run --project Tests/Regression/Regression.csproj -- \
  VerificationServer/bin/Release/net10.0/LightVerificationServer.dll Output/Light.dll
