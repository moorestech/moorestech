---
name: ref-uitk-design
description: moorestech のゲーム内 UI を UI Toolkit（USS・tss・Client.Ui）で作る規則と、見た目の正本（Web 版）の引き方。層・部品カタログ・USS とトークン・画面の型・比べ方・例外の記帳を持つ。Use When — Client.Ui・Client.Ui.State・USS・tss のコードを読む・書く・レビューする時、UITK の部品・画面・HUD を足す時、UITK 側の色・寸法・配置・装飾・動き・入力を変える時、Web 版との見た目の差を直す・記帳する時（Web 版の CSS そのものを直す作業は ref-webui-design）。
---

# moorestech UITK の作り方と見た目

対象は `moorestech_client/Assets/Scripts/Client.Ui`（描画）と `Client.Ui.State`（画面状態）。略記: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/src/`。

規則は逐語で守る。表に無い CSS の写し方だけはレーンが決めて `references/css-to-uss.md` に 1 行足す。

**印の読み方**: 〔レーンID〕の付いた名前・仕組みは、そのレーン（`docs/uitk-migration/execution/execution-plan.md` のレーン表）が `feature/uitk` に入るまで存在しない。無い間は自分で作らず（そのレーンの持ち物）、括弧の中の今の形に従う。そのレーンは自分を取り込む PR でこの文書と references の自分の印を外す。

## 1. 見た目の正本は Web 版

- 全画面を Web 版と同じ見た目にする（ユーザーの裁定。移行で見た目を変えない）。例外はユーザー原文「無理に同じにする必要はない、影響が小さく非本質的な対応が入るのであれば、推奨でも良い」だけ（§11）
- 見る順: ① その画面・部品の Web の CSS module（`W/shared/ui/<部品>/`・`W/features/<機能>/`）② `moorestech_web/webui/docs/design/components.md` の該当節 ③ 参照キャプチャ `moorestech_logs/uitk/reference-captures/web-<sha>/`（撮影元は台帳 `docs/uitk/TODO.md` の「## 参照キャプチャ」。画面と状態の一覧は `index.md`）
- 数値は Web の CSS から逐語で写す（rem は ×16 の px、em はその要素の文字サイズ（px）× 値）。目で合わせた値を足さない
- 面は Web 版のその画面と同じ枠にする。Web が GamePanel で描く画面は `GamePanel`、Mantine の枠で描く画面（ポーズ・設定・バグ報告・トースト・モーダル・チャレンジ）はその枠を写す。面を GamePanel に統一しない
- `ref-webui-design` のホワイトリスト（装飾 6 語彙・スロットは共有部品だけ・全画面の不透明面の禁止・stage 族と viewport 族）は、Web 版に無い新しい面を作るときにだけ効く。Web 版にある見た目はそのまま写す

## 2. 層と依存の向き

| 層 | 置き場 | 参照してよい | 参照しない |
|---|---|---|---|
| Foundation | `S/Client.Ui/Foundation/<領域>/` | Client.Input・Client.Localization・UniRx・UniTask・Client.Game の入力・UIState・アンカー ID | Components・Content・Screens・Client.Ui.State の機能別の型 |
| Components | `S/Client.Ui/Components/<族>/<部品>/`〔F1〕（既存の部品は F1 が namespace とともに族へそろえる。設計書 [A-7]） | Foundation・`Client.Ui.State` の `Text`（`UiText`）・`LocalizationKey` | Content・Screens・Client.Game・`MasterHolder`・Client.Ui.State の機能別の型 |
| Content〔C3-a〕 | `S/Client.Ui/Content/` | Foundation・Components の値型・Client.Game（`ClientContext`）・Core.Master | Screens |
| Screens | `S/Client.Ui/Screens/<機能>/` | 上のすべて・Client.Ui.State | 他の `Screens/<機能>` |
| State | `S/Client.Ui.State/<機能>/`（別 asmdef） | Client.Game・Client.Network・Core.Master | Client.Ui・UnityEngine.UIElements |

- 部品はドメインを知らない。ItemId・BlockId・Model を受けず、`UiIcon`〔C3-a〕・`UiText`・数・値型を受ける。ID から表示素材への解決は Content の `GameContentPresentation`〔C3-a〕の 1 か所に置く
- asmdef は `Client.Ui`・`Client.Ui.State` の 2 つ。層の向きは L2 の `UiLayerDependencyDirectionTest`〔F1〕が using で止める。破れたら asmdef の分割を裁定に上げる

## 3. 部品カタログの引き方

- 部品を作る・使う前に `references/component-catalog.md` で名前・置き場・公開 API・作るレーンを確かめる。カタログに有る部品を画面の中に作り直さない
- 名前は Web の `shared/ui/<名>` と同じ。Web に名前が無い Mantine 由来の見た目は役割の名前（`FormButton`・`DialogFrame`）にし、技術名を入れない
- Web で 2 feature 以上が使う部品は、その段の部品レーンが Components に直接作る。Web に無い新しい共有は 1 機能目を `Screens/<機能>/` に置き、2 機能目のレーンは自分で作らず指揮に申告する（integrator が波の境で Components へ移す）。1 画面の中の複数ファイルで使うだけの部品は `Screens/<機能>/Parts/`
- 部品を作ったレーンは、カタログの行の「段階 1」列をその部品の実装どおりに直す

## 4. 部品の書き方

```csharp
// web: W/shared/ui/IconButton（components.md §8.6・設計書 [A-27]）。形の例で、IconButton は〔C2-b〕が作るまで実在しない
// web: W/shared/ui/IconButton (components.md §8.6, design [A-27]). Shape example only; IconButton does not exist until C2-b
public sealed class IconButton : VisualElement
{
    private readonly Subject<Unit> _onClicked = new();
    public IObservable<Unit> OnClicked => _onClicked;

