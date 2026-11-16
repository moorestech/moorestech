# web-ui 設計負債監査レポート (2026-07-17)

- **対象**: `moorestech_web/webui` (`web-ui` ブランチ, React 18 + Mantine 8 + Zustand 5 + Vite + CSS Modules, 約5,400行 / 139ファイル)
- **手法**: 6観点 (CSS設計 / 共通コンポーネント化 / モジュール境界 / hooks・状態管理 / bridge通信層 / 抽象度・一貫性) の並列レビュー → 観点横断の重複統合 (61件→45件) → **全45件を独立エージェントが実コード照合で検証** (反証試行・行番号裏取り・severity再判定) の3段構成。計16エージェント・約140万トークン。
- **注意**: 行番号は監査時点のスナップショット。検証ノートに「行ズレ」とある箇所は検証時の実行番号が正。
- **鮮度更新 (2026-07-18)**: 監査後のuGUI視覚パリティ調整（22ファイル・+909/-299行）を反映し、B1/B3/B4/C2/D1/D3/D4/E4/E5/E7/H1 を現状に合わせて改稿。bridge・hooks・境界系（A/F/G群ほか）は対象ファイル無変更で監査時の記述が有効。

## 総評

**「致命傷 (critical)」は検証の結果ゼロ。** レビュー段階で critical とされた2件 (A1, A4) も、検証で「機能追加のたびに壊れる構造ではない」として major へ降格された。骨格 — feature-sliced 構成、bridge 境界の宣言、`*Logic.ts` + テストのロジック抽出パターン、blockComponentRegistry の宣言的登録、notify の sink 注入 — は健全で、45件中に「作り直しが必要」級のものは無い。

一方で major 12件が示す構造的な弱点は明確に2系統ある:

1. **暗黙の不変条件が自然なリファクタで無音破壊される構造** — readTopic の鮮度が常時マウントの偶然に依存 (A3)、z-index 層序がコメント相互参照のみ (B2)、パネル等高 445 の数値一致契約 (B3)。いずれも「型もテストも検出しない」ことが検証で実証されている。
2. **宣言した境界を強制する仕組みの不在** — bridge の barrel 迂回が約40ファイルで既成事実化 (A6)、CSS モジュール越境 import (C2)。検証で **ESLint 設定自体が存在しない** ことが判明しており、境界は現状すべて人間の注意力で維持されている。

CSS はデザイントークン層の不在 (B1) が最大の負債で、視覚パリティ作業の効率を直接損ねている (値1つの調整が3〜6ファイルの追跡になる)。パリティ作業が進むほど直書き値が増えるため、**着手は早いほど安い**。

## 推奨着手順

1. **小さく挙動リスクを摘む** — A1 (成功でループ終了)・A2 (shape ガード)・A5 (initBridge 化) は各10行前後。A3 (常時購読 topic の pin と明文化) もこの段で。
2. **境界の機械的強制** — ESLint + `no-restricted-imports` を導入し、A6 / C2 / G1 を機械的置換で一掃。以後再発しない。
3. **CSS トークン層** — B2 (z-index 一列化) → B1 (色・影・バッジのトークン化) → B3 (336/445 の CSS 集約)。D1〜D6 もこのついでに処理。
4. **構造統合 (設計判断を伴う)** — C1 (views の SectionStackView 統合)、B4 (GamePanel 正準化)、A4 (validators の zod 化)。E群はこの作業のついでに回収。
5. **掃除** — G5 / H群のデッドコード・命名統一・ユーティリティ統合。すべて機械的。

## 指摘一覧 (45件)

| ID | severity | 検証 | タイトル |
|----|----------|------|----------|
| A1 | major | 一部誇張 | itemMasterStoreが成功後も3秒毎に新Mapで全購読者を再レンダー |
| A2 | major | 確定 | itemMaster HTTP経路の無検証によるリトライループ恒久停止リスク |
| A3 | major | 確定 | readTopicの鮮度が常時マウント購読の暗黙前提に依存 |
| A4 | major | 一部誇張 | craftRecipesバリデータだけ要素検証ゼロでUIクラッシュ経路が開いている |
| A5 | major | 確定 | webSocketClientのimport時接続開始副作用シングルトン |
| A6 | major | 確定 | bridge公開境界の形骸化（深いimport多数・notify契約のindex未公開） |
| B1 | major | 確定 | デザイントークン層の不在（色・影・フォント値の直書き散在） |
| B2 | major | 確定 | z-index層序が多ファイル分散でコメント頼み |
| B3 | major | 確定 | パネル幅336/高さ445のマジック値がtsxインラインstyleに重複しCSSと分裂 |
| B4 | major | 確定 | パネル外装クロームがGamePanelを使わず複数系統に分裂 |
| C1 | major | 確定 | blockInventory/viewsの同型合成ラッパ増殖構造 |
| C2 | major | 確定 | MachineRecipeViewがItemSlotの内部CSSモジュールを直importする境界違反 |
| D1 | minor | 一部誇張→現状は確証 | recipeTreeButtonとcraftButtonの宣言重複 |
| D2 | minor | 一部誇張 | --slot-sizeトークンのバイパスとfallback不一致 |
| D3 | minor | 確定 | 未使用セレクタ.viewerCol/.recipeItems |
| D4 | minor | 確定 | CSSファイル命名・ディレクトリ構成・export形式の流儀混在 |
| D5 | minor | 確定 | カラー表記の新旧記法混在 |
| D6 | minor | 確定 | GamePanel装飾罫線のnth-child DOM順序依存 |
| E1 | minor | 一部誇張 | 素材充足の視覚表現がfeatureごとに別ハック（ItemSlot API不足） |
| E2 | minor | 一部誇張 | BuildMenuSlotがItemSlotのジェスチャ配線とスロットCSSを再実装 |
| E3 | minor | 一部誇張 | ItemIconとBlockIconの実質重複コンポーネント |
| E4 | minor | 一部誇張 | 入力→出力矢印と進捗バー構造の3重実装 |
| E5 | minor | 確定 | HotbarPanelがSlotGridを使わずflex手書き（SlotGridの死にAPI化） |
| E6 | minor | 確定 | 現在値/要求値・不足時赤のステータステキスト3重手書き |
| E7 | minor | 確定 | shared/ui内の意匠分裂（FluidSlot/ProgressArrowがMantine旧テーマのまま） |
| E8 | minor | 確定 | connecting...プレースホルダの3重手書き重複 |
| F1 | minor | 一部誇張 | unsubscribe後もtopicStoreに旧値が残留しstale描画・契約破り |
| F2 | minor | 一部誇張 | スロット操作プランのstale closureによる誤action選択 |
| F3 | minor | 確定 | useTopicSelectorのequality関数不在（コメント規約頼み） |
| F4 | minor | 確定 | アイテム名/maxStack解決経路の3方式分裂 |
| F5 | minor | 確定 | activeLayerがplayerInventoryをgame扱いしEsc/Tab閉じが非対称 |
| F6 | minor | 一部誇張 | shared/uiStateがapp層ルーティング知識と全feature名を保持 |
| G1 | minor | 確定 | barrel迂回の残件（toastStore/SlotGrid直指定） |
| G2 | minor | 確定 | protocol.tsのui_stateペイロード型がUiStateNamesと二重定義 |
| G3 | minor | 確定 | TankInventoryが存在テストで延命されたデッドコード |
| G4 | minor | 確定 | App.tsxに画面固有クロームとドメインactionが混入 |
| G5 | minor | 確定 | ClientMsgのsnapshot要求opが死んだワイヤ面 |
| G6 | minor | 確定 | アイコンURL等HTTPワイヤ面がshared/uiにハードコード散在 |
| H1 | minor | 一部誇張 | recipe/直下が10ファイル上限を超過（RecipeViewer.module.css分割の副作用） |
| H2 | minor | 一部誇張 | 所持数充足述語のrecipe/research二重実装 |
| H3 | minor | 確定 | buildMenuLogic/progressLogicの形式的パススルーLogicファイル |
| H4 | minor | 確定 | 小型ユーティリティ（clamp・比率・循環index）の再発明散在 |
| H5 | minor | 確定 | GamePanel/SlotGridの投機的未使用propsとコメント乖離 |
| H6 | minor | 確定 | 参照ゼロ/テスト専用exportの残存 |
| X1 | minor | 一部誇張 | BENIGN_ERRORSの網羅漏れで正常操作の競合がエラートースト化 |


---

## A. 挙動リスクに最も近い major

放置しても今日壊れるわけではないが、**自然な変更が無音でバグ化する経路**が実証されているもの。着手優先度が最も高い。

### A1. itemMasterStoreが成功後も3秒毎に新Mapで全購読者を再レンダー

