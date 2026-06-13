# moorestech Web UI 刷新 設計書

対象: `moorestech_web/webui`（Vite + React 18 + TypeScript、**pnpm**）
日付: 2026-06-13
レビュー反映: Codex外部監査 + フロントエンドarchサブエージェント（両者の指摘を統合済み）

## 背景・目的

現状の web-ui は直近で構築されたばかりだが、ユーザーから以下の不満が出ている:

- コード構造・階層が微妙（`components/` がフラットに14ファイル、bridge と密結合）
- 状態管理・データフローが微妙（App→子の props バケツリレー、グローバル WS singleton）
- 型・テスト・堅牢性が不足（e2e なし、型が手書きで散在）
- CSS の取り扱い／`index.tsx` を活用していない

**見た目（uGUI 準拠の3カラム＋ホットバーレイアウト）は維持**し、構造・状態管理・型・テスト・CSS規約のみ刷新する。

## ロックした方針

| 項目 | 決定 |
|---|---|
| 状態管理 | Zustand導入。**store化は `selectedItemId` と toast のみ**。topic購読は既存WS singleton維持 |
| e2e | Playwright + モックWSサーバー（Unity不要・CI可・決定的） |
| 単体テスト | **vitest を追加**。繊細な分岐ロジックを純粋関数化して網羅（e2eと2層） |
| 階層 | feature単位 + `index.ts` barrel（barrelは限定運用） |
| コンポーネント | **選択的フォルダ化**: CSS module/サブコンポーネント/専用testを持つものだけ `NAME/index.tsx`。単一ファイルは `NAME.tsx` のままフラット |
| CSS | 基本Tailwindユーティリティ、複雑な所のみ `style.module.css`（併用） |
| 見た目 | 現状維持 |

## レビューで判明した「壊してはいけない契約」（最優先で保全）

リファクタの本質的リスクはフォルダ整理ではなく、以下の繊細なロジックを壊すこと。これらは**ロジック不変・移動のみ**とし、テストで固定してから触る。

1. **dblclick collect のタイミング契約**（`InventoryPanel.tsx`）
   `clickGrabHistory`(useRef) が直近2回のmousedown時点のgrab状態を保持し、dblclick時は「現在のgrab表示は2 action分先行して必ずstale」という前提で、**連鎖開始時点のgrab状態**でcollect先(GRAB/ref)を決める。
   - grab状態は **WS topic由来**。**store に入れてはいけない**（参照タイミングがずれて壊れる）。
   - `InventoryPanel` に `key` や中間ラッパを挟まない（useRef履歴が再マウントで飛ぶ）。
2. **recipeIndex / tabKey のリセット契約**（`RecipeViewer.tsx`）
   現状 `key={itemId}` の再マウントでリセットされるローカルstate。**store化すると「別アイテム選択でも前のページ/タブが残る」リグレッション**。→ ローカルstateのまま維持。
3. **`dispatchAction` の戻り値契約**: `true`=サーバー受理であり、topic反映完了ではない。楽観更新を入れない（描画はevent駆動のまま）。

## 1. フォルダ階層

barrel(`index.ts`)は **feature公開口のみ・副作用ゼロ（型と純粋コンポーネントのre-exportのみ）**。「barrel経由import強制」はしない（循環import/公開漏れを生むため）。`shared` は薄く保つ。

```
src/
  app/
    main.tsx
    App.tsx                # レイアウトのみ（単一ファイル→フラット）
    App.module.css         # grid-template-areas（複雑→module）
    uiStore.ts             # zustand: selectedItemId のみ（UI状態はbridgeでなくappに置く）
    index.css              # globals/reset のみ
  bridge/                  # 通信境界のみ（UI状態は置かない）
    webSocketClient.ts     # WS singleton（温存。JSON.parse失敗ガードのみ追加）
    protocol.ts            # ServerMsg/ClientMsg/ActionResult + Topics定数 + TopicPayloads型 + ActionPayloads型
    useTopic.ts            # 型付き remote snapshot hook（useSyncExternalStore化を検討）
    useItemMaster.ts
    actions.ts             # 型付き dispatchAction
    index.ts               # public API を絞って公開
  features/
    inventory/
      InventoryPanel/        # サブコンポーネント+CSS module持ち→フォルダ化
        index.tsx            # ★ロジック不変・移動のみ
        GrabOverlay.tsx
        style.module.css
      inventoryLogic.ts      # directMove/collect先判定を純粋関数化（vitest対象）
      types.ts
      index.ts
    recipe/
      RecipeViewer.tsx       # tabKey/recipeIndex はローカルのまま
      CraftRecipeView.tsx
      MachineRecipeView.tsx
      RecipePager.tsx
      ItemListPanel.tsx
      ItemHeader.tsx
      craftLogic.ts          # craftable判定/index クランプを純粋関数化（vitest対象）
      types.ts               # UI専用型
      index.ts
    toast/
      ToastHost.tsx
      toastStore.ts          # zustand スライス（toastBus置換）
      index.ts
  shared/ui/
    ItemSlot/                # UI primitive、CSS module持ち→フォルダ化
      index.tsx
      style.module.css       # tooltip(group-hover)
    ItemIcon.tsx             # 単一ファイル→フラット
    index.ts
e2e/                         # src外
  mock-host/
    server.ts                # ws + http。protocol.ts の型を import して構築
    fixtures.ts              # canned snapshot。protocol型に satisfies
    tsconfig.json            # src/bridge/protocol を参照可能に
  tests/
    inventory.spec.ts
    recipe.spec.ts
    toast.spec.ts
  playwright.config.ts
```

