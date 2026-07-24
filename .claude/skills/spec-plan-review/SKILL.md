---
name: spec-plan-review
description: |
  【廃止・後継: user-simulator】旧・観点別レンズ並列レビュー。2026-07-24のユーザー裁定で
  ユーザーシミュレーター方式（単一Fable判事＋sonnet斥候＋opus反証役）に再構成された。
  Use when: 「spec-plan-reviewで」と言われた時（user-simulatorのreviewモードへ委譲する）。
---

# spec-plan-review（廃止）→ user-simulator へ

このスキルは **user-simulator** に置き換えられた。呼ばれたら `.claude/skills/user-simulator/SKILL.md` を読み、
**reviewモード**を実行すること。

- 旧レンズ6本は `user-simulator/knowledge/lenses/` に検査観点ファイルとして移設済み（エージェントとしては廃止）
- ADR（判断記録）仕様は各docのADRセクション運用として継続。スキル自身の判断は `user-simulator/decisions.md`
- 再構成の経緯・却下案は `user-simulator/decisions.md` #1〜#5 を参照
