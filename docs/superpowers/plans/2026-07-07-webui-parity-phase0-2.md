# WebUI パリティ Phase 0〜2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** パリティ監査の裏取り結果（`docs/webui/2026-07-07-parity-audit-verification-handoff.md`）を台帳へ一本化し、優先度1の「ブロックスロット操作パリティ」（右クリック半分取り/1個置き・ダブルクリック収集・Shift直接移動・e2e網羅）を実装する。

**Architecture:** 右クリック・Shift移動は既存の `block_inventory.move_item` を数量クライアント計算で使う（uGUI自身が `PlayerInventorySlotInteraction` / `PlayerInventoryDirectMover` で数量をクライアント計算しており、これが正のパリティ）。ダブルクリック収集のみサーバ集約が必要なため C# に `block_inventory.collect` ハンドラを新設し、既存 `inventory.collect` の「収集先は host の grab 状態で決める」規約を踏襲する。uGUI 側コードは一切変更しない（受動的統合）。

**Tech Stack:** React + zustand + Mantine（`moorestech_web/webui`）、vitest（純ロジック）、Playwright + mock-host（e2e）、C# Unity（`Client.WebUiHost`）、uloop CLI。

## Global Constraints（AGENTS.md より・全タスクに適用）

- 作業開始時に必ず `pwd` を確認。各タスク末尾で必ずコミット（作業消失防止）
- 1ファイル200行以下・1ディレクトリ10コードファイルまで・**partial 絶対禁止**
- 主要処理に日本語→英語の2行セットコメント（各1行厳守）。自明コメントは書かない
- 単純 getter/setter プロパティ禁止（`ActionType => "..."` のようなインターフェース実装の式形式は既存前例どおり可）
- try-catch 基本禁止（外部境界のみ・根拠コメント必須）。デフォルト引数禁止
- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client`（ErrorCount 0 を確認）
- uloop が「Unity is reloading (Domain Reload in progress)」を返したら **45秒待機してリトライ**
- `.meta` 手動作成禁止（本計画は新規 .cs ファイルを作らないので発生しない）
- web 側コマンドは全て `moorestech_web/webui` で実行: `pnpm test`（vitest）/ `pnpm build`（tsc -b + vite build）/ `pnpm test:e2e`（e2e用tsc + Playwright, mock-host 自動起動）
- コミットメッセージは既存ログの慣例（`feat(webui):` / `docs(webui):` / `chore:` 等の日本語サマリ）に合わせる

## 配置と前例（spec-architecture-review 済み）

| # | 項目 | 配置先 | 前例 |
|---|---|---|---|
| 1 | `planDirectMoves`（Shift配分計画・純関数） | `src/features/inventory/inventoryLogic.ts` | 同所の `resolveDirectMoveTarget`（本計画で置換） |
| 2 | `blockSlotRightClickPayload` / `blockShiftMovePayloads` | `src/features/blockInventory/blockLogic.ts` | 同所の `blockSlotClickPayload` |
| 3 | `resolveMaxStack` の context 追加 | `blockInteractionContext.ts` | 同 context の `resolveName` |
| 4 | `BlockAreaSlotParser` / `BlockCollectActionHandler` | `Client.WebUiHost/Game/Actions/Inventory/BlockInventoryActions.cs` | 同ファイルの `BlockMoveItemActionHandler`。複数ハンドラ/1ファイルは `InventoryActions.cs` 前例 |
| 5 | 収集先決定ロジックの再利用 | `CollectActionHandler.ResolveCollectTarget`（public static・既存） | `InventoryActions.cs:96` |
| 6 | action 登録 | `WebUiGameBinder.cs` の `RegisterAction` 列 | 既存13登録 |
| 7 | `"block_inventory.collect"` 型追加 | `src/bridge/transport/protocol.ts` の `ActionPayloads` | `block_inventory.move_item` |
| 8 | `applyBlockCollect` | `e2e/mock-host/inventoryModel.ts` | 同所の `applyCollect` / `applyBlockMove` |
| 9 | e2e 新規 spec | `e2e/tests/block/blockInventoryGestures.spec.ts` | block/ は現4ファイル（10未満）。既存 spec は59行だが追記すると200行超のため新ファイル |

**新規パターン（レビュー注目点）**: `blockLogic.ts` → `@/features/inventory/inventoryLogic` の **feature間 import**（現状 feature 間 import はゼロ）。配分計画は「プレイヤーインベントリへの割り当て」という inventory ドメインの純ロジックであり、blockInventory は既に `Topics.inventory` へ依存済み（`BlockInventoryPanel.tsx:15`）。依存方向は blockInventory → inventory の一方向のみ。UI を巻き込まないよう index.ts 経由ではなくロジックモジュール直 import とする。

**機構選択（受動的統合 vs 能動介入）**: 「C# に `block_inventory.split` / `block_inventory.direct_move` を新設する」能動案と比較し、**既存 `block_inventory.move_item` の数量クライアント計算**（受動案）を採用。根拠: (1) uGUI 自身が半分（`Count/2`）・移動先探索をクライアント計算している（`PlayerInventorySlotInteraction.cs:112` / `PlayerInventoryDirectMover.cs`）ので、host 権威化はむしろ uGUI と非対称になる。(2) 既存 web の block 左クリックも topic の `slot.count` から数量を計算して送る前例。(3) collect のみ「複数ソース集約」がサーバ側 `CollectItems` の責務で move_item では表現不能 → ここだけハンドラ新設が必要十分。

## 機能パリティ死活表（本計画が触れる機構にぶら下がる全操作）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| block スロット左クリック全取り/全置き | 生存 | `blockSlotClickPayload` 不変。Shift無しの経路は同一コード |
| player main/hotbar 左クリック・右クリック半分/1個・ダブルクリック・Sort・ホットバー選択/ホイール | 生存 | `directMove` 内部以外の `InventoryPanel` コードは無変更 |
| Shift main↔hotbar（block 閉時） | 生存（強化） | `planDirectMoves` は旧 `resolveDirectMoveTarget` の上位互換（単一→複数スタック配分。選好順「同種→空」は不変） |
| Shift main/hotbar（block **開**時） | **挙動変更: 反対エリア行き→block 行き** | 本タスクの目的そのもの。uGUI `PlayerInventoryDirectMover` と同一挙動（sub 開時は sub へ、満杯なら無操作・fallback なし） |
| ✕ボタンでの block close（`ui_state.request`） | 生存 | `BlockInventoryPanel` の該当コード無変更 |
| Esc での block close | 生存 | uGUI `SubInventoryState` が webモードでも稼働（INFRA-6 パススルー）。Task 9 の PlayMode スモークで実機確認し記録 |
| FilterSplitter 等 `BlockItemGrid` を使う全ブロックビュー | 生存＋拡張 | 右クリック/ダブルクリック/Shift が全ビューに自動付与（uGUI も sub スロット共通挙動なので意図どおり） |
| uGUI 側の全操作 | 生存 | uGUI コード無変更（`Client.WebUiHost` への追加のみ） |

---

### Task 0: ツール副産物3ファイルの後始末コミット

**Files:**
- Commit: `.moorestech-external-revisions.json`（外部リポ moorestech_master の commitHash 追従）
- Commit: `moorestech_client/.uloop/tools.json`（uloop 再生成物）
- Commit: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`（コンパイルトリガのタイムスタンプ。ファイル内コメントが「更新もコミットせよ」と明記）

