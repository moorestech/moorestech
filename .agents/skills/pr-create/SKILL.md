---
name: pr-create
description: |
  GitHub Pull Requestを作成するためのスキル。現在のブランチの変更をレビューし、PRを作成する。
  Use when: ユーザーが「PRを作成して」「プルリクエストを作って」などと依頼した時
context: fork
---

# Pull Request Creation Guide

## Workflow

### 1. Gather Information
Run these commands in parallel to understand the current state:

```bash
# Show untracked files
git status

# Show staged and unstaged changes
git diff

# Check if branch tracks remote and is up to date
git branch -vv

# Show commit history from base branch
git log --oneline origin/master..HEAD

# Show full diff from base branch
git diff origin/master...HEAD
```

### 2. Analyze Changes
Review all commits that will be included in the PR (not just the latest commit).

Draft a PR title and summary:
- Keep title under 70 characters
- Use description/body for details

### 3. Prepare Branch and Commit
ステップ1の結果を元に、ブランチやコミットがない場合は自動で作成する。ユーザーに確認せず実行すること。

- **masterブランチにいる場合**: 変更内容に基づいて適切なブランチ名で `git checkout -b feature/xxx` を実行
- **未コミットの変更がある場合**: 差分を分析し、PR対象の変更をコミットする
  - コミットメッセージは変更内容を端的に表す日本語で作成
- **既にコミット済みの場合**: そのまま次のステップへ進む

### 4. Create Pull Request

```bash
# Push to remote with tracking
git push -u origin HEAD

# Create PR using gh CLI
gh pr create --title "the pr title" --body "$(cat <<'EOF'
## Summary
<1-3 bullet points>

## Test plan
[Bulleted markdown checklist of TODOs for testing the pull request...]

Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

## Important Notes

- Return the PR URL when done so the user can see it
- Never use destructive git commands without explicit permission
- 未コミットの変更やブランチ未作成の状態でも、確認せず差分を分析して自動的にブランチ作成・コミット・PR作成まで一貫して実行する
