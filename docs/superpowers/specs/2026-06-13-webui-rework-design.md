# moorestech Web UI 刷新 設計書

対象: `moorestech_web/webui`（Vite + React 18 + TypeScript、**pnpm**）
日付: 2026-06-13
レビュー反映: Codex外部監査 + フロントエンドarchサブエージェント（3巡＋uGUI/WebUiHost実装照合を統合）

## 背景・目的

現状の web-ui は直近で構築されたばかりだが、ユーザーから以下の不満が出ている:

- コード構造・階層が微妙（`components/` がフラットに14ファイル、bridge と密結合）
- 状態管理・データフローが微妙（App→子の props バケツリレー、グローバル WS singleton）
- 型・テスト・堅牢性が不足（e2e なし、型が手書きで散在）
- CSS の取り扱い／`index.tsx` を活用していない

**見た目（uGUI 準拠の3カラム＋ホットバーレイアウト）は維持**し、構造・状態管理・型・テスト・CSS規約を刷新する（= フェーズA: 純リファクタ）。
加えて dblclick collect の挙動を uGUI と一致させる修正を**別フェーズB**として実施する（host C# 変更を含むためリスク隔離）。

## フェーズ分割

| | 内容 | 状態 |
|---|---|---|
| **フェーズA: 純リファクタ** | 階層/状態管理/型/テスト/CSS。挙動保存。 | レビュー収束済み。先行実装 |
| **フェーズB: dblclick collect 修正** | host側判定へ移行（C#+protocol+web+mock）。挙動変更。 | A完了後に着手。dblclick e2e緑がゲート |

## ロックした方針（フェーズA）

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
| tsconfig paths | `@/` を**導入する**（feature移動で全相対パスが書き換わるため） |

## 壊してはいけない契約（最優先で保全）

リファクタの本質的リスクは繊細なロジックを壊すこと。以下は**ロジック不変・移動のみ**とし、テストで固定してから触る。

1. **`dispatchAction` の戻り値契約**: `true`=サーバー受理であり topic 反映完了ではない。楽観更新を入れない（描画は event 駆動のまま）。
2. **grab 状態は WS topic 由来**。**Zustand store に入れてはいけない**（参照タイミングがずれて壊れる）。
3. **`recipeIndex` / `tabKey` のリセット契約**（`RecipeViewer.tsx`）: `key={itemId}` の再マウントでリセットされるローカルstate。**store化しない**。`selectedItemId` を store 化しても **`key={selectedItemId}` として key は残す**（供給元が props→store に変わっても再マウント条件は不変。実装者が誤って key を外さないこと）。
4. **`InventoryPanel` の gesture/ref 状態（現状 `clickGrabHistory` 等）を再マウントで飛ばさない**: `InventoryPanel` に `key` や中間ラッパを挟まない。※フェーズBで履歴方式自体を廃止する可能性あり（下記）、その場合この契約は不要化する。

## collect/split のアーキテクチャ（重要・事実確認済み）

**収集・分割の本体は server 側ではなく WebUiHost（Unity クライアント）側にある。** server に collect プロトコルは存在しない。

- web `dispatchAction("inventory.collect", {target})` → `WebSocketHub` → **`CollectActionHandler.ExecuteAsync`**（`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/InventoryActions.cs`）
- ハンドラは **web が送った `target`（Grab/slot）をそのまま信頼**し、host の**現在値** `GrabInventory`/`LocalPlayerInventory[slot]` で空判定のみ行い、空なら `ActionResult.Fail("empty_slot")`。
- 収集本体 `LocalPlayerInventoryController.CollectItems()`（`.../Client.Game/.../LocalPlayerInventoryController.cs:129-162`）= メイン+ホットバー+開サブ全域を**所持数の少ない順**に集積、集積先自身は除外。結果は複数 `ItemMove(SwapSlot)` プロトコルで server へ送る。
- split も同様に host 側 `SplitGrabActionHandler` が処理。