**Interfaces:** なし（後続タスクの git 状態をクリーンにする）

- [ ] **Step 1: 現在地と diff を確認**

Run: `pwd && git status --short && git diff --stat`
Expected: 上記3ファイルのみが ` M` で並ぶ（他に dirty があれば手を触れず報告して停止）

- [ ] **Step 2: コミット**

```bash
git add .moorestech-external-revisions.json moorestech_client/.uloop/tools.json "moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs"
git commit -m "chore: ツール副産物の更新を取り込み（external-revisions/uloop tools/_CompileRequester）"
```

---

### Task 1: 台帳一本化（TODO.md へパリティ台帳を統合）

**Files:**
- Modify: `docs/webui/TODO.md`
- Modify: `docs/webui/2026-07-06-all-code-review-progress.md:97`
- Modify: `docs/webui/2026-07-07-parity-implementation-plan.md`（冒頭にバナー追記）
- Modify: `AGENTS.md`（「実装漏れ…洗い出す」行を削除。台帳完成＝洗い出し完了のため）

**Interfaces:**
- Produces: TODO.md の「### 2a. 操作・表示パリティ台帳」— Task 9 がこの台帳のチェックボックスを更新する

- [ ] **Step 1: TODO.md の最終更新日を書き換え**

`**最終更新**: 2026-07-06` → `**最終更新**: 2026-07-07`

- [ ] **Step 2: TODO.md のドキュメントマップ表に1行追加**

`| 2026-07-07-parity-implementation-plan.md | ...` の行の直後に:

```markdown
| `../superpowers/plans/2026-07-07-webui-parity-phase0-2.md` | Phase 0〜2 の**実行計画**（writing-plans形式・完全コード付き）。着手はこちらから |
```

- [ ] **Step 3: TODO.md 残タスク2節のギア系完了行に注記を追加**

対象行（`### 2. 機能移行` 内）:
```markdown
- [x] 個別ブロック UI（FEAT-BLK-2/3/4/5/8）: 発電機 / 機械 / 採掘機 / ギア系（`GearEnergyTransformerUIView`）/ フィルタ分岐器（2026-07-06）
```
末尾に追記して次の形にする:
```markdown
- [x] 個別ブロック UI（FEAT-BLK-2/3/4/5/8）: 発電機 / 機械 / 採掘機 / ギア系（`GearEnergyTransformerUIView`）/ フィルタ分岐器（2026-07-06）※ギア**伝達**系5ブロック（blockType: Shaft/Gear/GearChainPole）はレジストリ未登録で Generic 落ち、ElectricToGearGenerator も未対応 — 2a 参照
```

- [ ] **Step 4: TODO.md の `### 3. 検証` の直前に台帳セクションを挿入**

```markdown
### 2a. 操作・表示パリティ台帳（2026-07-07 統合。裏取り済み監査＋種リストの一本化。台帳はここが唯一の正）
> 出典: `2026-07-07-parity-audit-verification-handoff.md`（**要訂正5点を反映済み**）と
> `2026-07-06-all-code-review-progress.md` の種リスト。個別の証拠ファイルパスは申し送りを参照。
> 画面カバレッジ: uGUI `UIStateEnum` 11 ステート中 web 対応は 3＋GameScreen（残り7画面は「2. 機能移行」の大物画面参照）。

**ブロックインベントリ操作（優先度1 → 実行計画 `../superpowers/plans/2026-07-07-webui-parity-phase0-2.md`）**
- [ ] ブロックスロット右クリック（空手: 半分取り(切り捨て) / grab保持: 1個置き）
- [ ] ブロックスロットダブルクリック収集（C# `block_inventory.collect` 新設が必要。既存 `inventory.collect` は block を拒否）
- [ ] Shift直接移動の SubInventory 対応（main/hotbar→block、block→main。uGUI `PlayerInventoryDirectMover` 準拠の複数スタック配分）
- [ ] blockInventory e2e のジェスチャ網羅（現状は左クリック pickup のみで欠落を検出できない）
- [ ] Esc でのブロックUIクローズ: uGUI `SubInventoryState` が webモードでも稼働しているため**動作している可能性が高い**。PlayModeスモークで検証し結果をここへ記録（未動作なら実装タスク化）

**ギア系・個別ブロック（優先度2）**
- [ ] ギア伝達系5ブロックが Generic 落ち: レジストリへ **Shaft / Gear / GearChainPole** を登録（⚠「GearEnergyTransformer」という blockType は存在しない。実装時は v8 blocks.json で `blockUIAddressablesPath: "Vanilla/UI/Block/GearEnergyTransformerUI"` を持つ blockType を再列挙して確定）
- [ ] ElectricToGearGenerator 専用ビュー＋出力モード選択（v8 に1ブロック実在。uGUI: `ElectricToGearGeneratorBlockInventoryView`）

**プレイヤーインベントリ（優先度4。⚠右クリック半分/1個置き/ダブルクリック収集は実装済み — 欠けはドラッグ系のみ）**
- [ ] スプリットドラッグ（grab の複数スロット均等配分）
- [ ] 右ドラッグ連続1個配置

**クラフト・表示系（優先度5）**
- [ ] CraftRecipeView に所持数/必要数の数値テキスト（不足赤字）とツールチップ内訳（⚠40%透過減光は実装済み。無いのはこの粒度のみ）
- [ ] アイテムリストのクラフト可能数バッジ/グレーアウト（`craftLogic.craftable()` はボタン活性にのみ使用中）
- [ ] 機械詳細の分間生産数表示（`details/MachineSection.tsx` は進捗＋電力率のみ）
- [ ] ホイールのホットバー切替を入力量累積に（現状 deltaY の符号のみ＝±1固定。uGUI は入力量累積）
- [ ] クラフト長押し・連続クラフト（FEAT-CRAFT-1 既存記載の再掲）

**品質フォロー（種リスト由来）**
- [ ] `ui_state.request` が現 state を問わず受理される（Story/PauseMenu 中の遅延要求で強制遷移し得る。ホワイトリスト検討）
- [ ] `useItemMaster` のモジュールキャッシュが WS 再接続後も stale（外部ブラウザ開発フロー限定の実害）
- [ ] crafting 系 validator の深掘り（壊れ payload での React クラッシュ耐性。all-code-review で見送り分）

**低優先・記録のみ**
- BaseCamp 完成ボタン: v8 マスタに BaseCamp ブロックは**0個**のため現行コンテンツでは発生しない。マスタに実体が出たら着手
- 研究ノードの UIScale 未反映（未検証・低影響）
- クラフト不可時のカーソルツールチップ（web にツールチップ基盤自体が無い可能性が高い・未検証）
- TankInventory は意図的温存（`blockLogic.test.ts` に意図明記済み）。**整理不要 — タスク化しないこと**
```

