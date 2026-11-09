# UITK コンポーネント設計書（段階 2〜5 の部品と画面の作り方）

> **For agentic workers:** 段階 2 以降の詳細計画（moores-writing-plans）と各レーンは、この文書を設計の正本として引く。部品の公開 API・置き場・USS の書き方・テストの型をここから外すときは、レーンの計画に理由を書き、§7 の判断記録へ足す。この文書の改訂はレーンが直接書かず、integrator が実行計画 §5 のレビューフローに通して入れる（§7.1 の ID は指揮が実行計画 §6.1 で先に割り当てる）。**レーンの切り方・依存・部品とファイルの持ち主は実行計画（§1・§6・§8）が正本**で、§5.2 は初期提案として残す。

- **ゴール（ユーザー原文の要旨）**: 「WebUI と同じ見た目で、適切な設計、コンポーネント化された UITK ベースの UI を開発すること」
- **出発点**: `origin/feature/uitk` = `54c8a4c8c8`（段階 1 済み）。現状図は `research/stage1-asbuilt.md`、Web 部品の棚卸しは `research/web-components.md`、テストの梯子は `research/tests-ladder.md`
- **上位の正本**: `origin/master:docs/uitk-migration/plan.md`（§3 共通基盤・§3.13 並列実行・§4 検証）と 2026-11-06 の裁定 6 本。とくに「全段階の画面を Web 版と同じ見た目にする（例外はユーザー原文『無理に同じにする必要はない、影響が小さく非本質的な対応が入るのであれば、推奨でも良い』）」と「全部 Web／全部 UITK の全体スイッチ 1 つ」
- **この文書の判断の出所**: 裁定に書かれていない判断はすべて「agent 既定」で、§7 に ID 付きで並べた。本文中は `[A-n]` で引く。ユーザーに上げる判断は `[Q-n]`（2026-11-06 の全体レビュー時点で 0 件。Q-1 は裁定から導いた agent 既定 [A-39] へ下げた）
- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/src/`
- **評価役 2 体の指摘の反映**（2026-11-06）: 判定と根拠は `research/design-eval-2026-11-06.md`。再現したものだけを A-40〜A-48 として入れた

---

## 0. 要点

1. **層は 5 つ**: Foundation（ドメインを知らない基盤）→ Components（ドメインを知らない見た目の部品）→ Content（アイテム・ブロック・液体を表示素材〈アイコン・名前〉へ解く薄い層、新設）→ Screens（画面・HUD）。状態・ViewModel・コマンドは別 asmdef の `Client.Ui.State`。asmdef は今の 2 つ（`Client.Ui`・`Client.Ui.State`）のままにし、層の向きは L2 の依存方向テストで止める `[A-1]`
2. **部品は Web の `shared/ui` と 1 対 1 で名前をそろえる**。Web の共有部品 24＋CSS だけの共有 3＋事実上の共有 4＋Mantine 由来の見た目 9 系統を、§2 の表で UITK 部品へ対応付けた。Mantine 由来の見た目（Button・Modal・Notification・Paper・Title/Text・ScrollArea 等）も Web 版どおり写す（2026-11-06 裁定。様式化しない）
3. **USS は部品ごとに 1 枚、配り方を変える**: 段階 1 の「部品の ctor が `StyleSheet` を受け、`UiAssetCatalog` に 1 枚ずつ登録」をやめ、部品・画面の USS を `GameUiTheme.tss` に `@import` で束ねる `[A-2]`。部品の ctor から `StyleSheet` 引数が消える。束ね漏れは L2 で止める
4. **見た目の一致は「トークン名の 1 対 1」「部品のつまみ（Web の局所上書き変数）を同名で受ける」「参照キャプチャとの並べ比べ」の 3 本で担保する**。PoC 由来の `--control-*`（Web に無い色）は Mantine の暗色テーマの値へ置き換える `[A-5]`
5. **画面は「Presenter（載せる・出す・購読）＋ View（部品を組む・操作をコマンドへ）＋ 型付き Model＋ 型付き Commands」**。段階 1 の PauseMenu を前例として固定する `[A-8]`。View は C# で組み、UXML は使わない `[A-3]`
6. **部品は段の頭で先に作る**: Web で既に 2 feature 以上が使う部品は、その段の「部品レーン」で Components に直接作る。§3.13 の「2 機能目で昇格」は、Web の棚卸しに無い新しい共有にだけ使う `[A-11]`
7. **文字の大きさは Web と同じ px**: 文字の 13px 下限（plan §3.10 の agent 既定）は、裁定「全段階で Web 版と同じ見た目」から外し、Mantine `size="xs"`（12px）とスロット内の 10〜11px も Web と同じ px にする。実機の比較で読めないものだけを例外に記帳する `[A-39]`（旧 `[Q-1]`。ユーザーに上げる判断は 0 件）

---

## 1. 層構造

### 1.1 層と依存の向き

```
                    Client.Input   Client.Localization   Client.Game（ClientContext・UIStateControl・AnchorIds）
                         ▲                 ▲                        ▲
                         │                 │                        │
 Client.Ui.State ────────┴─────────────────┴────────────────────────┘
   Routing / Commands / <機能>Model・ViewModel / <機能>Commands / Text(UiText)
         ▲
         │（Screens と Content だけが参照）
 Client.Ui（1 asmdef。中の層の向きは L2 で検査）
   Screens/<機能>      … Presenter・View・画面だけの部品
      │  ▼
   Content/            … ItemId/BlockId/FluidId → UiIcon・表示名（Client.Game の ClientContext を読む）
      │  ▼
   Components/<族>/<部品> … ドメインを知らない見た目の部品（UiIcon・UiText・数値だけを受ける）
      │  ▼
   Foundation/         … Root・Stage・Layers・Theme・Motion・Text・Input(Gesture)・Tooltip・DragDrop・Modal・Anchor
```

| 層 | 置き場 | 持つもの | 参照してよいもの | 参照してはいけないもの |
|---|---|---|---|---|
| Foundation | `S/Client.Ui/Foundation/<領域>/` | 全画面が使う仕組み。層・舞台・テーマ・動き・文言・入力規則・ジェスチャ・D&D・ツールチップの宿主・モーダルの宿主・アンカー | Client.Input・Client.Localization・UniRx・UniTask・`Client.Game` のうち入力と UIState とアンカー ID（段階 1 の現状どおり） | Components・Content・Screens・`Client.Ui.State` の機能別の型 |
| Components | `S/Client.Ui/Components/<族>/<部品>/` | 見た目の部品。状態は Set メソッドで受け、操作は `IObservable` で出す | Foundation・`Client.Ui.State/Text`（`UiText`）・`Mooresmaster.Localization.Generated`（`LocalizationKey`） | Content・Screens・`Client.Game`・`Client.Ui.State` の機能別の型・`MasterHolder` |
| Content（新設） | `S/Client.Ui/Content/` | マスタの ID から表示素材を作る写像（アイコン・表示名・液体の色）。状態を持たない | Foundation・Components の値型・`Client.Game`（`ClientContext` のアイコン容器）・`Core.Master` | Screens |
| Screens | `S/Client.Ui/Screens/<機能>/` | 画面・HUD の Presenter と View、1 機能だけが使う部品 | 上のすべて・`Client.Ui.State` | 他の `Screens/<機能>`（共有したくなったら §5 で昇格） |
| State | `S/Client.Ui.State/<機能>/`（別 asmdef） | ViewModel（不変 sealed）・Model（`IReadOnlyReactiveProperty<ViewModel>`）・Commands（`UiCommandResult` を返す）・純ロジック | Client.Game・Client.Network・Core.Master 等（段階 0 の参照のまま） | `Client.Ui`・UnityEngine.UIElements |

- **Content を分ける理由**: Web の `ItemSlot` は `itemId` を受けて中で `useItemMaster` を引くが、UITK でそれをやると Components が `Client.Game`（`ClientContext.ItemImageContainer` 等。`S/Client.Game/InGame/Context/ItemImageContainer.cs`）とマスタを知り、AGENTS.md「汎用基盤にドメイン語彙を持ち込まない」に反する。部品は `UiIcon`（テクスチャと代わりの文字）と数だけを受け、ID からの解決は Content に 1 か所で置く `[A-4]`
- **asmdef を割らない理由** `[A-1]`: 割ればコンパイルで向きを強制できるが、段階 1 の 80 ファイルを動かし、`internal` の見え方と .meta の再生成（手で作れない）を伴う。L2 の「`Components/` 配下の .cs が `using Client.Game`・`Client.Ui.State.<機能>`・`Client.Ui.Content`・`Client.Ui.Screens` を含まない」「`Content/` が `Screens` を含まない」の検査で同じ効果を安く得る。検査が破られたら asmdef 分割へ上げる
- **Editor 専用 asmdef を 1 つ足す**: `S/Client.Ui.Editor/`（`Client.Ui.Editor.asmdef`、Editor のみ）に部品ギャラリー（§3.4）を置く `[A-9]`。.meta は Unity に作らせる

### 1.2 Foundation の領域（段階 1 の現状＋足すもの）

| 領域 | 段階 1 にあるもの | 足すもの（段階） |
|---|---|---|
| Root | `GameUiRoot`（`GetLayer`・`SetCefVisible`・`SetRootClassEnabled`） | `GetLayer(UiLayer, UiFamily)`（段階 2 前の基盤整えレーン F1）。`SetCefVisible` は全体スイッチの作り直し（albof.17）で消す。全体非表示（全部隠し・HideAll。Ctrl+U・カットシーン）の口 `SetWholeUiVisible(bool)`（中身は `UiLayerStack.SetAllLayersVisible`。ルートの `display` で BehindStage を含む全層を隠し、状態は残す）を S2-skit が足す `[A-80]` |
| Stage（新設） | なし | `UiFamily { Stage, Viewport }` と、各層の中に「1280×720 の舞台」と「実画面いっぱい」の 2 つの子を置く `UiStageFrame`（Web `--ui-stage-*`／`--ui-viewport-*`・ADR 0013/0017 の stage 族と viewport 族）。PanelSettings の Expand（1280×720 基準）でルートは実画面になるので、舞台は中央寄せの固定寸法にする `[A-6]`（F1）。舞台の上段 3 列（Web `.stage` の grid `inv viewer items`・列間 `--stage-column-gap`・上 `--menu-upper-safe-area`・左 `--stage-padding-left`・研究画面だけ左 0）は `StageColumnsLayout` が持ち、画面は層ではなくその枠へ載せる `[A-45]`（C4-e）。Web が `--ui-scale` を掛けない Portal（Mantine `Modal`・eventLanguageGate の `Overlay`）の中身は、`UiScreenPixelScope` が舞台の倍率を打ち消して実画面 px で置く `[A-41]`（C3-b）。倍率の式は `StageScaleMeasure` の 1 本、毎フレームの見張りは `StageScaleWatch` の 1 点で、DI で組まれる宿主には `StageScaleSource` で配る `[A-75]`。置き場は `Foundation/Stage/Columns/`（`StageColumn`・`StageColumnsLayout`・`StageColumnsRouting`・`StageColumnsMount`）。舞台の Screen 層への取り付けと研究画面の畳みは `StageColumnsMount` が `UiRoutingState` を購読して行い、DI は Singleton（`StageColumnsGameUiRegistration`）。空の列も列間を保つ `[A-92]` |
| Layers | `UiLayer` 13 層・`UiLayerStack` | `Hud` を `OverlayPanel`（`--z-stage-overlay-panel` 30）と `Chrome`（31）に割る。`SkitStage`（`--z-stage-skit`）・`SkitTransition`（`--z-portal-skit-transition` 210）・`ViewportStage` を足す。`UiLayerOrderTest` の対応表を同時に直す（F1。integrator 所有） |
| Theme | Tokens/SkitTokens/SlotTokens/ControlTokens/Theme/Typography の 6 USS・`UssCustomStyleReader` | §3 のトークン体系（`MantineTokens.uss`・`DerivedTokens.uss`）。`ControlTokens.uss` は消す（F1）。読み口の `UssCustomStyleReader.TryReadKeyword`（C3-a）・`TryReadPercent`＋`UssTokenValue.TryParsePercent`（S2-hud）は使い手のレーンが末尾に足す `[A-59]` |
| Motion | `UiMotionPlayer`・tween・慣性・Step 3 種 | Step を使い手の段で足す（段階 2: `ScaleTweenStep`〈脈動。S2-tutorial〉・`TranslateXTweenStep`〈通知の入場。S2-notification〉、`UiEasing` に CSS の ease-in/ease-out〈S2-notification〉。実行計画 E-19・`[A-63]`・`[A-69]`） |
| Text | `LocalizedLabel`・ロケールクラス | `LocalizedLabel.SetText(UiText)` を足し、`SetKey(key, params)` との二重を畳む（F1）。`LocalizedTextBinding`（宿主がパネルにいる間 `UiText` を解いて `IObservable<string>` で流し、言語変更で解き直す。Label 1 つに入れない部品〈`ShortcutHintLabel`・`GameTextField` の置き文字〉が使う。C2-c が作り C2-b が使う `[A-31]`）。`ShortcutHintLabel`（kbd の差し込み）は Components へ（§2.6）。`UiNumberFormat` は `Client.Ui.State/Text` へ移動（L0-s4b。State の純ロジックも使うため `[A-21]`） |
| Input | 入力規則・Pick probe・IME | `Gesture/UiPressGesture`（tap・drag・abort の状態機械。Web `W/shared/pointerGesture`）（段階 3 部品レーン） |
| Tooltip（新設） | なし | `HoverTooltipHost`（Tooltip 層に 1 つ。パネルの木の根の TrickleDown の PointerMove と Pick で印の付いた要素を引き、捕捉中は出さない。印の変化は `HoverTooltipMark.OnMarkChanged` で受けて引き直す。面は口 `IHoverTooltipFace` で受け、実装は Components の `TooltipFace` `[A-76]`）（段階 3）。印の付いた要素を含む `ScrollView` のスクロールで引っ込め、無関係なパネルのスクロールでは引っ込めない。引っ込めた後はポインタの座標が実際に変わった `PointerMoveEvent` でだけ開き直す（Web `W/shared/ui/HoverTooltip/index.tsx:24-72`・`components.md` §8）`[A-43]`。CursorTooltip は画面（段階 2） |
| DragDrop（新設） | なし | `UiDragDrop`（源の印・落とし先の印。ゴーストは出さない。結果〈タップ・落とし先・外〉は源の `IUiDragPayload` が受けて解く。Web `useHotbarDragSource` の一般化）（段階 3）`[A-78]` |
| Modal（新設） | なし | `UiModalHost`（同時 1 件で、新しい表示は前の中身を差し替える・幕・中身は `UiScreenPixelScope` の中。幕の押下は `OnBackdropClicked` で知らせるだけで、閉じるかは画面の状態が決める。フォーカスは閉じ込めない〈Web `trapFocus={false}`〉）（段階 3）`[A-77]` |
| Anchor | `TutorialAnchor.Mark`・`Resolve` | `Resolve` はログを出さず、未発見・重複の報告は追跡する側（S2-tutorial の `TutorialAnchorTracker`）が状態の変化のときだけ出す（重複は LogError）。`TutorialAnchor.MarkClipContainer` を足し、USS の `overflow: hidden` で子を切る容器には画面がこの印を付ける（`IResolvedStyle` に overflow が無く C# から読めないため）。S2-tutorial が入れる `[A-65]` |
| Icons（新設） | なし | `UiIcon`（値型。§2.2）と `GameIcon` の描き方の規則。ID の解決は Content |

### 1.3 命名

| 対象 | 規則 | 例 |
|---|---|---|
| 部品のクラス名 | Web の `shared/ui/<名>` と同じ名前。Web に名前が無い Mantine 由来の見た目は、役割の名前（技術名を入れない。plan §3.1） | `ItemSlot`・`SlotFrame`・`FormButton`（Mantine `Button`）・`DialogFrame`（Mantine `Modal`） |
| namespace | ディレクトリと一致。`Client.Ui.Components.<族>`（部品ごとに切らない） | `Client.Ui.Components.Slots`。段階 1 の `Client.Ui.Components`（直下）と `Client.Ui.Components.TextField` は F1 でそろえる `[A-7]` |
| ディレクトリ | `Components/<族>/<部品>/`（1 部品 1 ディレクトリ、.cs と .uss を同居）。族は §2 の 8 つ | `Components/Slots/ItemSlot/ItemSlot.cs`・`ItemSlot.uss` |
| USS のクラス | BEM。ルートは部品名のケバブ、子は `__`、状態は `--`。ルートのクラス名は全 USS で一意（L2） | `.item-slot`・`.item-slot__count`・`.slot-frame--selected` |
| 状態のクラス | Web の data 属性 5 種などを `--<状態>` に写す。付け外しは部品の Set メソッドの中だけ（`EnableInClassList`） | `data-insufficient` → `.slot-frame--insufficient` |
| testid | `name` に Web と同じ綴り（DSL のシナリオがそのまま動く。plan §3.12） | `name = "hotbar-slot-0"` |
| つまみ（USS カスタムプロパティ） | Web の局所上書き変数と同じ名前（§3.3） | `--slot-size`・`--icon-pad`・`--count-font-size` |
| イベント | `IObservable<T> On<変化対象><過去分詞>`（AGENTS.md「イベントは変化対象を名前に含める」） | `OnOptionSelected`・`OnSlotGestured`・`OnClicked` |
| 画面 | `Screens/<機能>/<機能>ScreenPresenter`・`<機能>View`。HUD は `<名>HudPresenter`・`<名>HudView` | `BuildMenuScreenPresenter`・`HotbarHudView` |

### 1.4 段階 1 から変えるもの

| # | 今（段階 1） | 変えた形 | 理由 | いつ・誰が |
|---|---|---|---|---|
| C1 | 部品の ctor が `StyleSheet` を受け、`UiAssetCatalog` が USS 1 枚ごとにフィールド＋読み出し＋検査行を持つ（`S/Client.Ui/Foundation/Assets/UiAssetCatalog.cs`、7 枚で 60 行） | 部品・画面の USS は `GameUiTheme.tss` に `@import` で束ね、ctor から `StyleSheet` を消す。`UiAssetCatalog` は USS を持たなくなるので消す（テクスチャ等の資産が要る段で、必要なら同じ形で作り直す）。束ね漏れ・余り・ルートクラスの重複は L2 で止める | 段階 2〜4 で USS が 40 枚を超え、カタログが 200 行を超える（stage1-asbuilt §6.2）。トークンは既に tss 経由（theme D-7）。`UiTestHost` は本番の tss を当てるので L1 でも同じに効く `[A-2]` | F1（integrator） |
| C2 | `GameTextField` に USS が無く、入力欄の見た目（Web `textAreaField.module.css`）と Mantine Button 相当が `BugReport.uss:40-121` に直書き | `GameTextField.uss` を作り、`GameTextFieldLook { GaugeInset, MantineInput }` で見た目を選ぶ（置き文字の言語追従は Foundation の `LocalizedTextBinding`）`[A-29]`。Mantine Button 相当は `FormButton` 部品へ出す | 段階 3 の BP 名入力・ビルドメニュー検索、段階 5 のゲートで同じ見た目が要り、画面 USS の重複になる | C2-b（段階 2 部品レーン）。BugReport の置き換えも同レーン |
| C3 | 設定のスクロールは `PauseMenuSubPageView` に直書き。スクロールバーが Web（細い・枠の右端）と違う（web-look の cmp-settings.png） | `GameScrollView` 部品（Web `panelBodyScrollArea.module.css` と §8.10 の写し）に置き換える | 6 feature が同じスクロール領域を使う（web-components §1.3）。既知の見た目の差を同時に消す | C2-c（実行計画 E-7） |
| C4 | `--control-*`（PoC 由来の色。`--control-accent: #f2a640` 等）で既定コントロールを塗る | Mantine の暗色テーマの実値を `MantineTokens.uss`（名前は Web の `--mantine-*` と同じ）に生成し、`Theme.uss` をそれへ向け直す。`ControlTokens.uss` の残り（GamePanel の前計算色など）は `DerivedTokens.uss` に移す | 素の Button・Slider・Scroller を使うと Web と違う色になる（stage1-asbuilt §5.2）。Web は実際に `--mantine-color-dark-*` を参照している（FluidSlot・GameIcon・challenge・trainHud・treeView） `[A-5]` | F1 |
| C5 | `UiLayer.Hud` 1 層に overlay-panel と chrome が潰れ、skit・skit-transition・viewport-stage の層が無い | §1.2 Layers のとおり割る・足す | 研究パネルの上にチャレンジ HUD・キーヒント・採掘バーを出す Web の順を表せない（stage1-asbuilt §6.3） | F1 |
| C6 | 舞台（1280×720）と実画面の 2 族の区別が無い（全面の層だけ） | `UiFamily` と `UiStageFrame` | 21:9 で HUD を実画面の端へ、パネルを中央の舞台へ置く Web の配置（ADR 0013/0017）を写すため `[A-6]` | F1 |
| C7 | `GamePanel` が default だけ。題は `SetTitle(Label)`。box-shadow を描かない（theme D-6）。本番から未使用 | 枠付き（default・craft）の `GamePanel` と、面だけ（hud・skit）の `GamePanelSurface` に型で分ける（Web の union 型と同じ守り）。`SetTitle(UiText)`・`SetTitleAction(VisualElement)` を足す。外周の影（`0 0 12px 7px rgb(9 11 20 / 45%)`）は描かない（Web でも同じ `::before` の mask に切られて画面に出ない）`[A-22]` | Web 版と同じ見た目（2026-11-06 裁定）。影は参照の画素で出ていないことを実測した（C2-a 計画 D-1） | C2-a |
| C8 | `PanelActionButton` が `Button` を継承し、押下は `Button.clicked`（`Action`）で受ける | `IObservable<Unit> OnClicked` を足し、外では `clicked` を使わない（レビューの観点に足す） | AGENTS.md「イベント発火に Action を使わない。UniRx を使う」 | F1 |
| C9 | `UiText`（Key＋Params）と `LocalizedLabel.SetKey(key, params)` が同じ組を別々に持つ。`UiText` は辞書キーしか運べない | `LocalizedLabel.SetText(UiText)` に一本化する。`UiText` は辞書キー（`LocalizationKey`＋引数）・コンテンツ辞書キー（`ContentLocalizationKey`。アイテム名・研究名・チャレンジのカテゴリ名）・解決済みの文（サーバーのモーダル要求の題・本文・ボタン文言）の 3 種を運ぶ。文言を出す部品の API は `UiText` を受ける（`LocalizationKey` だけを受ける API は、辞書キー以外を出さないと Web で確かめた部品に限る）`[A-40]` | stage1-asbuilt §6.4 の重複。`Localize.GetContent` は通常キーと別の口で、`GameContentPresentation.ItemName` と `FormButton` のモーダル文言が `UiText` で表せない | F1 |
| C10 | `PauseMenuScreenPresenter.IsShownOn` が `MainPanelOwner == GameUi` を見る | 全体スイッチの作り直し（albof.17）で、Presenter はスイッチを見ない。UITK モードのときだけ composition root が Presenter を登録する（§4.4） | 分岐を 1 か所に集める（AGENTS.md「同種の条件分岐は一箇所へ」） | albof.17 |

