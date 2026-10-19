#!/usr/bin/env sh
cd "$(dirname "$0")" && exec dotnet run --project src/GestaoComercial.App -c Release