    public IconButton(string testid, VisualElement glyph)
    {
        // testid は name へ、ボタンでもフォーカスは持たない
        // testid goes to name; buttons never take focus
        name = testid;
        focusable = false;
        AddToClassList("icon-button");
        // 図形は画面が作って渡す。描くだけの子なので Pick しない
        // The screen builds the glyph; it only draws, so it ignores picking
        glyph.pickingMode = PickingMode.Ignore;
        Add(glyph);
        // 押下は Subject で外へ出す
        // Presses leave through the Subject
        RegisterCallback<ClickEvent>(_ => _onClicked.OnNext(Unit.Default));
    }
}
```

- ctor は `(string testid, …)` で `name = testid`（Web の `data-testid` と同じ綴り。DSL のシナリオがそのまま動く）。装飾だけの部品と、他の部品の中身として置かれ Web も `data-testid` を付けない部品（`SlotContent`・`KeyCap` 等。ctor はカタログどおり）は testid を取らない。デフォルト引数を使わない
- 表示する値は `SetXxx(値)` で受ける。値を返す素通しのプロパティを作らない（`{ get; private set; }` は可）
- 操作は `IObservable<T> On<変化対象><過去分詞>`（中は UniRx の `Subject`）で出す。`Button.clicked`・`Action`・`event`・コールバック引数で外へ出さない。素の `Button` を中で使うなら `clicked` は部品の中で `Subject` へ流すだけにする
- 部品は `VisualElement` 派生にする。例外は `OutlinedCountLabel`（置く側の要素へ Label 2 枚を兄弟で足す。箱に入れると幅の丸めが変わり字の画素が Web からずれた。設計書 [A-110]）
- `focusable = false`。フォーカスを持つのは入力欄だけ
- 描くだけの子（Painter2D・頂点カラー・装飾）と文字だけの子は `pickingMode = PickingMode.Ignore`。押せる要素だけが Pick を受ける
- 子を受ける部品（枠・スクロール）は `contentContainer` を返し、外は `Add` で入れる
- 色・寸法を C# に書かない。描画で値が要るときは部品の USS で `--<部品>-<名>: var(--トークン);` と受け直し、`CustomStyleResolvedEvent` の中で `UssCustomStyleReader.TryReadColor`・`TryReadPixels` で読む（前例 `S/Client.Ui/Components/Panels/GamePanel/GamePanelFace.cs`）
- `Update()` を使わない。変化は Set の呼び出しか Model の購読で受ける
- AGENTS.md のとおり 1 ファイル 200 行・1 ディレクトリ 10 ファイル・`partial` と `Func<>` の禁止・日英 2 行のコメント

## 5. USS の書き方

1. 1 部品 1 USS を `.cs` の横に置く（`Components/<族>/<部品>/<部品>.uss`〔F1〕）。画面の USS（`Screens/<機能>/<機能>.uss`）は配置とつまみの上書きだけを持つ
2. 新しい USS は `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` の自分の族の区画に `@import url("project://database/Assets/Scripts/Client.Ui/<…>.uss");` を 1 行足す〔F1〕。ctor で `StyleSheet` を受けて `styleSheets.Add` しない。束ね漏れと余りは `UiThemeImportBundleTest`〔F1〕が落とす
3. 規則はルートのクラスで始める（`.item-slot …`）。クラスは BEM（ルートは部品名のケバブ、子は `__`、状態は `--`）。ルートのクラス名は全 USS で一意
4. 他の部品のクラスを書かない。画面が部品の見た目を変えてよいのは、つまみ（Web の局所上書き変数と同じ名前のカスタムプロパティ）を画面のルートで上書きするときだけ。例 `.hotbar-hud { --slot-size: var(--hotbar-slot-size); }`
5. 状態はクラス `--<状態>` にし、付け外しは部品の Set メソッドの `EnableInClassList` だけが行う。`:hover`・`:active`・`:focus` は使ってよい。構造擬似クラス（`:first-child` 等）と z-index は使わず、C# でクラスを付ける・層で決める
6. 先頭に出所を日英 1 行ずつ書く: `/* web: W/shared/ui/SlotFrame/style.module.css（components.md §8.6） */`
7. CSS を写すとき（rem・calc・color-mix・data 属性・`::before`・grid・コンテナクエリ・影・グラデ・マスク・文字の縁・半透明）は `references/css-to-uss.md` を開く。USS に無い表現を自分で工夫する前に前例を見る

## 6. トークン

| ファイル（`S/Client.Ui/Foundation/Theme/`） | 出所 | 編集 |
|---|---|---|
| `Tokens.uss`・`SlotTokens.uss`・`SkitTokens.uss` | `W/app/tokens.css`・`slotTokens.css`・`skitTokens.css` | 生成物。手で直さず、リポジトリ直下で `node tools/uitk/convert-web-tokens.mjs`。名前の一致は `WebTokenParityTest` |
| `MantineTokens.uss`〔F1〕 | Mantine の暗色テーマの `--mantine-*` | 生成物。手で直さない |
| `DerivedTokens.uss`〔F1〕 | Web の `color-mix`・`rgb(from …)`・半透明・直書きの色 | 手で書く。各行に `/* web: 元の式 決め方 */` |
| `Theme.uss` | Mantine の既定コントロール | 既定コントロールを `--mantine-*` で塗る〔F1〕 |
| `Typography.uss`（クラスの拡充は〔C2-c〕）・`Layout.uss`〔C2-c〕 | Mantine の Text/Title・Stack/Group | カタログの「文字・装飾族」 |

- 部品・画面の USS の色は `var(--…)` だけ。`#hex`・`rgb()` は `UiStyleConventionTest` が落とす。新しい色が要るときは `DerivedTokens.uss`〔F1〕に元の式と決め方を付けて足す
- 寸法は Web がトークンで書く値はトークン、Web が直書きする値は同じ直書きで写す
- 半透明は Web の α をそのまま使わない（UITK は Linear 色空間で混ぜるので薄く出る）。決め方は `references/css-to-uss.md` の「半透明」
- 文字の大きさは Web と同じ px にする。13px 下限は外す（設計書 [A-39]。`TokenFloorAndTranslucencyTest` の下限は F1 が外す〔F1〕。F1 の取り込み前は下限テストが残る）
- フォントは `Typography.uss` が当てる SDF だけを使う。合成の太字（`-unity-font-style: bold`）は使わない