**型の振り分け基準**: サーバー由来payload型=`bridge/protocol.ts`、UI専用型=`features/*/types.ts`。

**DebugActionButton**: dev-only として本番ビルドから除外（`import.meta.env.DEV` ガード）。features配下には置かず `app/` の dev 区画に隔離。

## 2. 状態管理（Zustand）

- `app/uiStore.ts`（`useUiStore`）: `selectedItemId`, `setSelectedItem` のみ。App→ItemListPanel/RecipeViewer の props ドリリングを解消。
- `recipeIndex`/`tabKey` は `RecipeViewer` 内ローカルstateのまま（リセット契約保全）。
- WS購読（`useTopic`）は既存 singleton pub/sub を維持。remote topicデータは store に複製しない（「remote snapshot hook」と「local UI store」を名前で分離）。
- `toastBus` → `toast/toastStore.ts`（zustandスライス）。

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
  [Topics.inventory]: PlayerInventoryData;
  [Topics.craftRecipes]: CraftRecipesData;
  [Topics.machineRecipes]: MachineRecipesData;
  [Topics.itemList]: RecipeViewerItemListData;
};

export function useTopic<K extends keyof TopicPayloads>(topic: K): TopicPayloads[K] | null;
```

注意:
- `subscribeTopic` も `K extends keyof TopicPayloads` で型付け（hookだけ型付けても下層が `string` だと抜け道）。
- action も `ActionPayloads` で registry 化し `dispatchAction<K>(type: K, payload: ActionPayloads[K])` に。
- 戻り値の `| null`（初回snapshot前）は必ず維持。既存の全callerが null ガードに依存。
- `as TopicPayloads[K]` は**ランタイム非保証のキャスト境界**であることをコメント明示。
- 未知topicは破棄する方針を明記。
- **`JSON.parse` 失敗時のガードを追加**（現状 webSocketClient.ts のhandlerが壊れたフレームで落ちうる）。

## 4. テスト戦略（2層）

### vitest（単体・繊細ロジック）
純粋関数に抽出して決定的に網羅:
- `inventory/inventoryLogic.ts`: collect先判定（連鎖開始時点grab）、`directMove`（同種スタック優先→空スロットfallback、maxStack undefined時スキップ）
- `recipe/craftLogic.ts`: `craftable` 判定（counts vs requiredItems）、recipe index クランプ
- 必要に応じ React Testing Library で InventoryPanel のクリック分岐

### Playwright e2e（ハッピーパス・UIフロー）
モックWSホスト相手に:
1. 接続→inventory/itemList描画
2. 左クリックでgrab→追従オーバーレイ表示
3. craft: 素材不足でdisabled→充足でenable→クリックでaction送信・result反映
4. recipe pager 次/前 + アイテム切替でindex/tabリセット
5. action失敗時のトースト表示
6. **dblclick collect 契約**（mock が action→遅延event push を再現できる場合）

### モックWSホストの要件（drift対策）
- `protocol.ts` の `ServerMsg`/`ClientMsg`/`TopicPayloads` を **import** して構築。fixtureは `satisfies TopicPayloads[...]`。
- 受信した action の `type/payload/requestId` を**記録し、UI結果だけでなく送信契約もassert**。
- **action受理→数tick後にtopic event push** の非同期遅延を再現（同期即時resultにしない）。stale grab状態を作れないとdblclick契約を検証不能。
- 実Unityとの C#↔TS 契約drift はモックでは検証不能。→ **実Unityホスト相手の薄いsmoke手順**を別途ドキュメント化（CI外・手動/夜間）。

## 5. CSS規約

- 基本: Tailwindユーティリティ（JSX内）。
- `style.module.css` を使うのは以下の複雑なケースに限定（明文化）:
  - `grid-template-areas` レイアウト（App）
  - tooltip（group-hover、ItemSlot）
  - drag overlay の追従配置（GrabOverlay）
  - keyframes animation
- 単純なスタイルは module化しない。

## 6. 移行順序（段階移行・一括刷新しない）

回帰原因を追えるよう順番に実施し、各段でコンパイル/テスト通過を確認:

1. `protocol.ts` + 型付き `useTopic`/`dispatchAction` を導入（JSON.parseガード含む）
2. Playwright mock e2e で**現状挙動を固定**（リファクタ前のセーフティネット）
3. 繊細ロジックを純粋関数化 + vitest で固定（inventoryLogic/craftLogic）
4. `selectedItemId` と toast を Zustand化（props ドリリング解消）
5. `InventoryPanel` をテストで守ってから `GrabOverlay` 分割・フォルダ化
6. 最後にフォルダ移動（選択的）+ CSS module化 + DebugActionButton の dev隔離

## スコープ外

- 見た目・UXの再デザイン
- C#からの型自動生成（代償としてmock e2eはC#↔TS契約を検証しない点を明記）
- 全topicのstore化
- 後方互換性（破壊的移動でよい）
