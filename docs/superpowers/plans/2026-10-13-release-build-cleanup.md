# Steamアップロード成功後のビルド本体削除 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。ステップはチェックボックス記法で書く。

**Goal:** 成功したSteamアップロードの直後に、そのrunのWindows/Macビルド本体を削除してディスク占有を減らす。

**Architecture:** release-playtest.shの既存の直列パイプラインに、steamcmd正常終了直後の局所的な削除処理を加える。削除失敗は警告して続行し、終了時worktree破棄trapとは分ける。外部コマンドをスタブ化する既存契約テストで順序と保持範囲を検証する。

**Tech Stack:** Bash 3.2、既存shell契約テスト、GitHub CLI。

## Requirements

- steamcmd +run_app_buildが正常終了した直後、当該RUN_DIR/build-windowsとRUN_DIR/build-macだけが消える。Windows検証開始時に既に両方存在しない。
- ログ・steam/・verify/・promotion.mdを削除しない。正常系はpromotion.md生成まで完了する。他runのビルド本体も保持する。
- アップロード失敗・Windows/Macのビルド失敗・成果物検査失敗ではビルド本体を保持し、調査できる。
- Steam成功後のWindows検証失敗でも両ビルド本体は削除済みとする。promotion.md生成は従来どおり検証成功時だけ。
- 削除失敗は警告と残存パスを示し、Windows検証を続ける。他工程が成功すれば正常終了する。
- 過去runの一括削除や実Steamへのビルド・アップロードを今回の検証で実行しない。

## Global Constraints

- 作業場所: /Users/sakastudio/hermes-agent/data/worktrees/moorestech/release-build-cleanup。土台origin/master d5110abb04。
- バックグラウンドのポーリングで待たず全部前景で実行する。区切りごとに日本語でコミットする。
- コードは200行未満。空行・コメントの圧縮で行数制限を回避しない。
- .cs変更はないためUnityコンパイル・ゲーム録画は対象外。秘密envと実runには触れない。

## 設計検査記録

- 配置検査（Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 既存のrelease orchestratorと契約テストへ局所追加。
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱0・第3バケツ0 / fresh-context検査で発火なし

## 配置と前例

| ファイル | 責務 | 前例 |
| --- | --- | --- |
| scripts/playtest/release-playtest.sh | ビルド→検査→アップロード→本体削除→検証→反映手順生成 | 同ファイルのsteamcmd呼び出しとworktree破棄trap |
| scripts/playtest/tests/test-release-playtest.sh | 成功・アップロード失敗・検証失敗・削除失敗の契約 | 既存のmake_sandbox/run_target/fail |
| scripts/playtest/tests/test-release-playtest-mac.sh | Macを含む成功時削除と検査失敗時保持 | 既存のFAILURE_CASES |
| scripts/playtest/tests/lib/release-playtest-sandbox.sh | 外部境界スタブと証拠生成 | 既存steamcmd/verifyスタブ |
| scripts/playtest/README.md | 運用上の保持・削除条件 | 既存「配布工程」節 |

データフロー: Unity→artifact検査→VDF生成→steamcmd成功→2つの固定子パス削除→Steam取得でWindows検証→promotion.md。
既存操作: 失敗runの調査は保持、Steam経由の検証と手動反映は保持、成功runのローカルビルド再利用は裁定に従い廃止。

### Task 1: 削除処理と契約テスト・運用説明

**Files:**
- Modify: `scripts/playtest/release-playtest.sh`
- Modify: `scripts/playtest/tests/test-release-playtest.sh`
- Modify: `scripts/playtest/tests/test-release-playtest-mac.sh`
- Modify: `scripts/playtest/tests/lib/release-playtest-sandbox.sh`
- Modify: `scripts/playtest/README.md`

**Interfaces:**
- Consumes: 既存RUN_DIRと、正常終了したSTEAMCMD_BIN。run_targetは終了コードと出力を返す。
- Produces: 両ビルド本体削除（成功時）、削除失敗の警告と残存パス（失敗時）。新規公開API・設定・汎用cleanup基盤は作らない。

- [ ] **Step 1: steamcmd成功直後に次の処理を加える**

```bash
# Steamに格納済みの本体だけを消し、調査ログと検証資料は残す
# Remove only uploaded build payloads, keeping logs and verification evidence
if rm -rf -- "$RUN_DIR/build-windows" "$RUN_DIR/build-mac"; then
    echo "[release-playtest] uploaded build payloads removed: $RUN_DIR/build-windows $RUN_DIR/build-mac"
else
    echo "WARNING: アップロード済みビルド本体の削除に失敗しました" >&2
    for build_dir in "$RUN_DIR/build-windows" "$RUN_DIR/build-mac"; do
        [ ! -e "$build_dir" ] && [ ! -L "$build_dir" ] || echo "WARNING: 残存パス: $build_dir" >&2
    done
fi
```

