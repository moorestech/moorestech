---
name: tart-vm-moorestech-dev
description: Tart macOS VM 内の moorestech worktree で、開発者不在の無人前提で Unity/C# 開発を進めるための運用スキル。Use When — ユーザーが「tart vmモードで起動して」と発言した時に発動する。
---

# Tart VM 専用 moorestech 無人開発スキル

このスキルは **Tart macOS VM の中で、開発者が画面の前にいない状態** で moorestech の Unity/C# 開発を進めるための運用ルール。汎用スキルではない。環境が一致しないなら使わない。

## 0. 発動前チェック（厳格・全一致が必須）

以下を実際にコマンドで確認し、**すべて一致した場合のみ** このスキルの運用ルールを適用する。1つでも外れたら適用を中止し、通常の判断に戻る。

```bash
pwd                              # → /Users/admin/dev-agent/worktrees/moorestech* 配下であること
hostname                         # → Manageds-Virtual-Machine.local
uname -a                         # → Darwin ... VMAPPLE ... arm64 を含む
sysctl -n hw.model               # → VirtualMac2,1
ls /Applications/Unity/Hub/Editor # → 6000.3.8f1 が存在する
```

判定ルール:
- **作業ディレクトリが `/Users/admin/dev-agent` 配下でない** → 発動しない。
- **hostname が `Manageds-Virtual-Machine.local` でない、または `uname` に `VMAPPLE` が無い** → Tart VM ではない。発動しない。
- **対象が moorestech repo/worktree でない**（別 Unity プロジェクト、別リポジトリ） → 発動しない。
- 迷ったら発動しない方を選ぶ。誤発動は通常ローカルの開発フローを壊す。

## 1. 無人前提（最重要）

開発者は基本的に目の前にいない。**Unity Editor の手動操作をユーザーに要求しない。**

- 「Unity でこのボタンを押してください」「Play を押して確認して」「Inspector で値を入れて」は禁止。
- Unity 操作はすべて CLI 経由（`uloop` / `uloop-*` skill）で自分で行う。手動操作が前提のメニュー/プレハブ配線も `uloop execute-dynamic-code` / `uloop execute-menu-item` で実施する（`uloop-execute-dynamic-code`, `uloop-execute-menu-item` skill）。
- どうしても人手でしか進められない箇所に到達したら、勝手に放置せず **final report / 報告に「ここはユーザー操作が必要」と明示** して止める。手動操作をユーザーに丸投げして完了扱いにしない。
- `.meta` ファイルを手で作らない。Prefab/Scene/ScriptableObject を直接テキスト編集しない（AGENTS.md 準拠。変更は `uloop execute-dynamic-code` 経由）。

## 2. クライアント / UI 変更は evidence を残す（必須）

クライアントや UI など **見た目・挙動が伴う実装** は、ユーザーが後から差分を目で確認できる形にする。コードだけ出して「直しました」で終わらせない。

優先順位:
1. **録画**: `unity-playmode-recorded-playtest` skill を使う。第一選択はプレイテストDSL（同 skill 同梱の `scripts/run-scenario.sh` + `PlaytestRunOptions{Record=true}`）の1コマンド一発実行で、preflight〜PlayMode起動〜録画〜result.json回収まで自動化される。DSLが無いブランチのみ手動フロー（PlayMode を CLI 起動して Unity Recorder 制御）。フォーカス不要なので無人で回せる。入力は InputSystem QueueStateEvent で注入する（OS の simulate-keyboard / simulate-mouse は使わない）。
2. **スクリーンショット**: 録画が過剰な小さな UI 変更は `uloop-screenshot` skill で Game View / 該当 EditorWindow を PNG 保存する。
3. ランタイム挙動の不具合調査は `unity-runtime-bug-hunt`、PlayMode 統合テストは `editmode-in-playing-test` skill を併用する。

evidence の出力パス（録画ファイル / PNG）は最終報告に必ず列挙し、何を確認できるかを 1 行で添える。evidence を残せなかった場合は、その理由を報告に明記する。

## 3. 実装フロー（無人で完結させる）

1. AGENTS.md / CLAUDE.md の規約を守る（QA は「バグがある前提」で探す、1 ファイル 200 行以下、partial 禁止、try-catch 基本禁止、`#region Internal` はローカル関数用途のみ 等）。
2. `.cs` を変更したら **必ずコンパイル**: `uloop compile --project-path ./moorestech_client`（`uloop-compile` skill）。
3. テストは regex で対象限定: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`（`uloop-run-tests` skill）。サーバー側テストもこのクライアント project-path で同時に走る。
4. エラー確認: `uloop get-logs --project-path ./moorestech_client --log-type Error`。
5. Domain Reload 中の「Unity is reloading」エラーが出たら 45 秒待って再試行（AGENTS.md 準拠）。
6. **コンパイル/uloop 応答が 10 分待ってもダメなら Unity を kill して再起動する**: `pkill -f "projectPath .*<自worktree>/moorestech_client"` で自プロジェクトの Unity（AssetImportWorker 含む）を落とし、`uloop launch ./moorestech_client` で再起動してやり直す。**他タスクの Unity は絶対に kill しない**（VM は複数タスク共有）。
7. マスタデータ/スキーマを触るなら `edit-schema` → `validate-schema` skill を経由する。

## 4. 実装終了後 moores-code-review を必ず 1 回実行（必須）

実装が一段落したら、コミット前に **最低 1 回はコードレビューを実行する**。使うのは repo 同梱の **`moores-code-review`** skill（`.claude/skills/moores-code-review/`）。moorestech の実 PR レビュー指摘から抽出した設計レンズ群を、決定論スクリプト＋条件発火サブエージェントで並列実行する moorestech 特化版で、構成は all-code-review と同等（決定論チェック → レンズ並列 → 実コード照合 → 機械的修正の自動適用 → 報告）。

> **重要**: `moores-code-review` は**最新の `master-fable-tmp` にしか存在しない**。作業ブランチに `.claude/skills/moores-code-review/` が無い場合は、先に `origin/master-fable-tmp` を作業ブランチへ**逆マージ**して取り込む（skill だけ cherry-pick せず、ブランチ全体を最新化する）。
> 注意: harness の available-skills（system-reminder）は関連度でフィルタされるため自動提示されないことがある。その場合も Skill ツールで名前指定して直接起動する。

手順:
1. 変更を `git diff` / `git status` で確認する。
2. **`moores-code-review` skill を起動する**（Skill ツールで名前指定）。逆マージしても存在しない等でどうしても使えない時のみビルトイン `/code-review`（`high`〜`max`）にフォールバックし、その場合は repo の `code-review-perspectives.md` を読んで各観点を finder 角度として明示適用する（自動ロードされないため）。
3. 指摘が出たら対応または「対応しない理由」を残し、再度コンパイル/テストで確認する。
4. レビューを実行した事実と結果サマリを最終報告に必ず書く。

## 5. 終了時（コミットと PR 作成・必須）

- 全作業を **task branch にコミット**する（作業消失防止。AGENTS.md 準拠）。
- **PR を常に作成する**: コミット後、`pr-create` skill を使うサブエージェントを起動して task branch を push し PR を作成（既存 PR があれば更新）する。base はブランチの派生元（現在は `master-fable-tmp`）を指定し、master を base にしない。PR URL は検証（`gh pr view`）してから最終報告に記載する。
- `master` への直 push / merge / deploy / release はしない。worktree cleanup もしない。
- 最終報告に「変更ファイル」「実行した検証コマンドと結果」「moores-code-review 結果」「evidence パス」「PR URL」「残課題」を含める。