- [ ] **Step 5: 進捗ドキュメントの種リストへ移設済み注記**

`docs/webui/2026-07-06-all-code-review-progress.md` の行97（「実装漏れの徹底洗い出し」— 種リスト: … で始まる行）の末尾に追記:

```markdown
 → **2026-07-07 TODO.md「2a. 操作・表示パリティ台帳」へ移設済み（本行は履歴として保存）**
```

- [ ] **Step 6: 概要計画書にバナー追記**

`docs/webui/2026-07-07-parity-implementation-plan.md` のタイトル行直後に挿入:

```markdown
> **2026-07-07 更新**: Phase 0〜2 は writing-plans 形式の実行計画
> `docs/superpowers/plans/2026-07-07-webui-parity-phase0-2.md` に詳細化済み。実行はそちらに従う。
> Phase 3〜6 のロードマップは引き続き本書が正。
```

- [ ] **Step 7: AGENTS.md から洗い出しタスク行を削除**

削除する行: `- 実装漏れ、uGUIにあってwebにないものを徹底的に洗い出す`
（台帳完成が「洗い出し」の成果物。未検証残件は台帳の「低優先・記録のみ」で追跡する）

- [ ] **Step 8: コミット**

```bash
git add docs/webui/TODO.md docs/webui/2026-07-06-all-code-review-progress.md docs/webui/2026-07-07-parity-implementation-plan.md AGENTS.md docs/superpowers/plans/2026-07-07-webui-parity-phase0-2.md
git commit -m "docs(webui): パリティ台帳をTODO.mdへ一本化（訂正5点反映・種リスト移設・洗い出しタスククローズ）"
```

---

### Task 2: 状態管理の適正化チェック（AGENTS.md タスク1のクローズ）

**Files:**
- Modify: `AGENTS.md`（確認クリーンなら「現状のタスク」節を削除）

**Interfaces:** なし（検証のみ）

- [ ] **Step 1: topicStore の書き込み口が deliverTopicPayload だけであることを確認**

Run: `cd moorestech_web/webui && grep -rn "setTopic" src | grep -v "store/topicStore"`
Expected: 出力なし（`setTopic` の定義・呼び出しは `src/bridge/store/topicStore.ts` と同テストのみ）

- [ ] **Step 2: useTopicStore が bridge/store の外から直接触られていないことを確認**

Run: `grep -rn "useTopicStore" src | grep -v "src/bridge/store"`
Expected: `src/bridge/transport/webSocketClient.ts` の `setStatus` 呼び出しのみ（接続ステータスの書き込みは「WS 層のみが書く」という topicStore 自身の設計コメントが公認する正規経路。feature/コンポーネントからのヒットが出たら違反）

- [ ] **Step 3: deliverTopicPayload の呼び出し元が WS 層だけであることを確認**

Run: `grep -rn "deliverTopicPayload" src | grep -v "\.test\."`
Expected: `src/bridge/store/topicStore.ts`（定義）と `src/bridge/transport/webSocketClient.ts`（呼び出し）のみ

- [ ] **Step 4: 結果に応じて分岐**

3つとも Expected どおり → クリーン確定。AGENTS.md の以下を丸ごと削除:

```markdown
# 現状のタスク（終わったらこの記述は消す）WebUIでやること

うえから

- ステート、プロップス、Context、状態管理ライブラリの利用の適正化
```

Expected と異なる出力が出た場合 → **修正せず**、違反箇所を TODO.md「品質フォロー」に1行追記して報告・停止（レビュー全44系統 Critical 0 の前提が崩れるため人の判断を仰ぐ）

- [ ] **Step 5: コミット**

```bash
git add AGENTS.md
git commit -m "docs: 状態管理適正化タスクをクローズ（topicStore単一書き込み口をgrepで検証、違反ゼロ）"
```

---

### Task 3: planDirectMoves（Shift配分計画）と InventoryPanel の直接移動更新

**Files:**
- Modify: `moorestech_web/webui/src/features/inventory/inventoryLogic.ts`（全置換・17行→約35行）
- Modify: `moorestech_web/webui/src/features/inventory/inventoryLogic.test.ts`（全置換）
- Modify: `moorestech_web/webui/src/features/inventory/InventoryPanel/index.tsx:96-107`（directMove）

**Interfaces:**
- Produces: `planDirectMoves(sourceCount: number, itemId: number, maxStack: number | undefined, targetSlots: SlotData[]): PlannedMove[]`（`PlannedMove = { slot: number; count: number }`）— Task 4 の `blockShiftMovePayloads` と本タスクの `InventoryPanel` が使う。`resolveDirectMoveTarget` は削除（呼び出し元は InventoryPanel のみ）

- [ ] **Step 1: 失敗するテストを書く**

`src/features/inventory/inventoryLogic.test.ts` を以下の内容で**全置換**:

```ts
import { describe, it, expect } from "vitest";
import { planDirectMoves } from "./inventoryLogic";

const slot = (itemId: number, count: number) => ({ itemId, count });

describe("planDirectMoves", () => {
  it("同種スタックの空きへ順に詰める（複数スタック配分）", () => {
    const targets = [slot(1, 98), slot(2, 5), slot(0, 0), slot(1, 10)];
    expect(planDirectMoves(7, 1, 100, targets)).toEqual([
      { slot: 0, count: 2 },
      { slot: 3, count: 5 },
    ]);
  });

  it("スタックで吸収しきれない残りは最初の空スロットへ", () => {
    const targets = [slot(1, 98), slot(0, 0)];
    expect(planDirectMoves(7, 1, 100, targets)).toEqual([
      { slot: 0, count: 2 },
      { slot: 1, count: 5 },
    ]);
  });

  it("同種スタックに全量入るなら空スロットは使わない", () => {
    const targets = [slot(1, 10), slot(0, 0)];
    expect(planDirectMoves(7, 1, 100, targets)).toEqual([{ slot: 0, count: 7 }]);
  });

  it("同種が無ければ最初の空スロットへ全量", () => {
    const targets = [slot(2, 3), slot(0, 0), slot(0, 0)];
    expect(planDirectMoves(7, 1, 100, targets)).toEqual([{ slot: 1, count: 7 }]);
  });

  it("満杯スタックは飛ばす", () => {
    const targets = [slot(1, 100), slot(0, 0)];
    expect(planDirectMoves(7, 1, 100, targets)).toEqual([{ slot: 1, count: 7 }]);
  });

  it("maxStack 不明時（マスタ未ロード）は同種探索をスキップし空スロットのみ使う", () => {
    const targets = [slot(1, 10), slot(0, 0)];
    expect(planDirectMoves(7, 1, undefined, targets)).toEqual([{ slot: 1, count: 7 }]);
  });

  it("移動先が無ければ空配列、空スロットが無ければ部分配分のみ返す", () => {
    expect(planDirectMoves(7, 1, 100, [slot(2, 5)])).toEqual([]);
    expect(planDirectMoves(7, 1, 100, [slot(1, 98), slot(2, 5)])).toEqual([{ slot: 0, count: 2 }]);
  });
});
```

