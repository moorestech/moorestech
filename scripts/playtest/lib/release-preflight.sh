#!/usr/bin/env bash
# release-playtest.sh のビルド前検査群。数十分のビルドや Steam アップロードの後で初めて落ちる事態を防ぐ
# 失敗は理由を stderr へ出して exit する（source 元の set -e 下で呼ぶ前提）
# Pre-build checks for release-playtest.sh, so nothing fails only after the lengthy build or the Steam upload
# Failures print the reason to stderr and exit (meant to be called under the sourcing script's set -e)

# ラベルは sed 置換・VDF・リモート PowerShell 文字列・パスへ埋め込むため、許可リストで検証する
# The label is embedded in sed substitutions, VDF, remote PowerShell strings and paths, so it is allowlist-validated
PLAYTEST_BUILD_LABEL_PATTERN='^[A-Za-z0-9][A-Za-z0-9._-]*$'

playtest_require_build_label() {
    if ! printf '%s' "$1" | grep -Eq "$PLAYTEST_BUILD_LABEL_PATTERN" || [ "$(printf '%s' "$1" | wc -l)" -ne 0 ]; then
        echo "ERROR: ビルドラベルが許可形式 ${PLAYTEST_BUILD_LABEL_PATTERN} に合いません: '$1'" >&2
        exit 2
    fi
}

# 必須envはビルド前に全部そろっているか見る。検証機・受け口の env もここで見ないとアップロード後に落ちる
# Check every required env before building; the check-machine/receiver env must be checked here too or it fails after upload
release_require_env() {
    local missing="" name
    for name in MOORESTECH_STEAM_USER MOORESTECH_STEAM_DEPOT_ID_WINDOWS MOORESTECH_STEAM_DEPOT_ID_MAC MOORESTECH_VERIFY_HOST \
        MOORESTECH_VERIFY_USER MOORESTECH_VERIFY_MAC PLAYTEST_ADMIN_KEY; do
        [ -n "${!name:-}" ] || missing="$missing $name"
    done
    if [ -n "$missing" ]; then
        echo "ERROR: 必須の環境変数が未設定です:$missing (~/hermes-agent/data/services/playtest/env.sh を読み込んでください)" >&2
        exit 2
    fi
    # depot id は VDF へそのまま埋め込むため数字だけを許す
    # Depot ids are embedded verbatim into the VDF, so only digits are allowed
    for name in MOORESTECH_STEAM_DEPOT_ID_WINDOWS MOORESTECH_STEAM_DEPOT_ID_MAC; do
        if ! printf '%s' "${!name}" | grep -Eq '^[0-9]+$' || [ "$(printf '%s' "${!name}" | wc -l)" -ne 0 ]; then
            echo "ERROR: ${name} は数字のみを許可します" >&2
            exit 2
        fi
    done
}

# コミット済み HEAD のピンから、指定キーの relativePath と commitHash を2行で出す（worktree 上のファイルは Unity が書き戻すため読まない）
# Print the relativePath and commitHash of a key from the committed pin; the worktree copy is rewritten by Unity, so it is not read
release_read_pin() {
    local worktree="$1" key="$2" fields
    fields="$("$GIT_BIN" -C "$worktree" show HEAD:.moorestech-external-revisions.json | python3 -c '
import json, sys
for revision in json.load(sys.stdin)["repositories"]:
    if revision["key"] == sys.argv[1]:
        print(revision["relativePath"]); print(revision["commitHash"]); sys.exit(0)
sys.exit(1)
' "$key")" || {
        echo "ERROR: worktree のコミット済みピンから ${key} の relativePath/commitHash を読めません: ${worktree}/.moorestech-external-revisions.json" >&2
        exit 3
    }
    printf '%s\n' "$fields"
}

# チェックアウトの HEAD がピンと一致し、未コミット変更が無いことを確かめる（dirty なまま焼くと出所を保証できない）
# Confirm a checkout's HEAD equals the pin and it has no uncommitted changes; baking a dirty tree loses provenance
release_require_pinned_clean_checkout() {
    local label="$1" root="$2" pinned="$3" head dirty
    head="$("$GIT_BIN" -C "$root" rev-parse HEAD)" || {
        echo "ERROR: ${label} の HEAD を読めません: ${root}（ピン ${pinned} のチェックアウトを用意してください）" >&2
        exit 3
    }
    if [ "$head" != "$pinned" ]; then
        echo "ERROR: ${label} がピンとずれています: ${root} は ${head}、ピンは ${pinned}。${root} を ${pinned} へ合わせてから再実行してください" >&2
        exit 3
    fi
    dirty="$("$GIT_BIN" -C "$root" status --porcelain)" || {
        echo "ERROR: ${label} の git status を読めません: ${root}" >&2
        exit 3
    }
    if [ -n "$dirty" ]; then
        echo "ERROR: ${label} に未コミットの変更があります（git status --porcelain が空ではありません）: ${root}" >&2
        exit 3
    fi
}

