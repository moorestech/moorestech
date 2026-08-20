# 0022: 初期チュートリアルの提示強化（枠線ラベル・キー操作ヒント・アンカー語彙拡張・石の斧モデル）

- Status: Accepted
- Date: 2026-08-20

## Context

ADR 0016 でチュートリアル提示を WebUI 経路へ統一し、枠線ハイライト（`uiHighLight` / `itemViewHighLight`）・D&D 矢印ループ（`uiDragGuide`）・ワールドピンの3機構と、Web 側の単一アンカー語彙（`anchorIds.ts`）で初期チュートリアルを組んだ。その結果、Web で文言が出るのは title（左上HUD）・summary（Q画面）・ピン・スキットだけで、枠線には文言が出ず（`highLightText` は収集されるだけ）、`keyControl` は休眠（Web 配信 topic 無し）、インベントリ/装備スロットにはアンカーが無い。

実プレイ確認（ユーザー 2026-08-20）で次が不足と判明した:
- 左上HUDの枠線に「左上で現在の目標を確認する」の文言が出ない
- 石器を作るで「①石器を選択 ②クラフトボタンを長押し」の手順文言が出ない
- Tab（インベントリ）/ R（研究画面）のキー操作を促す提示が出ない
- 石器を装備スロットへ入れる誘導が無い（木の伐採は選択中の装備のみを見る）
- 石器・石の斧を装備しても手持ちモデルが出ない（石器はマスタ設定のみで解決 → moorestech_master PR #22。石の斧は Addressable 未登録）

調査: `docs/research/2026-08-20-tutorial-master-rewrite-feasibility.md`

## Decision

### 1. 枠線ハイライトに文言ラベルを描く（2026-08-18 裁定の更新）

- `uiHighLight` / `itemViewHighLight` の枠線（kind `outline`）に `tutorialGuid` を載せ、Web 側で `challengeTutorial.<tutorialGuid>.text` を解決して枠線の脇に小ラベルを描く。文言が空（ローカライズ解決結果が空）なら従来どおり枠線のみ。
- 文言の出所はマスタ `highLightText`（既存。ローカライズ収集済み）で、Unity は文言を送らない（ワールドピンと同方式・`WorldPinOverlay` 前例）。
- 出所: ユーザー裁定 2026-08-20「ラベル描画を追加する」（`.decisions/2026-08-20-枠線ハイライトに文言ラベルを描く.md`）
- agent前提: ラベルは枠線の下辺外側に配置し、`webui-design` の HUD トーンに従う。ラベル位置のアンカー解決は枠線と同じ `resolveTutorialAnchor` の rect を使い、枠線が非表示ならラベルも出ない。

### 2. keyControl をキーキャップ付き HUD ヒントとして Web で復活させる

- presentation に kind `keyControl { elementId, tutorialGuid, keyName }` を追加。Web は画面下中央（ホットバーの上）に「[Tab] インベントリを開く」形式（キーキャップ＋説明文。`LocalizedShortcutHint` と同じ見た目）で描く。説明文は `challengeTutorial.<tutorialGuid>.text`（= `controlText`）。
- `uiState` 一致判定は **Web 側**で行う。Unity はチャレンジ開始時に kind `keyControl { elementId, tutorialGuid, keyName, uiState }` を当該チャレンジの session に載せるだけで、Web が `ui_state.current` topic の `state`（Unity `UIStateEnum` 名と同一文字列。`UiStateNames` 参照）と `uiState` が一致する間だけ描く。（planning時のagent前提修正: Unity側で状態変化のたびに要素を足し引きすると、`TutorialPresentationStateStore.AddElement` が「最後に BeginSession した challenge」の session へ付けるため別チャレンジの session に紛れ込む。要素を適用時に正しい session へ固定し、表示可否だけを Web に委ねる方が単純で前例（outline/dragGuide の anchor 解決可否も Web 側判断）と一致する）
- schema `challenges.yml` の keyControl に `keyName: string`（必須）を追加し、`uiState` の enum を実 `UIStateEnum` に揃える（`BlockInventory`→`SubInventory`、`ChallengeList` / `ResearchTree` / `BuildMenu` / `TrainHUDScreen` を追加。`Debug` は含めない）。既存 keyControl データ（v8 に2件、moorestechAlphaMod_3 に1件の計3件）は keyName を付けて一括更新し、その追記は本体PRと同一マージ単位にする（`optional` で吸収しない。keyName 必須化だけが先に入ると3件ともロード時に例外死する）。
- `KeyControlTutorialManager` は単一インスタンス保持（last-wins）から、tutorialGuid ごとの複数同時保持へ変える（原始研究1で GameScreen 用と PlayerInventory 用の R ヒントを並存させるため）。
- 出所: ユーザー裁定 2026-08-20「HUDヒント＋キーキャップ付きで復活」「画面下中央（ホットバーの上）」（`.decisions/2026-08-20-keyControlはキーキャップ付きHUDヒントとしてWebで復活させる.md`）。2026-08-19「keyControlは将来使うので残す」の実現。
- agent前提: スキット中は目標HUDと同様に非表示。同時に複数の keyControl 要素があるときは縦に並べる。

