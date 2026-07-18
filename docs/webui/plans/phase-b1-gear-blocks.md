# Phase B1 実行計画: ギア系ブロック仕上げ

親: `../MIGRATION.md` / 進捗: `../TODO.md`
旧台帳 FEAT-BLK-6/7 相当。依存なし・即着手可。

## 背景

ギア**機械**（GearMachine/GearMiner）は Web 化済みだが、ギア**伝達**系ブロックが Web の
ブロックコンポーネントレジストリ未登録で Generic 表示に落ちる。ElectricToGearGenerator は
専用ビュー自体が未実装。

## タスク1: ギア伝達系のレジストリ登録

- **⚠ やってはいけない**: `GearEnergyTransformer` というキーをレジストリに追加する
  （その blockType はスキーマにも v8 マスタにも存在しない）
- 手順:
  1. `../../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json` で
     `blockUIAddressablesPath: "Vanilla/UI/Block/GearEnergyTransformerUI"` を持つ blockType を
     **再列挙して確定**（前回調査時の答えは Shaft / Gear / GearChainPole の5ブロックだが必ず裏取り）
  2. 確定 blockType を Web の blockComponents レジストリ（`src/features/blockInventory/`）に登録し、
     既存ギア UI ビュー（トルク/RPM/ネットワーク状態表示）を共用
- uGUI 参照実装: `InGame/UI/Inventory/Block/GearEnergyTransformerUIView.cs`
  （ブロック名 + トルク/RPM + ギアネットワーク集約 + 停止理由テキスト `GetStopReasonText`）
- 性能注記: トルク/RPM は連続変動値。Topic は変化時 publish でなく**固定間隔（例: 4tick）
  サンプリング**とし、ギアネットワーク全走査の高頻度実行を避ける（既存 GearMachine topic の方式に合わせる）

## タスク2: ElectricToGearGenerator 専用ビュー

- uGUI 参照実装: `InGame/UI/Inventory/Block/ElectricToGearGeneratorBlockInventoryView.cs` +
  `ElectricToGearOutputModeRowView.cs`
- 表示: 出力モード行の動的生成 + 充足率スライダー + 消費電力テキスト + StateDetail 同期
- 操作: モード選択送信（uGUI は `SetElectricToGearOutputMode`）。Topic/Action 配線が無ければ
  順序規約どおり C# 側から: `BlockDetailDtoBuilder` へ capability 追加 → action 追加 →
  `WebUiGameBinder` 登録 → `protocol.ts`/WireFixtures → ビュー
- v8 に実ブロック1種。実機確認はそのブロックで行う

## タスク3: 再発防止 e2e

- v8 blocks.json の全 blockType × Web レジストリ照合テストを追加し、「専用 UI を持つ blockType の
  レジストリ漏れ」を機械検出する（Generic 落ちの再発防止。意図的 Generic の allowlist を持つ）

## 完了条件

- 対象6ブロック（伝達系5 + ElectricToGear 1）が Generic 落ちせず専用 UI で開き、
  モード変更が操作できる（e2e + 実機）
- レジストリ網羅テストが green

## 検証

vitest / 該当 e2e / `.cs` 変更時 `uloop compile` / 完了時コミット + `../TODO.md` 更新
