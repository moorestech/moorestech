---
paths:
  - "moorestech_server/Assets/Scripts/Game.Block/**/*"
  - "moorestech_server/Assets/Scripts/Game.Gear/**/*"
  - "moorestech_server/Assets/Scripts/Game.EnergySystem/**/*"
---

Game.Block系の設計原則（違反はレビューで差し戻される。詳細: `.claude/skills/moores-code-review/references/lens-digest.md`）:

- 基底コンポーネント・共通サービス・Templateにドメイン語彙・`Func<bool>`述語・デフォルト値を持ち込まない。具体側から`SetHoge(値)`でプッシュ（前例: `GearEnergyTransformer.SetTorqueRequestRate`）
- `Update()`内の毎tick同値判定・状態導出は禁止。UniRx変化通知のSubscribeか操作直後プッシュへ。`Update()`は物理進行専用
- 歯車系の回転導出・接続列挙は`SimpleGearService`へ委譲（`=> _gearService.CurrentRpm`）。コンポーネント内に再実装しない
- 新規コンポーネント着手前に、同じ役割interfaceを実装する既存コンポーネントを1つReadして構造を合わせる