- [ ] **Step 2: テストが落ちることを確認**

Run: `cd moorestech_web/webui && pnpm exec vitest run src/features/inventory/inventoryLogic.test.ts`
Expected: FAIL（`planDirectMoves` が export されていない旨のエラー）

- [ ] **Step 3: inventoryLogic.ts を実装で全置換**

```ts
import type { SlotData } from "@/bridge/contract/payloadTypes";

// Shift+クリック直接移動の配分計画（uGUI PlayerInventoryDirectMover 準拠）
// Direct-move allocation plan for Shift+click (mirrors uGUI PlayerInventoryDirectMover)
export type PlannedMove = { slot: number; count: number };

// 同種スタック(空きあり)へ順に詰め、残りを最初の空スロットへ置く。入り切らない分は配分しない
// Fill same-item stacks with room in order, then drop the rest on the first empty slot; overflow stays unplanned
// maxStack が undefined（マスタ未ロード）なら同種探索をスキップし空スロットのみ使う
// When maxStack is undefined (master unloaded) skip the same-item search and use empty slots only
export function planDirectMoves(
  sourceCount: number,
  itemId: number,
  maxStack: number | undefined,
  targetSlots: SlotData[],
): PlannedMove[] {
  const moves: PlannedMove[] = [];
  let remaining = sourceCount;
  if (maxStack !== undefined) {
    for (let i = 0; i < targetSlots.length && remaining > 0; i++) {
      const target = targetSlots[i];
      if (target.count === 0 || target.itemId !== itemId || maxStack <= target.count) continue;
      const count = Math.min(remaining, maxStack - target.count);
      moves.push({ slot: i, count });
      remaining -= count;
    }
  }
  // ソースは1スタック分なので、残り全量は空スロット1つで収まる
  // The source is a single stack, so one empty slot always fits the remainder
  if (remaining > 0) {
    const empty = targetSlots.findIndex((s) => s.count === 0);
    if (empty >= 0) moves.push({ slot: empty, count: remaining });
  }
  return moves;
}
```

（旧 `resolveDirectMoveTarget` はこの置換で消える）

- [ ] **Step 4: テストが通ることを確認**

Run: `pnpm exec vitest run src/features/inventory/inventoryLogic.test.ts`
Expected: 7 tests PASS

- [ ] **Step 5: InventoryPanel の directMove を配分＋block対応に書き換え**

`src/features/inventory/InventoryPanel/index.tsx` の import 行を変更:

```ts
import { resolveDirectMoveTarget } from "../inventoryLogic";
```
↓
```ts
import { planDirectMoves } from "../inventoryLogic";
```

`directMove` 関数（現96〜107行）を以下に置換:

```ts
  // Shift+クリック: ブロックUIが開いていれば block へ、閉なら反対エリアへ配分する（uGUI DirectMover 準拠）
  // Shift-click: allocate into the block while its UI is open, else into the opposite area (mirrors uGUI DirectMover)
  const directMove = (from: SlotRef, slot: SlotData) => {
    // マスタ未ロード時は maxStack 不明として planDirectMoves が空スロットのみ使う
    // With the master unloaded, maxStack is unknown and planDirectMoves falls back to empty slots
    const maxStack = itemMaster?.get(slot.itemId)?.maxStack;
    // block 開閉は event 時点の最新値を readTopic で読む（キー入力リスナーと同じ規約）
    // Read the block open state at event time via readTopic (same contract as the keydown listener)
    const block = readTopic(Topics.blockInventory);
    if (block?.open) {
      for (const m of planDirectMoves(slot.count, slot.itemId, maxStack, block.itemSlots)) {
        void dispatchAction("block_inventory.move_item", { from, to: { area: "block", slot: m.slot }, count: m.count });
      }
      return;
    }
    const targetArea: InventoryArea = from.area === "hotbar" ? "main" : "hotbar";
    const targetSlots = targetArea === "main" ? inventory.mainSlots : inventory.hotbarSlots;
    for (const m of planDirectMoves(slot.count, slot.itemId, maxStack, targetSlots)) {
      void dispatchAction("inventory.move_item", { from, to: { area: targetArea, slot: m.slot }, count: m.count });
    }
  };
```

- [ ] **Step 6: 型と全vitestを確認**

Run: `pnpm build && pnpm test`
Expected: tsc エラー0・vitest 全件 PASS（`resolveDirectMoveTarget` の残参照があれば tsc がここで検出する）

- [ ] **Step 7: コミット**

```bash
git add src/features/inventory
git commit -m "feat(webui): Shift直接移動をuGUI準拠の複数スタック配分に、block開時はblockへ配分（planDirectMoves）"
```

---

### Task 4: protocol 型追加と blockLogic の右クリック/Shift 純関数

**Files:**
- Modify: `moorestech_web/webui/src/bridge/transport/protocol.ts:68`（ActionPayloads に1行）
- Modify: `moorestech_web/webui/src/features/blockInventory/blockLogic.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/blockLogic.test.ts`

**Interfaces:**
- Consumes: `planDirectMoves`（Task 3）
- Produces:
  - `ActionPayloads["block_inventory.collect"] = { slot: BlockSlotRef }` — Task 5/6/7 が使う
  - `blockSlotRightClickPayload(slotIndex: number, slotItemId: number, slotCount: number, grabCount: number): MoveItemPayload | null`
  - `blockShiftMovePayloads(blockSlotIndex: number, slotItemId: number, slotCount: number, mainSlots: SlotData[], maxStack: number | undefined): MoveItemPayload[]`

- [ ] **Step 1: protocol.ts の ActionPayloads に collect を追加**

`"block_inventory.move_item": { from: BlockSlotRef; to: BlockSlotRef; count: number };` の直後に:

```ts
  "block_inventory.collect": { slot: BlockSlotRef };
```

- [ ] **Step 2: 失敗するテストを書く**

`blockLogic.test.ts` の import 行を変更:

```ts
import { blockSlotClickPayload, pickUpPayload, placePayload, resolveBlockComponent } from "./blockLogic";
```
↓
```ts
import {
  blockShiftMovePayloads,
  blockSlotClickPayload,
  blockSlotRightClickPayload,
  pickUpPayload,
  placePayload,
  resolveBlockComponent,
} from "./blockLogic";
```

ファイル末尾（`describe("TankInventory", ...)` の後）に追記:

