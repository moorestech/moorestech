# Web UI Phase C1 Challenge Implementation Plan

> **For agentic workers:** Execute inline in this worktree. Follow TDD for each behavior and keep the two required commits independent.

**Goal:** research と challenge が共有する汎用ツリー描画基盤を確立し、チャレンジ一覧・カテゴリツリー・常駐 HUD を Web UI へ移行する。

**Architecture:** `shared/treeView` は座標変換、bounds、edge geometry、viewport の pan/zoom と render prop だけを所有する。`features/research` と `features/challenge` は DTO、カード、状態色、文言、操作を所有する。Unity WebUiHost の `ChallengeTopic` は既存 `GetChallengeInfoProtocol` snapshot と `CompletedChallengeEventPacket` event を直接合成し、`challenge.tree` と `challenge.current` を revision envelope 経由で publish する。

**Tech Stack:** React 18, TypeScript, Zustand Topic store, zod, Vitest, Playwright, Unity/C#, UniRx, MessagePack.

## Global Constraints

- `src/features/blockInventory`、`src/features/inventory`、`src/features/crafting`、`bridge/transport/webSocketClient.ts`、`topicStore.ts` は変更しない。
- C# は200行以下、partial/Action/event/デフォルト引数を使わず、主要処理へ日英2行コメントを置く。
- Unity YAML と `.meta` は新規手書きしない。指定された死コードの既存 `.meta` 削除だけを行う。
- 可視文字列はすべて `useI18n().t(key)` 経由、主要要素は `tutorialAnchor` 経由で anchor を付ける。
- コミットは段階ごとに `feat/refactor(webui): ... (C1)` 形式で行う。

## 調査結果と契約

### uGUI の正

- `ChallengeListState` が `ChallengeListView` を保持し、`UIStateEnum.ChallengeList`（Tキー）で表示する。
- `ChallengeListView` は `InitialHandshakeResponse.Challenges` のうち `IsUnlocked` のカテゴリだけを列挙する。カテゴリ選択で `ChallengeTreeView.SetChallengeCategory` を呼ぶ。
- 各カテゴリの `Category.Challenges` が全ノード。状態は `CurrentChallenges` → `current`、`CompletedChallenges` → `completed`、その他 → `locked`（uGUI enum 名は `Before`）。カテゴリ自体が未解放なら Web リストに出さない。
- ノード位置は `DisplayListParam.UIPosition`、scale は `UIScale`。接続線は各 child の `PrevChallengeGuids` に存在し、child 座標から同一カテゴリ内の prerequisite 座標へ直線を引く。欠損 prerequisite は描画しない。
- `ChallengeManager` は handshake で HUD に全カテゴリの current を追加し、完了 event で next を追加して completed を除去する。従って HUD 表示条件は「current が1件以上」。完了直後の正は event 同梱の全カテゴリ snapshot。
- `ChallengeListUI*` は空初期化を含む旧死コード、`InGame/UI/ChallengeList/*` は空スタブであり削除対象。

### Topic / Action 契約

- `challenge.tree`: `{ categories: ChallengeCategoryData[] }`
  - category: `{ guid, name, iconItemId, nodes }`
  - node: `{ guid, title, summary, iconItemId, state: "locked"|"current"|"completed", position:{x,y}, scale:{x,y}, prevGuids }`
- `challenge.current`: `{ challenges: CurrentChallengeData[], completedChallengeGuid: string|null }`
  - current: `{ guid, title, categoryGuid }`
  - snapshot の `completedChallengeGuid` は null、完了 event のみ完了 GUID を入れる。表示状態は常に `challenges` 全量で置換する。
- Action はない。uGUI にチャレンジ操作がなく、カテゴリ切替は Web ローカル表示状態だからである。
- 初期 snapshot は既存 `InitialHandshakeResponse.Challenges` と同じ `GetChallengeInfoProtocol` 応答を WebUiHost 起動時に取得する。完了 event は既存 `CompletedChallengeEventPacket.EventTag` を購読する。Web は Topic を購読する。新規サーバ経路を作らず「event + initial snapshot + subscription」を満たす。
- `research.tree` 同様、マスタの表示 DTO とサーバ可変状態を Unity 側で合成する。個別 payload に revision を入れず、Hub envelope の単調増加 revision を使う。

## 配置レビュー

| 項目 | 配置 | 理由 |
|---|---|---|
| geometry / viewport / TreeView | `src/shared/treeView` | feature語彙を含まない描画機構で、research/challenge双方が利用する |
| research DTO解決・カード | `src/features/research` | 所持数・研究状態・操作は研究ドメイン固有 |
| challenge category/card/HUD | `src/features/challenge` | チャレンジ表示状態と i18n/anchor はfeature責務 |
| Challenge Topic/cache | `Client.WebUiHost/Game/Topics/Challenge` | Unity→Web表示状態の既存Topic層。サーバのChallenge datastoreを複製しない |
| 完了通知 | 既存 `CompletedChallengeEventPacket` + UniRx系購読API | サーバ可変状態同期の既存3点セットを再利用 |

データフロー: `ChallengeDatastore → GetChallengeInfoProtocol / CompletedChallengeEventPacket → ChallengeTopic(cache) → WebSocketHub → challenge feature`。新規要素は既存イベントの「読み手」であり、サーバ状態の書き手や別経路を作らない。

## 段階1: ツリー描画基盤の共通化

### Task 1: geometry を test-first で抽出

**Files:** Create `src/shared/treeView/treeGeometry.ts`, `treeGeometry.test.ts`; modify `src/features/research/researchLogic.ts`, `researchLogic.test.ts`.