## 7. 層と族

- 層は `UiLayer`（`S/Client.Ui/Foundation/Layers/UiLayer.cs`）の固定順の兄弟コンテナ。順の正本は `W/app/tokens.css` の `--z-*`。層を足すのは integrator で、enum と `UiLayerOrderTest` を同時に直す。画面・部品のレーンは足さない
- 載せ方は `root.GetLayer(UiLayer.Screen, UiFamily.Stage)`〔F1〕。stage 族（1280×720 の舞台で一様に拡縮。パネル・グリッド）か viewport 族（実画面の辺に付く。常時 HUD）かは Web の所属（`ref-webui-design` §1.5）と同じにする
- 層コンテナ・ルート・幕・描くだけの子は `PickingMode.Ignore`。忘れるとワールドのクリックとパネル外の右短押しが効かなくなる
- 全体スイッチ〔W0-switch〕（`Client.Input.UiSystem` の Web／GameUi。Editor 限定・既定 Web の 1 つで、起動で 1 回決まる）を Presenter・View・部品が読まない。UITK のときだけ composition root（`S/Client.Starter/Registration/MainGameUiRegistration.cs`）が Presenter を登録する。入力の裁き（`UiPointerHitTest`・`UiInputExclusivity`）・DSL のロケータ・アンカーの解決はスイッチの系の値だけを読み、Web と UITK の値を論理和にしない

