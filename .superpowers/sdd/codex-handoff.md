# Codex への引き継ぎ: UITK 段階0 第2波（撤去と差し込み記法の統一）

## レーン
- bd: `moorestech-albof.5`（0-1 OneJS・DebugUI と 0-12 旧 SkitUI の撤去）と `moorestech-albof.7`（0-6 差し込み記法を名前付きに統一）。どちらも claim 済み
- worktree: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/uitk-s0-wave2`、ブランチ `feature/uitk-s0-wave2`
- plan: `docs/superpowers/plans/2026-10-26-uitk-s0-wave2-removal-and-named-placeholders.md`（`## Requirements`・`## Global Constraints`・`## 判断記録（ADR）` は全タスクに掛かる。ただし Codex 禁止の行は下の「Codex 側で守ること」で上書きされる）
- 台帳: `.superpowers/sdd/progress.md`
- 実行メモ: `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/sdd/feature-uitk-s0-wave2/memo.md`
- タスクブリーフ・報告・レビュー: `.superpowers/sdd/task-{1,2,3}-{brief,report,review}.md`（task-2-report.md はまだ無い）

## 済んだもの
- Task 1（C# の補間器・`LocalizationTextParam`・CSV 32 キー 190 か所・生産者・テスト）: コミット cac371064d・7b500408b5。レビュー ❌ Important 1 件（`MouseCursorTooltipState.cs:24` の `Array.Empty<string>()`）。この修正は Task 2 の WIP に入っている（WIP 内で直っているかを確かめること）
  - Task 1 でブリーフの外に足した変更: `ui.tooltip.deleteResultPending/Unconfirmed/Rejected` の `{0}` を全言語で `{label}` にした（旧実装では生の `{0}` が画面に出ていた）。pr-body の「ユーザーに見てほしい判断」に入れる
  - Minor（最終レビューへ）: 補間器は `{` を含む語をすべて枠とみなし LogError する／`AppendElapsed(key, leadingParam)` はほぼ TerrainReady 専用
- Task 3（manifest・lock から onejs と debug-ui を外し、`com.unity.ui.test-framework` 1.0.0 と Tests.asmdef の 2 参照を足した）: コミット edd6ed84a9。レビュー済み・指摘なし。lock 項目とアセンブリ名の解決は Task 4 で確かめる
  - Minor（最終レビューへ）: `moorestech_client/UIElementsSchema/DebugUI.UIElements.xsd` が tracked のまま残っている／`.gitignore` に `# OneJS` のコメントが残っている

## 途中のもの（Task 2）
- 最初の subagent（sonnet）が利用枠で止まり、途中の変更をコントローラーが WIP コミット 768008155a として push した（Task 2 の Files の大半で 50 ファイル。C# のサーバー・クライアント・テスト・Web・e2e。未検証）。続きの subagent（opus）は e2e を回し始めたところで止めた。task-2-report.md は無い
- 見立て: WIP は使える。前任の最後の言葉は「Web の編集は済んだ。e2e がタイムアウトしたので、残ったプロセスを kill して、分割を大きくして再実行する」だった。残っているのは次のとおり
  1. `git diff edd6ed84a9 768008155a` を task-2-brief.md の Step 1〜6 と突き合わせ、抜けを埋める
  2. Task 1 と Task 2 の型変更の追随漏れを全 .cs で git grep する（Editor が無いのでコンパイルの代わり）。対象は string 配列と `Array.Empty<string>()`
  3. Web の vitest・tsc 2 本・lint を緑にする。e2e は 1 回試し、回らなければ理由を記録する
  4. ブリーフ Step 6 のコミット・push・bd note を行う
  5. Task 2 のレビューを行う
- `ui.notification.deleteUnconfirmed` と `deleteRejected` の CSV `{0}`: Web の補間で埋まっていなければ、全言語で `{target}` に替え、`["target"] = label` で送る（コントローラーの既定）。WIP で済んでいるかを確かめる。画面の表示が変わる修正なので、pr-body の「ユーザーに見てほしい判断」に入れる
- **未コミットの変更（コミットせずに渡す）**: `.agents/skills/unity-playmode-recorded-playtest/scenarios/server-notification-smoke.cs` の 1 行
  - WIP では、検証を「研究名が『原始研究1』」から「`research` キーがある」に弱めていた。続きの subagent がこれを「`research` の値が『原始研究1』」に戻しかけていた
  - 見立て: レーン F の持ち物なので、型の追随だけに留める。つまり元の検証の意味（値が『原始研究1』）を保つ未コミット版を採る
  - ただしサーバーは表示名ではなく Guid を送る（notificationMessages.ts のコメント）。元の検証が実際に通っていたかは Task 6 の録画で確かめる。落ちたら、元の `{p0}` 時代も同じ値だったかを `git log -p` で見てから判断する
