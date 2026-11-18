---
name: ref-uitk-design
description: moorestech のゲーム内 UI を UI Toolkit（USS・tss・Client.Ui）で作る規則。層・部品カタログ・USS とトークン・見た目のホワイトリスト・画面の型・比べ方・例外の記帳を持つ。Use When — Client.Ui・Client.Ui.State・USS・tss のコードを読む・書く・レビューする時、UITK の部品・画面・HUD を足す時、色・寸法・配置・装飾・動き・入力を変える時、見た目の差を直す・記帳する時。
---

# moorestech UITK の作り方と見た目

対象は `moorestech_client/Assets/Scripts/Client.Ui`（描画）と `Client.Ui.State`（画面状態）。略記: `S/` = `moorestech_client/Assets/Scripts/`。

規則は逐語で守る。表に無い表現の描き方を決めたら `references/css-to-uss.md` に 1 行足す。

## 1. 見た目の正本

- 今の USS・部品・トークンが見た目の正本。見た目を変えるのは不具合の修正か、ユーザーの裁定があるときだけ（並べ比べの基準を黙って動かさないため）
- 迷ったら ① 部品カタログ（`references/component-catalog.md`）② 見た目のホワイトリスト（`references/visual-vocabulary.md`）③ 最後の参照キャプチャ `moorestech_logs/uitk/reference-captures/web-<sha>/`（撮影元は台帳 `docs/uitk/TODO.md` の「## 参照キャプチャ」）を見る
- 新しい面・部品は `references/visual-vocabulary.md` の約束の中で作る。無い表現が要るなら先にそこへ書いて裁定を取る
- 切替 PR のマージまで（master に Web 版がある間）に、master に入った Web 版の変更を写すときは、その画面の Web の CSS から数値を逐語で写す（rem は ×16 の px、em はその要素の文字サイズ（px）× 値。目で合わせた値を足さない）。参照は master の worktree で撮り直す（`references/compare-procedure.md` §2）。切替 PR のマージの後はこの行を消す

## 2. 層と依存の向き

| 層 | 置き場 | 参照してよい | 参照しない |
|---|---|---|---|
| Foundation | `S/Client.Ui/Foundation/<領域>/` | Client.Input・Client.Localization・UniRx・UniTask・Client.Game の入力・UIState・アンカー ID | Components・Content・Screens・Client.Ui.State の機能別の型 |
| Components | `S/Client.Ui/Components/<族>/<部品>/` | Foundation・`Client.Ui.State` の `Text`（`UiText`）・`LocalizationKey` | Content・Screens・Client.Game・`MasterHolder`・Client.Ui.State の機能別の型 |
| Content | `S/Client.Ui/Content/` | Foundation・Components の値型・Client.Game（`ClientContext`）・Core.Master | Screens |
| Screens | `S/Client.Ui/Screens/<機能>/` | 上のすべて・Client.Ui.State | 他の `Screens/<機能>` |
| State | `S/Client.Ui.State/<機能>/`（別 asmdef） | Client.Game・Client.Network・Core.Master | Client.Ui・UnityEngine.UIElements |

- 部品はドメインを知らない。ItemId・BlockId・Model を受けず、`UiIcon`・`UiText`・数・値型を受ける。ID から表示素材への解決は Content の `GameContentPresentation`の 1 か所に置く
- asmdef は `Client.Ui`・`Client.Ui.State` の 2 つ。層の向きは L2 の `UiLayerDependencyDirectionTest`が using で止める。破れたら asmdef の分割を裁定に上げる

## 3. 部品カタログの引き方

- 部品を作る・使う前に `references/component-catalog.md` で名前・置き場・公開 API を確かめる。カタログに有る部品を画面の中に作り直さない
- 名前は役割の名前にし、技術名を入れない（例 `FormButton`・`DialogFrame`）
- 2 画面以上が使う部品は Components に置く。1 画面目だけが使う新しい共有は `Screens/<機能>/` に置き、2 画面目を作るときに Components へ上げる（設計書 §5.1 の昇格規則）。1 画面の中の複数ファイルで使うだけの部品は `Screens/<機能>/Parts/`
- 部品の公開 API を変えたら、カタログの行を同じコミットで直す

## 4. 部品の書き方