```ts
describe("blockSlotRightClickPayload", () => {
  it("grab 保持時は block スロットへ1個置く", () => {
    expect(blockSlotRightClickPayload(2, 0, 0, 5)).toEqual({
      from: { area: "grab", slot: 0 },
      to: { area: "block", slot: 2 },
      count: 1,
    });
  });
  it("空手 + 2個以上は半分(切り捨て)を grab へ拾う", () => {
    expect(blockSlotRightClickPayload(0, 1, 7, 0)).toEqual(pickUpPayload(0, 3));
  });
  it("空手 + 1個は半分が0のため無操作(uGUI準拠)", () => {
    expect(blockSlotRightClickPayload(0, 1, 1, 0)).toBeNull();
  });
  it("空手 + 空スロットは無操作", () => {
    expect(blockSlotRightClickPayload(0, 0, 0, 0)).toBeNull();
  });
});

describe("blockShiftMovePayloads", () => {
  const slot = (itemId: number, count: number) => ({ itemId, count });
  it("main の同種スタック→空スロットの順に block からの配分 payload を作る", () => {
    const mainSlots = [slot(1, 98), slot(0, 0)];
    expect(blockShiftMovePayloads(4, 1, 7, mainSlots, 100)).toEqual([
      { from: { area: "block", slot: 4 }, to: { area: "main", slot: 0 }, count: 2 },
      { from: { area: "block", slot: 4 }, to: { area: "main", slot: 1 }, count: 5 },
    ]);
  });
  it("移動先が無ければ空配列", () => {
    expect(blockShiftMovePayloads(0, 1, 7, [slot(2, 5)], 100)).toEqual([]);
  });
});
```

- [ ] **Step 3: テストが落ちることを確認**

Run: `pnpm exec vitest run src/features/blockInventory/blockLogic.test.ts`
Expected: FAIL（`blockSlotRightClickPayload` 等の export 不在）

- [ ] **Step 4: blockLogic.ts に実装を追加**

import 部を変更（`SlotData` と feature間 import を追加。**新規パターン: 配置と前例の節を参照**）:

```ts
import type { ActionPayloads } from "@/bridge";
import type { BlockInventoryOpen, SlotData } from "@/bridge/contract/payloadTypes";
import { planDirectMoves } from "@/features/inventory/inventoryLogic";
```

`blockSlotClickPayload` の直後に追加:

```ts
// 右クリック: grab保持なら1個置き / 空手で2個以上なら半分(切り捨て)を grab へ / それ以外は無操作
// Right-click: place one while holding grab / grab half (floor) of 2+ items empty-handed / otherwise no-op
export function blockSlotRightClickPayload(
  slotIndex: number,
  slotItemId: number,
  slotCount: number,
  grabCount: number,
): MoveItemPayload | null {
  if (grabCount > 0) return { from: { area: "grab", slot: 0 }, to: { area: "block", slot: slotIndex }, count: 1 };
  // uGUI 準拠の切り捨て半分。1個は half=0 になるため無操作
  // uGUI-style floored half; a single item halves to 0, hence a no-op
  const half = Math.floor(slotCount / 2);
  if (slotItemId > 0 && half > 0) return pickUpPayload(slotIndex, half);
  return null;
}

// Shift+クリック: block スロットからプレイヤー main へ配分（uGUI はサブ→メインのみでホットバー除外）
// Shift-click: allocate a block slot into the player's main area (uGUI moves sub→main only, hotbar excluded)
export function blockShiftMovePayloads(
  blockSlotIndex: number,
  slotItemId: number,
  slotCount: number,
  mainSlots: SlotData[],
  maxStack: number | undefined,
): MoveItemPayload[] {
  return planDirectMoves(slotCount, slotItemId, maxStack, mainSlots).map((move) => ({
    from: { area: "block", slot: blockSlotIndex },
    to: { area: "main", slot: move.slot },
    count: move.count,
  }));
}
```

- [ ] **Step 5: テストが通ることを確認**

Run: `pnpm exec vitest run src/features/blockInventory/blockLogic.test.ts`
Expected: 全件 PASS（既存分含む）

- [ ] **Step 6: コミット**

```bash
git add src/bridge/transport/protocol.ts src/features/blockInventory/blockLogic.ts src/features/blockInventory/blockLogic.test.ts
git commit -m "feat(webui): ブロックスロット右クリック/Shift配分のpayloadロジックとblock_inventory.collect型を追加"
```

---

### Task 5: BlockItemGrid と context の配線

**Files:**
- Modify: `moorestech_web/webui/src/features/blockInventory/blockInteractionContext.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/BlockInventoryPanel.tsx:20-23`
- Modify: `moorestech_web/webui/src/features/blockInventory/BlockItemGrid.tsx`（全置換）

**Interfaces:**
- Consumes: `blockSlotRightClickPayload` / `blockShiftMovePayloads`（Task 4）、`ActionPayloads["block_inventory.collect"]`（Task 4）
- Produces: `BlockInteraction.resolveMaxStack: (itemId: number) => number | undefined`

- [ ] **Step 1: context に resolveMaxStack を追加**

`blockInteractionContext.ts` の型と noop を変更:

```ts
export type BlockInteraction = {
  grabCount: number;
  resolveName: (itemId: number) => string | undefined;
  resolveMaxStack: (itemId: number) => number | undefined;
};

const noop: BlockInteraction = {
  grabCount: 0,
  resolveName: () => undefined,
  resolveMaxStack: () => undefined,
};
```

- [ ] **Step 2: BlockInventoryPanel の interaction memo を更新**

```ts
  const interaction = useMemo<BlockInteraction>(
    () => ({
      grabCount,
      resolveName: (itemId) => itemMaster?.get(itemId)?.name,
      resolveMaxStack: (itemId) => itemMaster?.get(itemId)?.maxStack,
    }),
    [grabCount, itemMaster],
  );
```

- [ ] **Step 3: BlockItemGrid.tsx を全置換**