段階 1 の「本番未使用の基盤」（`TutorialAnchorRegistry`・`UiInertiaAnimation`・`TranslateYTweenStep`・`ScrollOffsetTweenStep`・`UiNumberFormat`・`GamePanel`）は、§2 の表の使い手の段で本番から使われる。

---

## 2. 部品カタログ

### 2.1 読み方と共通の約束

- **列**: 部品（UITK）／責務／公開 API 案（C#）／USS・状態クラス／使う画面（Web の feature）／作る段階・レーン（§5.2 のレーン ID）／Web の見た目の正本／段階 1 との関係（再利用・改修・新規）
- **公開 API の共通の約束**（AGENTS.md に合わせる）
  - ctor は `(string testid, …)`。装飾だけの部品（`FadeRule` 等）は testid を取らない。デフォルト引数は使わない
  - Web が testid を子（ラベル）に付けている部品は、ctor の第 1 引数をその子の testid にし、名前を `<子>Testid` とする `[A-24]`
  - 表示する値は `SetXxx(値)` で外から入れる。値を返す素通しプロパティは作らない（`{ get; private set; }` は可）
  - 操作は `IObservable<T>`（UniRx の `Subject` を中に持つ）で出す。`Action`・`Func<>`・`partial` は使わない
  - ボタン類は `focusable = false`（段階 1 の規則）。入力欄だけがフォーカスを持つ
  - `contentContainer` を持つ部品（枠・スクロール）は、子を `Add` で受ける
  - 色・寸法は C# に書かない。C# が値を要るとき（Painter2D）は `UssCustomStyleReader` でトークンを読む
- **表示素材の値型**（Foundation/Icons と State/Text に置く）

```csharp
// アイコン 1 つ分の表示素材。テクスチャが無い（未ロード・欠け）ときの代わりの文字も持つ
// Display material for one icon; carries fallback text for a missing or unloaded texture
public sealed class UiIcon
{
    public Texture2D Texture { get; }          // 外部ロード結果なので null を許す（AGENTS.md の null 規約の例外側）
    public string IdText { get; }              // GameIcon の fallback=idText 用
    public UiText Label { get; }               // GameIcon の fallback=label 用
    public UiIcon(Texture2D texture, string idText, UiText label) { … }
}

// Content 層（ID → 表示素材）。Screens の Presenter と View が使う
// Content layer (ID → display material), used by Screens presenters and views
public sealed class GameContentPresentation
{
    public UiIcon Item(ItemId id);
    public UiIcon Block(BlockId id);
    public UiIcon Fluid(FluidId id);
    public Color FluidFace(FluidId id);        // FluidSlot の背面フィル色（液体マスタ色）
    public UiText ItemName(ItemId id);        // UiText.FromContent(ContentLocalizationKeys.ItemName(guid))
}
```

- **`UiText` の 3 種** `[A-40]`: `new UiText(LocalizationKey key[, params])`（辞書キー）・`UiText.FromContent(ContentLocalizationKey key)`（コンテンツ辞書キー。`Localize.GetContent` で解く）・`UiText.FromVerbatim(string text)`（解決済みの文。訳さない）。`Resolve()` が種ごとに解き、`LocalizedLabel`・`LocalizedTextBinding` は言語変更で同じく解き直す（解決済みの文は変わらない）。`WithLeadingParam` は辞書キーにだけ効き、ほかの種で呼ぶと例外で止める（F1 計画 Task 5 Step 3b）

### 2.2 枠・面族（`Components/Panels/`）

| 部品 | 責務 | 公開 API 案 | USS・状態 | 使う画面 | 段階・レーン | Web の正本 | 段階 1 |
|---|---|---|---|---|---|---|---|
| `GamePanel` | 枠付きパネル（default・craft）。題・題の右端の副次アクション・本文 | `GamePanel(string testid, GamePanelFrame frame)`／`SetTitle(UiText title)`／`SetTitleAction(VisualElement action)`／`contentContainer` | `game-panel.uss`。`.game-panel--default`・`--craft`・`--titled`。面は `GamePanelFace`（頂点カラー）、罫線は `FadeGradientRule`（FadeRule の基底。C2-a 計画 D-14）、三角 `GamePanelBottomDeco`、craft の右下グリップは `GamePanelCraftGrip`（頂点カラー）、内周線は USS の border の入れ子 `[A-26]`。外周の影は描かない `[A-22]`。つまみ `--panel-top/right/bottom/left`・`--title-shift-x`・`--title-scale-x/y` `[A-25]` | blockInventory・research・recipe・inventory・buildMenu（default）、modeHud（craft） | default は段階 1 済み → C2-a で改修（C7）。craft は C2-a（modeHud が段階 2 で使う。plan §3.4 の段階 4 から前倒し `[A-12]`） | `W/shared/ui/GamePanel/style.module.css`・ref-webui-design §2 | 改修 |
| `GamePanelSurface` | 枠なしの面（hud・skit） | `GamePanelSurface(string testid, GamePanelSurfaceKind kind)`／`contentContainer` | `.game-panel-surface--hud`（`--hud-panel-padding`・4 辺 `--panel-edge-fade`）・`--skit`（`--skit-window-*`）・`--skit-choice`（持ち主のボタンいっぱい・左右だけの水平フェード・ボタンの `:active` で `--skit-choice-active-face`。S2-skit が足す `[A-84]`） | challenge HUD（hud）、skit（skit・skit-choice） | C2-a | `GamePanel/hudVariant.module.css`・components.md §8.12 | 新規（`GamePanelFace` を流用） |
| `PaperSurface` | Mantine `Paper` の写し（枠なし・影なし） | `PaperSurface(string testid)`／`contentContainer` | `.paper-surface`。面と角丸はつまみ `--paper-face`・`--paper-radius`（既定 `--mantine-color-body`・`--mantine-radius-sm`）`[A-23]` | challenge のノードカード・trainHud | C2-a | Mantine Paper（`W/features/challenge/ChallengeNodeCard`・`trainHud`） | 新規 |
| `DialogFrame` | Mantine `Modal` の枠（題・本文・操作列）。幕とフォーカスは Foundation の `UiModalHost` | `DialogFrame(string testid)`／`SetTitle(UiText)`／`contentContainer`／`AddAction(FormButton button)` | `.dialog-frame`。幅 `--modal-size`（Mantine md 440px。Web の `w={320}` は効いておらず参照の実寸は 440 `[A-77]`）・余白・文字は Web と同じく実画面 px（`UiModalHost` が `UiScreenPixelScope` の中に置く。舞台の倍率を掛けない）`[A-41]` | modal（BP 名）、eventLanguageGate | C3-b | Mantine Modal（`W/features/modal/ModalHost`） | 新規 |
| `PauseMenuFrame`（画面内に留める） | Web `.panel`（420px・角丸 12px・padding 32px）の写し | 段階 1 のまま | `PauseMenu.uss` | pauseMenu（設定・バグ報告も） | 済み | `W/features/pauseMenu/style.module.css` | 再利用 |

### 2.3 スロット族（`Components/Slots/`）

Web と同じ 2 層（枠と状態の `SlotFrame`＋中身の `SlotContent`）にする（web-components §0-2。`composes` の実測）。

| 部品 | 責務 | 公開 API 案 | USS・状態 | 使う画面 | 段階・レーン | Web の正本 | 段階 1 |
|---|---|---|---|---|---|---|---|
| `SlotFrame` | 1 マスの枠・ベベル・面と 5 状態、マウスの身振り | `SlotFrame(string testid)`／`SetState(SlotFrameState state)`／`IObservable<SlotGesture> OnSlotGestured`／`contentContainer` | `.slot-frame--selected`・`--filled`・`--catalog`・`--insufficient`・`--ghost`。リングは寸法に効かない装飾子 `.slot-frame__ring`（`PickingMode.Ignore`・絶対配置）の子の border の帯 4 本（`__band--1..3`・`__face-inset`）で描き、重なりは box-shadow と同じ先頭が最前。選択の白面の下端の帯 `__selected-trim` は `bottom`・`height` を USS の `var()` で受ける `[A-51]`。つまみ `--bevel-inset` で装飾子の外周を切り替える: 既定 `outset`＝面の外側へ成長（持ち物・レシピ。白面の寸法を削らない）、`inset`＝箱の内側へ食い込む（ホットバー。固定ピッチの外へ出ない）。Web の既定は空文字（`W/shared/ui/SlotFrame/style.module.css:3-9,27-31`・`W/features/hotbar/HotbarPanel/style.module.css:48`）`[A-42]`。つまみ `--slot-size`・`--bevel-inset`・`--bevel-1..3`・`--bevel-c1..3`・`--sel-c1..3`・`--face-inset`・`--selected-face-*` | hotbar・buildMenu（直接）、ItemSlot・BlockSlot・FluidAmountSlot の中 | C3-a | `W/shared/ui/SlotFrame/style.module.css`・ref-webui-design §4 | 新規 |
| `SlotGesture`（値型）／`SlotFrameState`（値型） | 左押下（Shift 付き）・右押下・左/右の進入・ダブルクリック・ホバーの変化 ／ 5 状態の組 | `readonly struct SlotGesture { SlotGestureKind Kind; bool Shift; }`、`readonly struct SlotFrameState { bool Selected, Filled, Catalog, Insufficient, Ghost; }`（ctor で全部受ける） | — | — | C3-a | `W/shared/ui/useSlotMouse.ts` | 新規 |
| `SlotContent` | マス内の中身（アイコン 100%・padding・右下の個数・縁取り） | `SlotContent()`／`SetIcon(UiIcon icon, GameIconFallback fallback)`／`ClearIcon()`／`SetCount(int count)`／`ClearCount()` | `.slot-content__icon`・`__count`。つまみ `--icon-pad`・`--count-bottom`・`--count-font-size`・`--count-letter-spacing`。個数の縁は `OutlinedCountLabel`（縁だけの Label を後ろに敷く 2 層。`-unity-text-outline` は字の内外に半分ずつ掛かり細い字を塗りつぶすため。`[A-110]`） | ItemSlot・BlockSlot・FluidAmountSlot・PlacementTargetFace の中 | C3-a | `W/shared/ui/slotContent.module.css`・ADR 0033 | 新規 |
| `OutlinedCountLabel` | アイコンの上に白い縁で描く数（個数・所持/必要）。Web `.iconTextOutlineLight` の写し | `OutlinedCountLabel(VisualElement parent, string placementClass, params string[] layerClasses)`／`SetText(UiText text)`。VisualElement 派生でなく、置く側の要素へ Label 2 枚（`--stroke`・`--glyph`）を兄弟で足す `[A-110]` | `.outlined-count-label`・`--stroke`・`--glyph`。つまみ `--outlined-count-label-color`・`--outlined-count-label-size` | SlotContent・ItemSlot の中 | C3-a | `W/app/tokens.css` の `.iconTextOutlineLight`・ADR 0033 | 新規（済み C3-a） |
| `GameIcon` | テクスチャ 1 枚の表示と、欠けたときの代わり（idText・label・none） | `GameIcon()`／`SetIcon(UiIcon icon, GameIconFallback fallback)`／`ClearIcon()` `[A-50]` | `.game-icon--image`・`--fallback`・`.game-icon__fallback`（10px・`--mantine-color-dark-2`） | SlotContent の中 | C3-a | `W/shared/ui/GameIcon` | 新規 |
| `ItemSlot` | アイテム 1 マス（個数・不足表示・ゴースト・ツールチップ） | `ItemSlot(string testid)`（testid は中の SlotFrame の name）／`SetContent(ItemSlotContent content)`／`IObservable<SlotGesture> OnSlotGestured`（filled・ghost・バッジ・ツールチップは中で決める）`[A-50]` | `.item-slot__shortage`（つまみ `--shortage-count-right/bottom/font-size`、`item-slot--lack` で `--text-insufficient`）。不足の個数も `OutlinedCountLabel` `[A-110]`。`shortage` があれば通常の個数を出さない | blockInventory・research・recipe・inventory・buildMenu | C3-a | `W/shared/ui/ItemSlot`・ref-webui-design §4 | 新規 |
| `ItemSlotContent`（値型） | ItemSlot に入れる値の束 | `sealed class`：`UiIcon Icon`（null＝空きマス）／`int? Count`／`ItemSlotMarks Marks`（selected・catalog・insufficient）／`ItemSlotShortage Shortage`（null 可。`Tooltip` を持つ）／`ItemSlotGhost Ghost`（null 可）／`UiText TooltipOverride`（null 可）`[A-50]` | — | — | C3-a | ItemSlot の props | 新規 |
| `BlockSlot` | ブロック 1 マス（名前のツールチップ） | `BlockSlot(string testid)`（testid は中の SlotFrame の name）／`SetBlock(UiIcon icon, UiText name)`（name 必須）`[A-50]` | — | research | C3-a | `W/shared/ui/BlockSlot` | 新規 |
| `PlacementTargetFace` | 配置対象のアイコンと 10px のラベル（SlotFrame の中身） | `PlacementTargetFace()`／`SetIconTarget(UiIcon icon, UiText displayName)`／`SetNameTarget(UiText displayName)` `[A-50]` | `.placement-target-face__label` | hotbar・buildMenu | C3-a | `W/shared/ui/PlacementTargetFace` | 新規 |
| `SlotGrid` | 列数固定の格子（`width: fit-content` の写し） | `SlotGrid(string testid, int columns)`／`SetColumns(int columns)`／`AddItem(VisualElement item)`／`ClearItems()` `[A-50]` | `columns` 個ずつの行（`.slot-grid__row(--first)`・`__cell(--row-start)`）に積み、間隔は `--slot-grid-gap`・`--slot-grid-row-gap` を USS だけで当てる。C# は幅を計算しない | blockInventory・buildMenu・recipe・inventory | C3-a | `W/shared/ui/SlotGrid` | 新規 |
| `OutlinedCountLabel` | 白（または黒）の縁付きの数字（Web `.iconTextOutlineLight/Dark`）。UITK の `-unity-text-outline` は字の内外へ半分ずつ掛かるので、縁だけの層を後ろに敷き縁の無い字を上に重ねる 2 層 | `OutlinedCountLabel(VisualElement parent, string placementClass, params string[] layerClasses)`／`SetText(UiText text)` | `.outlined-count-label`。大きさはつまみ `--outlined-count-label-size`、置き場・字間は置く側のクラス。基線は `translate: 0 18.1%`（Web の line-height: 1 に合わせる）。縁の色はつまみ `--outlined-count-label-stroke`（既定 `--icon-text-stroke-light`。`.iconTextOutlineDark` の置き場は dark に上書き）`[A-97]` | SlotContent の個数・ItemSlot の所持/必要・FluidSlot・FluidAmountSlot の量・ホットバーの番号 | C3-a（縁の色のつまみは C4-a） | `W/app/tokens.css` の `.iconTextOutlineLight`/`.iconTextOutlineDark` | 新規 |
| `FluidSlot` | 液体 1 マス（背面フィル＝液体色・絵・量 12px 太字の白に黒縁・ゴースト） | `FluidSlot(string testid)`／`SetFluid(FluidSlotContent content)`。判別 union `FluidSlotContent`（`Empty`／`Ghost(UiIcon icon, double amount)`／`Filled(UiIcon icon, double amount, double capacity, Color face)`。sealed 派生 `EmptyFluidSlotContent`・`GhostFluidSlotContent`・`FilledFluidSlotContent`） | 48px 固定・`--mantine-color-dark-4` の線・`--mantine-color-dark-8` の面（Web どおり Mantine 変数）。つまみ `--fluid-slot-ghost-opacity`・`--fluid-slot-amount-bottom`。量の縁は `OutlinedCountLabel`（`--outlined-count-label-stroke` を dark に） | FluidSlotRow の中 | C4-a | `W/shared/ui/FluidSlot` | 新規（`Slots/Fluid/FluidSlot/`） |
| `FluidAmountSlot` | 白面の SlotFrame に液体アイコンと量の帯 | `FluidAmountSlot(string frameTestid)`／`SetFluid(UiIcon icon, SlotAmountBadge badge)`／`IObservable<SlotGesture> OnSlotGestured`。判別 union `SlotAmountBadge`（`Show(double amount)`／`Hidden`。sealed 派生 `ShownSlotAmountBadge`・`HiddenSlotAmountBadge`） | 量 `--fluid-amount-font-size`(10px)・字間 0 は置き場に直書き（Web の `.amount.amount`）、下端 `--count-bottom`。帯は `name="<frameTestid>-amount"` で Hidden のとき木から外す | blockInventory・recipe | C4-a | `W/shared/ui/FluidAmountSlot` | 新規（`Slots/Fluid/FluidAmountSlot/`） |
| `FluidSlotRow` | 液体スロットの列＋進捗矢印 | `FluidSlotRow(string testid)`／`SetFluids(IReadOnlyList<FluidSlotContent> fluids)`／`SetProgress(float ratio01)`／`ClearProgress()` | `UiGroup(UiGap.Xs)`。子の testid は固定の `fluid-slot`・`progress-arrow-bar`。0 マスは `display: none` | blockInventory | C4-a | `W/shared/ui/FluidSlotRow` | 新規（`Slots/Fluid/FluidSlotRow/`） |
| `SlotAmountFormat`（static。`Client.Ui.State/Text/SlotAmountFormat.cs`） | 量の短縮表記 | `static string Format(double amount)` | — | research・blockInventory・液体スロット・素材ツールチップ | L0-s3 が作る（`slotAmountFormat.test.ts` 7 件も L0-s3）。C3-a は使うだけ `[A-19]` | `W/shared/ui/slotAmountFormat.ts` | 新規 |

世界の上に直に置く画面（ホットバー）は、画面ルートで `--slot-frame-face` を世界の地向けの値に上書きし、opacity の減光も Linear 合成で淡く出る分を補正する。値はランタイムの PanelSettings に明るい地・暗い地を描いて Web と画素で照合して決める `[A-108]`。

### 2.4 操作部品族（`Components/Controls/`）

| 部品 | 責務 | 公開 API 案 | USS・状態 | 使う画面 | 段階・レーン | Web の正本 | 段階 1 |
|---|---|---|---|---|---|---|---|
| `PanelActionButton` | パネル付随の副次アクション | `PanelActionButton(string testid, LocalizationKey label)`／`IObservable<Unit> OnClicked` | つまみ `--panel-action-button-width/height/font-size`。hover は `DerivedTokens` の前計算色 | pauseMenu・blockInventory・inventory | 済み → F1 で `StyleSheet` 引数を消し `OnClicked` を足す（C1・C8） | `W/shared/ui/PanelActionButton`・components.md §8.6 | 改修 |
| `ModeSwitch<TValue>` | 択一のボタン列（横・縦・タブ）`[A-79]` | `ModeSwitch(string testid, ModeSwitchOrientation orientation, ModeSwitchRole role, IReadOnlyList<ModeSwitchOption<TValue>> options)`／`SetSelected(TValue)`／`SetSelectedNone()`／`SetEnabledAll(bool)`／`SetOptionEnabled(TValue value, bool enabled)`／`IObservable<TValue> OnOptionSelected` | `--vertical`・`--tablist`・`__option--selected`・`--option-disabled`。つまみ `--mode-switch-option-height` | settings・pauseMenu（横）、buildMenu（縦）、blockInventory（タブ） | 横は済み。縦・タブ・個別無効は C3-b | `W/shared/ui/ModeSwitch`・§8.6・§8.22 | 改修（`OnSelected` → `OnOptionSelected` に改名、`role` を足す） |
| `IconButton` | 面なしの小さいアイコンボタン（既定は ×。× は Painter2D の `IconButtonCloseGlyph`。ほかの図形は画面が `VisualElement` で作って渡す） | `IconButton(string testid, VisualElement glyph)`／`IObservable<Unit> OnClicked`（図形は `IconButton.GlyphColorProperty`・`GlyphStrokeProperty` を `CustomStyleResolvedEvent` で読んで描く）`[A-27]` | つまみ `--icon-button-size`・`--icon-button-icon-size`。有効時の hover で `--text-high-contrast` | skit（ツールバー 4）・blockInventory・buildMenu | C2-b（plan §3.4 は段階 1 だが未作成。skit が段階 2 で使う） | `W/shared/ui/IconButton` | 新規 |
| `FormButton` | Mantine `Button` の写し（送信・OK・キャンセル） | `FormButton(string testid, UiText label, FormButtonVariant variant)`／`SetLabel(UiText label)`（モーダルのボタン文言は解決済みの文、チャレンジのカテゴリ名はコンテンツ辞書キー `[A-40]`）／`SetVariant(FormButtonVariant variant)`／`SetSize(FormButtonSize size)`／`IObservable<Unit> OnClicked`（busy 表示の Set は持たない）`[A-28]` | `FormButtonVariant { Filled, Danger, Subtle }`・`FormButtonSize { Small, ExtraLarge }`（Web で実際に使う組だけ）。`.form-button--filled`・`--danger`・`--subtle`・`--xl`。色は `MantineTokens`（Subtle のホバーは `DerivedTokens`）。モーダルの確定色は State の `ModalButtonTone` を View が 1 か所で写す（`Blue→Filled`・`Red→Danger`） | pauseMenu（バグ報告）・modal・challenge・eventLanguageGate（recipe のクラフトは `RecipeActionButton` `[A-46]`） | C2-b | Mantine Button（`W/features/pauseMenu/BugReportForm`・`modal/ModalHost`・`challenge/ChallengePanel`・`eventLanguageGate`） | 新規（`BugReport.uss` から移す。C2） |
| `RecipeActionButton` | 主要アクションの青グラデのボタン（クラフト・研究する）。角丸 0・横 3 色グラデの面・文字 `--text-high-contrast` | `RecipeActionButton(string testid, UiText label, RecipeActionLabelWeight weight)`（`Medium` は 500 の字形〈クラフト〉・`Regular` は既定の 400〈研究〉`[A-123]`）／`SetLabel(UiText label)`／`SetEnabled(bool)`（`VisualElement` の既定）／`IObservable<Unit> OnClicked`。長押しは置く側が Foundation の `UiPressGesture` を重ねる | 面は拾わない子が 16 列の頂点カラーの横 3 色グラデ（sRGB で補間）で描く。色はつまみ `--recipe-action-face-start/middle/end`（既定 `--recipe-action-background-*`）・`--recipe-action-hover-start/middle/end`（`#2680dd`→`#45bcf5`→`#6cebfa`）・`--recipe-action-disabled-start/middle/end`（既定は通常と同じ）・`--recipe-action-text`・`--recipe-action-disabled-text`・`--recipe-action-disabled-opacity`。寸法・字・縁・沈みはつまみ `--recipe-action-width/height/padding-block/font-size/letter-spacing/text-outline-width/text-outline/active-offset`（4 方向 1px の影は文字の縁で近似。LX-93）。研究は画面が祖先のクラスで寸法（幅・高さは研究の画面が撮って決める）と、無効の面 3 つ（`--research-action-disabled-face`）・文字（`--text-default`）・opacity（`--research-action-disabled-opacity`。Web 0.6 の Linear 合成の補正値を C4-c が決める）を上書きする `[A-123]` `[A-126]` | recipe（クラフト）・research（研究する） | C4-c | `W/features/recipe/views/RecipeActionButton.module.css`・`RecipeBox.module.css:186-212`・`W/features/research/style.module.css:131-148`・`W/app/tokens.css:104`・components.md「主要アクションの青グラデ」 | 新規 |
| `GameTextField` | 入力欄（単行・複数行・IME） | 段階 1 の API ＋ `GameTextField(string testid, bool multiline, GameTextFieldLook look)`／`SetPlaceholder(UiText)`（言語追従は Foundation の `LocalizedTextBinding`。置き文字を持つ欄だけが束縛を作る）`[A-29]`・`[A-31]` | `GameTextField.uss`（`--gauge-inset`・`--mantine-input`）。GaugeInset は面 `--gauge-track`＋`--bevel-c1` 枠・placeholder `--text-muted`・フォーカスで外側に `--bevel-1` の輪、MantineInput は Mantine TextInput の dark・sm | pauseMenu（バグ報告）・blockInventory（駅名）・buildMenu（検索）・modal（BP 名） | C2-b（C2） | `W/shared/ui/textAreaField.module.css`・buildMenu `.searchInput`・§8.9・Mantine TextInput | 改修 |
| `GameScrollView` | 本文いっぱいに伸びるスクロール領域とカスタムバー（`ScrollView` 派生。バーは本文に重ね、本文の幅を削らない）。スクロール量の通知 | `GameScrollView(string testid)`／`contentContainer`／`ScrollTo(VisualElement target)`／`SetScrollOffset(float y)`／`IObservable<float> OnScrollOffsetChanged` | トラック `--gauge-track`・ノブ `--bevel-c2`・ホバー `--game-scroll-view-thumb-hover`。つまみ `--scrollarea-scrollbar-size`（Mantine ScrollArea の `scrollbarSize`。`--recipe-list-scrollbar-reserve` は `RecipeListScrollView` のもの）`[A-35]`。つまみの幅は Web 7.2px（size 12 − 内側 2.4×2）だが、UITK はレイアウトが整数 px に丸まり実描画は幅 8px・内側 2px（指定値は 2.4px のまま。C2-c 実測。LX-42）。ホイール 1 ノッチは Web（CEF）と同じ 60px `[A-71]` | research・blockInventory・train・pauseMenu・buildMenu・recipe | C2-c（plan §3.4 の段階 1 の「スクロール領域」。C3。実行計画 E-7） | `W/shared/ui/panelBodyScrollArea.module.css`・§8.10 | 新規（PauseMenuSubPageView から移す） |
| `RecipeListScrollView` | 先頭の固定内容＋レシピ列のスクロール（bleed とバーの逃げ） | `RecipeListScrollView(string listTestid)`（列の `name` は `listTestid`、中の `GameScrollView` は `<listTestid>-scroll`）／`SetLeadingContent(VisualElement)`／`ClearLeadingContent()`／`SetEntries(IReadOnlyList<VisualElement>)` `[A-124]` | `--recipe-entry-bleed`・`--recipe-list-scrollbar-reserve`。右の余白（Web `calc(bleed + reserve)`）は C# が 2 つのつまみを読んで足しインラインで当てる `[A-47]`。行の列は `UiStack(UiGap.None)` で、部品 USS が 2 クラスで `--ui-gap: var(--recipe-list-gap)` に上書きする（既定 `--recipe-entry-gap`、機械のレシピ選択は画面が `--machine-recipe-row-gap` へ上書き）`[A-124]` | blockInventory・recipe | C4-c | `W/shared/ui/RecipeListScrollArea` | 新規（`GameScrollView`・`UiStack` を内包） |