## 8. 画面の型

| ファイル | 持つもの |
|---|---|
| `S/Client.Ui.State/<機能>/<機能>ViewModel.cs` | 不変 sealed。判別 union は抽象基底＋sealed 派生 |
| `S/Client.Ui.State/<機能>/<機能>Model.cs` | `IReadOnlyReactiveProperty<<機能>ViewModel> Current`（源を購読して作る） |
| `S/Client.Ui.State/<機能>/I<機能>Commands.cs`・`<機能>Commands.cs` | 型付きコマンド。`UiCommandResult` を返し、拒否は理由 enum とログ |
| `S/Client.Ui/Screens/<機能>/<機能>ScreenPresenter.cs`（HUD は `<名>HudPresenter`） | 起動時に 1 回マウント・ルータで表示を切替・表示中だけ Model を購読。判断を持たない |
| `S/Client.Ui/Screens/<機能>/<機能>View.cs`（HUD は `<名>HudView`） | 部品を組む。`Render(viewModel)` で値を受け、操作は Commands を直接呼ぶ。結果は `UiCommandResultToaster.Report` |
| `S/Client.Ui/Screens/<機能>/<機能>.uss` | 配置とつまみの上書きだけ |

- 前例は `S/Client.Ui/Screens/PauseMenu/`（`PauseMenuScreenPresenter`・`PauseMenuView`）。今の PauseMenu は `Screens/BugReport`・`Screens/Settings` を別ディレクトリから参照している（Web では pauseMenu 配下）。写すのは View→コマンドの責務配置だけで、別 Screens の参照は写さない
- View は C# で組む。UXML と UI Builder は使わない
- 出し分けは `UiRoutingState.Current.Screen` と `UiScreenTraitsTable` だけで決める
- 毎フレームの値（採掘の進捗など）は Presenter が源の変化通知を購読して View へ流し、フレーム末への間引きはしない（設計書 A-57）。View は `Update()` を持たない
- USS の `overflow: hidden` で子を切る容器には `TutorialAnchor.MarkClipContainer` を付ける。付けないと、その中のアンカーを指すチュートリアル枠が容器の外へはみ出して描かれる（祖先クリップの判定は印のある容器しか見ない。設計書 A-65）
- 面（パネル・紙）を張る部品を新しく作るとき、また画面が背景色を持つ枠を手で組むとき（例: ポーズメニューの `.pause-menu__panel`）は `UiSurface.Mark` を付ける。付けないと、その面の上のアンカーを指すチュートリアル枠のグローが世界の上の色（明るい地用）で描かれる（設計書 A-136）

## 9. 動き

- 入退場とホバーは USS の `transition`。追加した直後のクラスの付け外しは補間されないので `GeometryChangedEvent` を契機にする。`height: auto` は補間できないので px に固定してから動かす
- ループ・キーフレーム・smooth scroll・慣性は `UiMotionPlayer`（`S/Client.Ui/Foundation/Motion/`）の Step で書く。足りない Step は使う段のレーンが足す
- 尺と曲線はトークンに置き、既存の値を参照する（通知の生存 7000ms・入場 160ms・退場 200ms・脈動 1200ms）
- テストのために尺を 0 にする抜け道を作らない。テストは時計（`IUiMotionClock`）を進める