**含意1（mock）**: e2e モックWSホスト（TS/Unity非依存）には collect/split のロジックが無い。→ モックは **collect/split 後のインベントリ snapshot を canned fixture で用意**して push する（ロジック移植は不要。シナリオ固定で十分）。
**含意2（empty_slot）**: host は空 target を `empty_slot` で失敗させる（uGUI controller は単に return）。→ **web は空 target で collect を送らない**方針とし、テストで固定。

## dblclick collect の uGUI 照合（フェーズB・挙動変更）

### uGUI 正準実装
- `PlayerInventoryViewController.DoubleClick()`（`PlayerInventoryViewController.cs:110-120`）: `IsGrabItem`（=`GrabInventory.Id != EmptyItemId`、L49）を **DoubleClick 直前の現在値**で評価。grab保持→`CollectItems(Grab,0)` / 素手→`CollectItems(MainOrSub,slotIndex)`。ドラッグ中は早期return。**履歴機構は無い**。

### web-ui 現状の問題
- grab 状態は `local_player.inventory` topic 経由でしか観測できず host の真の `GrabInventory` に対し常に遅延（dblclick時に2 action分 stale）。
- 現状は `clickGrabHistory[0] ?? grabHeld` で開始時点を**近似**するが、**右mousedownでは履歴を更新しない欠陥**があり uGUI と一致しない経路が残る。
- web 側でいくら近似しても host の現在値には原理的に追いつけない（race が残る）。

### 修正方針（host側判定へ移行）= 採用案
**web が target 種別（Grab/slot）を決め打ちするのをやめ、host に現在値で判定させる。**
- `inventory.collect` の payload を **クリックした slot のみ**（`{ slot: ref }`）に変更。
- `CollectActionHandler` を改修し、uGUI の `DoubleClick` 分岐（`IsGrabItem` 現在値で Grab か slot か決定）を **host 側で実行**。
- これにより web の `clickGrabHistory`・2-action-stale 補正は**完全に不要化**し、uGUI と完全一致する。
- 影響範囲: `Client.WebUiHost`（`CollectActionHandler` の C#）、protocol（collect payload 契約）、web（`InventoryPanel` の onDoubleClick 簡素化）、e2e mock（新 payload 契約）。
- 実装前に WebUiHost 実機で期待値表を作り、vitest/e2e で固定してから改修（下記テスト）。

期待値（最低限のケース）:
- 素手で非空スロットを dblclick → host が slot を collect 先に
- grab 保持で dblclick → host が Grab を collect 先に
- 右クリックを含む dblclick 系列 → 誤判定しない（履歴に依存しないので構造的に解決）
- target が空 → web は送らない（または `empty_slot` を許容するか方針を固定）

### directMove（shift+click）の非対称について（明記）
web の `directMove`（`InventoryPanel.tsx:87-98`）は反対エリア1つだけを探す簡略版で、uGUI の `DirectMove`（sub インベントリ有無で探索範囲が変わる）とは乖離している。**これはフェーズB の照合対象外**とし、現状の web 挙動を保存する（web に sub インベントリ概念が無いため）。dblclick だけ照合し directMove は照合しないのは**意図的**。

## 1. フォルダ階層

barrel(`index.ts`)は **feature公開口のみ・副作用ゼロ**。barrel経由import強制はしない。`shared` は薄く。`bridge` は `features` を import しない（一方向）。

