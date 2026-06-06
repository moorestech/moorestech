# アップグレードシステム 今後のコード変更ロードマップ

> **For agentic workers:** これは複数サブシステムにまたがる**ロードマップ（索引）**。各フェーズの詳細な逐次TDDプランは**作成済み**（下記リンク）。riding-system が phase1〜4 を個別ファイルに割ったのと同じ運用。本書は割り方・順序・依存関係を示す目次。
>
> **詳細プラン（全て作成済み）:**
> - Phase A（サーバー基盤）: [`2026-06-05-upgrade-system-phase-a.md`](2026-06-05-upgrade-system-phase-a.md)
> - Phase A2（同期＋UI）: [`2026-06-06-upgrade-system-phase-a2-sync-ui.md`](2026-06-06-upgrade-system-phase-a2-sync-ui.md)
> - Phase A3（GearMachine省エネ）: [`2026-06-06-upgrade-system-phase-a3-gear-efficiency.md`](2026-06-06-upgrade-system-phase-a3-gear-efficiency.md)
> - Phase B（品質軸）: [`2026-06-06-upgrade-system-phase-b-quality.md`](2026-06-06-upgrade-system-phase-b-quality.md) ← **着手前に B-0 の3決定をユーザー承認**

**Goal:** 設計仕様（`2026-06-05-upgrade-system-design.md`）とPhase A計画（`2026-06-05-upgrade-system-phase-a.md`）の先にある「今後の具体的なコード変更」を、依存順に並んだ着手可能な単位へ分解する。

**Architecture:** Phase A（サーバー側ロジック＋セーブ＋サーバー操作API）は計画済み・未実装。その上に (A2) ネットワーク同期＋クライアントUI、(A3) GearMachine省エネ適用、(B) 品質軸（レベルファミリー）を積む。A2/A3/B は互いに独立だが、いずれも **Phase A の確定API（`MachineModuleEffect` / `IModuleSlotInventoryComponent` / `MachineModuleSlotComponent`）に依存**するため、Phase A 実装完了が全ての前提。

**Tech Stack:** Unity / C# / NUnit / Mooresmaster SourceGenerator / MessagePack プロトコル / uloop CLI

**設計仕様:** `docs/superpowers/specs/2026-06-05-upgrade-system-design.md`
**Phase A 計画:** `docs/superpowers/plans/2026-06-05-upgrade-system-phase-a.md`

---

## 依存関係と実装順序

```
[Phase A: サーバー基盤]  ← 計画済み・未実装。最優先。これが無いと他は全て着手不可
        │
        ├─→ [Phase A2: 同期＋クライアントUI]   ← モジュールを実際にプレイヤーが装着できるようにする
        │
        ├─→ [Phase A3: GearMachine省エネ]      ← 小。電力経路(A3-3)と対になる歯車経路
        │
        └─→ [Phase B: 品質軸/レベルファミリー]  ← 最大。Aの効果集計にQualityフックを足す
```

**推奨実装順:** A → **A2（同期+UI）** → A3（GearMachine省エネ）→ B（品質軸）。
理由: A2 が無いとモジュールはコード上存在するだけで遊べない（=ドッグフード不能）。A3 は小さく独立で、いつ入れてもよい。B は設計・実装とも最大なので、A の効果集計APIが実プレイで検証されてから着手するのが安全。

**詳細TDDプランは全て作成済み（上記リンク）。** 各プラン冒頭に「API名はPhase A計画に追従。A実装でシグネチャが変わったら同期」のヘッダを付けてある（drift対策）。Phase A 実装でAPIが微調整されたら、A2/A3/B の該当箇所を同期すること。

---

# Phase A2: ネットワーク同期 ＋ クライアントUI

**Goal:** モジュールスロットの内容をサーバー→クライアントへ同期し、機械インベントリ画面にモジュールスロットを表示、プレイヤーがモジュールを装着/取り外しできるようにする。

**前提:** Phase A 完了（`IModuleSlotInventoryComponent` / `MachineModuleSlotComponent` のサーバーAPIが存在）。

**Phase A計画での扱い:** 残課題として明示的にスコープ外（plan §「フェーズAの残課題」）。本フェーズで回収する。

## 調査済みの実パターン（着手前に必読）