### 2.5 表示・ゲージ族（`Components/Gauges/`）

| 部品 | 責務 | 公開 API 案 | USS・状態 | 使う画面 | 段階・レーン | Web の正本 | 段階 1 |
|---|---|---|---|---|---|---|---|
| `GaugeBar` | 0..1 の横ゲージ | `GaugeBar(string testid)`／`SetValue(float ratio01)` | 高 `--slot-size/6`（C# が祖先のつまみから割る `[A-47]`。C2-c 計画 Task 5）。つまみ `--gauge-fill`（progress が白へ上書き。§8.18） | blockInventory・progress | C2-c | `W/shared/ui/GaugeBar`・§8.6 | 新規 |
| `BarSeries` | 0..1 の値を古い順に縦棒で並べる時系列（null は溝だけ） | `BarSeries(string testid)`／`SetValues(IReadOnlyList<float?> ratios01)`（棒の数は値の数に合わせる。NaN と範囲外は 0..1 へ丸める）`[A-85]` | 棒は `GaugeBar` と同じ器（`--gauge-track`・内周の線 `--gauge-outline-width`／`--bevel-c1`・充填 `--gauge-fill` を底から高さ %）。null の棒は `bar-series__bar--empty`（印だけ）、隙間は先頭以外の左の margin（先頭は `--leading`）。つまみ `--bar-series-height`・`--bar-series-bar-width`・`--bar-series-gap` | blockInventory（ベルト搬出量） | C4-b | `W/shared/ui/BarSeries`・§8.6・§8.23 | 新規（`Gauges/BarSeries/`） |
| `ProgressArrowGlyph` | 矢印そのものがゲージ（作った直後と `SetIdle` は静止） | `ProgressArrowGlyph(string testid)`／`SetProgress(float ratio01)`／`SetIdle()`。`SetProgress(0)` は静止でなく幅 0 の進捗 `[A-87]` | 寸法 `--craft-arrow-width`×`--craft-arrow-height` を部品だけが持ち viewBox 121×78 を `xMidYMid meet` で当てはめる。`__view-box` の中に影・溝（`--gauge-track`）・充填の切り抜き（中に `--color-content-primary`。`--gauge-fill` に従わない）・輪郭（`--craft-arrow-outline`・viewBox の 2 単位・miter）の順。path は Painter2D、drop-shadow は輪の頂点カラー（`--progress-arrow-glyph-shadow`）`[A-88]`。静止は `--idle` で切り抜きを隠す | blockInventory（機械の加工行）・recipe（`RecipeRow`） | C4-b | `W/shared/ui/ProgressArrowGlyph`・§8.13 | 新規（`Gauges/ProgressArrowGlyph/`） |
| `ProgressArrowBar` | 0..1 を幅で満たす帯状のゲージ（名前に反して矢印形ではない） | `ProgressArrowBar()`／`SetValue(float ratio01)`。`name` は Web が固定で付ける `progress-arrow-bar` `[A-86]` | 幅 `--progress-arrow-width`・高 `--progress-arrow-height`、器は `GaugeBar` と同じ（`--gauge-track`・内周の線・`--gauge-fill` を幅 %）。出し入れは使い手 | blockInventory（採掘機）・`FluidSlotRow` | C4-b | `W/shared/ui/ProgressArrowBar` | 新規（`Gauges/ProgressArrowBar/`） |
| `Spinner` | Mantine `Loader` の写し | `Spinner()` | UiMotion の回転 | Web の 1 か所（レーンで出所を確かめる） | 使う段のレーン | Mantine Loader | 新規 |
| `ToastItem` | Mantine Notification の写し | `ToastItem(string text, ToastItemTone tone)`（`Client.Ui.State.Toast` を知らない）`[A-18]` | `ToastItem.uss`（`.toast-host` は `Screens/Toast/ToastHost.uss`） | toast | 済み → F1 | Mantine Notification | 改修 |

### 2.6 文字・装飾族（`Components/Text/`・`Components/Decor/`）

| 部品 | 責務 | 公開 API 案 | USS・状態 | 使う画面 | 段階・レーン | Web の正本 | 段階 1 |
|---|---|---|---|---|---|---|---|
| `FadeRule` | 横グラデの区切り線（装飾語彙 1） | `FadeRule()` | 高 2px、`rgb(128 132 159)` の 30→42→26% 横グラデ（頂点カラー。α は `DerivedTokens` で補正） | buildMenu・blockInventory・skit・modeHud・challenge | C2-a | `W/shared/ui/FadeRule`・ref-webui-design §6 | 新規 |
| `HudSectionHeading` | 小ラベル → FadeRule → 本文リストの HUD 見出し（Web で 2 か所が同形に重複） | `HudSectionHeading(string labelTestid, LocalizationKey label)`／`contentContainer` `[A-24]` | つまみ `--hud-label-font-size`・`--hud-label-letter-spacing`・`--hud-label-rule-gap`・`--hud-rule-list-gap`・`--hud-rule-width` を、Web の `--challenge-hud-*` と `--operation-hud-*`（同名同値）から受け直す | challenge HUD・modeHud | C2-a | `W/features/challenge/CurrentChallengeHud.tsx:21-32`・`modeHud/PlacementModeHud.tsx:20-29` | 新規（Web では未部品化。§5.1） |
| `KeyCap` | kbd 1 個 | `KeyCap(string keyText)`／`SetKeyText(string keyText)` | `.key-cap` は枠なし・色はつまみ `--key-cap-color`（既定 `var(--text-high-contrast)`。KeyControlHint が上書き。S2-tutorial が足す `[A-64]`）だけで字形は継ぐ。keyHintText の大きさ・字間・影は置く側の `UiTypographyClass.KeyHintText` `[A-34]` | keyHint・tutorial（KeyControlHint）・buildMenu | C2-c（実行計画 E-7。plan §3.4 は段階 1 だが未作成） | `W/app/tokens.css` の `:where(.keyHintText)` | 新規 |
| `ShortcutHintLabel` | 文中に KeyCap を差し込む文 | `ShortcutHintLabel(string testid, ShortcutHintLayout layout)`／`SetHint(UiText sentence, string shortcutText)`（Web と同じく kbd は 1 つ。layout は prefix/inline）`[A-34]`。Prefix は文に印の引数を足さず、コンテンツ辞書キー・解決済みの文もそのまま受ける（S2-tutorial が直す `[A-64]`） | つまみ `--shortcut-hint-label-gap`（既定 0） | keyHint・tutorial | C2-c（実行計画 E-7） | `W/shared/i18n` の `LocalizedShortcutHint` | 新規 |
| `TooltipFace` | ツールチップの面（`--tooltip-face`・`--tooltip-border`・`pre-line`） | `TooltipFace(string testid, TooltipFaceLook look)`／`SetText(UiText text)`／`SetLines(IReadOnlyList<UiText> lines)`（行の testid は `<testid>-line`。Cursor は角丸 `--mantine-radius-default`、Hover は 0）`[A-33]` | `--tooltip-max-width/padding/font-size`。UITK は折り返した面を最長行の幅へ縮めるため、折り返すかを文の幅で判定し、折り返すときだけ最大幅 320px を幅として当てる（Web は shrink-to-fit で 320px。C2-c） | CursorTooltip（画面）・HoverTooltip（Foundation の宿主） | C2-c | `W/shared/ui/HoverTooltip`・`W/shared/tooltip`・ADR 0019 | 新規 |
| Typography のクラス | Mantine `Text`/`Title` の size・c・fw の写し | 部品は作らず、`UiTypographyClass` の定数（`TextSm`・`TextXs`・`TextDimmed`・`TitleOrder3`・`TextBold` 等）を `LocalizedLabel` に付ける | `Typography.uss`。値は Mantine の既定（xs 12・sm 14・md 16・xl 20。Title は 34/26/22/18。行の高さも）`[A-39]`。Title は Web と同じ px で、フォントの字幅の差は letter-spacing で寄せる `[A-37]`。太さは Bold/Medium の字形 `[A-36]`。USS に line-height は無いので、1 行の文は min-height＝字×行高で写し（C2-c R2）、2 行以上の文の行送りは C2-c 計画 Task 3 Step 6 の実測で決めた方法（リッチテキストの `<line-height>` か、行ごとの Label を行高で固定）で行高トークン（`--challenge-hud-objective-line-height` 等）と 1px 以内にする。方法を使う最初の画面は S2-hud `[A-48]` | Text 42・Title 9 か所 | C2-c（実行計画 E-7） | Mantine Text/Title | 改修（今は `font-size: 14px` の 1 規則） |
| Layout のクラス | Mantine `Stack`/`Group` の写し | `UiStack(UiGap)`・`UiGroup(UiGap)`（`AddItem`・`SetItemShown`・`ClearItems`・`SetGap`）・`UiLayoutClass`（揃え） | `Foundation/Theme/Layout.uss`（新設）。gap は USS に無いので子の margin で表す。`UiStack`/`UiGroup` が `--ui-gap` を読み、表示中の先頭以外の子へインラインの margin で当てる（構造擬似クラスは禁止、クラス方式は部品の `margin: 0` と詳細度で負ける）`[A-32]` | Stack 33・Group 10 か所 | C2-c（実行計画 E-7。F1 から移す `[A-18]`） | Mantine Stack/Group | 新規 |

### 2.7 事実上の共有部品と、画面に留めるもの

**Web では feature の中にあるが 2 feature 以上が使うもの（Components へ直接作る）**

| 部品 | 置き場 | 公開 API 案 | 使う画面 | 段階・レーン | Web の正本 |
|---|---|---|---|---|---|
| `RecipeRow` | `Components/Recipe/` | `RecipeRow(string testid, string arrowTestid)`／`SetSlots(IReadOnlyList<VisualElement> materials, IReadOnlyList<VisualElement> results)`（画面が作った `ItemSlot`・`FluidAmountSlot`。操作は画面がスロットの `OnSlotGestured` を直接購読し、行は中継しない `[A-120]`。スロットの寸法はつまみの継承で上限のまま、はみ出しは LogError `[A-121]`）／`SetDuration(UiText)`／`SetProgress(float)`／`SetIdle()`／`SetAction(VisualElement)`／`ClearAction()`。光彩・外周の輪・括弧の影・四隅の括弧は拾わない子 `RecipeRowFrame` が頂点カラーで描く `[A-122]` | recipe・blockInventory（機械のレシピ選択） | C4-c | `W/features/recipe/views/RecipeRow.tsx`・`RecipeBox.module.css`・`logic/recipeSlotLayout.ts`・§8.17 |
| `LevelInheritanceBadge` | `Components/Recipe/` | `LevelInheritanceBadge(string testid)`／`Show(UiText)`（文は画面が State の `LevelInheritanceBadgeText.TryBuild` で作る。testid は文のラベルが持つ）／`Hide()`（ラベルを外し testid の要素を残さない） `[A-125]` | 同上 | C4-c | `W/features/recipe/views/LevelInheritanceBadge.tsx` |
| インベントリの列数 | トークン `--inventory-columns`（既存）を `UssCustomStyleReader` で読む | — | inventory・blockInventory | C4（画面） | `W/features/inventory/inventoryGridColumns.ts` |
| ドラッグ源 | Foundation `UiDragDrop` | `UiDragDrop.MarkSource(VisualElement source, IUiDragPayload payload)`／`MarkTarget(VisualElement target, IUiDropTarget handler)`／`UnmarkTarget`／`TryFindTarget`。`IUiDragPayload { OnTapped(); OnDropped(IUiDropTarget); OnDroppedOutside(); }`。ゴーストは出さない `[A-78]` | hotbar・buildMenu | C3-b | `W/features/hotbar/useHotbarDragSource.ts` |
| 配置対象の絵の出どころ | `Client.Ui.State/PlacementTarget/`（ID だけの判別 union と解き方）・`Client.Ui/Content/PlacementTargetIcons`（値への変換） | `PlacementTargetIcon`（`PlacementTargetBlockIcon(BlockId)`・`PlacementTargetTrainCarIcon(Guid)`・`PlacementTargetConnectToolIcon(Guid)`・`PlacementTargetBlueprintIcon(Guid)`・`PlacementTargetNameOnlyIcon.Instance`。値で等価。Blueprint だけは参照の等価で、撮り直しで絵が変わるため）／`PlacementTargetIconResolver.Resolve(IPlacementTarget, IBlueprintThumbnailLookup)`／`PlacementTargetIcons.TryToUiIcon(PlacementTargetIcon, out UiIcon)`（名前だけ・絵の欠けは偽。面へ当てる分岐は画面が持つ） `[A-100]` | buildMenu・hotbar | S3-buildmenu（S3-hotbar-modal は使うだけ） | Web の橋 `BuildMenuEntryDtoFactory.ResolveIconUrl`（`HotbarTopic` も使う） |
| ホットバーへの落としの受け口 | `Client.Ui.State/Hotbar/Logic/IHotbarDropReceiver` | `void Receive(HotbarDragSource source)`（`IUiDropTarget` は継がない。ホットバーの枠の印が両方を実装し、断る理由のログは受け手）。源は落とし先がこれなら `Receive` を呼び、そうでなければ枠外と同じ扱い `[A-100]`・`[A-105]` | buildMenu（源）・hotbar（受け手） | S3-buildmenu が作り S3-hotbar-modal が実装する | `W/features/hotbar/useHotbarDragSource.ts` |
| ホイール素通しの印 | Foundation `Input/WheelPassthroughMark` | `Mark(VisualElement)`／`bool IsPassthrough(VisualElement target)`。Web の `data-wheel-passthrough`・`isWheelPassthrough` の写し。判定を使う側（装備のホイール）は S4-inventory `[A-109]` | hotbar・inventory（装備） | S3-hotbar-modal | `W/shared/uiState/activeLayer.ts`・`HotbarPanel`・`EquipmentPanel` |
| ノードグラフ | `Components/NodeGraph/NodeGraphView/`（`Graph/`・`Viewport/`・`Pan/`） | `NodeGraphView(string testIdPrefix)`（根の `name` は `<prefix>-viewport`、キャンバスは `<prefix>-canvas`。[A-24] の子の testid の延長）／`SetGraph(NodeGraphContent)`（`NodeGraphNode(string key, Vector2 treePosition, IReadOnlyList<string> previousKeys, VisualElement card)` の列と初回の注目点 `NodeGraphFocus`）／`IObservable<string> OnNodeTapped`／`IObservable<NodeGraphViewport> OnViewportChanged`（マウント時と復元の値は流さない）・`RestoreViewport(NodeGraphViewport)`（初回の中央寄せと寸法の追従を止める）。保持は画面の State（`Client.Ui.State/<機能>/Session/`）。State の Session は素の値（x・y・倍率。前例 `BuildMenuSessionSnapshot`）で持ち、`NodeGraphViewport` との詰め替えは画面（`Screens/<機能>/`）がやる（`Client.Ui.State` は `Client.Ui` の型を参照できない）。パン・ズーム・慣性は中で `UiPressGesture` と `UiInertiaAnimation` `[A-115]`〜`[A-119]` | research（段階 4）・challenge（段階 5） | C4-d | `W/shared/treeView`・§8.5 |

**1 feature だけが使うもの（`Screens/<機能>/` に置く）**: 通知の行（notification）、チュートリアルの枠とラベル・ドラッグガイド（tutorial）、ワールドピン、スキットの選択肢の板（並び・印・押下。面は `GamePanelSurface` の種別 `skit-choice` `[A-84]`）・ツールバーのアイコン群、削除モードの斜線帯（modeHud。plan §3.4 は段階 2 の部品に挙げるが使い手は 1 つ）、クロスヘア、`LackHighlightText`・`BlockItemGrid`・`details/rows/*`（blockInventory 内で共有）、`GrabOverlay`（`Screens/GrabOverlay/`。持ち物・サブインベントリ・研究の 3 画面にまたがるので画面のディレクトリから独立。描く層は Foundation の `UiLayer.Grab`／`UiFamily.Viewport`。追従は InputSystem のポインタの位置の流れ。表示の Model は `Client.Ui.State/Inventory/Grab/GrabOverlayModel` で、Presenter が `GrabOverlayView.Render` へ渡す `[A-90]` `[A-91]`）、全画面ゲートの外殻（eventLanguageGate）、研究ノードカード、チャレンジノードカード、画面の幕とヴィネット（`Screens/Shell/`。`ScreenBackdropPresenter` が `UiRoutingState` を購読し `UiScreenTraitsTable.Get(..).Backdrop` で幕 `name="screen-backdrop"`〈Web `rgb(0 0 0 / 42%)`。α は §3.1 の半透明の規則で補正〉を `UiLayer.Backdrop` の Viewport 族へ出し入れする。四辺の放射状のヴィネット〈`--screen-vignette-*`〉は同じ族の最背面に常時置き、頂点カラーで描く。Web `W/app/App.tsx:95-96`・`App.module.css:3-18,72-78`。S2-hud `[A-44]`）。2 機能目が現れたら §5.1 で昇格する。

**作らないもの**: `ConnectingPlaceholder`（WS の未受信を表す。UITK は DI から同期で状態を取れる〈`WebUiHostlessStateAvailabilityTest`〉ので、その状態が無い）`[A-13]`、再接続オーバーレイ（Web↔C# の WS 切断）と辞書エラー（Web の辞書 HTTP 取得の失敗）（UITK に同じ状態が無い。disposition に理由を書く）`[A-38]`、`ItemIcon`/`BlockIcon`/`FluidIcon`（Web では `GameIcon` の薄い包み。UITK では Content の `UiIcon` 生成に吸収）、`useSlotMouse`（`SlotFrame` の中に吸収）。

### 2.8 Web の非 UI 共有モジュールの行き先

| Web | UITK | 段階 |
|---|---|---|
| `shared/itemMove`（クリック計画の純ロジック） | `Client.Ui.State/ItemMove/`（L0 で 33 件を写す） | 4 |
| `shared/pointerGesture` | Foundation `Input/Gesture/UiPressGesture` | 3 |
| `shared/clamp01`（ゲージの値の丸め） | `Components/Gauges/GaugeBar/GaugeRatio`（Gauges の internal。使い手が Gauges の部品だけのため。`[A-89]`） | 4（C4-b） |
| `shared/tooltip`（CursorTooltip） | `Screens/CursorTooltip/`＋`TooltipFace`（位置の純関数 `CursorTooltipPlacement` は `Client.Ui.State/CursorTooltip/`。`[A-60]`） | 2 |
| `shared/treeView` | `Components/NodeGraph/` | 4 |
| `shared/tutorialAnchor` | Foundation `Anchor/`（既存） | 済み |
| `shared/uiScale` | Foundation `Stage/`（PanelSettings の倍率で置き換え、`--ui-scale` 相当は不要。Web で倍率を掛けない Portal〈Mantine Modal・eventLanguageGate〉だけは `UiScreenPixelScope` で倍率を打ち消す `[A-41]`） | F1（打ち消しは C3-b） |
| `shared/uiState` | `Client.Ui.State/Routing/`（既存） | 済み |
| `shared/placementTarget` | `Client.Ui.State/PlacementTarget/`（配置対象の絵の出どころ `PlacementTargetIcon`・`PlacementTargetIconResolver` は S3-buildmenu `[A-100]`） | 3 |
| `shared/i18n` | `Localize`＋Foundation `Text/` | 済み |

---

## 3. 見た目を Web と一致させる仕組み

### 3.1 トークンの体系（USS が正本・Web の CSS 変数と 1 対 1）

