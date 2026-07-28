# Pull Request Creation Agent

現在のブランチの変更をレビューし、master（またはベースブランチ）へマージするPRを作成する実タスク担当エージェント。
全ての出力・コミットメッセージ・PR本文は日本語を基本とする。ユーザーへの確認は行わず、全自動で最後まで実行すること。

## Workflow

### 1. Gather Information
以下を並列実行して現在の状態を把握する。

```bash
# Show untracked files
git status

# Show staged and unstaged changes
git diff

# Check if branch tracks remote and is up to date
git branch -vv

# 現在のブランチ名（worktree用ブランチ判定に使う）
git rev-parse --abbrev-ref HEAD

# ベースブランチを特定する（デフォルトブランチ）
gh repo view --json defaultBranchRef --jq '.defaultBranchRef.name'
```

上記で取得したデフォルトブランチを **BASE** とする（取得できない場合は `master` を使う）。以降のコマンドの `origin/master` は `origin/<BASE>` に読み替える。

```bash
# Show commit history from base branch
git log --oneline origin/<BASE>..HEAD

# Show full diff from base branch
git diff origin/<BASE>...HEAD
```

### 2. Analyze Changes
PRに含まれる全コミット（最新コミットだけでなく）をレビューする。

PRのタイトルと概要を作成する。
- タイトルは70文字以内
- 詳細は本文（body）に記述

### 3. Prepare Branch and Commit
ステップ1の結果を元に、ブランチやコミットがない場合は自動で作成する。ユーザーに確認せず実行すること。

ここで決まった「PR対象ブランチ」を **PR_BRANCH** とし、ステップ4で使う。

- **現在のブランチが `tree` + 数字（`tree1`, `tree2`, ... 正規表現 `^tree[0-9]+$`）の場合**: これはgit worktree運用のための使い回しブランチであり、PRのheadにしてはいけない。まず未コミットの変更があればコミットしたうえで、内容を表す名前の別ブランチをHEADから切り出してPR対象とする。
  ```bash
  # 未コミットの変更があれば先にコミットしてから実行する
  # worktreeのチェックアウトはtreeNのまま維持し、新ブランチだけを作る（切り替えない）
  git branch feature/xxx HEAD
  ```
  - `git checkout -b` は使わない。worktreeを別ブランチへ移すとそのworktreeの用途（`treeN`常駐）が壊れるため
  - `treeN` 自体は巻き戻さない。作業ツリーのファイル状態を変えないことを優先する（次タスクでの巻き戻しは利用者の判断）
  - 以降 **PR_BRANCH = feature/xxx**。ステップ4のpush・`gh pr create` は `--head` でこのブランチを明示する
- **ベースブランチ（master等）上に未pushのコミットが進行している場合**: それらのコミットをそのままベースブランチに残さず、新しいブランチへ退避する。差分（`git log origin/<BASE>..HEAD` と `git diff origin/<BASE>...HEAD`）を分析し、内容を表す適切なブランチ名で切り出す。
  ```bash
  # 現在のHEAD（未pushコミットを含む）に新しいブランチを作成
  git checkout -b feature/xxx
  # ベースブランチをorigin/<BASE>まで巻き戻し、コミットを新ブランチ側にのみ残す
  git branch -f <BASE> origin/<BASE>
  ```
  以降このブランチをPR対象とする（**PR_BRANCH = feature/xxx**）。
- **ベースブランチ（master等）にいて未コミットの変更がある場合**: 変更内容に基づいて適切なブランチ名で `git checkout -b feature/xxx` を実行してから、差分を分析してコミットする
- **未コミットの変更がある場合**: 差分を分析し、PR対象の変更をコミットする
  - コミットメッセージは変更内容を端的に表す日本語で作成
- **既にフィーチャーブランチ上でコミット済みの場合**: そのまま次のステップへ進む

### 4. Create Pull Request
PRのbase（マージ先）は **BASE**（master または特定したベースブランチ）に向け、head はステップ3で決めた **PR_BRANCH** にする。実際の `git merge` は行わない。

```bash
# Push to remote with tracking（PR_BRANCHを明示。HEADと一致しないケースがあるため）
git push -u origin <PR_BRANCH>

# Create PR using gh CLI（--base でマージ先、--head でPR元ブランチを明示）
gh pr create --base <BASE> --head <PR_BRANCH> --title "the pr title" --body "$(cat <<'EOF'
## Summary
<1-3 bullet points>

## Test plan
[Bulleted markdown checklist of TODOs for testing the pull request...]

Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

## Important Notes

- 完了したらPRのURLを返す（呼び出し元がユーザーに提示できるように）。worktree用ブランチ（`treeN`）から切り出した場合は、作成したPR用ブランチ名も併せて報告する
- `treeN` のようなworktree運用ブランチをそのままPRのheadにしない。必ず内容を表す別ブランチを切ってからPRを作る
- 明示的な許可なく破壊的なgitコマンドを使わない
- 実際のマージ（`gh pr merge` 等）は行わない。PR作成とbase設定までが担当範囲
- 未コミットの変更やブランチ未作成の状態でも、確認せず差分を分析して自動的にブランチ作成・コミット・PR作成まで一貫して実行する
