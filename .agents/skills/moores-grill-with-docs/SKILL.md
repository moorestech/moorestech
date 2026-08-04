---
name: moores-grill-with-docs
description: |
  これはブレスト用のskillです。A relentless interview to sharpen a plan or design, which also creates docs (ADRs and glossary) as we go.
  Use when:
  1. 「設計を詰めたい」「壁打ちしたい」「〜機能を作りたい」「仕様を相談したい」と設計対話を始める時
  2. 創作的な作業（機能の作成、コンポーネントの構築、機能追加、挙動の変更）や設計相談 — 壁打ち / 仕様相談 / 「これどうしたらいい？」型の相談 — で発火する
  3. 「grillして」「grill-with-docsで」「ブレストして」と言われた時
hooks:
  PostToolUse:
    - matcher: "Write|Edit"
      hooks:
        - type: command
          command: "bash .claude/skills/user-simulator/scripts/shadow-gate.sh track"
  Stop:
    - hooks:
        - type: command
          command: "bash .claude/skills/user-simulator/scripts/shadow-gate.sh stop"
---

Run a `/grilling` session, using the `/domain-modeling` skill.

設計対話中のB判定（設計原則との照合）には [references/moorestech-principles.md](references/moorestech-principles.md) を参照する（旧brainstormingから移設。user-simulatorの知識indexも同ファイルを参照している）。

## HARD GATE（実装着手の禁止）

設計裁定が出揃いADRを書き終えてwriting-plansへ接続するまで、実装スキルの起動・コードの書き込み・プロジェクトのscaffoldを一切行わない。「シンプルすぎて設計不要」という例外は無い — TODOリスト1個・関数1本・設定変更1行でも通す。真に単純なら対話は数問で終わる。短くてよいが省略しない。

## moorestech追加規約

### 1. ADR出所欄（必須）

各ADRの決定（Considered Optionsの採択・却下を含む主要な裁定）には、誰が決めたかを機械可読に記録する:

- **ユーザー裁定**: ユーザーの発言引用または質問への回答に基づく決定。日付つきで記録する。
  例: `出所: ユーザー裁定 2026-07-28「ツールは装備スロットで使いたい」`
- **agent前提**: agentが原則・前例・調査から決めた事項。適用した根拠の実名を添える。
  ユーザーが文書を黙認しても、agent前提はユーザー裁定に昇格しない（免責力を持たない）。
  例: `出所: agent前提（既存GrabInventory同型の先行パターン）`

後日「これは誰が決めたか」を遡れることが目的。出所の偽装（agent判断を裁定済みの顔で書く）は禁止。

### 2. 出口の一本化（writing-plans へ直行）

設計・ADRが確定したら、終端状態は「**同一セッションでの writing-plans スキル起動**」のみ。spec等の中間文書は書かない — 要件は会話コンテキスト経由でplan先頭の `## Requirements` セクションへ流れ込む。他スキル・実装への分岐は禁止。
writing-plans 側の user-simulator による plan review（sim-gate配線）は既存のまま維持する。
設計フェーズでは user-simulator を自動起動しない（大きな設計で必要な場合のみユーザーが手動起動する）。

### 3. セッション終了時の自動シャドー採点（shadow-gate配線）

設計成果物（設計doc/plan）を書き終えたら、セッションを終える前に user-simulator の **shadowモード**
（`.claude/skills/user-simulator/modes/shadow/protocol.md`）で自セッションのtranscriptを盲検採点する。
インライン予測はしないので設計対話中の体感遅延はゼロ。採点はセッション末尾に1回だけ行う。

- 発動はfrontmatter hooksの shadow-gate（Stop関所）が機械的に保証する。設計doc書き込みで武装し、
  `user-simulator/datasets/` への格納で解除される（ブロックメッセージに自transcriptパスが入る）
- 予測体は **model: opus必須明示**・1質問1エージェント・バックグラウンド起動可
- 採点・永続化・misses.md記録まで shadowモード手順のとおり実施してからセッションを終了する
