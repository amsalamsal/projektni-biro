#!/usr/bin/env bash
# Builds and tests everything that can run without AutoCAD and SketchUp, then assembles dist/.
# Requirements: .NET SDK 8 and 10, Ruby >= 2.7 with minitest, Python 3 with jsonschema,
# Node with Playwright + Chromium (UI tests; skipped when unavailable).
set -euo pipefail
cd "$(dirname "$0")/.."
step() { printf '\n=== %s ===\n' "$1"; }

step "C# build (Core, CLI, AutoCAD adapter for AutoCAD 2025/2026 and 2027)"
dotnet build -c Release autocad-site-design-pro/src/SiteDesignPro.Cli
dotnet build -c Release autocad-site-design-pro/src/SiteDesignPro.AutoCAD

step "C# unit tests"
dotnet test -c Release autocad-site-design-pro/tests/SiteDesignPro.Core.Tests

step "Sample package regeneration (deterministic) and schema validation"
SDP="dotnet autocad-site-design-pro/src/SiteDesignPro.Cli/bin/Release/net8.0/sdp.dll"
$SDP sample --out shared/samples/residential-complex.sdpkg
$SDP sample --out shared/samples/residential-complex
python3 tools/validate_schema.py shared/samples/residential-complex.sdpkg shared/samples/residential-complex
python3 - <<'PY'
import json, jsonschema
d = json.load(open('shared/samples/residential-complex-changes.json'))
jsonschema.Draft202012Validator(json.load(open('shared/schema/changes.schema.json'))).validate(d)
print('valid   shared/samples/residential-complex-changes.json :: changes.schema.json')
PY
git diff --quiet -- shared/samples || echo "NOTE: sample packages changed - review and commit them."

step "Ruby tests (core, builders, change planning, SketchUp adapter against the API fake)"
for f in sketchup-site-design-pro/tests/test_*.rb; do [ "$(basename "$f")" = test_helper.rb ] && continue; ruby -Isketchup-site-design-pro/tests "$f" | grep -v '\[INFO\]' ; done

step "Ruby 2.7 syntax compatibility (SketchUp 2021-2023)"
if ruby -e 'require "parser/ruby27"' 2>/dev/null; then
  RP=$(gem contents parser | grep 'bin/ruby-parse$' | head -1)
  find sketchup-site-design-pro/src -name '*.rb' -print0 | xargs -0 -n1 ruby "$RP" --27 >/dev/null
  echo "all extension files parse as Ruby 2.7"
else
  echo "parser gem not installed (gem install parser) - skipped"
fi

step "SketchUp dialog UI tests (Chromium)"
if node -e "require('playwright')" 2>/dev/null || NODE_PATH=$(npm root -g) node -e "require('playwright')" 2>/dev/null; then
  ruby sketchup-site-design-pro/tests/ui/make_fixtures.rb
  NODE_PATH=$(npm root -g) node sketchup-site-design-pro/tests/ui/dialogs.test.js
else
  echo "playwright not available - skipped"
fi

step "Packages"
python3 tools/package.py Release
