#!/usr/bin/env bash
# 配布 run の確保までを焼き方に依らず共有する（必須env・ラベル・Steam資格・コミット解決・配布元 ref・RUN_DIR の排他確保）
# 呼び出し側が SCRIPT_DIR・GIT_BIN・COMMIT・BUILD_BRANCH・PLAYTEST_RUN_ROOT を用意し、COMMIT_FULL・BUILD_LABEL・RUN_DIR・STEAM_DIR を受け取る
# Shares everything up to claiming the release run regardless of where the players are baked (required env, label, Steam auth, commit, distribution ref, exclusive RUN_DIR)
# Callers provide SCRIPT_DIR, GIT_BIN, COMMIT, BUILD_BRANCH and PLAYTEST_RUN_ROOT and receive COMMIT_FULL, BUILD_LABEL, RUN_DIR and STEAM_DIR

release_claim_run() {
    release_require_env
    BUILD_LABEL="${MOORESTECH_STEAM_BUILD_LABEL:-playtest-$(date +%Y%m%d-%H%M)}"
    playtest_require_build_label "$BUILD_LABEL"
    # 保存資格が無ければ長時間ビルドへ進まない。
    # Reject missing cached authentication before the lengthy build.
    python3 "$SCRIPT_DIR/steam_auth/check_login.py" "$MOORESTECH_STEAM_USER"

    # 解決の前にfetchする。マージ直後のコミットや古いorigin/masterのrefを掴まないため
    # Fetch before resolving, so a just-merged commit or a stale origin/master ref is never used
    "$GIT_BIN" -C "$SCRIPT_DIR" fetch origin || {
        echo "ERROR: git fetch origin に失敗しました" >&2
        exit 2
    }

    # 短縮SHAやブランチ名でも受け付けられるよう、比較の前に40桁へ解決する
    # Resolve the input to a 40-char SHA before any comparison, so a short SHA or branch name still works
    COMMIT_FULL="$("$GIT_BIN" -C "$SCRIPT_DIR" rev-parse --verify "${COMMIT}^{commit}" 2>/dev/null)" || {
        echo "ERROR: COMMIT を解決できません（存在しないコミットか、このrepoにfetchされていません）: ${COMMIT}" >&2
        exit 2
    }
    # 焼く branch が嘘にならないよう、要求コミットが配布元 ref から到達できることをビルド前に確かめる
    # merge-base --is-ancestorは「祖先でない」を1、「refが無い・objectが無い」を128で返すため、終了コードで理由を分ける
    # （128のまま「含まれない」と出すと、ブランチ名の打ち間違いをコミットの取り違えと誤診させてしまう）
    # Confirm before the build that the requested commit is reachable from the distribution ref, so the baked branch never lies
    # merge-base --is-ancestor returns 1 for "not an ancestor" and 128 for "ref/object missing"; branch on the exit code
    # (reporting 128 as "not contained" would misdirect a typo'd branch name toward a commit mixup instead)
    if "$GIT_BIN" -C "$SCRIPT_DIR" merge-base --is-ancestor "$COMMIT_FULL" "origin/$BUILD_BRANCH"; then
        ANCESTOR_STATUS=0
    else
        ANCESTOR_STATUS=$?
    fi
    if [ "$ANCESTOR_STATUS" -eq 128 ]; then
        echo "ERROR: origin/${BUILD_BRANCH} が見つかりません（MOORESTECH_BUILD_BRANCH を確認してください）" >&2
        exit 2
    elif [ "$ANCESTOR_STATUS" -ne 0 ]; then
        echo "ERROR: ${COMMIT_FULL} は origin/${BUILD_BRANCH} に含まれません（配布元 ref が違うなら MOORESTECH_BUILD_BRANCH で指定してください）" >&2
        exit 2
    fi

    RUN_DIR="$PLAYTEST_RUN_ROOT/$BUILD_LABEL"
    STEAM_DIR="$RUN_DIR/steam"
    # 並行起動と前回成果物の混入を防ぐため、runを排他的に確保する
    # Claim the run exclusively to prevent concurrent launches and stale artifact reuse
    if ! mkdir -p -- "$PLAYTEST_RUN_ROOT"; then
        echo "ERROR: RUN_DIR の親を作成できません（IO失敗）: ${RUN_DIR}" >&2
        exit 2
    fi
    if ! mkdir -- "$RUN_DIR"; then
        echo "ERROR: RUN_DIR を確保できません（既存・同時確保・IO失敗）: ${RUN_DIR}" >&2
        exit 2
    fi
    mkdir -p "$RUN_DIR/build-windows" "$RUN_DIR/build-mac" "$STEAM_DIR/output"
    echo "[release-playtest] label=$BUILD_LABEL commit=$COMMIT_FULL branch=$BUILD_BRANCH run=$RUN_DIR"
}
