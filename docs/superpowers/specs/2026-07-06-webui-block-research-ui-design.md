# WebUI ブロック詳細UI×5 + 研究ツリー 設計書

**作成日**: 2026-07-06
**対象**: FEAT-BLK-2(発電機) / BLK-3(機械) / BLK-4(採掘機: 電気+ギア) / BLK-5(ギア機械) / BLK-8(フィルタ分岐器) / FEAT-RES-1(研究ツリー)
**関連**: `docs/webui/TODO.md`, `docs/webui/cef-webui-migration-todo.md`

---

## 0. スコープ

### 含む
- ブロックインベントリの種別別詳細表示: ElectricGenerator / FuelGearGenerator / SimpleGearGenerator / ElectricMachine / GearMachine / ElectricMiner / GearMiner / FilterSplitter
- FilterSplitter の設定操作（モード切替・フィルタアイテム設定/クリア）
- 研究ツリー（閲覧 + 研究実行）
- C#ホスト側 Topic/Action、WireFixtures、Web側ビュー、mock-host、vitest/e2e

### 含まない
- BLK-6(GearEnergyTransformer) / BLK-7(ElectricToGearGenerator)（指定外。ただし D1 の capability 合成により将来は組み替えで対応可能）
- INFRA-1(CEF恒久統合) / INFRA-4(型自動生成) / INFRA-11(i18n)
- 実機 web↔host 連携検証（INFRA-1 解消待ち。検証は mock-host e2e + コンパイルまで）

---

## 1. 確定済み意思決定（後戻りコスト高・ユーザー承認済み）

| # | 決定 | 根拠 |
|---|---|---|
| D1 | ブロック詳細は **capability合成**（`machine?` + `gear?` + `gearNetwork?` 等の機能単位optional）。ブロック種別unionにしない | サーバーのコンポーネント設計（StateDetailキー単位）と同型。将来ブロックは組み替えで対応 |
| D2 | サーバー由来stateの経路は **topic一本**。Action `result` は ok/error のみ維持 | Single Source of Truth。Action結果は C# が topic 再publish で届ける |
| D3 | 全画面UI開閉の主権は **C#/uGUI**（台帳D2既決の踏襲）。Rキー→`UIStateControl` 遷移を topic が映す | blockInventory と同型。チャレンジ・列車・ポーズの前例になる |
| D4 | ツリー描画は **研究専用実装 + レイアウト計算のみ純関数分離**。共通基盤化は2例目(CHAL-1)で抽出 | YAGNI。純関数化で抽出は機械的 |
| D5 | TS型は**手書き継続**（INFRA-4 は今やらない） | WireFixtures が C#⇔TS 両側から契約を強制するため後日置換も安全 |

### 前提（トリアージ済み・宣言）
- moorestech_server 本体の改修は不要。必要データは全てクライアント受信済み
- C#側は「WebUiHostのTopic/Action → WireFixtures → TS」の既存型を踏襲（**Client.Game 編集ゼロ**。既存公開getter/イベント直接購読のみ）
- 高頻度値（トルク/RPM）は既存 end-of-frame デバウンスに乗せる。サンプリング間引き等の最適化はしない
- UIは uGUI パリティ（ズームなし・スクロールのみ・停止理由テキストはハードコード）
- 消費アイテム充足ハイライトは Web側で inventory topic から算出（craftビューの `buildOwnedCounts` 型）

---

## 2. ワイヤ契約

> フィールド形状は実装中に変わり得る（変更容易・WireFixturesで両側強制）。軸となる構造のみ規範とする。

### 2-a. `block_inventory.current` 拡張（BlockInventoryOpen）

既存フィールドに加え、該当ブロックのみ optional 詳細を付与（`NullValueHandling.Ignore` で非該当キーは省略）:

```ts
type BlockInventoryOpen = {
  open: true;
  blockType: string; identifier: string; blockName: string;
  itemSlots: SlotData[];
  fluidSlots: FluidSlotData[];   // FluidMachineInventoryStateDetail から充填（既存の器）
  progress?: number;             // MachineBlockStateDetail.ProcessingRate 等から充填（既存の器）
  machine?: {
    recipeGuid: string;
    currentState: string;
    currentPower: number; requestPower: number;
    slotLayout: { input: number; output: number; module: number };  // itemSlots の分割位置
  };
  generator?: { remainingFuelTime: number; currentFuelTime: number; operatingRate: number };
  miner?: {
    currentPower: number; requestPower: number;
    miningItems: { itemId: number; itemsPerMinute: number }[];  // C#がマスタから算出
  };
  gear?: {
    isClockwise: boolean;
    currentRpm: number; currentTorque: number;
    baseRpm: number; baseTorque: number;  // マスタ GearConsumption 由来
  };
  electricNetwork?: {  // 開いている間 C# が1秒間隔ポーリング
    totalGeneratePower: number; totalRequiredPower: number;
    consumerCount: number; powerRate: number;
  };
  gearNetwork?: {      // 開時に1回取得（uGUIパリティ）
    totalRequiredGearPower: number; totalGenerateGearPower: number;
    stopReason: "none" | "rocked" | "overRequirePower";
  };
  filterSplitter?: {
    directionCount: number; filterSlotCountPerDirection: number;
    directions: { mode: "default" | "whitelist" | "blacklist"; filterItemIds: number[] }[];
  };
};
```