```tsx
import { dispatchAction, readTopic, Topics } from "@/bridge";
import { ItemSlot, SlotGrid } from "@/shared/ui";
import type { SlotData } from "@/bridge/contract/payloadTypes";
import { blockShiftMovePayloads, blockSlotClickPayload, blockSlotRightClickPayload } from "./blockLogic";
import { useBlockInteraction } from "./blockInteractionContext";

// itemSlots を 9 幅グリッドで描画し grab/move_item/collect と連動する共通部品
// 9-wide grid of a block's itemSlots, wired to grab via move_item/collect
export default function BlockItemGrid({ itemSlots, testId }: { itemSlots: SlotData[]; testId: string }) {
  // grab と名前/maxStack 解決は panel が context で供給する。送信は dispatchAction を直接呼ぶ（memo 安定のため context 外）
  // grab and name/maxStack resolution come from the panel's context; dispatch calls dispatchAction directly (outside context for stable memo)
  const { grabCount, resolveName, resolveMaxStack } = useBlockInteraction();

  // クリック分岐は blockLogic に共通化。payload が null なら無操作、表示更新は event 駆動に委ねる
  // Click branching is shared in blockLogic; a null payload means no-op, and rendering follows topic events
  const onLeftDown = (index: number, slot: SlotData, shiftKey: boolean) => {
    // grab 保持中の Shift は通常の置きと同じ（uGUI 同様 grab が優先）
    // Shift while holding grab behaves as a plain place (grab wins, matching uGUI)
    if (shiftKey && grabCount === 0 && slot.count > 0) {
      // 最新の main スロットは event 時点で readTopic から読む（購読による再レンダー増を避ける）
      // Read the latest main slots via readTopic at event time (avoids extra re-renders from subscribing)
      const inventory = readTopic(Topics.inventory);
      if (!inventory) return;
      const moves = blockShiftMovePayloads(index, slot.itemId, slot.count, inventory.mainSlots, resolveMaxStack(slot.itemId));
      for (const move of moves) void dispatchAction("block_inventory.move_item", move);
      return;
    }
    const payload = blockSlotClickPayload(index, slot.itemId, slot.count, grabCount);
    if (payload) void dispatchAction("block_inventory.move_item", payload);
  };

  const onRightDown = (index: number, slot: SlotData) => {
    const payload = blockSlotRightClickPayload(index, slot.itemId, slot.count, grabCount);
    if (payload) void dispatchAction("block_inventory.move_item", payload);
  };

  // 収集先（grab か クリックスロットか）は host が自身の現在 grab 状態で決める（inventory.collect と同じ規約）
  // The host decides the target (grab vs clicked slot) from its own grab state (same contract as inventory.collect)
  const onDoubleClick = (index: number) => {
    void dispatchAction("block_inventory.collect", { slot: { area: "block", slot: index } });
  };

  return (
    <SlotGrid testId={testId}>
      {itemSlots.map((slot, index) => (
        <ItemSlot
          key={index}
          itemId={slot.itemId}
          count={slot.count}
          name={resolveName(slot.itemId)}
          onLeftDown={(shiftKey) => onLeftDown(index, slot, shiftKey)}
          onRightDown={() => onRightDown(index, slot)}
          onDoubleClick={() => onDoubleClick(index)}
        />
      ))}
    </SlotGrid>
  );
}
```

- [ ] **Step 4: 型と全vitestを確認**

Run: `pnpm build && pnpm test`
Expected: tsc エラー0・vitest 全件 PASS（コンポーネント配線の実挙動検証は Task 8 の e2e が担う）

- [ ] **Step 5: コミット**

```bash
git add src/features/blockInventory
git commit -m "feat(webui): BlockItemGridに右クリック・ダブルクリック収集・Shift移動を配線"
```

---

### Task 6: C# block_inventory.collect ハンドラとパーサ共通化

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Inventory/BlockInventoryActions.cs`（全置換・87行→約150行）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:111` 直後に1行

**Interfaces:**
- Consumes: `CollectActionHandler.ResolveCollectTarget(bool grabHeld, int clickedSlot)`（`InventoryActions.cs:96`・public static・既存）、`LocalPlayerInventoryController.CollectItems` / `.GrabInventory`（既存）
- Produces: action type `"block_inventory.collect"`（payload: `{ slot: { area: "block", slot: number } }`）— Task 7/8 が使う。エラーコードは既存の `invalid_payload` / `invalid_slot` のみ再利用（`error_codes.json` 変更不要）

- [ ] **Step 1: BlockInventoryActions.cs を以下の内容で全置換**

```csharp
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.PlayerInventory.Interface;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// "block" エリアの slot を結合インベントリ index へ写す共通パーサ
    /// Shared parser mapping a "block"-area slot onto the combined-inventory index
    /// </summary>
    public static class BlockAreaSlotParser
    {
        public static bool TryParseBlockSlot(JToken token, SubInventoryState subInventoryState, out int combinedSlot)
        {
            combinedSlot = -1;
            if (token is not JObject obj) return false;
            if (obj["area"] is not JValue { Type: JTokenType.String } areaValue || (string)areaValue != "block") return false;

            // block の slot は必須。サブインベントリは結合インベントリの MainInventorySize 以降に並ぶ
            // block requires a slot; the sub-inventory lives after MainInventorySize in the combined inventory
            if (obj["slot"] is not JValue { Value: long slotLong }) return false;

            // 発生元がブロックのときだけ許可する。列車等の非ブロックサブは block action で操作させない
            // Allow only when the source is a block; non-block subs (e.g. trains) must not be operated via block actions
            if (subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource) return false;

            // 閉状態や範囲外 slot を弾く。サブ未オープンだと結合 identifier が null で MoveItem が例外になる
            // Reject closed/out-of-range slots; with no open sub-inventory the combined identifier is null and MoveItem throws
            var sub = subInventoryState.CurrentSubInventory;
            if (sub == null) return false;
            if (slotLong < 0 || sub.Count <= slotLong) return false;

            combinedSlot = PlayerInventoryConst.MainInventorySize + (int)slotLong;
            return true;
        }
    }

    /// <summary>
    /// block_inventory.move_item: from→to へ count 個移動する（main/hotbar/grab/block 対応）
    /// block_inventory.move_item: move count items from→to (supports main/hotbar/grab/block)
    /// </summary>
    public class BlockMoveItemActionHandler : IActionHandler
    {
        public string ActionType => "block_inventory.move_item";

        private readonly LocalPlayerInventoryController _controller;
        private readonly SubInventoryState _subInventoryState;

        public BlockMoveItemActionHandler(LocalPlayerInventoryController controller, SubInventoryState subInventoryState)
        {
            _controller = controller;
            _subInventoryState = subInventoryState;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            var countToken = payload["count"];
            if (countToken is not JValue { Value: long countLong } || countLong <= 0 || int.MaxValue < countLong) return UniTask.FromResult(ActionResult.Fail("invalid_count"));
            var count = (int)countLong;

            if (!TryParseAreaSlot(payload["from"], out var fromType, out var fromSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));
            if (!TryParseAreaSlot(payload["to"], out var toType, out var toSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));

            // 同一スロットへの移動は MoveItem 内部でアイテムが消失するため no-op にする
            // Same-slot moves corrupt the stack inside MoveItem, so treat them as a no-op
            if (fromType == toType && fromSlot == toSlot) return UniTask.FromResult(ActionResult.Success());

            // 実在・数量検証は controller に集約。block も MainOrSub 結合 index で移動する
            // Presence/count validation lives in the controller; block moves also use the MainOrSub combined index
            if (!_controller.TryMoveItem(fromType, fromSlot, toType, toSlot, count, out var denyReason)) return UniTask.FromResult(ActionResult.Fail(denyReason));
            return UniTask.FromResult(ActionResult.Success());

            #region Internal

            // main/hotbar/grab は既存マッパ、block は共通パーサで結合 index へ変換する
            // main/hotbar/grab via the existing mapper; block maps onto the combined index via the shared parser
            bool TryParseAreaSlot(JToken token, out LocalMoveInventoryType type, out int localSlot)
            {
                type = LocalMoveInventoryType.MainOrSub;
                localSlot = -1;
                if (token is not JObject obj) return false;

                if (obj["area"] is not JValue { Type: JTokenType.String } areaValue) return false;
                var area = (string)areaValue;

                // block 以外は area/slot の共通パースに委譲する
                // Delegate non-block areas to the shared area/slot parser
                if (area != "block") return InventoryAreaMapper.TryParseSlotRef(token, out type, out localSlot);

                if (!BlockAreaSlotParser.TryParseBlockSlot(token, _subInventoryState, out var combinedSlot)) return false;
                type = LocalMoveInventoryType.MainOrSub;
                localSlot = combinedSlot;
                return true;
            }

            #endregion
        }
    }

    /// <summary>
    /// block_inventory.collect: block スロット起点の同種収集（uGUI サブ側ダブルクリック相当）
    /// block_inventory.collect: gather same-type items from a block slot (uGUI sub-side double-click equivalent)
    /// </summary>
    public class BlockCollectActionHandler : IActionHandler
    {
        public string ActionType => "block_inventory.collect";

        private readonly LocalPlayerInventoryController _controller;
        private readonly SubInventoryState _subInventoryState;

        public BlockCollectActionHandler(LocalPlayerInventoryController controller, SubInventoryState subInventoryState)
        {
            _controller = controller;
            _subInventoryState = subInventoryState;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            // 入力は block スロットのみ。player 側スロットは既存 inventory.collect の責務
            // Input is a block slot only; player-side slots stay with the existing inventory.collect
            if (!BlockAreaSlotParser.TryParseBlockSlot(payload["slot"], _subInventoryState, out var combinedSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));

            // 収集先決定は inventory.collect と同一（host 自身の grab 状態で決める）。空手×空スロットは no-op
            // Target choice matches inventory.collect (decided by the host's own grab); empty-handed on empty is a no-op
            var grabHeld = _controller.GrabInventory.Id != ItemMaster.EmptyItemId;
            var (targetType, targetSlot) = CollectActionHandler.ResolveCollectTarget(grabHeld, combinedSlot);
            _controller.CollectItems(targetType, targetSlot);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
```