- **severity**: major (レビュー時 critical から検証で訂正)
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: hooks-state, bridge (独立指摘 2 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/bridge/store/itemMasterStore.ts`
  - `moorestech_web/webui/src/features/blockInventory/BlockInventoryPanel.tsx`
  - `moorestech_web/webui/src/bridge/store/itemMasterStore.test.ts`

**根拠 (レビューエージェントの実測):**

- *hooks-state*: itemMasterStore.ts:27-38 の loadWithRetry は `for (;;) { ... if (data) { setMaster(new Map(...)) } ... await new Promise((resolve) => setTimeout(resolve, RETRY_INTERVAL_MS)); }` で、成功時の break/return が無く恒久的に3秒ポーリングし、毎回 `new Map(...)` の新 identity で setMaster する。itemMasterStore.test.ts:43 は `expect(secondMaster).not.toBe(firstMaster);` とこの identity 置換を仕様として固定している。結果、useItemMaster の購読者6箇所（InventoryPanel/HotbarPanel/RecipeViewer/ItemListPanel/ResearchTreePanel/BlockInventoryPanel）が内容不変でも3秒毎に再レンダーされる。特に BlockInventoryPanel.tsx:17-26 は「identity 安定で Body の無駄な再レンダーを防ぐ」とコメントした `useMemo(..., [grabCount, itemMaster])` で context 値を安定化させているが、itemMaster が3秒毎に新参照になるため context 値も3秒毎に変わり、ブロックUI配下ツリー全体の再レンダーを引き起こす。コメントの主張が恒常的に偽になっている。

- *bridge*: itemMasterStore.ts:27-38 の `async function loadWithRetry() { for (;;) { ... if (data) { setMaster(new Map(...)) } await setTimeout(3000) } }` は成功後も return せず、データが同一でも毎回新規 Map 参照で set する（テスト itemMasterStore.test.ts:43 `expect(secondMaster).not.toBe(firstMaster)` が置換を仕様として固定している）。zustand v5 の等価判定は Object.is のため、useItemMaster の全消費者（HotbarPanel＝常時マウントHUD、InventoryPanel、BlockInventoryPanel、RecipeViewer、ItemListPanel、ResearchTreePanel の6箇所）が3秒毎に無条件再レンダーされる。さらに BlockInventoryPanel.tsx:19-26 は `useMemo(..., [grabCount, itemMaster])` で interaction context を作るため、ブロックUI表示中は配下の全 context 消費者にも3秒毎に伝播する。WS側は差分event購読なのに、このストアだけ変化の有無を問わないポーリング置換で、プロジェクト方針「状態変化の検知は購読で」とも逆行している。

**検証ノート (独立エージェントによる反証試行の結果):**

> 証拠は全て正確。itemMasterStore.ts:27-38のfor(;;)は成功後もbreak/returnせず3秒毎にnew Mapでsetし、test:43が`not.toBe`で置換を固定。useItemMaster.ts:11は`(s)=>s.master`のObject.is比較で、消費者6箇所(InventoryPanel:11/HotbarPanel:13/RecipeViewer:15/ItemListPanel:13/ResearchTreePanel:19/BlockInventoryPanel:15)をgrepで確認。BlockInventoryPanel.tsx:17-18の『identity安定』コメントは恒常的に偽で、useMemo(:25)経由でcontext配下へ3秒毎に伝播、shared/uiにReact.memoは無くサブツリー全体が再レンダーされる。ただしcriticalの基準『機能追加のたびに壊れる/バグ量産』には該当しない: React再レンダーは意味的に安全で機能バグは生まず、影響は無駄なCPU/fetchと誤解を招くコメントに限定され、修正も局所的(内容一致時のsetMasterスキップ等)。test:26のタイトルが示す通り再取得自体は意図設計(マスタのライブ再読込)。恒常的な全購読者再レンダー+テストによる固定化は実害ある設計欠陥なのでmajorが適正。

**推奨対応**: 取得成功でループを終了する（アイテムマスタは静的データであり、初回成功後の3秒ポーリングは根拠がない）。実行時リロードが本当に要件なら、fetch 結果が前回と内容一致する場合に setMaster をスキップして identity を保つか、ホストからの topic/イベント通知駆動のリロードに置き換え、テストの not.toBe アサーションも撤去する。

### A2. itemMaster HTTP経路の無検証によるリトライループ恒久停止リスク

- **severity**: major
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: bridge
- **対象ファイル**:
  - `moorestech_web/webui/src/bridge/store/itemMasterStore.ts`

**根拠 (レビューエージェントの実測):**

- *bridge*: itemMasterStore.ts:31 `const data: ItemMasterData | null = await res.json().catch(() => null);` は res.json() の any を無検証で型注釈に流し込むだけで、WS 経路の deliverTopicPayload（topicStore.ts:27-35 で validate→反映）に相当するチェックが無い。items キーを欠く JSON（例: エラーオブジェクトを 200 で返す等）が来ると :33 `data.items.map(...)` が TypeError を投げ、それは :24 `void loadWithRetry()` の void 済み Promise 内なので unhandled rejection となり、for(;;) ループ自体が脱出して二度と再試行されない。結果 master は null 固定で、全アイテム名・maxStack 解決と planDirectMoves の maxStack 依存経路が沈黙したまま劣化する。503/ネットワーク断/JSONパース失敗は .catch で防護済み（test:47-90）なのに、shape 不正だけ防護が抜けている非対称。

**検証ノート (独立エージェントによる反証試行の結果):**

> 全メカニズムを実コードで確認。itemMasterStore.ts:29-33: fetchとres.json()のみ.catch防護、data truthyならdata.items.mapを無検証実行。itemsキー欠落やitems内null要素(i.itemId参照)でTypeErrorが:24のvoid済みPromiseへ伝播しunhandled rejection、for(;;)ループは恒久脱出し再試行停止、masterはnull固定。itemMasterStore.test.ts:47(503)/:63(ネットワーク例外)/:75(JSONパース失敗)は防護テスト済みだがshape不正のみ未防護という非対称も正確。WS経路はdeliverTopicPayloadで検証されるがHTTP経路は無検証という設計非対称も確認(topicStore経由のvalidate vs 生キャスト)。発火には信頼済みホストが200+正JSON+誤shapeを返す必要があり確率は低いが、一過性異常が恒久停止に化ける非回復性(リロードまで復旧不能)はループ系の欠陥として実害があり、majorは妥当。

**推奨対応**: validators.ts と同列に ItemMasterData の shape ガード（items が配列で各要素に itemId/name/maxStack の number/string 検査）を置き、不合格ならスキップして次ループへ継続する。最低でも `Array.isArray(data?.items)` ガードでループの生存を保証する。

### A3. readTopicの鮮度が常時マウント購読の暗黙前提に依存

- **severity**: major
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: hooks-state, bridge (独立指摘 2 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/shared/uiState/activeLayer.ts`
  - `moorestech_web/webui/src/bridge/store/useTopic.ts`
  - `moorestech_web/webui/src/app/App.tsx`

**根拠 (レビューエージェントの実測):**

- *hooks-state*: useTopic.ts:43-45 の readTopic は「購読はしない」命令的読み出し。activeLayer.ts:20-22 はキー入力排他の根拠となる modal/blockInventory/uiState を readTopic で読むが、これらが最新であるのは ModalHost（App.tsx:95 無条件マウント）・BlockInventoryPanel（App.tsx:94 無条件マウント）・App の useTopicSelector(Topics.uiState)（App.tsx:51）がたまたま購読を保持しているからで、この依存はどこにも文書化・強制されていない。App.tsx では他のパネルがすべて `screen !== "none" &&` の条件付きレンダー（62,66,78,84,88-93行）なので、ModalHost 等を同様に条件付ける自然な変更をした瞬間、unsubscribe により topic が更新されなくなり、useGameLayerKeydown.ts:16 の `readActiveLayer() !== "game"` ゲートが古い値で誤判定する。エラーは一切出ない。

- *bridge*: activeLayer.ts:19-28 の readActiveLayer は `readTopic(Topics.modal)` / `readTopic(Topics.blockInventory)` / `readTopic(Topics.uiState)` の3値からゲーム入力の排他を決めるが、readTopic は useTopic.ts:43-47 のコメント通り「購読はしない」ため、これらの topic が最新である保証は ModalHost・BlockInventoryPanel・App が常時マウントで購読し続けているという App.tsx:51,94,95 の構成上の偶然にある。どれかが条件マウント化された瞬間、readActiveLayer は unsubscribe 後に残置された stale 値（前項の残置問題と複合）で誤ったレイヤーを返し、キー入力が誤った層に流れるが、型もテストもこの結合を検出しない。

**検証ノート (独立エージェントによる反証試行の結果):**

> 全evidence一致を確認。readTopicは購読しない(useTopic.ts:43-47)、readActiveLayerはmodal/blockInventory/uiStateをreadTopicで読む(activeLayer.ts:20-22)、ModalHost/BlockInventoryPanelは無条件マウント(App.tsx:94-95)、他パネルはscreen条件付き(62,66,78,84,88-93)。webSocketClient.ts全文を確認したがbridge初期化時のtopic pinは存在せず、購読はマウント中のフックのみ。subscriptionManager.ts:27-37は最終releaseでunsubscribeし、topicStoreは値を削除しないため stale値が残置される。破壊シナリオも実在: 例えばBlockInventoryPanelを周囲のパターンに合わせ screen==="subInventory" 条件にすると、閉鎖時にunsubscribeされ open=false イベントが届かず store に open=true が永久残置→readActiveLayerが"blockInventory"を返し続けgame層キー入力(Esc/1-9/ホイール)が無音で全死する。同じ暗黙依存は slotActions.ts:28・useBlockSlotGestures.ts:25・HotbarPanel:22 のreadTopicにも及ぶ。軽微な誇張として「他のパネルがすべて条件付き」は不正確(HotbarPanel:87とProgressBar:96も無条件)だが結論に影響なし。無文書の跨モジュール不変条件が自然なリファクタで無音破壊される構造でありmajor妥当。

**推奨対応**: レイヤー判定と event 時読み出しに使う topic（modal/blockInventory/uiState/inventory）は bridge 初期化または App ルートの effect で subscriptions.acquire して pin し、「常時購読 topic」の明示リストとして一元管理する。readTopic の JSDoc に「pin 済み topic のみ読んでよい」と契約を書く。

### A4. craftRecipesバリデータだけ要素検証ゼロでUIクラッシュ経路が開いている

- **severity**: major (レビュー時 critical から検証で訂正)
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: bridge, abstraction-consistency (独立指摘 2 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/bridge/contract/validators.ts`
  - `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`
  - `moorestech_web/webui/src/bridge/store/useTopic.ts`
  - `moorestech_web/webui/src/features/recipe/views/CraftRecipeView.tsx`
  - `moorestech_web/webui/src/features/recipe/craftLogic.ts`

**根拠 (レビューエージェントの実測):**

- *bridge*: payloadTypes.ts:107-114 は CraftRecipe を `{ recipeGuid: string; resultItemId: number; resultCount: number; craftTime: number; requiredItems: RequiredItem[] }` と定義するが、validators.ts:107-109 の対応バリデータは `function validCraftRecipes(d: unknown): boolean { return isObject(d) && Array.isArray(d.recipes); }` のみで要素を一切検査しない（隣の validMachineRecipes:113-121 は全フィールド検査しており、同一ファイル内で検査水準が乖離済み）。素通りした値は useTopic.ts:18 の `(s.topics[topic] ?? null) as TopicPayloads[K]` で CraftRecipesData に断定キャストされ、CraftRecipeView.tsx:28 `craftable(recipe, counts)`（内部で craftLogic.ts:50 `recipe.requiredItems.every(...)`）と :44 `recipe.requiredItems.map(...)` で requiredItems 欠落時に TypeError → ErrorBoundary 直行。さらにこの topic だけ validators.test.ts に単体テストが無く、C# 共有フィクスチャ（WireFixtures/ 一覧に craft_recipes 系が存在しない）にも無い。新topic追加は payloadTypes.ts（型）・validators.ts（関数＋registry の2箇所）・protocol.ts（Topics＋TopicPayloads の2箇所）の3ファイル5箇所を手で整合させる構造で、検査の抜けを機械的に検出する仕組みが無いため、topicが増えるたびに今回と同種の穴が再生産される。

- *abstraction-consistency*: validators.ts:107-109 `function validCraftRecipes(d) { return isObject(d) && Array.isArray(d.recipes); }` は配列であることしか見ないのに対し、隣の validMachineRecipes (113-121) は recipeGuid/blockId/blockName/time/inputItems/outputItems を全数検査、validResearchNode も「不正ノード1件で全体破棄」と明記して全フィールド検査している。一方 craftLogic.ts:50 `recipe.requiredItems.every((r) => ...)` や CraftRecipeView.tsx:32 `recipe.craftTime` は要素 shape を前提にしており、`{recipes:[{}]}` が単一チェックポイント (deliverTopicPayload) を素通りしてレンダー中 TypeError → AppErrorBoundary の全画面フォールバックに至る。「validate → store write の単一チェックポイント」という設計意図が craftRecipes だけ実質無効。

**検証ノート (独立エージェントによる反証試行の結果):**

> コード照合は概ね正確: validators.ts:107-109は配列判定のみ、validMachineRecipes(119-121)・validResearchNode(128-134)は全数検査で水準乖離は実在。useTopic.ts:18の断定キャスト、craftLogic.ts:50のevery、CraftRecipeView.tsx:44のmap、validators.test.tsにcraftRecipesテスト無し、WireFixtures/にcraft_recipes系無し、全て確認。ただし例示の{recipes:[{}]}は実際にはクラッシュしない: selectCraftRecipes(craftLogic.ts:14-16)がresultItemId===itemIdでフィルタするため空配列となりRecipeContent.tsx:60-67の「レシピはありません」分岐に落ちる。クラッシュには{recipes:[null]}(filter内でTypeError)か「選択itemIdに一致するresultItemIdを持ちrequiredItems欠落の要素」が必要で経路は主張より狭い。発火条件は信頼済みローカルC#ホストの契約ドリフトのみで、10topic中9つは深い検査が書かれておりパターン自体は定着している。criticalの定義(機能追加のたびに壊れる/バグ量産)には該当せず、防御層の実在する穴+テスト/フィクスチャ欠落としてmajorが妥当。補足発見: validatorsレジストリはRecord<string,...>で網羅性のコンパイル時強制が無く、未知topicは素通し(validators.ts:162-164)のため新topicのバリデータ追加漏れは無言で無検証になる—構造的懸念自体は一部正しい。

**推奨対応**: zod（またはvalibot）でスキーマを単一定義にし、型は z.infer で導出、validators と TopicPayloads をスキーマから機械生成して「型⊃バリデータ」の乖離を構造的に不可能にする。全面移行前の応急処置として validCraftRecipes を validMachineRecipes と同水準の要素検査に引き上げ、craft_recipes の共有フィクスチャと validators.test.ts を追加する。

### A5. webSocketClientのimport時接続開始副作用シングルトン

- **severity**: major
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: abstraction-consistency
- **対象ファイル**:
  - `moorestech_web/webui/src/bridge/transport/webSocketClient.ts`
  - `moorestech_web/webui/src/features/blockInventory/blockComponentRegistry.test.ts`

**根拠 (レビューエージェントの実測):**

- *abstraction-consistency*: webSocketClient.ts:128 `const client = new WebSocketClient(`ws://${location.host}/ws`);` がモジュールトップレベルで実行され、import しただけで location.host 参照と接続開始が走る。この副作用は既にテストへ波及しており、blockComponentRegistry.test.ts:3-7 は「解決対象コンポーネントは BlockItemGrid 経由で webSocketClient を読み込む。import 時に location.host を触るため node 環境では stub する」というコメント付きで `vi.mock("@/bridge/transport/webSocketClient", ...)` を強いられている。bridge を transitive import する node 環境テストを書くたびにこの vi.mock 儀式が必要になる。

**検証ノート (独立エージェントによる反証試行の結果):**

> webSocketClient.ts:128のモジュールトップレベルnew(location.host参照)とコンストラクタ:37のopenSocket()即時接続を確認。blockComponentRegistry.test.ts:3-7の引用コメントとvi.mockも一致。検証で範囲が主張より広いことを追加発見: vi.mock儀式は5テストファイルに既に波及(wireContract.test.ts:7、actions.test.ts:5、blockComponentRegistry.test.ts:7、activeLayer.test.ts:5、uiScreenRouting.test.ts:5)。特にuiScreenRouting.test.tsは純定数UiStateNamesをbarrel経由で読むだけでstubを強制されており、bridgeに触れるnodeテスト追加のたびに儀式が再生産される構造は実証済み。推奨案の前例setToastSink注入もsrc/main.tsx:10,15に実在。ランタイムバグは無いがテスト追加毎の再発friction+import副作用の設計смellとしてmajor妥当。

**推奨対応**: 接続開始を `initBridge()` のような明示関数へ移し main.tsx から呼ぶ（同ファイルの setToastSink 注入と同じ前例）。client は初回 init まで null とし、sendAction は未 init なら reject("disconnected") に落とせば既存のエラーパスに乗る。これで blockComponentRegistry.test.ts の vi.mock は不要になる。

### A6. bridge公開境界の形骸化（深いimport多数・notify契約のindex未公開）

- **severity**: major
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: boundaries, bridge (独立指摘 3 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/bridge/index.ts`
  - `moorestech_web/webui/src/features/inventory/HotbarPanel/index.tsx`
  - `moorestech_web/webui/src/features/inventory/slotActions.ts`
  - `moorestech_web/webui/src/shared/itemMove/plannedAction.ts`
  - `moorestech_web/webui/src/features/toast/toastStore.ts`
  - `moorestech_web/webui/src/main.tsx`

**根拠 (レビューエージェントの実測):**

- *boundaries*: bridge/index.ts:1-2 は「bridge の public API。feature 層はここ経由で通信境界へアクセスする」と宣言し、index.ts:8 で `export type * from "./contract/payloadTypes";` を公開済み。にもかかわらず非テスト39ファイルが `@/bridge/contract/payloadTypes` を直import しており、うち11ファイル（HotbarPanel/index.tsx:1 `from "@/bridge"` と :4 `from "@/bridge/contract/payloadTypes"`、slotActions.ts:1-2、ModalHost.tsx:3-4 等）は同一ファイル内で barrel と深いimport を併用している。さらに shared/itemMove/plannedAction.ts:1 は `import type { ActionPayloads } from "@/bridge/transport/protocol";` と transport 内部を直指定する一方、隣の dispatchPlanned.ts:1-2 は同じ型を `@/bridge` から取っており、bridge 内部構造（contract/transport の分割）が層外に漏れて既成事実化している。

- *boundaries*: bridge→features の逆依存を避ける sink 注入（notify.ts:5-8「実体（toast store）は起動時に features 側から注入する」）は正しい設計だが、その公式な境界APIである setToastSink と NotifyVariant が bridge/index.ts に export されていない。結果、main.tsx:10 `import { setToastSink } from "@/bridge/transport/notify";` と toastStore.ts:2 `import type { NotifyVariant } from "@/bridge/transport/notify";` が transport 内部への深いimportを強いられており、「feature 層は index 経由」という bridge 自身の宣言（index.ts:1-2）と矛盾する。

- *bridge*: index.ts:1-2 は「feature 層はここ経由で通信境界へアクセスする」と宣言し `export type * from "./contract/payloadTypes"` まで用意しているのに、grep 実測で features/shared の30ファイル超が `@/bridge/contract/payloadTypes` を直接 import（例: ResearchNodeCard.tsx:2, blockComponentRegistry.ts:2, slotActions.ts:2）、plannedAction.ts:1 は `@/bridge/transport/protocol`、toastStore.ts:2 は `@/bridge/transport/notify` を深部 import している。さらに main.tsx:10 の `import { setToastSink } from "@/bridge/transport/notify"` は index.ts が setToastSink/NotifyVariant を export していないため深部 import 以外の選択肢が無い。宣言された単一入口が lint で強制されず API も不完全なため、bridge 内部のファイル再配置（今後の zod 化等）が外部30ファイルの import パス修正を伴う状態になっている。

**検証ノート (独立エージェントによる反証試行の結果):**

> 数字まで実測一致。@/bridge/contract/payloadTypes の bridge外深部importは42ファイル（テスト3件: craftLogic.test.ts/researchLogic.test.ts/playerSlotPlan.test.ts を除き非テスト39件で指摘どおり）。barrel併用はgrepでちょうど11ファイル（HotbarPanel/index.tsx:1,4・slotActions.ts:1-2・ModalHost.tsx:3-4等）。bridge/index.ts:3-8にsetToastSink/NotifyVariantのexportは無く、main.tsx:10とtoastStore.ts:2は深部import以外の選択肢が無いことを確認。plannedAction.ts:1(transport/protocol直指定)とdispatchPlanned.ts:1-2(@/bridge経由)の非対称も確認。追加発見: eslint設定ファイル自体が存在せず(eslint.config.*無し)、境界強制の手段が完全にゼロ。宣言した単一入口が40ファイル規模で形骸化しておりmajor妥当。

**推奨対応**: 型も含めて層外からのimportは `@/bridge` 一本に統一し（barrel は既に全型を再export済みなので機械的置換で完了する）、eslint の no-restricted-imports で `@/bridge/*` 内部パスを bridge 外から禁止して再発を止める。plannedAction.ts は `@/bridge` からの import に修正する。


---

## B. CSS 負債 (major)

視覚パリティ作業の効率に直結する構造負債。値を1つ調整するたびに複数ファイルを追いかける構造が中心。

### B1. デザイントークン層の不在（色・影・フォント値の直書き散在）

- **severity**: major
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: css
- **対象ファイル**:
  - `moorestech_web/webui/src/app/index.css`
  - `moorestech_web/webui/src/app/App.module.css`
  - `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css`
  - `moorestech_web/webui/src/features/recipe/RecipeViewer.module.css`
  - `moorestech_web/webui/src/shared/ui/ItemSlot/style.module.css`
  - `moorestech_web/webui/src/shared/ui/FluidSlot/style.module.css`
  - `moorestech_web/webui/src/features/inventory/HotbarPanel/style.module.css`

**根拠 (レビューエージェントの実測):**

- *css*: index.css:5-7 の :root は `--slot-size: 3rem;` のみでカラー/影/z のトークンが無い。実測: hex 色は全 CSS で 26 種。主文字色 `color: #f2f3f7` が 3 ファイル 3 回 (App.module.css:80, GamePanel/style.module.css:72, RecipeViewer.module.css:186)。near-white 文字色は 6 種が併存 (#f2f3f7 / #e8eaf0 GamePanel:9 / #e2e5ee HotbarPanel:39 / #d7dbe5 RecipeViewer:129 / #cdd2df App:88 / #eee index.css:13, buildMenu:43)。暗色 text-shadow は 6 ファイルで 6 バリアント (App:81 `0 2px 4px rgba(0,0,0,0.9)`, RecipeViewer:131 `0 1px 2px rgb(0 0 0 / 85%)`, RecipeViewer:187 `70%`, HotbarPanel:41 と FluidSlot:28 は `0 1px 2px rgba(0, 0, 0, 0.8)` の完全一致ペア, GamePanel:73 `0 1px 1px rgba(0,0,0,0.6)`)。`font-size: 10px` は 5 ファイル 5 回 (RecipeViewer:47, HotbarPanel:36, buildMenu:42, ItemIcon.module.css:7, ItemSlot:40)。cyan/blue 系アクセントは #5db2cd(ItemSlot:24)・#3fa0e8(RecipeViewer:107 他16回)・#42d5f3/#1385dc/#13cff0(RecipeViewer:44)・#48a0e8/#168ce5(RecipeViewer:153) 等 12 種以上に分裂。ItemSlot の .count (style.module.css:36-45) と FluidSlot の .amount (style.module.css:21-29) は「絶対配置右下・weight700・白文字・暗影」の同型バッジがサイズ違い(10px/12px)で重複。

**検証ノート (独立エージェントによる反証試行の結果):**

> evidenceはほぼ正確。:rootは--slot-sizeのみ(index.css:5-7)、実測hex色は28種(指摘の26とほぼ一致、rgb()系は別途多数)。#f2f3f7はApp.module.css:80/GamePanel:73/RecipeViewer:186の3箇所(GamePanelは72でなく73、些細なずれ)。near-white 6種(#f2f3f7/#e8eaf0 GamePanel:9/#e2e5ee HotbarPanel:39/#d7dbe5 RecipeViewer:129/#cdd2df App:88/#eee index.css:13+buildMenu:43)全て実在確認。HotbarPanel:41とFluidSlot:28のtext-shadow完全一致ペアも確認。font-size:10pxはRecipeViewer:47/HotbarPanel:36/buildMenu:42/ItemIcon.module.css:7/ItemSlot:46(指摘の40は誤りだが実在)。ItemSlot .countは実際は42-51行(指摘36-45)、#5db2cdは30行(指摘24)と行番号ずれはあるが内容は全一致。青系hexは15種確認(ただし約半数はcraftButtonのhover/disabled/グラデ段でありトークン化しても消えない点は割引材料)。完全重複ペアの実在と視覚パリティ調整中の追跡コスト増は事実で、共通化機会の上限であるmajor妥当。

**推奨対応**: index.css の :root に文字色(主/副)・暗色 text-shadow・アクセント cyan・バッジ文字(10px/700)の 4 系統だけでも CSS 変数として昇格し、既存 26 hex を uGUI 正本と突き合わせて統廃合する。視覚パリティ作業中に値を微調整するたび 3〜6 箇所を追いかける現状は、トークン 1 箇所の変更に置き換えられる。

**鮮度更新 (2026-07-18)**: index.css の :root に `--face-inset`/`--bevel-1〜3`/`--bevel-c1〜3` の色トークンが新設され「:rootは--slot-sizeのみ」は過去の記述になった。ただし distinct hex 色は28種で監査時と同水準のままで、新設された RecipeBox.module.css だけで青系8色が新規に散在しており直書き総量は減っていない。RecipeViewer.module.css の分割（後述H1）により本項が引用する行番号は総崩れになっている。「主要装飾色は直書きのまま散在」という結論自体は不変。

### B2. z-index層序が多ファイル分散でコメント頼み

- **severity**: major
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: css, shared-components (独立指摘 2 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/app/App.module.css`
  - `moorestech_web/webui/src/features/blockInventory/style.module.css`
  - `moorestech_web/webui/src/features/toast/style.module.css`
  - `moorestech_web/webui/src/features/research/style.module.css`
  - `moorestech_web/webui/src/features/buildMenu/style.module.css`
  - `moorestech_web/webui/src/features/progress/style.module.css`
  - `moorestech_web/webui/src/features/inventory/InventoryPanel/GrabOverlay.module.css`
  - `moorestech_web/webui/src/app/App.tsx`

**根拠 (レビューエージェントの実測):**

- *css*: スケールは buildMenu:12 `z-index: 20`, progress:10 `z-index: 20`, blockInventory:8 `z-index: 30`, research:6 `z-index: 30`, GrabOverlay:7 `z-index: 40`, toast:7 `z-index: 300`, App.tsx:106 `zIndex={2000}`(インライン) と 7 箇所に分散し、20 と 30 は 2 ファイルずつ偶然の一致で衝突予備軍。全体の順序契約は blockInventory/style.module.css:1 「grab(z-40)/toast(z-300) の下、progress(z-20) の上」、toast/style.module.css:1 「Mantine Modal(z-200) より上に出すため z-index 300」、research/style.module.css:1 のコメント 3 箇所にしか存在せず、基準となる Mantine Modal の 200 はコード上どこにも現れない。新しいオーバーレイを足すには 3 ファイルのコメントと Mantine のデフォルトを人手で照合する必要がある。

- *shared-components*: blockInventory/style.module.css:1-2 のコメント「grab(z-40)/toast(z-300) の下、progress(z-20) の上」、buildMenu/style.module.css:1「buildMenuはblockInventory/researchより下位」、toast/style.module.css:1-2「Mantine Modal(z-200) より上に出すため z-index 300」と、各CSSが他featureの値を文言で参照して層序を保っている。値は blockInventory=30 / research=30 / buildMenu=20 / progress=20 / grab=40 / toast=300 で、blockInventory と research は同値30のため重なった場合の勝敗は App.tsx の描画順（92-94行）依存。新しいオーバーレイを足すたびに全ファイルのコメントと値を目視で整合させる必要がある。

**検証ノート (独立エージェントによる反証試行の結果):**

> 全値を実測で一致確認: buildMenu:12=20/progress:10=20/blockInventory:8=30/research:6=30/GrabOverlay:7=40/toast:7=300/App.tsx:106=2000。層序契約はblockInventory:1-2・toast:1-2・research:1-2・buildMenu:1-2のコメントのみに存在し、基準のMantine Modal z=200はgrepでコード上どこにも現れないことを確認(features/modal配下にzIndex指定なし)。blockInventory=research=30の同値もApp.tsx:92-94の描画順が唯一のタイブレークである点も正確。割引材料: BlockInventoryPanelはdata.openガード(BlockInventoryPanel.tsx:32)で閉時null、ui_state経由でresearchTreeと排他になる可能性が高く30/30衝突の実害は現状潜在的。ただしGrabOverlay(40)>blockInventory(30)やtoast(300)>Mantine既定200という操作上重要な層序がコメントと外部ライブラリ既定値だけで維持されており、オーバーレイ追加が活発な現段階でトークン一列化の価値は高い。majorの範囲内。

**推奨対応**: index.css の :root に `--z-screen: 20; --z-overlay-panel: 30; --z-grab: 40; --z-modal: 200; --z-toast: 300; --z-reconnect: 2000;` のレイヤトークンを一列で宣言し、各モジュールは var() 参照に置換する。順序の全体像が 1 箇所に並ぶため、コメントによる相互参照は削除できる。

### B3. パネル幅336/高さ445のマジック値がtsxインラインstyleに重複しCSSと分裂

- **severity**: major
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: css, shared-components (独立指摘 2 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/features/recipe/RecipeViewer.tsx`
  - `moorestech_web/webui/src/features/recipe/ItemListPanel.tsx`
  - `moorestech_web/webui/src/features/inventory/InventoryPanel/index.tsx`
  - `moorestech_web/webui/src/app/App.module.css`

**根拠 (レビューエージェントの実測):**

- *css*: 3 カラムの各パネル幅 336px が RecipeViewer.tsx:24 `style={{ ..., width: 336, ... }}`、ItemListPanel.tsx:14 `style={{ ..., width: 336, minHeight: 445 }}`、InventoryPanel/index.tsx:22 `style={{ ..., width: 336 }}` の 3 箇所にインラインで重複。等高契約の 445 も ItemListPanel.tsx:14 と RecipeViewer.tsx:21 `const panelMinHeight = ... ? 445 : 300;` の 2 箇所に分裂している。一方カラム構造自体は App.module.css:32-38 の `grid-template-areas`/`grid-template-columns: auto auto auto` が持っており、1280px ステージ上のカラム寸法という単一の関心が CSS(グリッド)と tsx(インライン幅) の 2 層 4 ファイルに割れている。justifySelf/alignSelf も 3 箇所で毎回指定している。

- *shared-components*: 3箇所全てが style 逃げ道で配置を手書き: InventoryPanel:22 `style={{ justifySelf: "start", alignSelf: "start", width: 336 }}`、RecipeViewer:24 `style={{ alignSelf: "start", justifySelf: "center", width: 336, minWidth: 0, minHeight: panelMinHeight }}`、ItemListPanel:14 `style={{ justifySelf: "end", alignSelf: "start", width: 336, minHeight: 445 }}`。RecipeViewer.tsx:20-21 は「選択時は左右パネルと等高」のコメント付きで `panelMinHeight = ... ? 445 : 300` を持ち、ItemListPanel の 445 と数値一致が暗黙の契約（片方だけ変えると等高が静かに崩れる）。App.module.css:43-47 の `.viewerCol` は grep 上利用者ゼロの死にCSSで、レイアウト責務が App から feature の inline style へ移った残骸。

**検証ノート (独立エージェントによる反証試行の結果):**

> 全数値を実コードで確認。width:336はRecipeViewer.tsx:24・ItemListPanel.tsx:27・InventoryPanel/index.tsx:22の3箇所（指摘のItemListPanel.tsx:14は実際は27行、内容は一致）。445はRecipeViewer.tsx:21のpanelMinHeight三項演算子とItemListPanel.tsx:27のminHeightに分裂し、コメント（19-20行「選択時は左右パネルと等高」）が数値一致を暗黙契約と自認している。App.module.css:32-38のgrid-template-columns:autoは実寸をtsx側インラインに依存、43-46の.viewerColはgrep上利用者ゼロも確認。片方の445だけ変えると等高が静かに崩れる3ファイル契約でmajor妥当。

**推奨対応**: App.module.css に `--panel-width: 336px; --panel-min-height: 445px;` を置き `grid-template-columns` を auto から固定値ベースへ寄せるか、各 feature の module.css に panel クラス(width/justify-self/align-self)を移してインライン style を撤去する。パリティ調整で幅を触るとき 3 ファイル同時更新が必要な現状を 1 箇所にする。

**鮮度更新 (2026-07-18)**: 336/445 の数値自体は消滅した。現在は RecipeViewer.tsx:34 が width:337.2、ItemListPanel.tsx:38 と InventoryPanel/index.tsx:29 が width:378、InventoryPanel minHeight:452.391、ItemListPanel minHeight:452、RecipeViewer panelMinHeight:432.983（選択時、RecipeViewer.tsx:26）と、3ファイルへのマジック値分裂そのものは温存されている。しかも「選択時は左右パネルと等高」というコメントの意図に反し 432.983 ≠ 452/452.391 という新たな不整合が発生しており、分裂はむしろ悪化した。ホットバーは HotbarPanel/style.module.css:1-2 のコメントの通り grid カラム依存から stage 絶対配置（position: absolute + transform）へ変更され(コミット3eb199362)、常時画面中央に固定される構成に変わった。この項目への対応はパリティ較正が安定するまで凍結する方針が決定済み。

### B4. パネル外装クロームがGamePanelを使わず複数系統に分裂

- **severity**: major
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: css, shared-components (独立指摘 2 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css`
  - `moorestech_web/webui/src/features/buildMenu/style.module.css`
  - `moorestech_web/webui/src/features/blockInventory/BlockInventoryPanel.tsx`
  - `moorestech_web/webui/src/features/research/style.module.css`
  - `moorestech_web/webui/src/features/buildMenu/BuildMenuPanel.tsx`
  - `moorestech_web/webui/src/features/research/ResearchTreePanel.tsx`
  - `moorestech_web/webui/src/shared/ui/GamePanel/index.tsx`

**根拠 (レビューエージェントの実測):**

- *css*: uGUI パリティ済みの GamePanel(style.module.css:3-58, 溶け込み背景+craft 二層クローム)に対し、buildMenu/style.module.css:3-13 は独自の `background: rgba(30, 30, 30, 0.92); border-radius: 8px;`、BlockInventoryPanel.tsx:38 は `<Paper ... withBorder bg="dark.6" c="dark.1">`、research/style.module.css:7 は `background: var(--mantine-color-dark-8)` と、画面ごとに別のパネル外装を再発明している。角丸だけでも 0(GamePanel) / 8px(buildMenu) / Mantine radius(Paper) の 3 通り。visual parity の対象が buildMenu・blockInventory・research に進むたび、既存外装を捨てて GamePanel へ寄せる二度手間が確定している。

- *shared-components*: BlockInventoryPanel.tsx:39-49 は `<Group justify="space-between" mb="sm"><Title order={2} size="h4">{data.blockName}</Title><CloseButton ... onClick={() => { void dispatchAction("ui_state.request", { state: UiStateNames.gameScreen }); }} />` を手書き。BuildMenuPanel.tsx:21-23 が同型の `<Group justify="space-between"><Title order={2} size="h4">ビルドメニュー</Title><CloseButton data-testid="build-menu-close" onClick={close} />` を持ち、close も同一の `dispatchAction("ui_state.request", { state: UiStateNames.gameScreen })`（同13行のコメント自身が「BlockInventoryPanel と同型」と自認）。ResearchTreePanel.tsx:32 も `<Title order={2} size="h4" p="sm">研究ツリー</Title>` を手書きで閉じるボタン無し。一方 GamePanel/index.tsx:22-31 には title/headerRight のヘッダAPIが既にあり、grep 上 headerRight の利用者はゼロ。外観も Paper bg="dark.6"（blockInventory）/ rgba(30,30,30,0.92) 手書きCSS（buildMenu）/ dark-8 全画面（research）と3様で、uGUIパリティ済みの GamePanel クローム（decoLine・フェード罫線）が一切届いていない。

**検証ノート (独立エージェントによる反証試行の結果):**

> 全証拠一致を確認。buildMenu/style.module.css:10-11のrgba(30,30,30,0.92)+radius8px、BlockInventoryPanel.tsx:38のPaper withBorder bg=dark.6、research/style.module.css:7のdark-8、GamePanel/style.module.css:3-58のパリティ済みクロームで角丸3通りも正確。headerRightはgrepでGamePanel/index.tsx内の定義のみ（利用者ゼロ）。BuildMenuPanel.tsx:13コメントが「BlockInventoryPanelと同型」と自認し、close処理（ui_state.request gameScreen遷移）もBlockInventoryPanel.tsx:47と完全同一。ResearchTreePanel.tsx:32は閉じるボタン無しのTitle手書きも確認。buildMenu/researchがGamePanelクロームに収束するかは未確定（uGUI側の該当画面の見た目次第）だが、パリティ進行中プロジェクトで3画面+共有API不使用の分裂は共通化機会の上限majorに相当。

**推奨対応**: GamePanel を正準パネルと明示し、buildMenu/blockInventory/research の外装をパリティ作業のバックログとして GamePanel(必要なら fullscreen バリアント追加)へ移行する方針をコメントか設計メモで宣言する。少なくとも新規画面が Paper や自前 rgba 背景を選ばないよう、shared/ui 側にガイドを置く。

**鮮度更新 (2026-07-18)**: GamePanel は bevel・mask-fade・decoLine 等の高度な独自クローム体系へ大幅に進化した一方、buildMenu・blockInventory(Paper withBorder bg="dark.6")・research は完全無変更で、指摘した3系統分裂は温存されたままGamePanel側だけが先行し格差はむしろ拡大している。headerRight は依然として全呼び出しで未使用のまま。

---

## C. 共通コンポーネント化 (major)

CSSも含めた共通コンポーネント化の本丸。ファイル増殖構造と、共有部品を迂回した実装。

### C1. blockInventory/viewsの同型合成ラッパ増殖構造

- **severity**: major
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: shared-components
- **対象ファイル**:
  - `moorestech_web/webui/src/features/blockInventory/views/MachineInventory.tsx`
  - `moorestech_web/webui/src/features/blockInventory/views/GearMachineInventory.tsx`
  - `moorestech_web/webui/src/features/blockInventory/views/MinerInventory.tsx`
  - `moorestech_web/webui/src/features/blockInventory/views/GearMinerInventory.tsx`
  - `moorestech_web/webui/src/features/blockInventory/views/GeneratorInventory.tsx`
  - `moorestech_web/webui/src/features/blockInventory/blockComponentRegistry.ts`

**根拠 (レビューエージェントの実測):**

- *shared-components*: Machine(9-15行)/GearMachine(10-16)/Miner(10-16)/GearMiner(11-18)/Generator(11-19) の5ビューは全て `<Stack gap="sm">` + セクション列挙のみで、差分は子リストだけ（例: MachineInventory=`<MachineSection/><ElectricNetworkSection/>`、GearMachineInventory=`<MachineSection/><GearSection/><GearNetworkSection/>`）。Chest/Tank は1行委譲、Generic は grid+fluids で、固有UIを持つのは FilterSplitter のみ（=8/9が合成のみ）。しかも各セクションは GearSection.tsx:7 `if (!data.gear) return null`、NetworkSections.tsx:8,26 の様に自前 null ガード済みで、GeneratorInventory は既にこれを利用して ElectricGenerator/FuelGearGenerator/SimpleGearGenerator の3 blockType を1ビューで捌いている（registry 22-24行）。つまり全セクションを並べた1ビューで5本が既に成立するのに、blockType 追加のたびに同型ファイルを増やす構造になっている。

**検証ノート (独立エージェントによる反証試行の結果):**

> 全evidence一致を確認: 5ビュー（Machine 8-15行/GearMachine 9-17/Miner 9-17/GearMiner 10-19/Generator 10-20）はStack+セクション列挙のみ。セクション自己ガードはGearSection.tsx:7・NetworkSections.tsx:8,26に加えMachineSection.tsx:16・MinerSection.tsx:11・GeneratorSection.tsx:8でも確認。registry 22-24行でGeneratorInventoryが3 blockTypeを1ビューで捌く前例も実在。9ビュー中固有UIはFilterSplitterのみ（Chest:7行・Tank:7行は1行委譲、Genericはgrid+fluids）で8/9合成の主張も正確。views/は既に9ファイルで規約の10ファイル上限に接触しており、blockType追加ごとにファイルが増える構造指摘は妥当。補足: 統合時はMachineSectionがitemSlotsをslotLayoutで自前描画するためBlockItemGridを!data.machine条件にする必要があり「そのまま成立」はやや言い過ぎだが、指摘の本筋は揺るがない。共通化上限のmajorで妥当。

**推奨対応**: セクションが自己ガードする設計を活かし、BlockItemGrid＋全セクション＋FluidSlotRow を順に並べる単一の SectionStackView に Machine/GearMachine/Miner/GearMiner/Generator/Generic を統合する（testId は blockType から導出）。registry は FilterSplitter のような真に固有なUIだけを個別登録する場所に戻す。

### C2. MachineRecipeViewがItemSlotの内部CSSモジュールを直importする境界違反

- **severity**: major
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: css, shared-components, boundaries (独立指摘 3 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/features/recipe/views/MachineRecipeView.tsx`
  - `moorestech_web/webui/src/shared/ui/ItemSlot/style.module.css`

**根拠 (レビューエージェントの実測):**

- *css*: MachineRecipeView.tsx:3 `import slotStyles from "@/shared/ui/ItemSlot/style.module.css";` で他コンポーネントの実装詳細である CSS モジュールへ直接依存し、33-34 行で `<div className={slotStyles.slot}><BlockIcon ... className={slotStyles.icon} /></div>` と手組みの偽スロットを構築している。ItemSlot 本体は `data-filled` / `data-selected` 属性(style.module.css:17-27)で二層の見た目を切り替えるが、この偽スロットは属性を持たないため常に「空セル」の暗色描画になり、ItemSlot 側のクラス改名・構造変更(例: filled 表現の変更)が型チェックに掛からず無言でここを壊す。

- *shared-components*: MachineRecipeView.tsx:3 `import slotStyles from "@/shared/ui/ItemSlot/style.module.css";`、33-35行で `<div className={slotStyles.slot}><BlockIcon blockId={recipe.blockId} ... className={slotStyles.icon} /></div>` とコンポーネントを迂回してスロット枠を手組みしている。ItemSlot 本体は index.tsx:38 で `data-filled={hasItem ? "true" : undefined}` を付与し、style.module.css:17-19 の `.slot[data-filled="true"] { background-color: rgb(236 237 239 / 90%); }` で「中身あり=明色塗り」になるが、この手組み div には data-filled が無いため機械アイコンだけが空セル用の暗背景で描かれる（現時点で実害のある見た目乖離）。今後 ItemSlot の DOM/クラス構成を変えるとここだけ静かに壊れる。

- *boundaries*: MachineRecipeView.tsx:3 `import slotStyles from "@/shared/ui/ItemSlot/style.module.css";` の上で、:33-34 `<div className={slotStyles.slot}><BlockIcon ... className={slotStyles.icon} /></div>` と ItemSlot の内部クラスを流用してブロック用の擬似スロットを組んでいる。ItemSlot/style.module.css は ItemSlot コンポーネントの内部実装（data-filled/data-catalog/data-selected 属性とセットで意味を成す）であり、実際に視覚パリティ作業で背景色系が変更されている最中のファイル。擬似スロット側は data-filled 等の属性を持たないため、ItemSlot 本体への uGUI 一致修正（例: 所持セルの白面化）がこの箇所には反映されず、レシピ画面のブロック枠だけ「空セル見た目」のまま乖離していく。

**検証ノート (独立エージェントによる反証試行の結果):**

> 全evidence正確。MachineRecipeView.tsx:3の越境import、33-35行の手組み偽スロット(div className={slotStyles.slot}+BlockIcon)を確認。ItemSlot本体はindex.tsx:44-46でdata-selected/data-filled/data-catalogを付与し、style.module.css:23-25の.slot[data-filled=true]{background:rgb(254 254 254)}で所持セルを白面化する契約——偽スロットは属性なしのため機械アイコンが空セル用暗背景rgb(58 48 51)(9-11行)に描かれる乖離も現物確認。grepで全module.css import 22件中、越境はこの1件のみという主張も正確に一致。直近コミット(スロット背景の白面化等)でItemSlot側CSSが活発に変更中の領域であり、data属性契約を迂回した結合は型チェック外で無言破壊されるという構造リスクは具体的。1箇所だが境界規律の唯一の破れとして潰す価値が高く、major妥当。ItemSlotがitemId専用でブロックアイコンを受けられないというAPI欠落が根因である点も追認。

**推奨対応**: shared/ui に BlockSlot(または ItemSlot の icon 差し替え受け入れ)を追加し、MachineRecipeView はそれを使う。CSS モジュールの越境 import は他に前例が無く(全 22 import 中これ 1 件)、この 1 件を潰せば「module.css は同居コンポーネント専用」という境界が守られる。

**鮮度更新 (2026-07-18)**: MachineRecipeView.tsx は完全無変更で越境 import と手組み偽スロットはそのまま残存。一方 ItemSlot 側は filled 面 `rgb(254 254 254)`、非 filled 面 `rgb(50 52 67 / 50%)` へ再定義され、data-filled 属性を持たない偽スロットとの視覚乖離は監査時より拡大した。境界違反を放置したままパリティ作業が ItemSlot 側だけ進んだ結果、乖離という実害が伸びている。

---

## D. CSS 負債 (minor)

個別には小さいが、B1〜B4 のトークン化・分割とまとめて処理すると効率が良いもの。

### D1. recipeTreeButtonとcraftButtonの宣言重複

- **severity**: minor (レビュー時 major から検証で訂正)
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: css
- **対象ファイル**:
  - `moorestech_web/webui/src/features/recipe/RecipeViewer.module.css`

**根拠 (レビューエージェントの実測):**

- *css*: RecipeViewer.module.css:36-48 `.recipeTreeButton { width: 106px; height: 21px; min-height: 21px; border-radius: 0; background: linear-gradient(180deg, #42d5f3 0%, #1385dc 48%, #13cff0 100%); color: #fff; font-weight: 700; }` と、同 143-159 `.craftButton { width: 106px; min-height: 21px; height: 21px; border-radius: 0; background: linear-gradient(180deg, #48a0e8 0%, #b1bedf 38%, #50aff5 52%, #168ce5 100%); color: #fff; font-weight: 700; }` は寸法(106x21)・radius 0・白太字が完全一致で、差分はグラデ配色と letter-spacing のみ。recipeTreeButton 側には hover/disabled 状態(161-171 行相当)が未定義で、片方だけ状態進化する分裂が既に始まっている。

**検証ノート (独立エージェントによる反証試行の結果):**

> 重複自体は実在: 106x21/min-height21/radius0/#fff/weight700の6宣言が36-48行と143-159行で一致、recipeTreeButtonにhover/disabled未定義(craftButtonのみ161-171行に存在)も正確。ただしevidenceの「差分はグラデ配色とletter-spacingのみ」は不正確で、craftButtonは他にtext-transform:uppercase(157行)・inset box-shadow(158行)・padding0(151行)・margin-bottom30px(148行)を持ち、recipeTreeButtonのみfont-size:10px(47行)を持つなど差分は6項目以上。グラデも3段シアン系vs4段グロス系(#b1bedf中間帯)で明確に別スキンであり、uGUI正本で別デザインの可能性が高い。さらにItemHeader.tsx:14-16のコメントで recipeTreeButton は「レシピツリー連携前の見た目確認用プレースホルダ」(onClick={()=>{}})と明記されており、disabled状態が未定義なのは「分裂の始まり」ではなく未実装機能の当然の状態。同一ファイル内6宣言の重複は小規模な共通化機会でminorが妥当。

**推奨対応**: `.blueActionButton` 基底クラス(寸法・radius・文字・hover/disabled)を切り出し、2 つのボタンはグラデ配色の差分クラスだけを重ねる。uGUI 正本で 2 つのグラデが本当に別配色なのかも突き合わせ、同一なら 1 クラスに統合する。

**鮮度更新 (2026-07-18)**: 検証ノートの反証根拠（「グラデが3段シアン系vs4段グロス系で別スキン」）が現状不成立になった。両ボタンは ItemHeader.module.css:96(recipeTreeButton) と RecipeBox.module.css:135(craftButton) へ別ファイルへ移動した上で、寸法(107.6px×19px/106.8px×19px)・border-radius:0・color:#fff・font-weight:500 に加えグラデーションまで完全同一の `linear-gradient(90deg, #0072d8 0%, #008fed 50%, #04d8ec 100%)` に収束している。「同一ファイル内の重複」という監査時の骨格は分割で消えたが、完全同一スキンの2実装が別ファイルに分かれた形になっただけで、共通化の必要性はむしろ確定した。

### D2. --slot-sizeトークンのバイパスとfallback不一致

- **severity**: minor (レビュー時 major から検証で訂正)
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: css
- **対象ファイル**:
  - `moorestech_web/webui/src/shared/ui/ItemSlot/style.module.css`
  - `moorestech_web/webui/src/features/inventory/InventoryPanel/GrabOverlay.module.css`
  - `moorestech_web/webui/src/features/inventory/InventoryPanel/GrabOverlay.tsx`
  - `moorestech_web/webui/src/shared/ui/FluidSlot/style.module.css`
  - `moorestech_web/webui/src/app/index.css`

**根拠 (レビューエージェントの実測):**

- *css*: スロット寸法 48px(3rem) が 4 表現に分裂している。(1) index.css:6 `--slot-size: 3rem`(正)。(2) ItemSlot/style.module.css:7-8 `width: var(--slot-size, 2rem)` の fallback 2rem は :root の 3rem と食い違い、:root 宣言が消えた瞬間に別サイズで描画される死んだ嘘の既定値。(3) GrabOverlay.module.css:4 で `--slot-size: 3rem` を :root と同値で再宣言した上、8-9 行で `width: 3rem; height: 3rem;` と生値も直書き。(4) GrabOverlay.tsx:41 `style={{ left: mousePos.x - 24, top: mousePos.y - 24 }}` の -24 は 3rem の半分の px 直書きで、--slot-size 変更時に追随しない。FluidSlot/style.module.css:5-6 も `width: 3rem; height: 3rem;` 直書き(コメントで固定と明言はある)。

**検証ノート (独立エージェントによる反証試行の結果):**

> 事実関係は全て一致（index.css:6の3rem、ItemSlot/style.module.css:7-8のfallback 2rem不一致、GrabOverlay.module.css:4再宣言+8-9直書き、GrabOverlay.tsx:41の-24、FluidSlot/style.module.css:5-6直書き）。ただし指摘が見落とした反証: HotbarPanel/style.module.css:22が--slot-size:3.15625remをセル単位で上書きしており、コンテキスト毎のトークン上書きがこのコードベースの確立パターン。GrabOverlayの再宣言はこのパターンに準拠した正当な書き方で「バイパス」ではない。GrabOverlayは同一12行ファイル+隣接tsx内で自己完結し1-2行コメントで「48px固定」と明言、FluidSlotも1行目で固定と明言済み。実害が残るのはItemSlotのfallback 2remのみで、これはindex.cssが読み込まれる限り発火しない死値。majorは過大でminorが妥当。

**推奨対応**: ItemSlot の fallback を 3rem に揃える(または fallback 自体を削除)、GrabOverlay は width/height を var(--slot-size) 参照にして再宣言と -24 直書きを `calc`/`transform: translate(-50%, -50%)` に置換、FluidSlot も意図的固定なら `/* --slot-size 非追随 */` の根拠コメントを添えて var 参照 + 局所上書きへ寄せる。

### D3. 未使用セレクタ.viewerCol/.recipeItems

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: css
- **対象ファイル**:
  - `moorestech_web/webui/src/app/App.module.css`
  - `moorestech_web/webui/src/features/recipe/RecipeViewer.module.css`
  - `moorestech_web/webui/src/features/recipe/RecipeViewer.tsx`

**根拠 (レビューエージェントの実測):**

- *css*: 全 tsx/ts を grep した結果、App.module.css:43-46 `.viewerCol { grid-area: viewer; align-self: start; }` と RecipeViewer.module.css:119-122 `.recipeItems { width: 100%; justify-content: center; }` はどこからも className 参照されていない(定義行のみヒット)。viewerCol の意図(viewer 領域+上寄せ)は RecipeViewer.tsx:24 のインライン `style={{ alignSelf: "start", ... }}` と `gridArea="viewer"` prop で再実装されており、死んだ CSS と生きたインラインの二重管理になっている。

**検証ノート (独立エージェントによる反証試行の結果):**

> grepで再確認: viewerColはApp.module.css:43の定義のみ、recipeItemsはRecipeViewer.module.css:119の定義のみで、src全域にclassName参照ゼロ。viewerColの意図（grid-area:viewer+align-self:start）はRecipeViewer.tsx:24のgridArea prop+インラインalignSelf:"start"で再実装されている点も一致。追加確認: RecipeViewer.module.cssはwc -lでちょうど200行であり、プロジェクトの200行上限に張り付いているため.recipeItems 4行削除の即効性という補足も正確。minor妥当。

**推奨対応**: 両セレクタを削除する。viewerCol は削除ではなくインライン style をこのクラスへ戻す選択も可(パネル幅 336 の finding と併せて CSS 側へ集約するならそちらが良い)。RecipeViewer.module.css は 200 行上限のため 4 行の削除に即効性がある。

**鮮度更新 (2026-07-18)**: `.recipeItems` は RecipeViewer.module.css の分割（H1参照）の過程で削除済みで解消した。`.viewerCol`(App.module.css:43) は依然として定義のみで参照ゼロのまま残存している。

### D4. CSSファイル命名・ディレクトリ構成・export形式の流儀混在

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: css, abstraction-consistency (独立指摘 2 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/features/recipe/RecipeViewer.module.css`
  - `moorestech_web/webui/src/features/inventory/InventoryPanel/GrabOverlay.module.css`
  - `moorestech_web/webui/src/shared/ui/ItemIcon.module.css`
  - `moorestech_web/webui/src/app/App.module.css`
  - `moorestech_web/webui/src/features/inventory/InventoryPanel/GrabOverlay.tsx`
  - `moorestech_web/webui/src/features/blockInventory/style.module.css`
  - `moorestech_web/webui/src/shared/ui/ItemIcon.tsx`
  - `moorestech_web/webui/e2e/mock-host/tests/httpHandler.test.ts`

**根拠 (レビューエージェントの実測):**

- *css*: 16 個の CSS のうち 12 個はディレクトリ同居の `style.module.css`(blockInventory, buildMenu, HotbarPanel, progress, research, toast, FluidSlot, GamePanel, ItemSlot, ProgressArrow, SlotGrid + index.css を除く)だが、4 個は `App.module.css` / `GrabOverlay.module.css` / `ItemIcon.module.css` / `RecipeViewer.module.css` とコンポーネント名方式。しかも RecipeViewer.module.css は RecipeViewer.tsx ではなく views/ 配下 4 ファイルが使い、ItemIcon.module.css は ItemIcon.tsx と BlockIcon.tsx の 2 つが共有しており、名前が実際の対応関係を裏切っている。

- *abstraction-consistency*: 構成方式が3通り並存: (a) ディレクトリ+index.tsx+style.module.css（InventoryPanel/, HotbarPanel/, shared/ui の6部品）、(b) フラット+汎用名 style.module.css（blockInventory/BlockInventoryPanel.tsx、buildMenu, research, progress, toast）、(c) フラット+コンポーネント名 css（recipe/RecipeViewer.tsx+RecipeViewer.module.css、GrabOverlay.module.css、ItemIcon.module.css）。GrabOverlay.tsx は InventoryPanel/ 配下にあるのに inventory/index.ts:3 で InventoryPanel と対等の部品として export され、置き場所と公開単位が食い違う。export も default（BlockInventoryPanel 等7つ）と named（BuildMenuPanel/ModalHost/ProgressBar）が混在。テスト配置も src は colocation だが e2e/mock-host だけ tests/ サブディレクトリ方式。

**検証ノート (独立エージェントによる反証試行の結果):**

> 全構造的主張を検証し一致: RecipeViewer.module.cssの利用者はviews/配下4ファイル（CraftProgressArrow/ItemHeader/CraftRecipeView/RecipeContent）でRecipeViewer.tsx自身は不使用、ItemIcon.module.cssはItemIcon.tsx+BlockIcon.tsxの2ファイル共有、inventory/index.ts:3がInventoryPanel/配下のGrabOverlayを対等exportしている点、named export（BuildMenuPanel.tsx:10/ModalHost.tsx:9/ProgressBar.tsx:8）とdefault混在、e2e/mock-host/testsサブディレクトリ方式とsrcのcolocation（*.test.ts同居20件）の食い違いも全て実在。軽微な誤り: style.module.cssは12個でなく11個（find実測、指摘自身の列挙も11件で総数だけ誤記）だが結論に影響なし。minor妥当。

**推奨対応**: 「1 コンポーネントディレクトリ = style.module.css」か「1 tsx = 同名 .module.css」のどちらかに統一する。RecipeViewer.module.css の views/ 分割(別 finding)と ItemIcon.module.css の共有解消(IconFallback.module.css 等へ改名)を先に行えば、残りは改名だけで揃う。

**鮮度更新 (2026-07-18)**: RecipeViewer.module.css が RecipeViewer.tsx ではなく views/ 側に使われるという名前不整合は分割（H1参照）で解消した。しかしその副作用として ItemHeader.module.css / RecipeBox.module.css / CraftProgressArrow.module.css という「1 tsx = 同名 .module.css」パターンの3件と、recipe/直下の ItemListPanel.module.css が新規に追加され、構成方式の流儀の数(3通り)と該当ファイル数はむしろ増加している。

### D5. カラー表記の新旧記法混在

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: css
- **対象ファイル**:
  - `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css`
  - `moorestech_web/webui/src/shared/ui/ItemSlot/style.module.css`
  - `moorestech_web/webui/src/features/inventory/HotbarPanel/style.module.css`

**根拠 (レビューエージェントの実測):**

- *css*: 全 CSS で新記法 29 箇所・旧記法 9 箇所が混在。同一ファイル内の例: GamePanel/style.module.css:22 `rgb(3 5 8 / 88%)` に対し 73 行 `rgba(0, 0, 0, 0.6)`。ItemSlot/style.module.css:9 `rgb(150 153 158 / 60%)` に対し 51 行 `rgba(255, 255, 255, 0.85)`。HotbarPanel/style.module.css:33 `rgb(112 110 102 / 85%)` に対し 41 行 `rgba(0, 0, 0, 0.8)`。grep でのアルファ値横断検索(例: `/ 80%` と `0.8`)が二重に必要になり、パリティ調整時の同値検出を妨げる。

**検証ノート (独立エージェントによる反証試行の結果):**

> 混在は実在。実測で新記法31箇所・旧記法9箇所（旧: App.module.css:81, HotbarPanel:41, buildMenu:10/25/26/33, FluidSlot:28, ItemSlot:57, GamePanel:74）で、同一ファイル内混在も3ファイル全てで確認（GamePanel: 新22/40/41/42等+旧74、ItemSlot: 新9/24/32+旧57、HotbarPanel: 新33/35+旧41）。ただし引用値に微妙なズレあり: GamePanel:22の実体はrgb(67 54 44)でrgb(3 5 8 / 88%)は存在せず（近似値rgb(3 7 11 / 78%)は42行）、ItemSlot:9は60%でなく72%、旧記法の行番号も73→74・51→57とドリフト。指摘の本質（grep二重検索の負担）とminor判定は妥当。

**推奨対応**: 新記法 `rgb(r g b / a%)` に全面統一する(既に 29:9 で多数派)。stylelint の color-function-notation ルールを devDependencies に足せば以後は機械強制できる。

### D6. GamePanel装飾罫線のnth-child DOM順序依存

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: css
- **対象ファイル**:
  - `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css`
  - `moorestech_web/webui/src/shared/ui/GamePanel/index.tsx`

**根拠 (レビューエージェントの実測):**

- *css*: style.module.css:89 `.decoLine:first-child` と 94 `.decoLine:nth-child(3)` は、index.tsx:24-32 の「decoLine(1番目) → header(2番目) → decoLine(3番目) → body(4番目)」という兄弟順序に暗黙依存して上罫線/下罫線の配色を切り替えている。GamePanel の JSX に要素を 1 つ挿入(例: パネル右上の閉じるボタンや装飾)しただけで nth-child(3) が別要素を指し、罫線の明暗が無言で入れ替わる。title 未指定時(RecipeViewer.tsx:24 の craft バリアント)は罫線自体が描画されないため今は破綻していないが、構造とスタイルの結合が不可視。

**検証ノート (独立エージェントによる反証試行の結果):**

> 実セレクタはstyle.module.css:90 (.decoLine:first-child) と95 (.decoLine:nth-child(3))で指摘の89/94から1行ズレのみ。index.tsx:22-32でフラグメント展開後の兄弟順序がdecoLine(1)→header(2)→decoLine(3)→body(4)となる構造依存を確認。title未指定のcraftバリアント使用はRecipeViewer.tsx:24で確認、title付き使用はItemListPanel.tsx:27とInventoryPanel/index.tsx:22の2箇所で位置擬似クラスが実際に効いている。要素挿入で上下罫線の明暗が無言で入れ替わる指摘は正確。単一コンポーネント内で完結する結合なのでminorが妥当。

**推奨対応**: `.decoLineTop` / `.decoLineBottom` の明示クラスに分け、index.tsx 側で `className={styles.decoLineTop}` と書き分ける。位置擬似クラスによる意味の判別を排除すれば、GamePanel の DOM 変更が罫線配色へ波及しなくなる。


---

## E. 共通コンポーネント化 (minor)

同型UIの手書き重複と shared/ui の死にAPI。C1・B4 の統合作業のついでに回収できるものが多い。

### E1. 素材充足の視覚表現がfeatureごとに別ハック（ItemSlot API不足）

- **severity**: minor (レビュー時 major から検証で訂正)
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: shared-components
- **対象ファイル**:
  - `moorestech_web/webui/src/features/recipe/views/CraftRecipeView.tsx`
  - `moorestech_web/webui/src/features/research/ResearchNodeCard.tsx`
  - `moorestech_web/webui/src/shared/ui/ItemSlot/index.tsx`

**根拠 (レビューエージェントの実測):**

- *shared-components*: 同じ「所持数 vs 要求数」の充足表示が、CraftRecipeView.tsx:47 では `<Box key={i} opacity={(counts.get(r.itemId) ?? 0) >= r.count ? 1 : 0.4}>` のラッパで不足を40%透過に、ResearchNodeCard.tsx:36 では `<ItemSlot ... selected={isItemSufficient(node, c.itemId, c.count, owned)} />` とホットバー選択用のシアン発光（ItemSlot/style.module.css:23-27）を流用して充足を強調、と正反対の別表現になっている。さらに ResearchNodeCard.tsx:34-38 は `名前 x個数` のツールチップを出すために ItemSlot 内蔵 Tooltip（name prop）を使えず `<Tooltip><div><ItemSlot .../></div></Tooltip>` の二重ラップで回避している。ItemSlot に充足状態・ツールチップ内容の口が無いことが原因。

**検証ノート (独立エージェントによる反証試行の結果):**

> 事実関係は全て正確（CraftRecipeView.tsx:47のopacity Box、ResearchNodeCard.tsx:34-38のTooltip二重ラップ+36行のselected流用、ItemSlotに充足/ツールチップpropなし。selectedのCSSは実際はItemSlot/style.module.css:29-33）。ただし「同じ充足表示が正反対の別表現」という統合前提が過大: craft側はCraftRecipeView.tsx:45-46で「不足を40%透過＝uGUI準拠」と明記、research側はresearchLogic.ts:49-56で「充足をハイライト（completedは非ハイライト）」とuGUI挙動から導出しており、両者は表示したい意味自体が異なる（不足減光 vs 充足強調）意図的な別表現の可能性が高く、見た目の一元化はパリティを壊しうる。実害として残るのはselected流用によるe2e契約（style.module.css:1-2に明記、hotbar.spec.tsが参照）の意味汚染とTooltip二重ラップだが、呼び出し2箇所に閉じており現時点で破綻なし（hotbar.spec.tsはhotbarスコープでクエリ）。minorが妥当。

**推奨対応**: ItemSlot に `insufficient`（または `sufficiencyState`）prop を追加して減光/強調の見た目を data 属性+CSS で一元化し、両 feature の Box/selected ハックを置き換える。ツールチップは label を上書きできる prop（name の代わりに ReactNode 許容）にして二重ラップを不要にする。

### E2. BuildMenuSlotがItemSlotのジェスチャ配線とスロットCSSを再実装

- **severity**: minor (レビュー時 major から検証で訂正)
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: shared-components
- **対象ファイル**:
  - `moorestech_web/webui/src/features/buildMenu/BuildMenuSlot.tsx`
  - `moorestech_web/webui/src/features/buildMenu/style.module.css`
  - `moorestech_web/webui/src/shared/ui/ItemSlot/index.tsx`

**根拠 (レビューエージェントの実測):**

- *shared-components*: BuildMenuSlot.tsx:17-21 の `const onMouseDown = (e: MouseEvent) => { e.preventDefault(); if (e.button === 0) onLeftClick(); if (e.button === 2) onRightClick?.(); }` と 29行の `onContextMenu={(e) => e.preventDefault()}` は、ItemSlot/index.tsx:22-26 `if (e.button === 0) onLeftDown?.(e.shiftKey); if (e.button === 2) onRightDown?.();` と 41行の同処理の逐語的コピー。スロット枠も buildMenu/style.module.css:19-30 で 64px・radius 4px・hover 白veilを独自定義しており、ItemSlot の uGUI パリティ枠（2px 枠・radius 0・--slot-size 可変）と別系統。Tooltip ラップ構造（BuildMenuSlot.tsx:24 vs ItemSlot:33）も同型。

**検証ノート (独立エージェントによる反証試行の結果):**

> コピーの事実は正確（BuildMenuSlot.tsx:17-21と29行 vs ItemSlot/index.tsx:25-29と49行。指摘の22-26/41は実際は25-29/49で行ズレ）。CSS独自定義（buildMenu/style.module.css:19-34の64px/radius4px/hover veil）とTooltipラップ同型（BuildMenuSlot:24 vs ItemSlot:40）も確認。ただし重複はマウスダウン振り分け約5行+contextMenu抑止1行の1ファイルのみで、両者の契約は実質的に異なる（BuildMenuSlotはiconUrl画像/テキストラベル・shiftKey/doubleClick/count/data属性なし、ItemSlotはitemId→ItemIcon・data-filled/selected/catalog）。ビルドメニューはfixedオーバーレイのメニューUIでuGUIパリティ対象のスロット枠と視覚が異なるのは意図的である可能性が高い。機能追加のたびに壊れる構造ではなく2コンポーネント間の小規模重複に留まるためmajorは過大、minorが妥当。

**推奨対応**: マウスダウン振り分け＋contextMenu 抑止を useSlotMouse フックまたは SlotFrame（children 受け）として shared/ui へ抽出し、BuildMenuSlot はコンテンツ（iconUrl 画像 or テキストラベル）だけを差し込む形にする。枠CSSも SlotFrame 側に寄せ、ビルドメニューの視覚も uGUI パリティ調整の対象に入るようにする。

### E3. ItemIconとBlockIconの実質重複コンポーネント

- **severity**: minor (レビュー時 major から検証で訂正)
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: boundaries, abstraction-consistency (独立指摘 2 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/shared/ui/ItemIcon.tsx`
  - `moorestech_web/webui/src/shared/ui/BlockIcon.tsx`

**根拠 (レビューエージェントの実測):**

- *boundaries*: ItemIcon.tsx:12-30 と BlockIcon.tsx:12-28 は「img描画 + onError で erroredId を記録して #id フォールバック」という完全に同型の実装で、差分は URL（`/api/icons/${itemId}.png` vs `/api/block-icons/${blockId}.png`）と prop 名のみ。BlockIcon.tsx:2 は `import styles from "./ItemIcon.module.css";` と ItemIcon の CSS module を借用しており、独立コンポーネントとしての体裁は既に崩れている。フォールバック挙動を変える修正が常に2箇所同時変更になる。

- *abstraction-consistency*: ItemIcon.tsx:12-29 と BlockIcon.tsx:12-27 は、URL prefix（`/api/icons/${itemId}.png` vs `/api/block-icons/${blockId}.png`）と prop 名（itemId/erroredItemId vs blockId/erroredBlockId）以外、エラー状態管理・`#${id}` フォールバック・draggable={false}・className 合成まで完全に同一。BlockIcon.tsx:2 が `import styles from "./ItemIcon.module.css"` と他コンポーネントの CSS を直接共有している時点で、実体は1部品の分裂。フォールバック挙動を変える修正（例: ロード中プレースホルダ追加）は必ず2ファイル同時修正になる。

**検証ノート (独立エージェントによる反証試行の結果):**

> evidenceは正確: ItemIcon.tsx:12-30とBlockIcon.tsx:12-27はURL(/api/icons/ vs /api/block-icons/)とprop名・デフォルトalt以外完全同型、BlockIcon.tsx:2のimport styles from "./ItemIcon.module.css"によるCSS借用も確認。ただしmajorは過大。実効差分は各ファイル17行程度の2ファイル限定の重複で、両者はshared/ui直下に隣接しCSS共有が結合を可視化しているため無自覚な片側修正のリスクは低い。CSSはすでに共有されているためスタイル変更は1箇所で済み、二重修正が必要なのはJSのフォールバックロジックのみ。「機能追加のたびに壊れる構造」には該当せず、共通化の機会としてminorが妥当。統合提案(GameIcon/IconWithFallback)自体は有効。

**推奨対応**: `GameIcon({ kind: "item" | "block", id, alt, className })` に統合し URL を kind で切り替えるか、内部ベース部品 `IconWithFallback({ src, fallbackLabel })` を作って ItemIcon/BlockIcon を各3行のラッパにする。CSS も icon 用の単独モジュールに改名して共有を明示する。

### E4. 入力→出力矢印と進捗バー構造の3重実装

- **severity**: minor
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: shared-components
- **対象ファイル**:
  - `moorestech_web/webui/src/shared/ui/ProgressArrow/index.tsx`
  - `moorestech_web/webui/src/features/recipe/views/CraftProgressArrow.tsx`
  - `moorestech_web/webui/src/features/recipe/views/MachineRecipeView.tsx`
  - `moorestech_web/webui/src/features/recipe/RecipeViewer.module.css`

**根拠 (レビューエージェントの実測):**

- *shared-components*: 同じ「入力→出力」意味論に、(1) shared ProgressArrow（index.tsx:9-11 track+fill、width %）、(2) CraftProgressArrow.tsx:8-13 の `➔` グリフ+RecipeViewer.module.css:189-200 craftArrowTrack/craftArrowFill（clamp01×width% のfillパターンを再実装）、(3) MachineRecipeView.tsx:31,38 の素の `<Text c="dimmed" mx="xs">→</Text>` ×2、の3実装が並存。(2)のtrack/fill構造とclamp01利用は(1)と同型のコピーで、(3)はパリティ調整の対象にすらならないプレーンテキスト。

**検証ノート (独立エージェントによる反証試行の結果):**

> 行番号は全て正確（ProgressArrow/index.tsx:9-11、CraftProgressArrow.tsx:8-13、RecipeViewer.module.css:189-200、MachineRecipeView.tsx:31,38）。ただし「同じ意味論の3重実装」は範囲誇張。(1)はuGUI ProgressArrowView準拠の4rem枠付きバー、(2)は「uGUIの白矢印に合わせる」とコメント明記された別意匠（グリフ+1.9rem×3pxの細バー）で、視覚パリティ目的で別々のuGUI正本を写している。(3)MachineRecipeView.tsx:31,38の→は進捗を持たない静的セパレータであり進捗バー構造の実装ではない。実際に重複しているのはclamp01×width%のfillイディオム約6行のみ（track/fillのCSS値は寸法・色とも別物）。共通化自体は選択肢だが、パリティ対象が異なる以上variant結合は逆に結合度を上げるリスクもある。minor据え置きで指摘の骨子（micro-pattern重複）だけ有効。

**推奨対応**: ProgressArrow に variant（bar / glyph+bar）を追加するか RecipeArrow を shared/ui に置き、CraftProgressArrow の track/fill 再実装を吸収、MachineRecipeView の → テキストも同コンポーネントに置き換える。

**鮮度更新 (2026-07-18)**: craftArrowTrack/Fill は RecipeViewer.module.css の分割（H1参照）で新設 CraftProgressArrow.module.css:27,35 へ移設され、旧引用箇所(RecipeViewer.module.css:189-200)は消滅した。ファイル分割による行番号変化のみで、3実装並存という結論そのものは不変。

### E5. HotbarPanelがSlotGridを使わずflex手書き（SlotGridの死にAPI化）

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: shared-components
- **対象ファイル**:
  - `moorestech_web/webui/src/features/inventory/HotbarPanel/index.tsx`
  - `moorestech_web/webui/src/features/inventory/HotbarPanel/style.module.css`
  - `moorestech_web/webui/src/shared/ui/SlotGrid/index.tsx`

**根拠 (レビューエージェントの実測):**

- *shared-components*: HotbarPanel/style.module.css:12-15 `.hotbarFrame { display: flex; gap: 5px; }` は SlotGrid/style.module.css:5-6 の `column-gap: 0.3125rem`（=5px）と同値の再実装で、HotbarPanel/index.tsx:51 は手書き div に `data-testid="hotbar-grid"` と onWheel を配線している。一方 SlotGrid/index.tsx:10 の `onWheel?: (e: WheelEvent<HTMLDivElement>) => void;` と className prop は grep 上利用者ゼロ（ホットバー向けに用意されたまま使われていない）。番号タブ付き .cell ラッパは SlotGrid の children としてそのまま渡せる構造。

**検証ノート (独立エージェントによる反証試行の結果):**

> 全て実コードと一致。HotbarPanel/style.module.css:12-15の.hotbarFrame{display:flex;gap:5px}はSlotGrid/style.module.cssのcolumn-gap:0.3125rem(=5px)と同値の二重管理、index.tsx:51で手書きdivにdata-testid="hotbar-grid"とonWheelを配線。grepでSlotGridの全利用5箇所（BlockItemGrid:15、MachineSection:37,39,41、ItemListPanel:30、InventoryPanel:23、BuildMenuPanel:26）を確認し、いずれもcols/testIdのみでonWheel(index.tsx:10)とclassName(index.tsx:11)の利用者はゼロ。ホットバー用に用意されたpropが使われないまま残る死にAPI化の指摘は正確。番号タブ付き.cell(style.module.css:19-23)はSlotGridのchildrenとして渡せる構造で載せ替えも可能。minorが妥当。

**推奨対応**: HotbarPanel を `SlotGrid cols={hotbarSlots.length} onWheel={onHotbarWheel} testId="hotbar-grid"` に載せ替えて gap 値の二重管理を解消するか、載せ替えない判断をするなら SlotGrid から誰も使わない onWheel/className を削除して API を実態に合わせる。

**鮮度更新 (2026-07-18)**: SlotGrid/style.module.css の column-gap 既定値が `var(--slot-grid-gap, 0.425rem)`(6.8px) へ変更され、HotbarPanel の `gap: 5px`(style.module.css:17) との「同値の再実装」という evidence は無効化された。ただし SlotGrid を使わず flex で独自実装している構造自体、また onWheel/className が全7呼び出しで未使用という構造指摘は不変で、根拠の一部が置き換わっただけで結論は揺らいでいない。

### E6. 現在値/要求値・不足時赤のステータステキスト3重手書き

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: shared-components
- **対象ファイル**:
  - `moorestech_web/webui/src/features/blockInventory/details/GearSection.tsx`
  - `moorestech_web/webui/src/features/blockInventory/details/PowerRateText.tsx`
  - `moorestech_web/webui/src/features/blockInventory/details/NetworkSections.tsx`

**根拠 (レビューエージェントの実測):**

- *shared-components*: GearSection.tsx:12-17 が `c={torqueLack ? "red.5" : "dark.1"}` / `c={rpmLack ? "red.5" : "dark.1"}` の2行、PowerRateText.tsx:17 が `c={rate < 1 ? "red.5" : "dark.1"}` と、「A / B 形式＋不足時 red.5」の Text を3回別実装。NetworkSections.tsx:31-33 の `供給 {…} / 要求 {…}` は同じ A/B 形式なのに不足赤表示が無く、パターン不統一が既に発生している。

**検証ノート (独立エージェントによる反証試行の結果):**

> 全て実コードと一致。GearSection.tsx:12,15でc={lack?"red.5":"dark.1"}が2行、PowerRateText.tsx:17でc={rate<1?"red.5":"dark.1"}、NetworkSections.tsx:31-33の供給/要求はc="dark.1"固定で不足赤なし。追加発見: ElectricNetworkSection(NetworkSections.tsx:15-17)も「発電/需要」のA/B形式でpowerRateを持ちながら不足赤なしで、不統一は指摘より1箇所多い。ただしGearNetworkSectionは停止理由を赤表示(34行)する別系統の強調を持ち、各コンポーネントが異なるuGUI正本（SetGearText/CommonMachineBlockStateDetail/ElectricNetworkInfoView）準拠とコメント明記されているため、赤付与の是非をuGUI照合で決めるという推奨のヘッジは適切。共通化機会としてminorが妥当。

**推奨対応**: details 配下に LackHighlightText（label, current, required, 不足判定）を1つ切り出し、Gear の2行・PowerRateText・GearNetwork の供給/要求行を統一する。GearNetwork に不足赤を付けるかは uGUI 正本と突き合わせて明示的に決める。

### E7. shared/ui内の意匠分裂（FluidSlot/ProgressArrowがMantine旧テーマのまま）

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: shared-components
- **対象ファイル**:
  - `moorestech_web/webui/src/shared/ui/FluidSlot/style.module.css`
  - `moorestech_web/webui/src/shared/ui/ProgressArrow/style.module.css`
  - `moorestech_web/webui/src/shared/ui/ItemSlot/style.module.css`

**根拠 (レビューエージェントの実測):**

- *shared-components*: ItemSlot は `border: 2px solid rgb(150 153 158 / 60%); border-radius: 0;`（style.module.css:9-10）と uGUI パリティ配色へ更新済みだが、FluidSlot/style.module.css:7-9 は `border: 1px solid var(--mantine-color-dark-4); border-radius: var(--mantine-radius-sm);`、ProgressArrow/style.module.css:7-9 も同じ Mantine 変数系のまま。FluidSlot の .amount バッジ（21-29行: 右下・bold・text-shadow）は ItemSlot の .count バッジ（36-45行）と同概念の別実装で、フォントサイズ・位置も微妙に不一致（12px/bottom:0 vs 10px/bottom:-1px）。同一パネル内（MachineSection 等）でアイテムスロットと流体スロットが並ぶため枠の太さ・角丸の差がそのまま画面に出る。

**検証ノート (独立エージェントによる反証試行の結果):**

> 骨子は実コードと一致。FluidSlot/style.module.css:7-8とProgressArrow/style.module.css:7-8は border:1px solid var(--mantine-color-dark-4)+radius-sm のMantine旧テーマのまま、ItemSlot/style.module.css:9-10はuGUIパリティ済（border:2px/radius:0）。バッジ差異（FluidSlot .amount 21-29行:12px/bottom:0 vs ItemSlot .count:10px/bottom:-1px）も実在。MachineSection.tsx:37-45でItemSlot・ProgressArrow・FluidSlotRowが同一パネルに並ぶことを確認し、差が画面に出る主張は正しい。evidenceの微細な誤り2点: ItemSlot枠の不透明度は60%でなく72%（9行目）、.countは36-45行でなく42-51行。いずれも主張の実質に影響なし。視覚パリティが現段階の目標である以上、実害のあるminor指摘として妥当。

**推奨対応**: スロット枠の色・枠線・角丸を app/index.css のカスタムプロパティ（--slot-border 等）に括り出して ItemSlot/FluidSlot で共有し、個数/量バッジも共通クラス（または SlotBadge）に統一する。FluidSlot/ProgressArrow の uGUI パリティ化を ItemSlot と同じトークンで行えるようにする。

**鮮度更新 (2026-07-18)**: ItemSlot はパリティ調整が進み、単純な border 指定から3層リング(--bevel-inset 等)+ box-shadow による表現へ大幅に進化した(style.module.css:1-20)。一方 FluidSlot(style.module.css:5-8)・ProgressArrow(style.module.css:3-9)は `border: 1px solid var(--mantine-color-dark-4); border-radius: var(--mantine-radius-sm);` のまま完全無変更で、Mantine 旧テーマとの意匠分裂は監査時より拡大している。

### E8. connecting...プレースホルダの3重手書き重複

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: shared-components
- **対象ファイル**:
  - `moorestech_web/webui/src/features/inventory/InventoryPanel/index.tsx`
  - `moorestech_web/webui/src/features/recipe/RecipeViewer.tsx`
  - `moorestech_web/webui/src/features/recipe/ItemListPanel.tsx`

**根拠 (レビューエージェントの実測):**

- *shared-components*: InventoryPanel:14 `<Text size="sm" c="dimmed" style={{ gridArea: "inv" }}>connecting...</Text>`、RecipeViewer:26 `<Text size="sm" c="dimmed" m="auto">connecting...</Text>`、ItemListPanel:29 `<Text size="sm" c="dimmed">connecting...</Text>` と同一文言・同一スタイル意図の3実装（配置 prop だけバラバラ）。InventoryPanel だけ GamePanel 外に生テキストを置くため、接続待ち中は「持ち物」枠自体が消えるという表示差も生む。

**検証ノート (独立エージェントによる反証試行の結果):**

> 3箇所の重複は実在。InventoryPanel/index.tsx:14とRecipeViewer.tsx:26は引用どおり、ItemListPanel.tsxのみ実際は44行目（evidenceの29行は誤り、内容は一致）。grepで確認した限りconnecting...の表示はこの3箇所で全て。InventoryPanelだけ!inventory時にGamePanel外の生Textを返すため「持ち物」枠自体が消える表示差の主張も実コードで確認（RecipeViewer/ItemListPanelはGamePanel内にテキストを置く）。補足: HotbarPanel/index.tsx:43-44のコメントが「connecting...表示はInventoryPanelが担う」と依存を明記しており、文言・表示方法の一元化はこの暗黙契約の面でも意味がある。App.tsx:45-47の再接続オーバーレイとの役割分担があるため初回接続前の表示自体は意図的だが、3重手書き＋不統一の指摘はminorとして妥当。

**推奨対応**: GamePanel に loading（未受信）状態を渡すか shared/ui に ConnectingPlaceholder を置き、3箇所を統一する。文言変更やパリティ調整（uGUI のローディング表現）を1箇所で済むようにする。


---

## F. hooks・状態管理 (minor)

ストア/context/prop drilling の使い分けの乱れと、APIの防御力不足。A1・A3 と同根のものが多い。

### F1. unsubscribe後もtopicStoreに旧値が残留しstale描画・契約破り

- **severity**: minor (レビュー時 major から検証で訂正)
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: hooks-state, bridge (独立指摘 2 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/bridge/store/topicStore.ts`
  - `moorestech_web/webui/src/bridge/transport/subscriptionManager.ts`
  - `moorestech_web/webui/src/bridge/store/useTopic.ts`
  - `moorestech_web/webui/src/app/App.tsx`
  - `moorestech_web/webui/src/features/research/ResearchTreePanel.tsx`

**根拠 (レビューエージェントの実測):**

- *hooks-state*: subscriptionManager.ts:27-37 は最終 release で `this.counts.delete(topic)` と unsubscribe 送信を行うが、topicStore.ts(全36行) には topic 値を削除する API が存在せず `topics` レコードに旧値が永久残留する。useTopic.ts:6-7 は「初回 snapshot 前は null」と契約を謳うのに、再マウント時は 18行 `(s.topics[topic] ?? null)` が unsubscribe 前の旧値を即座に返す。App.tsx:88-89 で RecipeViewer/ItemListPanel は playerInventory 画面中のみマウントされるため、画面を閉じて研究解放等でサーバー側の craftRecipes/itemList が変わった後に再オープンすると、新 snapshot 到着までの間、旧データが描画され、その旧データを根拠にした操作（旧レシピの craft.execute 等）も可能になる。researchTree/buildMenu も同構造。

- *bridge*: subscriptionManager.ts:27-37 の release は counts の削除と unsubscribe 送信のみで、topicStore.ts:11-23 の `topics: Record<string, unknown>` から値を消す経路がどこにも無い。ResearchTreePanel（App.tsx:92 で screen 一致時のみマウント）や RecipeViewer/ItemListPanel（:88-89）は閉→開のたびに useTopic が再 acquire するが、useTopic.ts:18 は残置された旧値を即返すため、サーバーの subscribe→snapshot 応答（WebSocketMessageDispatcher.cs:41）が届くまでの 1RTT は閉じる前の研究状態・レシピを描画する。研究完了直後に開き直すと旧 state のノードが一瞬見える。ブロックUIは常時マウント購読（App.tsx:94）のため影響しないという偶然に依存した非一貫性でもある。

**検証ノート (独立エージェントによる反証試行の結果):**

> 構造の指摘自体は正確: topicStore.ts(全36行)に削除APIは無くsetTopic(:21)のみ、subscriptionManager.ts:27-37のreleaseはcounts削除とunsubscribe送信だけ、useTopic.ts:6-7の『初回snapshot前はnull』契約に対し:18が残留旧値を即返す。C#側も確認しWebSocketMessageDispatcher.cs:38-42でsubscribe即snapshot送信のため、stale窓はlocalhost 1RTT+snapshot生成(メインスレッドホップ)の数十ms程度。『旧データを根拠にした操作が可能』は過大: 全actionはサーバー側で自状態に対し検証・実行され(HandleActionAsync)、旧レシピのcraftは安全に失敗するだけで状態は壊れない。切断中はApp.tsx:104-113のオーバーレイが入力を遮断。実害は画面再オープン時の一瞬の旧データ表示に留まるためminor。契約コメントと実挙動の乖離、および常時マウントの偶然に依存する非一貫性の指摘は正しい。

**推奨対応**: SubscriptionManager の 1→0 遷移時に topicStore の該当キーを削除する clearTopic を呼び、「購読が無い topic の読み値は null」を機械的に保証する。閉→開の再取得ラグでちらつく topic があれば、その topic のみアプリレベルで購読を pin する明示リストに載せる。

### F2. スロット操作プランのstale closureによる誤action選択

- **severity**: minor (レビュー時 major から検証で訂正)
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: hooks-state
- **対象ファイル**:
  - `moorestech_web/webui/src/features/inventory/slotActions.ts`
  - `moorestech_web/webui/src/features/blockInventory/useBlockSlotGestures.ts`
  - `moorestech_web/webui/src/bridge/transport/actions.ts`

**根拠 (レビューエージェントの実測):**

- *hooks-state*: slotActions.ts:26-28 は「block 開閉は event 時点の最新値を readTopic で読む」として `const block = readTopic(Topics.blockInventory);` する一方、同ファイル 21-24行の createSlotActions は render 時に capture した `inventory` 引数を使い、39-41行 onRightDown は `inventory.grab.count` を render 時点の値で planPlayerRightClick に渡す。鏡像の useBlockSlotGestures.ts は逆で、23-25行が mainSlots を `readTopic(Topics.inventory)` で event 時に読むのに、20行 `const { grabCount, resolveMaxStack } = useBlockInteraction();` の grabCount は render 時値。右クリック連打で「サーバー上は grab が空になったが再レンダー前」の2打目が grabCount=1 のクロージャで move_item(from: GRAB) を送り、本来の split（半分掴み）が実行されず empty_slot で失敗する。actions.ts:5-9 のコメントどおり BENIGN_ERRORS（9-17行, empty_slot/insufficient_count 等）がこの stale クリック失敗を握りつぶすため、誤った action 選択がユーザーにもログにも見えない。

**検証ノート (独立エージェントによる反証試行の結果):**

> 引用行は全て正確(slotActions.ts:21-24のrender時inventory捕捉・:28のreadTopic・:40のgrab.count、useBlockSlotGestures.ts:20のcontext grabCountと:25のreadTopic、InventoryPanel:19/HotbarPanel:47)。混在規約(同一関数内でevent時読みとrender時捕捉が併存)は実在する設計の非一貫性。ただし因果と深刻度が誇張: 連打シナリオの支配的なstale窓は[action送信→topic event到着]のRTT区間で、この間はreadTopicも同じ旧ストア値を返すため、推奨修正(event時読み統一)では指摘の誤plan自体は解消しない。render捕捉が固有に追加する窓は[ストア更新→再レンダーcommit]の約1フレームのみ(useTopic購読でイベント毎に即再レンダー)。さらにこの失敗クラスはactions.ts:5-8が『stale state由来のクリック連鎖失敗は良性、後続topic eventが再同期』と明示的に許容する設計で、playerSlotPlan.ts:25-26も『半分掴みはホスト計算、staleなclient数量に依存しない』とstale前提を織り込み済み。誤planはサーバー検証で良性失敗に落ち状態破壊は起きない。残る実害は規約混在の可読性・一貫性問題でminor。

**推奨対応**: プラン入力（grab・mainSlots・blockSlots・maxStack）をすべて event 時の readTopic / ストア getState 読みに統一し、createSlotActions を render 引数なしのモジュール関数（または useBlockSlotGestures と対の hook）に揃える。これで InventoryPanel:19 と HotbarPanel:47 の factory 呼び出し重複も消え、BENIGN_ERRORS に依存する場面自体を減らせる。

### F3. useTopicSelectorのequality関数不在（コメント規約頼み）

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: hooks-state
- **対象ファイル**:
  - `moorestech_web/webui/src/bridge/store/useTopic.ts`

**根拠 (レビューエージェントの実測):**

- *hooks-state*: useTopic.ts:23-24 に「selector は安定値/プリミティブを返すこと（毎回新しいオブジェクトを返すと無限再レンダーになる）」と警告コメントがあり、34行の実装は `useTopicStore((s) => selector(...))` と素の zustand フックへ渡すだけで比較関数を指定する手段が無い。現在の3呼び出し（App.tsx:51, HotbarPanel/index.tsx:17, BlockInventoryPanel.tsx:14）はすべてプリミティブ返却で無事だが、`(inv) => inv?.hotbarSlots.map(...)` のような自然な派生を書いた瞬間に useSyncExternalStore の無限ループとしてクラッシュする API 形状で、防御が人間の注意力のみ。

**検証ノート (独立エージェントによる反証試行の結果):**

> evidence正確。useTopic.ts:23-24に警告コメント、34行は素のuseTopicStore(zustand v5)へselectorを渡すのみでequality関数を指定する手段なし(v5のuseStoreにequality引数は無く、zustand/traditionalのuseStoreWithEqualityFnが必要)。3呼び出し全て検証: App.tsx:51はstring(screenForUiState)、HotbarPanel:17はboolean、BlockInventoryPanel.tsx:14はnumber(grab.count)でいずれもプリミティブ。オブジェクト返却selectorを書くとuseSyncExternalStoreの無限ループでクラッシュするAPI形状であり、防御がコメント規約のみという指摘は正しい。minor妥当。

**推奨対応**: useTopicSelector に省略可の equalityFn 引数を追加して zustand/traditional の useStoreWithEqualityFn + shallow を利用可能にするか、少なくとも dev ビルドで selector を2回評価して結果が Object.is 不一致なら console.error する検査を入れ、コメント規約をコードで強制する。

### F4. アイテム名/maxStack解決経路の3方式分裂

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: hooks-state
- **対象ファイル**:
  - `moorestech_web/webui/src/features/blockInventory/blockInteractionContext.ts`
  - `moorestech_web/webui/src/features/recipe/views/RecipeContent.tsx`
  - `moorestech_web/webui/src/features/inventory/InventoryPanel/index.tsx`
  - `moorestech_web/webui/src/shared/ui/ItemSlot/index.tsx`

**根拠 (レビューエージェントの実測):**

- *hooks-state*: 同一の「itemId→表示名」解決が3通りに実装されている。(a) block 系: blockInteractionContext.ts:9 の `resolveName: (itemId: number) => string | undefined` を context 供給し BlockItemGrid.tsx:21 `name={resolveName(slot.itemId)}`。(b) recipe 系: itemMaster Map を RecipeViewer→RecipeContent(Props:26)→CraftRecipeView と3層 prop drilling して CraftRecipeView.tsx:48 `name={itemMaster?.get(r.itemId)?.name}`。(c) inventory/list 系: 各パネルが useItemMaster を直接呼びインライン参照（InventoryPanel:31, HotbarPanel:60, ItemListPanel:22 いずれも `itemMaster?.get(...)?.name`）。React context と store の使い分け基準が feature ごとにバラバラで、名前表示仕様の変更（未ロード時表示等）に3系統の修正が要る。

**検証ノート (独立エージェントによる反証試行の結果):**

> 3方式の分裂は実在を確認。(a) blockInteractionContext.ts:9のresolveName+BlockItemGrid.tsx:21で消費、(b) RecipeViewer.tsx:38→RecipeContent.tsx:26(Props)→CraftRecipeView.tsx:48,56の3層prop drilling、(c) InventoryPanel:31・HotbarPanel:60・ItemListPanel:37の直接useItemMaster参照(指摘のItemListPanel:22は実際は37行で行番号のみ誤り)。(a)にはblockInteractionContext.ts:3-6に「登録コンポーネントのcontractが{data}固定のためcontext供給」という文書化された正当理由があり分裂の一部は意図的だが、(b)と(c)の使い分けに基準は無く、名前解決仕様の変更が3系統に波及する指摘自体は正しい。追加確認: recommendationが参照する「3秒identity churn」も実在(itemMasterStore.ts:27-38のloadWithRetryは成功後もfor(;;)で3秒毎にsetMaster(new Map)を呼び続ける)ため、順序制約の注意書きも妥当。minor妥当。

**推奨対応**: 葉である ItemSlot が useItemMaster で name（必要なら maxStack）を自己解決する形へ寄せ、name prop・resolveName context・itemMaster drilling を段階的に撤去する。ただし finding 1 の3秒 identity churn を先に直さないと全スロットの周期再レンダーに直結するため順序必須。

### F5. activeLayerがplayerInventoryをgame扱いしEsc/Tab閉じが非対称

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: hooks-state
- **対象ファイル**:
  - `moorestech_web/webui/src/shared/uiState/activeLayer.ts`
  - `moorestech_web/webui/src/app/App.tsx`
  - `moorestech_web/webui/src/features/inventory/HotbarPanel/index.tsx`

**根拠 (レビューエージェントの実測):**

- *hooks-state*: deriveActiveLayer（activeLayer.ts:9-15）は modal/blockInventory/research/buildMenu しか区別せず、playerInventory 画面表示中は "game" を返す。そのため App.tsx:55-58 の `useGameLayerKeydown` Esc ハンドラはインベントリ画面中にも発火し、Unity 側の画面クローズ（App.tsx:80 のヒント「Tab/ESC: インベントリを閉じる」）と同時に clearSelectedItem() が走る。一方 Tab で閉じた場合は selectionStore の selectedItemId が残るため、次回オープン時の中央パネル表示が「どのキーで閉じたか」で変わる。同じモデリング欠落により 1-9 キー（HotbarPanel:21-30）とホイール（34-41行）のホットバー選択もインベントリ画面中ずっと有効で、game レイヤー専用ゲートとしての意味が画面表示中は効いていない。

**検証ノート (独立エージェントによる反証試行の結果):**

> 核心のEsc/Tab非対称は実在を確認。deriveActiveLayer(activeLayer.ts:9-15,23-28)はplayerInventoryを区別せず"game"を返すため、App.tsx:55-58のEscハンドラはインベントリ画面中に発火してclearSelectedItem()が走る。Unity側PlayerInventoryState.cs:45はCloseUI(ESC)とOpenInventory(Tab)の両方でGameScreenへ遷移するため画面は両キーで閉じるが、selectionStore.ts:11-15はモジュールレベルzustandでTab閉じでは selectedItemId が残置され、次回オープン時の中央パネルが閉じたキーに依存する非対称は事実。HotbarPanel:21-30(1-9)と34-41(ホイール)がインベントリ画面中も有効という記述も正確。ただし補正2点: (1) Esc時の選択解除自体はApp.tsx:53-54コメントの通り意図された機能でありバグは非対称性のみ、(2) uGUI側HotBarView.cs:46-54もUIStateゲート無しのUpdate()で毎フレーム選択キーを処理しており、1-9有効はuGUIパリティの可能性がある(欠陥とまでは言えない)。モデリング欠落の指摘と非対称の実害は成立するためconfirmed、minor妥当。

**推奨対応**: screenForUiState の UiScreen を deriveActiveLayer の入力に含めて playerInventory/subInventory を独立レイヤーとして陽にモデル化し、「画面クローズ時に選択をクリアするか」を Esc ハンドラではなく ui_state topic の遷移購読側で一元的に決める。

### F6. shared/uiStateがapp層ルーティング知識と全feature名を保持

- **severity**: minor (レビュー時 major から検証で訂正)
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: boundaries
- **対象ファイル**:
  - `moorestech_web/webui/src/shared/uiState/activeLayer.ts`
  - `moorestech_web/webui/src/shared/uiState/uiScreenRouting.ts`
  - `moorestech_web/webui/src/features/inventory/HotbarPanel/index.tsx`

**根拠 (レビューエージェントの実測):**

- *boundaries*: uiScreenRouting.ts:3-4 は自ら「App.tsx ルーティングの単一の正」と述べており、:5 `export type UiScreen = "none" | "playerInventory" | "subInventory" | "researchTree" | "buildMenu";` と画面名を列挙。activeLayer.ts:5 も `export type ActiveLayer = "modal" | "blockInventory" | "research" | "buildMenu" | "game";` と全オーバーレイfeature名を列挙し、readActiveLayer (:19-28) は Topics.modal/blockInventory/uiState の3トピックを読む。つまり shared 層が全feature の存在と優先順位を知っており、新しい画面/オーバーレイを1つ足すたびに shared を編集することになる。AGENTS.md の「汎用基盤にドメイン語彙を持ち込まない」に対する構造的違反。ただし HotbarPanel/index.tsx:2 が feature から import するため src/app へは移せない（app→features の逆流になる）。

**検証ノート (独立エージェントによる反証試行の結果):**

> 事実は全て正確（activeLayer.ts:5のレイヤ列挙・:19-28の3トピック読み・uiScreenRouting.ts:3-5の画面名列挙・HotbarPanel/index.tsx:2のfeature側import）。ただしmajorは過大。(1)レイヤ優先順位の一元化はactiveLayer.ts:7-8「優先順位を1箇所に固定」と意図明記された設計で、中央ルーティングである以上どこに置いても新画面追加時の編集は発生し、提案(独立モジュール昇格)はディレクトリ移動と契約の明文化に過ぎず編集頻度は変わらない。(2)影響範囲はモジュール3ファイル＋importer 2件(App.tsx:12・HotbarPanel:2)のみ。(3)列挙されているのはUIレイヤ/画面名でありAGENTS.mdの想定する基底コンポーネントへの業務ロジック混入とは距離がある。実害は「sharedという名の層にルーティング知識がある」という命名・配置上の曖昧さに留まり、バグ量産構造ではないためminor。

**推奨対応**: uiState を shared から出して独立モジュール（例: src/uiState）に昇格し、レイヤ順を app→features→uiState→shared/bridge と明文化する。import 方向は現状のまま（features が import 可、bridge のみ依存可）で、shared は「featureを知らない汎用部品」の契約を回復する。


---

## G. 境界・bridge (minor)

宣言済み境界の残件と、死んだワイヤ面。A6 の ESLint 強制とセットで潰すと再発しない。

### G1. barrel迂回の残件（toastStore/SlotGrid直指定）

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: boundaries
- **対象ファイル**:
  - `moorestech_web/webui/src/main.tsx`
  - `moorestech_web/webui/src/features/buildMenu/BuildMenuPanel.tsx`

**根拠 (レビューエージェントの実測):**

- *boundaries*: features への深いimportの全件は main.tsx:11 `import { emitToast } from "@/features/toast/toastStore";` の1件のみ（features/toast/index.ts:2 は `export { useToastStore, emitToast } from "./toastStore";` と公開済みで、同じ app 層の DebugActionButton.tsx:3 は barrel 経由）。shared/ui への深いimportは BuildMenuPanel.tsx:3 `import SlotGrid from "@/shared/ui/SlotGrid";` の1件（shared/ui/index.ts:7 が export 済みで、他の全利用箇所は `@/shared/ui` 経由）。件数は少ないが、境界違反ゼロを保っている本コードベースでは例外の存在自体が規約の拘束力を弱める。

**検証ノート (独立エージェントによる反証試行の結果):**

> main.tsx:11のemitToast深部import（features/toast/index.ts:2で公開済み・DebugActionButton.tsx:3はbarrel経由）とBuildMenuPanel.tsx:3のSlotGrid深部import（shared/ui/index.ts:7で公開済み）を実コードで確認。ただし「全件」の主張は不完全で、追加発見としてMachineRecipeView.tsx:3が@/shared/ui/ItemSlot/style.module.cssを直importしており、他コンポーネントのCSS module借用というより深い境界違反が第3の残件として存在する。件数は過少報告だが指摘の方向性とminor判定は妥当（むしろ補強）。修正時はこの3件目も対象に含めるべき。

**推奨対応**: 2件とも barrel 経由へ書き換え、finding 1 と同じ eslint no-restricted-imports ルールに `@/features/*/[内部]` `@/shared/ui/*` のパターンを含めて機械的に強制する。

### G2. protocol.tsのui_stateペイロード型がUiStateNamesと二重定義

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: boundaries
- **対象ファイル**:
  - `moorestech_web/webui/src/bridge/transport/protocol.ts`

**根拠 (レビューエージェントの実測):**

- *boundaries*: protocol.ts:47-48 は「C# UIStateEnum 由来の state 名。文字列リテラルの散在を防ぐ」と UiStateNames（:49-55）を単一の正と宣言しているのに、:89 の ActionPayloads は `"ui_state.request": { state: "GameScreen" | "PlayerInventory" };` とリテラルを直書きしており、同一ファイル内で自己矛盾している。C# 側の state 名が変わった場合に2箇所の同期が必要で、片方だけ直すと dispatch 側の型チェックをすり抜ける。

**検証ノート (独立エージェントによる反証試行の結果):**

> protocol.ts:47-48のコメント「文字列リテラルの散在を防ぐ」・:49-55のUiStateNames定義・:89の"GameScreen" | "PlayerInventory"リテラル直書きを全て確認。同一ファイル内自己矛盾は事実。補足検証: 現存の呼び出しはBlockInventoryPanel.tsx:47とBuildMenuPanel.tsx:15の2箇所で共にUiStateNames.gameScreen経由のため、UiStateNames側だけ変えた場合はこれら呼び出しでコンパイルエラーになり捕捉される。すり抜けが成立するのは呼び出し側が生リテラルを直書きした場合（古い側のリテラルが型を通り実行時にC#側で失敗）に限られ、failure scenarioはやや限定的だがDRY違反自体は実在しminor妥当。修正提案のtypeof UiStateNames.gameScreen方式も有効。

**推奨対応**: `{ state: typeof UiStateNames.gameScreen | typeof UiStateNames.playerInventory }` へ書き換えて UiStateNames を唯一の正に戻す。

### G3. TankInventoryが存在テストで延命されたデッドコード

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: boundaries, abstraction-consistency (独立指摘 2 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/features/blockInventory/views/TankInventory.tsx`
  - `moorestech_web/webui/src/features/blockInventory/blockComponentRegistry.ts`
  - `moorestech_web/webui/src/features/blockInventory/blockComponentRegistry.test.ts`

**根拠 (レビューエージェントの実測):**

- *boundaries*: blockComponentRegistry.ts:17-27 の登録キー（Chest/FilterSplitter/ElectricMachine/GearMachine/ElectricGenerator/FuelGearGenerator/SimpleGearGenerator/ElectricMiner/GearMiner）に Tank 系が無く、views/TankInventory.tsx の本番コードからの参照はゼロ。唯一の参照は blockComponentRegistry.test.ts:11,42-45 で、コメント「実流体ブロック配線時に再登録する想定」と共に `expect(TankInventory).toBeTypeOf("function")` を検証するのみ。registry 設計自体は宣言的で健全（新ブロック型追加は view 新規1ファイル + registry 1行、switch分岐ゼロ、未登録は GenericBlockInventory へフォールバック）だが、このテストは実質デッドコードの保護材になっている。

- *abstraction-consistency*: blockComponentRegistry.ts:17-27 の登録表に Tank キーは無く、grep で TankInventory の参照は自テストのみ。blockComponentRegistry.test.ts:42-45 は `expect(TankInventory).toBeTypeOf("function")` という「関数として存在する」だけの空虚なアサーションで、コメント（40-41行「実流体ブロック配線時に再登録する想定」）でデッドコードの延命を宣言している。本文はFluidSlotRow 1行呼び出し（TankInventory.tsx:6-8）で、GenericBlockInventory のフォールバックが既に同等描画を担う。なお registry 自体も「後続 feature が再代入なしで拡張できるよう可変オブジェクト」(12-13行) と謳うが、blockComponents を変更する箇所は存在しない。

**検証ノート (独立エージェントによる反証試行の結果):**

> 全証拠一致。blockComponentRegistry.ts:17-27にTankキー無し、grepでTankInventory参照は自テスト(blockComponentRegistry.test.ts:11,42-45)のみ、テストはtoBeTypeOf('function')の存在確認だけ(:44)。GenericBlockInventory.tsx:18がFluidSlotRowを描画しフォールバックで同等表示可能なことも確認。追加確認: blockComponentsの変更箇所は定義(:17)と読み出し(:32)のみで、『可変オブジェクト』コメント(:12-13)を裏付ける実変更コードは存在しない。minorのデッドコード指摘として妥当。

**推奨対応**: GenericBlockInventory が fluidSlots を既に描画できる（GenericBlockInventory.tsx:18）ため、TankInventory とその温存テストは削除し、流体ブロック実装時に git から復元するか作り直す。残すなら対応する実 blockType キーで registry に登録して到達可能にする。

### G4. App.tsxに画面固有クロームとドメインactionが混入

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: boundaries
- **対象ファイル**:
  - `moorestech_web/webui/src/app/App.tsx`

**根拠 (レビューエージェントの実測):**

- *boundaries*: App.tsx は screen→パネルのマッピング（:88-93）に加えて、`screen !== "none"` ガードを5回繰り返し（:62,66,78,84,98）、整理ボタンが :68 `onClick={() => void dispatchAction("inventory.sort", {})}` と inventory ドメインの action を app 層から直接発行し、:78-83 でキーヒント（Tab/ESC・R）を直書きしている。全featureをimportして並べる合成ルート自体は妥当だが、「どの画面に何を出すか」（ルーティング）と「インベントリ画面の中身の装飾」（sortボタン・ヒント）という2責務が同居しており、インベントリ画面の仕様変更のたびに app 層を触ることになる。

**検証ノート (独立エージェントによる反証試行の結果):**

> 証拠一致。App.tsx:62,66,78,84,98で`screen !== "none"`ガードが5回、:68でinventory.sortのdispatchAction直書き、:78-83でキーヒント直書き、:88-93が画面→パネルのマッピング。補足: 整理ボタンとヒントはplayerInventory限定でなくresearchTree/buildMenu画面でも表示される(screen!=="none"条件)ため『インベントリ画面の装飾』という表現はやや不正確だが、app層に画面固有クロームとドメインactionが混入している指摘自体は正しい。minor妥当。

**推奨対応**: InventoryPanel+RecipeViewer+ItemListPanel+topControls+keyHints を束ねる PlayerInventoryScreen を features/inventory（または screens ディレクトリ）に切り出し、App は screenForUiState の結果を画面コンポーネントへマップするだけに縮める。inventory.sort ボタンはインベントリ側へ移す。

### G5. ClientMsgのsnapshot要求opが死んだワイヤ面

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: bridge
- **対象ファイル**:
  - `moorestech_web/webui/src/bridge/transport/protocol.ts`
  - `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebSocketMessageDispatcher.cs`

**根拠 (レビューエージェントの実測):**

- *bridge*: protocol.ts:27 は ClientMsg に `{ op: "snapshot"; topic: string }` を定義するが、grep 実測で送信箇所はゼロ（subscriptionManager は subscribe/unsubscribe のみ、webSocketClient は action のみ）。サーバー側 WebSocketMessageDispatcher.cs:36-42 が subscribe 受信時に必ず `SendSnapshotAsync` を呼ぶため個別 snapshot 要求は不要になっており、:48-51 の `case "snapshot":` 実装ごと両側で死んでいる。ワイヤ契約の見かけ上の面積が実態より広く、契約監査（wireContract.test.ts）でも未検証の面が残る。

**検証ノート (独立エージェントによる反証試行の結果):**

> grep実測で送信箇所ゼロを再確認: src全体でop:"snapshot"の出現はprotocol.ts:20(ServerMsg)/:27(ClientMsg定義)とwebSocketClient.ts:93(受信判定)のみで、subscriptionManagerはsubscribe/unsubscribe、sendActionはactionのみ。C#側WebSocketMessageDispatcher.cs:36-42でsubscribe受信時に必ずSendSnapshotAsync実行、:48-51のcase "snapshot"は対応する送信者が存在せず両側で死んだ面。wireContract.test.tsはserver→client payloadとerror_codesのみ検証しClientMsg面は未監査という点も正確。再購読で同効果が得られる(TryAdd冪等+再snapshot送信)という削除根拠も:40-41で確認。デッドコード/契約面の整理課題としてminorが適切。

**推奨対応**: ClientMsg から snapshot バリアントを削除し、C# 側の case "snapshot" も対で削除する。将来「再取得」が必要になったら subscribe の再送で同じ効果が得られる（サーバーは TryAdd + 再snapshot送信のため冪等）。

### G6. アイコンURL等HTTPワイヤ面がshared/uiにハードコード散在

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: bridge
- **対象ファイル**:
  - `moorestech_web/webui/src/shared/ui/ItemIcon.tsx`
  - `moorestech_web/webui/src/shared/ui/BlockIcon.tsx`
  - `moorestech_web/webui/src/bridge/store/itemMasterStore.ts`

**根拠 (レビューエージェントの実測):**

- *bridge*: WS の topic 名は protocol.ts の Topics に「単一の真実」として集約されている一方、HTTP のエンドポイントは itemMasterStore.ts:29 `fetch("/api/master/items")`（bridge 内）、ItemIcon.tsx:23 `src={`/api/icons/${itemId}.png`}`、BlockIcon.tsx:21 `src={`/api/block-icons/${blockId}.png`}`（いずれも shared/ui）に生文字列で散在し、Unity ホスト側のルーティング変更が2レイヤー3ファイルの文字列修正になる。buildMenu の iconUrl は payload でサーバーから受け取る方式（payloadTypes.ts:164）を採っており、同じ「アイコンの所在」の伝え方がタイプ毎に不統一。

**検証ノート (独立エージェントによる反証試行の結果):**

> 3箇所全て実在確認: itemMasterStore.ts:29 fetch("/api/master/items")、ItemIcon.tsx:23 /api/icons/${itemId}.png、BlockIcon.tsx:21 /api/block-icons/${blockId}.png。対比のbuildMenuはpayloadTypes.ts:164のiconUrl?でサーバーから受け取る方式、WS topic名はprotocol.ts:34-45のTopics定数に集約済みで「HTTP面だけ散在」という非一貫性の指摘は正確。実害はUnityホスト側ルーティング変更時の2レイヤー3ファイル文字列修正に留まり、共通化機会としてminorが妥当。

**推奨対応**: bridge に Topics と同格の ApiRoutes 定数（itemIcon(itemId)/blockIcon(blockId)/itemMaster）を置き、shared/ui はそれを参照する。あるいは buildMenu と同様にサーバー payload へ iconUrl を載せる方式に寄せて、URL 組み立ての知識をホスト側へ一元化する。


---

## H. 抽象度・デッドコード・規約 (minor)

過剰抽象(パススルーLogic・投機的props)と過少抽象(1行述語の重複・ユーティリティ再発明)、および掃除リスト。

### H1. 200行・10ファイル規約上限への張り付き（RecipeViewer.module.css混載含む）

- **severity**: minor (レビュー時 major から検証で訂正)
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: css, abstraction-consistency (独立指摘 2 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/features/recipe/RecipeViewer.module.css`
  - `moorestech_web/webui/src/features/recipe/views/ItemHeader.tsx`
  - `moorestech_web/webui/src/features/recipe/views/CraftRecipeView.tsx`
  - `moorestech_web/webui/src/features/recipe/views/CraftProgressArrow.tsx`
  - `moorestech_web/webui/src/features/recipe/views/RecipeContent.tsx`
  - `moorestech_web/webui/src/features/blockInventory/filterSplitterLogic.ts`
  - `moorestech_web/webui/e2e/mock-host/wsHandler.ts`

**根拠 (レビューエージェントの実測):**

- *css*: RecipeViewer.module.css は 200 行で 1 ファイル 200 行規約の上限に到達済み。しかし RecipeViewer.tsx 自身はこのファイルを import しておらず、実際の利用者は views/ 配下の 4 コンポーネント: ItemHeader.tsx(itemHeader/toolTab/itemName/itemHeaderRule/recipeTreeButton = 3-75 行), CraftRecipeView.tsx(recipeBox/craftTime/craftButton = 79-171 行), CraftProgressArrow.tsx(craftArrow 系 = 175-200 行), RecipeContent.tsx(tabIcon/recipeContent)。さらに未使用の .recipeItems(119-122 行)を含む。次にどのビューへスタイルを 1 つ足しても即規約違反になる。

- *abstraction-consistency*: RecipeViewer.module.css はちょうど200行で、視覚パリティ調整が続く中央パネルのCSSなので次の1ルールで規約割れする。ディレクトリ直下ファイル数は src/features/blockInventory=10、src/features/recipe=10、e2e/mock-host=10 と3箇所が上限ちょうど（blockInventory は BLK 系ビュー追加のたびに view+logic+test が増える活発な領域）。

**検証ノート (独立エージェントによる反証試行の結果):**

> 事実関係は全て正確: wc -lでRecipeViewer.module.css=ちょうど200行(リポジトリ最大ファイル)、grepでRecipeViewer.tsx自身は非import・利用者はviews/の4コンポーネント(ItemHeader:2/CraftRecipeView:8/CraftProgressArrow:2/RecipeContent:5)のみ、.recipeItems(119-122行)は定義のみで参照ゼロの死にコード、blockInventory/recipe/e2e/mock-hostは各ちょうど10ファイル(サブディレクトリ除く)。ただし全て「上限ちょうどで未違反」の状態であり、現時点で規約違反もバグも存在しない予防的指摘。次の1変更で機械的な分割作業が発生するというスケジューリング上の注意であって、放置してもバグを生む構造ではない。CSSファイル名と実利用者の乖離は実在するcohesion問題だが分割は機械的に可能(指摘自身も認めている)。majorは過大でminorが妥当。

**推奨対応**: views/ 単位で ItemHeader.module.css / CraftRecipeView.module.css / CraftProgressArrow.module.css に分割する（利用グラフは既にコンポーネント単位で綺麗に分かれているため機械的に分けられる）。あわせて未使用 .recipeItems の削除と craftButton/recipeTreeButton の共通化(別 finding)で総行数も減る。

**鮮度更新 (2026-07-18)**: 監査の推奨対応そのものが実行済み。RecipeViewer.module.css(200行)は ItemHeader.module.css(152行)/RecipeBox.module.css(169行)/CraftProgressArrow.module.css(38行)へ分割され、RecipeViewer.module.css 本体は12行に縮小、混載問題自体は解消した。ただしその副作用として新規違反が発生している: ItemListPanel.module.css の新設により features/recipe/ 直下が11ファイルとなり、AGENTS.md の「1ディレクトリ10ファイル上限」に抵触した（監査時点は10でセーフだった）。views/ も9ファイルで上限に接近している。

### H2. 所持数充足述語のrecipe/research二重実装

- **severity**: minor (レビュー時 major から検証で訂正)
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: abstraction-consistency
- **対象ファイル**:
  - `moorestech_web/webui/src/features/recipe/craftLogic.ts`
  - `moorestech_web/webui/src/features/research/researchLogic.ts`
  - `moorestech_web/webui/src/shared/ownedCounts.ts`
  - `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`

**根拠 (レビューエージェントの実測):**

- *abstraction-consistency*: craftLogic.ts:50 `return recipe.requiredItems.every((r) => (counts.get(r.itemId) ?? 0) >= r.count);` と researchLogic.ts:38 `return node.consumeItems.every((c) => (owned.get(c.itemId) ?? 0) >= c.count);` は同一ロジック。しかも両者は既に共有の buildOwnedCounts(shared/ownedCounts.ts) が作る Map を入力にしており、集計だけ共通化して判定だけ手書き重複という中途半端な境界になっている。型レベルでも payloadTypes.ts:106 `RequiredItem = { itemId; count }` と :116 `MachineRecipeItem = { itemId; count }`、ResearchNodeData.consumeItems のインライン `{ itemId: number; count: number }[]` (146行) と同一 shape が3通りに定義されている。

**検証ノート (独立エージェントによる反証試行の結果):**

> evidenceは全て正確。craftLogic.ts:50とresearchLogic.ts:38は同一述語で、両者ともbuildOwnedCounts産のMapを入力にしている(ResearchTreePanel.tsx:25 / RecipeContent.tsx:40で確認)。型もRequiredItem(payloadTypes.ts:106)/MachineRecipeItem(:116)/consumeItemsインライン(:146)の3重定義に加え、SlotData(:6)も同shapeで実際は4重。さらにresearchLogic.ts:55 isItemSufficientにも同じ比較式`(owned.get(itemId) ?? 0) >= required`の第3の手書きがある(指摘の補強)。ただし重複の実体は各1行の述語×2ファイルで、TypeScriptの構造的型付けにより型aliasの重複は実害なし。集計(buildOwnedCounts)は既に共通化済みで、ドリフトが起きても影響範囲は2画面の充足判定のみ。「機能追加のたびに壊れる構造」ではなく典型的な共通化余地であり、majorではなくminorが妥当。

**推奨対応**: shared/ownedCounts.ts に `hasRequiredItems(required: {itemId,count}[], owned: Map<number,number>): boolean` を追加し、craftable と hasEnoughItems の本体を置き換える（uGUI 準拠の閾値変更が入ったとき2箇所ドリフトするのを防ぐ）。あわせて payloadTypes に `ItemStack = { itemId; count }` を1つ定義し RequiredItem/MachineRecipeItem/consumeItems を寄せる。

### H3. buildMenuLogic/progressLogicの形式的パススルーLogicファイル

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: abstraction-consistency
- **対象ファイル**:
  - `moorestech_web/webui/src/features/buildMenu/buildMenuLogic.ts`
  - `moorestech_web/webui/src/features/progress/progressLogic.ts`
  - `moorestech_web/webui/src/features/buildMenu/BuildMenuPanel.tsx`

**根拠 (レビューエージェントの実測):**

- *abstraction-consistency*: buildMenuLogic.ts の全実体は `selectPayload(entry) { return { entryType: entry.entryType, entryKey: entry.entryKey }; }` と `deletePayload(name) { return { name }; }` の2つで、判定・変換が一切ない型付き object literal パススルー。呼び出しは BuildMenuPanel.tsx:31,34 の各1回のみで、型安全性は dispatchAction の `ActionPayloads[K]` 制約が既に担保している。progressLogic.ts も `percentValue(n) { return clamp01(n) * 100; }` の1行関数1つで使用は ProgressBar.tsx:23 の1回。craftLogic/holdCraftLogic/researchLogic のような実判定を持つ Logic ファイルと同格の見た目になり、パターンの信号価値を薄めている。

**検証ノート (独立エージェントによる反証試行の結果):**

> buildMenuLogic.ts(全14行)の実体はselectPayload/deletePayloadの型付きobject literalパススルー2つのみで、判定・変換ゼロを確認。使用はBuildMenuPanel.tsx:31,34の各1回のみ。buildMenuLogic.test.tsも「写す」だけのトートロジー的テスト(toEqualで入力をそのまま検証)。progressLogic.ts:5-7はclamp01(n)*100の1行でProgressBar.tsx:23の1回のみ使用。実判定を持つcraftLogic/researchLogic/hotbarLogicと同じ命名パターンで形骸ファイルが並ぶという指摘は実態と一致。minorとして妥当。

**推奨対応**: buildMenuLogic.ts はテストごと削除し payload をインライン記述する（modalLogic の canConfirm のような実判定が生まれた時に初めて Logic ファイルを作る）。percentValue は次項の共有数値ヘルパへ移して progressLogic.ts を畳む。

### H4. 小型ユーティリティ（clamp・比率・循環index）の再発明散在

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: abstraction-consistency
- **対象ファイル**:
  - `moorestech_web/webui/src/shared/ui/ProgressArrow/index.tsx`
  - `moorestech_web/webui/src/features/recipe/views/CraftProgressArrow.tsx`
  - `moorestech_web/webui/src/shared/ui/FluidSlot/fluidLogic.ts`
  - `moorestech_web/webui/src/features/blockInventory/details/detailLogic.ts`
  - `moorestech_web/webui/src/features/recipe/views/RecipePager.tsx`

**根拠 (レビューエージェントの実測):**

- *abstraction-consistency*: (1) `clamp01(value) * 100` の%変換が ProgressArrow/index.tsx:7 と CraftProgressArrow.tsx:11 に手書きされる一方、同じ式を名前付けした percentValue (progressLogic.ts:6) は ProgressBar の1箇所しか使われない——同一概念に対し命名済み1・手書き2の不整合。(2) fluidLogic.ts:11-14 `fillRatio` と detailLogic.ts:25-28 `fuelRatio` は「分母<=0なら0、else clamp01(a/b)」の完全同形。(3) RecipePager.tsx:17 `(index + count - 1) % count` / :23 `(index + 1) % count` は hotbarLogic.ts:13-15 `cycleHotbar` と同概念の循環移動の手書き（負値配慮の有無だけ違う）。

**検証ノート (独立エージェントによる反証試行の結果):**

> 3点とも実コードと一致。(1)ProgressArrow/index.tsx:7とCraftProgressArrow.tsx:11に`clamp01(value) * 100`が手書きされ、同式を命名したpercentValue(progressLogic.ts:5)はProgressBar.tsx:23の1箇所のみ使用——命名済み1・手書き2の不整合を確認。(2)fluidLogic.ts:11-14 fillRatioとdetailLogic.ts:25-28 fuelRatioは「分母<=0→0、else clamp01(a/b)」の完全同形。(3)RecipePager.tsx:17,23の循環indexとhotbarLogic.ts:13-15 cycleHotbarは同概念(cycleHotbarのみ負値対応の二重剰余)。6箇所の散在は事実でminor妥当。

**推奨対応**: shared/clamp01.ts を shared/mathUtil.ts（等）に拡張して `percent01(n)`・`safeRatio(num, denom)`・`cycleIndex(current, delta, count)` を置き、上記6箇所を寄せる。fillRatio/fuelRatio は safeRatio の別名として残してもよいが本体は1つにする。

### H5. GamePanel/SlotGridの投機的未使用propsとコメント乖離

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: abstraction-consistency
- **対象ファイル**:
  - `moorestech_web/webui/src/shared/ui/GamePanel/index.tsx`
  - `moorestech_web/webui/src/shared/ui/SlotGrid/index.tsx`
  - `moorestech_web/webui/src/features/inventory/HotbarPanel/index.tsx`

**根拠 (レビューエージェントの実測):**

- *abstraction-consistency*: GamePanel の `headerRight`(7行) と `testId`(12行) は全3呼び出し（InventoryPanel/RecipeViewer/ItemListPanel）で一度も渡されていない。SlotGrid の `onWheel`(10行) と `className`(11行) も全6呼び出し箇所（BlockItemGrid/MachineSection×3/ItemListPanel/InventoryPanel/BuildMenuPanel）で未使用。さらに SlotGrid/index.tsx:14-15 のコメント「inventory/hotbar/block/itemList で共用」に対し、HotbarPanel は SlotGrid を使わず独自の `styles.hotbarFrame` div で実装しており（HotbarPanel/index.tsx:51）、onWheel prop の想定利用者だったはずのホイール処理もその div 側に付いている。

**検証ノート (独立エージェントによる反証試行の結果):**

> GamePanelのheaderRight(index.tsx:7)/testId(:12)は全3呼び出し(RecipeViewer.tsx:24/ItemListPanel.tsx:27/InventoryPanel/index.tsx:22)で未使用、SlotGridのonWheel(:10)/className(:11)も全JSX呼び出し(BlockItemGrid:15/MachineSection:37,39,41/ItemListPanel:30/InventoryPanel:23/BuildMenuPanel:26)で未使用を確認。テストからの利用も無し(grepで両propsのtest内使用0件、両コンポーネントに専用testファイル自体無し)。SlotGrid/index.tsx:14-15のコメント「inventory/hotbar/block/itemListで共用」に対しHotbarPanel/index.tsx:51は独自のstyles.hotbarFrame divでonWheel=onHotbarWheelを直付けしておりコメント乖離も事実。指摘中の呼び出し箇所数は「全6」と書きつつ列挙は7インスタンス(MachineSection×3)だが結論に影響なし。minor妥当。

**推奨対応**: headerRight/testId/onWheel/className の未使用 props を削除し、必要になった PR で復活させる（YAGNI）。SlotGrid のコメントから hotbar を外し実際の利用箇所リストに直す。

### H6. 参照ゼロ/テスト専用exportの残存

- **severity**: minor
- **検証判定**: 確定 (実コード照合済み)
- **検出観点**: abstraction-consistency
- **対象ファイル**:
  - `moorestech_web/webui/src/features/toast/toastStore.ts`
  - `moorestech_web/webui/src/bridge/transport/subscriptionManager.ts`
  - `moorestech_web/webui/src/shared/ui/index.ts`

**根拠 (レビューエージェントの実測):**

- *abstraction-consistency*: toastStore.ts:26 の `removeToast` は grep で参照0（ToastHost は `withCloseButton={false}` で閉じるUI自体が無く、自動消滅は addToast 内の setTimeout が担う）。subscriptionManager.ts:46-48 の `subscribedTopics()` はプロダクションコードから未使用で、subscriptionManager.test.ts:34 の1アサーションのみが使う実質テスト専用API。shared/ui/index.ts:2,4 の `ItemIcon`/`FluidSlot` barrel export は外部から一度も import されず（利用は ItemSlot/FluidSlotRow の相対 import のみ）。

**検証ノート (独立エージェントによる反証試行の結果):**

> 3点とも一致。removeToastはtoastStore.ts:12(型),26(実装)以外にsrc全体で参照0、ToastHost.tsx:15はwithCloseButton={false}で閉じるUIが無く自動消滅はaddToast内setTimeout(toastStore.ts:24)が担う。subscribedTopics(subscriptionManager.ts:46-48)はプロダクション参照0でsubscriptionManager.test.ts:34の1アサーションのみが使用する実質テスト専用API。shared/ui/index.ts:2,4のItemIcon/FluidSlotはbarrelから一度もimportされず(全@/shared/ui importをgrepで確認)、実利用はItemSlot/index.tsx:3とFluidSlotRow/index.tsx:3の相対importのみ。minor妥当。

**推奨対応**: removeToast を削除。subscribedTopics は削除し、当該テストは send に渡る subscribe/unsubscribe メッセージの検証（既に同ファイルで行っている方式）へ寄せる。barrel からは内部専用の ItemIcon/FluidSlot を外し、公開部品の一覧としての意味を保つ。


---

## X. 検証で反証された指摘

レビューで挙がったが、検証エージェントの実コード照合で主要部分が反証されたもの。記録として残す。

### X1. BENIGN_ERRORSの網羅漏れで正常操作の競合がエラートースト化

- **severity**: minor
- **検証判定**: 一部誇張 (問題は実在、severity/範囲を訂正)
- **検出観点**: hooks-state, bridge (独立指摘 2 件を統合)
- **対象ファイル**:
  - `moorestech_web/webui/src/features/recipe/views/CraftRecipeView.tsx`
  - `moorestech_web/webui/src/features/recipe/holdCraftLogic.ts`
  - `moorestech_web/webui/src/bridge/transport/actions.ts`
  - `moorestech_web/webui/src/shared/itemMove/blockSlotPlan.ts`

**根拠 (レビューエージェントの実測):**

- *hooks-state*: CraftRecipeView.tsx:30-31 は「素材チェックはサーバー側で行われる」と明記し、useHoldCraft は inventory topic の往復を待たず craftTime 周期で dispatchAction("craft.execute") を発火する（32-34行）。holdCraftLogic.ts:16-17 は「craftTime<=0 は毎tick即発火。rAF 頻度に律速される」と毎フレーム発火（約60回/秒）まで明示的に許容している。しかし actions.ts:9-17 の BENIGN_ERRORS に craft.execute のエントリが無いため、素材が尽きる境目で in-flight のクラフト要求がサーバーに拒否されると shouldToastFailure(19-22行) が true になり `craft.execute failed: ...` のエラートーストがユーザーに出る。クライアント側の楽観発火（stale 前提）と transport 側の失敗抑止リストが対応していない。

- *bridge*: actions.ts:9-17 の BENIGN_ERRORS は `"inventory.split": new Set(["grab_not_empty", "empty_slot"])` と `"inventory.move_item"` / `"block_inventory.move_item"` を持つが、`block_inventory.split` と `inventory.collect` / `block_inventory.collect` のエントリが無い。一方 blockSlotPlan.ts:41 は空手右クリックで `{ type: "block_inventory.split", payload: { from: blockRef(index) } }` を発行しており、プレイヤースロットなら握り潰される empty_slot 相当の stale クリックが、ブロックスロットでは shouldToastFailure(actions.ts:19-22) を通ってエラートーストされる。move/split を対で良性化した設計意図（コメント「stale state 由来のクリック連鎖失敗は良性」）に対して片側だけ抜けている。

**検証ノート (独立エージェントによる反証試行の結果):**

> 半分は反証された。craft.executeの指摘はwrong: Unity側ハンドラ(Client.WebUiHost/Game/Actions/CraftActions.cs:43-56)はrecipeGuid検証後 SendOnly.Craft で fire-and-forget し無条件で Success を返すため、素材不足がaction失敗になる経路が存在せず「素材が尽きる境目でエラートースト」は発生し得ない(失敗コードはinvalid_payload/invalid_recipe/recipe_lockedの実バグ系のみ)。actions.test.ts:43も shouldToastFailure("craft.execute", "anything")===true を意図的にピン留め。inventory.collect/block_inventory.collectの網羅漏れ指摘も不成立: 両ハンドラの失敗コードはinvalid_payload/invalid_slotのみで(InventoryActions.cs:104-108, BlockInventoryActions.cs:161-175)、空スロットcollectは成功no-op扱いのため登録すべき良性コードが存在しない。一方 block_inventory.split の指摘は confirmed: BlockInventoryActions.cs:131でgrab_not_empty、134でempty_slotを返し得るのにBENIGN_ERRORS(actions.ts:9-17)に未登録で、inventory.splitとの対称性が欠けている。ブロックスロットは機械が自律的に消費するためstale競合は player側より起きやすく実害あり。範囲が誇張(3-4件中1件のみ実在)のためexaggerated、severityはminorのまま。

**推奨対応**: サーバーの素材不足系エラーコードを BENIGN_ERRORS の craft.execute に登録する。あわせて「楽観発火する action は必ず BENIGN_ERRORS に想定失敗コードを持つ」という対応関係を actions.ts のコメント表で管理する。


## 監査のカバレッジと限界

- Find 6観点 → Dedup → Verify の3段は完了。最終の網羅性チェック (Critic) のみサブエージェントの利用上限で未実行。ただし検証段で ESLint 不在 (A6)・バリデータレジストリの網羅性欠如 (A4)・vi.mock 儀式の5ファイル波及 (A5) など指摘範囲を広げる追加発見が複数出ており、主要観点はカバーされている。
- e2e/ ハーネス・vite/tsconfig/postcss 設定の専任レビューは未実施 (ファイル数上限接触 (H1) と ESLint 不在の検出に留まる)。
- 検証は「反証試行」方式のため、severity は保守側 (低め) に倒れている。X1 のように Unity 側 C# 実装まで読んで反証したケースがある一方、uGUI 正本との視覚突き合わせは本監査の範囲外 (E1, E6 など「uGUI 正本と突き合わせて決めよ」とした項目が該当)。
- **鮮度更新 (2026-07-18)**: e2e/parity-check.py(47項目機械判定)・crop-parity-parts.py・parity_targets.py・実フォントアセットが監査後に新設され、幾何・色の uGUI 一致検証は部分的に自動化された。ただし E1/E6 等の意味論的な設計判断（充足表現の方式など）は自動検証の範囲外で、当該項目の留保は依然有効。
- **鮮度更新 (2026-07-18)**: ESLint 設定は本更新時点でも不在（`eslint.config.*` は webui プロジェクト配下に無く、node_modules 内のみ）で、A6/G1 の前提は不変。
