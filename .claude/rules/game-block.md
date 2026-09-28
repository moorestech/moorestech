---
paths:
  - "moorestech_server/Assets/Scripts/Game.Block/**/*"
  - "moorestech_server/Assets/Scripts/Game.Gear/**/*"
  - "moorestech_server/Assets/Scripts/Game.EnergySystem/**/*"
---

AGENTS.md「設計原則」（基盤にドメイン語彙を持ち込まずSetHogeでプッシュ・Update()は物理進行専用・前例を探す）がこの配下で最も差し戻されている。詳細: `.claude/skills/moores-code-review/references/moores-reviewer-digest.md`

- 基底へのプッシュの前例: `GearEnergyTransformerComponent.SetTorqueRequestRate`
- 歯車系の回転導出・接続列挙は`SimpleGearService`へ委譲（`=> _gearService.CurrentRpm`）。コンポーネント内に再実装しない
