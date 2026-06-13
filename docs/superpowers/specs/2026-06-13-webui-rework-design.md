# moorestech Web UI 刷新 設計書

対象: `moorestech_web/webui`（Vite + React 18 + TypeScript、**pnpm**）
日付: 2026-06-13
レビュー反映: Codex外部監査 + フロントエンドarchサブエージェント（2巡＋uGUI実装照合を統合）

## 背景・目的

現状の web-ui は直近で構築されたばかりだが、ユーザーから以下の不満が出ている:

- コード構造・階層が微妙（`components/` がフラットに14ファイル、bridge と密結合）
- 状態管理・データフローが微妙（App→子の props バケツリレー、グローバル WS singleton）
- 型・テスト・堅牢性が不足（e2e なし、型が手書きで散在）
- CSS の取り扱い／`index.tsx` を活用していない

**見た目（uGUI 準拠の3カラム＋ホットバーレイアウト）は維持**し、構造・状態管理・型・テスト・CSS規約を刷新する。加えて、後述の dblclick collect 契約は uGUI 実装と照合して**正しい挙動に修正**する（唯一の挙動変更スコープ）。

## ロックした方針

| 項目 | 決定 |
|---|---|
| 状態管理 | Zustand導入。**store化は `selectedItemId` と toast のみ**。topic購読は既存WS singleton維持 |
| useTopic | `useSyncExternalStore`化は**今回見送り**。現状の useState/useEffect 実装を温存（型付けのみ追加） |
| e2e | Playwright + モックWSサーバー（Unity不要・CI可・決定的） |
| 単体テスト | **vitest を追加**。繊細な分岐ロジックを純粋関数化して網羅（e2eと2層） |
| 階層 | feature単位 + `index.ts` barrel（barrelは限定運用・副作用ゼロ） |
| 依存方向 | **`bridge` は `features` を絶対 import しない**。feature が `bridge/protocol` を import する（一方向） |
| コンポーネント | **選択的フォルダ化**: CSS module/サブコンポーネント/専用testを持つものだけ `NAME/index.tsx`。単一ファイルは `NAME.tsx` のままフラット |
| CSS | 基本Tailwindユーティリティ、複雑な所のみ `style.module.css`（併用） |
| 見た目 | 現状維持（レイアウト・配色不変） |
| dblclick collect | **uGUI 実装に照合して挙動を修正**（下記） |

## 壊してはいけない契約（最優先で保全）

リファクタの本質的リスクはフォルダ整理ではなく、繊細なロジックを壊すこと。以下は**ロジック不変・移動のみ**とし、テストで固定してから触る。

1. **`dispatchAction` の戻り値契約**: `true`=サーバー受理であり topic 反映完了ではない。楽観更新を入れない（描画は event 駆動のまま）。
2. **grab 状態は WS topic 由来**。**Zustand store に入れてはいけない**（参照タイミングがずれて壊れる）。
3. **`recipeIndex` / `tabKey` のリセット契約**（`RecipeViewer.tsx`）: `key={itemId}` の再マウントでリセットされるローカルstate。**store化しない**。`selectedItemId` を store 化しても **`key={selectedItemId}` として key は残す**（供給元が props→store に変わっても再マウント条件は不変。実装者が誤って key を外さないこと）。
4. **`InventoryPanel` に `key` や中間ラッパを挟まない**（`clickGrabHistory` useRef 等の状態が再マウントで飛ぶ）。

## dblclick collect の uGUI 照合（唯一の挙動変更スコープ）

### uGUI 正準実装（照合元）
- `PlayerInventoryViewController.DoubleClick()`（`Client.Game/.../Inventory/Main/PlayerInventoryViewController.cs:110-120`）
  - `IsGrabItem`（= `GrabInventory.Id != EmptyItemId`）を **DoubleClick 直前の現在値**で評価。
  - grab 保持 → `CollectItems(Grab, 0)` / 素手 → `CollectItems(MainOrSub, slotIndex)`。
  - **ドラッグ中（`_isItemSplitDragging`/`_isItemOneDragging`）は DoubleClick を無視**（早期 return）。
  - **履歴機構（clickGrabHistory 相当）は無い**。
