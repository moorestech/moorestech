---
name: moores-grill-with-docs
description: |
  A relentless interview to sharpen a plan or design, which also creates docs (ADRs and glossary) as we go.
  grill-with-docs（mattpocock/skills）のmoorestechローカルフォーク。上流との同期はせずオリジナル路線で編集する。
  Use when:
  1. 「設計を詰めたい」「壁打ちしたい」「〜機能を作りたい」「仕様を相談したい」と設計対話を始める時
  2. 「grillして」「grill-with-docsで」と言われた時
---

Run a `/grilling` session, using the `/domain-modeling` skill.

## moorestech追加規約

### 1. ADR出所欄（必須）

各ADRの決定（Considered Optionsの採択・却下を含む主要な裁定）には、誰が決めたかを機械可読に記録する:

- **ユーザー裁定**: ユーザーの発言引用または質問への回答に基づく決定。日付つきで記録する。
  例: `出所: ユーザー裁定 2026-07-28「ツールは装備スロットで使いたい」`
- **agent前提**: agentが原則・前例・調査から決めた事項。適用した根拠の実名を添える。
  ユーザーが文書を黙認しても、agent前提はユーザー裁定に昇格しない（免責力を持たない）。
  例: `出所: agent前提（既存GrabInventory同型の先行パターン）`

後日「これは誰が決めたか」を遡れることが目的。出所の偽装（agent判断を裁定済みの顔で書く）は禁止。

### 2. 設計完了後は writing-plans へ接続

設計・ADRが確定したら、実装着手前に writing-plans スキルで実装計画を作成する。
writing-plans 側の user-simulator による plan review（sim-gate配線）は既存のまま維持する。
設計フェーズでは user-simulator を自動起動しない（大きな設計で必要な場合のみユーザーが手動起動する）。
