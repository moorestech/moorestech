#!/usr/bin/env bash
# テンプレートを実値へ置き換え、run の steam/ へ書く。depot id は repo へ書かない
# Render templates under the run's steam/; depot ids are account-specific and never committed

release_render_steam_vdfs() {
    local run_dir="$1" steam_dir="$2" template escaped_run_dir
    # パスに現れない制御文字を sed の区切り文字にする
    # Use a control character absent from paths as the sed delimiter
    local d=$'\x01'
    if [[ "$run_dir" == *"$d"* ]]; then
        echo "ERROR: RUN_DIR に VDF 置換用の制御文字が含まれています" >&2
        exit 4
    fi
    # sed の置換値では \ と & が特別な意味を持つためパス中の両方を逃がす
    # Escape backslash and ampersand because both have special meaning in sed replacements
    escaped_run_dir="${run_dir//\\/\\\\}"
    escaped_run_dir="${escaped_run_dir//&/\\&}"
    for template in app_build_playtest.vdf depot_build_windows.vdf depot_build_mac.vdf; do
        sed -e "s${d}__BUILD_LABEL__${d}${BUILD_LABEL}${d}g" \
            -e "s${d}__RUN_DIR__${d}${escaped_run_dir}${d}g" \
            -e "s${d}__CONTENT_ROOT_WINDOWS__${d}${escaped_run_dir}/build-windows${d}g" \
            -e "s${d}__CONTENT_ROOT_MAC__${d}${escaped_run_dir}/build-mac${d}g" \
            -e "s${d}__DEPOT_ID_WINDOWS__${d}${MOORESTECH_STEAM_DEPOT_ID_WINDOWS}${d}g" \
            -e "s${d}__DEPOT_ID_MAC__${d}${MOORESTECH_STEAM_DEPOT_ID_MAC}${d}g" \
            "$SCRIPT_DIR/steam/$template" >"$steam_dir/$template"
    done
}
