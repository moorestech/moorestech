#!/bin/bash
# moorestech_master内のPNGファイルをJPEGに変換するスクリプト
# Usage: ./convert_png_to_jpeg.sh [target_directory]
# Default: ../moorestech_master

set -euo pipefail

TARGET_DIR="${1:-$(cd "$(dirname "$0")/../../../../moorestech_master" && pwd)}"

if [ ! -d "$TARGET_DIR" ]; then
    echo "Error: Directory not found: $TARGET_DIR"
    exit 1
fi

echo "Scanning for PNG files in: $TARGET_DIR"

# PNG検索
# Find PNG files
PNG_FILES=$(find "$TARGET_DIR" -type f -name "*.png" 2>/dev/null)

if [ -z "$PNG_FILES" ]; then
    echo "No PNG files found. All assets are already in JPEG format."
    exit 0
fi

COUNT=0
while IFS= read -r png_file; do
    jpeg_file="${png_file%.png}.jpeg"

    echo "Converting: $(basename "$png_file") -> $(basename "$jpeg_file")"
    sips -s format jpeg "$png_file" --out "$jpeg_file" > /dev/null 2>&1
    rm "$png_file"
    COUNT=$((COUNT + 1))
done <<< "$PNG_FILES"

echo ""
echo "Done: $COUNT file(s) converted from PNG to JPEG."