- 収集本体 `LocalPlayerInventoryController.CollectItems()`（同 `LocalPlayerInventoryController.cs:129-162`）
  - 対象=メイン+ホットバー+開いているサブインベントリ全域、**所持数の少ない順**（`OrderBy(Count)`）に集積、集積先スロット自身は除外。

### web-ui 現状との差分
| 項目 | web-ui 現状 | uGUI 正準 |
|---|---|---|
| grab 評価 | `clickGrabHistory[0] ?? grabHeld`（連鎖開始時点の近似） | DoubleClick 直前の現在値 |
| 履歴 | 左mousedownのみ更新（**右クリックでは更新されず古い値が残る**） | 無し |
| ドラッグ中 dblclick | 概念なし（各クリック独立） | 無視 |

差分の根本原因: web-ui は action-response 模型で各クリックが独立非同期のため、dblclick 時の表示 grab が 2 action 分 stale になる。`clickGrabHistory` はその補正の近似だが、**右クリック時に履歴が更新されない欠陥**があり、uGUI と一致しない経路が残る。

### 修正方針
- **uGUI の意味論「ユーザーがダブルクリック開始時に意図した grab 状態」を正しく再現する**ことをゴールとする。
- 具体策は実装計画で詰めるが、候補は (a) 右mousedownでも履歴を更新して連鎖開始時点を正確化、(b) grab 評価の基準時刻を gesture 開始に厳密化。**実装前に uGUI 実機（または server 側 collect プロトコル）で期待値を確定**し、その期待値を vitest/e2e で固定してから修正する。
- 収集本体（少ない順・自スロット除外・全域対象）は server 側 `CollectItems` に集約済みのため web-ui 側で再現不要。web-ui の責務は **collect 先（Grab か クリックスロットか）の決定のみ**。

## 1. フォルダ階層

barrel(`index.ts`)は **feature公開口のみ・副作用ゼロ（型と純粋コンポーネントのre-exportのみ）**。barrel経由import強制はしない。`shared` は薄く保つ。`bridge` は `features` を import しない（一方向依存）。

```
src/
  app/
    main.tsx
    App.tsx                # レイアウトのみ（単一ファイル→フラット）
    App.module.css         # grid-template-areas（複雑→module）
    uiStore.ts             # zustand: selectedItemId のみ（UI状態はbridgeでなくappに置く）
    DebugActionButton.tsx  # dev専用。App から static import しない（後述）
    index.css              # globals/reset のみ
  bridge/                  # 通信境界のみ（UI状態・feature型を置かない / featuresをimportしない）
    webSocketClient.ts     # WS singleton（温存。JSON.parse失敗ガードのみ追加）
    protocol.ts            # ServerMsg/ClientMsg/ActionResult + Topics定数 + TopicPayloads/ActionPayloads型
    payloadTypes.ts        # サーバー由来DTO型（PlayerInventoryData等）。protocolが参照
    useTopic.ts            # 型付き remote snapshot hook（useState実装温存）
    useItemMaster.ts
    actions.ts             # 型付き dispatchAction
    index.ts               # public API を絞って公開
  features/
    inventory/
      InventoryPanel/        # サブコンポーネント+CSS module持ち→フォルダ化
        index.tsx            # ★collect先決定は uGUI 照合で修正。それ以外は移動のみ
        GrabOverlay.tsx
        style.module.css
      inventoryLogic.ts      # pickCollectTarget/resolveDirectMoveTarget を純粋関数化（vitest対象）
      types.ts               # UI専用型のみ
      index.ts
    recipe/
      RecipeViewer.tsx       # tabKey/recipeIndex はローカルのまま、key={selectedItemId}
      CraftRecipeView.tsx
      MachineRecipeView.tsx
      RecipePager.tsx
      ItemListPanel.tsx
      ItemHeader.tsx
      craftLogic.ts          # buildOwnedCounts/craftable判定/indexクランプ を純粋関数化（vitest対象）
      types.ts
      index.ts
    toast/
      ToastHost.tsx
      toastStore.ts          # zustand スライス（toastBus置換）。React外emit保全（下記）
      index.ts
  shared/ui/
    ItemSlot/                # UI primitive、CSS module持ち→フォルダ化
      index.tsx
      style.module.css       # tooltip(group-hover)
    ItemIcon.tsx             # 単一ファイル→フラット
    index.ts
e2e/                         # src外
  mock-host/
    server.ts                # ws + http。bridge/protocol の型を import して構築
    fixtures.ts              # canned snapshot。protocol型に satisfies
    tsconfig.json            # src/bridge/protocol を参照可能に
  tests/
    inventory.spec.ts
    recipe.spec.ts
    toast.spec.ts
  playwright.config.ts
```

