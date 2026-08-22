# 0029: 初期チュートリアル調整（装備チャレンジ新設・木ピンのドロップ品指定・キーヒント赤字・ドラッグ矢印・研究説明文）

- Status: Accepted
- Date: 2026-08-22

## Context

ADR 0016/0022 で組んだ初期チュートリアルの実プレイ確認（ユーザー 2026-08-22）で次が挙がった:

1. 「[Tab] インベントリを開く」の文字が見づらい
2. 石器の装備を独立チャレンジにして「石器を装備→木を伐採」の順にしたい
3. ドラッグガイド矢印が速すぎ・小さすぎる（速度半分・大きさ2倍）
4. 「木を伐採して原木を入手する」のピンが石のmapObjectに刺さる
5. 研究画面の説明文が全ノード「New Research Description」のまま
6. 「石の斧を作る」の後に石の斧を装備するチャレンジとチュートリアルが欲しい

調査で確定した事実:
- チュートリアル表示は全て WebUI（`moorestech_web/webui`）。ドラッグ矢印は `--tutorial-drag-guide-size: 28px` / `--tutorial-drag-guide-duration: 1600ms`（`app/tokens.css`）。キーヒント文字様式は `:where(.keyHintText)`（白 `--text-high-contrast`）をチュートリアルHUD・インベントリ画面・研究画面の3箇所が共有。
- ピン不具合の真因: マスタの `mapObjectPin.mapObjectGuid` は `木`(6a53fef8) だが、v8 の `generation.json` はこの GUID を1本も配置しない（実際の木は BirchTree01/Fir1/Sequoia1 等100種超の個別GUID）。`MapObjectGameObjectDatastore.SearchNearestMapObject` が null を返し `MapObjectPin` の transform が前チャレンジ（小石）の位置に残留＋毎フレーム LogError。
- 研究説明文は `research.yml` の schema default「New Research Description」が全ノード（47件）に残り、`localization.csv` の `research.<guid>.description` も同文。
- 装備スロットは3枠（`items.json` equipmentSlotCount=3）。採掘は選択中装備（`LocalPlayerEquipment.SelectedItem`）だけを見る。
- challenges.json は `moorestech_master/tools/tutorial_v3_port/generate_challenges.py` が生成（GUIDはkey由来で安定）。

## Decision

### 1. 装備チャレンジの新設と `equipItem` 完了種別（2026-08-20 裁定の更新）

- 「石器を装備する」を「石器を作る」と「木を伐採して原木を入手する」の間に、「石の斧を装備する」を「石の斧を作る」の直後（原始研究3の前）に独立チャレンジとして追加する。
- 新 taskCompletionType `equipItem { itemGuid }`。達成条件は**選択中の装備スロット（`IEquipmentInventory.GetSelectedItem()`）に対象アイテムが入った時**。`IEquipmentInventoryUpdateEvent` のスロット更新・選択index更新の両方を購読し、チャレンジ開始時点で既に装備済みなら初回 `ManualUpdate` で達成（`CompleteResearchChallengeTask` 前例）。
- 装備チャレンジの tutorials: `keyControl{GameScreen, Tab, "インベントリを開いて<道具>を装備"}` ＋ `uiDragGuide{from: inventory.item-<guid>, to: equipment.selected-slot}`。「木を伐採」からは keyControl/uiDragGuide を外し mapObjectPin だけ残す。
- 出所: ユーザー裁定 2026-08-22 原文「石器を装備 で1チャレンジ作って、木を伐採とチャレンジを分ける（石器を装備→木を伐採）の順」「石の斧を作ったあと、石の斧を装備するチャレンジとチュートリアルを作る」→ 達成条件は選択「選択中スロットに入った時」（`.decisions/2026-08-22-石器と石の斧の装備は独立チャレンジにしequipItem完了種別を新設する.md`）。
- agent前提: 「石の斧を装備する」の配置は原文「石の斧を作ったあと」から直後に置く。challenge title/summary 文言・localization.csv の english は agent が起案する。

### 2. mapObjectPin の対象指定をドロップ品でも指定できるようにする

