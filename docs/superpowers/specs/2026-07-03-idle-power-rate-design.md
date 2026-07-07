# アイドル時要求エネルギー低減（idlePowerRate）設計書

作成日: 2026-07-03

## 概要

電力・トルクを消費するブロックが、稼働中とアイドル状態で要求エネルギーを変える機能。
アイドル時はマスタデータの比率 `idlePowerRate` を要求値に乗算する。

## 決定事項

| 論点 | 決定 |
|---|---|
| 指定方法 | 比率（`idlePowerRate`）を各ブロックのマスタデータに持たせる |
| スキーマデフォルト値 | **0.2**（エディタ入力時の既定値。JSON では必須キーとして明示する） |
| 歯車ネットワークのハードストップ | **維持**。アイドル中は回るが、加工開始で需要超過になれば停止する。発電追加等で供給が需要を上回れば毎tick再計算により自動復帰。過負荷対策はプレイヤーの電源設計の責務とする |
| 適用範囲 | 消費ブロック全般（電気: 機械・採掘機・ポンプ / 歯車: 機械・採掘機・MapObject採掘機・ポンプ・ベルトコンベア） |
| スコープ | サーバーロジックのみ。クライアント表示は既存の BlockState 同期に自然に追従する範囲でよい |
| 実装方式 | **具体→抽象の一方向依存**。稼働判定と倍率決定は各具体コンポーネント（機械プロセス・採掘機プロセス等）が行い、汎用部品（`GearEnergyTransformer`）へは `SetTorqueRequestRate` で変更要求を叩く。汎用部品は「アイドルかどうか」を知らない |

## マスタデータ（スキーマ）変更

新キー `idlePowerRate`（number、必須、エディタデフォルト 0.2、0〜1想定）を追加する。

- 歯車系: `VanillaSchema/ref/gearConsumption.yml` に `optional: false`（必須）相当で追加。`gearConsumption` は歯車消費5ブロック（GearMachine / GearMiner / GearMapObjectMiner / GearPump / GearBeltConveyor）が共有する ref のため、1箇所で全てに行き渡る
- 電気系: `VanillaSchema/blocks.yml` の ElectricMachine / ElectricMiner / ElectricPump の properties に `optional: false`（必須）相当で追加（`requiredPower` の隣）
- スキーマ編集時は edit-schema スキルに従い、SourceGenerator を再生成する

意味論:
- 電気: アイドル時要求電力 = `requiredPower × idlePowerRate`
- 歯車: アイドル時要求トルク = `CalcRequiredTorque(rpm) × idlePowerRate`（RPM 側は変えずトルクのみ縮む）

## 実装方式

### 電気側（各 `IElectricConsumer.RequestEnergy` の算出箇所）

- 機械: `VanillaMachineProcessorComponent.EffectiveRequestPower` を変更。
  現在の `RequestPower × (Processing ? PowerMultiplier : 1f)` の Idle 側を `idlePowerRate` に差し替える
- 採掘機: `VanillaMinerProcessorComponent` の要求電力算出に `Mining ? 1f : idlePowerRate` を乗算し、
  `VanillaElectricMinerComponent.RequestEnergy`（現在コンストラクタ固定値）はプロセッサ参照の動的プロパティに変更
- ポンプ: `ElectricPumpComponent.RequestEnergy` で内部タンクの受入可否を判定して乗算

### 歯車側（具体コンポーネントから `GearEnergyTransformer` へ変更要求を叩く）

- `GearEnergyTransformer` は要求トルク倍率 `_torqueRequestRate`（初期値1）と `SetTorqueRequestRate(float)` だけを持ち、`GetRequiredTorque` は計算結果に倍率を乗算するのみ。稼働状態・idlePowerRate の語彙を持たない
- 稼働判定とスキーマ必須キー（エディタデフォルト0.2）の参照は各具体側が行い、状態変化時に倍率を叩く
  - GearMachine: `VanillaGearMachineComponent` が processor の `OnChangeBlockState` 購読＋初期化時に叩く
  - GearMiner: `VanillaGearMinerComponent` が同様に `IsMining` で叩く
  - GearMapObjectMiner: `VanillaGearMapObjectMinerProcessorComponent` が初期化時と採掘対象消滅時のみ採掘対象の有無により叩く
  - GearPump: `GearPumpComponent` が `Update` で生成可否により叩く
  - GearBeltConveyor: `GearBeltConveyorComponent` がベルトのアイテム増減イベント購読＋初期化時に叩く
- 発電機・シャフト・歯車等の非消費ブロックは一切変更しない（倍率1のまま）
- ロード直後の初回tickのみ倍率1で要求する可能性があるが、遷移1tickラグとして許容範囲

## ブロック別の稼働（フル要求）判定

| ブロック | エネルギー | 稼働の条件 | 判定元（既存状態） |
|---|---|---|---|
| ElectricMachine / GearMachine | 電気 / 歯車 | `ProcessState.Processing` | `VanillaMachineProcessorComponent.CurrentState` |
| ElectricMiner / GearMiner | 電気 / 歯車 | `VanillaMinerState.Mining` | `VanillaMinerProcessorComponent` |
| GearMapObjectMiner | 歯車 | 採掘対象の MapObject が範囲内に1つ以上存在 | `VanillaGearMapObjectMinerProcessorComponent` の採掘対象リスト |
| ElectricPump / GearPump | 電気 / 歯車 | 内部タンクに空きがある（`Amount < Capacity`） | `PumpFluidOutputComponent` のタンク |
| GearBeltConveyor | 歯車 | ベルト上にアイテムが1つ以上ある | `VanillaBeltConveyorComponent.BeltConveyorItems` |

## 変更不要と確認済みの箇所

- `EnergySegment`: 毎tick `RequestEnergy` を合計し直すため要求変動に自動追従
- `GearNetwork`: 毎tick再計算。ハードストップ仕様は維持
- 加工速度の分母（`MachineCurrentPowerToSubSecond`）: Processing 中しか意味を持たず、Processing 中はフル要求のため影響なし
- セーブデータ・プロトコル: 変更なし（倍率は実行時状態から都度導出）

## 既知の挙動（仕様として受容）

- 歯車ネットワークが「全員アイドルなら回るが全員稼働は賄えない」規模の場合、加工開始時に需要超過で停止し、
  加工中ブロックはフル要求を出し続けるため停止が継続する。プレイヤーが発電を増強すれば自動復帰する
- アイドル→稼働の遷移直後の1tickは、直前のアイドル水準の供給で加工が始まるため初tickのみわずかに遅い（許容）

## テスト計画

creating-server-tests スキルに従い作成する。

1. 電気機械: レシピ材料なし（Idle）で `RequestEnergy == requiredPower × idlePowerRate`、材料投入で Processing → フル要求、完了後 Idle に戻り再び低減
2. 歯車機械: アイドル時の要求トルクが低減され供給不足だったネットワークが回ること／加工開始で需要超過 → 既存ハードストップが発動すること
3. 電気採掘機・ポンプ・ベルトコンベア: 各アイドル条件（鉱石なし・タンク満杯・ベルト空）で要求が低減されること
4. EnergySegment 結合: アイドル機械が要求を下げた分、同一ネットワークの稼働中機械への配分が増えること
5. マスタデータ: テスト用 blocks.json に `idlePowerRate` 明示指定ブロックを追加し、必須キーとして読み込まれることを検証