## 10. 入力

- フォーカスを持つのは入力欄（`GameTextField`）だけ。Space・Enter でボタンが押されず、Tab でフォーカスが動かないことを L1 で固定する
- ESC は UIState が持つ。入力欄での ESC はフォーカスを外すだけにし、値を戻す既定の処理を止める
- 文字入力中の移動の停止と IME のモードは `UiInputExclusivity`・`ImeCompositionModeApplier` の 1 か所で決める。画面で `imeCompositionMode` を触らない
- 画面のキー処理（ESC・←→・Enter）は Presenter か UIState に集める
- 入力の規則の根拠は `origin/master:docs/uitk-migration/plan.md` の §3.8

## 11. 比べ方と例外の記帳

- 部品は部品ギャラリー〔F1〕で全状態を並べて撮り、画面は PlayMode・全体スイッチ〔W0-switch〕UITK（今は所有表で UITK 所有にした画面）で 720p と 2560×1080 を撮る。Web の参照と状態ごとに並べて PR の成果物の載せ先に載せ、差を作業報告に表で書く。手順・置き場・合否の目安は `references/compare-procedure.md`
- `uloop screenshot` は `--capture-mode window` で撮る。`rendering` には UITK が写らない
- 例外にしてよい条件は `references/compare-procedure.md` §6 だけが決める（ここに書き写さない）
- 例外は台帳 `docs/uitk/TODO.md` の「## 見た目の例外」〔F1〕に `LX-n` で書き、その USS・C# の行に `/* look-exception: LX-n */` を付ける

## 12. やらないこと

- UXML・UI Builder にしか無い状態。インラインの style は計算で決まる位置・寸法だけ
- 部品・画面の USS での色の直書き・構造擬似クラス・z-index・合成の太字・ビットマップの参照・UI 装飾の画像化
- 他の部品の中身（子のクラス・状態クラス・`style`）を外から塗る
- `Func<>`・`partial`・デフォルト引数・`Action`/`event`/`Button.clicked` で外へ出すイベント
- Components から Client.Game・`MasterHolder`・機能別の State を参照する
- `feature/uitk` で `moorestech_web/` を書き換える（Web の変更は master の取り込みでだけ入る）
- 記帳なしに Web と違う見た目にする

## 13. 検証

| 層 | 何を | 土台 | 置き場 | 最小 |
|---|---|---|---|---|
| L0 | 純ロジック・ViewModel の導出・コマンドの前提 | EditMode NUnit | `S/Client.Tests/Ui/State/<機能>/` | 写す Web の vitest の全件。元のテスト名を XML コメントに書く |
| L1 部品 | 状態クラス・つまみの効き（`resolvedStyle`）・イベント・`PickingMode`・`focusable` | `UiTestHost`（本番のテーマ・1280×720） | `S/Client.Tests/Ui/Components/<族>/` | 状態ごと 1・イベントごと 1・つまみ上書き 1 |
| L1 画面 | View＋Model＋Commands の結線 | `ScreenTestHost`・`FakeModel<T>`・`CommandRecorder<T>` | `S/Client.Tests/Ui/Screens/<機能>/` | Web のコンポーネントテストと e2e のうちブラウザ固有でない全件 |
| L2 | 規約 | `UiStyleConventionTest` ほか | `S/Client.Tests/Ui/Conventions/` | 規約を足したら違反の文字列のケースを 1 つ |
| L3 | 実ゲームの結線 | EditModeInPlayingTest | `S/Client.Tests/EditModeInPlayingTest/Ui/` | 段ごとに 1 本（スイッチ UITK で開く・閉じる） |
| L4 | 録画のプレイテスト | `unity-playmode-recorded-playtest` | `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/` | `origin/master:docs/uitk-migration/plan.md` §4.4 のラダー |

- 回し方: `uloop compile --project-path ./moorestech_client` → L0〜L2 は `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\."`。L3 は別に `--filter-value "^Client\.Tests\.EditModeInPlayingTest\.Ui\."` で回す（PlayMode 遷移でドメインリロードが入る）
- 写したテストと Web の元テストの対応は `docs/uitk/disposition.md` に書く
