# auto-merge.yml is now disabled

このワークフローは無効化されています。

This workflow has been disabled.

## 理由 / Reason

`workflow_run` トリガーを使用すると、各CIワークフローが完了するたびにこのワークフローが起動され、無駄な実行が発生していました。

Using `workflow_run` triggers caused this workflow to execute every time each CI workflow completed, resulting in redundant executions.

## 新しい実装 / New Implementation

自動マージ機能は `sync-skills.yml` ワークフロー内に統合されました。
PR作成後、すべてのCIチェックが完了するまで待機してから、1回だけマージを実行します。

Auto-merge functionality has been integrated into the `sync-skills.yml` workflow.
After PR creation, it waits for all CI checks to complete, then merges once.

## 利点 / Benefits

- 無駄なワークフロー実行を削減（4回→1回）
- Reduces redundant workflow executions (4 times → 1 time)
- ロジックが一箇所に集約
- Logic consolidated in one place
- より直感的で理解しやすい
- More intuitive and easier to understand

## 復元方法 / How to Restore

必要に応じて、`.disabled` 拡張子を削除すれば復元できます。

If needed, you can restore by removing the `.disabled` extension.

```bash
mv .github/workflows/auto-merge.yml.disabled .github/workflows/auto-merge.yml
```
