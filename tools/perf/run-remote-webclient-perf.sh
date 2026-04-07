#!/usr/bin/env bash
set -euo pipefail

pushd tests/ProDiagnostics.Remote.WebClient.Perf >/dev/null
if [[ ! -d node_modules ]]; then
  npm install
fi
npm run install:browsers --silent
npm test "$@"
popd >/dev/null