1. 空ノード、負座標、Y反転、欠損 edge、水平/垂直線を検証する failing Vitest を書く。
2. `pnpm vitest run src/shared/treeView/treeGeometry.test.ts` が module missing で失敗することを確認する。
3. 汎用 `TreeNodePosition {id,x,y}`、`computeTreeCanvasBounds(nodes,padding)`、`toTreeCanvasPoint(position,bounds)`、`lineBetween(from,to)` を実装する。
4. research のドメイン関数だけ `researchLogic.ts` に残し、既存 test を新APIへ載せ替える。

### Task 2: viewport と render prop コンポーネントを test-first で抽出

**Files:** Create `src/shared/treeView/TreeView.tsx`, `TreeView.module.css`, `viewport.ts`, `viewport.test.ts`, `index.ts`; modify `ResearchTreePanel.tsx`, research CSS.

1. zoom clamp、cursor固定、stage縮尺補正、primary/backgroundだけのpan開始を pure function test にする。
2. test の import failure を確認後、`TreeView<T>` を実装する。入力は `nodes`, `getId`, `getPosition`, `getPrevIds`, `renderNode`, `nodeTargetSelector`, `testIdPrefix`。
3. canvas/edge/viewport のDOMとpan/zoomを基盤へ移し、research は topic/owned/name/card renderだけを残す。
4. `pnpm vitest run src/shared/treeView src/features/research` と `pnpm playwright test e2e/tests/research.spec.ts` を実行する。
5. `git commit -m "refactor(webui): extract shared tree view foundation (C1)"`。

## 段階2: チャレンジ実装

### Task 3: wire schema と fixture を test-first で追加

**Files:** Create `src/bridge/contract/schemas/challenge.ts`, challenge fixture JSON; modify schema barrel, `payloadTypes.ts`, `protocol.ts`, `wireContract.test.ts`; create C# wire test.

1. fixture が `challenge.tree/current` schema を parse し、未知 state・欠損 edge配列・不正座標を拒否する failing test を書く。
2. zod schema、inferred public types、Topics registry を実装する。
3. C# fixture deserialize test を追加し、両側の camelCase shape を固定する。

### Task 4: Unity Challenge topics を test-first で追加

**Files:** Create focused files under `Client.WebUiHost/Game/Topics/Challenge/`; modify `WebUiGameBinder.cs`.

1. category filtering、state precedence、edge/position/scale、current flatten、completed GUID event を検証する C# builder test を先に作る。
2. `ChallengeTreeDtoBuilder` を pure builder として実装する。
3. `ChallengeTopicState` が全カテゴリresponseを保持し、tree/current JSONを生成する。
4. `ChallengeTreeTopic` と `CurrentChallengeTopic` を200行以下で実装する。初期取得は `VanillaApi.Response.GetChallengeInfo`、完了event購読は `SubscribeEventResponse`、publishは両topicへ全量。
5. Binder に2 Topic を登録する。破棄可能な購読は既存 API の lifecycle に従う。

### Task 5: challenge feature を test-first で追加

**Files:** Create focused files under `src/features/challenge/`; modify localization resources only through existing key mechanism.

1. unlocked categoriesのみ、初期カテゴリ選択、選択維持/消失時fallback、3状態 class、欠損prev edge無視、HUD hidden/visible/completion update を Vitest で定義する。
2. `ChallengePanel` は category tabs + shared `TreeView` + render prop `ChallengeNodeCard` を構成する。
3. `CurrentChallengeHud` は `challenge.current` を購読し、0件なら null、1件以上なら overlay listを表示する。
4. 見出し・状態ラベル・HUDラベルを `t(key)` のみにし、panel/category/current node/HUDへ `tutorialAnchor` を付ける。

### Task 6: routing, overlay, uGUI gate, dead code, classification

**Files:** Modify `App.tsx`, ui routing; modify `ChallengeListView.cs`, `CurrentChallengeHudView.cs`; delete指定5 `.cs` + `.meta`; modify actual `WebUiGateClassification.cs`.

1. `ChallengeList` → `challengeList` routing test を先に失敗させてから実装する。
2. App stage 内に ChallengePanel、screen routing外 overlay に CurrentChallengeHud を置く。
3. uGUI 2ビューの active 条件へ `&& !WebUiScreenGate.IsWebUiMode` を追加する。
4. 指定死コード10ファイルを削除し、分類を root=`ChallengeListView`,`CurrentChallengeHudView`、covered=`ChallengeTreeView`等、deleted=旧5ファイルへ更新する。

### Task 7: QA と段階2 commit

1. targeted Vitest、challenge/research e2e、C#関連testを実行する。
2. `uloop compile --project-path ./moorestech_client`、Error logを確認する。domain reloadなら45秒後再実行する。
3. `cd moorestech_web/webui && pnpm test && pnpm build && pnpm lint && pnpm test:e2e` を順に全実行する。
4. C#を200行、partial、try-catch、Action/event、default arg、日英コメント、不要null check、禁止パス差分の観点でセルフレビューする。
5. `git status` と diff を精査し、`git commit -m "feat(webui): migrate challenge tree and HUD (C1)"`。
6. 実機残作業として T開閉、カテゴリ切替、pan/zoom、完了時HUD animation相当、Web gate、再接続snapshotを報告する。

## QA反例

- child が別カテゴリまたは fixture に存在しない prerequisite GUID を持つ場合、線を推測生成しない。
- event が snapshot より先に届いた場合、Hub revision gate により古い snapshot が Web 状態を上書きしない。
- 選択中カテゴリが完了eventで未解放へ変わる場合、先頭の可視カテゴリへfallbackし、存在しないツリーを保持しない。
- current が空の場合、HUD枠や見出しもDOMへ残さない。
