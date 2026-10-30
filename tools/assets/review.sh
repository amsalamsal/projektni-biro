#!/usr/bin/env bash
# Rebuilds, renders and montages the models of one library folder for visual review.
# Usage: tools/assets/review.sh <library-dir> <Folder> <id-regex> <out.png>
set -euo pipefail
cd "$(dirname "$0")/../.."
ids=$(ruby -e 'require "./sketchup-site-design-pro/src/pb_site_design_pro/core"; require "json"; t = JSON.parse(File.read("shared/catalog/element-types.json"))["types"].map { |x| x["id"] }; puts t.grep(Regexp.new(ARGV[0])).join(" ")' "$3")
VERBOSE=1 ruby tools/assets/build_library.rb "$1" $ids 2>&1 | grep -v "Uvezeno u biblioteku" || true
node tools/assets/render_previews.js "$1" 320 $ids | tail -1
python3 tools/assets/contact_sheet.py "$1" "$2" "$4" 6
