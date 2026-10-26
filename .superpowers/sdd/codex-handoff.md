# Codex 引き継ぎ: UITK段階0 0-7 プレイテストDSLの描画系非依存化（レーン F）

- bd: `moorestech-albof.6`（claim 済み。区切りごとに `bd note moorestech-albof.6 "…"`）
- ブランチ / worktree: `feature/uitk-s0-dsl` / `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/uitk-s0-dsl`
- plan: `docs/superpowers/plans/2026-10-26-uitk-s0-playtest-dsl-locator.md`（`## Requirements`・`## Global Constraints`・`## 判断記録（ADR）` を全タスク共通の制約として扱う）
- 台帳: `.superpowers/sdd/progress.md`（モード: SDD本体＝タスクごと派遣。実装 6 タスク・約 60 ファイル）
- 実行メモ: `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/sdd/feature-uitk-s0-dsl/memo.md`
- Task 1 ブリーフ: `.superpowers/sdd/task-1-brief.md`（task-brief で抽出済み）

## 済んだもの・残り

- Task 1（基準測定）: **途中**。`docs/uitk-migration/stage0-dsl-parity.md` に 30 行の枠があり、15 本の基準列を記録・コミット済み（45bad4c..3b84a55）。Task 1 の報告ファイルはまだ無い
  - 未測定の 15 本（表で「未測定」の行）: `playtest-receiver/developer-report-delivery` `train/fluid-inventory-via-interact` `building/blueprint-large-paste-flicker` `building/blueprint-place-like-block` `building/blueprint-terrain-sink-partial-paste` `connect/electric-wire-tool-isolated-place-via-ui` `connect/electric-wire-tool-pole-cycle-rotate-via-ui` `connect/electric-wire-tool-pole-ghost-hide-paths-check` `connect/electric-wire-tool-pole-ghost-visibility-check` `misc/localization-language-switch-via-ui` `misc/walk-while-menus-open` `misc/webui-buildmenu-click` `train/train-station-default-name` `train/train-timetable-via-ui` `graphics-quality/quality-recovery-ui`
  - 測り済みの行は流し直さない。基準で失敗しているもの（`pause-menu-hierarchy`・`rotary-generator-ui`・コンパイル失敗の `quality-ui-switching`・`vein-hand-mining-smoke` 等）も、そのまま正しい基準値
  - 基準測定は **未変更の C# と Web UI** で行う必要がある。Task 2（`bridge/playtest/` の変更）は Vite dev server 経由で PlayMode に即反映されるので、Task 1 の測定を終えてから着手する
  - 測定が終わったらブリーフ Step 3 のメッセージでコミットし、bd note を書く
- Task 2〜6（実装）: 未着手。Task 7（録画 2 本）・Task 8（moores-code-review）・最終手順: 未着手

## 作業ツリーの未コミットの変更

- `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` のみ。Unity がコンパイル時に自動で書き換えた印の文字列で、このレーンの作業の途中ではない。**捨ててよい**（コミットしない。`git checkout -- <path>` で戻しても、Editor が再び書き換えることがある）

## Editor

- このworktree用の Editor は **PID 27849**（起動済み・PlayMode 停止中）。レーンが終わるまで止めない（止めると枠を他レーンに取られる）。終わったら `kill 27849` で止め、lane-done.md に書く
- マシン全体で 2 本まで（`.decisions/2026-10-26-Unity-Editorの同時起動はマシン全体で2本までにする.md`）。plan の Global Constraints と依頼文の「3 本まで」は 2 本に読み替える（plan の記述も直す）

## 未回答の判断

- なし（plan の D1〜D14 はすべて「agent 既定（ユーザー未裁定）」として既に記載。pr-body の「ユーザーに見てほしい判断」に並べる）

## 最終の手順（plan Task 9 の置き換え）

- PR は作らない（`.decisions/2026-10-26-UITK段階0のPRは1本に統合する.md`）。台帳 13:52 の行の「origin/master をマージ」は **`origin/feature/uitk-stage0` を取り込む** に読み替える
- Task 8 の moores-code-review の後、`origin/feature/uitk-stage0` を取り込んでコンフリクトを解消し、コンパイルを確かめて push する
- 統合 PR に載せる本文の下書き（変更内容・検証・ユーザーに見てほしい判断・省いた系統、plan Task 9 Step 1 の必須項目）を `.superpowers/sdd/pr-body.md` に書く（commit 不要）
- 済んだら `.superpowers/sdd/lane-done.md` に 3 行（最終コミット・残したもの・pr-body のパス）を書いて止まる

## Codex 側で守ること

- moores-code-review は `references/codex-host.md` の runner（`scripts/codex_workflow_runner/run.mjs`）で `origin/feature/uitk-stage0...HEAD` に対して回す。③ Codex 外部監査も通常どおり回してよい。`独立レビュー待ち` ラベルは貼らない（独立レビューは統合 PR #1559 に 1 回だけ）
- PR は作らない（`.decisions/2026-10-26-UITK段階0のPRは1本に統合する.md`）。最後に `origin/feature/uitk-stage0` を取り込んで push し、統合 PR に載せる本文の下書き（変更内容・検証・ユーザーに見てほしい判断・省いた系統）を `.superpowers/sdd/pr-body.md` に書く。済んだら `.superpowers/sdd/lane-done.md` に 3 行（最終コミット・残したもの・pr-body のパス）を書いて止まる
- Unity Editor はマシン全体で 2 本まで（`.decisions/2026-10-26-Unity-Editorの同時起動はマシン全体で2本までにする.md`）。いま動いている PID 27849 をそのまま使い、レーンが終わるまで止めない（途中で止めると枠を他のレーンに取られる）。終わったら `kill 27849` で止め、そのことを lane-done.md に書く
- 長い処理（コンパイル・テスト・録画・Windows ビルド）はバックグラウンドのポーリングで待たず前景で実行する。subagent への依頼文にも同じことを書く
- 区切りごとにコミットして push する
- ユーザーに質問しない。スキルが「ユーザーに聞く」と定める問いは推奨案で決め、plan の判断記録に「agent 既定（ユーザー未裁定）」と書き、pr-body の「ユーザーに見てほしい判断」に並べる
- Steam（playtest-staging を含む）・Discord など外へ出す操作はしない
- 自分で起動したプロセスは PID を控えて `kill <PID>` で止める。`pkill -f` は使わない
- 指揮セッションへの連絡は台帳 `progress.md` と bd note に書く（Codex からは SendMessage できない）
