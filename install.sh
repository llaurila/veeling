#!/usr/bin/env bash

set -euo pipefail

dotnet pack -c Release
dotnet tool install --global --add-source ./Veeling.CLI/nupkg veeling