- [ ] **Step 2: 既存スタブと契約テストを更新する**

steamcmdスタブで両本体が存在する時点を検査する。verifyスタブで両本体の削除済み状態を検査し、ログ/steam出力/verify資料のsentinelを作る。削除失敗はPATH先頭のsandbox/bin/rmスタブで当該ビルド本体だけのrmを失敗させ、それ以外は実rmへ委譲する。初回rmでWindowsだけ削除する部分失敗もテストする。新しい本番RM_BIN設定は不要。

```bash
for os in windows mac; do
    [ ! -e "$run/build-$os" ] || fail "$os build survived upload success"
done
[ -f "$run/unity-build-windows.log" ] || fail "Windows log was deleted"
[ -f "$run/steam/output/upload.log" ] || fail "Steam output was deleted"
[ -f "$run/verify/result.json" ] || fail "verification evidence was deleted"
[ -f "$other_run/build-windows/keep" ] || fail "another run was changed"
```

既存Mac成功テストの「ビルドディレクトリが残る」期待はアップロード時点の存在確認と終了時の不在確認へ置き換える。UNITY_EXIT、UNITY_MAC_EXIT、BUILD_INFO_TARGET_WINDOWS、BUILD_INFO_TARGET_MAC、STEAMCMD_EXITの失敗ケースでは作成済み本体の保持を検査する。VERIFY_EXIT失敗では本体不在・資料保持・promotion.md不在を検査する。削除の全失敗と部分失敗では警告・残存パス・verify実行・正常終了を検査する。既存ファイルが200行以上になる場合は責務ごとにtests/lib配下へ分割し、入口test-release-playtest*.shからsourceする。

- [ ] **Step 3: READMEへ成功直後削除・アップロード前失敗保持・後続検証失敗時削除済み・削除失敗警告続行を記す**

- [ ] **Step 4: 契約テストと構文検査を前景実行する**

```bash
for test_file in scripts/playtest/tests/test-release-playtest*.sh; do
    bash "$test_file" || exit
done
bash -n scripts/playtest/release-playtest.sh scripts/playtest/tests/test-release-playtest*.sh scripts/playtest/tests/lib/release-playtest*.sh
git diff --check
```

Expected: release-playtest/mac/origin契約すべてPASS、構文エラー・差分空白エラーなし。

- [ ] **Step 5: 実装・テスト・運用説明をコミットする**

```bash
git add scripts/playtest/release-playtest.sh scripts/playtest/tests scripts/playtest/README.md
git commit -m "feat: Steamアップロード成功後にビルド本体を自動削除する"
```

### Task 2: 最終レビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行する。Codexホストではreferences/codex-host.mdのrunnerで同じ台本を実行する。外部監査3本は先に前景で完了させ、runner内のバックグラウンド待機を発生させない。
- [ ] 指摘反映後に変更した条件経路の契約テストを再実行し、反映内容をコミットする。最終bug-passも実行する。

### Task 3: PR作成と終了

- [ ] pr-createスキルでPRと報告ページを作成し、masterとのコンフリクトがあれば解消・検証・pushする。
- [ ] 全変更がコミット・push済みでPRがマージ可能なことを確認する。moores-wt rm release-build-cleanupでworktreeを畳む。

## 判断記録（ADR）

- 削除時点と保持範囲: ユーザー裁定 2026-10-13 原文「Steam へのアップロード（steamcmd +run_app_build）が成功したら、その RUN_DIR の build-windows と build-mac（各約6GB、mac-mini のディスクを圧迫している）を自動で消す」「ログ・steam/・verify/・promotion.md は残す想定。アップロード失敗やビルド検査失敗で止まった run は調査用に残す想定」。後続検証失敗でも削除する質問への回答「それで」。[[2026-10-13-Steamアップロード成功直後に配布ビルド本体を削除する]]。
- 削除失敗時: ユーザー裁定 2026-10-13「それで、早くすすめて」。質問「削除に失敗した場合は、警告と残存パスを出して Windows 検証を続ける方針でよいですか？ 残った容量の回収は手対応になります」への承認。[[2026-10-13-配布ビルド本体の削除失敗は警告して検証を続ける]]。
- agent前提（既存set -euo pipefailと正常終了契約）: steamcmdの終了コード0を既存と同じ成功境界とする。Steam成功判定自体の刷新は行わない。
- agent前提（責務の前例）: 削除を既存orchestratorへ直接置く。汎用ライブラリ・オプションは追加しない。
- agent前提（変更対象）: Unityゲーム実行時の挙動には触れないため、unityプレイ録画テスト・Unityコンパイルは実施せずshell契約テストで検証する。
- 独立質問generate/filter: Q1は裁定で回答済み、残問Q2はユーザーが警告続行を承認して解消。

- user-simulator review: 指摘・Warning・要裁定0件。後段の本体非依存と既存テスト波及、削除失敗の続行を確認。
