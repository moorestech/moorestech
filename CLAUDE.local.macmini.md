# この環境（Mac mini自宅サーバー）ローカルのworktree運用ルール

このファイルはgit管理外（.git/info/excludeで除外）で、この環境でのみ有効。リポジトリ共通ルール（AGENTS.md）には含めない。

## 基本方針: タスク毎の使い捨てworktree

- リスクがほとんどない作業（仕様記述・壁打ち・調査・ドキュメント閲覧）を除き、**すべてのタスクはタスク毎に新規worktreeを切って作業する**。
- 理由: 並列セッション数が高く、メインワークツリーを共有可変状態にすると競合する（Editor占有・無人applyの死亡）。
- worktreeの使い回しはしない。隔離性を優先し、毎回使い捨てる。
- **PRを出したら（またはユーザーが打ち切ったら）その場で `moores-wt rm <name>` を実行し、worktreeとUnity Editorを畳む。** 放置したEditorがメモリを食い、孤児worktreeがmasterピンを掴んだままになる。

## メインワークツリーでのUnity起動は原則禁止

- メインクローン（このディレクトリ）でUnity Editorを立ち上げてよいのは、**worktreeへコピーするためのLibrary生成（日次ウォームアップ）のときだけ**。
- 実装・テスト・PlayMode検証などのUnity作業は、すべてタスク用worktree側のEditorで行う。

## worktreeの作成/破棄は `moores-wt` で行う

実体は `~/bin/moores-wt`（git管理外・PATH上は `~/.local/bin/moores-wt` のsymlink）。手作業の `git worktree add` は使わない（スキルがそう案内していても、この環境では `moores-wt new`）。

```bash
moores-wt new <branch> [--dir NAME] [--from BASE] [--no-editor] [--fetch] [--force]
moores-wt rm  <name|path> [--force] [--prune-branch] [--prune-master]
moores-wt status [--json]
```

- `new` … メインでUnity起動中なら拒否 → `git worktree add --no-checkout` → 追跡ファイルをclonefile(2)で複製しHEADとの差分だけ書く（SSD書き込み対策） → CLAUDE.local.md等の未追跡ローカル規約をコピー → PersonalAssets(非公開アセット)とLibraryを`cp -Rc`でコピー → 互換コミットのmasterピンworktree（`worktrees/moorestech_master/pin-<commit8>`）を用意 → `uloop launch`。所要3分強。
- `rm` … 未コミット/未pushがあれば拒否（`--force`で破棄）→ Editor終了 → `worktree remove --force` → 未参照masterピンと孤児Editorを報告。
- `status` … worktree一覧（branch/Library有無/Editor pid/dirty）、Editor本数、孤児Editor、未参照masterピン。`--json`は機械可読（unity-modal-watchdogとの突き合わせ用）。
- 孤児Editorの定義: Unityプロセスの`-projectPath`の親を worktree root とみなし、`git worktree list`に無ければ`orphan-unregistered`、パス自体が消えていれば`orphan-missing`。watchdogはこの2つを再launch対象外とする。
- pr-review の baseline clone（`~/hermes-agent/data/repos/moorestech-pr-review-baseline`）は別経路: sparse-checkout で `Assets/Dependencies` を外し、pr-independent-review が生やす `pr-<N>` / `skills-canon-<sha8>` へ継承させる。置き場は moores-wt と同じ `~/hermes-agent/data/worktrees/moorestech/`（canon_setup.py が固定）だが moores-wt status の一覧には出ない（baseline clone の worktree のため。`git -C ~/hermes-agent/data/repos/moorestech-pr-review-baseline worktree list` で見る）（詳細は `~/hermes-agent/data/services/pr-review/README.md`）。

## 運用上の注意

- PlayModeの内蔵サーバーはポート0（OS自動採番）で起動しBoundPortへ接続するため、複数worktreeのPlayMode同時実行はポート衝突しない（ServerConnectionInitializer参照）。ローカルプレイは接続試行なしで必ず内蔵サーバーを起動し、スタンドアロンサーバーが11564に居ても誤接続しない（外部接続はConnectServerメニューの明示指定のみ）。
- .cs変更のコンパイル必須ゲートを満たすには、worktree側で自分用のEditorをuloopで立ち上げる必要がある。Mac miniのメモリ上、同時Editor数は2〜3本を目安に抑える（目安であって機械的な拒否はしない）。
- Editorのカウントは`-batchMode`付きの子プロセス（AssetImportWorker等）を除外する。
- 常設スロットは`~/hermes-agent/data/worktrees/moorestech/pr-apply`と`pr-apply-2`の2つだけ（pr-adjudicated-applyのスロットプール。固有ポート8707/8708・Library配備済み）。pr-independent-reviewはPRごとに使い捨てworktreeを作り、共用スロットは使わない。

## メインワークツリーでのブランチ操作はhookで物理拒否

- 実体: `.dev-hooks/main-worktree-guard.mjs`（tracked）。登録は `.claude/settings.local.json` の PreToolUse(Bash)（gitignore済み＝このマシン限定）
- 有効化・復旧・動作確認の手順は当該.mjsの冒頭ヘッダが正本。settings.local.jsonを消してもそこから復旧できる
- 登録コマンドは `node "${CLAUDE_PROJECT_DIR}/.dev-hooks/main-worktree-guard.mjs"`
- 拒否対象: メインワークツリー上での `checkout -b` / `switch -c` / `git branch <新規>` / 既存refへの `checkout|switch`（HEAD移動）
- 通す: linked worktree内の同操作、`git -C <worktree>` 経由、`status`/`log`/`fetch`/`branch --list|-d`/`checkout -- <path>`/`worktree add`、moorestech以外のrepo
- 上書き: `MOORES_MAIN_WT_OK=1` をコマンドに前置（低リスク作業で本当にメイン上で実行する場合のみ）
- 理由: cmuxは全セッションをメインワークツリーのcwdで起こすため、`moores-wt new` を踏まないセッションはそのままメインで作業してしまう

# その他

- 調査・壁打ち・文書閲覧以外は毎回 `moores-wt new <branch>` で専用worktreeを作る。手動の `git worktree add`、本体でのbranch切替、`MOORES_MAIN_WT_OK=1`、worktree再利用は禁止。PR作成後または打切り時に `moores-wt rm <name>`。

- Unityは自分のworktreeのEditorだけを使う。起動・停止前に `moores-wt status` で所有者を確認する。本体Editorが原因で `moores-wt new` が拒否されたら、他者のEditorを落とさず、`--force` も使わず、終了を待つ。Unity作業をsubagentへ渡す前に自分のEditor起動とcompile疎通を確認する。
- 「よしなに」「進められるだけ」の委任では実装前の独自承認待ちを挟まない。止まるのは解決不能な障害か破壊的・不可逆な操作だけ。`.decisions/` には明示されたユーザー裁定だけを記録し、仮定・未決事項は報告かbd noteへ記す。未実測のplan項目に `[x]` を付けない。