- レーン境界で触ったファイル（統合のマージで解く。bd note moorestech-albof.7 に書く）
  - B: `InitializeScenePipeline.cs:181`
  - C: `TooltipTopic.cs`・`NotificationTopic.cs`
  - F: `Client.Playtest/Operations/Ui` の 3 本、`server-notification-smoke.cs`

## 残りのタスク（plan の番号）
- Task 2 の仕上げとレビュー（上記）
- Task 4: Editor でのコンパイルと関連テスト（Editor 枠が要る）
- Task 5: 旧 SkitUI の削除。prefab とシーンの書き換えは uloop で行う（Editor 枠が要る）
- Task 6: unityプレイ録画テスト（Editor 枠が要る）
- Task 7: Windows の batchmode ビルドで quickjs が消えたことを確かめる（Unity 1 本に数える。`$PLAYTEST_RUNS` は `~/hermes-agent/data/services/playtest/runs`）
- Task 8: moores-code-review
- Task 9: 統合ブランチの取り込み・push・pr-body

## 未回答の判断（すべて agent 既定。pr-body の「ユーザーに見てほしい判断」に並べる）
- plan の D1〜D16
- `deleteResult*` の `{0}`→`{label}`（Task 1）
- `deleteUnconfirmed/deleteRejected` の `{0}`→`{target}`（Task 2）
- `server-notification-smoke.cs` の検証の扱い
- moores-code-review で出る設計判断

## 最終の手順
Task 8 → Task 9（`git merge --no-ff origin/feature/uitk-stage0` → コンパイル・テスト → push → `.superpowers/sdd/pr-body.md`）→ `.superpowers/sdd/lane-done.md` に 3 行（最終コミット・残したもの・pr-body のパス）を書いて止まる。bd の albof.5・albof.7 は plan の Task 9 Step 5 どおり閉じる。

## Codex 側で守ること（指揮セッション moorestech-f2 の指示をそのまま写す）
- moores-code-review は `references/codex-host.md` の runner（`scripts/codex_workflow_runner/run.mjs`）で `origin/feature/uitk-stage0...HEAD` に対して回す。③ Codex 外部監査も通常どおり回してよい。`独立レビュー待ち` ラベルは貼らない（独立レビューは統合 PR #1559 に 1 回だけ）
- PR は作らない（`.decisions/2026-10-26-UITK段階0のPRは1本に統合する.md`）。最後に `origin/feature/uitk-stage0` を取り込んで push し、統合 PR に載せる本文の下書き（変更内容・検証・ユーザーに見てほしい判断・省いた系統）を `.superpowers/sdd/pr-body.md` に書く。済んだら `.superpowers/sdd/lane-done.md` に 3 行（最終コミット・残したもの・pr-body のパス）を書いて止まる
- Unity Editor はマシン全体で 2 本まで（`.decisions/2026-10-26-Unity-Editorの同時起動はマシン全体で2本までにする.md`）。このレーンはまだ Editor を持っていない。枠は C → B → 第2波の順に回す。Editor の要る Task に入る前に、`/Users/sakastudio/hermes-agent/data/worktrees/moorestech/uitk-s0-wave2/.superpowers/sdd/editor-slot-granted`（指揮セッションが作る）ができるまで前景で待つ（`until [ -f <そのパス> ]; do sleep 30; done` を 1 回の呼び出し 10 分までで繰り返す）。できたら `pgrep -fl "Unity.app/Contents/MacOS/Unity -projectPath"` で 2 本未満を確かめて `uloop launch ./moorestech_client` で起動し、PID を控え、終わったら `kill <PID>` で止める。待つあいだは Editor の要らない Task を先に進める
- 長い処理（コンパイル・テスト・録画・Windows ビルド）はバックグラウンドのポーリングで待たず前景で実行する。subagent への依頼文にも同じことを書く
- 区切りごとにコミットして push する
- ユーザーに質問しない。スキルが「ユーザーに聞く」と定める問いは推奨案で決め、plan の判断記録に「agent 既定（ユーザー未裁定）」と書き、pr-body の「ユーザーに見てほしい判断」に並べる
- Steam（playtest-staging を含む）・Discord など外へ出す操作はしない
- 自分で起動したプロセスは PID を控えて `kill <PID>` で止める。`pkill -f` は使わない
- 指揮セッションへの連絡は台帳 `progress.md` と bd note に書く（Codex からは SendMessage できない）