| UITK のファイル（`S/Client.Ui/Foundation/Theme/`） | Web の出所 | 作り方 | 名前の規則 |
|---|---|---|---|
| `Tokens.uss`（既存 178 行） | `W/app/tokens.css` | `node tools/uitk/convert-web-tokens.mjs` の生成物。手編集禁止 | Web と同じ名前。`WebTokenParityTest` が除外分（`TokenConversionExclusions.txt`）を除いて名前の一致を検査 |
| `SlotTokens.uss`・`SkitTokens.uss`（既存） | `W/app/slotTokens.css`・`skitTokens.css` | 同上 | 同上 |
| `MantineTokens.uss`（新設・F1） | Mantine の暗色テーマが解決する値（`--mantine-color-dark-0..9`・`--mantine-radius-*`・`--mantine-spacing-*`・`--mantine-font-size-*`・`--mantine-line-height-*`・`--mantine-color-blue-*` 等。Web の CSS と features が参照する名前だけ） | 変換スクリプトを広げ、`moorestech_web/webui/node_modules/@mantine/core/styles.css` と Web の `MantineProvider` の theme 指定から生成する。手編集禁止 | Web と同じ `--mantine-*`。パリティテストの対象に足す |
| `DerivedTokens.uss`（新設・F1。`ControlTokens.uss` の残りを移す） | Web が `color-mix`・`rgb(from …)`・半透明・直書きで出す色 | 手で書く。各行に「web: 元の式」と「決め方（前計算・α補正の厳密式・実機で決めた値）」をコメントで残す（今の `ControlTokens.uss` の書き方を継ぐ） | `--<部品>-<役割>`（例 `--panel-action-hover-face`） |
| `Theme.uss`（既存） | Mantine の既定コントロールの見た目 | 既定ランタイムコントロール（Button・BaseField・Slider・Scroller・Dropdown）を `MantineTokens` で塗る。PoC 由来の `--control-*` は消す `[A-5]` | — |
| `Typography.uss`（既存）・`Layout.uss`（新設） | Mantine Text/Title・Stack/Group | §2.6 | `.text--*`・`.title--*`・`.ui-stack--gap-*` |

- **半透明**: Web の値をそのまま使わない（plan §3.5 の PoC 実測）。`DerivedTokens`/`Tokens` の α 補正の行は「Web の値・UITK の値・決め方」を持つ（`docs/uitk/translucent-tokens.md`）。明暗は最大成分 0.5 で分け、暗い色は 1-(1-α)^1.85、明るい色は α^1.85 で（`[A-113]`）、生成器（`tools/uitk/webTokens/translucency.mjs`）が `DerivedTokens.uss` の式の行も計算して照合する `[A-72]` `[A-73]`。色はランタイムの PanelSettings（Linear 合成）で比べ、部品ギャラリー（sRGB 合成で淡く出る）では比べない `[A-74]`。撮る道具は `Client.Ui.Editor/Capture/` の `UiRuntimePanelCapture`・`GameViewResolution` `[A-111]`
- **字間**: UITK の `letter-spacing` は px と書いても「字の大きさ/100 × 値」で効く（C2-a・C2-c・I-fix1 実測）。生成器は em を ×100、px を使う字の大きさのトークンで割って ×100 した値で書き、px の字間に字の大きさの対応（`tools/uitk/webTokens/letterSpacing.mjs`）が無ければ生成を止める。部品 USS に字間を直書きするときも同じ換算で書く（例: `GamePanel` の題 0.02em → 2）`[A-70]`
- **calc()・rem**: 変換スクリプトが px に計算し、導出式をコメントに残す（既存）。plan §3.5 の「定数の検算 L2」は F1 で足す
- **C# は値を持たない**: Painter2D などで色・寸法が要る部品は、部品 USS で `--<部品>-<名>: var(--token)` と受け直し、`UssCustomStyleReader` で読む（段階 1 の `GamePanelFace` の前例）

### 3.2 部品ごとの USS の書き方

1. **1 部品 1 USS**。ファイルは部品の .cs の横（`Components/<族>/<部品>/<部品>.uss`）。`GameUiTheme.tss` に `@import` する（C1）
2. **ルートのクラスで必ず始める**（`.item-slot …`）。他の部品のクラスを書かない（部品の中身を外から塗らない）。画面が部品の見た目を変えたいときは、つまみ（下の 3）だけを画面 USS で上書きする
3. **つまみは Web の局所上書き変数と同じ名前**: Web が feature の CSS で上書きしている変数（web-components §3.3 の 45 か所: `--slot-size`・`--icon-pad`・`--count-*`・`--shortage-count-*`・`--bevel-*`・`--sel-c*`・`--icon-button-size`・`--panel-action-button-width`・`--mode-switch-option-height`・`--gauge-fill`・`--recipe-*`）を、部品 USS はそのまま `var()` で読む。USS のカスタムプロパティは子孫へ継承されるので、画面 USS がその画面のルートで値を変えれば Web と同じ効き方になる（例: `.hotbar-hud { --slot-size: var(--hotbar-slot-size); }`）
4. **状態はクラス**。data 属性・`:hover` 以外の状態（selected・disabled・lack 等）は `--<状態>` のクラスにし、付け外しは部品の Set メソッドだけが行う。`:hover`・`:active`・`:focus` は USS の擬似クラスを使ってよい（構造擬似クラスは禁止のまま）
5. **CSS からの写し方**は plan §3.5 の変換表に従う（`::before` → 子要素、grid → flex-wrap＋固定幅、z-index → 層 enum、コンテナクエリ → `GeometryChangedEvent`）。box-shadow・drop-shadow・グラデ・マスクは `generateVisualContent`（頂点カラー）か Painter2D で描き、描く子要素は `PickingMode.Ignore` にする
6. **USS の先頭に出所を書く**: `/* web: W/shared/ui/SlotFrame/style.module.css（components.md §8.6） */` と日英の 1 行ずつ。Web 側の変更を追従するとき、どの USS を見ればよいかを引けるようにする
7. **Web で calc() を通るつまみは部品の C# が導出する** `[A-47]`: USS には calc が無い（段階 1 の `Tokens.uss` は全部前計算・`GamePanel.uss:15-19`）。部品の CSS 規則の中で `calc(var(--つまみ) …)` を使う値は、部品 USS で `--<部品>-<名>: var(--つまみ)` と受け直し、部品の C# が `CustomStyleResolvedEvent` で `UssCustomStyleReader` から読んで計算し、インラインの style に当てる。対象と持ち主: `GaugeBar` の高さ（C2-c 計画 Task 5）・`GamePanel` の面の上端（C2-a 計画 Task 2。top と margin-top に分けて足す）・`RecipeListScrollView` の右の余白（C4-c）・`RecipeRowFrame` の位置（`--recipe-row-frame-outset` から inline の負の left/top/right/bottom。C4-c）・`NodeGraphView` のクリップの逃げ（`--node-graph-clip-inset` を読んで padding と同量の負の margin を当てる。C4-d 計画 Task 4）。ビルドメニューの面 `calc(var(--slot-size) - 8px)` は [A-101] で改めた（C# はクリップの逃げ `--build-menu-scroll-clip-inset` を導く。S3-buildmenu）。`SlotFrame` の選択の白面の下端の帯は calc が要らず USS の `var()` で受けるので対象外 `[A-51]`。`:root` で宣言したトークンの calc（`--icon-button-size: calc(var(--slot-size) / 2)` 等）は Web でも `:root` で解決済みの値が継がれるので、変換スクリプトの前計算で同じになる（対象外）

### 3.3 比べ方（誰が・いつ・何を）

| 何を | 誰が | いつ | どう |
|---|---|---|---|
| Web の参照キャプチャ | integrator（(b) は I-follow のレーンが撮り直して並べ比べる） | (a) 段の頭（その段の画面を撮り直す）、(b) master の取り込みで、移し終えた画面に効く Web 変更が入ったとき（I-follow-n の完了条件。実行計画 §1.6）、(c) 撤去の前（X-prerun で全画面） | master の worktree で `W/../e2e/uitk-reference/referenceShots.ts` を回し、`moorestech_logs/uitk/reference-captures/web-<master の短い sha>/` に置く（既存 2 組の続き）。feature/uitk の Web には手を入れない（plan §3.3 R3）。部品だけの参照は、画面の参照から切り抜く（Web に部品の見本ページは無く、作ると Web の変更になる）。参照の組に無い状態（例: 掴み絵）を比べる部品・画面レーンは、同じ撮り方（mock-host・`shotRunner.ts` の `prepareShot`）の使い捨てのスクリプトで自分で撮って比べ、撮影表へ足すことは integrator へ依頼する（完了は integrator の撮影を待たない。実行計画 E-22） |
| UITK の部品の見本 | 部品レーン | 部品を作り終えたら（L1 が通った後） | `Client.Ui.Editor` の **部品ギャラリー**（EditorWindow。本番の tss を当て、部品ごとに全状態を並べる。各部品レーンが `IUiGalleryEntry` を 1 つ足す）を `uloop screenshot` の window モードで撮る（rendering モードには UITK が写らない。plan §3.12）`[A-9]` |
| UITK の画面 | 画面レーン | 画面の L1 が通り、L3/L4 に入る前 | PlayMode・全体スイッチ UITK で、参照キャプチャと同じ状態（Web のフィクスチャ名と同じ状態を作る DSL シナリオ）を ScreenCapture で 720p と 2560×1080 の 2 種撮る。フレーム番号の透かしを入れる |
| 並べ比べ | 作ったレーン（自己確認）→ 指揮（判定） | レーンの完了報告の前 → 波の取り込みの前・I-follow の後・X-prerun（切替前に全画面を最新の参照と） | 左 Web・右 UITK の比較 HTML の原本は worktree の gitignore 下に置き、PR の報告ページに載せる（`moorestech_logs` にはスクショ 3 枚程度まで）。レーンは「位置・寸法・色・文字の段組・装飾の有無」の差を表にする。指揮は波ごとに codex-audit（画像付き）にかける（plan §4.6）。差は Web へ寄せて直すか、§3.4 の例外に記帳する |
| ユーザーの目視 | ユーザー | 切替 PR の検証ビルドで 1 回（plan §2.0・§4.6。段階ごとの staging は棄却済み） | — |

合否の目安（agent 既定 `[A-14]`）: 720p で位置・寸法の差が 2px 以内、色はトークン経由で同じ値（α 補正の行は補正後）、装飾（罫線・三角・ベベル・グラデ）の有無が同じ（`GamePanel` の外周の影は Web でも出ないので描かない `[A-22]`）、文字の段組（折返し・行数）が同じ。文字の字形の差（Chrome と TextCore）は差に数えない。

### 3.4 例外（Web と同じにしないもの）の記帳

- **入れてよい条件**: ユーザー原文「無理に同じにする必要はない、影響が小さく非本質的な対応が入るのであれば、推奨でも良い」。agent はこれを「同じにするための対応が大きい（部品の作り直し・描画の自作が数百行・性能の劣化）か、ゲームの本質から外れた対応（ブラウザの癖の再現）が要り、かつ差が一目で分からない」と読む。**配置・色の系統・装飾の有無・文字の大きさの差は一目で分かるので例外にしない**（直すか、`[Q-n]` で上げる）
- **書く場所**: 台帳 `docs/uitk/TODO.md` に「## 見た目の例外」の表を足す（F1。台帳は integrator 所有）。列: ID（`LX-n`）／画面・部品／差／同じにしない理由／推奨案／撮影（比較 HTML の URL）／出所（agent 既定・裁定）
- **コードに印を残す**: 例外の USS 行・C# 行に `/* look-exception: LX-n */` を付ける。L2 で「印の ID が台帳にある」「台帳の ID が印のどこかにある」を照合する
- **文字の大きさは Web と同じ px にする**。GameUi のフォントの字幅が Web と違うぶんは letter-spacing で寄せ、字の大きさ（px）は変えない `[A-37]`。寄せても並べ比べで差が一目で分かるときは例外にせず `[Q-n]` で上げる
- 段階 1 で既にある差（題 34px → 32px のフォント幅の補正、`PanelActionButton` の text-shadow → outline 近似、`ModeSwitch` の ring → border 近似）は、F1 で `LX-1..3` として記帳する。`LX-1` は C2-c が Title を Web と同じ px＋letter-spacing へ直すときに台帳と印ごと消す `[A-37]`
- `LX-n` の番号は実行計画 §6.1 の割り当て（レーンごとの範囲）から取り、台帳「## 見た目の例外」の末尾へレーンが追記する（実行計画 E-1）

---

## 4. 画面の作り方の型

### 4.1 1 画面のファイル（例: ビルドメニュー）

```
S/Client.Ui.State/BuildMenu/
  BuildMenuViewModel.cs          不変 sealed。判別 union は抽象基底＋sealed 派生
  BuildMenuModel.cs              IReadOnlyReactiveProperty<BuildMenuViewModel> Current（源を購読して作る）
  IBuildMenuCommands.cs          型付きコマンド。UiCommandResult を返す
  BuildMenuCommands.cs           前提の検証（state の一致・解放）と実行。拒否は理由 enum とログ
  Logic/…                        Web の純ロジックの写し（L0）
S/Client.Ui/Screens/BuildMenu/
  BuildMenuScreenPresenter.cs    起動時に 1 回マウント・ルータで表示切替・表示中だけ Model を購読
  BuildMenuView.cs               Components を組む。操作は IBuildMenuCommands へ。結果は UiCommandResultToaster へ
  BuildMenuCategorySidebar.cs    画面だけの部品（2 機能目で Components へ）
  BuildMenu.uss                  画面の配置とつまみの上書きだけ
S/Client.Tests/Ui/Screens/BuildMenu/
  BuildMenuViewTest.cs           L1（ScreenTestHost・FakeModel・CommandRecorder）
  FakeBuildMenuModel.cs / RecordingBuildMenuCommands.cs
S/Client.Tests/Ui/State/BuildMenu/
  BuildMenuGroupingTest.cs       L0（Web の buildMenuGrouping.test.ts を写す）
```

### 4.2 コードの型

```csharp
// Presenter: 載せる・出す・購読だけ。判断は持たない（前例 PauseMenuScreenPresenter）
// Presenter: mount, show and subscribe only; holds no decisions (precedent: PauseMenuScreenPresenter)
public sealed class BuildMenuScreenPresenter : IInitializable, IDisposable
{
    public BuildMenuScreenPresenter(GameUiRoot root, UiRoutingState routing, BuildMenuModel model, IBuildMenuCommands commands, GameContentPresentation content, UiCommandResultToaster toaster) { … }
    public void Initialize();   // View を作り root.GetLayer(UiLayer.Screen, UiFamily.Stage) へ 1 回 Add（舞台の上段 3 列に並ぶ画面は StageColumnsLayout の枠へ。A-45）。routing.Current を購読して SetShown
    public void Dispose();
}

// View: 部品の木と操作の結線。Model の値は Render で受ける。MonoBehaviour に依存しない
// View: component tree and input wiring; takes model values via Render; no MonoBehaviour dependency
public sealed class BuildMenuView : VisualElement
{
    public BuildMenuView(IBuildMenuCommands commands, GameContentPresentation content, UiCommandResultToaster toaster) { … }
    public void Render(BuildMenuViewModel viewModel);
    public void SetShown(bool shown);   // 非表示の間は描かない。表示するとき最新値で 1 回描く（plan §3.2）
}

// コマンドの結果をトーストへ。良性の拒否は出さない（UiCommandRejectReasonTraits.IsBenign）
// Routes command results to toasts; benign rejections stay silent
public sealed class UiCommandResultToaster
{
    public void Report(string commandName, UiCommandResult result);
}
```

- **View がコマンドを直接呼ぶ**（段階 1 の PauseMenuView・BugReportFormView の前例）`[A-8]`。plan §3.1 は「Presenter が操作をコマンドへ渡す」と書くが、コマンドは既にインターフェースで、L1 では `CommandRecorder` で受けられる。Presenter を経由させても中継の配線が増えるだけなので、前例に合わせる
- **Model の購読は Presenter**。View は `Render` で値を受けるだけ（購読の寿命を Presenter の表示・非表示に揃えるため）
- **2 源を合成する表示の例**: `GrabOverlayModel`（掴みと画面の 2 源）→ `GrabOverlayPresenter` → `GrabOverlayView.Render`。ID→アイコンは View が `GameContentPresentation` で解く `[A-90]`
- **`Update()` を使わない**。変化は Model の `ReactiveProperty`。毎フレームの物理（採掘進捗）は Presenter が源の変化通知を購読して View へ流し、フレーム末への間引きはしない（同じフレームの描き直しは UITK が 1 回の描画にまとめる。Web の間引きは WebSocket の送り過ぎを抑えるためのもの）`[A-57]`
- `UiCommandResultToaster`（`Report(string commandName, UiCommandResult result)`）は段階 1 の `GameUiToastFeed.ShowRejected` の呼び方を 1 つにまとめる F1 の追加 `[A-15]`。F1 で `ShowRejected` は消え、`GameUiToastFeed` は `Show(string, GameUiToastVariant)` だけを持つ。`commandName` はログ用のコマンド名（Web の失敗コードの綴り）で、利用者へ出す文は拒否理由ごとの翻訳文 `[A-18]`
- **ビルドメニューの視口 `IBuildMenuScrollViewport`**（`Screens/BuildMenu/CategoryScroll/`。[A-20]）は `ScrollTop`・`ClientHeight`・`ScrollHeight` に加えて `AppliedSpacerHeight`（レイアウト確定後のスペーサの実寸。style に書いた値ではない）を返す。追跡器はこの実寸と要求値を 1px 許容で比べて反映済みを判定するので、View は実寸を返し、スペーサを当てた後の `GeometryChangedEvent` で `OnLayoutChanged()` を呼ぶ（style 値を返すと反映前に確定し、保存位置が潰れる）`[A-49]`

### 4.3 テストの型

| 層 | 何を | 土台（既存） | 置き場 | 1 部品・1 画面あたりの最小 |
|---|---|---|---|---|
| L0 | 純ロジック・ViewModel の導出・コマンドの前提検証。Web の vitest（L 種別）を 1 対 1 で写す | EditMode NUnit（`dotnet test` の S12 は未確認。回らなければ Editor のまま。tests-ladder §3.1） | `Client.Tests/Ui/State/<機能>/` | Web の該当 test ファイルの全件。テスト名は日本語の挙動文で、元の Web テスト名を XML コメントに書く（名前の突き合わせ用。tests-ladder §3.4） |
| L1（部品） | 状態クラスの付け外し、つまみの効き（`resolvedStyle` の寸法）、イベント、`PickingMode`、`focusable=false` | `UiTestHost`（本番の tss・1280×720） | `Client.Tests/Ui/Components/<族>/` | 状態ごとに 1 件・イベントごとに 1 件・つまみを 1 つ上書きして寸法が変わる 1 件 |
| L1（画面） | View＋Model＋Commands の結線、Web の C/P 種別の挙動 | `ScreenTestHost`・`FakeModel<T>`・`CommandRecorder<T>` | `Client.Tests/Ui/Screens/<機能>/` | Web の該当コンポーネントテストと e2e の P のうち、ブラウザ固有でないもの全件 |
| L2 | 規約 | `UiStyleConventionTest` ほか 7 クラス | `Client.Tests/Ui/Conventions/` | F1 で足す: tss の束ね漏れ・余り／ルートクラスの一意／層の依存方向（§1.1）／`look-exception` の照合／`MantineTokens` のパリティ／calc 検算 |
| L3 | 実ゲームでの結線（DI・層・入力） | EditModeInPlayingTest | `EditModeInPlayingTest/Ui/` | 段ごとに 1 本（その段の画面が全体スイッチ UITK で開く・閉じる） |
| L4 | 録画プレイテストと CEF 無し起動ラダー | unity-playmode-recorded-playtest | `scenarios/uitk/` | plan §4.4 のラダー（段 2 `uitk-opening-hud` …） |

- テスト名と由来の対応は `docs/uitk/disposition.md` に、レーンが写したときに書く（束の最後のレーンで突き合わせスクリプトが差分 0 を見る。tests-ladder §3.4）
- 部品の L1 は部品レーンの完了条件。画面レーンは部品の L1 を書き直さない

### 4.4 全体スイッチとの関係

- 全体スイッチ（albof.17。plan §3.3 R1〜R4）は Editor 限定の 1 つの enum。**UITK のときだけ** composition root（`MainGameUiRegistration`）が Presenter 群を登録し、Web のときは Client.Ui の Presenter を 1 つも登録しない `[A-16]`。Presenter・View・部品はスイッチを読まない（分岐は composition root の 1 か所）
- 画面の出し分けは `UiRoutingState.Current.Screen` と `UiScreenTraitsTable` だけで決める（段階 1 の `MainPanelOwner` は消える。C10）
- HUD（常駐表示）はルータの主パネル画面ではなく、Web `App.tsx` の条件を Presenter が読んで出す。`UiScreenTraits` の `AlwaysOnHud` が絞るのはホットバーと装備だけで、他の常駐 HUD はそれぞれの条件（`CrosshairVisibility`・`TrainHud` ∧ 乗車中・¬blocking スキット等）に従う `[A-57]`。`Backdrop` 列は `Screens/Shell/` の `ScreenBackdropPresenter` だけが読む `[A-44]`
- 舞台の上段 3 列に並ぶ画面（持ち物＝`Inv`、ブロックインベントリとレシピビューア＝`Viewer`、アイテム一覧＝`Items`）の Presenter は、`StageColumnsLayout.Get(StageColumn)` の枠へ View を載せる。研究画面の左余白 0 は `StageColumnsLayout` が `UiRoutingState` の画面で切り替える（Web `App.module.css:26-54`・`App.tsx:100-103`）`[A-45]`
- L1 と L0 はスイッチと無関係（View を `UiTestHost` に直接組む）。L3・L4 はスイッチを UITK にして始める（plan §3.3 R4）
- UITK の間、未実装の画面は使えない（裁定）。Presenter の未登録の画面は何も出ないだけで、ログは不要（裁定どおりの既知の状態）

---

## 5. 部品の昇格規則とレーンの切り方

### 5.1 昇格規則

1. **Web で既に 2 feature 以上が使う部品**（§2.2〜2.7 の Components 行）は、使う段の頭の部品レーンで `Components/` に直接作る `[A-11]`。§3.13-3 の「2 機能目で昇格」を待つと、同じ段の 2 画面レーンが同じ部品を別々に作る（並列のレーンはファイルの所有が重ならないように切るので、先に置き場を決める必要がある）
2. **Web の棚卸しに無い新しい共有**（UITK で作ってみて 2 画面目が同じものを欲しがった）は、plan §3.13-3 のとおり: 1 機能目は `Screens/<機能>/` に置き、2 機能目のレーンは自分で作らず指揮に申告し、integrator が波の境で `Components/<族>/` へ移す（2 画面の参照を書き換え、L1 を `Components/` 側へ移す）
3. **昇格の条件**: ドメインを知らない形に直せること（ID・Model を受けず、`UiIcon`・`UiText`・数値・値型で受ける）。直せないものは Content か、`Client.Ui.State` のロジックへ分けてから上げる
4. **昇格しないもの**: 1 画面の中で複数ファイルから使うだけの部品（`LackHighlightText` 等）は `Screens/<機能>/Parts/` に置く（10 ファイル/ディレクトリの上限で分ける）

