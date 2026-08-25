# 0033. 採掘tooltipは取得アイテム名を前置し、動作語を「採掘」に統一する

日付: 2026-08-25
状態: 採択

## Context

手掘りのカーソルtooltipは `MiningFocusState` が `MouseCursorTooltip` へ出しており、現状は操作の言い方だけを持つ。

| 状態 | 現行文言 |
| --- | --- |
| `Ready`（進捗掘り可） | 左クリック長押しで取得する（`ui.tooltip.holdToGet`） |
| `InstantPickUp` | 左クリックで取得（`ui.tooltip.pickUpLeftClick`） |
| `ToolMismatch` | このアイテムが必要です: {p0}（`ui.tooltip.requiredItems`） |
| `HandMiningNotAllowed` | 手掘りできません（`ui.tooltip.cannotHandMine`） |

そのため、目の前の岩・木・露頭から**何が手に入るのかがカーソルを合わせても分からない**。特に鉱脈の露頭は見た目が近く、掘って初めて中身が分かる。

対象は `IMiningTargetObject` の2実装だけである。

- `MapObjectGameObject` … `MapObjectMasterElement.EarnItems`（配列）
- `OutcropGameObject` … `MapVeinMasterElement.VeinParam`（`item` なら単一 `itemGuid`、`fluid` なら `fluidGuid`）

実データ（`moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json`）の調査で判明した制約:

- `mapObjectName` は `Strate_0` / `RedFir3` のような**英語のアセット名で、ローカライズ辞書にも載っていない**。表示名の出所にはできない。
- mapObject 195件のうち**約60件（ブッシュ・サボテン・草・花）は `earnItems` が空**。名前を持てない対象が実在する。
- 現行データの `earnItems` はすべて0件か1件。複数は未出現だがスキーマ上は配列。
- 鉱脈11件のうち水・原油の2件は `veinType: fluid` かつ `handMiningType: none`。アイテム名を持たず、かつ永久に手掘り不可。

## Decision

- **tooltipの先頭に取得アイテム名を置き、`{名前} : {動作文}` の形にする。動作語は「採掘」に統一する**（`Ready` →「小石 : 左クリック長押しで採掘」、`InstantPickUp` →「小石 : 左クリックで採掘」）。既存の「取得する」表現は捨てる。長押しと単クリックの区別は残す。
  出所: ユーザー裁定 2026-08-25 原文「採掘時、石 : 左クリックで採掘　みたいに、採掘出来るもののアイテム名をtooltipで出したい」→ 選択「名前を前置し、動作語は『採掘』に統一」
- **表示名の出所はドロップアイテム名とする**（mapObject は `earnItems` のアイテム名、鉱脈は `veinParam.itemGuid` のアイテム名）。`mapObjectName` / `veinName` は使わない。
  出所: ユーザー裁定 2026-08-25 原文「採掘出来るもののアイテム名」（agent調査: `mapObjectName` は非ローカライズの英語アセット名で表示に使えない）
- **`earnItems` が空の対象では名前欄ごと出さず、現行の文言のままにする**（草・サボテンは「左クリック長押しで取得する」を維持）。
  出所: ユーザー裁定 2026-08-25 選択「名前なしの現行文言のまま」
  棄却案: ①「何も手に入らない」と明示する ②空ドロップ自体をデータのバグとして別issue化する（`.decisions/2026-08-25-採掘tooltipの名前は取得アイテム名を出所とする.md`）
- **採掘できない状態でも名前を前置する**（ツール不足→「鉄鉱石 : このアイテムが必要です: 鉄のツルハシ」、手掘り不可→「タングステン鉱石 : 手掘りできません」）。ツルハシを手に入れる前に、どの鉱脈を見つけたのかが分かることを優先する。
  出所: ユーザー裁定 2026-08-25 選択「分からせたい（名前を出す）」
  棄却案: ①現行の理由文だけ ②ツール不足の時だけ出す
- **液体鉱脈（水・原油）には名前を出さず、理由文だけを現行どおり表示する。tooltip自体は出す。**
  出所: ユーザー裁定 2026-08-25 選択「名前なし（理由文だけ）」＋ 確認質問「名前だけ出さない（理由文は現行どおり残る）」
  棄却案: ①液体名を同じ形で出す ②tooltip自体を出さない
- **`earnItems` が複数のときは全件をカンマ区切りで並べる**（「小石, 原木 : 左クリック長押しで採掘」）。既存の `ui.tooltip.requiredItems` が複数ツールを `", "` で並べているのと同じ流儀。
  出所: ユーザー裁定 2026-08-25 選択「全部カンマ区切りで並べる」
  棄却案: 先頭1件だけ
- 区切りは依頼原文どおり半角スペース＋コロン＋半角スペース（` : `）とする（agent前提: 原文の見た目をそのまま採る）。
- 名前あり文言は `Localization/localization.csv` に名前つきバリアントのキーを新設して3言語ぶん持ち、名前が無い対象は既存キーへ落とす（agent前提: `MouseCursorTooltip.Show(key, textParams)` が1キー＋`{p0}`置換の機構であり、キーを分けるのが前例どおり）。
- **`IMiningTargetObject` が公開するのは取得アイテムの `ItemId` 列（マスタ由来のデータ）までとし、ローカライズと連結は行わない。** 「何が取れるか」の判断は具体側にあり、`MapObjectGameObject` は `EarnItems` を、`OutcropGameObject` は `ItemVeinParam` を解決する。**液体鉱脈（`FluidVeinParam`）は空列を返す**という判断も `OutcropGameObject` 側に置く（agent前提: 設計原則「汎用基盤にドメイン語彙を持ち込まない。判断は具体側で行う」）。
- **ローカライズ文字列の組み立ては `MiningControllerContext` がフォーカス実体の変化時に1回だけ行い、結果を保持する。** `MiningFocusState.GetNextUpdate` は毎フレーム走るため、フォーカス中ずっと `Localize.GetContent` と `string.Join` を回してはならない（agent前提: 設計原則「`Update()` 内で毎tickの同値判定をしない／変化を起こす操作の直後にプッシュする」。`SetFocusTarget` は既に実体変更時だけを検出しており、そこが唯一の変化点）。
- **言語切り替え時は保持中の文字列を作り直す。** `MiningControllerContext` が `Localize.OnLanguageChanged` を1本だけ購読して再解決する（agent前提: UniRx購読が標準機構。対象個体ごとの購読は数千個体に膨れるため採らない）。

## Consequences

- Web UI 側は `TooltipTopic` が `TooltipPresentation`（キー＋params）をそのまま配るため、辞書追加だけで追従する。翻訳キーの新設漏れがそのまま両画面の不具合になる。
- `Client.Tests/Mining/MiningFocusStateTest` の期待文言が全面的に変わる。空ドロップ・液体鉱脈・複数ドロップの3分岐はテストで固定する。
- 「取得する」という語がゲーム内から消え、PickUp（小石を拾う）も「採掘」と呼ばれることになる。これはユーザーがプレビュー付きで選んだ帰結として受け入れる。
