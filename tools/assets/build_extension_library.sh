#!/usr/bin/env bash
# Regenerates the model library shipped inside the SketchUp extension:
#   sketchup-site-design-pro/src/pb_site_design_pro/Assets/<Folder>/<asset-id>/{model.dae, asset.json, preview.jpg}
#   .../Assets/library.json, .../Assets/inventory.json
# Needs Ruby, Node and (for previews) three.js in tools/assets/node_modules plus Playwright with Chromium.
# Usage: tools/assets/build_extension_library.sh
set -euo pipefail
cd "$(dirname "$0")/../.."
out=sketchup-site-design-pro/src/pb_site_design_pro/Assets
python3 tools/assets/make_textures.py >/dev/null
rm -rf "$out"
SDP_SHARED_TEXTURES=1 SDP_PREVIEW=preview.jpg ruby tools/assets/build_library.rb "$out"
SDP_PREVIEW=preview.jpg node tools/assets/render_previews.js "$out" 256
find "$out" -name mesh.json -delete
du -sh "$out"