- **専用インベントリ取得プロトコルの前例:** `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetFluidInventoryProtocol.cs` — 流体インベントリ専用の取得プロトコル。モジュールスロット取得も同型で新設できる。
- **汎用インベントリ取得/移動:** `InventoryRequestProtocol.cs`（取得）/ `InventoryItemMoveProtocol.cs`（インベントリ間アイテム移動）。プレイヤー↔モジュールスロットの移動はここに乗せるか、専用プロトコルを足す。
- **ブロック状態同期:** `BlockStateProtocol.cs` / `AllBlockStateProtocol.cs`。コンポーネントの state をクライアントへ送る既存経路。
- **クライアント機械UIの基底:** `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/CommonBlockInventoryViewBase.cs` / `MachineBlockInventoryView.cs` / `GearMachineBlockInventoryView.cs` / `IBlockInventoryView.cs`。
- **機械に追加情報UIを足す前例:** `GearEnergyTransformerUIView.cs` — 機械インベントリ画面に副情報パネルを足している実例。モジュールスロット列の追加もこの構造を踏襲。
- **サブインベントリのデータ源:** `UI/UIState/State/SubInventory/BlockSubInventorySource.cs` / `ISubInventorySource.cs` / `ISubInventoryView.cs`。
- **アイテム移動の整合:** モジュールスロットは「Count=1専用・モジュールのみ・装着済み上書き拒否」（Phase A の `TryInsertModule` 制約）。移動プロトコルもこの制約をサーバー側で再検証すること（クライアント送信値を信用しない）。

## 変更範囲（ファイル単位）

**サーバー（新規/修正）:**
- Create: `Server.Protocol/PacketResponse/GetModuleSlotInventoryProtocol.cs` — モジュールスロット内容の取得（`GetFluidInventoryProtocol` をテンプレート）
- Create or Modify: モジュール装着/取り外しプロトコル。`InventoryItemMoveProtocol` に「移動先=モジュールスロット」を通せるか調査し、通せないなら `MoveModuleProtocol.cs` を新設。サーバー側で `MachineModuleSlotComponent.TryInsertModule/RemoveModule` を呼び、制約違反は拒否。
- Modify: `BlockStateProtocol` 経由で「装着中モジュールGUID配列」を state 詳細に載せる（UIの初期描画用）。

**クライアント（新規/修正）:**
- Create: `Client.Game/InGame/UI/Inventory/Block/ModuleSlotRowView.cs`（または機械Viewにスロット列を内蔵）— モジュールスロット1列のView。`ItemSlotView` を流用。
- Modify: `MachineBlockInventoryView.cs` / `GearMachineBlockInventoryView.cs` — `moduleSlotCount > 0` のときモジュールスロット列を描画。`GearEnergyTransformerUIView` の副パネル追加を参考。
- Modify: `BlockSubInventorySource.cs` — モジュールスロットを sub-inventory source として供給し、プレイヤーインベントリとの drag&drop 移動を成立させる。
- Modify: 機械インベントリprefab（Unity YAML。**手編集禁止**。`uloop execute-dynamic-code` 経由 or ユーザーに依頼）。

## タスク群（詳細TDDはA着地後に確定）

- **A2-1 サーバー: モジュールスロット取得プロトコル** — `GetFluidInventoryProtocol` 型で新設。サーバー統合テストで「装着済みモジュールGUID配列が返る」ことを検証。
- **A2-2 サーバー: 装着/取り外し移動プロトコル** — プレイヤーインベントリ↔モジュールスロットの移動。サーバー側で Phase A の挿入制約を再検証するテスト（非モジュール拒否・装着済み上書き拒否・Count≠1拒否）。
- **A2-3 クライアント: モジュールスロット列View** — `moduleSlotCount` 分のスロットを描画、サーバー取得結果を反映。
- **A2-4 クライアント: drag&drop 装着** — プレイヤーインベントリからモジュールをスロットへ移動→A2-2プロトコル送信→再取得で反映。
- **A2-5 PlayModeラウンドトリップ** — 既存の `EditModeInPlayingTest`（最近の `TestElectricToGearGeneratorUI` ブロックが前例）に倣い、機械を開く→モジュール装着→閉じ開き直しで保持、のUI往復を検証。

**完了条件:** プレイヤーが機械を開き、モジュールを装着でき、装着が画面とセーブの両方に反映される。

→ **詳細プラン（作成済み）:** [`2026-06-06-upgrade-system-phase-a2-sync-ui.md`](2026-06-06-upgrade-system-phase-a2-sync-ui.md)

---

# Phase A3: GearMachine の省エネ適用

**Goal:** 省エネ（Efficiency）モジュール効果を、歯車機械の消費（必要トルク）削減にも適用する。Phase A の A3-3 は電力機械の `RequestEnergy` 経路にしか省エネを適用していない。

**前提:** Phase A 完了（`MachineModuleEffect.PowerMultiplier` と、機械が持つ `IModuleSlotInventoryComponent` が存在）。

**Phase A計画での扱い:** 残課題に明示（「GearMachine の消費（RPM/トルク）への省エネ適用は別フォロー。処理時間効果は両機械で有効」）。

## 調査済みの実パターン

