---
name: brainstorming
description: "創作的な作業（機能の作成、コンポーネントの構築、機能追加、挙動の変更）や設計相談 — 壁打ち / 仕様相談 / 「これどうしたらいい？」型の相談 — で発火する。ただし本スキルはアーカイブ済みで、実体は moores-grill-with-docs への転送のみを行う。"
---

# brainstorming（アーカイブ済み）

このスキルはアーカイブされた。設計対話の本命は **moores-grill-with-docs** に移行している。

**このスキルが発動したら、他の作業を行わず直ちに Skill ツールで `moores-grill-with-docs` を起動し、以後はそちらの指示に従うこと。**

## アーカイブの経緯と将来の方針

- 2026-07-28、同一タスクを両方式で実走比較した結果（調査深度・決定カバレッジ・QA具体性で grill 側が優位）、設計フェーズを grill-with-docs 系へ全面移行した（ユーザー裁定）。
- 旧本文は [ARCHIVE-SKILL.md](ARCHIVE-SKILL.md) にそのまま保存されている。design-question-triage（A/B/C分類）・ビジュアルコンパニオン・references/moorestech-principles.md 等の資産も本ディレクトリに残置している。
- **今後 grill 側の運用で不満が出た場合は、その不満を分析し、対応する機構（例: 質問トリアージ、preanswer 予測、セルフ反証、台帳承認ゲート）を本アーカイブから moores-grill-with-docs へ移植する可能性がある。** アーカイブは削除しないこと。
- user-simulator による review / sim-gate は writing-plans 段階に限定運用する（本スタブには hooks を配線しない）。
