#!/usr/bin/env sh
# Gera os executáveis (arquivo único, sem precisar instalar .NET) em dist/:
#   dist/windows-x64/GestaoComercial.exe     dist/linux-x64/GestaoComercial
# Requer o .NET SDK 8 (https://dotnet.microsoft.com/download). Funciona em Linux, macOS e Windows (Git Bash).
# Uso: ./publicar.sh            (padrão: abre mais rápido, ~120 MB)
#      COMPRIMIR=1 ./publicar.sh (arquivo ~55 MB, porém abre ~2x mais devagar)
set -e
cd "$(dirname "$0")"
COMP=false; [ "$COMPRIMIR" = "1" ] && COMP=true
for RID in win-x64 linux-x64; do
  case $RID in win-x64) PASTA=windows-x64;; linux-x64) PASTA=linux-x64;; esac
  echo "==> Publicando $RID ..."
  dotnet publish src/GestaoComercial.App -c Release -r $RID --self-contained true \
    -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=$COMP -p:DebugType=none -o dist/$PASTA
done
echo; echo "Pronto:"; ls -lh dist/*/GestaoComercial*