- **歯車消費の算出点:** `Game.Block/Blocks/Gear/GearEnergyTransformerComponent.cs:45` の `GetRequiredTorque(RPM rpm, bool isClockwise)` → `GearConsumptionCalculator.CalcRequiredTorque(_consumption, rpm)`。ここが「機械が動力網へ要求するトルク」を返す。
- **電力経路との対比:** 電力機械は `VanillaElectricMachineComponent` の要求電力に倍率を掛ける（A3-3 で `EffectiveRequestPower`）。歯車機械は **トルク要求側に倍率**を掛ける構造になる（電力＝トルク×RPMなので、消費削減＝必要トルク削減）。
- **効果スナップショットの一貫性:** A3 の効果は「処理開始時スナップショット」。歯車消費は連続的に問われるため、`GetRequiredTorque` がスナップショットされた `PowerMultiplier` を参照する経路を、`VanillaMachineProcessorComponent` が保持する `_currentEffect` から引けるよう接続する（処理中でないIdle時は倍率1.0=中立）。

## 変更範囲（ファイル単位）

- Modify: `Game.Block/Blocks/Gear/GearEnergyTransformerComponent.cs` — `GetRequiredTorque` の戻り値に省エネ倍率（`MachineModuleEffect.PowerMultiplier`）を乗じる。機械の `GearEnergyTransformerComponent` か、その派生（機械用の歯車消費コンポーネント）かを Read で特定し、機械文脈でのみ倍率適用（汎用歯車部品に効かせない）。
- Modify: `VanillaGearMachineTemplate.cs` — 歯車機械の消費コンポーネントへ、モジュール効果の参照（`IModuleSlotInventoryComponent` or `VanillaMachineProcessorComponent` のスナップショット）を注入。
- Test: `Tests/CombinedTest/Core/` に「省エネモジュール装着の歯車機械が、未装着より低い必要トルクを要求する」サーバー統合テスト。

## タスク群

- **A3g-1** 失敗テスト: 省エネモジュール装着GearMachineの `GetRequiredTorque` が未装着より小さい。
- **A3g-2** `GearEnergyTransformerComponent`（機械文脈）に倍率適用、効果スナップショット参照を接続。
- **A3g-3** clamp 確認（消費倍率の下限 `MinPowerMultiplier` は A3-1 と共有。トルク0は不正なので下限維持）。

**完了条件:** 省エネモジュールが電力機械・歯車機械の両方で消費を下げる。設計仕様§5.1「全機械共通」の嘘を解消。

→ **詳細プラン（作成済み）:** [`2026-06-06-upgrade-system-phase-a3-gear-efficiency.md`](2026-06-06-upgrade-system-phase-a3-gear-efficiency.md)

---

# Phase B: 品質軸（レベルファミリー）

**Goal:** 品質（Quality）モジュールで、機械の産出物が確率的に上位レベル（Mk2/Mk3…）になる。レベル違いは**独立ItemId**（設計仕様§3決定）で表現し、同一アイテムのレベル変種と、それらを作る合成レシピを定義する。

**前提:** Phase A 完了。特に `MachineModuleEffect` に Quality 軸の集計分岐（空き）が確保済み（A3-1で「Quality はフェーズBで扱う（ここでは無視）」のコメント箇所）。`VanillaMachineProcessorComponent` の決定的抽選（A3-4 `DeterministicRoll`）と仮想容量予約（A3-4）が実装済み。

**設計仕様での扱い:** §8.5 フェーズB、§7.2 抽選順序の決定性、§4.3 レベルファミリー定義。

## 設計仕様で確定済みの方針

- レベル表現 = **独立ItemId**（メタデータ不採用。理由は§3：セーブ/ネットワークがメタデータを直列化しないため）。
- 抽選順序の決定性（§7.2）= `blockInstanceId` ＋ 永続化サイクルカウントから splitmix64 系の決定的RNG。A3-4 の `DeterministicRoll` を品質抽選にも使う。
- 品質シフト = 低レベル確率を圧迫して高レベル確率を上げる分布シフト。分布の和=1を clamp 不変条件で保証（§5.3）。

## 未決事項（B着手時に詰める。設計仕様§9）

- **レベルファミリー定義を `items.yml` 同居か新規スキーマか**（§9）。
- **品質シフトの具体的計算式**（低レベル確率の圧迫ルール）（§9）。
- **決定的GUID生成手段が現状コードに無い**（調査で確認: Core.Master 配下に文字列→GUIDユーティリティ無し。スキーマの `autoGenerated: true` はエディタ/ツール時生成）。レベル変種アイテムのGUIDを「ファミリー定義から決定的に」生成するには新ユーティリティが要る。または変種を明示的に items.json へ書き出す方式にするか、ここで決める。

## 調査済みの実パターン

