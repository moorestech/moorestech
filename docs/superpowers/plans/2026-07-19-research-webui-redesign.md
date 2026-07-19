# 研究UI デザイン哲学準拠リデザイン Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 既存Web研究UI（全画面不透明・Mantine素）を、モック準拠の2ペイン構成（左=既存InventoryPanel、右=GamePanel上の研究グラフ+選択式詳細ペイン）へ再設計する。

**Architecture:** ロジック層（`researchLogic.ts`・TreeView・トピック購読・`research.complete`アクション）は無傷。見た目層のみ差し替え: ノードカードを「名前+アイコン」へ縮小し、詳細（説明・消費・報酬・実行ボタン）は選択式詳細ペイン（GamePanel craft）へ移動。アイコン用に`iconItemId`をDTOへ追加。

**Tech Stack:** React 18 + TypeScript + CSS Modules + zod（moorestech_web/webui）、C#（Client.WebUiHost）、vitest、uloop。

**Spec:** `docs/superpowers/specs/2026-07-19-research-webui-redesign-design.md`

## Global Constraints

- webui-designスキルはホワイトリスト。新様式（研究ノードカード・グラフ内詳細ペイン）は**実装前にスキル本文へ追記**（Task 3が全UI実装タスクの前提）
- 表示文字列は必ず`t()`（lint: no-jsx-visible-literal）
- 色・z-indexは`src/app/index.css`のCSS変数トークンのみ。機能側CSSへの新色・z-index直書き禁止
- 面は必ず半透明。全画面固定塗り潰し禁止。青グラデ`--recipe-action-background`は主要アクションボタン限定
- パネル背景は`GamePanel`のみ。スロット表現は`shared/ui`のみ
- partial禁止・1ファイル200行以下・デフォルト引数禁止（C#）
- .cs変更後は必ず`uloop compile --project-path ./moorestech_client`
- 各タスク末尾でコミット（git worktree事故防止のため作業前に`pwd`確認）

## 配置と前例（spec-architecture-review済み）

| 項目 | 配置先 | 前例 |
|---|---|---|
| `iconItemId`フィールド追加 | `Client.WebUiHost/Game/Topics/Research/ResearchTopicDtos.cs`（既存DTO拡張） | 同DTO内の`ConsumeItems`（GuidをItemIdへ変換して配信） |
| iconItemId変換ロジック | `ResearchNodeDtoFactory.Create`（既存メソッド内） | 同Factoryの`MasterHolder.ItemMaster.GetItemId(...).AsPrimitive()` |
| zodスキーマ拡張 | `src/bridge/contract/schemas/research.ts` | 同ファイルの既存フィールド定義 |
| ノードカード再設計 | `src/features/research/ResearchNodeCard.tsx`（書き換え） | `InventoryPanel`のItemSlot利用・data属性状態表現 |
| 詳細ペイン新設 | `src/features/research/ResearchDetailPane.tsx`（新規） | `RecipeViewer`（GamePanel craft + 主要アクションボタン） |
| カード状態導出の純関数 | `src/features/research/researchLogic.ts`（既存へ追加） | 同ファイル`deriveResearchButton` |
| キーヒント | `src/features/research/ResearchScreenChrome.tsx`（新規） | `InventoryScreenChrome.tsx`のkeyHints様式 |
| 画面合成 | `src/app/App.tsx`（表示条件の追加） | 既存の`inventoryScreen`/`modalScreen`導出 |
| 新色トークン | `src/app/index.css`（`--research-node-*`） | 既存`--recipe-action-background`等のトークン化運用 |

データフロー: `research.tree`トピック→`useTopic`→描画（読み手のみ）。書き込みは既存`dispatchAction("research.complete")`一本。新規経路なし。選択状態はReactローカルstate（サーバー同期しない）。

## 機能パリティ死活表

| 操作 | 計画後 | 根拠 |
|---|---|---|
| Rキーで研究画面の開閉・ESC/Tab遷移 | 生きる | `ui_state`駆動・uGUI側ステート機構は無変更 |
| パン・ズーム | 生きる | `shared/treeView`無変更、GamePanel body内で継続 |
| 研究実行 | 生きる | 詳細ペインのボタンから既存`research.complete` |
| ノード上の消費/報酬/説明の閲覧 | 移動 | カード直載せ→ノード選択で詳細ペイン表示（ユーザー承認済み） |
| 不足理由のツールチップ | 移動 | 詳細ペイン内の理由テキスト表示へ（`deriveResearchButton.tooltip`流用） |
| チュートリアルアンカー`research.node-*` | 生きる | 新カードでも`tutorialAnchor`を維持（Task 4） |
| インベントリ操作（掴み・移動） | 追加 | 研究画面でもInventoryPanel+GrabOverlayを表示 |

