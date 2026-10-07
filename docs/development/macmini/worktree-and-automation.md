# Mac mini での worktree・無人実行の運用

Mac mini 自宅サーバー固有の事情。索引は `CLAUDE.local.macmini.md`、環境に依存しない一般則は `docs/development/worktree-and-review-pitfalls.md`。

## moores-wt の癖
### `new` が失敗する / 古いコミットで始まる
- 非ログインシェル（PATH に `/opt/homebrew/bin` が無い）で叩くと、checkout 時の LFS フック（`git-lfs` が見つからない）で `checkout -- .` が落ちて途中終了する。`zsh -lc 'moores-wt new ...'` で叩く。途中で落ちたら `git -C <main> worktree remove --force <wt>` と `git -C <main> branch -D <branch>` で片付けてやり直す
- 分岐元の既定 `--from master` はローカルの master。`--fetch` は `git fetch origin` するだけでローカル master を進めない。最新から切るなら `--from origin/master`
- 同名のローカルブランチが既にあると「既存ブランチを使用」でそれを checkout し、origin へは追従しない（`--fetch` を付けても同じ）
- 規則: 作成直後に `git -C <wt> log --oneline -1` と `origin/<branch>`（新規なら `origin/master`）を突き合わせる。ズレていたら `git -C <wt> reset --hard origin/<branch>`（メインワークツリーの hook を避けるため `git -C` で打つ）

### `rm` の後にディレクトリが残る
- `--no-editor` で作った worktree などで `moores-wt rm <name> --prune-branch` が `git worktree remove --force` の exit 255 で止まることがある。登録だけ外れ、Library 込み数十 GB のディレクトリとローカルブランチが残る。登録が外れるので `moores-wt status` にも出ない
- 規則: `moores-wt rm` の後は `ls -d <worktree>` で消えたか確かめる。残っていたら `git branch --merged master` でマージ済みを確かめてから `rm -rf` する。`Directory not empty`（`.DS_Store` が書き戻される）で失敗したら数秒待って再実行する。最後に `git branch -d` でブランチを消す
- 自分の cwd がその worktree の中だと、Claude Code の安全チェックが `rm -rf` を拒否する。cwd を外へ移してから消す

### checkout の書き込み量
- worktree の checkout は約 4.3GB（うち `moorestech_client/Assets/Dependencies` が 3.1GB）を SSD に書く。対策済みの経路は2つ
  - `moores-wt new`: `worktree add --no-checkout` → 追跡ファイルを clonefile(2) → `reset` / `checkout -- .` / `clean` で HEAD に揃える
  - pr-review の baseline clone（`~/hermes-agent/data/worktrees/moorestech/pr-review-baseline`）: 非 cone の sparse-checkout で Dependencies を外す。`extensions.worktreeConfig=true` なので、pr-independent-review が `git -C <baseline> worktree add` で生やす `pr-<N>` / `skills-canon-*` に継承される
- 規則: 新しい worktree 生成経路を足すときは、このどちらかに乗せる。手書きの `git worktree add` は書き込み対策が効かない。`git -C <repo> worktree add <path>` の相対パスは `<repo>` 起点で解決されるので、必ず絶対パスで渡す

## 新規 worktree で録画シナリオを回す前
- `moores-wt new` は `moorestech_web/node`（同梱 Node）と `moorestech_web/webui/node_modules` を用意しない。run-scenario.sh の前に `moorestech_web/setup.sh`（またはメインから `cp -Rc moorestech_web/node`）と `pnpm install --frozen-lockfile` を済ませる。詳細は worktree-and-review-pitfalls.md の録画シナリオ節

## 無人 apply のスロット（pr-apply / pr-apply-2）
- 無人 apply の作業場は `~/hermes-agent/data/worktrees/moorestech/pr-apply` と `pr-apply-2` の固定スロットプール（poller.py の `APPLY_SLOT_DIRS`）。pr-review poller（`~/hermes-agent/data/services/pr-review/`）が env `PR_REVIEW_APPLY_DIRS` のスロットを先着順で払い出し、割当を `state/pr-<N>/apply.slot` に記録する。ディレクトリが無いスロットは払い出さない
- スロットを新設・作り直すときは次の4点を揃える。どれか欠けると無人 apply がテスト段階で失敗する
  1. `moorestech_client/Library` をメインクローンから `cp -Rc`（APFS clonefile なので容量を食わない）
  2. uloop の疎通。package 3.x は project ごとの Unix ソケットで繋がるのでポート設定は要らない。スロットで `uloop launch` → `uloop compile` が通ることを確かめる（旧 package 1.x の時代はスロットごとに `UnityMcpSettings.json` の `customPort`（8707/8708）を分ける必要があった）
  3. `~/hermes-agent/data/worktrees/moorestech/` 直下に `moorestech_master` / `moorestech_logs` の symlink（`ServerDirectory.cs` が `../../moorestech_master` を見る。既存なら流用）
  4. `moorestech_web/node` を既存スロットから `cp -Rc`（または `moorestech_web/setup.sh`。無いと PlayMode テストが1件落ちる）

## 無人・夜間に plan を実装させるとき
- 別ペインのエージェントに plan を丸ごと任せるときは、指示文に次の2つを入れ、続けて `/goal <plan の全タスク完了 + PR 作成 + 質問せず完走>` を送る。ペインのフッタに `◎ /goal active` が出たことを確かめる
  1. ユーザーへ一切質問しない（AskUserQuestion 禁止）。設計判断は自分で裁定し、根拠と却下案を ADR / PR 本文へ記録して完走する
  2. `独立レビュー待ち` ラベルは下の基準を満たす PR にだけ貼る
- 質問禁止は指示文に書くだけでは守られない。`/goal` は毎ターン後に条件充足を判定して作業を続けさせるので、構造的に塞げる
- `/goal` の達成条件にラベル付与を入れない（入れると外せなくなり、全 PR に貼られる）。ラベルの基準は指示文側に書く
- トリガーになるラベルやフックを指示へ入れる前に、それが何を起動するかを確かめる

### `独立レビュー待ち` ラベルの基準（ユーザー裁定 2026-09-20）
- このラベルは pr-review poller のトリガーで、PR ごとに pr-independent-review → 裁定待ち → pr-adjudicated-apply が無人で走る。非常に重いので乱発しない
- 貼らない: プロダクションコード以外だけの変更（テスト・hooks 等の開発スクリプト・録画シナリオ・スキル・ドキュメント）
- 貼らない: プロダクションコードでも 3 ファイル程度までの変更
- 貼る: 上記以外。`gh pr edit <PR番号> --add-label '独立レビュー待ち'`
