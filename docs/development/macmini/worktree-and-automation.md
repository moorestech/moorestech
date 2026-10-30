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
- worktree の checkout は約 4.3GB（うち `moorestech_client/Assets/Dependencies` が 3.1GB）を SSD に書く。対策済みの経路は `moores-wt new`（`worktree add --no-checkout` → 追跡ファイルを clonefile(2) → `reset` / `checkout -- .` / `clean` で HEAD に揃える）か、Dependencies を外す非 cone の sparse-checkout
- 規則: 新しい worktree 生成経路を足すときは、このどちらかに乗せる。手書きの `git worktree add` は書き込み対策が効かない。`git -C <repo> worktree add <path>` の相対パスは `<repo>` 起点で解決されるので、必ず絶対パスで渡す

## 新規 worktree で録画シナリオを回す前
- `moores-wt new` は `moorestech_web/node`（同梱 Node）と `moorestech_web/webui/node_modules` を用意しない。run-scenario.sh の前に `moorestech_web/setup.sh`（またはメインから `cp -Rc moorestech_web/node`）と `pnpm install --frozen-lockfile` を済ませる。詳細は worktree-and-review-pitfalls.md の録画シナリオ節

## 無人・夜間に plan を実装させるとき
- 別ペインのエージェントに plan を丸ごと任せるときは、指示文に「ユーザーへ一切質問しない（AskUserQuestion 禁止）。設計判断は自分で裁定し、根拠と却下案を ADR / PR 本文へ記録して完走する」を入れ、続けて `/goal <plan の全タスク完了 + PR 作成 + 質問せず完走>` を送る。ペインのフッタに `◎ /goal active` が出たことを確かめる
- 質問禁止は指示文に書くだけでは守られない。`/goal` は毎ターン後に条件充足を判定して作業を続けさせるので、構造的に塞げる
- トリガーになるラベルやフックを指示へ入れる前に、それが何を起動するかを確かめる