---

### Task 1: C# DTOへiconItemId追加 + wireフィクスチャ更新

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Research/ResearchTopicDtos.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Research/ResearchNodeDtoFactory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/research_tree.json`

**Interfaces:**
- Produces: `ResearchNodeDto.IconItemId`（int、`graphViewSettings.IconItem`のGuidをItemId変換した値）。wire上は`iconItemId`。

- [ ] **Step 1: pwd確認**

Run: `pwd` → `/Users/katsumi/moorestech` であること。

- [ ] **Step 2: DTOにフィールド追加**

`ResearchTopicDtos.cs`の`ResearchNodeDto`へ、`State`の下に追加:

```csharp
        public string State;
        public int IconItemId;
```

- [ ] **Step 3: FactoryでIconItemを変換**

`ResearchNodeDtoFactory.Create`のオブジェクト初期化子へ追加（`State = ToStateString(state),`の次の行）:

```csharp
                IconItemId = MasterHolder.ItemMaster.GetItemId(master.GraphViewSettings.IconItem).AsPrimitive(),
```

（`IconItem`は`ref/graphViewSettings.yml`でitemsへのforeignKey必須項目。マスタ保証値のためnullチェック不要。）

- [ ] **Step 4: フィクスチャへiconItemId追加**

`research_tree.json`の両ノードへ`"state"`の次に追加。ノード1: `"iconItemId": 2,`、ノード2: `"iconItemId": 3,`

- [ ] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0。（「Unity is reloading」エラー時は45秒待ってリトライ）

- [ ] **Step 6: C#側wire契約テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContract"`
Expected: PASS（C#シリアライズとフィクスチャの整合が取れていること。失敗したらフィクスチャのフィールド順・カンマを確認）

- [ ] **Step 7: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Research/ moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/research_tree.json
git commit -m "feat: 研究ノードDTOへiconItemIdを追加"
```

---

### Task 2: zodスキーマとTS契約テストの追従

**Files:**
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/research.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/wireContract.test.ts`

**Interfaces:**
- Consumes: Task 1の`iconItemId`（wireフィクスチャ）
- Produces: `ResearchNodeData.iconItemId: number`（`@/bridge`経由で全UIタスクが参照）

- [ ] **Step 1: スキーマへ追加**

`ResearchNodeDataSchema`の`state`の次に:

```typescript
  state: ResearchNodeStateSchema,
  iconItemId: z.number(),
```

- [ ] **Step 2: TS契約テストへ検証追加**

`wireContract.test.ts`の`research_tree fixture`のitへ、既存の`validateTopicPayload`検証の後に追加:

```typescript
    const tree = data as ResearchTreeData;
    expect(tree.nodes[0].iconItemId).toBe(2);
```

（`ResearchTreeData`は同ファイルのimport済み型。未importなら型importへ追加。）

- [ ] **Step 3: テスト実行（失敗確認→成功確認の一体）**

Run: `cd moorestech_web/webui && npx vitest run src/bridge`
Expected: PASS。スキーマ追加**前**にStep 2だけ入れると`iconItemId`不在でFAILすることが契約の担保（順序を入れ替えて確認してもよい）。

- [ ] **Step 4: 型エラーが出ないことを確認**

Run: `cd moorestech_web/webui && npx tsc -b --noEmit 2>/dev/null || npm run build`
Expected: 型エラー0（buildまで通ればOK）。

- [ ] **Step 5: Commit**

```bash
git add moorestech_web/webui/src/bridge
git commit -m "feat: research.treeスキーマへiconItemIdを追加"
```

---

### Task 3: webui-designスキルへ新様式を追記（様式が先）

**Files:**
- Modify: `.claude/skills/webui-design/SKILL.md`

**Interfaces:**
- Produces: Task 4〜6が従う様式定義（研究グラフパネル・研究ノードカード・グラフ内詳細ペイン）

- [ ] **Step 1: セクション追記**

「## 8. 通知・情報表示」の後に追加:

