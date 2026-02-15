# Auto-merge Workflow

## 概要 / Overview

このリポジトリでは、特定の自動化されたプルリクエストを自動的にマージする機能が実装されています。

This repository has an auto-merge feature for specific automated pull requests.

## 対象のプルリクエスト / Target Pull Requests

以下の条件を満たすプルリクエストが自動マージの対象となります：

The following pull requests are eligible for auto-merge:

- ブランチ名が `sync-skills-` で始まるプルリクエスト
- Pull requests from branches starting with `sync-skills-`

現在、以下のワークフローから作成されるPRが対象です：

Currently, PRs created by the following workflows are targeted:

- **Sync skills directories** (`.github/workflows/sync-skills.yml`)
  - `.claude/skills/` と `.codex/skills/` ディレクトリ間の同期
  - Synchronization between `.claude/skills/` and `.codex/skills/` directories

## 動作フロー / Workflow

1. **PR作成 / PR Creation**
   - 自動化ワークフローがPRを作成
   - Automated workflow creates a PR
   - ラベル `automated` と `auto-merge` が付与される
   - Labels `automated` and `auto-merge` are added

2. **CIチェック / CI Checks**
   - 以下のチェックが実行される
   - The following checks run:
     - Build (Windows/macOS)
     - Unity Tests
     - Mooresmaster Tests
     - JSON Zero-width Character Check

3. **自動承認 / Auto-approval**
   - すべてのCIチェックが成功すると、PRが自動承認される
   - When all CI checks pass, the PR is auto-approved

4. **自動マージ / Auto-merge**
   - 承認後、PRが自動的にマージされる
   - After approval, the PR is automatically merged
   - マージ方法: Squash merge
   - Merge method: Squash merge

## 安全機能 / Safety Features

- ✅ 特定のブランチパターン（`sync-skills-*`）のみが対象
- ✅ Only specific branch patterns (`sync-skills-*`) are eligible
- ✅ すべての必須CIチェックが成功する必要がある
- ✅ All required CI checks must pass
- ✅ マージコンフリクトがある場合は自動マージしない
- ✅ Will not auto-merge if there are merge conflicts
- ✅ GitHub App トークンを使用した適切な権限管理
- ✅ Proper permission management using GitHub App token

## 手動介入が必要な場合 / When Manual Intervention is Needed

以下の場合、自動マージは実行されず、手動での対応が必要です：

Auto-merge will not execute in the following cases, requiring manual intervention:

- CIチェックが失敗した場合
- When CI checks fail
- マージコンフリクトが発生した場合
- When there are merge conflicts
- ブランチ名が対象パターンに一致しない場合
- When the branch name doesn't match the target pattern

## 設定ファイル / Configuration Files

- **自動マージワークフロー**: `.github/workflows/auto-merge.yml`
- **Auto-merge workflow**: `.github/workflows/auto-merge.yml`
- **Sync Skillsワークフロー**: `.github/workflows/sync-skills.yml`
- **Sync Skills workflow**: `.github/workflows/sync-skills.yml`

## トラブルシューティング / Troubleshooting

### 自動マージが実行されない / Auto-merge not executing

1. ブランチ名が `sync-skills-` で始まっているか確認
   Check if branch name starts with `sync-skills-`

2. すべてのCIチェックが成功しているか確認
   Check if all CI checks have passed

3. GitHub App トークンの権限を確認
   Check GitHub App token permissions

4. ワークフローログを確認（Actions タブ）
   Check workflow logs (Actions tab)

### 新しい自動マージ対象を追加する / Adding New Auto-merge Targets

`.github/workflows/auto-merge.yml` の条件を編集：

Edit the conditions in `.github/workflows/auto-merge.yml`:

```yaml
if: startsWith(github.head_ref, 'sync-skills-') || startsWith(github.head_ref, 'your-prefix-')
```

## セキュリティ / Security

- GitHub App トークンは `secrets.APP_ID` と `secrets.PRIVATE_KEY` で管理
- GitHub App token is managed via `secrets.APP_ID` and `secrets.PRIVATE_KEY`
- トークンは最小限の権限（contents:write, pull-requests:write, checks:read）のみを持つ
- Token has minimal permissions (contents:write, pull-requests:write, checks:read)