```csharp
// 押せる図形のボタン（components の IconButton の簡略形）
// A pressable glyph button (a simplified IconButton)
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

- ctor は `(string testid, …)` で `name = testid`（DSL のシナリオが `name` で探す）。装飾だけの部品と、testid を持たない中身の部品（`SlotContent`・`KeyCap` 等。ctor はカタログどおり）は testid を取らない。デフォルト引数を使わない
- 表示する値は `SetXxx(値)` で受ける。値を返す素通しのプロパティを作らない（`{ get; private set; }` は可）
- 操作は `IObservable<T> On<変化対象><過去分詞>`（中は UniRx の `Subject`）で出す。`Button.clicked`・`Action`・`event`・コールバック引数で外へ出さない。素の `Button` を中で使うなら `clicked` は部品の中で `Subject` へ流すだけにする
- 部品は `VisualElement` 派生にする。例外は `OutlinedCountLabel`（置く側の要素へ Label 2 枚を兄弟で足す。箱に入れると幅の丸めが変わり字の画素がずれる。設計書 [A-110]）
- `focusable = false`。フォーカスを持つのは入力欄だけ。例外: キーで操作するボタン（クラフトの Enter 長押し）は置く側がインスタンスに `focusable = true` と `UiKeyFocusKeepMark`を付ける。クリックで乗り Tab では乗らない（設計書 [A-166]）
- 描くだけの子（Painter2D・頂点カラー・装飾）と文字だけの子は `pickingMode = PickingMode.Ignore`。押せる要素だけが Pick を受ける
- 子を受ける部品（枠・スクロール）は `contentContainer` を返し、外は `Add` で入れる
- 色・寸法を C# に書かない。描画で値が要るときは部品の USS で `--<部品>-<名>: var(--トークン);` と受け直し、`CustomStyleResolvedEvent` の中で `UssCustomStyleReader.TryReadColor`・`TryReadPixels` で読む（前例 `S/Client.Ui/Components/Panels/GamePanel/GamePanelFace.cs`）
- `Update()` を使わない。変化は Set の呼び出しか Model の購読で受ける
- ctor で要素へ登録する UI のイベントのハンドラは、同クラスの private メソッドを method group で渡してよい（`RegisterCallback<TEvent>(OnX)`・`generateVisualContent += DrawX`。自分への登録も、`UiPressGesture` のように他の要素への登録も同じ）。ラムダで書いてもよい
- AGENTS.md のとおり 1 ファイル 200 行・1 ディレクトリ 10 ファイル・`partial` と `Func<>` の禁止・日英 2 行のコメント

## 5. USS の書き方

1. 1 部品 1 USS を `.cs` の横に置く（`Components/<族>/<部品>/<部品>.uss`）。画面の USS（`Screens/<機能>/<機能>.uss`）は配置とつまみの上書きだけを持つ
2. 新しい USS は `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` の自分の族の区画に `@import url("project://database/Assets/Scripts/Client.Ui/<…>.uss");` を 1 行足す。ctor で `StyleSheet` を受けて `styleSheets.Add` しない。束ね漏れと余りは `UiThemeImportBundleTest`が落とす
3. 規則はルートのクラスで始める（`.item-slot …`）。クラスは BEM（ルートは部品名のケバブ、子は `__`、状態は `--`）。ルートのクラス名は全 USS で一意
4. 他の部品のクラスを書かない。画面が部品の見た目を変えてよいのは、つまみ（部品の USS が受け直すカスタムプロパティ）を画面のルートで上書きするときだけ。例 `.hotbar-hud { --slot-size: var(--hotbar-slot-size); }`
5. 状態はクラス `--<状態>` にし、付け外しは部品の Set メソッドの `EnableInClassList` だけが行う。`:hover`・`:active`・`:focus` は使ってよい。構造擬似クラス（`:first-child` 等）と z-index は使わず、C# でクラスを付ける・層で決める
6. 新しい USS に出所のコメントは書かない。既存の `/* web: web@<sha10>/src/… */` は値の式の出所で、`git show <sha10>:moorestech_web/webui/src/…` で読める
7. USS に無い表現（影・グラデ・マスク・grid・文字の縁・半透明）は `references/css-to-uss.md` の前例を先に見る

## 6. トークン

| ファイル（`S/Client.Ui/Foundation/Theme/`） | 編集 |
|---|---|
| `Tokens.uss`・`SlotTokens.uss`・`SkitTokens.uss` | 手で書く正本。名前を変えたら使い手を全部直す |
| `MantineTokens.uss` | 手で書く正本 |
| `DerivedTokens/*.uss` | 手で書く。各行の `/* web: … */` は値の式の出所（§5 の 6 の読み方） |
| `Theme.uss` | 既定コントロールを `--mantine-*` で塗る |
| `Typography.uss`・`Layout.uss` | カタログの「文字・装飾族」 |

- 部品・画面の USS の色は `var(--…)` だけ。`#hex`・`rgb()` は `UiStyleConventionTest` が落とす。新しい色が要るときは `Foundation/Theme/DerivedTokens/` の族のファイル（`<族>DerivedTokens.uss`。Panels・Controls・Text・Slots・Gauges・Recipe・Screens・Skit）に元の式と決め方を付けて足す。ファイルを足すときは tss の Foundation/Theme 区画と `UiStyleConventionRules.ColorLiteralExemptUssFileNames` にも足す（設計書 A-228）
- 寸法はトークンがあるものはトークン、無いものは部品の USS に直書きする
- 半透明は α・opacity をそのまま使う（GameUi は sRGB 空間で合成する。設計書 A-180〜A-184）。決め方は `references/css-to-uss.md` の「半透明」
- 文字の大きさは既存の画面と同じ px に揃える（設計書 [A-39]）
- フォントは `Typography.uss` が当てる SDF だけを使う。合成の太字（`-unity-font-style: bold`）は使わない