- [ ] **Step 2: WebUiGameBinder に登録を追加**

`hub.RegisterAction(new BlockMoveItemActionHandler(controller, subInventoryState));`（111行）の直後に:

```csharp
            hub.RegisterAction(new BlockCollectActionHandler(controller, subInventoryState));
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0（Domain Reload エラー時は45秒待ってリトライ）

- [ ] **Step 4: C# 回帰テスト**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WebUi"`
Expected: 全件 PASS（`WireContractTest` 含む。エラーコードは既存のみ再利用なので fixture 変更不要）

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost
git commit -m "feat(webui-host): block_inventory.collectハンドラ追加とblockスロットパーサ共通化"
```

---

### Task 7: mock-host の block_inventory.collect 対応

**Files:**
- Modify: `moorestech_web/webui/e2e/mock-host/inventoryModel.ts`（末尾に関数追加・77行→約100行）
- Modify: `moorestech_web/webui/e2e/mock-host/wsHandler.ts`（KNOWN_ACTIONS・import・分岐追加）

**Interfaces:**
- Consumes: `ActionPayloads["block_inventory.collect"]`（Task 4）、`blockSlotOf`（既存）
- Produces: `applyBlockCollect(inv: PlayerInventoryData, currentBlock: BlockInventoryData, p: ActionPayloads["block_inventory.collect"]): void` — Task 8 の e2e が mock 経由で使う

- [ ] **Step 1: inventoryModel.ts 末尾に applyBlockCollect を追加**

```ts
// host の CollectItems と同様に grab 状態で集積先を決め、main/hotbar/block を跨いで同種を集約する
// Like the host's CollectItems, pick the target from grab state and consolidate across main/hotbar/block
export function applyBlockCollect(
  inv: PlayerInventoryData,
  currentBlock: BlockInventoryData,
  p: ActionPayloads["block_inventory.collect"],
) {
  const grabHeld = inv.grab.count > 0;
  const target = grabHeld ? inv.grab : blockSlotOf(inv, currentBlock, p.slot);
  if (target.count === 0) return;
  const blockSlots = currentBlock.open ? currentBlock.itemSlots : [];
  for (const s of [...inv.mainSlots, ...inv.hotbarSlots, ...blockSlots]) {
    if (s === target || s.itemId !== target.itemId || s.count === 0) continue;
    target.count += s.count;
    s.count = 0;
    s.itemId = 0;
  }
}
```

- [ ] **Step 2: wsHandler.ts を3箇所変更**

import 行:
```ts
import { applyMove, applyBlockMove, applyCollect } from "./inventoryModel";
```
↓
```ts
import { applyMove, applyBlockMove, applyCollect, applyBlockCollect } from "./inventoryModel";
```

`KNOWN_ACTIONS` の `"block_inventory.move_item",` の直後に:
```ts
  "block_inventory.collect",
```

`block_inventory.move_item` の else-if ブロックの直後に分岐を追加:
```ts
        } else if (msg.type === "block_inventory.collect") {
          // 実 host と同様に集約適用後、inventory と block の両 topic を再配信する
          // Apply the consolidation then republish both the inventory and block topics, like the real host
          applyBlockCollect(inv, state.currentBlock, msg.payload as ActionPayloads["block_inventory.collect"]);
          setTimeout(() => {
            send(ws, { op: "event", topic: Topics.inventory, data: inv });
            send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock });
          }, 30);
```

- [ ] **Step 3: e2e 側の型チェック**

Run: `pnpm exec tsc -p e2e/tsconfig.json --noEmit`
Expected: エラー0

- [ ] **Step 4: コミット**

```bash
git add e2e/mock-host
git commit -m "test(webui): mock-hostにblock_inventory.collectを実装"
```

---

### Task 8: ブロック操作ジェスチャの e2e 追加

**Files:**
- Create: `moorestech_web/webui/e2e/tests/block/blockInventoryGestures.spec.ts`

**Interfaces:**
- Consumes: mock-host の `/__block?type=chest`（chest fixture: slot0=Wood(itemId1)×7, slot1=Stone(itemId2)×4）、`/__actions`、inventory fixture（main0=Wood×10, main1=Stone×10, main2=Wood×5, hotbar0=Stone×3, maxStack=100）
- 検証規約: `received` は全テスト横断で蓄積されるため、**`.find` 単発ではなく payload 全等値の `toContainEqual`** で検証する

- [ ] **Step 1: spec ファイルを作成**

```ts
import { test, expect } from "@playwright/test";

type ActionRecord = { type: string; payload: unknown };

// 指定 type の action payload 一覧を返す。received は全テスト横断で蓄積されるため全等値で照合する
// List payloads of a given action type; received accumulates across tests, so match by full equality
const payloadsOf = async (page: import("@playwright/test").Page, type: string) => {
  const actions: ActionRecord[] = await page.request.get("/__actions").then((r) => r.json());
  return actions.filter((a) => a.type === type).map((a) => a.payload);
};

// 各テスト冒頭で chest を配信状態へリセットし、終了後は閉に戻して後続へ漏らさない
// Reset to chest before each test and back to closed afterwards so state never leaks
test.beforeEach(async ({ page }) => {
  await page.request.get("/__block?type=chest");
  await page.goto("/");
  await expect(page.getByTestId("block-inventory")).toBeVisible();
});
test.afterEach(async ({ page }) => {
  await page.request.get("/__block?type=closed");
});

