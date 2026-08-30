#!/usr/bin/env bash
set -Eeuo pipefail

mkdir -p /tmp/inkflow
tar --exclude='.env' --exclude='.env.*' --exclude='.git' -C /workspace -cf - . \
  | tar --no-same-owner -C /tmp/inkflow -xf -
cd /tmp/inkflow

exec dotnet run \
  --property:UseAppHost=false \
  --project tools/InkFlow.AcceptanceFixtures/InkFlow.AcceptanceFixtures.csproj \
  --configuration Release \
  -- "$@"