## 7. 層と族

- 層は `UiLayer`（`S/Client.Ui/Foundation/Layers/UiLayer.cs`）の固定順の兄弟コンテナが正本。層を足すときは enum と、層の上下の組を確かめる `UiLayerStackTest` を同時に直す
- 載せ方は `root.GetLayer(UiLayer.Screen, UiFamily.Stage)`。stage 族か viewport 族かは `references/visual-vocabulary.md` §2
- 層コンテナ・ルート・幕・描くだけの子は `PickingMode.Ignore`。忘れるとワールドのクリックとパネル外の右短押しが効かなくなる
- Presenter の登録は composition root（`S/Client.Starter/Registration/MainGameUiRegistration.cs`）の 1 か所。Presenter・View・部品は登録の都合を読まない

## 8. 画面の型

| ファイル | 持つもの |
|---|---|
| `S/Client.Ui.State/<機能>/<機能>ViewModel.cs` | 不変 sealed。判別 union は抽象基底＋sealed 派生 |
| `S/Client.Ui.State/<機能>/<機能>Model.cs` | `IReadOnlyReactiveProperty<<機能>ViewModel> Current`（源を購読して作る） |
| `S/Client.Ui.State/<機能>/I<機能>Commands.cs`・`<機能>Commands.cs` | 型付きコマンド。ctor で `IUiCommandResultToaster` を受け、結果（`UiCommandResult`。拒否は理由 enum とログ）を自分で `Report` する。操作の戻り値は `void` |
| `S/Client.Ui/Screens/<機能>/<機能>ScreenPresenter.cs`（HUD は `<名>HudPresenter`） | 起動時に 1 回マウント・ルータで表示を切替・表示中だけ Model を購読。判断を持たない |
| `S/Client.Ui/Screens/<機能>/<機能>View.cs`（HUD は `<名>HudView`） | 部品を組む。`Render(viewModel)` で値を受け、操作は Commands を直接呼ぶ。Toaster を持たない |
| `S/Client.Ui/Screens/<機能>/<機能>.uss` | 配置とつまみの上書きだけ |

- トーストは結果を作る Commands が `IUiCommandResultToaster.Report` で出し、View は Report しない（入口を足しても Report を書き忘れないため）。テストは偽物 `RecordingUiCommandResultToaster` に届いた結果を見る
- 前例は `S/Client.Ui/Screens/PauseMenu/`（`PauseMenuScreenPresenter`・`PauseMenuView`）。今の PauseMenu は `Screens/BugReport`・`Screens/Settings` を別ディレクトリから参照している。写すのは View→コマンドの責務配置だけで、別 Screens の参照は写さない
- View は C# で組む。UXML と UI Builder は使わない
- 出し分けは `UiRoutingState` と `UiScreenTraitsTable` で決め、画面の開閉を読む State は `<機能>ScreenVisibility.IsShown` を通す。`UiScreen` に無い状態だけ `UIStateEnum` を読み、その行に理由を 1 行書く（設計書 A-248）
- 毎フレームの値（採掘の進捗など）は Presenter が源の変化通知を購読して View へ流し、フレーム末への間引きはしない（設計書 A-57）。例外: 源の変化 1 件ごとに全エントリの VM を組み直す Model（`BuildMenuModel`）は、組み直しをフレーム末の 1 回に畳む（A-57）。View は `Update()` を持たない
- USS の `overflow: hidden` で子を切る容器には `TutorialAnchor.MarkClipContainer` を付ける。付けないと、その中のアンカーを指すチュートリアル枠が容器の外へはみ出して描かれる（祖先クリップの判定は印のある容器しか見ない。設計書 A-65）

## 9. 動き