test("空手の右クリックで半分(切り捨て)を grab へ拾う", async ({ page }) => {
  // slot0 = Wood×7 → 半分は 3
  // slot0 = Wood x7, so half floors to 3
  await page.getByTestId("chest-grid").locator("> div").first().click({ button: "right" });
  await expect
    .poll(() => payloadsOf(page, "block_inventory.move_item"))
    .toContainEqual({ from: { area: "block", slot: 0 }, to: { area: "grab", slot: 0 }, count: 3 });
  await expect(page.getByTestId("grab-overlay")).toBeVisible();
});

test("grab 保持中の右クリックで block スロットへ1個置く", async ({ page }) => {
  // 左クリックで slot0 全量(7)を grab に取り、grab 反映を待ってから slot1 を右クリック
  // Left-click grabs all of slot0 (7); wait for the grab to reflect, then right-click slot1
  await page.getByTestId("chest-grid").locator("> div").first().click();
  await expect(page.getByTestId("grab-overlay")).toBeVisible();
  await page.getByTestId("chest-grid").locator("> div").nth(1).click({ button: "right" });
  await expect
    .poll(() => payloadsOf(page, "block_inventory.move_item"))
    .toContainEqual({ from: { area: "grab", slot: 0 }, to: { area: "block", slot: 1 }, count: 1 });
});

test("ダブルクリックで block_inventory.collect を送る", async ({ page }) => {
  await page.getByTestId("chest-grid").locator("> div").nth(1).dblclick();
  await expect
    .poll(() => payloadsOf(page, "block_inventory.collect"))
    .toContainEqual({ slot: { area: "block", slot: 1 } });
});

test("Shift+クリックで block→main へ配分移動する", async ({ page }) => {
  // slot0 = Wood×7。main0 が Wood×10(空き90) なので単一 move で全量入る
  // slot0 = Wood x7; main0 holds Wood x10 (room 90), so a single move takes it all
  await page.getByTestId("chest-grid").locator("> div").first().click({ modifiers: ["Shift"] });
  await expect
    .poll(() => payloadsOf(page, "block_inventory.move_item"))
    .toContainEqual({ from: { area: "block", slot: 0 }, to: { area: "main", slot: 0 }, count: 7 });
});

test("block 開時は main の Shift+クリックが block へ配分移動する", async ({ page }) => {
  // main1 = Stone×10。chest slot1 が Stone×4(空き96) なので block slot1 へ全量入る
  // main1 = Stone x10; chest slot1 holds Stone x4 (room 96), so it all goes to block slot1
  await page.getByTestId("main-grid").locator("> div").nth(1).click({ modifiers: ["Shift"] });
  await expect
    .poll(() => payloadsOf(page, "block_inventory.move_item"))
    .toContainEqual({ from: { area: "main", slot: 1 }, to: { area: "block", slot: 1 }, count: 10 });
});
```

- [ ] **Step 2: e2e 全件実行**

Run: `pnpm test:e2e`
Expected: 既存34件＋新規5件 = 39 passed（mock-host は webServer として自動起動）

- [ ] **Step 3: コミット**

```bash
git add e2e/tests/block/blockInventoryGestures.spec.ts
git commit -m "test(webui): ブロックインベントリ操作ジェスチャ(右クリ/1個置き/収集/Shift双方向)のe2eを追加"
```

---

### Task 9: 統合QAゲート・PlayModeスモーク・台帳クローズ

**Files:**
- Modify: `docs/webui/TODO.md`（2a のチェックボックス更新・Esc検証結果の記録）

**Interfaces:**
- Consumes: Task 1 の台帳セクション、Task 3〜8 の全成果物

- [ ] **Step 1: web 側フル QA**

Run: `cd moorestech_web/webui && pnpm build && pnpm test && pnpm test:e2e`
Expected: tsc 0 / vitest 全件 PASS / e2e 39 passed

- [ ] **Step 2: C# 側フル QA**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WebUi"`
Expected: ErrorCount 0 / 全件 PASS

- [ ] **Step 3: PlayMode スモーク（録画付き）**

`unity-playmode-recorded-playtest` スキルを起動して実施（未消化だった InitializeScenePipeline 分割後の起動スモークをここで兼ねる）:
1. PlayMode 起動が InitializeScenePipeline 分割後も正常に完走すること（エラーログ0）
2. webモードでチェストを開き、右クリック半分取り・Shift移動が実機で動くこと
3. **Esc でブロックUIが閉じるか**を確認（uGUI `SubInventoryState.GetNextUpdate` の CloseUI 経路が webモードでも生きているかの検証）

- [ ] **Step 4: TODO.md の台帳を更新**

2a セクションの完了項目を更新:
- 「ブロックスロット右クリック」「ダブルクリック収集」「Shift直接移動」「blockInventory e2e」の4項目を `- [x] ...（2026-07-07）` に変更
- Esc 行を検証結果で書き換え: 動作していれば `- [x] Esc でのブロックUIクローズ: uGUI SubInventoryState 経由で動作確認済み（2026-07-07 PlayModeスモーク）`、動作しなければ `- [ ]` のまま実測結果と原因観察を追記
- 「### 3. 検証」の PlayMode 関連に1行追記: `- [x] InitializeScenePipeline 分割後の PlayMode 起動スモーク（2026-07-07、ブロック操作パリティ検証と同時実施）`

- [ ] **Step 5: 最終コミットと全体確認**

```bash
git add docs/webui/TODO.md
git commit -m "docs(webui): ブロック操作パリティ完了を台帳へ反映、PlayModeスモーク結果を記録"
git status --short
```
Expected: `git status` がクリーン（uloop 副産物が再度 dirty なら Task 0 と同様に chore コミット）

---

## 自己レビュー記録（作成時に実施済み）

- **スペック網羅**: 申し送りの優先度1（ブロックスロット操作4ジェスチャ＋e2e）→ Task 3〜8。台帳一本化 → Task 1。AGENTS.md 残タスク2件のクローズ → Task 1/2。Esc close・PlayModeスモーク未消化分 → Task 9。副産物3ファイル → Task 0
- **訂正5点の反映確認**: 訂正1（GearEnergyTransformer キー禁止）→ 台帳に⚠付きで記載し本計画のスコープ外。訂正2（player 右クリ系は実装済み）→ player 側は directMove のみ変更。訂正3（不足表示の粒度）→ 台帳の文言に反映。訂正4（BaseCamp 0個）・訂正5（TankInventory 温存）→ 台帳「低優先・記録のみ」に反映
- **型整合**: `planDirectMoves` の署名は Task 3 定義＝Task 4 consume で一致。`BlockSlotRef`（`payloadTypes.ts:13`）は area に "block"/"main"/"grab" を許容済みで新ジェスチャの payload は全て型内。`ResolveCollectTarget` は既存 public static をそのまま consume
- **プレースホルダ**: なし（全ステップに実コード・実コマンド・期待値を記載）