### 3. 石器の装備誘導は「木を伐採して原木を入手する」の tutorials に付ける

- 新チャレンジ・新 taskCompletionType は作らない。tutorials に keyControl（Tab・GameScreen）と `uiDragGuide{from: inventory.item-<石器guid>, to: equipment.selected-slot}` を付ける。
- 出所: ユーザー裁定 2026-08-20「木を伐採の tutorials に付ける（チャレンジ追加なし）」（`.decisions/2026-08-20-石器の装備誘導は木を伐採チャレンジのtutorialsに付ける.md`）

### 4. アンカー語彙にインベントリ所持スロットと装備スロットを足す

単一語彙 `anchorIds.ts` に追加し、uiHighLight / itemViewHighLight / uiDragGuide のどれからでも直書き参照できる:
- 動的 prefix `inventory.item-` … `inventory.item-<itemGuid>`。メインインベントリで該当アイテムを持つ先頭スロット。guid→itemId は Web 側で item master topic から解決（Unity は無変換。2026-08-19 裁定どおり）
- 動的 prefix `equipment.slot-` … `equipment.slot-<index>`
- 静的 `equipment.selected-slot` … 選択中の装備枠（ホイールで動く）
- 出所: ユーザー裁定 2026-08-20（復唱確認）（`.decisions/2026-08-20-アンカー語彙にインベントリ所持スロットと装備スロットを足す.md`）
- agent前提: 同一要素が複数アンカー（`equipment.slot-0` と `equipment.selected-slot`）を持つため、`data-tutorial-anchor` を空白区切りトークン列にし、解決セレクタを `[data-tutorial-anchor~="…"]` にする（アンカーIDに空白は無い）。Unity 側フィクスチャ `tutorial_anchor_ids.json` と `TutorialAnchorContractTest` / `anchorIds.test.ts` を同時更新。

### 5. 石の斧の手持ちモデル

- `Assets/Dependencies/Sketchfab/StoneAxe/StoneAxe.prefab` を元に `AddressableResources/Item/StoneAxe.prefab`（手持ちオフセット/スケール焼き込み）を Unity Editor 経由で作成し `Vanilla/Item/StoneAxe` で登録。PlayMode スクリーンショットで確認後、マスタ `石の斧.addressablePaths.handGrabModel` に設定。
- 出所: ユーザー裁定 2026-08-20（`.decisions/2026-08-20-石の斧の手持ちモデルはStoneToolと同方式でAddressable登録する.md`）

### 6. パッケージング

本体は (a) 枠線ラベル＋keyControl 復活（schema 変更含む）(b) 新アンカー3種 (c) StoneAxe Addressable の3PR、マスタは3本マージ後に1PR（keyControl 追記・ラベル文言更新・装備誘導・斧モデル）。schema の keyName 必須化を含むため、マスタが先に入ると旧本体で起動不能になる順序事故を避ける。
出所: ユーザー裁定 2026-08-20「本体3PR（a/b/c）＋マスタ1PR」

## Considered Options

- 枠線のみ維持・文言は title/スキットへ（却下: 手順文言を示せない）
- 文言付きの独立 kind「hint」新設（却下: kind が増え枠線との位置関係を二重管理）
- keyControl を文言のみで復活 / 復活させず title で代替（却下: キーの視認性・UI状態別の出し分け）
- 独立チャレンジ「石器を装備する」＋新完了種別 equipItem（却下: チャレンジと完了種別が増える。ユーザー選択）
- D&D の to を `equipment.hud` 全体（却下: 選択外の枠に入れると伐採できない）
- 新 tutorialType `itemDragGuide` で Unity が guid→itemId 変換（却下: 2026-08-19「Unityは無変換」の例外を増やす）
- 石の斧を見送り / 既存モデル流用（却下）

## Consequences

- 2026-08-18 裁定の「Web で文言が出るのは title/summary/ピン/スキットのみ」は「＋枠線ラベル＋キー操作ヒント」に更新される。
- schema 変更（keyName 必須・uiState enum）により `challenges.yml` 由来の生成コードが変わる。マスタ側 keyControl 3件への keyName 追記だけは本体と同一マージ単位で入れ、残りの文言・誘導追記は本体マージ後に行う。
- Web のアンカー属性がトークン列になるため、`resolveTutorialAnchor` と既存テストが追従する。
- マスタ側の後続追記（本体3PRマージ後）: 小石の HUD 枠線文言「左上で現在の目標を確認する」、石器を作るの文言「①石器を選択」「②クラフトボタンを長押し」と keyControl(Tab)、木を伐採の keyControl(Tab)＋uiDragGuide、木の板の keyControl(Tab)、原始研究1の keyControl(R; GameScreen / PlayerInventory)、石の斧の handGrabModel。
