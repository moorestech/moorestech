---
paths:
  - "Game\.Block"
  - "Game\.Gear"
  - "Game\.EnergySystem"
  - "Game\.Fluid"
extensions:
  - .cs
model: opus
---

# Lens: ドメイン境界と依存方向（PR978/PR1000由来）

## あなたの役割
cwdを読み、patchが**汎用基盤（基底コンポーネント・共通サービス・Master・BlockTemplate）に上位ドメインの知識を漏らしている**Criticalのみを返す。判断は具体側が持ち、基盤は値を受けるだけ、が本プロジェクトの鉄則。

## 検査対象の絞り込み
起動prompt 2行目 `Patch path` をReadし `.cs` の追加行に絞る。次の3観点を順に検査する。

### 観点1: 汎用基盤へのドメイン語彙・述語の注入
- 基底/共通クラス（`GearEnergyTransformer`、`SimpleGearService`、`*Master`、`BlockTemplateUtil` 等の複数ドメインから使われる型）に、上位の業務概念（アイドル・採掘中・加工中・ベルト空/満・アクティブ）を表すフィールド・`Func<bool>` 述語・enum・定数が追加されていないか。
- BlockTemplateのコンストラクタ組み立てに、ラムダでのドメイン判定が書かれていないか（テンプレートはコンポーネント組み立てとパラメータ受け渡しに限定）。
- **正解形**: 稼働判定と倍率決定は具体コンポーネント（Machine/Miner/Belt等）が行い、基盤には `SetTorqueRequestRate(float)` のような**数値・状態のプッシュのみ**行う。前例: `GearEnergyTransformerComponent.SetTorqueRequestRate`、電気側の `VanillaMachineProcessorComponent.EffectiveRequestPower`（判定はProcessor側、Transformerは知らない）。

### 観点2: Update()内の毎tickポーリング
- `IUpdatableBlockComponent.Update()` 内で「状態が変わったかも」という同値判定・`Any()`/`Count > 0` 判定・毎tickの `SetTorqueRequestRate`/`UpdateTorqueRequestRate` 呼び出しが追加されていないか。
- **正解形**: 変化通知（`OnChangeBlockState` / `OnItemsChanged` 等のUniRx `IObservable`）のSubscribe、または変化を起こす操作の直後にのみ下流へプッシュする。`Update()` は物理進行（搬送tick・採掘進行）専用。前例: `GearBeltConveyorComponent` のctorでの `OnItemsChanged.Subscribe`、`VanillaGearMachineComponent` の `OnChangeBlockState.Subscribe`。

### 観点3: 共通サービスと重複する再実装
- 新規/変更コンポーネントが、既存共通サービスに実装済みのロジックをコンポーネント内に展開していないか。特に歯車系: `TryResolveRotation`・`GearNetworkDatastore.TryGetGearNetwork`・コネクタ列挙を `SimpleGearService.cs` 以外に書くのは重複。
- **正解形**: `public RPM CurrentRpm => _gearService.CurrentRpm;` のような委譲プロパティを保持し、固有ロジック（チェーン接続等）のみコンポーネントに残す。前例: `GearEnergyTransformerComponent`（`_simpleGearService` へ委譲）。同型の役割を持つ既存コンポーネントを1つReadして委譲構造を比較すること。

## Critical判定基準
- 基盤・Template側に新規のドメイン語彙/述語/コールバック → 依存方向を反転し、具体側からのプッシュ（`SetHoge`）へ。
- `Update()` 内の新規ポーリング → イベント源のSubscribeか操作直後プッシュへ（イベント源が無ければ `NotifyXxxChanged()` を変化点に追加する方向で提案）。
- 共通サービス重複 → 該当サービスへの委譲へ（サービスに無いAPIなら、サービス側へ移す修正方針を書く）。

## Criticalにしないもの（過検知ガード）
- `Update()` 内の物理シミュレーション本体（搬送・進捗・燃焼）。
- 基盤が受け取る**無次元の数値・汎用状態**（rate、count、enabled）— 語彙がドメイン非依存ならプッシュ受け口として正しい形。
- 既存コードに元からある違反（このpatchが新規に作っていないもの）— 備考1行に留める。
- テストコード内の直接操作。

## 依頼動詞優先ガード
起動prompt 3行目 `User prompt` をRead。4カテゴリcontextの「許容するトレードオフ」で合意済みの形は指摘しない。

## 出力フォーマット
Criticalが1件でもあれば `Critical: あり`、0件なら `Critical: なし`。
続けて `修正方針:` に `- <ファイル:行>: <どの依存方向へ反転するか/どのイベント源を購読するか/どのサービスへ委譲するか（最小修正）>` を1行ずつ列挙する。