```
src/
  app/
    main.tsx
    App.tsx                # レイアウトのみ（単一ファイル→フラット）
    App.module.css         # grid-template-areas（複雑→module）
    uiStore.ts             # zustand: selectedItemId のみ
    DebugActionButton.tsx  # dev専用。App から static import しない
    index.css
  bridge/                  # 通信境界のみ（featuresをimportしない）
    webSocketClient.ts     # WS singleton（温存。safeParseガードのみ追加）
    protocol.ts            # ServerMsg/ClientMsg/ActionResult + Topics + TopicPayloads/ActionPayloads
    payloadTypes.ts        # サーバー由来DTO型（PlayerInventoryData等）。protocolが参照
    useTopic.ts            # 型付き remote snapshot hook（useState実装温存）
    useItemMaster.ts
    actions.ts             # 型付き dispatchAction
    index.ts               # public API を絞って公開
  features/
    inventory/
      InventoryPanel/
        index.tsx
        GrabOverlay.tsx
        style.module.css
      inventoryLogic.ts      # pickCollectTarget/resolveDirectMoveTarget（vitest対象）
      types.ts               # UI専用型のみ
      index.ts
    recipe/
      RecipeViewer.tsx       # tabKey/recipeIndex ローカル、key={selectedItemId}
      CraftRecipeView.tsx  MachineRecipeView.tsx  RecipePager.tsx
      ItemListPanel.tsx  ItemHeader.tsx
      craftLogic.ts          # buildOwnedCounts/craftable/indexクランプ（vitest対象）
      types.ts
      index.ts
    toast/
      ToastHost.tsx
      toastStore.ts          # zustand スライス。React外emit保全
      index.ts
  shared/ui/
    ItemSlot/
      index.tsx
      style.module.css       # tooltip(group-hover)
    ItemIcon.tsx
    index.ts
e2e/
  mock-host/
    server.ts                # ws + http。bridge/protocol 型を import。collect/split後snapshotはcanned
    fixtures.ts              # protocol型に satisfies
    tsconfig.json            # src/bridge/protocol を参照可能に
  tests/  inventory.spec.ts  recipe.spec.ts  toast.spec.ts
  playwright.config.ts
```

**型の所有・依存方向**: payload型は `bridge/payloadTypes.ts`。`bridge/protocol.ts` が参照し `TopicPayloads` 構成。`features/*/types.ts` は UI専用型のみ。feature→bridge の一方向。`subscribeTopic`/`useTopic`/`dispatchAction` の型制約は protocol 層に置く（webSocketClient は protocol を参照、依存方向と矛盾しない）。

**DebugActionButton**: App から static import しない。`import.meta.env.DEV` 内で dynamic import（lazy）。JSX 条件ガードのみは tree-shaking されず不可。

## 2. 状態管理（Zustand）

- `app/uiStore.ts`（`useUiStore`）: `selectedItemId`, `setSelectedItem` のみ。`RecipeViewer` は `key={useUiStore(s=>s.selectedItemId)}` で再マウント契約維持。
- `recipeIndex`/`tabKey` はローカルのまま。
- WS購読は既存 singleton 維持。remote topic を store に複製しない。
- `toastBus` → `toast/toastStore.ts`。**React外（actions.ts）からの emit を `getState().addToast()` 経由で保全**。

## 3. 型・堅牢性

`bridge/protocol.ts` に集約（`Topics` 定数、`TopicPayloads`、`ActionPayloads`）。

- `useTopic<K extends keyof TopicPayloads>(topic: K): TopicPayloads[K] | null`。`subscribeTopic` も同じ制約。
- `dispatchAction<K>(type: K, payload: ActionPayloads[K])`。
- 戻り値 `| null`（初回snapshot前）は必ず維持（既存全callerのnullガード前提）。
- `as TopicPayloads[K]` は**ランタイム非保証のキャスト境界**とコメント明示。未知topicは破棄。
- **`JSON.parse` 失敗ガード**: AGENTS規約「try-catch原則禁止」の正当な例外として `safeParse(raw): T | null` ラッパに try-catch を隔離し、呼び出し側は null 分岐（例外箇所とコメント明記）。

## 4. テスト戦略（2層）

### vitest（単体・繊細ロジック）
純粋関数に抽出。**副作用（`clickGrabHistory` 更新・`dispatchAction`）はコンポーネントに残す**。
- `inventory/inventoryLogic.ts`: `pickCollectTarget(...)`（フェーズB後の期待値で固定）、`resolveDirectMoveTarget(targetSlots, itemId, maxStack, fromArea): number`（同種スタック優先→空fallback、maxStack undefinedスキップ）
- `recipe/craftLogic.ts`: `buildOwnedCounts(inventory)`（main+hotbarのみ・**grab除外**）、`craftable(recipe, counts)`、index クランプ（**呼び出し側が `recipes.length>0` を保証する契約**を明記。length0時 `recipes[-1]` クラッシュ回避）