**型の所有・依存方向**:
- サーバー由来 payload 型（`PlayerInventoryData`/`CraftRecipesData`/`RecipeViewerItemListData` 等）は **`bridge/payloadTypes.ts`** に置く。`bridge/protocol.ts` がそれを参照して `TopicPayloads` を構成。
- `features/*/types.ts` は **UI専用型のみ**。feature が `bridge/protocol`(payload型) を import する。**bridge は feature を import しない**。
- import 書き換えコスト軽減のため tsconfig paths（`@/`）導入を推奨（feature 移動で全相対パスが書き換わるため）。

**DebugActionButton（dev専用）**: 本番バンドルに残さないため **App から static import しない**。`import.meta.env.DEV` 条件内で **dynamic import（lazy）** するか dev 専用 entry に分離。JSX 条件ガードだけでは tree-shaking されず残るので不可。

## 2. 状態管理（Zustand）

- `app/uiStore.ts`（`useUiStore`）: `selectedItemId`, `setSelectedItem` のみ。props ドリリング解消。`RecipeViewer` は `key={useUiStore(s=>s.selectedItemId)}` で再マウント契約を維持。
- `recipeIndex`/`tabKey` は `RecipeViewer` 内ローカルstateのまま。
- WS購読（`useTopic`）は既存 singleton pub/sub を維持。remote topic を store に複製しない（「remote snapshot hook」と「local UI store」を名前で分離）。
- `toastBus` → `toast/toastStore.ts`（zustandスライス）。**React外（bridge層 actions.ts）からの emit 契約を保全**: `useUiStore`/`toastStore` の `getState().addToast()` 経由で非フック呼び出しを維持する。

## 3. 型・堅牢性

`bridge/protocol.ts` に集約:

```ts
export const Topics = {
  inventory: "local_player.inventory",
  craftRecipes: "crafting.recipes",
  machineRecipes: "crafting.machine_recipes",
  itemList: "recipe_viewer.item_list",
} as const;

export type TopicPayloads = {
  [Topics.inventory]: PlayerInventoryData;        // payloadTypes.ts から
  [Topics.craftRecipes]: CraftRecipesData;
  [Topics.machineRecipes]: MachineRecipesData;
  [Topics.itemList]: RecipeViewerItemListData;
};

export function useTopic<K extends keyof TopicPayloads>(topic: K): TopicPayloads[K] | null;
```

注意:
- `subscribeTopic` も `K extends keyof TopicPayloads` で型付け（下層が `string` だと抜け道）。
- action も `ActionPayloads` で registry 化し `dispatchAction<K>(type: K, payload: ActionPayloads[K])` に。
- 戻り値の `| null`（初回snapshot前）は必ず維持。既存全callerが null ガードに依存。
- `as TopicPayloads[K]` は**ランタイム非保証のキャスト境界**であることをコメント明示。未知topicは破棄。
- **`JSON.parse` 失敗ガードを追加**。ただし AGENTS 規約「try-catch 原則禁止」と衝突するため、**JSON.parse はその数少ない正当な例外**として、`safeParse(raw): T | null` ラッパに try-catch を隔離し、呼び出し側は null 分岐で扱う（規約の例外箇所であることをコメント明記）。

## 4. テスト戦略（2層）