```markdown
## 8.5 グラフビュー（研究ツリー等のノードグラフ）

- グラフの置き場は `GamePanel variant="default"` + タイトル罫線。body内で `shared/treeView` のパン・ズームを使う。
- **研究ノードカード**: 「名前1行(ellipsis) + `ItemSlot`アイコン」の縦積みのみ。説明・消費・報酬・ボタンはカードに載せない。
  面は `--research-node-face`、枠は `--research-node-border`（index.cssのトークン）。
  状態はdata属性（`data-completed` / `data-researchable` / `data-locked` / `data-selected`）。
  lockedはopacity減衰、selectedは `--text-high-contrast` のoutlineで表す。新しい色相・光彩は使わない。
- **グラフ内詳細ペイン**: ノード選択で開く `GamePanel variant="craft"` のフロート。グラフパネル内の固定位置
  （パン・ズーム非追従）。内容は名前・説明・消費(`ItemSlot`+insufficient)・報酬/解放(`ItemSlot`)・
  主要アクションボタン（青グラデ）・閉じるボタン。オンオフ可能（同ノード再クリック/閉じるで消える）。
```

- [ ] **Step 2: Commit**

```bash
git add .claude/skills/webui-design/SKILL.md
git commit -m "docs: webui-designへ研究グラフビューの様式を追記"
```

---

### Task 4: カード状態導出の純関数 + ResearchNodeCard書き換え

**Files:**
- Modify: `moorestech_web/webui/src/features/research/researchLogic.ts`
- Modify: `moorestech_web/webui/src/features/research/researchLogic.test.ts`
- Modify: `moorestech_web/webui/src/features/research/ResearchNodeCard.tsx`
- Modify: `moorestech_web/webui/src/features/research/style.module.css`（カード部分。パネル部分はTask 6）
- Modify: `moorestech_web/webui/src/app/index.css`（トークン追加）

**Interfaces:**
- Consumes: `ResearchNodeData.iconItemId`（Task 2）、`isPreNodeMet`（既存）
- Produces:
  - `deriveNodeCardState(state: ResearchNodeState): { completed: boolean; researchable: boolean; locked: boolean }`
  - `ResearchNodeCard` props: `{ node: ResearchNodeData; left: number; top: number; selected: boolean; onSelect: (guid: string) => void }`（owned/resolveNameは不要になる）

- [ ] **Step 1: 失敗するテストを書く**

`researchLogic.test.ts`へ追加（`deriveNodeCardState`は既存の`./researchLogic` import行へ追記し、import文を重複させない）:

```typescript
describe("deriveNodeCardState", () => {
  it("completed/researchable/lockedを状態から導出する", () => {
    expect(deriveNodeCardState("completed")).toEqual({ completed: true, researchable: false, locked: false });
    expect(deriveNodeCardState("researchable")).toEqual({ completed: false, researchable: true, locked: false });
    expect(deriveNodeCardState("unresearchableNotEnoughItem")).toEqual({ completed: false, researchable: false, locked: false });
    expect(deriveNodeCardState("unresearchableNotEnoughPreNode")).toEqual({ completed: false, researchable: false, locked: true });
    expect(deriveNodeCardState("unresearchableAllReasons")).toEqual({ completed: false, researchable: false, locked: true });
  });
});
```

- [ ] **Step 2: 失敗確認**

Run: `cd moorestech_web/webui && npx vitest run src/features/research/researchLogic.test.ts`
Expected: FAIL（deriveNodeCardState未定義）

- [ ] **Step 3: 実装**

`researchLogic.ts`末尾へ追加:

```typescript
// カードのdata属性用の状態導出（lockedは前提未達）
// Derive card data-attribute state (locked = prerequisites unmet)
export type NodeCardState = { completed: boolean; researchable: boolean; locked: boolean };

export function deriveNodeCardState(state: ResearchNodeState): NodeCardState {
  return {
    completed: state === "completed",
    researchable: state === "researchable",
    locked: state !== "completed" && !isPreNodeMet(state),
  };
}
```

- [ ] **Step 4: テスト成功確認**

Run: `cd moorestech_web/webui && npx vitest run src/features/research/researchLogic.test.ts`
Expected: PASS

- [ ] **Step 5: トークン追加**

`src/app/index.css`の`:root`へ（`--recipe-action-background`の次）:

```css
  /* 研究ノードカードの面と枠（GamePanelのネイビー族に揃える） */
  /* Research node card face and border, matching the GamePanel navy family */
  --research-node-face: rgb(10 14 27 / 80%);
  --research-node-border: rgb(104 106 120);
```

- [ ] **Step 6: ResearchNodeCard書き換え（全置換）**