- **マスタ自動生成:** `VanillaSchema/*.yml` → SourceGenerator → `Mooresmaster.Model.*Module` / `Mooresmaster.Loader.*`。登録は `Core.Master/csc.rsp`（`additionalfile`）＋ `_CompileRequester.cs`（`dummyText` で再生成トリガー）。Phase A の A1-2 で modules.yml を同手順で登録済みのはず。
- **アイテム定義:** `VanillaSchema/items.yml`。レベル変種は新規アイテムとして増える（独立ItemId方針）。
- **合成レシピ:** `VanillaSchema/craftRecipes.yml`（クラフト）/ `machineRecipes.yml`（機械加工）。レベルアップ合成は `craftRecipes` に乗る想定。
- **マスタロード:** `Core.Master/MasterHolder.cs`。Phase A の許容ロード（不在マスタを空扱い）パターンを踏襲。

## 変更範囲（ファイル単位・暫定）

**スキーマ/生成:**
- Create or Modify: レベルファミリー定義スキーマ（`items.yml` 拡張 or 新規 `levelFamilies.yml`）。未決事項の決定に従う。
- Modify: `Core.Master/csc.rsp` / `_CompileRequester.cs` — 新スキーマ登録（新規スキーマ採用時）。

**マスタ:**
- Create: `Core.Master/LevelFamilyMaster.cs`（新規スキーマ採用時）— ファミリー定義の保持と「baseItem→レベルNのItemId」解決。`ModuleMaster.cs`（A1-3）がテンプレート。
- Create: 決定的GUIDユーティリティ（未決事項の決定次第）`Core.Master/DeterministicGuidUtil.cs` 等。
- Modify: `MasterHolder.cs` — `LevelFamilyMaster` 追加（許容ロード）。

**効果/抽選:**
- Modify: `Game.Block/Blocks/Machine/Module/MachineModuleEffect.cs` — A3-1 で空けた Quality 分岐を実装。品質シフト量（分布シフト）を集計。clamp（分布和=1, 各確率∈[0,1]）。
- Modify: `Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs` — 完了時の産出物を、品質分布＋決定的抽選（A3-4 `DeterministicRoll` 再利用）でレベル変種ItemIdへ差し替え。仮想容量予約（A3-4）はレベル変種の出力にも効くこと。

**テスト:**
- Test: `Tests/UnitTest/Core/Other/MachineModuleEffectTest.cs` 追記 — 品質シフトで分布が変わる・和=1・clampのユニットテスト。
- Test: `Tests/CombinedTest/Core/` — 固定シードで「品質モジュール装着時に上位レベル産出が決定的分布で出る」サーバー統合テスト。
- Test: レベルファミリー生成（family定義→期待変種数＋合成レシピ）の検証（設計仕様§10）。

## タスク群（粗）

- **B-0 未決事項の決定** — 定義場所・品質式・GUID手段を確定（仕様§9を埋める）。必要なら brainstorming スキルで詰める。
- **B-1 レベルファミリー定義＋マスタ** — スキーマ/生成/`LevelFamilyMaster`/決定的GUID。
- **B-2 効果集計の Quality 実装** — `MachineModuleEffect` の品質シフト＋clamp（純粋関数ユニットテスト）。
- **B-3 抽選統合** — プロセッサ完了時にレベル変種へ差し替え、決定的抽選、容量予約整合。
- **B-4 セーブ/同期整合** — レベル変種ItemIdがセーブ/A2同期で round-trip（独立IdなのでA2の仕組みにそのまま乗るはず。確認テスト）。

**完了条件:** 品質モジュールで産出物が決定的分布に従い上位レベル化し、セーブ/UIに矛盾なく反映。

→ **詳細プラン（作成済み）:** [`2026-06-06-upgrade-system-phase-b-quality.md`](2026-06-06-upgrade-system-phase-b-quality.md)（B-0でユーザー承認が必要な3決定あり）

---

## まとめ：次にやる具体的アクション

1. **最優先: Phase A を実装する。** 計画は `2026-06-05-upgrade-system-phase-a.md` に逐次TDDで完備（二重Codex監査済み）。実装の進め方は Subagent-Driven（推奨）/ Inline の2択。ただし設計ドキュメント・計画はこのブランチ（feature/cleanroom-design）にあるが、Phase A実装はサーバーコード本体を触るため、**どのブランチで実装するか**を先に決める必要がある（現状の作業ツリー整合に注意）。
2. Phase A 着地後、**A2（同期+UI）の詳細プラン**を本書の変更範囲を元に書き起こす。
3. 並行可能な小タスクとして **A3（GearMachine省エネ）**。
4. 最後に **B（品質軸）**。着手前に B-0 未決事項（定義場所・品質式・決定的GUID手段）を確定。

> **注:** 本書は索引。各フェーズの逐次TDD（失敗テスト→実装→コミットの粒度）は、依存元の実APIが確定してから個別プランに書く。先行して全部TDD化しないのは、Phase A の実装過程でAPIが微調整され、空論プランの全面書き直しを避けるため。