### 5.2 レーンの切り方（初期提案）

> レーンの切り方・依存・所有の正本は実行計画 §1・§6・§8。この表は初期提案で、実行計画の割り当て（E-7 など）に合わせて直してある。食い違ったら実行計画が勝つ。

各段は「基盤 → 部品の波 → 画面の波」の順。部品の波の中は族ごとに並列。画面の波は部品の波の取り込み後に並列。共有ファイル（層 enum・tss・Tokens/Theme・composition root・台帳・`AnchorIds`・`localization.csv`）は integrator が直列で持つ（plan §3.13-2）。

| 段 | ID | レーン | 中身 | 依存 | 並列 |
|---|---|---|---|---|---|
| 段階 2 の前 | albof.17 | 全体スイッチの作り直し | plan §3.3。C10 | albof.20（master 取り込み） | 直列（integrator） |
| 段階 2 の前 | F1 | 基盤整え | C1・C4・C5・C6・C8・C9・namespace（[A-7]）・`MantineTokens`/`DerivedTokens`（C2 の 3 レーンが使うつまみの既定・前計算色と `UssCustomStyleReader.TryReadNumber` を含む）・`UiCommandResultToaster`・L2 の新検査・部品ギャラリーの枠（`Client.Ui.Editor`）・台帳の「見た目の例外」・段階 1 の既存差の記帳 | albof.17 | 直列（integrator。共有ファイルを持つ） |
| 段階 2 | C2-a | 面・装飾 | `GamePanel`（改修・craft。影は描かない `[A-22]`）・`GamePanelSurface`・`PaperSurface`・`FadeRule`・`HudSectionHeading` | F1 | C2-c と並列 |
| 段階 2 | C2-b | 操作・入力 | `IconButton`・`FormButton`・`GameTextField`（USS）・段階 1 の BugReport をこれらへ置き換え（C2）・albof.12 | F1・C2-c（`LocalizedTextBinding`） | C2-a の後 |
| 段階 2 | C2-c | 文字・表示・スクロール | `GaugeBar`・`TooltipFace`・`GameScrollView`・`KeyCap`・`ShortcutHintLabel`・Typography・`UiStack`/`UiGroup`（`Layout.uss`）・`LocalizedTextBinding`・段階 1 の設定のスクロールをこれらへ置き換え（C3）・albof.16.2 の (1)(2) | F1 | C2-a と並列 |
| 段階 2 | S2-* | 画面 | notification・modeHud・skit・commonHud/keyHint/progress・challenge HUD・tutorial（KeyControlHint・WorldPin）・CursorTooltip・trainHud・画面の幕とヴィネット（S2-hud `[A-44]`） | C2-a〜c の取り込み | 画面ごとに並列（Editor の予算まで） |
| 段階 3 | C3-a | スロット族 1 | `UiIcon`・Content（`GameContentPresentation`）・`GameIcon`・`SlotFrame`・`SlotContent`・`ItemSlot`・`BlockSlot`・`PlacementTargetFace`・`SlotGrid`（`SlotAmountFormat` は L0-s3 が作り済み `[A-19]`） | 段階 2 の部品の波（段階 2 の画面とは並走可） | C3-b と並列 |
| 段階 3 | C3-b | 相互作用 | `UiPressGesture`・`UiDragDrop`・`HoverTooltipHost`（スクロールの退避と再表示 `[A-43]`）・`ModeSwitch`（縦・タブ・個別無効）・`GameScrollView` のスクロールスパイの口・`UiModalHost`・`UiScreenPixelScope` と倍率の読み口 `StageScaleSource` `[A-41]`・`DialogFrame` | 同上 | 同上 |
| 段階 3 | S3-* | 画面 | buildMenu・hotbar・modal | C3-a・C3-b | 並列 |
| 段階 4 | C4-a〜e | スロット族 2（液体）／進捗（矢印・棒）／レシピ（`RecipeRow`・`LevelInheritanceBadge`・`RecipeListScrollView`・`RecipeActionButton` `[A-46]`）／`NodeGraphView`／掴み層と分割ドラッグ・舞台の上段 3 列 `StageColumnsLayout` `[A-45]` | 族ごと | 段階 3 の部品の波 | 5 本並列（Editor の予算で 2〜3 本ずつ） |
| 段階 4 | S4-* | 画面 | inventory・blockInventory（族ごとに分ける）・recipe・research・train | C4-* | 並列 |
| 段階 5 | S5-* | 画面 | challengePanel（`NodeGraphView` を再利用）・eventLanguageGate（全画面ゲートの外殻は画面内）・tutorial overlay | 段階 4 | 並列 |

各部品レーンの完了条件: 部品の L1・ギャラリーの見本・Web の参照からの切り抜きとの比較 HTML・例外の記帳（あれば）・`uloop compile`・moores-code-review → feature/uitk へマージコミット（agent）。

---

## 6. ref-uitk-design スキルの目次案（新設）

bd albof.19 の対象（`ref-uitk-design`）は repo の `.agents/skills/` にも git 履歴にも無い（stage1-asbuilt §9）。W0-skill で **新設** する `[A-17]`。`ref-webui-design` を写すが、見た目の正本は「Web 版と参照キャプチャ」で、ホワイトリストは Web に無い新しい面を作るときだけ効く（plan §3.12）。

```
.agents/skills/ref-uitk-design/
  SKILL.md                         （200 行以内）
  references/component-catalog.md （この文書の §2 の機械的な写し。§2 を改訂したら W0-skill 計画 Task 1 Step 3 のコマンドで再生成する。実行計画 §4.3・§5）
  references/css-to-uss.md         （plan §3.5 の変換表と、影・グラデ・マスクの描き方の前例）
  references/compare-procedure.md  （§3.3 の撮影と並べ比べの手順・合否の目安）
```

SKILL.md の目次:

0. いつ読むか（Use when: `Client.Ui`・`Client.Ui.State` の UI を読む・書く・レビューする／部品・画面・HUD を足す／USS・トークン・色・配置を変える）
1. 見た目の正本は Web 版（裁定 2 本の要旨と例外の原文）。迷ったら Web の該当 CSS module と参照キャプチャを開く
2. 層と依存の向き（§1.1 の表の短縮版）。Components はドメインを知らない
3. 部品カタログの引き方（`references/component-catalog.md`）。新しい部品を作る前に、Web の `shared/ui` と事実上の共有の表を見る
4. 部品の書き方: ctor・Set メソッド・`IObservable` のイベント・`focusable=false`・`contentContainer`・testid は `name`
5. USS の書き方: 1 部品 1 USS・tss に束ねる・ルートクラスで始める・つまみは Web と同名・状態はクラス・出所の 1 行
6. トークン: 5 ファイルの役割、生成物は手で直さない、色は `var()` だけ、半透明は補正の行に従う、C# は `UssCustomStyleReader`
7. 層と族: `UiLayer` の並び・`UiFamily`（stage／viewport）・`PickingMode.Ignore` の既定
8. 画面の型: Presenter／View／Model／Commands と `UiCommandResultToaster`。`Update()` を使わない
9. 動き: transition と `UiMotion` の使い分け・尺はトークン・時計の注入
10. 入力: フォーカス・ESC・IME・ホイールの通し（plan §3.8 の要約）
11. 比べ方と例外の記帳（`references/compare-procedure.md`）。一目で分かる差は例外にしない
12. やらないこと: UXML・UI Builder だけの状態・インラインの色・構造擬似クラス・z-index・合成の太字・ビットマップ参照・部品の中身を外から塗る・`Func<>`・partial・`Action` のイベント
13. 検証: L0〜L4 の型と最小件数（§4.3）。`uloop screenshot` は window モード

---

## 7. 判断記録

### 7.1 agent 既定（この文書で決めたもの）