```tsx
import type { ResearchNodeData } from "@/bridge";
import { ItemSlot } from "@/shared/ui";
import { deriveNodeCardState } from "./researchLogic";
import styles from "./style.module.css";
import { tutorialAnchor, type AnchorId } from "@/shared/tutorialAnchor";

type Props = {
  node: ResearchNodeData;
  left: number;
  top: number;
  selected: boolean;
  onSelect: (guid: string) => void;
};

// モック準拠の「研究名+アイコン」ノードカード。詳細は選択時の詳細ペインが担う
// Mock-compliant "name + icon" node card; details live in the selection detail pane
export default function ResearchNodeCard({ node, left, top, selected, onSelect }: Props) {
  const cardState = deriveNodeCardState(node.state);
  return (
    <div
      className={styles.node}
      style={{ left, top }}
      data-research-node
      data-selected={selected || undefined}
      data-completed={cardState.completed || undefined}
      data-researchable={cardState.researchable || undefined}
      data-locked={cardState.locked || undefined}
      data-testid={`research-node-${node.guid}`}
      onClick={() => onSelect(node.guid)}
      {...tutorialAnchor(`research.node-${node.guid}`.toLowerCase() as AnchorId)}
    >
      <span className={styles.nodeName}>{node.name}</span>
      <ItemSlot itemId={node.iconItemId} />
    </div>
  );
}
```

（`node.name`はマスタ由来データの埋め込みでありリテラルでないためlint対象外。`ItemSlot`はhandler無し=クリックがカードdivへバブルする。iconItemId≤0は`ItemSlot`が空枠表示するためフォールバック不要。）

- [ ] **Step 7: カードCSS（style.module.cssの`.node`/`.nodeCompleted`を置換）**

```css
/* ノードカード: 名前+アイコンの縦積み。面と枠はトークン、状態はdata属性 */
/* Node card: name + icon stack; face/border via tokens, states via data attributes */
.node {
  position: absolute;
  width: 132px;
  transform: translate(-50%, -50%);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  padding: 8px 6px;
  background: var(--research-node-face);
  border: 1px solid var(--research-node-border);
  cursor: pointer;
}
.node[data-locked] { opacity: 0.45; }
.node[data-completed] { border-color: var(--text-default); }
.node[data-selected] { outline: 2px solid var(--text-high-contrast); }
.nodeName {
  max-width: 100%;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
  font-size: 14px;
  color: var(--text-default);
}
```

- [ ] **Step 8: この時点の参照エラー確認**

`ResearchTreePanel.tsx`が旧props（owned/resolveName）を渡しているため型エラーになるのは想定内（Task 6で解消）。research配下のテストのみ実行:

Run: `cd moorestech_web/webui && npx vitest run src/features/research/researchLogic.test.ts`
Expected: PASS

- [ ] **Step 9: Commit**

```bash
git add moorestech_web/webui/src/features/research/ moorestech_web/webui/src/app/index.css
git commit -m "feat: 研究ノードカードを名前+アイコンの新様式へ縮小"
```

---

### Task 5: ResearchDetailPane新設

**Files:**
- Create: `moorestech_web/webui/src/features/research/ResearchDetailPane.tsx`
- Modify: `moorestech_web/webui/src/features/research/style.module.css`（ペイン部分追記）
- Create: `moorestech_web/webui/src/features/research/ResearchDetailPane.test.ts`

**Interfaces:**
- Consumes: `deriveResearchButton` / `isItemSufficient`（既存）、`dispatchAction`（既存）、`GamePanel` / `ItemSlot`
- Produces: `ResearchDetailPane` props: `{ node: ResearchNodeData; owned: Map<number, number>; resolveName: (itemId: number) => string | undefined; onClose: () => void }`

- [ ] **Step 1: 失敗するテストを書く**

`ResearchDetailPane.test.ts`（`ResearchTreePanel.test.ts`のmock構成に倣う）:

```typescript
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { ResearchNodeData } from "@/bridge";

const dispatchMock = vi.hoisted(() => vi.fn());
vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  dispatchAction: dispatchMock,
  useItemMaster: () => null,
}));
vi.mock("@/shared/i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));
// MantineProvider依存（Tooltip等）を避けるためGamePanel/ItemSlotはスタブにする
// Stub GamePanel/ItemSlot to avoid MantineProvider dependencies (Tooltip, etc.)
vi.mock("@/shared/ui", () => ({
  GamePanel: ({ children }: { children: unknown }) => createElement("mock-game-panel", null, children as never),
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
}));

import ResearchDetailPane from "./ResearchDetailPane";

const node: ResearchNodeData = {
  guid: "research-a", name: "Research A", description: "Desc", state: "researchable", iconItemId: 1,
  position: { x: 0, y: 0 }, prevGuids: [], consumeItems: [{ itemId: 1, count: 2 }], rewardItems: [], unlockItemIds: [],
};

describe("ResearchDetailPane", () => {
  it("研究可能ノードでボタン活性・クリックでresearch.completeを送る", () => {
    const renderer = create(createElement(ResearchDetailPane, {
      node, owned: new Map([[1, 5]]), resolveName: () => "Iron", onClose: () => {},
    }));
    const button = renderer.root.findByProps({ "data-testid": "research-button-research-a" });
    expect(button.props.disabled).toBe(false);
    act(() => button.props.onClick());
    expect(dispatchMock).toHaveBeenCalledWith("research.complete", { researchGuid: "research-a" });
  });

  it("不足時はボタン非活性で理由を表示し、閉じるでonCloseが呼ばれる", () => {
    const onClose = vi.fn();
    const renderer = create(createElement(ResearchDetailPane, {
      node, owned: new Map(), resolveName: () => "Iron", onClose,
    }));
    expect(renderer.root.findByProps({ "data-testid": "research-button-research-a" }).props.disabled).toBe(true);
    expect(renderer.root.findByProps({ "data-testid": "research-detail-reason" })).toBeTruthy();
    act(() => renderer.root.findByProps({ "data-testid": "research-detail-close" }).props.onClick());
    expect(onClose).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: 失敗確認**

Run: `cd moorestech_web/webui && npx vitest run src/features/research/ResearchDetailPane.test.ts`
Expected: FAIL（ResearchDetailPane未定義）

- [ ] **Step 3: 実装**

`ResearchDetailPane.tsx`:

```tsx
import type { ResearchNodeData } from "@/bridge";
import { dispatchAction } from "@/bridge";
import { GamePanel, ItemSlot } from "@/shared/ui";
import { deriveResearchButton, isItemSufficient } from "./researchLogic";
import { useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

type Props = {
  node: ResearchNodeData;
  owned: Map<number, number>;
  resolveName: (itemId: number) => string | undefined;
  onClose: () => void;
};

// 選択ノードの詳細と研究実行を担うフロートペイン（パン・ズーム非追従）
// Floating pane for selected-node details and research execution (not affected by pan/zoom)
export default function ResearchDetailPane({ node, owned, resolveName, onClose }: Props) {
  const { t } = useI18n();
  const button = deriveResearchButton(node, owned);
  return (
    <div className={styles.detailPane} data-testid="research-detail-pane">
      <GamePanel variant="craft">
        <div className={styles.detailBody}>
          <div className={styles.detailHeader}>
            <span className={styles.detailName}>{node.name}</span>
            <button type="button" className={styles.detailClose} data-testid="research-detail-close" onClick={onClose}>
              {t("×")}
            </button>
          </div>
          <p className={styles.detailDescription}>{node.description}</p>
          {node.consumeItems.length > 0 && (
            <div className={styles.detailSlots}>
              {node.consumeItems.map((c, i) => (
                <ItemSlot key={`consume-${c.itemId}-${i}`} itemId={c.itemId} count={c.count} name={resolveName(c.itemId)}
                  insufficient={!isItemSufficient(node, c.itemId, c.count, owned) && node.state !== "completed"} />
              ))}
            </div>
          )}
          {node.rewardItems.length + node.unlockItemIds.length > 0 && (
            <div className={styles.detailSlots}>
              {node.rewardItems.map((reward, i) => (
                <ItemSlot key={`reward-${reward.itemId}-${i}`} itemId={reward.itemId} count={reward.count} name={resolveName(reward.itemId)} />
              ))}
              {node.unlockItemIds.map((id, i) => (
                <ItemSlot key={`unlock-${id}-${i}`} itemId={id} name={resolveName(id)} />
              ))}
            </div>
          )}
          <button
            type="button"
            className={styles.researchButton}
            disabled={!button.interactable}
            data-testid={`research-button-${node.guid}`}
            onClick={() => void dispatchAction("research.complete", { researchGuid: node.guid })}
          >
            {button.completed ? t("研究済み") : t("研究")}
          </button>
          {!button.completed && !button.interactable && (
            <p className={styles.detailReason} data-testid="research-detail-reason">{t(button.tooltip)}</p>
          )}
        </div>
      </GamePanel>
    </div>
  );
}
```

- [ ] **Step 4: ペインCSS追記（style.module.css）**

```css
/* 詳細ペイン: グラフパネル内の右上固定フロート。パン・ズーム非追従 */
/* Detail pane: fixed float at the graph panel's top-right, unaffected by pan/zoom */
.detailPane {
  position: absolute;
  top: 12px;
  right: 12px;
  width: 300px;
}
.detailBody {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 10px;
}
.detailHeader {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.detailName {
  font-size: 16px;
  color: var(--text-high-contrast);
}
.detailClose {
  border: 0;
  background: transparent;
  color: var(--text-default);
  font-size: 16px;
  cursor: pointer;
  padding: 0 4px;
}
.detailDescription {
  margin: 0;
  font-size: 13px;
  color: var(--text-default);
}
.detailSlots {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}
/* 主要アクション: 青グラデはこのボタン限定 */
/* Primary action: the blue gradient is exclusive to this button */
.researchButton {
  border: 0;
  border-radius: 0;
  padding: 8px 0;
  background: var(--recipe-action-background);
  color: var(--text-high-contrast);
  font-family: inherit;
  font-size: 14px;
  cursor: pointer;
}
.researchButton:disabled {
  background: rgb(50 52 67);
  color: var(--text-default);
  cursor: default;
  opacity: 0.6;
}
.detailReason {
  margin: 0;
  font-size: 12px;
  color: var(--text-default);
  white-space: pre-line;
}
```

（`rgb(50 52 67)`は`InventoryScreenChrome.module.css`のボタン面と同値の既存色族。新色ではない。）

- [ ] **Step 5: テスト成功確認**

Run: `cd moorestech_web/webui && npx vitest run src/features/research/ResearchDetailPane.test.ts`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add moorestech_web/webui/src/features/research/
git commit -m "feat: 研究詳細ペインを新設（説明・消費・報酬・実行ボタン）"
```

---

### Task 6: ResearchTreePanel再構成 + キーヒント + App合成

**Files:**
- Modify: `moorestech_web/webui/src/features/research/ResearchTreePanel.tsx`
- Modify: `moorestech_web/webui/src/features/research/style.module.css`（パネル部分置換）
- Create: `moorestech_web/webui/src/features/research/ResearchScreenChrome.tsx`
- Create: `moorestech_web/webui/src/features/research/ResearchScreenChrome.module.css`
- Modify: `moorestech_web/webui/src/features/research/index.ts`
- Modify: `moorestech_web/webui/src/features/research/ResearchTreePanel.test.ts`
- Modify: `moorestech_web/webui/src/app/App.tsx`

**Interfaces:**
- Consumes: `ResearchNodeCard`（Task 4のprops）、`ResearchDetailPane`（Task 5のprops）、`GamePanel`
- Produces: `ResearchScreenChrome`（App.tsxが研究画面で表示）

- [ ] **Step 1: ResearchTreePanel書き換え（全置換）**

```tsx
import { useCallback, useMemo, useState } from "react";
import { useTopic, Topics, useItemMaster } from "@/bridge";
import type { ResearchNodeData } from "@/bridge";
import { buildOwnedCounts } from "@/shared/ownedCounts";
import { GamePanel } from "@/shared/ui";
import { TreeView } from "@/shared/treeView";
import type { TreePoint } from "@/shared/treeView";
import ResearchNodeCard from "./ResearchNodeCard";
import ResearchDetailPane from "./ResearchDetailPane";
import { useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

// topic未受信時の空配列を固定参照にしてuseMemoの空振りを防ぐ
// Stable empty-array reference so useMemo doesn't recompute every render before the topic arrives
const EMPTY_NODES: ResearchNodeData[] = [];
const getResearchNodeId = (node: ResearchNodeData) => node.guid;
const getResearchNodePosition = (node: ResearchNodeData) => node.position;
const getPreviousResearchNodeIds = (node: ResearchNodeData) => node.prevGuids;

// GamePanel上の研究グラフ + 選択式詳細ペイン
// Research graph on a GamePanel plus a selection-driven detail pane
export default function ResearchTreePanel() {
  const { t } = useI18n();
  const tree = useTopic(Topics.researchTree);
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();
  const nodes = tree?.nodes ?? EMPTY_NODES;
  const [selectedGuid, setSelectedGuid] = useState<string | null>(null);
  const owned = useMemo(
    () => buildOwnedCounts([...(inventory?.mainSlots ?? []), ...(inventory?.hotbarSlots ?? [])]),
    [inventory],
  );
  const resolveName = useCallback((itemId: number) => itemMaster?.get(itemId)?.name, [itemMaster]);
  // 同ノード再クリックで閉じるトグル選択
  // Toggle selection: clicking the same node again closes the pane
  const toggleSelect = useCallback((guid: string) => {
    setSelectedGuid((current) => (current === guid ? null : guid));
  }, []);
  const renderResearchNode = useCallback((node: ResearchNodeData, point: TreePoint) => (
    <ResearchNodeCard node={node} left={point.x} top={point.y}
      selected={node.guid === selectedGuid} onSelect={toggleSelect} />
  ), [selectedGuid, toggleSelect]);
  const selectedNode = nodes.find((node) => node.guid === selectedGuid);

  return (
    <div className={styles.researchArea} data-testid="research-tree">
      <GamePanel title={t("研究")} style={{ height: "100%" }}>
        <div className={styles.treeContainer}>
          <TreeView nodes={nodes} getId={getResearchNodeId} getPosition={getResearchNodePosition}
            getPrevIds={getPreviousResearchNodeIds} nodeTargetSelector="[data-research-node]" testIdPrefix="research"
            renderNode={renderResearchNode} />
        </div>
      </GamePanel>
      {selectedNode && (
        <ResearchDetailPane node={selectedNode} owned={owned} resolveName={resolveName}
          onClose={() => setSelectedGuid(null)} />
      )}
    </div>
  );
}
```

- [ ] **Step 2: パネルCSS置換（style.module.cssの旧`.panel`を削除し追加）**

```css
/* 研究エリア: stageグリッドのviewer〜items列を占有。全画面固定・不透明塗りは廃止 */
/* Research area occupies the viewer..items grid columns; no fixed fullscreen, no opaque fill */
.researchArea {
  grid-column: viewer-start / items-end;
  grid-row: 1;
  position: relative;
  height: 100%;
  min-width: 0;
}
.treeContainer {
  position: relative;
  height: 100%;
  overflow: hidden;
}
```

- [ ] **Step 3: ResearchScreenChrome作成**

`ResearchScreenChrome.tsx`:

```tsx
import { useI18n } from "@/shared/i18n";
import styles from "./ResearchScreenChrome.module.css";

// 研究画面のキー操作ヒント（InventoryScreenChromeのkeyHints様式）
// Key hints for the research screen, following the InventoryScreenChrome style
export default function ResearchScreenChrome() {
  const { t } = useI18n();
  return (
    <div className={styles.keyHints} data-testid="research-key-hints">
      <div><kbd>{t("Tab")}</kbd>{t(": インベントリ")}</div>
      <div><kbd>{t("ESC/R")}</kbd>{t(": 閉じる")}</div>
    </div>
  );
}
```

`ResearchScreenChrome.module.css`（InventoryScreenChrome.module.cssの`.keyHints`ブロックと同値をコピー）:

```css
/* 左下へuGUI準拠のキー操作ヒントを固定する */
/* Fix the uGUI-aligned key guide to the bottom-left */
.keyHints {
  position: absolute;
  left: 7px;
  bottom: 8px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  font-size: 25px;
  line-height: 1.2;
  letter-spacing: 0.055em;
  font-weight: 500;
  color: var(--text-high-contrast);
  -webkit-font-smoothing: antialiased;
  text-rendering: optimizeLegibility;
  text-shadow: 0.35px 0.35px 0 rgb(0 0 0 / 80%);
  pointer-events: none;
  z-index: 10;
}
.keyHints kbd {
  font: inherit;
  color: var(--text-high-contrast);
}
```

- [ ] **Step 4: index.tsへエクスポート追加**

`src/features/research/index.ts`へ:

```typescript
export { default as ResearchScreenChrome } from "./ResearchScreenChrome";
```

- [ ] **Step 5: App.tsx変更**

`App.tsx`の該当箇所を変更:

```tsx
import { ResearchTreePanel, ResearchScreenChrome } from "@/features/research";
```

```tsx
  const inventoryScreen = screen === "playerInventory" || screen === "subInventory";
  const researchScreen = screen === "researchTree";
```

InventoryPanel/GrabOverlay/Chromeの表示条件:

```tsx
        {inventoryScreen && <InventoryScreenChrome />}
        {researchScreen && <ResearchScreenChrome />}
        {(inventoryScreen || researchScreen) && <InventoryPanel />}
```

```tsx
      {(inventoryScreen || researchScreen) && <GrabOverlay />}
```

（`modalScreen`は既にresearchTreeを含むため変更不要。`{screen === "researchTree" && <ResearchTreePanel />}`も変更不要。）

- [ ] **Step 6: ResearchTreePanel.testの追従**

変更点: Mantineモック不要（Box/Title不使用になる）、`ResearchDetailPane`と`@/shared/ui`のGamePanelをモックへ追加、renderNodeのprops検証を`selected`/`onSelect`へ変更。既存の「所持数更新で再構築」の意図は「選択変更で再構築」の検証に置き換える:

```typescript
vi.mock("./ResearchDetailPane", () => ({
  default: (props: object) => createElement("mock-research-detail-pane", props),
}));
vi.mock("@/shared/ui", () => ({
  GamePanel: ({ children }: { children: ReactNode }) => createElement("mock-game-panel", null, children),
}));
```

テスト本体の置き換え:

```typescript
  it("選択トグルで詳細ペインが開閉しrenderNodeが更新される", () => {
    const renderer = create(createElement(ResearchTreePanel));
    const firstTree = renderer.root.findByProps({ "data-testid": "mock-tree-view" }) as TreeViewInstance;
    const firstRenderNode = firstTree.props.renderNode;
    const card = firstRenderNode(node, node.position);
    expect(card.props.selected).toBe(false);
    expect(renderer.root.findAllByType("mock-research-detail-pane" as never).length).toBe(0);

    // ノード選択で詳細ペインが開く
    // Selecting a node opens the detail pane
    act(() => card.props.onSelect(node.guid));
    expect(renderer.root.findAllByType("mock-research-detail-pane" as never).length).toBe(1);
    const selectedTree = renderer.root.findByProps({ "data-testid": "mock-tree-view" }) as TreeViewInstance;
    expect(selectedTree.props.renderNode).not.toBe(firstRenderNode);
    expect(selectedTree.props.renderNode(node, node.position).props.selected).toBe(true);

    // 同ノード再選択で閉じる
    // Re-selecting the same node closes it
    act(() => selectedTree.props.renderNode(node, node.position).props.onSelect(node.guid));
    expect(renderer.root.findAllByType("mock-research-detail-pane" as never).length).toBe(0);
  });
```

（`TreeViewInstance`型のrenderNode戻り値propsは`selected`/`onSelect`を持つ形へ型注釈を更新。ownedやresolveNameの検証は削除し、nodeフィクスチャへ`iconItemId: 1`を追加。）

- [ ] **Step 7: テスト・lint・build全実行**

Run: `cd moorestech_web/webui && npx vitest run && npm run lint && npm run build`
Expected: すべてPASS/エラー0（no-jsx-visible-literal含む）

- [ ] **Step 8: Commit**

```bash
git add moorestech_web/webui/src/features/research/ moorestech_web/webui/src/app/App.tsx
git commit -m "feat: 研究画面をインベントリ+GamePanelグラフの2ペイン構成へ再設計"
```

---

### Task 7: 実機視覚検証と仕上げ

**Files:**
- なし（検証のみ。問題があれば該当タスクのファイルを修正）

- [ ] **Step 1: プレイテストDSLで研究画面を表示・撮影**

unity-playmode-recorded-playtestスキルに従い、PlayModeで研究画面（Rキー相当のUIState遷移）を開いたスクリーンショットを取得する。masterデータは互換ピンのworktreeを使用（スキル参照）。

確認観点:
- 左にインベントリパネル、右に「研究」タイトルのGamePanelが出る（世界背景が透ける）
- ノードが「名前+アイコン」カードで表示され、接続線・パン・ズームが機能
- ノードクリックで詳細ペインが開き、消費不足が赤系（data-insufficient）表示、研究実行で状態が更新される
- lockedノードが減衰表示

- [ ] **Step 2: エラーログ確認**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 研究UI関連のエラー0

- [ ] **Step 3: 視覚調整があれば修正してコミット**

パネル幅・カード寸法・詳細ペイン位置の微調整はstyle.module.cssの該当値のみ変更し、コミット:

```bash
git add -A moorestech_web/webui/src/features/research/
git commit -m "fix: 研究画面の視覚調整"
```

- [ ] **Step 4: 未コミット確認**

Run: `git status`
Expected: 作業ツリーがクリーン（.moorestech-external-revisions.jsonの既存変更を除く）