### vitest（単体・繊細ロジック）
純粋関数に抽出して決定的に網羅。**副作用（`clickGrabHistory` 更新・`dispatchAction`）はコンポーネントに残し、純関数化しない**。
- `inventory/inventoryLogic.ts`:
  - `pickCollectTarget(grabHeldAtStart, ref): SlotRef`（collect 先決定。uGUI 照合後の期待値で固定）
  - `resolveDirectMoveTarget(targetSlots, itemId, maxStack, fromArea): number`（同種スタック優先→空スロットfallback、maxStack undefined時スキップ）
- `recipe/craftLogic.ts`:
  - `buildOwnedCounts(inventory): Map<number,number>`（main+hotbarのみ、**grab除外**。craftable の入力）
  - `craftable(recipe, counts): boolean`、recipe index クランプ
- 必要に応じ React Testing Library で InventoryPanel のクリック分岐

### Playwright e2e（ハッピーパス・UIフロー）
モックWSホスト相手に:
1. 接続→inventory/itemList描画
2. 左クリックでgrab→追従オーバーレイ表示
3. craft: 素材不足でdisabled→充足でenable→クリックでaction送信・result反映
4. recipe pager 次/前 + アイテム切替でindex/tabリセット
5. action失敗時のトースト表示
6. **dblclick collect 契約**（uGUI 照合後の期待値）。**これは必須**（optional ではない）。

### モックWSホストの要件（drift対策）
- `bridge/protocol.ts` の型を **import** して構築。fixtureは `satisfies TopicPayloads[...]`。
- 受信 action の `type/payload/requestId` を**記録し、送信契約も assert**。
- **`result`（action ack, requestId 対応）と `event`（topic push）は別経路**。モックは **action受理→`result`返却→数tick後に topic `event` push** の非同期遅延を再現（同期即時にしない）。これが無いと stale grab 状態を作れず dblclick 契約を検証不能。
- `satisfies` は TS 型同士の一致しか見ず C#↔TS 実ドリフトは捕捉不能 → **実Unityホスト相手の薄い smoke 手順**を別途ドキュメント化し、**PRチェックリストに1項目追加**（形骸化防止）。
- **e2e/mock-host は root tsconfig（`include:["src"]`）の検査外**。`e2e/mock-host/tsconfig.json` を作り、**`pnpm test:e2e` で必ず tsc 型チェックを走らせる**（モックだけ古い型で通り続ける事故を防ぐ）。

## 5. CSS規約

- 基本: Tailwindユーティリティ（JSX内）。
- `style.module.css` を使うのは以下に限定（明文化）:
  - `grid-template-areas` レイアウト（App）
  - tooltip（group-hover、ItemSlot）
  - drag overlay の追従配置（GrabOverlay）
  - keyframes animation
- 単純なスタイルは module 化しない。

## 6. 移行順序（段階移行・一括刷新しない）

各段でコンパイル/テスト通過を確認。回帰原因を追えるよう順に実施:

1. **型ファイル追加のみ**（`payloadTypes.ts`/`protocol.ts`/型付き `useTopic`・`dispatchAction`・`safeParse`）。呼び出し置換は最小限に抑える。
2. Playwright mock e2e で**現状挙動を固定**（リファクタ前セーフティネット）。dblclick は現状挙動を一旦記録。
3. **uGUI 実機/server で dblclick collect の期待値を確定** → vitest（`pickCollectTarget`/`resolveDirectMoveTarget`/`buildOwnedCounts`/`craftable`）で固定。
4. `selectedItemId` と toast を Zustand化（`key={selectedItemId}` 維持、React外emit保全）。
5. **dblclick collect を uGUI 照合で修正**。dblclick e2e が緑になるまで次に進まない（ゲート条件）。`InventoryPanel` を守った上で `GrabOverlay` 分割・フォルダ化。
6. 最後にフォルダ移動（選択的）+ CSS module化 + DebugActionButton の dev隔離（dynamic import）+ tsconfig paths 導入。

## スコープ外

- 見た目・UXの再デザイン
- C#からの型自動生成（代償としてmock e2eはC#↔TS契約を検証しない点を明記、smokeで補完）
- 全topicのstore化
- 後方互換性（破壊的移動でよい）
- dblclick collect **以外**の挙動変更（他は挙動保存リファクタ）
