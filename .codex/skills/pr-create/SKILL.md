---
name: pr-create
description: |
  GitHub Pull Requestを作成するためのスキル。現在のブランチの変更をレビューし、master（またはベースブランチ）へマージするPRを作成する。
  Use when: ユーザーが「PRを作成して」「プルリクエストを作って」などと依頼した時
---

# Pull Request Creation

このスキルは実タスクを sonnet subagent に委譲する薄いオーケストレータ。
本体は詳細を実行せず、subagent を立てて `.claude/skills/pr-create/agent.md` の手順を実行させる。

## 手順

1. **引数を確認する。** `/pr-create` が引数なし（無言）で呼ばれた場合は、確認を一切せず日本語で全自動実行する。追加指示（対象ブランチ・タイトル方針等）がある場合はそれを subagent へ引き継ぐ。

2. **sonnet subagent を立てる。** Agent ツールで `model: sonnet` を指定し、次のプロンプトで起動する（同期実行 = `run_in_background: false`）:

   ```
   `.claude/skills/pr-create/agent.md` の手順に従い、現在のブランチの変更を
   master（またはベースブランチ）へマージするPRを作成せよ。
   出力・コミットメッセージ・PR本文はすべて日本語。ユーザーへの確認はせず全自動で実行し、
   最後に作成したPRのURLを返すこと。
   （ユーザーからの追加指示があればここに引き継ぐ）
   ```

   subagent は起動後すぐ `.claude/skills/pr-create/agent.md` を Read し、その Workflow を実行する。

3. **結果を提示する。** subagent が返した PR の URL をユーザーに提示する。

## Important Notes

- 詳細な PR 作成フロー（情報収集・差分分析・ブランチ/コミット作成・`gh pr create`）は `agent.md` 側に定義済み。本体で重複実装しない
- 実際のマージ（`gh pr merge` 等）は行わない。PR作成と base 設定までが範囲