### Playwright e2e（ハッピーパス・UIフロー）
モックWSホスト相手に:
1. 接続→inventory/itemList描画
2. 左クリックでgrab→追従オーバーレイ
3. craft: disabled→enable→action送信・result反映
4. recipe pager + アイテム切替で index/tab リセット
5. action失敗時のトースト
6. **split**（host依存・canned snapshot）
7. **dblclick collect**（フェーズB後の期待値・**必須ゲート**）

### モックWSホストの要件
- `bridge/protocol.ts` の型を import。fixtureは `satisfies`。
- 受信 action の `type/payload/requestId` を記録し送信契約も assert。
- **`result`（ack）と `event`（topic push）は別経路**。action受理→result→**数tick後に topic event push** の非同期遅延を再現（stale grab を作るため）。
- **collect/split は host ロジックが無い**ので、結果 snapshot を canned fixture で生成して push。
- C#↔TS 実ドリフトは捕捉不能 → **実Unityホスト smoke 手順**を別途ドキュメント化し**PRチェックリストに1項目追加**。
- e2e/mock-host は root tsconfig 検査外 → 専用 tsconfig で **`pnpm test:e2e` 時に tsc 型チェック**。

## 5. CSS規約

基本 Tailwindユーティリティ。`style.module.css` は以下に限定: `grid-template-areas`（App）/ tooltip group-hover（ItemSlot）/ drag overlay 追従（GrabOverlay）/ keyframes。単純なものは module 化しない。

## 6. 移行順序

**フェーズA（純リファクタ・挙動保存）**: 各段でコンパイル/テスト通過を確認。
1. 型ファイル追加（`payloadTypes`/`protocol`/型付き `useTopic`・`dispatchAction`・`safeParse`）。呼び出し置換は最小限。
2. Playwright mock e2e で現状挙動を固定（セーフティネット）。**dblclick の現状記録は診断用**であり恒久の緑回帰テストではない（フェーズBで意図的に更新する旨を明記）。
3. 繊細ロジックを純関数化＋vitest で固定（`resolveDirectMoveTarget`/`buildOwnedCounts`/`craftable`/index クランプ）。
4. `selectedItemId` と toast を Zustand化（`key={selectedItemId}` 維持、React外emit保全）。
5. フォルダ移動（選択的）+ CSS module化 + DebugActionButton dev隔離（dynamic import）+ tsconfig paths 導入。

**フェーズB（dblclick collect 修正・挙動変更）**: A完了後。
6. WebUiHost 実機で dblclick 期待値表を確定。
7. `CollectActionHandler`（C#）を host側判定に改修＋`inventory.collect` payload を `{slot}` に変更。web `InventoryPanel.onDoubleClick` を簡素化（`clickGrabHistory` 廃止）。mock の collect 契約を更新。
8. dblclick e2e が緑になるまで完了としない（**ゲート条件**）。

## 実装ゲート（writing-plans で明文化）

- `bridge` → `features` import が無いことを `rg` で確認。
- `DebugActionButton` static import が無いことを確認。
- `pnpm build` / `pnpm test`（vitest） / `pnpm test:e2e`（tsc含む）の各段ゲート。
- dblclick collect は「現状記録 → 期待値確定 → host側修正 → e2e緑」の順。
- 空 target で collect を送らない方針をテストで固定。

## スコープ外

- 見た目・UXの再デザイン
- C#からの型自動生成（mock e2eはC#↔TS契約を検証しない点を明記、smokeで補完）
- 全topicのstore化
- 後方互換性（破壊的移動でよい）
- dblclick collect 以外の挙動変更（directMove の uGUI 照合含め、他は挙動保存）