- 入退場とホバーは USS の `transition`。追加した直後のクラスの付け外しは補間されないので `GeometryChangedEvent` を契機にする。`height: auto` は補間できないので px に固定してから動かす
- ループ・キーフレーム・smooth scroll・慣性は `UiMotionPlayer`（`S/Client.Ui/Foundation/Motion/`）の Step で書く。足りない Step は使う側が足す
- 出入りと動きの尺・曲線はトークンに置き、既存の値を参照する（`--notification-enter-duration` 160ms・`--notification-exit-duration` 200ms・`--tutorial-pulse-duration` 1200ms）。表示の生存のようなロジックの尺はトークンに置かず、Model の定数を正にする（通知は `NotificationModel.DisplayMilliseconds`。View はそこから動きの長さを決める。設計書 A-67）
- CSS の `ease-out`・`ease-in` は `UiEasing.CssEaseOut`・`CssEaseIn`、横の移動は `TranslateXTweenStep`（`EaseOutCubic` で代用しない。設計書 A-69）
- テストのために尺を 0 にする抜け道を作らない。テストは時計（`IUiMotionClock`）を進める

## 10. 入力

- フォーカスを持つのは入力欄（`GameTextField`）だけ。Space・Enter でボタンが押されず、Tab でフォーカスが動かないことを L1 で固定する
- ESC は UIState が持つ。入力欄での ESC はフォーカスを外すだけにし、値を戻す既定の処理を止める
- 文字入力中の移動の停止と IME のモードは `UiInputExclusivity`・`ImeCompositionModeApplier` の 1 か所で決める。画面で `imeCompositionMode` を触らない
- 画面のキー処理（ESC・←→・Enter）は Presenter か UIState に集める
- 入力の規則の根拠は `origin/master:docs/uitk-migration/plan.md` の §3.8

## 11. 比べ方と例外の記帳

- 部品は部品ギャラリー（`Client.Ui.Editor`）で全状態を並べて撮り、画面は PlayMode で 720p と 2560×1080 を撮る。変更の前後と、最後の参照キャプチャと並べて PR の成果物の載せ先に載せ、差を作業報告に表で書く。手順・置き場・合否の目安は `references/compare-procedure.md`
- `uloop screenshot` は `--capture-mode window` で撮る。`rendering` には UITK が写らない
- 例外にしてよい条件は `references/compare-procedure.md` §6 だけが決める（ここに書き写さない）
- 例外は台帳 `docs/uitk/TODO.md` の「## 見た目の例外」に `LX-n` で書き、その USS・C# の行に `/* look-exception: LX-n */` を付ける（照合は `LookExceptionLedgerTest`）

## 12. やらないこと

- UXML・UI Builder にしか無い状態。インラインの style は計算で決まる位置・寸法だけ
- 部品・画面の USS での色の直書き・構造擬似クラス・z-index・合成の太字・ビットマップの参照・UI 装飾の画像化
- 他の部品の中身（子のクラス・状態クラス・`style`）を外から塗る
- `Func<>`・`partial`・デフォルト引数・`Action`/`event`/`Button.clicked` で外へ出すイベント
- Components から Client.Game・`MasterHolder`・機能別の State を参照する
- 見た目のホワイトリスト（`references/visual-vocabulary.md`）に無い表現を断りなく足す

## 13. 検証

| 層 | 何を | 土台 | 置き場 | 最小 |
|---|---|---|---|---|
| L0 | 純ロジック・ViewModel の導出・コマンドの前提 | EditMode NUnit | `S/Client.Tests/Ui/State/<機能>/` | 純ロジックの分岐ごとに 1 件 |
| L1 部品 | 状態クラス・つまみの効き（`resolvedStyle`）・イベント・`PickingMode`・`focusable` | `UiTestHost`（本番のテーマ・1280×720） | `S/Client.Tests/Ui/Components/<族>/` | 状態ごと 1・イベントごと 1・つまみ上書き 1 |
| L1 画面 | View＋Model＋Commands の結線 | `ScreenTestHost`・`FakeModel<T>`・`CommandRecorder<T>` | `S/Client.Tests/Ui/Screens/<機能>/` | 操作ごと・表示の分岐ごとに 1 件 |
| L2 | 規約 | `UiStyleConventionTest` ほか | `S/Client.Tests/Ui/Conventions/` | 規約を足したら違反の文字列のケースを 1 つ |
| L3 | 実ゲームの結線 | EditModeInPlayingTest | `S/Client.Tests/EditModeInPlayingTest/Ui/` | 画面を足したら 1 本（開く・閉じる） |
| L4 | 録画のプレイテスト | `unity-playmode-recorded-playtest` | `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/<段>/`（1 ディレクトリ 10 本まで） | `origin/master:docs/uitk-migration/plan.md` §4.4 のラダー |

- 回し方: `uloop compile --project-path ./moorestech_client` → L0〜L2 は `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\."`。L3 は別に `--filter-value "^Client\.Tests\.EditModeInPlayingTest\.Ui\."` で回す（PlayMode 遷移でドメインリロードが入る）
- `/// <web-test>` の行は消さない・書き換えない（`tools/uitk/test-name-parity.mjs` が突き合わせる）