| ID | 判断 | 棄却した案と理由 |
|---|---|---|
| A-1 | asmdef は `Client.Ui`・`Client.Ui.State` のまま。層の向きは L2 の using 検査で止める | asmdef を層ごとに割る: 段階 1 の 80 ファイルの移動と .meta の再生成・`internal` の見直しが要る。検査が破られたら上げる |
| A-2 | 部品・画面の USS を `GameUiTheme.tss` に束ね、ctor の `StyleSheet` 引数と `UiAssetCatalog` を消す | カタログを使い続ける: 40 枚超で 200 行を超え、全呼び出し側が `catalog.Xxx` を渡し続ける。部品ごとに `Resources.Load`: 文字列パスで欠けが実行時まで分からない |
| A-3 | View は C# で組み、UXML を使わない（plan §3.1 の「UXML と USS は機能の横」は USS だけに読む） | UXML: 段階 1 は 0 枚で前例が無く、部品が型付きの ctor を持つ設計と二重になる。testid の綴りがコンパイルで守られない |
| A-4 | Content 層（ID → `UiIcon`・名前）を新設し、Components は ID を受けない | Web どおり `ItemSlot(itemId)`: 部品が `Client.Game` とマスタを知り、AGENTS.md の基盤規約に反する |
| A-5 | `--control-*` を消し、`MantineTokens.uss`（Web と同名の `--mantine-*`、生成物）で既定コントロールを塗る | PoC の色のまま: Web と違う色になる（段階 1 で確認済みの差） |
| A-6 | `UiFamily { Stage, Viewport }` と各層の 2 つの子で stage 族・viewport 族を表す | 全面の層だけ: 21:9 で Web と配置がずれる |
| A-7 | namespace を `Client.Ui.Components.<族>` にそろえる | 今の混在（直下と `TextField`）のまま |
| A-8 | View が型付きコマンドを直接呼ぶ（段階 1 の前例）。Model の購読は Presenter | Presenter が中継: 配線が増えるだけで、テストのしやすさは同じ |
| A-9 | `Client.Ui.Editor` asmdef と部品ギャラリー（EditorWindow）を作り、部品の見本を window モードで撮る | 部品の見本を Web 側に作る: feature/uitk で Web を書き換えることになる（R3） |
| A-10 | `GamePanel` の影を頂点カラーで描く（theme D-6 を改める）。**A-22 で改めた（描かない）** | 描かない: Web と見た目が違い、描けない理由が無い |
| A-11 | Web で既に共有の部品は段の頭の部品レーンで Components に直接作る | 2 機能目を待つ: 同じ段の並列レーンが同じ部品を二重に作る |
| A-12 | `GamePanel` の craft を段階 2 へ前倒し（modeHud が使う） | plan §3.4 どおり段階 4: 段階 2 の modeHud が作れない |
| A-13 | `ConnectingPlaceholder` を作らない | 写す: UITK に「未受信」の状態が無く、出る場面が無い |
| A-14 | 合否の目安（720p で 2px 以内・トークン同値・装飾の有無・段組が同じ。字形の差は数えない） | 画素一致: plan §4.6 が目標にしないと決めている |
| A-15 | `UiCommandResultToaster` で拒否のトーストの出し方を 1 か所にする | 画面ごとに `GameUiToastFeed.ShowRejected` を呼ぶ: 良性の判定が散る |
| A-16 | 全体スイッチの分岐は composition root の 1 か所。Presenter はスイッチを読まない | Presenter ごとに読む: 同種の分岐が散る |
| A-17 | `ref-uitk-design` を新設する（albof.19 は「合わせる」ではなく「作る」）。担当は W0-skill（実行計画 §1.1） | 作らない: plan §3.12 が段階 1 の初版を前提にしている |
| A-18 | F1 の詳細計画で次の 3 点を改める（F1 計画 F-1・F-2・F-14）: `UiCommandResultToaster.Report(string commandName, UiCommandResult result)`（コマンド名はログ用で、翻訳キーにしない）／`ToastItem(string text, ToastItemTone tone)`（Components が `Client.Ui.State.Toast` を参照しない。§1.1）／`Layout.uss` は F1 で作らず C2-c の `UiStack`/`UiGroup` と一緒に作る | 設計書どおり: `LocalizationKey` はログ用に辞書の行を足すだけで利用者に出ない。`GameUiToast` を受けると層の L2 に例外が要る。空の `Layout.uss` は使い手が無い |
| A-19 | `SlotAmountFormat` は `Client.Ui.State/Text/` に `Format(double)` で置き、L0-s3 が作る。C3-a は使うだけ（L0-s3 計画 D-3） | C3-a が Components/Slots に `Format(long)`: State の `MaterialTooltipText`（素材ツールチップの数）が Components を参照できず、液体の小数も表せない |
| A-20 | ビルドメニューの 1 本スクロールの追跡器と `IBuildMenuScrollViewport` は `Screens/BuildMenu/CategoryScroll/` に置く。純ロジック（`BuildMenuScrollSpy`）とセッションのストアは State（L0-s3 計画 D-8）。視口の口は [A-49] | State に置く: 視口を逆向きに動かし px のレイアウト値を持つ表示の制御器で、§4.1・§4.2 の State→View 一方向に反する |
| A-21 | `UiNumberFormat` を `Client.Ui.State/Text` に置く（L0-s4b 計画 D-2） | Foundation/Text に置く: State の純ロジックが Web と同じ文字列を返せない（`Client.Ui.State` は `Client.Ui` を参照できない）。State に別の書式関数を作る: 同じ規則が 2 か所になる |
| A-22 | `GamePanel` の外周の影を描かない（A-10 と §1.4 C7 を改める。C2-a 計画 D-1） | 描く: Web の影は同じ `::before` の mask に切られて画面に出ず、描くと Web と違う（参照の画素で実測。C2-a が台帳「## 実測の結論」に記す） |
| A-23 | `PaperSurface` は枠を持たず、面と角丸はつまみ `--paper-face`・`--paper-radius`（既定 `--mantine-color-body`・`--mantine-radius-sm`）で受ける（C2-a 計画 D-6） | withBorder の写し: Web の 3 か所（ChallengeNodeCard・TrainRidingHud・CursorTooltip）は withBorder を使わない。画面ごとに背景を直書き: 部品の中身を外から塗ることになる |
| A-24 | `HudSectionHeading(string labelTestid, LocalizationKey label)`。testid は Web と同じくラベルに付け、つまみ `--hud-rule-width` を足す（C2-a 計画 D-7） | `(string testid, …)` で根に付ける: Web の `challenge-hud-label`・`operation-mode-label` はラベルの testid で、DSL が同じ綴りで引けなくなる。罫線の幅を画面に直書き: 部品の中身を外から塗ることになる |
| A-25 | `GamePanel` のつまみ `--panel-top/right/bottom/left`・`--title-shift-x`・`--title-scale-x/y` を Web と同名で受け、既定を `DerivedTokens.uss` の `:root` に置く（F1 が入れる。C2-a 計画 D-4） | つまみを作らない: 段階 4 の持ち物・レシピ一覧が Web と同じ値で上書きする（`InventoryPanel/index.tsx:52`・`ItemListPanel.tsx:57`） |
| A-26 | craft の内周の 2 本（`inset 0 0 0 3.132px`・`inset 0 0 0 3.524px`）は `GamePanel` の USS の border の入れ子で描き、`GamePanelCraftGrip`（頂点カラー）は右下グリップだけを描く（統合計画 §3.4「inset の 2 層は border の入れ子」。C2-a 計画 D-5） | 内周線をグリップと同じ頂点カラーで描く: USS の border で同じ形になり、描画コードが要らない |
| A-27 | `IconButton` は図形を `VisualElement` で受ける（`IconButton(string testid, VisualElement glyph)`）。既定の × は `IconButtonCloseGlyph`。読み上げ名は受けない（C2-b 計画 D-2・D-3） | 図形の列挙を受ける: スキットの自動送り・スキップ・非表示の図形が Components の列挙に入り、ドメインを知らない部品にスキットの語彙が入る。Web も children で図形を渡す。読み上げ名: UITK のランタイムに読み上げの口が無く使われない引数になる |
| A-28 | `FormButton` の variant・size は Web を grep した実使用分（`Filled`・`Danger`・`Subtle`／`Small`・`ExtraLarge`）。`SetLabel`・`SetVariant`・`SetSize` を持ち、busy 表示の Set と別の色引数は持たない。モーダルの確定色（State の `ModalButtonTone { Blue, Red }`）は S3-hotbar-modal の View が `Blue→Filled`・`Red→Danger` の 1 か所の switch で写す（C2-b 計画 D-4・L0-s3 計画 D-7） | 設計書旧版の filled・default・subtle と busy 表示: `variant="default"` と `loading` は Web に 0 件。`FormButtonColor` を別の引数で受ける: Web で使う color は filled の青と `color="red"` の 2 つだけで、variant の `Danger` と同じ組を 2 本の引数で表すことになる |
| A-29 | `GameTextFieldLook` は `GaugeInset`（textAreaField と buildMenu `.searchInput`。宣言が同一）と `MantineInput`（Mantine TextInput。モーダルの BP 名）（C2-b 計画 D-5） | 設計書旧版の記述欄・検索欄の 2 種: 2 つの Web の CSS は宣言が同じで USS が同じ規則を 2 回持つだけ。モーダルの名前欄の別の見た目が抜け、S3-hotbar-modal が C2-b のファイルを書き換えることになる |
| A-30 | 部品は外側の margin を書かず、置く側の画面 USS が `.<画面> .<画面の要素クラス>` で付ける（C2-b 計画 D-8・C2-c） | `PanelActionButton` の前例どおり部品で `margin: 0` を当て画面が部品のクラスを重ねて勝つ: 画面 USS に部品のクラスが入り §3.2-2 に反する |
| A-31 | 言語変更に追従する文（Label 1 つに入れないもの）は Foundation/Text の `LocalizedTextBinding` 1 本が持つ。C2-c が作り、`ShortcutHintLabel` と `GameTextField.SetPlaceholder`（C2-b）が使う。`LocalizedLabel` をこの上に作り直すのは後の提案 | 部品の中で `Localize.OnLanguageChanged` を購読する: Components が `Client.Localization` を参照して §1.1 に反する。C2-b と C2-c が別々に束縛を作る（`LocalizedPlaceholder` と `LocalizedTextBinding`）: 寿命管理が 3 か所に散り、片方だけ直る |
| A-32 | `UiStack`/`UiGroup` の間隔は USS の `--ui-gap` を C# が読み、表示中の先頭以外の子へインラインの margin で当てる。表示切替は `SetItemShown` に限る（C2-c 計画 D-4・D-5） | §2.6 旧版の「Add 時に先頭以外へクラスを付け USS で余白」: `PanelActionButton` の 2 クラスの `margin: 0` と詳細度が並び、区画の順で部品が勝って間隔が消える |
| A-33 | `TooltipFace(string testid, TooltipFaceLook look)`・`SetText(UiText)`・`SetLines(IReadOnlyList<UiText>)`（C2-c 計画 D-8） | `TooltipFace()`・`SetText(UiText)`: Web の CursorTooltip は行ごとに testid を持ち、角丸も Paper（4px）と HoverTooltip（0）で違う |
| A-34 | `ShortcutHintLabel(string testid, ShortcutHintLayout layout)`・`SetHint(UiText sentence, string shortcutText)`。`KeyCap` は枠を描かず色だけ持ち、keyHintText は置く側の `UiTypographyClass.KeyHintText`（C2-c 計画 D-11・D-13） | 複数の kbd を受ける: Web の `LocalizedShortcutHint` は kbd 1 つ。`.key-cap` に keyHintText を写す: Web は行に付け kbd は `font: inherit` で、kbd だけに付けると文と kbd の大きさが分かれる |
| A-35 | `GameScrollView` は `ScrollView` 派生で、バーは本文に重ね（本文の幅を削らない）、幅のつまみは `--scrollarea-scrollbar-size`（C2-c 計画 D-2・D-3・D-21） | 内包: `contentContainer`・`ScrollTo` を全部転送する。流れの中に置く: Mantine ScrollArea は重ねて置き、削ると折り返しが Web と変わる |
| A-36 | Web の太さ（Title 700・Text fw 500/700・keyHintText 500）は、Bold の Latin/JP/KR と Latin の Medium の SDF を足して写す（C2-c 計画 D-1） | Regular のまま例外にする: 裁定「段階1の画面はWeb版と同じ見た目」が「大きい太字の題」を名指ししており、太さの差は一目で分かる（§3.4） |
| A-37 | 文字の大きさは Web と同じ px。GameUi のフォントの字幅の差は letter-spacing で寄せる（Title 34/26/22/18px。段階 1 の 34→32px の補正 `LX-1` は C2-c が台帳と印ごと消す） | 字幅に合わせて px を下げる（段階 1 の LX-1 を Title 全般へ広げる）: §3.4 の「文字の大きさの差は例外にしない」に反し、段階 2 以降の全画面の題が Web より小さくなる。例外の条件に「字幅の補正」を足す: 裁定の例外の条件を広げることになる |
| A-38 | 再接続オーバーレイ（Web↔C# の WS 切断）と辞書エラー（Web の辞書 HTTP 取得の失敗）は UITK に作らない。S5-crosscut が disposition に理由を書く。統合計画 §2.6 の対象一覧との差は実行計画 §8 E-17 に残す | 写す: UITK には WS も辞書の HTTP 取得も無く、その状態が起きない。出す場面の無い画面を作ることになる |
| A-39 | 文字の 13px 下限（plan §3.10 の agent 既定）を外し、Web と同じ px にする（Mantine `size="xs"` の 12px、スロット内の 10〜11px を含む）。実機の比較で読めないものだけを見た目の例外に記帳する（旧 Q-1。実行計画 E-11） | 13px へ上げる: 裁定「全段階で Web 版と同じ見た目」に反し、行・マスの中の段組が Web とずれる。ユーザーに上げる: 上位の裁定から答えが決まり、クリティカルパスの頭（F1）に待ちが入るだけ |
| A-40 | `UiText` を辞書キー・コンテンツ辞書キー・解決済みの文の 3 種にし、文言を出す部品の API は `UiText` を受ける（`FormButton` も）。F1 計画 Task 5 Step 3b が作り、C2-b が `FormButton(string, UiText, FormButtonVariant)` で使う | `UiText` を辞書キーのままにし、画面が `Localize.GetContent` やサーバーの文を自分で Label に入れる: 共通部品（`FormButton`・`TooltipFace`・`HoverTooltipMark`）を画面が迂回し、言語変更の解き直しが画面ごとに散る。部品ごとに `string` の口を足す: 同じ「文言の種」を部品の数だけ持つ |
| A-41 | Web で `--ui-scale` を掛けない Portal（Mantine `Modal`〈`W/features/modal/ModalHost.tsx:35-37` の `w={320}`〉・eventLanguageGate の `Overlay`）の中身は、Foundation/Stage の `UiScreenPixelScope`（舞台の倍率の逆数で縮め、実画面いっぱいの寸法を持つ箱）に置き、実画面 px で Web と同じにする。C3-b が作り、`UiModalHost` と S5-gate が使う。倍率は `panel.scaledPixelsPerPoint` から読む（PanelSettings の倍率と一致するかは C3-b の先頭で実測する。§8）。C3-b はその倍率を Foundation/Stage の `StageScaleSource`（`IReadOnlyReactiveProperty<float> Scale`。解像度の変更で値が変わる。Unity には解像度変更の事象が無く、Expand では 16:9 どうしの変更で `GeometryChangedEvent` も来ないため、`StageScaleSource` だけがパネルの `schedule.Execute().Every(0)` で倍率を読み、変わったときだけ `Scale` を更新する。外部の状態〈OS の窓の寸法〉を見る唯一の点で、ゲーム状態の毎 tick の同値判定には当たらない。Web は `window` の resize で計り直す）として公開し、`UiScreenPixelScope` はそれを購読する（読み手の配り方と式の置き場は A-75 で改めた。Web の `w={320}` は効いておらずモーダルの実寸は 440 で、A-77 が改める）。通知の左端（Web `.host` の `left: 1rem` は倍率の外、中身だけ `scale(var(--ui-scale))`）も `StageScaleSource` を購読して左端を実画面 16px に保つ（S2-notification） | 全部を舞台の倍率で拡げる: 2560×1080 と 1080p（倍率 1.5）でモーダルが Web の 1.5 倍（幅 480px）になる。トースト・通知・ツールチップ・チュートリアル・ワールドピン・掴み絵は Web でも `scale(var(--ui-scale))` を掛けているので打ち消さない。通知を `UiScreenPixelScope` に入れて中身に倍率を掛け直す: 打ち消して掛け直す二重の変換になり、Web は位置（`left`）だけが倍率の外。通知が自分で `GeometryChangedEvent` から倍率を出す: 16:9 どうしの解像度変更ではパネルの寸法が変わらず事象が来ない |
| A-42 | `SlotFrame` のリングは寸法に効かない装飾子に描き、つまみ `--bevel-inset`（`outset` 既定／`inset`）で外側成長と内側食い込みを切り替える（C3-a） | 3 重の border を `SlotFrame` 自身に付ける: border は箱の内側に入り、持ち物とレシピの白面が Web より狭まる。外側へ統一する: ホットバーのリングが固定ピッチの外へ出る |
| A-43 | `HoverTooltipHost` は印の付いた要素を含む `ScrollView` のスクロールで引っ込め、ポインタの座標が実際に変わった `PointerMoveEvent` でだけ開き直す。無関係なパネルのスクロールでは引っ込めない（C3-b） | PointerEnter/Leave だけで出し入れする: ホバーしたままスクロールすると、クリップの外へ動いたセルの説明がパネルの外に残る（Web がユーザー指摘 2026-08-22 で直した不具合の再発） |
| A-44 | 画面の幕（`UiScreenTraits.Backdrop`）とヴィネットは `Screens/Shell/` が持ち、S2-hud が作る | F1 に入れる: F1 はクリティカルパスの頭で、`UiRoutingState` の購読と描画を足すと延びる。幕は段階 1 のポーズメニューから要るが、配らない裁定の下では S2-hud までに入れば切替の検証に間に合う |
| A-45 | 舞台の上段 3 列（Web `.stage` の grid）は Foundation/Stage の `StageColumnsLayout`（名前付きの枠 `Inv`・`Viewer`・`Items`、`flex-direction: row`・`align-items: flex-start`、余白と列間はトークン、研究画面だけ左余白 0）が持ち、C4-e が作る。S4-recipe・S4-research も C4-e に依存する | S4 の各レーンが絶対位置を決める: 持ち物の幅（`--inventory-panel-width`）に viewer 列が追従せず、研究画面で持ち物が左端へ寄らない。F1 に入れる: 使い手は段階 4 だけで、クリティカルパスの頭が延びる |
| A-46 | 主要アクションの青グラデのボタンを `RecipeActionButton`（Components/Controls）にし、クラフトと「研究する」が使う。C4-c が作る（C2-b 計画 D-4 のクラフトの行き先を改める） | `FormButton(Filled)` に画面 USS で重ねる（C2-b 旧 D-4）: USS の背景にグラデは書けず（§3.2-5）、部品の中身を外から塗ることになる。Web と同じく研究は画面だけで書く: 同じグラデの描画コードが 2 画面に重複する |
| A-47 | Web で calc() を通るつまみは部品の C# が導出してインラインで当てる（§3.2-7） | 画面 USS に計算済みの値を直書きする: つまみの上書きが導出値に追従せず、ホットバー（`--slot-size` 51.68px）などで 2px の目安を外す |
| A-48 | 2 行以上の文の行送りは C2-c が実測して方法を決め（リッチテキストの `<line-height>` を先に試し、効かなければ行ごとの Label を行高で固定）、最初の使い手 S2-hud が写す。合否は「2 行の行の間隔が行高トークンと 1px 以内」 | フォントの既定の行送りのまま: チャレンジ HUD の目標（20px）・操作 HUD の詳細（25px）などで行が積むほどずれ、HUD の高さが Web と変わる。`-unity-paragraph-spacing` で補正する: 段落の間にしか効かず、折り返しの行には効かない（推測。採る方法の根拠にはしない） |
| A-49 | `IBuildMenuScrollViewport` は `ScrollTop`・`ClientHeight`・`ScrollHeight` に加えて `AppliedSpacerHeight`（レイアウト確定後のスペーサの実寸）を返し、追跡器はスペーサの反映済みをこの実寸と要求値の 1px 許容の一致で判定する。View（S3-buildmenu）は実寸を返し、反映後の `GeometryChangedEvent` で `OnLayoutChanged()` を呼ぶ（L0-s3 判断 D-17） | 反映済みを追跡器の要求値との一致で判定する: 反映前の再通知で保存位置が潰れる（L0-s3 レビュー C6。Codex が偽の視口で 250→100 を再現）。View が style に書いた値を返す: レイアウト確定前に Settled になり、古い scrollHeight で保存位置の復元が確定する |
| A-50 | C3-a のスロット族の API を実際の使い方に合わせて改める: `GameIcon()`＋`SetIcon(UiIcon, GameIconFallback)`、`PlacementTargetFace` は `SetIconTarget`／`SetNameTarget`、`ItemSlotContent` は `SlotFrameState` でなく `ItemSlotMarks`＋`TooltipOverride`＋不足の `Tooltip`、`SlotGrid` は `AddItem`／`ClearItems`（行で積み幅を計算しない）。`ItemSlot`・`BlockSlot` の testid は中の `SlotFrame` に付け、`BlockSlot` の名前は必須（C3-a 計画 D-1） | 旧案のまま: SlotContent が 1 つの GameIcon に違う fallback を渡せない、絵の無い対象をテクスチャ null の UiIcon で表すことになる、filled・ghost を画面が組めて Web の式と食い違う、行頭のマスの間隔を決められない。testid を外側に付ける: Web の `data-testid` は SlotFrame に付き DSL・e2e の照準と合わない |
| A-51 | SlotFrame のリングは box-shadow の 3 層＋面際を border の帯 4 本で写し、重なり順は box-shadow と同じ。C# は `--bevel-inset` と 3 つの幅から帯の外周だけを決める。選択の白面の下端の帯は USS の `var()` で `bottom`・`height` を受け、A-47 の C# 導出は要らない（C3-a 計画 D-2） | Painter2D で描く: 帯の色・幅が USS のトークンで効かない。3 重の border の入れ子: box-shadow の重なり（同じ spread で c3 が c2 に隠れる）を写せない |
| A-52 | 部品レーンは自分のつまみの既定と前計算色を `DerivedTokens.uss` の末尾に名札付きのブロックで足してよい（実行計画 E-1 の共有区画と同じ扱いで、衝突時は両方採る）。暗い半透明色の α は `1-(1-α)^1.85` で補正する（C3-a 計画 D-4・D-5） | integrator の前提パッチを待つ: 持ち主の無い工程になりクリティカルパスが止まる。部品のルートで宣言する: 画面のつまみの上書きに勝つ |
| A-53 | Content 層（`Client.Ui/Content/`）は `GameContentPresentation` が ID→`UiIcon`・`UiText` を解き、テクスチャの出所は `IGameContentTextureSource`（本番 `ClientContextContentTextureSource`）で受ける。`Client.Ui.asmdef` に `Core.Master`・`Client.Mod` を足し、登録は `ContentGameUiRegistration` を `MainGameUiRegistration` の画面登録区画に 1 行。空の ItemId は例外で止める。`FluidFace` は C4-a（C3-a 計画 D-6・D-7） | `ClientContext` を直に読む: テストでテクスチャの有無を作れない。Content を別 asmdef: 層は `Client.Ui` の中のディレクトリで分け `UiLayerDependencyRules` が向きを止めている。空の ID に `#0` を返す: Web の itemId 0 は中身なし |
| A-54 | C3-a の Web テストの処遇: `useSlotMouse` は L0（`SlotGestureRulesTest`）へ、`BlockSlot`「nameが無ければ…」と `iconEndpointArchitecture.test.ts` は捨てる、`app/iconTextOutlineDesign.test.ts` は C3-a で記帳せず integrator が C4-a・S2-hud・S3 へ配る（C3-a 計画 D-9） | 名前の無い BlockSlot・HTTP のアイコン配信は UITK に無い。縁取りのテストは複数レーンの部品にまたがる |
| A-55 | 幕 `--screen-backdrop-face`（Web `.backdrop` の 42% を A-52 の式で補正した α 0.6350）と列車乗車 HUD の面 `--train-riding-hud-face`（Web `color-mix(dark-8 82%)` を同じ式で補正した α 0.9581）は S2-hud が `DerivedTokens.uss` の末尾に足し、`convert-web-tokens.mjs` の前計算の表と `docs/uitk/translucent-tokens.md` も同じレーンで直す（S2-hud 計画 D-4） | 段階 2 の頭に integrator が入れる（旧 §1.4）: feature/uitk に行が無く、持ち主の無い工程になっていた |
| A-56 | blocking スキットの有無は `Client.Ui.State.SkitPresence.BlockingSkitPresence`（`IReadOnlyReactiveProperty<bool> IsBlocking`）で配り、S2-hud が作り `MainGameUiRegistration` の本体（`GameUiToastFeed` の隣）に store の `RegisterInstance` と `Register<BlockingSkitPresence>(Lifetime.Singleton)` の 2 行で登録する。モード名は持ち主の `Client.Skit.UI.SkitPresentationData.BlockingMode` の定数を生成と判定の両方が読む。`Client.Ui.State.asmdef` に `Client.Skit` を足す。使い手は S2-hud・S2-tutorial・S3-hotbar-modal（S2-hud 計画 D-5・D-23・D-26） | 画面ごとにスキットのストアを読む: Web の `useBlockingSkitActive` 1 本の規則が 3 か所に割れる。S2-hud 専用の登録口で手組みする: 他の 2 レーンが S2-hud の登録に依存し、未マージの worktree で VContainer の解決例外になる。消費側で `"blocking"` を再定義する: 持ち主側の変更で判定が無音で偽になる |
| A-57 | 常駐 HUD の出し分けと層は Web `App.tsx` の写し。`AlwaysOnHud` が絞るのはホットバーと装備だけで、クロスヘア（`CrosshairVisibility`）・列車乗車 HUD（`TrainHud` ∧ 乗車中）・進行中チャレンジとキーヒント（¬blocking スキット ∧ 非空）・採掘進捗（`ProgressBarState.IsShown`）はそれぞれの条件で出す。出し分けの規則が源の値そのもの（クロスヘア・採掘進捗）のときは Model を作らず Presenter が源を購読する（S2-hud 計画 D-2・D-3・D-14・D-18） | 全 HUD を `AlwaysOnHud` で絞る（旧 §4.4）: Web でチャレンジ HUD はインベントリ・ポーズでも出ており、見た目が変わる |
| A-58 | 常駐 HUD まわりの e2e の割り当て: commonHud.spec は S2-hud 2・S2-mode-tooltip 3（＋tooltip の半分）・S2-skit 1、challenge.spec は S2-hud 4・S5-challenge 3、progress.spec「ラベル付き汎用進捗を維持する」は捨てる、alwaysOnHudWheel.test.ts は S3-hotbar-modal・S4-inventory が 1 件ずつ（S2-hud 計画 D-12・D-13） | S2-hud が全部写す: 比べる相手の画面（モードの帯・スキット・チャレンジ画面）が UITK にまだ無い |
| A-59 | キーヒントの kbd は `KeyCap`＋`LocalizedTextBinding` で組み `ShortcutHintLabel` は使わない。ヴィネットとクロスヘアの影は頂点カラーのメッシュで描き、α の段は `LinearBlendOpacity.OpacityAt`（Foundation/Theme） を使う。USS の % とキーワードは Foundation の読み口で読む: `UssTokenValue.TryParsePercent`・`UssCustomStyleReader.TryReadPercent`（S2-hud のヴィネット）、`UssCustomStyleReader.TryReadKeyword`（C3-a の `--bevel-inset`。変種 B を採れば消す）。使い手のレーンが要る口だけを既存の口と同じ形・同じ LogError の書式で末尾に足し、先に足されていればそれを使う（S2-hud 計画 D-7・D-9〜D-11・D-24、C3-a 計画 D-24） | `ShortcutHintLabel`: キー名と文言が別の鍵で来る。USS の背景: radial-gradient・box-shadow が無い。% とキーワードを画面・部品の側で読み integrator が後で寄せる: 読み口が 3 か所に割れ、LogError の書式と失敗時の既定が口ごとに違うまま C3-b・S3 が始まる |
| A-60 | CursorTooltip は `UiLayer.Tooltip` の Viewport 族に置き、面の寸法は舞台と同じ倍率で拡げる（Web も `scale(var(--ui-scale))`）。位置は Web の `tooltipPosition.ts` と同じく実画面 px で `OFFSET` 12 を足して端で押し戻し（clamp。反転はしない）、`panel.scaledPixelsPerPoint`（[A-41] の読み口）でパネル単位へ換算する。位置の純関数は `Client.Ui.State/CursorTooltip/CursorTooltipPlacement`（S2-mode-tooltip 計画 D-2・D-3・D-23） | 倍率を打ち消して実画面 px で置く: Web はツールチップに倍率を掛けている。`Screen.width / layout.width` で倍率を出す: [A-41] と別の読み口が増える（C3-b の実測で不一致なら C3-b の代替式に揃える）。端で反転: Web の実物は clamp で、`tooltipPosition.test.ts` の期待値と食い違う |
| A-61 | カーソルロック中は CursorTooltip を画面中央を指したものとして置く。ロックの判定と中央は画面のアダプタ `ICursorTooltipScreen` が持ち、本番は `Cursor.lockState` と `Client.Input.ScreenCenter.GetPosition()` を読む。View はロックも中央も知らない（S2-mode-tooltip 計画 D-4） | Web と同じくロック時のワープで届くポインタ移動に任せる: UITK のパネルにワープの移動が届く保証が無い。View が `viewport/2` を持つ: 画面中央の定義が `ScreenCenter` と 2 つになる |
| A-62 | 操作モード HUD は `UiLayer.Chrome` の Viewport 族に置く（Web の `.viewportOverlay` z 31 の中）。Web の `pointer-events: none` は、組み終えた部分木を `UiPickingIgnore.ApplyToSubtree`（Foundation/Input。I-fix2 で画面内の `ModeHudPickingIgnore` から昇格し、常駐 HUD も使う）が 1 か所で `PickingMode.Ignore` にして写す。削除モードの斜線帯は `Screens/ModeHud/Parts/` の Painter2D（S2-mode-tooltip 計画 D-1・D-10・D-11・D-24） | `OverlayPanel` 層: Web の外から見た重なりは z 31。部品ごとに pickingMode を書く: 書き手が散り、1 つ漏れると HUD の下の入力が奪われる。斜線帯を部品にする: 使い手が 1 画面（§2.7） |
| A-63 | 脈動の拡縮は `Foundation/Motion/Steps/ScaleTweenStep.cs`（新規。前例 `OpacityTweenStep`）として S2-tutorial が作る（実行計画 E-19） | `Screens/Tutorial/` に置いて後で昇格: §1.2 が Motion の Step の置き場を決めており、移す手戻りと他画面が使えない期間が出る |
| A-64 | `ShortcutHintLabel` の Prefix は文に印の引数を足さず、コンテンツ辞書キー・解決済みの文もそのまま受ける。`KeyCap` に色のつまみ `--key-cap-color`（既定 `var(--text-high-contrast)`）を足す。どちらも S2-tutorial が直す（実行計画 E-19） | 画面ルートで `--text-high-contrast` を上書きする: 画面がグローバルトークンで部品の中身を塗ることになり §3.2-2 に反する。integrator に依頼して待つ: KeyControlHint が作れず S2-tutorial が録画の前で止まる |
| A-65 | `TutorialAnchorRegistry.Resolve` はログを出さず、未発見・重複の報告は `TutorialAnchorTracker` が状態の変化のときだけ出す（重複は LogError）。`TutorialAnchor.MarkClipContainer` を足し、USS の `overflow: hidden` で子を切る容器には画面がこの印を付ける義務を持つ（祖先クリップ ADR 0024 の判定に使う）。S2-tutorial が入れる（実行計画 E-1・E-19） | Resolve を呼ぶ側でログを抑える: 毎フレームの追跡で Warning が溢れる。overflow を C# で読む: `IResolvedStyle` に overflow が無い。印の無い容器の検出を L2 にする: USS と要素の対応を静的に取れない（S2-tutorial 計画 D-27。義務は ref-uitk-design へ申告） |
| A-66 | アンカーのカバレッジ L1 は、各 `AnchorIds` が「UITK で `TutorialAnchor.Mark` 済み」か「未移植の表 `TutorialAnchorCoveragePending`（id → 持ち主レーン）にある」のどちらか一方だけを許す。印を付けたレーンが同じ PR で自分の行を消し、S5-ladder で表が空になる（実行計画 E-1） | 未移植のアンカーを L1 から外す: 移植した画面が印を付け忘れても赤にならない。全アンカーの印を今すぐ要求する: 段階 3〜4 の画面が無く満たせない |
| A-67 | 通知の行の生存は Model の定数（Web `NOTIFICATION_DISPLAY_MS`）で、入場 160ms・退場 200ms の出入りは View が `UiTweenAnimation`（前例トースト）で回し、終わったら View が Model の書き口で行を除く。Model は読み口（`IReadOnlyReactiveProperty`）と書き口を interface で分ける。Web の unmount 起因の保険タイマーは写さない（S2-notification 計画 D-2） | Model に `IScheduler` の保険タイマーを持つ: UITK のホストは常駐し外れるのは Dispose のときだけで、保険が要る原因（Web の再マウント）が無い。生存尺を USS のトークンに置く: Web でも store（ロジック）の定数 |
| A-68 | 通知のアイテム獲得のアイコンは C3-a の Content（`GameContentPresentation`）を使い、S2-notification は C3-a に依存する。文言は `UiText` の辞書キーに解決済みの内容名を引数で入れ、言語変更で View が解き直す。名前・Guid が欠けたら理由付きの LogError（S2-notification 計画 D-5 ほか） | 画面の中で `ItemImageContainer` を直接引く: Content 層 [A-4] を迂回し、C3-a の取り込み後に書き直しになる |
| A-69 | Web の CSS の `ease-in`/`ease-out` を `UiEasing` に足し、通知の入場の横移動は `Foundation/Motion/Steps/TranslateXTweenStep.cs`（新規）で表す。S2-notification が入れる（実行計画 E-19） | 画面の中に独自の時間曲線と独自の `IUiAnimation` を置く: 既存の曲線の表し方（`UiEasing`）と Step の前例から外れ、後で昇格する手戻りになる。`EaseOutCubic` で代用する: x=0.5 で 0.875 と CSS の 0.6846 から外れ、出入りの速さが Web と違って見える |
| A-70 | 字間トークンは生成器が UITK の単位へ換算する（em は ×100、px は使う字の大きさのトークンで割って ×100）。px の字間に字の大きさの対応が無ければ生成を止める（I-fix1） | 部品 USS で手換算する: 生成器を直した後に二重補正になり、トークンを読む部品ごとに換算が散る。px のまま出す: UITK では字の大きさ/100 倍にしか効かず、12px の HUD 見出しで Web の 1/8 の字間になる |
| A-71 | `GameScrollView` のホイール 1 ノッチを Web（CEF。CefUnity の `WheelPixelsPerStep`）と同じ 60px にする（`mouseWheelScrollSize = 60 / WheelEvent.scrollDeltaPerTick`）。DSL の `SemanticInput.MouseWheel` はノッチ数を実マウスと同じ正規化値で注入し、`CefInputForwarder` は 1 ノッチ＝`WheelPixelsPerStep` px で CEF へ送る（I-fix1。指揮の決定「1 ノッチを Web 相当に正規化」） | DSL の注入量だけ変える: 実マウスの UITK は 1 ノッチ 54px（既定 18px/行×3 行）で Web と違うまま。既定のまま: DSL の -120 が UITK では 120 ノッチになり設定の本文が末尾へ飛ぶ |
| A-72 | 暗い色の半透明の α 補正の指数を 2.2→1.85 にし、`GamePanelFaceOpacityRamp`（今は `LinearBlendOpacity`）の逆算も同じ指数にする（I-fix1。ランタイムの PanelSettings で地 30〜180 に載せた実測） | 2.2 のまま: 地 30〜180 のどこでも面が Web より暗い（`--surface-navy` で最大 16 段）。トークンごとに手で α を選ぶ: C2-a の推奨値（0.95・0.98）は指数 1.85 の値と同じで、式にすれば他の暗いトークンにも同じ根拠が及ぶ。地ごとの最良値（minimax）: 合う α が地の明るさで大きく変わり、ゲームの地（明るい地表）から外れる |
| A-73 | `DerivedTokens.uss` の半透明の行のうち式で決める行は、生成器（`tools/uitk/webTokens/handWrittenDerivedRows.mjs`）が Web の色から値を計算し、`DerivedTokens.uss` の値と違えば生成を止める（I-fix1） | 表の行を文字列で手書きする: 式を変えたときに手書きの行だけ古い値のまま残る |
| A-74 | 色（半透明・α 補正のトークン）はランタイムの PanelSettings（`targetTexture` に描く Linear 合成）で Web と比べる。部品ギャラリーは配置・寸法・装飾の有無だけを比べる（C2-a 実測・I-fix1） | ギャラリーで色も比べる: EditorWindow は sRGB 合成で、Linear 用に補正したトークンが Web より淡く出る |
| A-75 | 舞台の倍率の式は `StageScaleMeasure`（`TryRead(IPanel, out float)`。C3-b の先頭の実測で変種 A〈`scaledPixelsPerPoint`〉か B〈実画面の幅 ÷ 根の幅〉を決める）の 1 本、毎フレームの見張りは `StageScaleWatch`（GameUi の entry point `GameUiStageScaleWatch`）の 1 点。`StageScaleSource(IReadOnlyReactiveProperty<float>)` がその流れを包み、DI で組まれる宿主（`UiScreenPixelScope`・`UiModalHost`・`HoverTooltipHost`・通知）は ctor で受ける。DI の外で new される `UiPressGesture` だけが押下の時点で `StageScaleMeasure.ReadOrUnit` を呼ぶ。A-41 の「`StageScaleSource` だけが読む」をこの形に改める（C3-b 計画 D-2・D-15） | `UiPressGesture` にも `StageScaleSource` を注入: Components（C4-d の `NodeGraphView`）が new するので、部品が DI に依存する。宿主ごとに `scaledPixelsPerPoint` を直読み: 式が割れ、実測が変種 B のとき直読みの側だけ倍率を外す |
| A-76 | `HoverTooltipHost` は Foundation/Tooltip に置き、面は口 `IHoverTooltipFace`（`SetText`・`AttachTo`）で受ける（実装は Components の `TooltipFace`、DI で差す）。開閉はパネルの木の根の TrickleDown の PointerMove＋Pick で「ポインタの下の対象」を決め、`HoverTooltipMark.TryFindMarked` で印を引く。捕捉の始まりで閉じ、ポインタの下の対象の祖先の `Scroller.valueChanged` で閉じ、スクロールで閉じた後は座標が変わったときだけ開き直す。印の付け外し・文の差し替え（`HoverTooltipMark.OnMarkChanged`）と対象の取り外しでも引き直し、ポインタを動かさずに Web の `disabled` の切替と同じく出し直す（C3-b 計画 D-6・D-7・D-37・D-40・D-42・D-46） | Foundation が `TooltipFace` を直接使う: 層の向き（Foundation → Components の参照禁止）に反する。PointerEnter/Leave: 速い移動で飛び越えた要素・捕捉中の進入の性質が版で違う |
| A-77 | `DialogFrame` の幅は Mantine の `--modal-size`（md 440px）。`UiModalHost` は同時 1 件で `Show` は前の中身を差し替え、幕の押下は `OnBackdropClicked` で知らせるだけで閉じず、フォーカスは閉じ込めない（ESC は UIState）。幕の色は Web の 0.6 を暗い色の式で補正し、生成器の formula 行で照合する（C3-b 計画 D-11・D-12・D-21） | Web のコードの `w={320}`: Mantine の content の `flex: 0 0 var(--modal-size)` に負けて効いておらず、参照 3-modal の実寸は 440。フォーカスを閉じ込める: Web は `trapFocus={false}`、GameUi の入力規則はテキスト欄以外のフォーカスを外す |
| A-78 | `UiDragDrop` はゴーストを出さない。API は源の `IUiDragPayload`（`OnTapped`・`OnDropped(IUiDropTarget)`・`OnDroppedOutside`）と落とし先の印 `IUiDropTarget`。結果は源が解く（解放点の Pick → 最寄りの落とし先）。Foundation の仕組みの通知（押下の分類・ドラッグの結果）は UniRx の `IObservable` でなく受け手 interface（`IUiPressGestureHandler`・`IUiDragPayload`）で出す（C3-b 計画 D-9・D-28） | ゴーストを Grab 層へ: Web の useHotbarDragSource にゴーストが無く、参照と違う見た目になる。`Subject` を複数出す: 1 回の押下の順序を購読側が組み直すことになる |
| A-79 | `ModeSwitch<TValue>` の ctor は `(testid, ModeSwitchOrientation, ModeSwitchRole, options)`。選択肢の文は `UiText` 1 つ、`SetOptionEnabled(value, bool)`、`OnSelected` を `OnOptionSelected` に改名、縦の高さはつまみ `--mode-switch-option-height`（既定 34.8px）、`Tablist` の下は根の padding-bottom。Foundation の USS のために tss の区画 `Foundation/Tooltip`・`Foundation/Modal` を `Foundation/Stage` の後ろに足す（C3-b 計画 D-10・D-14） | 文を辞書キーと生の文の 2 引数のまま: S3-buildmenu のコンテンツのキーが入らない。タブの下を margin: 部品は外側の margin を書かない（A-30）。区画の外の import: 束ねの規約が拒む |
| A-80 | 全体非表示（全部隠し・HideAll）: Ctrl+U・カットシーンの間は UITK のルート 1 つの `display` を None にし、抜けたら `StyleKeyword.Null` へ戻す。BehindStage を含む全層が対象で、要素・購読・打ち出しの状態は残る（Web `App.tsx:86` は部分木ごとアンマウントして状態が消える。差が見えるのは打ち出しの途中で隠して戻したときの本文だけで LX-54）。隠す前に入力欄のフォーカスを外す。ルートの書き換えは Foundation の持ち物で、口は `GameUiRoot.SetWholeUiVisible(bool)`（前例 `SetRootClassEnabled`。中身は `UiLayerStack.SetAllLayersVisible`）。2 源を合わせて呼ぶのは `Screens/WholeUiVisibility/WholeUiVisibilityPresenter`、値の持ち主は S2-skit の `WholeUiVisibilityModel.IsShown`（他レーンは ctor で受ける）。S2-skit が最初の使い手として Foundation の 2 ファイルへメソッドを 1 本ずつ足す（E-19）（S2-skit 計画 D-13・D-26・D-28・D-44・D-47・D-51） | Screens からルートの `style.display` を直に書く: 持ち主の外から書く。隠す層の一覧を持つ: 新しい層を足したときに漏れる。`visibility`: 子の `visible` で漏れる。階層から外す: 要素と購読の状態が消える |
| A-81 | スキットの表示の正は `SkitPresentationStateStore` のモード（`SkitViewModel`・`BlockingSkitPresence` とも）。`SkitManager.IsPlayingSkit` は UIState の Story の出入りだけに使い、`Client.Ui`・`Client.Ui.State` は読まない。モード名・許可 intent の綴り・表示方式（`typewriter`・`instant`）は持ち主 `Client.Skit.UI.SkitPresentationData` の公開定数、許可の判定は同じクラスの `IsIntentAllowed(string[], string)` 1 本で、store の検証（`SkitIntentGate`）と Playtest の skip も同じものを読む（A-56 の延長）。store は分割し、送りの解放の順（版を進めてから待ちを解く）は `SkitIntentWaitController.CompleteAdvance` 1 か所が持つ。スキット画面は面の出し分けにモード全体が要るので store を直接読む（S2-skit 計画 D-4・D-6・D-30・D-32・D-43） | `IsPlayingSkit` を表示の正にする: 直接起動の `StartSkit` 後に偽へ落ちても store は送り待ちを続ける（bd moorestech-xcvd）。綴りを画面・Playtest に写す: 持ち主の変更で判定が無音で外れる |
| A-82 | タイプライタ: blocking の本文は残りの字を透明（`<alpha=#00>`、駄目なら `<color=#00000000>`）にして配置を止める。文の `<` の直後に語結合子（U+2060。駄目ならゼロ幅空白）を挟んで山括弧を字のまま出し、`</noparse>` の字面も削らない。経過時間は `UiMotionPlayer`、数える単位はコードポイント、View が載って最初の描画は即全文。背景の 1 行は Web と同じ切り出し。透明な残りが配置を保つかは S2-skit の L1 で先に測る（S2-skit 計画 D-8〜D-10・D-23・D-48） | Web と同じ切り出し: 打ち出し中に行末の語が跳ぶのは Web の副作用で、目的（同じ見た目）には配置固定が近い（差は LX-51）。`<noparse>` で囲む: 文中の `</noparse>` で抜けられる |
| A-83 | スキットの Enter は UITK のキーイベントでなく `Client.Input.InputManager` の `InputKey` で受ける。`moorestechInputSettings.inputactions` の UI 図に `SkitAdvance`（`<Keyboard>/enter`・`<Keyboard>/numpadEnter`）を足し、`InputManager.UI.SkitAdvance` を `InputSuppressionScope.Keyboard` で作る。入力欄フォーカス中は `InputSuppressionFilter` が止めてログを出し、画面は自前で判定しない。`Client.Ui.asmdef` に `Unity.InputSystem` を足さない（S2-skit 計画 D-11・D-29） | UITK のキーイベント: GameUi は入力欄以外にフォーカスを持たせないので届かない。Screens で `new InputAction`: 入力の第 2 経路と抑止の判定の複製になる |
| A-84 | 会話の操作の拒否は `UiCommandRejectReason.SkitIntentRejected`（良性でない・wire `skit_intent_rejected`・store の失敗コードを Detail）1 つでトーストを出す。選択肢の板は `GamePanelSurface` の種別 `SkitChoice` で描き、画面は internal の `GamePanelFace` を new しない（E-19 の最初の使い手として S2-skit が種別を 1 つ足す）（S2-skit 計画 D-15・D-16・D-37）。Web ホストの `SkitActionHandlers` も `ISkitCommands` を受け、失敗の写しは `SkitCommands` 1 本（S2-skit 計画 D-54） | 失敗コードごとに理由を分ける: 画面の出し分けが無く、Detail で足りる。`GamePanelFace` を公開する: 面の種類の守り（型で分けた A-22 系）が崩れる。板の面を Screens で描き直す: 面の格子の描き方が 2 本になる（種別 `skit` が既に Components にある前例に合わせる） |
| A-85 | `BarSeries` の ctor は testid だけを取り、棒の数は `SetValues` の値の数に合わせる（足りなければ足し、多ければ末尾から外す）。null は溝だけの棒で状態クラス `bar-series__bar--empty`（Web の `data-empty` と同じく見た目の規則を持たない印）。Web の `gap` は先頭以外の棒の左の margin（先頭は `--leading` の印で 0。C3-a 計画 D-15 の形）（C4-b 計画 D-6・D-7） | ctor で `barCount` を固定: Web の部品は数を持たず、値の数と食い違ったときの扱いを部品が決めることになる。`:first-child` で隙間を消す: 構造擬似クラスは禁止（§3.2） |
| A-86 | `ProgressArrowBar` は testid を取らず `name` を Web が固定で付ける `progress-arrow-bar` にする。形は帯（角丸の溝に幅 % の充填。`GaugeBar` と同じ器）で矢印形ではない。値なしの状態を持たず、出し入れは使い手（C4-a の `FluidSlotRow`・S4-machine の採掘機）（C4-b 計画 D-4・D-5・D-21） | testid を取る: 使い手と e2e が同じ固定の名前で照準するのに、綴りを写す箇所が増え Web と違う名前を付けられる。矢印形に描く: Web の CSS は矩形だけ |
| A-87 | `ProgressArrowGlyph` は Web の SVG を、viewBox 121×78 を `xMidYMid meet` で当てはめた箱（`__view-box`）の子要素の層（影・溝・充填の切り抜き・輪郭）で写す。切り抜きは Web の clipPath の矩形（x=2〜2+117×値）を viewBox の幅に対する % の `overflow: hidden` の要素で持ち、静止（Web の `value=null`）は `--idle` で切り抜きごと隠す。作った直後は静止、`SetProgress(0)` は静止と別。線幅は viewBox の単位で倍率を掛ける（`IconButtonCloseGlyph` の前例）・miter・miterlimit 4。充填は `--color-content-primary` で `--gauge-fill` に従わない（Web §8.13 のユーザー裁定）。箱の位置と充填の面の px はルートの `GeometryChangedEvent` で C# が当て、寸法 0（非表示の祖先・配置前）は黙って飛ばし NaN と負だけ LogError（C4-b 計画 D-14・D-15・D-16・D-19・D-25） | 1 つの要素の `generateVisualContent` で 3 層を描き充填を Painter2D の多角形で切る: 切り口を path と矩形の交差で解く幾何が要り、層ごとの色の受け直し（customStyle）も 1 要素に集まる。`SetProgress(float?)` の 1 本: 0 と null の取り違えが使い手のコードで見えにくい |
| A-88 | 矢印の drop-shadow は、影の形（線の外縁）を `offset-y` だけ下げ、縁の内 2σ〜外 2σ（σ＝ぼかしの値そのもの。Filter Effects 1 の drop-shadow は feGaussianBlur stdDeviation=radius で、box-shadow の σ＝ぼかし/2 と違う）を 8 段の輪で刻む頂点カラーのメッシュで描く。輪の α は Web の α×被覆率 Φ(-d/σ) を `LinearBlendOpacity.OpacityAt`（Foundation/Theme） で Linear 合成用に補正し、内端の内側は芯として塗る。濃さは形全体で一律。色は前計算トークン `--progress-arrow-glyph-shadow`（`DerivedTokens.uss`。Web α 0.45 を暗い色の式で 0.6691、生成器の式の行で照合。並べ比べで選び直してよい）（C4-b 計画 D-10〜D-13・D-26） | 形をずらした写しを重ねる（S2-skit LX-52・S2-tutorial LX-35）: にじみが段になり、重なった α が足し算になる。Painter2D の線を太さを変えて重ねる: AA の縁が筋になり頂点ごとに α を補正できない。ぼかしたテクスチャを焼く: 寸法と倍率ごとに焼き直しが要る |
| A-89 | 進捗の 3 部品は Web の CSS module ごとに別々の USS へ写し、器（溝・内周の線・充填）を共有する部品や基底を作らない。`GaugeBar` の器・USS・テストは触らない（値の丸めだけ Gauges の internal `GaugeRatio.Clamp01` 1 本を 4 部品で共有する）。進捗に transition を付けない（Web §8.13）（C4-b 計画 D-1・D-18・D-23） | 共通の器 `GaugeVessel` を作る: Web に無い抽象で、`GaugeBar`（C2-c の持ち物。高さを C# が割る `[A-47]`）を触ることになる |
| A-90 | 掴み絵 `GrabOverlayView` は `Client.Ui/Screens/GrabOverlay/` の画面部品で、`GrabOverlayPresenter` が Foundation の `UiLayer.Grab`／`UiFamily.Viewport` に 1 つ載せる。中身は `ItemSlot("grab-overlay-slot")`、`name` は `grab-overlay`、48×48 をポインタ中心（`translate: -50% -50%`）、部分木の全要素を拾わない。表示は State の `GrabOverlayModel`（`Client.Ui.State/Inventory/Grab/`。掴み `PlayerGrabModel` と画面 `UiRoutingState` を別々に購読し、`GrabOverlayViewModelFactory.Create(screen, grab)` で「掴みを扱う画面 かつ 何か持っている」なら `Holding(ItemId, 個数)`、それ以外は `Hidden`。抽象基底＋sealed 派生 `GrabOverlayHidden`・`GrabOverlayHolding` の判別 union で、前例 `ModeHudViewModel`）が出し、Presenter が `View.Render` へ渡す（§4.2 の形。C4-e 計画 D-1・D-14・D-23） | 絵を Foundation に置く: Components・Content・State を参照して層の規則に反する。`Screens/Inventory/` に置く: 3 画面にまたがり持ち主が割れる。Screens 側で 2 源を合成する: §4.2・A-57 から外れる |
| A-91 | 掴み絵の追従は `PointerMoveEvent` でなく `Client.Input.Pointer.InputSystemPointerPositionStream`（入力と同じフレーム）で、その登録は `MainGameUiRegistration` の基盤区画（CursorTooltip と共有）。分割ドラッグの終わりは `InputSystemPointerButtonStream.OnPrimaryButtonReleased`（Web の window `mouseup`）で、確定（配る副作用と `inventory.split_drag` のトースト）の持ち主は解放を購読する `PlayerSlotDragCommands` の中の 1 か所。S4-alpha に渡す口（`IPlayerSlotDragCommands`）に終わりは無い。同じフレームで解放が押下より先に流れたときは `BeginSplitDrag` が同じ確定をその場で呼ぶ（C4-e 計画 D-2・D-3・D-20・D-26） | `PointerMoveEvent`／`PointerUpEvent`: 拾わない所・パネルの外で届かない（S2-mode-tooltip の実測）。`SlotFrame` に capture を足す: C3-a の部品の挙動を変える。終わりを口に出して呼び手に任せる: 確定が二重になりうる |
| A-92 | `StageColumnsLayout`（`Foundation/Stage/Columns/`）は flex の横並びと 2・3 列目の `margin-left: var(--stage-column-gap)` で Web の grid（auto 3 列）を写し、空の列も列間を保つ。取り付けと研究の畳みは `StageColumnsMount`。研究画面の右の領域（`left: calc(var(--inventory-panel-width) + var(--stage-column-gap))`）とパネル自身の translate は各画面（S4-research・S4-inventory・S4-recipe）の USS で、列ではない（C4-e 計画 D-8・D-9）。DI に VisualElement を出すのは層・列のように画面が持たない共有の器だけで、画面の View は Presenter が持つ（C4-e 計画 D-8） | `UiGapContainer` で列間を当てる: 空の列で Web と間隔が変わる。研究の領域を列に入れる: Web は stage の padding box への絶対配置で、持ち物の高さに引きずられる |
| A-93 | slotActions の持ち主: C4-e は進入（左／右ボタン）・分割ドラッグ（`IPlayerSlotDragCommands`: 開始と進入だけ）・プレイヤー側の番地と行為の型（`PlayerSlotRef`〈main・装備〉・`PlayerItemAction`）と L0-s4a の計画からの写し（`PlayerItemActionConversion.TryConvert`。false はブロック側）・実行器（`PlayerItemActionExecutor`）・持ち物の口（`IPlayerInventoryOperations`。移動の結果は `PlayerItemMoveOutcome` 1 本）・掴みの中身（`PlayerGrabModel`。掴み専用の流れ `OnGrabChanged` から）。S4-alpha は押下・ダブルクリックの解釈と `SlotFrame` からの配線・Shift の拒否の通知・`TryConvert` が false の計画（ブロック側）の実行器・L0-s4a の申し送り（`DestinationFull` の文言・`BlockSlotPlanner`・`DirectMovePlanner`）（C4-e 計画 D-6・D-10・D-24・D-25） | 全部を S4-alpha: 掴み絵と分割ドラッグの確定（C4-e の行）が実行の口なしに作れない。全部を C4-e: 押下の解釈は画面の配線と一体で、ブロック側は S4-alpha の持ち物 |
| A-94 | 持ち物の行為の拒否理由は `UiCommandRejectReason` の `ItemSourceSlotEmpty`（`empty_slot`）・`ItemSourceCountShort`（`insufficient_count`）・`SplitGrabNotEmpty`（`grab_not_empty`）を良性、`SplitDragNoValidSlots`（`no_valid_slots`）をトーストにする（Web `BENIGN_ERRORS` の `inventory.move_item`・`inventory.split` と同じ）。ブロック側（S4-alpha）も同じ理由を使う（C4-e 計画 D-7） | 行為ごとに良性を持つ: 既存の理由ごとの判定と割れ、持ち物には同じコードが行為で割れる例が無い |
| A-95 | 液体の族の API: `FluidSlotContent` は判別 union（抽象基底＋sealed 派生 `Empty`・`Ghost`・`Filled`。各派生は自分に要る値だけを持ち null・0 の埋め草を持たない）で、Web の実体とゴーストの対は使い手が 1 値に畳む。`FluidAmountSlot` の量も判別 union の `SlotAmountBadge`（`Show`／`Hidden`。Hidden は量を持たない）。部品の中だけの純関数 `FluidFillRatio` は internal。部品は `Components/Slots/Fluid/<部品>/`（namespace は族の `Client.Ui.Components.Slots`）。子の testid は Web と同じ固定の `fluid-slot`・`progress-arrow-bar`・`<frameTestid>-amount`（C4-a 計画 D-2・D-4・D-10・D-14・D-15） | Web と同じ `fluid`＋`ghost?` の対を部品が受ける: ありえない組（中身とゴーストの両方）を型で作れてしまう。種類の enum＋全種のフィールドを持つ 1 クラス: 判別子と null・0 の 2 表現が並ぶ（設計検査 Phase 2.6 の 5-A・5-B）。Slots 直下に 3 部品を直に置く: 直下が 11 になり 10 の上限を超える |
| A-96 | 液体の色は必須で、形式（`#RRGGBB`）はサーバーの `FluidMasterUtil.Validate` がマスタのロード時に検証し、外れたら起動で止める。`GameContentPresentation.FluidFace(FluidId)` は §2.1 どおり `Color` を返し、中で `ColorUtility` で解く（検証済みなので解けなければ検証の穴として例外）。空の液体は `ArgumentException`。部品は色の欠けを扱わない。容量はサーバーの `FluidContainer(double capacity)` が非有限・負の値を `ArgumentOutOfRangeException` で止める（全部の容量の合流点。割合の NaN は量の NaN だけになる）（C4-a 計画 D-7・D-15・D-16・D-18）。容量の検査は ctor にあるので、壊れた容量の mod は起動では止まらずワールドのロードかブロックを置いた時点で落ちる（マスタのロード時の検証へ寄せるのは C4-a 計画「本 PR 外のリファクタ提案」）。色の形式を縛るので、`#RRGGBB` 以外で色を書いた既存の mod は起動で止まる | 描くときに解き、読めない色は LogError を出してフィル無し（`Uncolored`）で描く: 本番の console は気づく経路にならず、4 つの Presenter に同じ分岐を配る。読めない色を黒・白に倒す: 壊れたマスタの色に誰も気づかない。スキーマの pattern で検証する: `fluids.yml` に機構の実績が無い |
| A-97 | `OutlinedCountLabel` の縁の層の色はつまみ `--outlined-count-label-stroke`（既定 `var(--icon-text-stroke-light)`）。`.iconTextOutlineDark` の置き場（FluidSlot の量・ホットバーの番号）は置き場のクラスで `--icon-text-stroke-dark` に上書きする（A-59 の形。C4-a 計画 D-5） | 黒縁の 2 つ目の部品: 縁の色だけが違う重複 |
| A-98 | C4-a は C4-b に依存する（`FluidSlotRow` が `ProgressArrowBar` を描く）。C4-b は W3 を待たず先行させる（実行計画 E-21）。`FluidSlotRow` は C4-a の最後の Task に置き、C4-b のマージ前でも Task 1〜4 は進められる（C4-a 計画 D-3） | 矢印の口だけ空けて後から足す: 使い手の S4 が 2 回取り込み直す。C4-a で `ProgressArrowBar` を作る: C4-b の所有を侵す。W3 の枠 2 の先頭を C4-b にする: クリティカルパスの C4-e が遅れる（E-21） |
| A-99 | `FluidSlot` のゴーストは絵と量だけに `--fluid-slot-ghost-opacity`（既定は `--slot-frame-ghost-opacity` を共有し、dark-8 の面の上の実測で選び直す。LX-66）。量の基線は Web の本文の行高 1.55 の半行間 `--fluid-slot-amount-bottom` 3.3px を下端に足して合わせる。`FluidAmountSlot`「液体名が辞書に無ければ」は捨てる（UITK の液体名は必ず解ける）（C4-a 計画 D-9・D-11） | ゴーストをルートの opacity で: UITK の opacity は要素ごとに掛かり、枠の線と面まで透ける |
| A-100 | 配置対象の絵の出どころと、ホットバーへの落としの受け口は、先に走る S3-buildmenu が最終の置き場に作り、S3-hotbar-modal は使うだけにする。(1) `Client.Ui.State/PlacementTarget/PlacementTargetIcon`: ID だけの判別 union（Block・TrainCar・ConnectTool・撮影済みの Blueprint・NameOnly。値で等価。Blueprint だけは参照の等価で、撮り直しで絵が変わるため）。State は Texture2D とアイコンの容器を持たない (2) `PlacementTargetIconResolver.Resolve(IPlacementTarget, IBlueprintThumbnailLookup)`（thumbnails は撮影済みかの判定だけ） (3) `Client.Ui/Content/PlacementTargetIcons`（DI のクラス。`bool TryToUiIcon(icon, out UiIcon)` で値だけを返し、面へ当てる分岐は画面が持つ。車両・接続ツールの絵は `IGameContentTextureSource` に足す 2 メソッドで引く） (4) `Client.Ui.State/Hotbar/Logic/IHotbarDropReceiver`（`void Receive(HotbarDragSource)`）。受け口でない落とし先への落としは枠外と同じく何もしない（Web と同じ正常操作で、ガードの縮退ではない） | 置き場の出所は §5.1-1（Web でビルドメニューとホットバーの 2 機能が既に `ResolveIconUrl` を共有）。後から走るレーンが先のレーンのファイルを書き換えると所有が混ざる。Content が値だけを返し State が ID だけを持てば、層の向き（§1.1）と [A-4] に沿う。状態を書く口なので bool を返さない |
| A-101 | [A-47] の改め（ビルドメニュー）: マスの面は `.build-menu__entry` の `--icon-pad: 4px` で決まる（面 = `--slot-size` − 8px）。C# が導く calc は Web で実際に効いているクリップの逃げ（`.scroll` の負の margin・viewport の padding・縦バーの inset。`--tutorial-anchor-clip-inset` から）にし、`BuildMenuScrollClipInset` が `--build-menu-scroll-clip-inset` から導く | Web の `.slotIcon`・`.slotLabel` は `BuildMenuSlot.tsx` から使われていない CSS で、面の大きさは `PlacementTargetFace` の `--icon-pad` が決めている。使われていない CSS を写すと Web と違う物になる |
| A-102 | ビルドメニューの State は `Client.Ui.State/BuildMenu/` の `Entries`（VM・合成）・`Source`（Model・工場）・`Commands` に分ける。検索・群分け・サイドバーの無効は純関数 `BuildMenuShownListComposer` 1 か所。エントリの規則は Web の橋 `BuildMenuEntryDtoFactory` を写し、橋の DTO と突き合わせるテストで縛る | State は L0 でテストでき、見出しの名前が VM にあれば L1 が Web の英語名を逐語で入れられる |
| A-103 | `IBuildMenuScrollViewport` の実装は表示ごとに作り直す。隠す直前に位置を保存し、最初の `SetScrollTopImmediately` まではスクロール通知を追跡器へ流さない（転送の門。実測で `OnScrolled(0)` が保存位置を潰さないと分かれば外す）。スペーサの `GeometryChangedEvent`・`GameScrollView` の中身のレイアウト変化・各カテゴリ群の `GeometryChangedEvent`（Web の群ごとの ResizeObserver）の 3 つで `OnLayoutChanged` を呼ぶ。視口が今の表示でまだレイアウトされていない間（閉じた後の高さ 0 を含む）は見出し・群を測れないとする | 追跡器の `AttachViewport` は同じ視口に冪等なので使い回すと復元が起きない。スペーサだけが変わったときに引き直さないと末尾へのジャンプが手前で止まる。全体の高さが同じまま群の配分だけ変わるとハイライトが古いまま残る。閉じた後の 0 を実測と取り違えると復元位置を 0 に潰す（L0-s3 の後段への 5 点） |
| A-104 | サイドバーのジャンプの smooth scroll は `UiMotionPlayer` で 300ms・EaseOutCubic に固定し、見た目の例外 LX-71 に記帳する | Web はブラウザが長さと緩急を決めるので決まった値が無い |
| A-105 | ホットバーは `UiLayer.Chrome`・`UiFamily.Viewport`。落とし先は枠の印（`HotbarSlotDropMark`・列の隙間の `HotbarRowGapDropMark`）が `IUiDropTarget` と `IHotbarDropReceiver` の両方を実装して受け、`HotbarDropResolver.Resolve` → `IHotbarCommands.Apply` で解く。印はホットバーの語彙なので `Screens/Hotbar/Parts/` に置く | 源が操作を持つ形にすると、ビルドメニューの源にホットバーの操作を渡すことになる |
| A-106 | 選択を受ける UI 状態の規則は `Client.Ui.State/Hotbar/HotbarSelectRule` 1 か所。`IHotbarCommands.Select` は `UiCommandResult` を返し、受けない状態は良性の拒否 `HotbarSelectNotAccepted`（wire は Web `HotbarSelectActionHandler` と同じ） | Web は同じ規則を 2 か所に持ち、ずれると「光っていないのに選べる」が起きる。結果を返すのは [A-8] の形 |
| A-107 | モーダルには Model を作らない。Presenter が `ModalRequestService` の保留を購読し、要求 1 件ごとに `ModalView` を作り直して `UiModalHost.Show` する。応答は `Client.Ui.State/Modal/IModalCommands`（`UiCommandResult Respond(ModalResponse)`。古い応答は良性の拒否）で、View が確定・取消・幕の押下を直接呼ぶ（[A-8]）。ESC は Web の Mantine と同じ 1 回で取り消す。入力欄では既存の規則（`GameUiInputRules`）が ESC を消費してフォーカスを外し、View はその外れを、このフレームの ESC が消費済み（`UiInputExclusivity.IsEscapeConsumedThisFrame`）のときだけ取消と読む（規則は書き換えない。変換中の ESC は規則がフォーカスを外さないので閉じない）。フォーカスの無い ESC は View が聞かず、既存のゲーム入力の経路（`InputManager.UI.CloseUI` → 建築モードの進行中の操作の取消 → BP 名の入力の `Close` → 保留の取消）が取り消す（[A-83] と同じく UITK のキーイベントは届かない） | 状態の正は既に State にある。1 回目はフォーカスだけ外し 2 回目で閉じる: Web と押す回数が違い、入力欄の ESC は規則が伝播を止めるので 2 回目が View に届かない。規則に「モーダルでは閉じる」を足す: Foundation の規則にモーダルの語彙が入る |
| A-108 | 世界の上に直に置く半透明（ホットバーの `--slot-frame-face` の画面ルートでの上書き・番号タブの面・未解決と受理外の opacity）は、ランタイムの PanelSettings（Linear 合成）で明るい地と暗い地を描いて画素を読み、Web の sRGB 合成の値と照合して決める。明るい地を優先して合わせ、暗い地で外れた分は LX に記帳する。番号タブの暗い縁（Web `.iconTextOutlineDark`）は、部品 `OutlinedCountLabel` の縁の色のつまみ `--outlined-count-label-stroke`（既定 `--icon-text-stroke-light`）を画面で上書きして出す（共有トークンを部分木で裏返さない）。つまみは同じ暗い縁を要る C4-a（`FluidSlot`）と S3-hotbar-modal のうち先に入るレーンが足し、後のレーンは `origin/feature/uitk` にあれば使うだけにする | Linear 合成では 1 つの α で両方の地を Web に揃えられない（bd albof.34。指揮の判断「Web の見た目に合わせる」） |
| A-109 | ホイール素通しの印 `WheelPassthroughMark`（Web の `data-wheel-passthrough`）は最初から `Foundation/Input/` に置き、印（`Mark`）と読み手の判定（`IsPassthrough`。Web `isWheelPassthrough` の祖先たどり）の両方を S3-hotbar-modal が作る。判定を使って装備のホイールを素通すのは S4-inventory | Web で既に 2 機能（ホットバー・装備）が使うので §5.1-1 に当たる。印だけ先に置くと、判定が Web と同じかを確かめる場所が無い |
| A-110 | `OutlinedCountLabel`（アイコン上の数の白い縁）は `VisualElement` 派生の部品でなく、置く側の要素へ Label 2 枚（縁だけの層と縁の無い字の層）を兄弟で足す形を部品規則の例外として認める（指揮の判断。C3-a の moores-code-review 設計判断 D1 案B） | `-unity-text-outline` を字に直に掛ける: 字の内外に半分ずつ掛かり 12〜19px の細い数字が白く潰れる。2 層を箱（VisualElement）に入れる: 箱に入れる 4 通りとも幅の丸めが変わって字の画素が Web とずれた（C3-a 実測）。画素が Web と一致する形を優先する |
| A-111 | ランタイム撮影（本番の PanelSettings の複製・sRGB の RenderTexture に描いて読む）と Game View の解像度切替は `Client.Ui.Editor/Capture/` の `UiRuntimePanelCapture`・`GameViewResolution` に置き、uloop の `execute-dynamic-code` 1 行と DSL の `PlaytestGameViewOps.SetResolution` から呼ぶ（A-74 の共有化。I-fix2） | レーンごとの使い捨て手順: C2-a・C3-a・S2-hud・C3-b・S2-mode-tooltip が同じ反射コードを書き直していた。`PlaytestDriver` に足す: 199 行で 200 行を超える |
| A-112 | 通知の文字影は `--notification-text-shadow` として `DerivedTokens.uss` に手書きする（Web はトークンを持たず `notification/style.module.css:38` の直書き）。値は Web のまま、α は補正しない（同じ値の `--key-hint-text-shadow` も補正していない）（I-fix2） | Web の tokens.css にトークンを足す: 束 2 の Web 変更になり記帳が増える。`--key-hint-text-shadow` を借り続ける: キーヒントの値を変えると通知も変わる |
| A-113 | 明るい色の半透明の α 補正の指数も 1.85 にし、明暗は輝度でなく最大成分 0.5 で分ける。ランタイムの PanelSettings で α 0.01〜1.00 × 地 8 種を描いて Web の sRGB 合成と比べた（I-fix2）。中〜明るい地の残差は LX-81 | `α^2.2` のまま: 暗い地で白 22% が 17〜24 段淡かった。輝度で分ける: 純色の赤（輝度 0.21）が暗い色の式に入り暗い地で 87 段濃かった。トークンごとに α を選ぶ: 合う α は地の明るさで 2 倍以上動き、使い手の地が決まるまで選べない |
| A-114 | 辞書キーの `UiText` は引数 0 件でも補間（`Localize.GetFormatted`）を通し、残った `{name}` は補間器のエラーログで知らせる。L2 の層の規則で Foundation に `Core.Master`・`Client.Mod`、Components に `Client.Mod` を禁止に足す（C3-a で `Client.Ui.asmdef` が両方を参照したため）（I-fix2） | 引数 0 件は `Localize.Get` のまま: `{count}` 等の枠が無言で画面に出る。UiText で枠を自前検出: 補間器と検出が 2 か所になる |
| A-115 | `NodeGraphView` の公開口は `(string testIdPrefix)`・`SetGraph(NodeGraphContent)`・`OnNodeTapped`・`OnViewportChanged`・`RestoreViewport` にし、任意の時に寄せる `CenterOn` は持たない。ビューポートのセッション内の保持は画面の State が持ち、部品は値を流して受けるだけ。State の Session は素の値（x・y・倍率。前例 `BuildMenuSessionSnapshot`）で持ち、`NodeGraphViewport` との詰め替えは画面（`Screens/<機能>/`）がやる（`Client.Ui.State` は `Client.Ui` の型を参照できない）（前例 `GameScrollView.OnScrollOffsetChanged`／`SetScrollOffset`・`BuildMenuSessionStore`）（C4-d） | `CenterOn` を残す: Web に無い挙動でテストの写し先も無い。Web と同じキー文字列の静的な Map: 部品が全体の可変状態を持ちテストの間で漏れる。可変の保持の入れ物を ctor で渡す: 部品が外の可変状態へ書き込み Set／IObservable の約束が崩れる |
| A-116 | 滑走は Foundation の `UiInertiaAnimation`（閉じた式）を離したときの向きに固定して 1 次元で載せる（総距離は Web より 2.5% 短い。LX-87）。速度の時刻はビューポートに `UiPressGesture` より先に登録したポインタ事象の `timestamp` から読む（C4-d） | Web の離散積分を部品に写す: 動きの基盤が 2 本になる（§1.2）。`UiPressMove` に時刻を足す: Foundation の変更でレーンの所有外（integrator へ申告） |
| A-117 | 線は Painter2D の 1 層をキャンバスの最初の子に置き 1 本のパスで描く。中心は Web と同じく点から線幅の半分下。グラフに無い親のキー・重なったキーは描かず `Debug.LogError`（C4-d） | 線ごとに回した 3px の要素: 要素がノード数だけ増えレーンの指定と違う。点を通す: Web から 1.5px ずれる。Web と同じく黙って飛ばす: AGENTS.md が無音の縮退を禁じる |
| A-118 | ホイールは UITK の 1 ノッチ（`WheelEvent.scrollDeltaPerTick`）を Web の deltaY 60 に換算し、カーソルは内容 box の座標に直す。60 は `GameScrollView` と同じ値の 2 本目として integrator へ申告し、Foundation には作らない（C4-d） | UITK の delta をそのまま使う: ズームの速さが Web と変わる。Foundation に共有の定数を新設する: レーンの所有外 |
| A-119 | 初回の中央寄せはノード・注目点・クリップの逃げ・寸法がそろった最初の 1 回（`SetGraph` か `GeometryChangedEvent`）だけ行い、寸法の追従も逃げを読めた後の寸法だけを前回の値にする（C4-d） | `SetGraph` の時だけ寄せる: UITK はレイアウトが次のフレームで寸法が 0 のまま寄せてしまう。逃げの前の寸法で寄せる: padding が当たって 12px ずれる |
| A-120 | `RecipeRow` はスロットを画面が作った `VisualElement`（`ItemSlot`・`FluidAmountSlot`）の列で受け、操作は画面がスロットを直接購読する。矢印の testid は ctor の `arrowTestid` で受ける（Web の `arrowTestId`。クラフト `craft-arrow-<guid>`・機械 `machine-recipe-<guid>-arrow`）（C4-c 計画 D-1・D-2） | `SetRecipe(RecipeRowContent)` で中身を受ける: 部品が `ItemSlotContent` と液体の内容の 2 型を知り、量バッジ・不足の表示まで中継する（Web に無い抽象）。`OnSlotGestured` を中継する: スロット 2 種で操作の型が違い、中継の型を新設する。矢印の testid を行の testid から導く: Web の e2e が引く名前と食い違う |
| A-121 | スロットの寸法は `RecipeRow.uss` の `.recipe-row__slots` が宣言するつまみ（`--slot-size: var(--recipe-slot-size-max)` ほか）の継承で決め、列の幅に合わせて縮めない（はみ出しは `Debug.LogError`。LX-91）。列数（1〜2）は `RecipeSlotColumn` が持つ（C4-c 計画 D-3・D-4・D-21） | `SlotFrame` に寸法の Set を足す: C3-a の部品の変更で所有外、幅が足りない場面の実在が未確認。`transform: scale` で縮める: 文字・リングの太さまで縮む。State の `RecipeSlotLayout` を使う: Components は State の Text しか参照できない（`UiLayerDependencyRules`。2 本目の式は integrator へ申告） |
| A-122 | `RecipeRow` の光彩（`0 0 2px 0 rgb(0 205 255 / 25%)`）・外周の輪（1.2px）・括弧の影（`drop-shadow(0 0 1px rgb(47 188 255 / 30%))`）・四隅の括弧（9×1・1×9）は拾わない最初の子 `RecipeRowFrame` が頂点カラーで描く。輪と括弧は矩形 12 枚、光彩と影は A-88 と同じ 8 段の輪の刻みで ±2σ（σ は box-shadow がぼかしの半分、drop-shadow がぼかしそのもの）を被覆率（光彩は Φ(-d/σ)、太さ t の括弧の棒は両縁の差 Φ(-d/σ)−Φ(-(d+t)/σ)）と明るい色の式 α^1.85 で塗る。位置は C# が `--recipe-row-frame-outset` から inline で当てる `[A-47]`（C4-c 計画 D-5） | 光彩と影を描かず例外にする: 装飾の有無は一目で分かる差（§3.4）で、A-88 の刻みで描ける。`LinearBlendOpacity` で α を出す: 暗い色の式しか持たず明るい色の光彩が濃すぎる。border の入れ子で輪を作る: 括弧の 8 本は border で作れない。USS の負の値で位置を書く: outset と線の太さの 2 つから決まり USS に calc が無い |
| A-123 | `RecipeActionButton` は `Button` の派生で、面の子（16 列の頂点カラーの横 3 色グラデ・sRGB で補間）と文言の `LocalizedLabel` を持ち、通常・ホバー・無効の 3 組の色と寸法・字・縁・沈み・無効の opacity をつまみで受ける。字の太さは ctor の `RecipeActionLabelWeight`（`Medium` 500〈クラフト〉・`Regular` 400〈研究〉）で Web と同じ字形を選ぶ。文字の 4 方向 1px の影は文字の縁で近似する（LX-93）（C4-c 計画 D-6〜D-8） | 背景画像のグラデ: USS に gradient が無く色をつまみで替えられない。500 に固定して研究を例外にする: 太さの差は一目で分かる（§3.4・A-36）。太さのつまみを足す: `-unity-font-definition` は字の資産の差し替えでつまみ 1 本では書けない。研究の画面が子孫セレクタで字形を差し替える: 部品の文言のクラスに画面が触り、和文の規則も二重になる |
| A-124 | `RecipeListScrollView(string listTestid)`・`SetEntries(IReadOnlyList<VisualElement>)` とし、行の列は Foundation の `UiStack(UiGap.None)`。行の間隔はつまみ `--recipe-list-gap` を部品 USS が 2 クラスで `--ui-gap` へ渡し、画面が祖先のクラスで上書きする（Web の `<Stack gap={listGap}>`。前例 `CurrentChallengeHud.uss`）（C4-c 計画 D-9） | `listContainer` を公開する: 画面が `UiStack.AddItem` を通さず足すと間隔が当たらない。float の `listGap` で受ける: トークンの値を C# に写す。部品が自前で 2 行目以降の margin を当てる: `UiStack` と同じ役割の 2 本目で、隠した行を数え先頭の USS の margin を 0 で消す |
| A-125 | `LevelInheritanceBadge(string testid)`・`Show(UiText)`・`Hide()`。文は State の `LevelInheritanceBadgeText.TryBuild` を画面が解いて渡す。testid は `Show` で作る文のラベルが持ち、`Hide` はラベルを外して testid の要素を木に残さない（Web は継承しないとき `null` を返す）（C4-c 計画 D-10） | `SetBadge(LevelInheritanceBadgeContent)`: 文を作る型が State に既にあり部品が内容の型を知る必要が無い。ラベルを ctor で作る: 文の無い `LocalizedLabel` は 1 フレーム後にエラーを出す。根に testid を付け `display: none` で隠す: testid の要素が残り Web の「要素が無い」と食い違う |
| A-126 | Web の CSS module の直書き色とつまみの既定（`--recipe-row-*`・`--recipe-action-*`・`--research-action-disabled-face`・`--research-action-disabled-opacity`・`--recipe-list-gap`）は C4-c が `DerivedTokens.uss` の末尾の「C4-c」ブロックに書き、半透明は A-113 の式で α 補正して生成器の式の行で照合する。研究の無効の opacity（Web 0.6）は暗い面に A-113 の暗い色の式を当てた値を初期値にし、C4-c の並べ比べで Web の画素と比べて確定する。研究の画面（S4-research）は幅と高さだけを自分の撮影で決め、色・淡さは `var()` で当てる（C4-c 計画 D-11・D-15。A-46 の「integrator が書く」を改める） | integrator が書く（A-46）: 値の正本が部品の Web の CSS で、部品と同じ PR で L1 が値を確かめる方が食い違わない。衝突は末尾の追記どうしで両方を採る。研究の無効の opacity を S4-research が決める: 部品の見た目の値の持ち主が 2 つになる |
| A-127 | `MachineRecipeSelectionRow.test.ts` の 5 件は、どれもレシピのデータから行・液体のスロット・バッジへの結線（GUID・量・testid・継承の判定）を確かめるので、5 件とも S4-machine の「写す予定」にする。C4-c は同じ見た目と挙動（液体の枠が量付きで並ぶ・バッジの文と隠し）をタグ無しの部品 L1 で固定する。部品そのものの vitest は無い（C4-c 計画 D-12） | 部品で決まる 3 件（液体・バッジあり・なし）を C4-c がタグ付きで写す: 部品の L1 は自分で組んだ値を渡すので、画面が量を取り違えてもバッジを出し忘れても通り、結線を確かめたことにならない。5 件とも C4-c で写す: 名前と選択の枠は画面の包みの責務で、部品レーンに無い |