# 同梱元 master data は、moorestech_master clone の worktree のうち HEAD がピンと一致するものに一本化し
# MOORESTECH_MASTER_DATA_ROOT として Unity へ注入する。moores-wt の master_worktree_for と同じく一致する既存 worktree
# （メインclone含む）を再利用するため pin-<8桁> の名前は当てにせず、pin-*→detached→その他の順で clean なものを優先する
# The bundled master data is the moorestech_master worktree whose HEAD equals the pin, injected into Unity as
# MOORESTECH_MASTER_DATA_ROOT. Like moores-wt's master_worktree_for, any matching worktree (main clone included) is reused,
# so the pin-<8> name is not relied on; clean candidates are preferred in pin-*, detached, other order
release_resolve_master_data_root() {
    local worktree="$1" pin_fields pinned_commit candidates candidate
    pin_fields="$(release_read_pin "$worktree" moorestech_master)"
    pinned_commit="$(printf '%s\n' "$pin_fields" | sed -n 2p)"
    candidates="$("$GIT_BIN" -C "$MASTER_CLONE" worktree list --porcelain | python3 -c '
import os, sys
entries, current = [], {}
for line in sys.stdin.read().splitlines():
    if line.startswith("worktree "):
        current = {"path": line[9:], "head": None, "detached": False}
        entries.append(current)
    elif line.startswith("HEAD ") and current:
        current["head"] = line[5:].strip()
    elif line == "detached" and current:
        current["detached"] = True
matches = [e for e in entries if e["head"] == sys.argv[1]]
matches.sort(key=lambda e: 0 if os.path.basename(e["path"]).startswith("pin-") else (1 if e["detached"] else 2))
print("\n".join(e["path"] for e in matches))
' "$pinned_commit")" || {
        echo "ERROR: master data clone の worktree 一覧を読めません: ${MASTER_CLONE}（MOORESTECH_MASTER_CLONE を確認してください）" >&2
        exit 3
    }
    if [ -z "$candidates" ]; then
        echo "ERROR: ピン ${pinned_commit} を HEAD に持つ master data worktree がありません（${MASTER_CLONE} の worktree 一覧）。moores-wt new で pin worktree を作るか、git -C ${MASTER_CLONE} worktree add --detach <path> ${pinned_commit} で用意してください" >&2
        exit 3
    fi
    # 候補の中で未コミット変更の無いものを選ぶ。全部 dirty なら先頭を選び、下の検査で理由付きで止める
    # Pick the first candidate without uncommitted changes; if all are dirty take the first and let the check below stop with a reason
    MASTER_DATA_ROOT="$(printf '%s\n' "$candidates" | sed -n 1p)"
    while IFS= read -r candidate; do
        if [ -z "$("$GIT_BIN" -C "$candidate" status --porcelain 2>/dev/null)" ]; then
            MASTER_DATA_ROOT="$candidate"
            break
        fi
    done <<EOF
$candidates
EOF
    release_require_pinned_clean_checkout "同梱元の master data" "$MASTER_DATA_ROOT" "$pinned_commit"
    export MOORESTECH_MASTER_DATA_ROOT="$MASTER_DATA_ROOT"
}

# 非公開アセットがピンどおりで、Windows/Mac の ffmpeg 実体と LICENSE が揃うことを確かめる
# Confirm the private assets match the pin and both Windows/Mac ffmpeg binaries and licenses exist
release_require_private_assets() {
    local worktree="$1" pin_fields private_root
    pin_fields="$(release_read_pin "$worktree" moorestech_client_private)"
    private_root="$worktree/$(printf '%s\n' "$pin_fields" | sed -n 1p)"
    release_require_pinned_clean_checkout "非公開アセット" "$private_root" "$(printf '%s\n' "$pin_fields" | sed -n 2p)"
    release_require_ffmpeg_source "$private_root/ffmpeg/win-x64" ffmpeg.exe
    release_require_ffmpeg_source "$private_root/ffmpeg/macos-arm64" ffmpeg
}

# ffmpeg の実体と LICENSE を検査し、LFS 未解決の殻を拒否する
# Check the real ffmpeg and license, rejecting an unresolved LFS pointer
release_require_ffmpeg_source() {
    local ffmpeg_dir="$1" executable="$2"
    if [ ! -f "$ffmpeg_dir/$executable" ] || { [ "$(wc -c <"$ffmpeg_dir/$executable")" -le 1024 ] &&
        [ "$(head -c 23 "$ffmpeg_dir/$executable")" = "version https://git-lfs" ]; }; then
        echo "ERROR: ffmpeg の実体が無いか LFS ポインタのままです: ${ffmpeg_dir}/${executable}（git lfs pull を確認してください）" >&2
        exit 3
    fi
    if [ ! -f "$ffmpeg_dir/LICENSE" ]; then
        echo "ERROR: ffmpeg の LICENSE がありません: ${ffmpeg_dir}/LICENSE" >&2
        exit 3
    fi
}