- `mapObjectPin` の tutorialParam を `pinTargetType: enum{mapObject, earnItem}` ＋ `pinTargetParam` switch（`mapObject{mapObjectGuid}` / `earnItem{itemGuid}`）に変える（`map.yml` の `veinParam`/`handMiningParam` と同型のネストswitch）。`pinText` は据え置き。
- クライアント `MapObjectPin` は earnItem 指定のとき「`earnItems` に当該 itemGuid を含む mapObject 群のうち未破壊の最寄り」へピンする（`MapObjectMaster` から該当 mapObjectGuid 集合を導出し `SearchNearestMapObject` に渡す）。
- 木を伐採は `earnItem{原木}`、小石拾いは `mapObject{小石}` のまま。
- 出所: ユーザー裁定 2026-08-22 原文「木を伐採して原木を入手 の木を掘るチュートリアルでピンを石のmapobjectのターゲットにしてしまっている」→ 選択「ドロップ品で探す新param」（`.decisions/2026-08-22-mapObjectPinはドロップ品指定で最寄りを探せるようにする.md`）。
- agent前提: 未配置GUID『木』のマスタ行は本ADRでは触らない（別件）。草花が原木を落とすマスタ事実もスコープ外。

### 3. キー操作ヒントの文字色を全画面で赤にする

- `app/tokens.css` の `--key-hint-*` 群に色トークン `--key-hint-color: var(--text-insufficient)` を足し、`:where(.keyHintText)` と配下 `kbd` の `color` をそれで描く。チュートリアルHUD（`KeyControlHintHud`）・インベントリ画面・研究画面左下のキーヒントすべてが赤になる。
- 出所: ユーザー裁定 2026-08-22 原文「『Tabでインベントリを開く』の文字列が見づらい（赤とかにしても良いかも）」→ 選択「キーヒント全部を赤文字に」（`.decisions/2026-08-22-キー操作ヒントの文字色は全画面で赤にする.md`）。
- agent前提: 赤は新色相を増やさず既存 `--text-insufficient`（#ff7878）を使う（webui-design §9「新しい装飾モチーフの無断追加禁止」）。

### 4. ドラッグガイド矢印は速度半分・大きさ2倍

- `--tutorial-drag-guide-size: 56px`、`--tutorial-drag-guide-duration: 3200ms`。keyframes の比率（15%→75%移動）は据え置き。ビルドメニュー→ホットバーの矢印にも同じトークンが効く。
- 出所: ユーザー裁定 2026-08-22 原文「ドラッグの速度をもっと遅く（今の半分、カーソルを大きく今の2倍に）」。
- agent前提: 「速度半分」＝1周期の duration を2倍。

### 5. 研究説明文は全ノードに解放内容ベースの1行を書く

- `research.json` 全ノードの `researchNodeDescription` を「何を解放するか」を軸にした1行（日本語）に差し替え、`localization.csv` の `research.<guid>.description` 行の Source/japanese/english を更新する。`research.yml` の `default: New Research Description` は削除して必須にする。
- 出所: ユーザー裁定 2026-08-22 原文「new research descriptionをなくしてちゃんとした説明を入れる。文章は1行程度」→ 選択「全49ノード・解放内容ベース」（`.decisions/2026-08-22-研究説明文は全ノードに解放内容ベースの1行を書く.md`）。
- agent前提: clearedActions が空のノード（スマート分岐器・機械オーバークロック・反物質爆弾・核融合炉・ロケット）は「（準備中）今後の研究で解放内容が追加される」旨の1行にする。

## Consequences

- 本体 repo: schema（challenges.yml / research.yml）、サーバー `EquipItemChallengeTask`＋Factory登録、クライアント `MapObjectPin`（earnItem解決）、webui tokens.css（3トークン）、MasterSourceTextCollector/型参照の追従。
- マスタ repo: generate_challenges.py の表更新＋challenges.json 再生成、research.json、localization.csv。スキーマ変更（mapObjectPin param / equipItem / research default削除）を含むため、本体PRとマスタPRは同一マージ単位（ピン更新同時）。
- 2026-08-20 裁定ファイルは本ADRから更新済みとしてリンクする。