### 7.2 ユーザーに上げるもの

| ID | 問い | 推奨 |
|---|---|---|
| （Q-1） | 取り下げ。裁定「全段階で Web 版と同じ見た目」から答えが決まるので agent 既定 [A-39] にした（実行計画 E-11） | — |
| Q-2 | `NodeGraphView` の掴む手のカーソル（Web `cursor: grab`・パン中 `grabbing`）をどうするか。(a) USS の `cursor: url(...)` でテクスチャのカーソル（手の絵のアセットが要り、`Client.Ui` に前例なし） (b) 写さず LX-86 に記帳（形だけの差で操作は同じ） (c) 画面全体の入力（OS カーソルの持ち主）へ渡し、部品はパン中の状態を流すだけ。裁定まで矢印のまま（C4-d 計画 D-12） | (a)。ゴールが「Web と同じ見た目」で、USS の `cursor` は部品の USS だけで閉じる（手の絵 2 枚のアセットが要る）。基盤を足さず安く済ませるなら (b) |

---

## 8. 未確認

- Web で実際に使う Mantine Button の variant と、`MantineProvider` の theme 上書きの有無（C2-b と F1 の頭で grep して決める）
- （解消）`GamePanel` の外周の影は A-22 で描かないことにした（実測は C2-a が台帳「## 実測の結論」に記す）
- USS のカスタムプロパティの継承で、つまみの局所上書きが Web と同じ範囲に効くか（F1 で `--slot-size` 1 本の L1 を先に書いて確かめる）
- `Client.Ui.Editor` の EditorWindow に本番のランタイムテーマ（tss）を当てて、PlayMode と同じ見た目で撮れるか（F1 で確かめる。撮れなければ PlayMode の見本シーンへ切り替える）
- `UiFamily` の舞台の倍率が Web の `--ui-scale`（`W/app/App.tsx:39-47`）と同じ式になるか（F1 で 720p・2560×1080 の 2 解像度で比べる）
- `panel.scaledPixelsPerPoint` が PanelSettings（ScaleWithScreenSize・Expand・1280×720）の倍率と同じ値か（C3-b の先頭で 720p・1080p・2560×1080 の 3 つで実測し、違えば `StageScaleSource` が `panel.visualTree.layout` と `Screen.width/height` の比で倍率を出し、`UiScreenPixelScope` と通知はその値を購読するだけにする。[A-41]）
- `--bevel-inset` のキーワード（`inset`/`outset`）を `CustomStyleProperty<string>`（`UssCustomStyleReader.TryReadKeyword`）で読めるか（C3-a の先頭で確かめる。読めなければ同じ名前 `--bevel-inset` のまま数 1／-1〈1＝外側・-1＝内側〉で受け、§2.3 を直す。画面の上書きの名前を Web と揃えるため。C3-a 計画 D-3・[A-42]）
- UITK の `Label` のリッチテキスト `<line-height=Npx>` が折り返しを含む 2 行の行送りに効くか（C2-c 計画 Task 3 Step 6 で実測。[A-48]）
