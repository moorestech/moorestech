---
name: structured-dev-workflow
description: >
  アイデア出しから実装・デバッグまでの開発ワークフローを4フェーズ(Phase1壁打ち設計
  →Phase2実装計画→Phase3サブエージェント実行→Phase4デバッグ)でこのrepo単体で完結
  させる。Phase1では実装前に設計をユーザー承認させるHARD GATEがあり、簡単に見える
  作業でも省略しない。Phase3は1タスクごとに新規サブエージェントを起動して実装+レビュー
  を回す。Phase4はまずこのファイル内の軽量4フェーズ(単独調査)を使い、原因が複数
  観点にまたがり収束しない場合のみ既存の debug-workflow スキル(7観点並列)へ
  エスカレーションする。
  Use When — 「新機能を作りたい」「仕様を固めたい」「壁打ちしたい」「〜を作って」
  (Phase1へ)、「実装計画を書いて」「planを作って」(Phase2へ)、「このplanを実行
  して」「タスクを順に実装して」(Phase3へ)、「バグを直して」「テストが落ちる」
  「原因不明の不具合」(Phase4へ、まず軽量tier)と言われた場合。
---

# Structured Dev Workflow

新機能のアイデアを固めるところから、実装計画を書き、サブエージェントで実行し、出てきたバグを直すところまでの一連の流れを1つのスキルにまとめたもの。4フェーズで構成され、通常は順番に Phase1 → Phase2 → Phase3 と進む。Phase4(デバッグ)はどのタイミングでも独立して呼び出してよい。

## フェーズ一覧

| Phase | 目的 | 参照 | 次のフェーズへの遷移条件 |
|-------|------|------|------------------------|
| 1. Brainstorming | アイデアを設計(spec)に落とし込み、ユーザー承認を得る | `references/01-brainstorming.md` | ユーザーがspecを承認したら Phase 2 へ |
| 2. Writing Plans | specから詳細な実装計画(plan)を書く | `references/02-writing-plans.md` | planを保存したら Phase 3 へ |
| 3. Subagent-Driven Execution | planのタスクを1つずつ新規サブエージェントで実装・レビュー | `references/03-subagent-driven-development.md` | 全タスク完了 → `/code-review` → ユーザーに統合方法を確認 |
| 4. Debugging | バグ・テスト失敗の原因調査と修正(2 tier制) | `references/04-debugging.md` | 軽量tierで収束しなければ既存の `debug-workflow` スキルへ |

## Hard Gates(省略禁止)

- **Phase 1:** ユーザーが設計(spec)を承認するまで、実装・コード作成・プロジェクトの雛形作成など一切の実装行為を行わない。「単純そうだから設計不要」は禁止 — どんなに小さい変更でも短いspecを提示し承認を得る。
- **Phase 3:** タスクレビューで Critical/Important の指摘が残っている状態で次のタスクに進まない。実装者の自己レビューはレビュアーの代わりにならない。
- **Phase 4 (軽量tier):** Phase 1(根本原因調査)を完了する前に修正を提案しない。証拠(エラーメッセージ・再現手順・ログ)に基づかない「たぶんこれが原因」の修正は禁止。

## ディレクトリ構成

```
structured-dev-workflow/
  SKILL.md                              このファイル(ルーター)
  references/
    01-brainstorming.md                 Phase 1 詳細
    02-writing-plans.md                 Phase 2 詳細
    03-subagent-driven-development.md   Phase 3 詳細
    04-debugging.md                     Phase 4 詳細(軽量tier本体 + 重量tierへの案内)
    visual-companion.md                 Phase 1 のオプション機能(ブラウザ上のモックアップ表示)
    root-cause-tracing.md               Phase 4 補助技法
    defense-in-depth.md                 Phase 4 補助技法
    condition-based-waiting.md          Phase 4 補助技法
  templates/
    spec-document-reviewer-prompt.md    Phase 1 のspecレビュー用サブエージェントプロンプト
    plan-document-reviewer-prompt.md    Phase 2 のplanレビュー用サブエージェントプロンプト
    implementer-prompt.md               Phase 3 の実装者サブエージェントプロンプト
    task-reviewer-prompt.md             Phase 3 のタスクレビュアープロンプト
  scripts/
    task-brief                          Phase 3: planから1タスク分のテキストを抽出
    sdd-workspace                       Phase 3: 一時ファイル置き場を解決(.dev-workflow/sdd/)
    review-package                      Phase 3: レビュー用diffパッケージを生成
    find-polluter.sh                    Phase 4: テスト間の状態汚染元を二分探索
    condition-based-waiting-example.ts  Phase 4: condition-based-waiting の実装例
  assets/
    start-server.sh / stop-server.sh    Phase 1 visual companion サーバーの起動/停止
    server.cjs / frame-template.html / helper.js   visual companion サーバー本体
```

## 保存先パスの規約

- spec: `docs/dev-workflow/specs/YYYY-MM-DD-<topic>-design.md`
- plan: `docs/dev-workflow/plans/YYYY-MM-DD-<feature-name>.md`
- Phase 3 の一時ファイル(タスクブリーフ・レポート・レビューパッケージ・進捗台帳): `<repo-root>/.dev-workflow/sdd/`(git-ignore済み、`scripts/sdd-workspace` が自動生成)
- Phase 1 visual companionのセッションファイル: `<project-dir>/.dev-workflow/brainstorm/`(`.gitignore` に追加すること)

## 他スキルとの連携(このrepo内で完結させるための対応表)

このスキルは元々複数の外部プラグインスキルに分かれていた内容を1つに統合したもの。統合時に、このrepoに既にある同等のスキルへ差し替えている箇所がある:

| 統合前に必要だったもの | このrepoでの代替 |
|---|---|
| 最終レビュー用の専用レビュアースキル | `/code-review`(または `/all-code-review` でより深く) |
| 開発ブランチ完了処理スキル | Phase 3完了後にユーザーへ「マージ/PR/そのまま」を確認、PR作成は `pr-create` スキル |
| 完了前検証スキル | `/verify` スキル |
| worktree作成・確認スキル | `pwd` で現在ディレクトリを確認する(AGENTS.mdのworktree運用方針を参照) |
| もう1つのバグ調査スキル(7観点並列) | 既存の `debug-workflow` スキル(Phase 4から重量tierとして案内) |

TDD(失敗するテスト→最小実装→テスト成功→コミット)は独立したスキルに切り出さず、Phase 2 のタスク構造と Phase 4 の Step 1 に直接埋め込まれている。