- `powerRate`（機械/採掘機）はワイヤに乗せず Web側で `requestPower==0 ? 1 : currentPower/requestPower` を算出（uGUIと同式）
- enum は camelCase 文字列（wire は可読JSON）
- **capability組み合わせ表**:

| blockType | 付与される capability |
|---|---|
| ElectricMachine | machine (+fluidSlots/progress) + electricNetwork |
| GearMachine | machine (+fluidSlots/progress) + gear + gearNetwork |
| ElectricGenerator | generator + electricNetwork |
| FuelGearGenerator / SimpleGearGenerator | generator + gear + gearNetwork |
| ElectricMiner | miner (+progress) + electricNetwork |
| GearMiner | miner (+progress) + gear + gearNetwork |
| FilterSplitter | filterSplitter（itemSlots 空） |

### 2-b. 新topic `research.tree`

nodes のみを運ぶ（**表示可否は既存 `ui_state.current` topic の `state === "ResearchTree"` から導出**。
計画作成時に `UiStateTopic`/`screenForUiState` の既存実装を発見したため、open union による状態の二重配信を避ける — D2/SSOT 準拠）:

```ts
type ResearchTreePayload = { nodes: ResearchNodeDto[] };

type ResearchNodeDto = {
  guid: string;
  name: string; description: string;
  state: "completed" | "researchable"
       | "unresearchableNotEnoughItem" | "unresearchableNotEnoughPreNode" | "unresearchableAllReasons";
  position: { x: number; y: number };   // マスタ GraphViewSettings.UIPosition
  prevGuids: string[];
  consumeItems: { itemId: number; count: number }[];  // Guid は C# が ItemId へ変換
  rewardItemIds: number[];   // clearedActions: giveItem 由来
  unlockItemIds: number[];   // clearedActions: unlockItemRecipeView 由来
};
```

- ノード再取得は uGUI と同じく ResearchTree 突入時（`UIStateControl.OnStateChanged` 購読、C#追加改修ゼロ）
- 状態int→文字列対応: 0=completed, 1=researchable, 2=unresearchableAllReasons, 3=unresearchableNotEnoughItem, 4=unresearchableNotEnoughPreNode

### 2-c. 新Action

| ActionType | payload | 挙動 |
|---|---|---|
| `research.complete` | `{ researchGuid: string }` | `va:completeResearch` 送信 → 応答 `NodeState` で topic 再publish。失敗は `research_failed` |
| `filter_splitter.set_mode` | `{ directionIndex, mode }` | 明示モード指定（冪等・サイクルにしない）。対象は現在開いているブロック |
| `filter_splitter.set_filter_item` | `{ directionIndex, slotIndex, clear }` | `clear:false` は C# が Grab の持ち手アイテムをセット（uGUIと同じ権威）。応答で topic 再publish |

---

## 3. C#ホスト側設計（Client.WebUiHost）

### 3-a. 情報源（uGUI編集ゼロ・計画時に確定）
- ブロック状態変更の検知は WebUiHost が `va:event:changeBlockState:{pos}` サーバーイベントを**直接購読**（`CraftingRecipesTopic` と同型）。データは既存公開の `BlockGameObject.GetStateDetail<T>(key)` を読む
- `BlockGameObject` の解決は既存公開の `ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(pos)`。**Client.Game への変更は一切不要**（並行UIStateセッションとの衝突回避）

### 3-b. BlockInventoryTopic の拡張
- 開いたブロックの `OnBlockStateChanged` を購読 → end-of-frame デバウンスで再publish（閉時に購読解除）
- capability別 DTO ビルダーをディレクトリ分割（1ファイル200行制約）: `Game/Topics/BlockDetail/` 配下に MachineDetailBuilder / GeneratorDetailBuilder / MinerDetailBuilder / GearDetailBuilder / NetworkInfoPoller / FilterSplitterDetailLoader 等
- blockType → 付与capability の判定は StateDetail の**存在**で行う（`GetStateDetail` が null なら省略）。マスタ由来値（slotLayout / baseRpm / miningItems 等）は `BlockMasterElement` の param から取得
- electricNetwork: 開いている間 1秒間隔で `GetElectricNetworkInfo` をポーリングし変化時 publish。gearNetwork: 開時1回 `va:getGearNetInfo`。filterSplitter: 開時1回 `CreateGetRequest`
- 列車等の非ブロックsourceは従来通り「閉」扱い（既存挙動維持）

