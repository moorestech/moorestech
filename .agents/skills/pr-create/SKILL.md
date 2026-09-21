---
name: pr-create
description: |
  GitHub Pull Requestを作成するためのスキル。現在のブランチの変更をレビューし、master（またはベースブランチ）へマージするPRを作成する。
  Use when: ユーザーが「PRを作成して」「プルリクエストを作って」などと依頼した時
---

# Pull Request Creation

このスキルは実タスクを sonnet subagent に委譲する薄いオーケストレータ。
本体は詳細を実行せず、subagent を立てて `.claude/skills/pr-create/agent.md` の手順を実行させる。

## 実行体別（Claude Code / Codex）

共通の読み替え（質問・subagent派遣・待機・モデル名・パス）は `agent-runtime-compat` スキルが正本。このskill固有の差分のみ:

- **委譲** — Claude: `Agent`・`model: sonnet`・同期。Codex: `spawn_agent`・`model: gpt-5.6-sol` → `wait_agent`。subagent に読ませるパスは `.agents/skills/pr-create/agent.md`。
- **コンフリクト解消** — Claude: opus subagent。Codex: `gpt-6-astra` の subagent。
- **PR後の撤収** — 両者共通: この環境では PR 作成直後に `moores-wt rm <name>`（Codex の2026-09-21セッションは PR 未作成のまま使用量上限で終わり worktree が残った。上限が近いなら先に push だけ済ませる）。

## 手順

1. **引数を確認する。** `/pr-create` が引数なし（無言）で呼ばれた場合は、確認を一切せず日本語で全自動実行する。追加指示（対象ブランチ・タイトル方針等）がある場合はそれを subagent へ引き継ぐ。

2. **sonnet subagent を立てる。** Agent ツールで `model: sonnet` を指定し、次のプロンプトで起動する（同期実行 = `run_in_background: false`）:

   ```
   `.claude/skills/pr-create/agent.md` の手順に従い、現在のブランチの変更を
   master（またはベースブランチ）へマージするPRを作成せよ。
   出力・コミットメッセージ・PR本文はすべて日本語。ユーザーへの確認はせず全自動で実行し、
   Web関連の変更を含む場合は検証済みの実画面画像をPR本文へ添付し、
   最後に作成したPRのURLと添付画像数を返すこと。
   （ユーザーからの追加指示があればここに引き継ぐ）
   ```

   subagent は起動後すぐ `.claude/skills/pr-create/agent.md` を Read し、その Workflow を実行する。

3. **結果を提示する。** subagent が返した PR の URL をユーザーに提示する。

## Important Notes

- 詳細な PR 作成フロー（情報収集・差分分析・ブランチ/コミット作成・`gh pr create`）は `agent.md` 側に定義済み。本体で重複実装しない
- Web関連の変更ではスクリーンショット添付を完了条件とし、ローカルパスだけをPR本文へ書いて完了扱いにしない
- Web関連の変更では、検証済みスクリーンショットを `docs/pr-assets/<PRの題材>/` へコミットし、そのGitHub URLをPR本文へ掲載することを完了条件とする
- 実際のマージ（`gh pr merge` 等）は行わない。PR作成と base 設定までが範囲
- ただしPR作成後のコンフリクト確認は必須。CONFLICTINGならbaseをPRブランチへマージして解消・pushまで行う。コンフリクト解消の実作業はsonnetにやらせず、必ず**opus subagent**へさらに委譲する（設計判断を要する場合のみ未解消のまま報告。詳細は `agent.md` ステップ5）
- PR の head は指定がない限り cwd の現在ブランチをそのまま使う。ブランチ名の prefix は問わず、切り直しや改名はしない（詳細は `agent.md` ステップ3）
- `tree1` / `tree2` のような `tree`+数字のブランチは git worktree 運用用の使い回しブランチ。PR の head にはせず、別ブランチを切って PR を作る（詳細は `agent.md` ステップ3）
