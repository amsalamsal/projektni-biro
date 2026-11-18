#!/usr/bin/env bash
# Builds and tests everything that can run without AutoCAD and SketchUp, then assembles dist/.
# Requirements: .NET SDK 8 and 10, Ruby >= 2.7 with minitest, Python 3 with jsonschema,
# Node with Playwright + Chromium (UI tests; skipped when unavailable).
set -euo pipefail
cd "$(dirname "$0")/.."
step() { printf '\n=== %s ===\n' "$1"; }

step "3D models react to their parameters"
ruby tools/assets/check_params.rb | tail -1

step "AutoCAD command registration"
python3 tools/check_commands.py

step "C# build (Core, CLI, road engine for SketchUp, AutoCAD adapter for AutoCAD 2020-2027)"
dotnet build -c Release autocad-site-design-pro/src/SiteDesignPro.Cli
dotnet build -c Release autocad-site-design-pro/src/SiteDesignPro.Engine
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

step "Road engine for SketchUp: sample request -> package (read by the Ruby tests)"
ENGINE="dotnet autocad-site-design-pro/src/SiteDesignPro.Engine/bin/Release/net8.0/sdp-ceste.dll"
rm -rf out/roads-sample && mkdir -p out
$ENGINE build --in shared/samples/roads-request.json --out out/roads-sample || { cat out/roads-sample/result.json; exit 1; }
python3 tools/validate_schema.py out/roads-sample
python3 -c "import json;r=json.load(open('out/roads-sample/result.json'));print('engine:',r['summary']);assert r['ok']"
export SDP_ROADS_SAMPLE="$PWD/out/roads-sample"
export SDP_ROADS_ENGINE_TEST="dotnet $PWD/autocad-site-design-pro/src/SiteDesignPro.Engine/bin/Release/net8.0/sdp-ceste.dll"

step "Ruby tests (core, builders, change planning, SketchUp adapter against the API fake)"
for f in sketchup-site-design-pro/tests/test_*.rb; do [ "$(basename "$f")" = test_helper.rb ] && continue; ruby -Isketchup-site-design-pro/tests "$f" | grep -v '\[INFO\]' ; done

step "Ruby 2.0 syntax compatibility (SketchUp 2014-2016; newer Rubies run in the ruby-compat job)"
if ruby -e 'require "parser/ruby20"' 2>/dev/null; then
  RP=$(gem contents parser | grep 'bin/ruby-parse$' | head -1)
  find sketchup-site-design-pro/src -name '*.rb' -print0 | xargs -0 -n1 ruby "$RP" --20 >/dev/null
  echo "all extension files parse as Ruby 2.0"
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