### 3-c. ResearchTopic（新規、`research.tree`）
- `UIStateControl.OnStateChanged` 購読。ResearchTree 突入時に `GetResearchNodeStates` を取得し、`MasterHolder.ResearchMaster.GetAllResearches()` と合成して publish。表示可否の判定は `ui_state.current` 側の責務（本topicは退出時に何もしない）
- Guid→ItemId 変換は `MasterHolder.ItemMaster` で実施

### 3-d. ActionHandler（新規3つ）
- `ResearchCompleteActionHandler`: `ClientContext.VanillaApi.Response.CompleteResearch` → 応答 `NodeState` で ResearchTopic 再publish。`Success=false` は `ActionResult.Fail("research_failed")`
- `FilterSplitterSetModeActionHandler` / `FilterSplitterSetFilterItemActionHandler`: 現在開いているブロック座標で `va:filterSplitterState` リクエスト送信 → 応答スナップショットを BlockInventoryTopic 経由で再publish。ブロックが閉じている場合は `block_not_open` で Fail
- `WebUiGameBinder.Bind()` に登録追加。新エラーコードは `error_codes.json` + `WireContractTest` の期待集合に追加

---

## 4. Web側設計（moorestech_web/webui）

### 4-a. bridge 層
- `protocol.ts`: `Topics.researchTree` 追加、`TopicPayloads` / `ActionPayloads` に登録
- `payloadTypes.ts`: §2 の型を追加（BlockInventoryOpen 拡張 + ResearchTreePayload）
- `validators.ts`: capability別バリデータ + researchTree バリデータ。違反は既存どおり破棄+toast

### 4-b. ブロックビュー（features/blockInventory/）
- `blockComponents` レジストリに8種登録（capability合成なので**ビューはcapability部品の組み合わせ**）:
  - `views/` サブディレクトリに GeneratorInventory / MachineInventory / GearMachineInventory / MinerInventory / GearMinerInventory / FilterSplitterInventory（1ディレクトリ10ファイル制約に対応）
  - `details/` サブディレクトリに capability 表示部品: MachineSection（入力→出力→モジュールのslotLayout分割グリッド + ProgressArrow + 電力率） / GeneratorSection（燃料グリッド + 燃料プログレス + 稼働率） / MinerSection / GearSection（トルク/RPM、不足時赤） / ElectricNetworkSection / GearNetworkSection（停止理由） / FilterSplitterColumns（方向カラム + モードボタン + フィルタスロット左=設定/右=クリア）
- 共有部品（ItemSlot / FluidSlot / SlotGrid / ProgressArrow）を再利用

### 4-c. 研究ツリー（features/research/ 新規）
- `ResearchTreePanel`: フルスクリーンオーバーレイ。スクロールコンテナ内に `position:absolute` でノード配置（UIPosition直置き）、接続線は「距離+角度」のdiv（CSS transform、最背面）
- `researchLogic.ts`（純関数・D4）: ノード境界からのキャンバスサイズ算出（uGUI `TreeViewAdjuster` 同等・padding 200）、接続線の距離/角度算出、消費アイテム充足判定（inventory topic と突合）
- ノードカード: 名前/説明/報酬・解放アイテムアイコン/消費アイテム（充足ハイライト）/研究ボタン（`state=="researchable"` のみ活性）→ `dispatchAction("research.complete")`
- `activeLayer` に `research` 追加（優先順位: modal > blockInventory > research > game）

### 4-d. エラーハンドリング
- Action失敗は既存 `dispatchAction` の toast 経路。`research_failed` / `block_not_open` は BENIGN 扱いにしない（明示表示）
- バリデーション違反 payload は破棄+toast（既存機構）

---

## 5. テスト戦略

| 層 | 内容 |
|---|---|
| WireFixtures | 新規: machine詳細つきopen / gearMachineフル合成 / generator(progress省略) / miner / filterSplitter / research_tree。C# `WireContractTest` と TS `wireContract.test.ts` 両側で照合 |
| vitest | `researchLogic`（キャンバスサイズ/接続線/充足判定）、capability分岐ロジック、filterSplitterモード表示ロジック等の純関数テスト |
| mock-host | fixtures に各ブロック種別 + research を追加。`/__block?type=machine|generator|miner|gearMachine|gearMiner|filterSplitter` 拡張、`/__uistate?state=ResearchTree` 追加。新Action を `KNOWN_ACTIONS` + apply ロジックに追加 |
| Playwright e2e | 各ブロックビューの表示検証 + filterSplitter操作の `/__actions` payload検証 + 研究ボタンのaction payload検証 |
| C# | `uloop compile` ErrorCount 0。`WireContractTest` / `ErrorCodesFixtureCoversAllHandlerCodes` green |

## 6. 実装順序（概要）

1. ワイヤ契約層（payloadTypes/validators/protocol + C# DTO + WireFixtures）— 契約を先に固定
2. C#ホスト: BlockGameObject additive event → BlockInventoryTopic capability拡張 → ResearchTopic → ActionHandler×3
3. Web: capability部品（details/）→ ブロックビュー6種 + レジストリ登録 → 研究ツリー
4. mock-host / e2e / vitest 整備、`uloop compile`、全テスト green 確認
