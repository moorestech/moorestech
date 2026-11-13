# UITK S2-notification 通知 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**L0 タスク（Task 1〜4）は Editor 無しで先行可（Task 5 で Editor 枠を 1 回だけ借りて compile と L0 テストを回す＝L0 の末）、Task 6 以降は Editor 要（C3-a と C3-b の `feature/uitk` への取り込み後。どちらも実行計画のレーン行の依存）。**

**Goal:** Web 版の通知（`W/features/notification/`）を UITK へ写す。ストアの規則（獲得の合流・生存尺 `NOTIFICATION_DISPLAY_MS`・除去告知の 1 回出し）と文言の解決（messageId→キー・Guid の内容名・獲得のアイテム名と数）を `Client.Ui.State/Notification/` の C# モデルへ、行の見た目と出入り（入場 160ms・退場 200ms・12px のずれ）を `Client.Ui/Screens/Notification/` の View へ置き、背面の層 `UiLayer.BehindStage` に常駐させる。出入りの曲線（CSS の ease-in・ease-out）と横移動の Step は実行計画 E-19 の割り当てで `Foundation/Motion` に足す。Web のテスト 47 件（台帳の数え方。`it` の行で 48、`it.each` を展開して 50。うち 1 件は保険タイマーの廃止で `捨てる`。件数の正本は「## 写すテストの対応と件数」）を NUnit へ写し、録画 `server-notification-smoke`（bd moorestech-wwxy）を現行マスタで動く形へ直して UITK と Web の両方で通す。

**Architecture:** Model（`NotificationModel`）は読み口 `INotificationListSource`（`IReadOnlyReactiveProperty<NotificationListViewModel>`）と書き口 `INotificationListWriter`（`AddNotification`・`RemoveNotification`）の 2 つの interface を実装し、時間を持たない（保険タイマーも無い。D-2）。行の生存（入場→保持→退場）は View が `UiTweenAnimation` 2 本（入場・生存尺の終端から逆算した遅延つきの退場）をパネルの時計で回し、退場が終わったら書き口の `RemoveNotification(id)` を呼ぶ。出入りの尺とずれはトークンを画面 USS が受け直して C# が読み、生存尺は Model の定数 `DisplayMilliseconds` だけが持つ。中継（`NotificationFeedRelay`）が既存の `NotificationFeed` を購読し、通知パケットを種別ごとの中身の型（獲得・実績・拒否・除去告知）と型付きの引数（文字の引数・内容名の引数）へ変換して書き口へ積む。アイコンは中身の型 `NotificationIcon`（アイテム／無し）で表し、C3-a の Content 層 `GameContentPresentation.Item(ItemId)` を Screens の薄い口（`INotificationRowIcons`）越しに引く。文言は `UiText`（辞書キー＋解決済みの引数）で `LocalizedLabel` に渡す。

**Tech Stack:** C# 9（Unity 6000.3.8f1）、UI Toolkit（ランタイムパネル・USS・`CustomStyleResolvedEvent`）、UniRx（`ReactiveProperty`）、UiMotion（`UiTweenAnimation`・`UiMotionPlayer`）、VContainer、NUnit＋`com.unity.ui.test-framework`（`UITestFixture`・`simulate.FrameUpdateMs`）、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、Node（`tools/uitk/test-name-parity.mjs`・Web の参照撮影 `e2e/uitk-reference/capture.ts`）、gh CLI、bd。

**パスの略記:** `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/src/`、`SC/` = `.agents/skills/unity-playmode-recorded-playtest/scenarios/`。Files 節と git コマンドは repo 相対の完全パスで書く。

## Requirements

- R1. Web の `notificationStore.ts` の規則を `S/Client.Ui.State/Notification/NotificationModel.cs` へ写す: 獲得（`EarnedNotificationContent`）だけが「同じアイテム・同じ messageId の表示中の行」へ合流し、id を据え置いて数を足し `LifetimeEpoch` を 1 進める。合流しても並びは変わらない。ほかの種別は同じ内容でも別行。生存尺 `DisplayMilliseconds = 7000`（Web `NOTIFICATION_DISPLAY_MS`）。保険タイマーは持たない（D-2）。同時表示数の上限を持たない。Model は読み口と書き口の 2 つの interface を実装し、View には書き口だけを渡す。受け入れ: `NotificationModelTest` 10 件が緑
- R2. 既存の `NotificationFeed`（`SaveMigrationNotice` の初期データ＋`OnNotified`）を購読して Model へ積む中継 `NotificationFeedRelay` を作る。除去告知は購読前に来ていれば初期データから 1 回だけ、購読後の新しい告知は再び出る。パケットは種別ごとの中身の型と型付きの引数へ変換する（獲得以外の型に獲得を渡して例外になる口を作らない）。受け入れ: `NotificationFeedRelayTest` 4 件が緑
- R3. Web の `notificationMessages.ts` を `S/Client.Ui.State/Notification/Text/` と `Params/` へ写す: messageId→キーの表 70 件（「## 写すテストの対応と件数」）（未知は `ui.notification.unknownMessage`）、Guid を運ぶ 2 種の引数を内容名の引数の型へ変換（欠けたら・読めなければ警告して文字の引数のまま）、補間値（params→messageId、獲得は itemName と count を足す）。文言は `UiText`（辞書キー＋解決済みの引数）で表す（D-4）。内容名・アイテム名が辞書やマスタに無いときは理由つきの LogError を出し、Web と同じ見え方（アイテムは `ui.common.itemFallback`「Item {itemId}」、内容名は辞書の欠けの印）で出す。受け入れ: `NotificationMessageKeysTest` 5 件・`NotificationTextTest` 11 件（UITK だけ 1）・`NotificationTextLocalizeTest` 4 件（TestCase 3 行＋獲得の引数の重複の UITK だけ 1）・`LocalizeNotificationTextLookupTest` 2 件（UITK だけ）が緑
- R4. Web の S 種別 `notificationServerIdCoverage.test.ts` を C# へ写す: サーバーとクライアントの送信元 `.cs` を走査して通知 id を集め、C# の表 `NotificationMessageKeys` が全部を未知以外へ解決することを確かめる。受け入れ: `NotificationServerIdCoverageTest` 5 件が緑
- R5. 行の見た目を Web と同じにする: 幅 `--notification-width`（256px）・padding 5px 12px・1px 枠（`--notification-border`）・面 `--notification-face`・文字 15px／Medium／行送り 18px／字間 0.055em／`--text-high-contrast`（拒否は `--text-insufficient`）・文字影 0.35px・アイコン 22px と 8px の間・行間 10px・新着が最上段。ホストは実画面の左 16px（実画素）・縦の中央線から下へ積み、舞台と同率で拡縮する（`UiFamily.Viewport`。Web の `scale(var(--ui-scale))`）。受け入れ: L1 の `NotificationHostViewTest`・`NotificationHostViewLayoutTest`（倍率を変えても左端が実画面の 16px）・`NotificationRowViewTest`・`NotificationViewportScalingTest`（1280×720 と論理 1706.7×720）が緑、Task 10 の並べ比べで 720p の差が 2px 以内か `LX-39`〜`LX-41` に記帳
- R6. 行の出入り: 入場は `--notification-enter-duration`（160ms）の CSS ease-out で透明・左 12px から、退場は生存尺の終端に `--notification-exit-duration`（200ms）の CSS ease-in で戻り、終わった時点で行を除く。合流で生存尺が回り直したら動きを頭から再生する。曲線は `UiEasing.CssEaseOut`・`CssEaseIn`、横移動は `TranslateXTweenStep`（Foundation/Motion。E-19）。尺とずれはトークン、生存尺は Model の定数だけ（USS に 7000ms を書かない）。受け入れ: `UiEasingCssCurveTest`・`UiTweenStepTest` の TranslateX・`NotificationRowLifetimeTest`・`NotificationHostViewTest` の該当が緑
- R7. ホストを背面の層 `UiLayer.BehindStage` の `UiFamily.Viewport` に常駐させ（`NotificationLayerPresenter`）、入力を奪わない（ホストも行も `PickingMode.Ignore`）。`MainGameUiRegistration` の画面登録区画に `NotificationGameUiRegistration.Register(builder);` の 1 行、`GameUiTheme.tss` の Screens 区画に `@import` 1 行。受け入れ: `NotificationLayeringTest` 5 件（全部隠しが背面の層を隠す実物なら 6 件。Task 6 Step 2）が緑、GameUi で起動すると通知が背面に出る（Task 11 の録画）
- R8. 言語を切り替えたら表示中の行の文言を解き直す（内容名・アイテム名も新しい言語で）。受け入れ: `NotificationHostViewTest` の言語の 1 件が緑
- R9. 写した NUnit に `/// <web-test file="…">…</web-test>` を付け、写さない 1 件（`notificationStore.test.ts` の「退場アニメが発火しなくても保険タイマーで消える」＝`捨てる`）を `docs/uitk/disposition.md` に書く。受け入れ: `node tools/uitk/test-name-parity.mjs --bundle 2`（portalLayerScaling は台帳の行が束 2 5 なので `--bundle 5` も）の出力で通知の単体 5 ファイル＋e2e 4 ファイル＋portalLayerScaling の通知の 1 件の未対応が 0（ほかのレーンの分は数えない）
- R10. 並べ比べ: Web の参照 `2-notifications`（1280×720）と、このレーンで撮る 2560×1080 の Web と、UITK の撮影（実画面の 720p と、PanelSettings を `Object.Instantiate` した複製に描いた 720p・2560×1080）を並べ、差を done.md の表に書く。本番の PanelSettings 資産は書き換えない（S1-theme の撮影の前例）。受け入れ: done.md に 2 解像度の組と差の表、`git status` に `GameUiPanelSettings.asset` の変更が無い
- R11. 録画 `server-notification-smoke` を現行マスタで動く形に直し（研究をマスタから選ぶ・⚠ 見出しを外す・UITK では背面の層に出たことと文言を確かめる）、GameUi（UITK）と Web の両方で通す。受け入れ: 2 回分の `result.json` が成功、UITK 回の録画を報告ページに載せる、台帳「## 壊れた録画の割り振り」の該当行の扱いを done.md に書く
- R12. 完了: `uloop compile` が ErrorCount 0、本計画の全テストが緑、moores-code-review を通し、`base=feature/uitk` の PR をマージコミットで取り込む
- やらないこと: トースト（`ToastHostView`。済み）。全部隠し（`UiHideAll` 相当）の仕組みそのもの（S2-skit の持ち物。このレーンは「## 死活表」の Ctrl+U・カットシーンの行だけを確かめ、隠れない実物なら S2-skit のファイルは書かずに止めて integrator へ返す）。`GameContentPresentation`・`UiIcon`・`GameIcon` の実装（C3-a）。`Tokens.uss` 等の共有トークンの追加（integrator への依頼にする）。Web 側の変更。master への PR

## Global Constraints

- 作業ブランチ: `uitk-lane/s2-notification`（`moores-wt new uitk-lane/s2-notification --from origin/feature/uitk --no-editor` で指揮が作る。Task 5 と Task 6 以降で Editor を `uloop launch` する）。Task 1〜4 は C3-a に依存しない。Task 6 以降は C3-a（`GameContentPresentation`・`UiIcon`・`GameIcon` と `Client.Ui.asmdef` の `Core.Master` 参照）と C3-b（倍率の読み口 `StageScaleSource`。設計書 [A-41]）が `origin/feature/uitk` に入ってから始める（実行計画のレーン行の依存。W2 の枠 2 で C3-b はこのレーンより先に流れる。Task 6 Step 1 で確かめ、無ければ止めて指揮へ返す）
- 正本の優先: 裁定（`.decisions/2026-11-06-UITK*`）→ 統合計画（`origin/master:docs/uitk-migration/plan.md`）→ 設計書 `docs/uitk-migration/execution/component-architecture.md`（このレーンの割り当ては §7.1 の A-67〜A-69）→ 実行計画 `docs/uitk-migration/execution/execution-plan.md`（E-19）→ この計画
- 層の向き（設計書 §1.1）: `Client.Ui.State` は `Client.Ui`・`UnityEngine.UIElements` を参照しない。Screens は State・Content・Components・Foundation を参照してよい。Components は Content（`GameContentPresentation`）と `Client.Game` を参照しない
- 所有: `S/Client.Ui/Screens/Notification/**`・`S/Client.Ui.State/Notification/**`（既存の `NotificationFeed.cs`・`SequencedNotification.cs` を含む）・`S/Client.Tests/Ui/Screens/Notification/**`・`S/Client.Tests/Ui/State/Notification/**`・`S/Client.Starter/Registration/GameUi/NotificationGameUiRegistration.cs`・`SC/server-notification-smoke.cs`・新規 `SC/uitk/stage2/uitk-notification-compare.cs`
- **E-19 による Foundation/Motion の例外**（指揮の割り当て。実行計画 E-19・設計書 A-69）: このレーンは `S/Client.Ui/Foundation/Motion/Tween/UiEasing.cs`（enum に `CssEaseOut`・`CssEaseIn` を足し `UiEasingFunctions.Evaluate` に分岐を足す）と新規 `S/Client.Ui/Foundation/Motion/Steps/TranslateXTweenStep.cs`、そのテスト（新規 `S/Client.Tests/Ui/Motion/UiEasingCssCurveTest.cs`・既存 `S/Client.Tests/Ui/Motion/UiTweenStepTest.cs` への 1 メソッド）を書いてよい。W2 で `UiEasing` を書き換えるのはこのレーンだけ。Foundation/Motion のほかのファイル（`UiTweenAnimation`・`UiMotionPlayer`・既存の Step）は変えない
- 共有ファイルに書いてよいのは: `GameUiTheme.tss` の Screens 区画の `@import` 1 行、`MainGameUiRegistration` の画面登録区画の 1 行、`Localization/localization.csv`（このレーンは足す予定なし）、`docs/uitk/TODO.md` の「## 実測の結論」「## 見た目の例外」（`LX-39`〜`LX-41`）「## 追従記録」の末尾、`docs/uitk/disposition.md` の「## 処遇」の末尾。`Tokens.uss`・`DerivedTokens.uss`・`MantineTokens.uss`・`UiLayer`・`UiScreenTraits`・`AnchorIds`・`Components/**`・`Foundation/**`は書かない。要るものは done.md の「integrator への依頼」に書いて、このレーンでは Screens の中で済ませる（設計書 §5.1-2）
- AGENTS.md: 1 ファイル 200 行未満、1 ディレクトリ 10 ファイルまで、`partial` 禁止、`Func<>` 禁止、デフォルト引数禁止、イベントは UniRx、try-catch 禁止（ファイル読みの境界も使わない。`File.Exists` で分岐）、主要処理に日本語→英語の 2 行コメント（各 1 行）、fail-closed の早期 return はログか `silent-ok: <理由> / <誰がどこで気づくか>`、単純な get/set プロパティ禁止（`{ get; }`・`{ get; private set; }` は可）、`.meta` は手で作らない、`Update()` を使わない
- テスト名は日本語の挙動文。写したテストには直前に `/// <web-test file="<moorestech_web/webui/ からの相対>"><it の第 1 引数></web-test>` を 1 行以上。Web に元が無い UITK だけのテストにはタグを付けない
- 数値の書式は `CultureInfo.InvariantCulture`
- 比較の撮影はランタイム PanelSettings（PlayMode・GameUi）で撮る。ギャラリーの撮影で色を比べない。暗い半透明面（`--surface-navy` 由来の `--notification-face`）の α の差も、ほかの差と同じく直す対象にする（I-fix1 は取り込み済み。Task 6 Step 1 で `e26010aa0c` を確かめる）
- UITK の `letter-spacing` の px は「文字の大きさ/100 × 値」として効く（台帳「## 実測の結論」）。生成トークンの字間が Web と一致するとは書かない
- 録画と L3 の前に全体スイッチが UITK（`UiSystem.GameUi`）であることを確かめる（UI のテストの TearDown が既定の Web へ戻す。`run-scenario.sh` は 1 回目に "already playing" で落ちることがあり、そのときは 1 回だけ再実行する）
- `.cs` を変えたタスクの最後に `uloop compile --project-path ./moorestech_client`（Task 1〜4 は Editor 無しなので Task 5 でまとめて回す）
- 計画・done.md・シナリオに個人の絶対パス（ホームディレクトリ）を書かない。`../moorestech_logs/...` のような repo 相対で書き、PR の報告ページの公開スクリプトは「環境ローカル規約（CLAUDE.local.md の『PR の成果物の載せ先』）が指す公開スクリプト」と書く

## 設計検査記録

- 配置検査: 実施済み / 違反4件・修正4件 / 曲線と横移動を Foundation/Motion へ（E-19）、保険タイマーを案 a/b 比較で決定、死活表、撮影の資産書き換え
- Phase 2.6: 実施済み / 強4・弱3・第3バケツ2 / 内容を種別の型に、Model を読み口と書き口に分割、欠けは LogError。裁定は agent 既定で判断記録へ

## 配置と前例

データの流れ（矢印は呼び出し・購読の向き。`[ ]` は状態を持つもの）:

```
NotificationFeed（既存。初期データ SaveMigrationNotice ＋ OnNotified）
   │ 購読（Initialize で初期データ→購読を同じ同期区間で）
   ▼
NotificationFeedRelay ──Push──▶ NotificationContentFactory.FromMessage
   │                              （種別→中身の型、NotificationParams.FromWire で引数を型付きに）
   │ INotificationListWriter.AddNotification
   ▼
[NotificationModel]（行の一覧・獲得の合流。時間を持たない）
   │ INotificationListSource.Current（IReadOnlyReactiveProperty<NotificationListViewModel>）
   ▼ Presenter が Subscribe
NotificationLayerPresenter ──▶ NotificationHostView.Render（Id で差分描画）
   ├─ TextOf(row) = NotificationText.Resolve（INotificationTextLookup）──▶ NotificationRowView（アイコンは INotificationRowIcons → GameContentPresentation）
   └─ NotificationRowLifetime.Play（UiTweenAnimation 2 本：入場・遅延つき退場。UiMotionPlayer＝パネルの時計）
          │ 退場の Completed
          ▼
      INotificationListWriter.RemoveNotification(id) ──▶ [NotificationModel]（書き口へ戻る）
```

| 置き場 | 前例（役割で選んだもの） | 従い方 |
|---|---|---|
| 行の出入りの動き（Screens の Parts） | `S/Client.Ui/Screens/Toast/ToastHostView.cs`（`UiTweenAnimation(UiMotionSpec, IUiTweenStep)` を `UiMotionPlayer.Play` で回し、終わりの `Completed` で片付ける） | 同じ形。スタイルを書く独自の `IUiAnimation` は作らず、Foundation の Step（`OpacityTweenStep`・新規 `TranslateXTweenStep`）を束ねる Step 1 つと、2 本の tween を組む口だけを Screens に置く |
| 横移動の Step（Foundation/Motion/Steps。E-19） | `TranslateYTweenStep`（縦だけを補間し横は 0） | 軸を入れ替えた写し。テストは `UiTweenStepTest` の TranslateY と同じ形 |
| CSS の曲線（Foundation/Motion/Tween。E-19） | `UiEasing`＋`UiEasingFunctions.Evaluate` の switch | enum に 2 値、switch に 2 分岐。未知は既存の LogError のまま |
| サーバー可変状態の受け（State） | 既存 `NotificationFeed`（初期データ＋イベント＋購読の 3 点セット） | 中継が初期データを読んでから購読する。新しいパケットは作らない |
| 時計の要るテスト | `S/Client.Tests/Ui/Motion/UiMotionTimelineTest.cs` の `ManualClock`（`IUiMotionClock`） | Model が時間を持たなくなったので使わない（時計を新しく作らない。L1 はパネルの `simulate.FrameUpdateMs`） |

## 死活表

ホストが常駐し保険タイマーを持たない（D-2 案 a）ので、「行の除去は退場の動きの終わりだけが起こす」が成り立つ操作を並べる。上の 5 行は UITK の中の行の生存、最後の行はこのレーンの開始時（Task 6 Step 2）に確かめる操作。

| 操作 | ホストとパネル | 行の動き | 除去 | 確かめるテスト・手順 |
|---|---|---|---|---|
| 研究・チャレンジの完了（実績の通知） | 常駐・付いたまま | 入場→保持→退場 | 退場の終わり | `NotificationHostViewTest` の出入り、Task 11 の録画 |
| 操作の拒否（警告色の行） | 常駐・付いたまま | 同上 | 同上 | `NotificationRowViewTest` の警告色、`NotificationHostViewTest` |
| 獲得の連続（合流） | 常駐・付いたまま | 回り直しで頭から再生（古い 2 本は取り消し） | 回り直した退場の終わり | `NotificationHostViewTest`「合流で生存尺が回り直し…」 |
| 起動時の除去告知（初期データ） | Presenter の Initialize より前に Model へ積まれ、ホストが付いた後の最初の描画で出る | 同上 | 同上 | `NotificationFeedRelayTest`、`NotificationHostViewLayoutTest`「スタイル解決前の描画は解決後に出る」 |
| 言語の切り替え | 常駐・付いたまま | 止めない（文だけ解き直す） | 変わらない | `NotificationHostViewLayoutTest`「言語を変えると…」 |
| Ctrl+U・カットシーンの全部隠し（S2-skit） | 隠しが `display`／`visibility` で隠す実物なら付いたまま（パネルのスケジューラは回り続け、隠れている間も生存は進む＝推測。Task 6 Step 2 の L1 で実測）。要素を外す実物なら動きが止まり除去が来ない | 同左 | 同左。外す実物なら `NotificationHostView` の外れの LogWarning が開発者に知らせる | Task 6 Step 2（S2-skit が背面の層を隠すなら L1 を 1 件、隠さないなら隠しの対象へ 1 行、未マージなら done.md に S2-ladder の確認事項として書く） |


既存の操作のうちこの計画が触れる機構にぶら下がるもの（設計検査〈fresh-context〉が実物で確かめた）:

| 操作 | 計画の後も生きるか | 根拠 |
|---|---|---|
| 全体スイッチ Web のときの通知 | 生きる | `MainGameUiRegistration` は Web 系では早期 return し、`NotificationFeed` と Web 橋の `NotificationTopic` は変えない |
| 起動時の除去告知の 1 回出し | 生きる | Web 側は既読表、UITK 側は中継の初期データ（`NotificationFeed.SaveMigrationNotice`） |
| DSL のロケータ `notification-host` | 生きる | UITK のホストも `name = "notification-host"` を持つ（Task 8） |
| `NotificationStateProbe`・`WireContractNotificationTest` | 生きる | 通知の wire と Feed の公開面を変えない |
| 入力を奪わない・画面の裏に沈む | 生きる | ホストは `PickingMode.Ignore` で `UiLayer.BehindStage` に置く（Task 8・9。`NotificationLayeringTest`） |
---

## 写すテストの対応と件数（件数の正本。ほかの節はここを引く）

数え方: 「`it` の行」は `it(`・`test(` の行と `it.each` を 1 件と数える。「展開」は `it.each` の行を表の行数（3）で数える。台帳（`docs/uitk/TODO.md`）と実行計画の数は台帳の数え方のまま写す。

| 範囲 | `it` の行 | 展開 | 台帳・実行計画 |
|---|---|---|---|
| 単体の 5 ファイル（`src/features/notification/*.test.ts`。`notificationLayering.test.ts` を含む） | 42（L 27・S 15） | 44（L 29・S 15） | L 26・S 15 |
| e2e の 4 ファイル（`e2e/tests/notification/*.spec.ts`） | 6 | 6 | P 6 |
| 計 | 48 | 50 | 47（実行計画の「実数 48」は `it` の行の数で、展開すると 50。指揮へ返した） |
| 別枠: `portalLayerScaling.spec.ts` の通知の 1 件（台帳は uiScale の行） | 1 | 1 | L1 に写す（2 fixture で 2 件走る） |

写さないのは 1 件: `notificationStore.test.ts` の 1 件（`捨てる`。D-2）。portalLayerScaling の通知の 1 件は論理 1706.7×720 の L1 に写す（D-12）。messageId→キーの表は Web の `notificationKeys` の実物 70 行。

| Web のファイル | 種別 | 件数 | 写し先（NUnit） | タスク |
|---|---|---|---|---|
| `src/features/notification/notificationStore.test.ts` | L | 11 | `NotificationModelTest` 8・`NotificationFeedRelayTest` 2・`捨てる` 1（「退場アニメが発火しなくても保険タイマーで消える」。D-2） | 1・2・10 |
| `src/features/notification/notificationMessages.test.ts` | L | 16（`it` 15＋`it.each` 1） | `NotificationMessageKeysTest` 5・`NotificationTextTest` 10・`NotificationTextLocalizeTest` 1（TestCase 3 行） | 3 |
| `src/features/notification/notificationServerIdCoverage.test.ts` | S | 5 | `NotificationServerIdCoverageTest` 5 | 4 |
| `src/features/notification/notificationAnimationDesign.test.ts` | S | 7 | `NotificationModelTest` 1・`NotificationRowLifetimeTest` 2・`NotificationHostViewTest` 4 | 1・6・8 |
| `src/features/notification/notificationLayering.test.ts` | S | 3 | `NotificationLayeringTest` 3 | 9 |
| `e2e/tests/notification/animation.spec.ts` | P | 1 | `NotificationHostViewTest` 1 | 8 |
| `e2e/tests/notification/fixedWidth.spec.ts` | P | 1 | `NotificationHostViewLayoutTest` 1 | 8 |
| `e2e/tests/notification/itemEarned.spec.ts` | P | 1 | `NotificationHostViewLayoutTest` 1 | 8 |
| `e2e/tests/notification/layering.spec.ts` | P | 3 | `NotificationHostViewLayoutTest` 1・`NotificationLayeringTest` 2 | 8・9 |
| `e2e/tests/system/portalLayerScaling.spec.ts`（通知の 1 件のみ） | P | 1 | `NotificationViewportScalingTest` 1（`[TestFixture]` 2 つで 2 件走る） | 8 |

UITK だけのテストは別に 23 メソッド（タグ無し。各タスクに明記）。NUnit の件数（TestCase を 1 行 1 件）は L0 41（Model 10・Relay 4・Keys 5・Text 11・Localize 4・Lookup 2・Coverage 5）・Motion 11・Screens 24（Lifetime 3・Row 3・Host 7・Layout 9・Scaling 1×2 fixture）・Layering 5 の計 81（全部隠しの L1 を足すなら Layering 6・計 82）。以下、件数はこの表を正とする。

---

## File Structure

| パス | 責務 | タスク |
|---|---|---|
| `S/Client.Ui.State/Notification/Content/NotificationContent.cs` | 行の中身の判別 union（基底＋sealed の獲得・実績・操作の拒否〈警告色〉・セーブの除去告知〈アイコン無し〉。前例 `HotbarDropAction.cs`） | 1 |
| `S/Client.Ui.State/Notification/Content/NotificationIcon.cs` | アイコンの判別 union（基底＋アイテム＋アイコン無し） | 1 |
| `S/Client.Ui.State/Notification/Params/NotificationParam.cs` | 引数の判別 union（基底＋文字のまま＋内容名のキーを持つ引数〈研究名・チャレンジ題〉） | 1 |
| `S/Client.Ui.State/Notification/Params/NotificationParams.cs` | 受信の文字の辞書 → 型付きの引数 | 1 |
| `S/Client.Ui.State/Notification/NotificationRow.cs` | 1 行（Id・LifetimeEpoch・Content） | 1 |
| `S/Client.Ui.State/Notification/NotificationListViewModel.cs` | 表示中の行の並び（古い順） | 1 |
| `S/Client.Ui.State/Notification/INotificationListSource.cs` | 読み口（`Current`） | 1 |
| `S/Client.Ui.State/Notification/INotificationListWriter.cs` | 書き口（`AddNotification`・`RemoveNotification`） | 1 |
| `S/Client.Ui.State/Notification/NotificationModel.cs` | 行の一覧・獲得の合流（両方の口を実装） | 1 |
| `S/Client.Ui.State/Notification/NotificationFeedRelay.cs` | Feed を購読し中身へ変換して書き口へ積む | 2 |
| `S/Client.Ui.State/Notification/NotificationContentFactory.cs` | `NotificationMessagePack` → 中身の型 | 2 |
| `S/Client.Ui.State/Notification/NotificationFeed.cs` | 既存（変更なし） | — |
| `S/Client.Ui.State/Notification/SequencedNotification.cs` | 既存（変更なし） | — |
| `S/Client.Ui.State/Notification/Text/NotificationMessageKeys.cs` | messageId→辞書キーの表 | 3 |
| `S/Client.Ui.State/Notification/Text/INotificationTextLookup.cs` | 内容名・アイテム名を引く口 | 3 |
| `S/Client.Ui.State/Notification/Text/LocalizeNotificationTextLookup.cs` | 本番の口（Localize・マスタ。欠けは LogError と Web と同じ見え方） | 3 |
| `S/Client.Ui.State/Notification/Text/NotificationText.cs` | 中身 → `UiText` | 3 |
| `S/Client.Ui/Foundation/Motion/Tween/UiEasing.cs` | 変更: `CssEaseOut`・`CssEaseIn`（E-19） | 6 |
| `S/Client.Ui/Foundation/Motion/Steps/TranslateXTweenStep.cs` | 新規: 横の translate の Step（E-19） | 6 |
| `S/Client.Ui/Screens/Notification/NotificationLayerPresenter.cs` | 背面の層へホストを常駐させ読み口を購読 | 9 |
| `S/Client.Ui/Screens/Notification/NotificationHostView.cs` | 行の差分描画・生存の動き・書き口への除去・言語 | 8 |
| `S/Client.Ui/Screens/Notification/NotificationRowView.cs` | 1 行（アイコンと文） | 7 |
| `S/Client.Ui/Screens/Notification/INotificationRowIcons.cs` | ItemId → `UiIcon` の口 | 7 |
| `S/Client.Ui/Screens/Notification/GameContentNotificationRowIcons.cs` | 本番の口（`GameContentPresentation`） | 7 |
| `S/Client.Ui/Screens/Notification/Notification.uss` | ホストと行の USS | 8 |
| `S/Client.Ui/Screens/Notification/Parts/NotificationRowTiming.cs` | 入場・退場・生存・ずれの値 | 6 |
| `S/Client.Ui/Screens/Notification/Parts/NotificationRowFadeSlideStep.cs` | 不透明度と横移動の Step を同じ進みで書く束ね | 6 |
| `S/Client.Ui/Screens/Notification/Parts/NotificationRowLifetime.cs` | 入場と遅延つき退場の 2 本の tween を組んで回す | 6 |
| `S/Client.Ui/Screens/Notification/Parts/NotificationRowLifetimeHandle.cs` | 2 本の取り消しと退場の終わりの通知 | 6 |
| `S/Client.Starter/Registration/GameUi/NotificationGameUiRegistration.cs` | DI 登録 | 9 |
| `S/Client.Starter/Registration/MainGameUiRegistration.cs` | 画面登録区画に 1 行 | 9 |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` | Screens 区画に `@import` 1 行 | 8 |
| `S/Client.Tests/Ui/State/Notification/NotificationModelTest.cs` | R1 | 1 |
| `S/Client.Tests/Ui/State/Notification/NotificationTestContents.cs` | テストの中身の作り方 | 1 |
| `S/Client.Tests/Ui/State/Notification/NotificationFeedRelayTest.cs` | R2 | 2 |
| `S/Client.Tests/Ui/State/Notification/NotificationMessageKeysTest.cs` | R3 | 3 |
| `S/Client.Tests/Ui/State/Notification/NotificationTextTest.cs` | R3 | 3 |
| `S/Client.Tests/Ui/State/Notification/NotificationTextLocalizeTest.cs` | R3（Localize で解く 4 件） | 3 |
| `S/Client.Tests/Ui/State/Notification/LocalizeNotificationTextLookupTest.cs` | R3（欠けの LogError と見え方） | 3 |
| `S/Client.Tests/Ui/State/Notification/FakeNotificationTextLookup.cs` | テストの口 | 3 |
| `S/Client.Tests/Ui/State/Notification/Coverage/NotificationServerIdCoverageTest.cs` | R4 | 4 |
| `S/Client.Tests/Ui/State/Notification/Coverage/NotificationServerIdScanner.cs` | 送信元の走査 | 4 |
| `S/Client.Tests/Ui/State/Notification/Coverage/InterpolatedNotificationIdEnums.cs` | 補間 id の接頭辞と enum の表 | 4 |
| `S/Client.Tests/Ui/Motion/UiEasingCssCurveTest.cs` | R6（CSS の曲線。E-19） | 6 |
| `S/Client.Tests/Ui/Motion/UiTweenStepTest.cs` | R6（TranslateX の 1 メソッドを足す。E-19） | 6 |
| `S/Client.Tests/Ui/Screens/Notification/NotificationRowLifetimeTest.cs` | R6 | 6 |
| `S/Client.Tests/Ui/Screens/Notification/NotificationRowViewTest.cs` | R5 | 7 |
| `S/Client.Tests/Ui/Screens/Notification/FakeNotificationRowIcons.cs` | テストの口 | 7 |
| `S/Client.Tests/Ui/Screens/Notification/NotificationHostViewTest.cs` | R6（出入りと除去の時間） | 8 |
| `S/Client.Tests/Ui/Screens/Notification/NotificationHostViewLayoutTest.cs` | R5・R8（並び・寸法・文・入力・言語） | 8 |
| `S/Client.Tests/Ui/Screens/Notification/NotificationHostViewTestFixture.cs` | ホストの L1 の土台 | 8 |
| `S/Client.Tests/Ui/Screens/Notification/NotificationViewportScalingTest.cs` | R5・R9（portalLayerScaling の通知の 1 件を 2 解像度で） | 8 |
| `S/Client.Tests/Ui/Screens/Notification/NotificationLayeringTest.cs` | R7 | 9 |
| `SC/server-notification-smoke.cs` | 録画（直す） | 11 |
| `SC/uitk/stage2/uitk-notification-compare.cs` | 並べ比べの撮影 | 10 |
| `docs/uitk/disposition.md` | 処遇の 2 行 | 10 |
| `docs/uitk/TODO.md` | 実測の結論・見た目の例外（必要時）・追従記録（必要時） | 10・12 |

`Client.Ui.State/Notification/` は直下 9 ファイル（既存 2＋新規 7）＋`Content/` 2＋`Params/` 2＋`Text/` 4、`Screens/Notification/` は直下 7 ファイル＋`Parts/` 4、`Foundation/Motion/Steps/` は 4 ファイル（S2-tutorial の `ScaleTweenStep.cs` も入ると 5）、`Client.Tests/Ui/State/Notification/` は直下 8 ファイル＋`Coverage/`、`Client.Tests/Ui/Screens/Notification/` は 8 ファイル、`Client.Tests/Ui/Motion/` は 6 ファイル（S2-tutorial の `ScaleTweenStepTest.cs` も入ると 7）。両方入っても 10 以下。

---

### Task 1: 通知の中身の型と Model（L0・Editor 不要）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Content/NotificationContent.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Content/NotificationIcon.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Params/NotificationParam.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Params/NotificationParams.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/NotificationRow.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/NotificationListViewModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/INotificationListSource.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/INotificationListWriter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/NotificationModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/NotificationTestContents.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/NotificationModelTest.cs`

**Interfaces:**
- Consumes: `Core.Master.ItemId`（`new ItemId(int)`・`AsPrimitive()`）、`Client.Localization.ContentLocalizationKey`、`Mooresmaster.Localization.Generated.ContentLocalizationKeys.ResearchName(Guid)`・`.ChallengeTitle(Guid)`、UniRx の `ReactiveProperty<T>`・`IReadOnlyReactiveProperty<T>`
- Produces（Task 2・3・7・8・9 が使う）:
  - 中身（`Client.Ui.State.Notification.Content`）: `abstract class NotificationContent { string MessageId { get; } IReadOnlyList<NotificationParam> MessageParams { get; } NotificationIcon Icon { get; } }`、`sealed class EarnedNotificationContent(string messageId, IReadOnlyList<NotificationParam> messageParams, ItemId itemId, int count) { ItemId ItemId; int Count; bool CanMergeWith(EarnedNotificationContent); EarnedNotificationContent MergeWith(EarnedNotificationContent) }`（アイコンは `ItemNotificationIcon(itemId)`）、`sealed class AchievementNotificationContent(string, IReadOnlyList<NotificationParam>, NotificationIcon)`、`sealed class OperationDeniedNotificationContent(string, IReadOnlyList<NotificationParam>, NotificationIcon)`、`sealed class SaveMigrationNotificationContent(string, IReadOnlyList<NotificationParam>)`（アイコンは `NoNotificationIcon.Instance`）
  - アイコン: `abstract class NotificationIcon`、`sealed class ItemNotificationIcon(ItemId itemId) { ItemId ItemId { get; } }`、`sealed class NoNotificationIcon { static readonly NoNotificationIcon Instance }`
  - 引数（`Client.Ui.State.Notification.Params`）: `abstract class NotificationParam { string Name { get; } }`、`sealed class TextNotificationParam(string name, string value) { string Value { get; } }`、`sealed class ContentNameNotificationParam(string name, ContentLocalizationKey key) { ContentLocalizationKey Key { get; } }`、`static class NotificationParams { static IReadOnlyList<NotificationParam> FromWire(string messageId, IReadOnlyDictionary<string, string> wireParams); }`（警告 `[NotificationParams] {messageId}: missing param {name}`・`[NotificationParams] {messageId}: param {name} is not a guid: {raw}`）
  - `sealed class NotificationRow { long Id { get; } int LifetimeEpoch { get; } NotificationContent Content { get; } }`、`sealed class NotificationListViewModel { IReadOnlyList<NotificationRow> Rows { get; } static readonly NotificationListViewModel Empty }`（古い順）
  - 読み口 `interface INotificationListSource { IReadOnlyReactiveProperty<NotificationListViewModel> Current { get; } }`（読む側: `NotificationLayerPresenter`）、書き口 `interface INotificationListWriter { void AddNotification(NotificationContent content); void RemoveNotification(long id); }`（書く側: `NotificationFeedRelay` が Add、`NotificationHostView` が Remove の 2 つだけ）
  - `sealed class NotificationModel : INotificationListSource, INotificationListWriter { const int DisplayMilliseconds = 7000; NotificationModel(); }`
  - テスト用: `NotificationTestContents.NoParams`・`NoIcon`・`ItemIcon(int)`・`Earned(int itemId, int count, string messageId)`・`Achievement(string, IReadOnlyList<NotificationParam>, NotificationIcon)`・`Denied(string, IReadOnlyList<NotificationParam>, NotificationIcon)`・`SaveMigration(string, IReadOnlyList<NotificationParam>)`・`Wire(string messageId, Dictionary<string, string> wireParams)`

- [ ] **Step 1: テストの中身の作り方を書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/NotificationTestContents.cs`）

```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Notification.Params;
using Core.Master;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// テストで使う通知の中身の作り方。受信の引数は本番と同じ FromWire で型付きにする
    /// Builders for the notification contents used by tests; wire params become typed through the production FromWire
    /// </summary>
    internal static class NotificationTestContents
    {
        public static readonly IReadOnlyList<NotificationParam> NoParams = Array.Empty<NotificationParam>();
        public static readonly NotificationIcon NoIcon = NoNotificationIcon.Instance;

        public static NotificationIcon ItemIcon(int itemId)
        {
            return new ItemNotificationIcon(new ItemId(itemId));
        }

        public static IReadOnlyList<NotificationParam> Wire(string messageId, Dictionary<string, string> wireParams)
        {
            return NotificationParams.FromWire(messageId, wireParams);
        }

        public static EarnedNotificationContent Earned(int itemId, int count, string messageId)
        {
            return new EarnedNotificationContent(messageId, NoParams, new ItemId(itemId), count);
        }

        public static AchievementNotificationContent Achievement(string messageId, IReadOnlyList<NotificationParam> messageParams, NotificationIcon icon)
        {
            return new AchievementNotificationContent(messageId, messageParams, icon);
        }

        public static OperationDeniedNotificationContent Denied(string messageId, IReadOnlyList<NotificationParam> messageParams, NotificationIcon icon)
        {
            return new OperationDeniedNotificationContent(messageId, messageParams, icon);
        }

        public static SaveMigrationNotificationContent SaveMigration(string messageId, IReadOnlyList<NotificationParam> messageParams)
        {
            return new SaveMigrationNotificationContent(messageId, messageParams);
        }
    }
}
```

- [ ] **Step 2: Model のテストを書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/NotificationModelTest.cs`）

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Content;
using Core.Master;
using NUnit.Framework;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// 通知の行の一覧と獲得の合流を確かめる（L0。Web の notificationStore.test.ts。保険タイマーの 1 件は捨てる＝D-2）
    /// Verifies the notification rows and earned-row merging (L0; Web notificationStore.test.ts; the fallback-timer test is dropped, D-2)
    /// </summary>
    public class NotificationModelTest
    {
        private NotificationModel _model;

        private IReadOnlyList<NotificationRow> Rows => _model.Current.Value.Rows;

        [SetUp]
        public void CreateModel()
        {
            _model = new NotificationModel();
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">itemIdを保持する</web-test>
        [Test]
        public void 拒否の行もアイコンのアイテムを保持する()
        {
            _model.AddNotification(Denied("denied.craftMaterialShortage", NoParams, ItemIcon(42)));
            Assert.That(Rows[0].Content.Icon, Is.TypeOf<ItemNotificationIcon>().With.Property(nameof(ItemNotificationIcon.ItemId)).EqualTo(new ItemId(42)));
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">退場用の状態を持たない</web-test>
        [Test]
        public void 行は退場用の状態を持たない()
        {
            var names = typeof(NotificationRow).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).OrderBy(name => name);
            CollectionAssert.AreEqual(new[] { "Content", "Id", "LifetimeEpoch" }, names);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">表示中の同一アイテムの獲得は1行に加算される</web-test>
        [Test]
        public void 表示中の同一アイテムの獲得は1行に加算される()
        {
            _model.AddNotification(Earned(7, 5, "itemEarned.mined"));
            _model.AddNotification(Earned(7, 3, "itemEarned.mined"));
            Assert.AreEqual(1, Rows.Count);
            Assert.AreEqual(8, EarnedAt(0).Count);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">加算時はidを据え置きlifetimeEpochで生存尺が回り直す</web-test>
        [Test]
        public void 加算時はidを据え置きLifetimeEpochで生存尺が回り直す()
        {
            // 生存尺の回り直しは epoch の進みで View に伝わる（時間は View が持つ。再生の頭出しは NotificationHostViewTest）
            // A lifetime restart reaches the View as an advanced epoch (the View owns time; the replay is in NotificationHostViewTest)
            _model.AddNotification(Earned(7, 5, "itemEarned.mined"));
            var firstId = Rows[0].Id;
            _model.AddNotification(Earned(7, 3, "itemEarned.mined"));
            Assert.AreEqual(firstId, Rows[0].Id);
            Assert.AreEqual(1, Rows[0].LifetimeEpoch);
            _model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            Assert.AreEqual(firstId, Rows[0].Id);
            Assert.AreEqual(2, Rows[0].LifetimeEpoch);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">加算しても表示順は入れ替わらない</web-test>
        [Test]
        public void 加算しても表示順は入れ替わらない()
        {
            _model.AddNotification(Earned(7, 5, "itemEarned.mined"));
            _model.AddNotification(Earned(9, 1, "itemEarned.mined"));
            _model.AddNotification(Earned(7, 3, "itemEarned.mined"));
            CollectionAssert.AreEqual(new[] { new ItemId(7), new ItemId(9) }, new[] { EarnedAt(0).ItemId, EarnedAt(1).ItemId });
            Assert.AreEqual(8, EarnedAt(0).Count);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">別アイテムの獲得は別行になる</web-test>
        [Test]
        public void 別アイテムの獲得は別行になる()
        {
            _model.AddNotification(Earned(7, 5, "itemEarned.mined"));
            _model.AddNotification(Earned(9, 3, "itemEarned.mined"));
            Assert.AreEqual(2, Rows.Count);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">messageIdが異なる同一アイテムの獲得は合流しない</web-test>
        [Test]
        public void MessageIdが異なる同一アイテムの獲得は合流しない()
        {
            _model.AddNotification(Earned(7, 5, "itemEarned.mined"));
            _model.AddNotification(Earned(7, 3, "itemEarned.crafted"));
            Assert.AreEqual(2, Rows.Count);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">獲得以外のカテゴリは同じ内容でも集約されない</web-test>
        [Test]
        public void 獲得以外のカテゴリは同じ内容でも集約されない()
        {
            _model.AddNotification(Achievement("achievement.unlockedItem", NoParams, ItemIcon(7)));
            _model.AddNotification(Achievement("achievement.unlockedItem", NoParams, ItemIcon(7)));
            Assert.AreEqual(2, Rows.Count);
        }

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">同時表示数の上限を持たない</web-test>
        [Test]
        public void 同時表示数の上限を持たない()
        {
            for (var i = 0; i < 50; i++) _model.AddNotification(Earned(i + 1, 1, "itemEarned.mined"));
            Assert.AreEqual(50, Rows.Count);
        }

        [Test]
        public void 除去すると指定の行だけが消え同じidの再除去は何もしない()
        {
            _model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            _model.AddNotification(Earned(9, 1, "itemEarned.mined"));
            var firstId = Rows[0].Id;
            _model.RemoveNotification(firstId);
            _model.RemoveNotification(firstId);
            Assert.AreEqual(1, Rows.Count);
            Assert.AreEqual(new ItemId(9), EarnedAt(0).ItemId);
        }

        private EarnedNotificationContent EarnedAt(int index)
        {
            if (Rows[index].Content is EarnedNotificationContent earned) return earned;
            Assert.Fail($"row {index} is {Rows[index].Content.GetType().Name}, not an earn");
            return null;
        }
    }
}
```

- [ ] **Step 3: 引数の型を書く**（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Params/NotificationParam.cs`）

```csharp
namespace Client.Ui.State.Notification.Params
{
    /// <summary>
    /// 通知の文に補間する引数 1 つ。値が文字のままか、内容名のキーかを型で分ける
    /// One interpolation argument of a notification text; the type tells a plain-text value from a content-name key
    /// </summary>
    public abstract class NotificationParam
    {
        public string Name { get; }

        protected NotificationParam(string name)
        {
            Name = name;
        }
    }
}
```

（同じ `NotificationParam.cs` の続き。型 `TextNotificationParam`）

```csharp
namespace Client.Ui.State.Notification.Params
{
    /// <summary>
    /// 受信した文字をそのまま補間する引数（件数・Guid として読めなかった値など）
    /// An argument interpolated as the received text (counts, values that did not parse as a Guid, etc.)
    /// </summary>
    public sealed class TextNotificationParam : NotificationParam
    {
        public string Value { get; }

        public TextNotificationParam(string name, string value) : base(name)
        {
            Value = value;
        }
    }
}
```

（同じ `NotificationParam.cs` の続き。型 `ContentNameNotificationParam`）

```csharp
using Client.Localization;

namespace Client.Ui.State.Notification.Params
{
    /// <summary>
    /// 内容名（研究名・チャレンジ題）のキーを持つ引数。表示の時にいまの言語で引く
    /// An argument holding a content-name key (research name, challenge title), looked up in the current language at display time
    /// </summary>
    public sealed class ContentNameNotificationParam : NotificationParam
    {
        public ContentLocalizationKey Key { get; }

        public ContentNameNotificationParam(string name, ContentLocalizationKey key) : base(name)
        {
            Key = key;
        }
    }
}
```

（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Params/NotificationParams.cs`）

```csharp
using System;
using System.Collections.Generic;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Notification.Params
{
    /// <summary>
    /// 受信した文字の辞書を型付きの引数へ変える。Guid を運ぶ 2 種の通知だけ、その値を内容名のキーにする（Web の contentParamKeyBuilders）
    /// Turns the received text dictionary into typed arguments; only the two Guid-bearing notifications turn that value into a content-name key (Web contentParamKeyBuilders)
    /// </summary>
    public static class NotificationParams
    {
        private const string ResearchCompletedId = "achievement.researchCompleted";
        private const string ChallengeCompletedId = "achievement.challengeCompleted";

        public static IReadOnlyList<NotificationParam> FromWire(string messageId, IReadOnlyDictionary<string, string> wireParams)
        {
            var hasGuidParam = TryGetGuidParamName(messageId, out var guidParamName);
            var result = new List<NotificationParam>(wireParams.Count);
            foreach (var pair in wireParams) result.Add(ToParam(pair.Key, pair.Value));

            // 名前が欠けると文言に生の枠が残るので、開発者が気づけるよう警告する（Localize も {name} を残して LogError を出す）
            // A missing value leaves a raw token in the text, so warn for developers (Localize also keeps {name} and logs an error)
            if (hasGuidParam && !wireParams.ContainsKey(guidParamName)) Debug.LogWarning($"[NotificationParams] {messageId}: missing param {guidParamName}");
            return result;

            #region Internal

            NotificationParam ToParam(string name, string raw)
            {
                if (!hasGuidParam || name != guidParamName) return new TextNotificationParam(name, raw);
                if (Guid.TryParse(raw, out var guid)) return new ContentNameNotificationParam(name, ContentKeyOf(messageId, guid));

                // 読めない値は文字のまま出し、理由を開発者に残す
                // Show an unreadable value as text and leave the reason for developers
                Debug.LogWarning($"[NotificationParams] {messageId}: param {name} is not a guid: {raw}");
                return new TextNotificationParam(name, raw);
            }

            #endregion
        }

        private static bool TryGetGuidParamName(string messageId, out string name)
        {
            name = messageId switch
            {
                ResearchCompletedId => "research",
                ChallengeCompletedId => "challenge",
                _ => string.Empty,
            };
            return name.Length > 0;
        }

        private static ContentLocalizationKey ContentKeyOf(string messageId, Guid guid)
        {
            // TryGetGuidParamName が真の messageId だけがここへ来る
            // Only messageIds for which TryGetGuidParamName is true arrive here
            return messageId switch
            {
                ResearchCompletedId => ContentLocalizationKeys.ResearchName(guid),
                ChallengeCompletedId => ContentLocalizationKeys.ChallengeTitle(guid),
                _ => throw new ArgumentOutOfRangeException(nameof(messageId), messageId, null),
            };
        }
    }
}
```

- [ ] **Step 4: アイコンと中身の型を書く**（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Content/NotificationIcon.cs`）

```csharp
namespace Client.Ui.State.Notification.Content
{
    /// <summary>
    /// 行の頭に出すアイコン。アイテムか無しかを型で表す（null で「無し」を表さない）
    /// The icon at the head of a row; the type says item or none (null never means "none")
    /// </summary>
    public abstract class NotificationIcon
    {
    }
}
```

（同じ `NotificationIcon.cs` の続き。型 `ItemNotificationIcon`）

```csharp
using Core.Master;

namespace Client.Ui.State.Notification.Content
{
    /// <summary>
    /// アイテムのアイコン
    /// An item icon
    /// </summary>
    public sealed class ItemNotificationIcon : NotificationIcon
    {
        public ItemId ItemId { get; }

        public ItemNotificationIcon(ItemId itemId)
        {
            ItemId = itemId;
        }
    }
}
```

（同じ `NotificationIcon.cs` の続き。型 `NoNotificationIcon`）

```csharp
namespace Client.Ui.State.Notification.Content
{
    /// <summary>
    /// アイコン無し（除去告知・アイテムを伴わない実績と拒否）
    /// No icon (prune notices, achievements and denials without an item)
    /// </summary>
    public sealed class NoNotificationIcon : NotificationIcon
    {
        public static readonly NoNotificationIcon Instance = new();

        private NoNotificationIcon()
        {
        }
    }
}
```

（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Content/NotificationContent.cs`）

```csharp
using System.Collections.Generic;
using Client.Ui.State.Notification.Params;

namespace Client.Ui.State.Notification.Content
{
    /// <summary>
    /// 通知 1 行の中身の基底。種別ごとの sealed な型が持つ項目を決める（数を持つのは獲得だけ。Web の NewGameNotification の判別 union）
    /// Base of one notification row's content; sealed per-kind types decide the fields (only earns carry a count; Web NewGameNotification union)
    /// </summary>
    public abstract class NotificationContent
    {
        public string MessageId { get; }
        public IReadOnlyList<NotificationParam> MessageParams { get; }
        public NotificationIcon Icon { get; }

        protected NotificationContent(string messageId, IReadOnlyList<NotificationParam> messageParams, NotificationIcon icon)
        {
            MessageId = messageId;
            MessageParams = messageParams;
            Icon = icon;
        }
    }
}
```

（同じ `NotificationContent.cs` の続き。型 `EarnedNotificationContent`）

```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Notification.Params;
using Core.Master;

namespace Client.Ui.State.Notification.Content
{
    /// <summary>
    /// アイテム獲得の中身。同じアイテム・同じ messageId の行へ数を足して合流する
    /// Content of an item earn; merges into a row with the same item and messageId by adding the count
    /// </summary>
    public sealed class EarnedNotificationContent : NotificationContent
    {
        public ItemId ItemId { get; }
        public int Count { get; }

        public EarnedNotificationContent(string messageId, IReadOnlyList<NotificationParam> messageParams, ItemId itemId, int count) : base(messageId, messageParams, new ItemNotificationIcon(itemId))
        {
            // 0 個以下の獲得は表せない結果なので作らせない（サーバーの CreateItemEarned と同じ約束）
            // A zero-or-less earn is not representable, so refuse it (same contract as the server CreateItemEarned)
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, null);
            ItemId = itemId;
            Count = count;
        }

        public bool CanMergeWith(EarnedNotificationContent newer)
        {
            return newer.ItemId == ItemId && newer.MessageId == MessageId;
        }

        // 数を足し、ほかの項目は新しい方を採る（Web の { ...n, count: merged.count + n.count }）
        // Add the counts and take the newer fields otherwise (Web { ...n, count: merged.count + n.count })
        public EarnedNotificationContent MergeWith(EarnedNotificationContent newer)
        {
            return new EarnedNotificationContent(newer.MessageId, newer.MessageParams, newer.ItemId, Count + newer.Count);
        }
    }
}
```

（同じ `NotificationContent.cs` の続き。型 `AchievementNotificationContent`）

```csharp
using System.Collections.Generic;
using Client.Ui.State.Notification.Params;

namespace Client.Ui.State.Notification.Content
{
    /// <summary>
    /// 実績（研究・チャレンジの完了、ブロック・アイテムの解放）の中身
    /// Content of an achievement (research or challenge completion, block or item unlock)
    /// </summary>
    public sealed class AchievementNotificationContent : NotificationContent
    {
        public AchievementNotificationContent(string messageId, IReadOnlyList<NotificationParam> messageParams, NotificationIcon icon) : base(messageId, messageParams, icon)
        {
        }
    }
}
```

（同じ `NotificationContent.cs` の続き。型 `OperationDeniedNotificationContent`）

```csharp
using System.Collections.Generic;
using Client.Ui.State.Notification.Params;

namespace Client.Ui.State.Notification.Content
{
    /// <summary>
    /// 操作の拒否の中身。View はこの型を見て警告色で出す
    /// Content of a denied operation; the View shows this type in the warning color
    /// </summary>
    public sealed class OperationDeniedNotificationContent : NotificationContent
    {
        public OperationDeniedNotificationContent(string messageId, IReadOnlyList<NotificationParam> messageParams, NotificationIcon icon) : base(messageId, messageParams, icon)
        {
        }
    }
}
```

（同じ `NotificationContent.cs` の続き。型 `SaveMigrationNotificationContent`）

```csharp
using System.Collections.Generic;
using Client.Ui.State.Notification.Params;

namespace Client.Ui.State.Notification.Content
{
    /// <summary>
    /// セーブの変換で除いた件数の告知。アイコンは持たない
    /// Notice of what the save migration pruned; it carries no icon
    /// </summary>
    public sealed class SaveMigrationNotificationContent : NotificationContent
    {
        public SaveMigrationNotificationContent(string messageId, IReadOnlyList<NotificationParam> messageParams) : base(messageId, messageParams, NoNotificationIcon.Instance)
        {
        }
    }
}
```

- [ ] **Step 5: 行・並び・2 つの口を書く**（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/NotificationRow.cs`）

```csharp
using Client.Ui.State.Notification.Content;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// 表示中の 1 行。合流すると Id を据え置いて LifetimeEpoch が進む（退場の状態は持たない）
    /// One visible row; a merge keeps the Id and advances LifetimeEpoch (no exit state is held)
    /// </summary>
    public sealed class NotificationRow
    {
        public long Id { get; }
        public int LifetimeEpoch { get; }
        public NotificationContent Content { get; }

        public NotificationRow(long id, int lifetimeEpoch, NotificationContent content)
        {
            Id = id;
            LifetimeEpoch = lifetimeEpoch;
            Content = content;
        }
    }
}
```

（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/NotificationListViewModel.cs`）

```csharp
using System;
using System.Collections.Generic;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// 表示中の行の並び（古い順。新着が末尾）
    /// The visible rows, oldest first (newest last)
    /// </summary>
    public sealed class NotificationListViewModel
    {
        public static readonly NotificationListViewModel Empty = new(Array.Empty<NotificationRow>());

        public IReadOnlyList<NotificationRow> Rows { get; }

        public NotificationListViewModel(IReadOnlyList<NotificationRow> rows)
        {
            Rows = rows;
        }
    }
}
```

（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/INotificationListSource.cs`）

```csharp
using UniRx;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// 表示中の通知の行を読む口（読む側は NotificationLayerPresenter）
    /// Read side of the visible notification rows (read by NotificationLayerPresenter)
    /// </summary>
    public interface INotificationListSource
    {
        IReadOnlyReactiveProperty<NotificationListViewModel> Current { get; }
    }
}
```

（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/INotificationListWriter.cs`）

```csharp
using Client.Ui.State.Notification.Content;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// 通知の行を足す・除く口。書く側は中継（Add）とホストの View（退場の終わりの Remove）の 2 つだけ
    /// Write side that adds and removes notification rows; the only writers are the relay (Add) and the host View (Remove at the exit end)
    /// </summary>
    public interface INotificationListWriter
    {
        void AddNotification(NotificationContent content);
        void RemoveNotification(long id);
    }
}
```

- [ ] **Step 6: Model を書く**（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/NotificationModel.cs`）

```csharp
using System.Collections.Generic;
using Client.Ui.State.Notification.Content;
using UniRx;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// 表示中の通知の行を持ち、獲得だけを合流させる。時間は持たず、除去は View の退場の終わりが書き口から呼ぶ（Web の notificationStore。保険タイマーは D-2 で持たない）
    /// Holds the visible notification rows and merges only earns; it owns no time, and the View's exit end calls removal through the write side (Web notificationStore; no fallback timer, D-2)
    /// </summary>
    public sealed class NotificationModel : INotificationListSource, INotificationListWriter
    {
        // 生存尺は出入りを含めて 7 秒。View の動きの長さはこの定数だけから決まる（Web NOTIFICATION_DISPLAY_MS）
        // The 7s lifetime includes enter and exit; the View's motion length comes only from this constant (Web NOTIFICATION_DISPLAY_MS)
        public const int DisplayMilliseconds = 7000;

        private readonly ReactiveProperty<NotificationListViewModel> _current = new(NotificationListViewModel.Empty);
        private long _nextId = 1;

        public IReadOnlyReactiveProperty<NotificationListViewModel> Current => _current;

        public void AddNotification(NotificationContent content)
        {
            var rows = _current.Value.Rows;

            // 合流するのは獲得だけ。合流先は同じアイテム・同じ messageId の表示中の行で、id と位置を保ち epoch を進める
            // Only earns merge; the target is a visible row with the same item and messageId, keeping its id and position and advancing the epoch
            if (content is EarnedNotificationContent earned && TryFindMergeTarget(rows, earned, out var index, out var shown))
            {
                var merged = new NotificationRow(rows[index].Id, rows[index].LifetimeEpoch + 1, shown.MergeWith(earned));
                _current.Value = new NotificationListViewModel(new List<NotificationRow>(rows) { [index] = merged });
                return;
            }

            // 新しい行は末尾（新着）に足す
            // A new row goes to the end (newest)
            var row = new NotificationRow(_nextId++, 0, content);
            _current.Value = new NotificationListViewModel(new List<NotificationRow>(rows) { row });
        }

        public void RemoveNotification(long id)
        {
            var rows = _current.Value.Rows;
            var kept = new List<NotificationRow>(rows.Count);
            foreach (var row in rows)
            {
                if (row.Id != id) kept.Add(row);
            }

            // silent-ok: 同じ行の除去が 2 度来るのは、退場の終わりと差分描画の片付けが重なる正常な場合 / 消し漏れは NotificationModelTest と L1 の行数の検査が気づく
            if (kept.Count == rows.Count) return;
            _current.Value = new NotificationListViewModel(kept);
        }

        private static bool TryFindMergeTarget(IReadOnlyList<NotificationRow> rows, EarnedNotificationContent newer, out int index, out EarnedNotificationContent shown)
        {
            for (index = 0; index < rows.Count; index++)
            {
                if (rows[index].Content is EarnedNotificationContent candidate && candidate.CanMergeWith(newer))
                {
                    shown = candidate;
                    return true;
                }
            }
            index = -1;
            shown = null;
            return false;
        }
    }
}
```

- [ ] **Step 7: 行数と規約を目で確かめる**（Editor 無し）

Run: `wc -l moorestech_client/Assets/Scripts/Client.Ui.State/Notification/*.cs moorestech_client/Assets/Scripts/Client.Ui.State/Notification/*/*.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/*.cs` → 各 200 行未満。`grep -rn "Func<\|partial \|try\b\|Update()\|IScheduler" moorestech_client/Assets/Scripts/Client.Ui.State/Notification` → 0 件

- [ ] **Step 8: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Notification moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification
git commit -m "feat(uitk): 通知の中身を種別の型に分け、行の Model を読み口と書き口で Web の notificationStore から写す（S2-notification L0）"
```

---

### Task 2: Feed からの中継と中身への変換（L0・Editor 不要）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/NotificationContentFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/NotificationFeedRelay.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/NotificationFeedRelayTest.cs`

**Interfaces:**
- Consumes: 既存 `NotificationFeed`（ctor `(IVanillaApiEvent, ClientLocalNotificationSource)`・`Initialize()`・`SequencedNotification SaveMigrationNotice { get; private set; }`・`IObservable<SequencedNotification> OnNotified`）、`SequencedNotification`（`long Seq`・`NotificationMessagePack Message`）、`Server.Event.Notification.NotificationMessagePack`（`Category`・`MessageId`・`MessageParams`・`ItemId`・`Count`、生成は `CreateAchievement`・`CreateAchievementWithItem`・`CreateOperationDenied`・`CreateItemEarned(ItemId, int)`・`CreateSaveMigrationPruned(int, int, int)`）・`NotificationCategory`（`Achievement`・`OperationDenied`・`ItemEarned`・`SaveMigration`）、`ItemMaster.EmptyItemId`、`ClientLocalNotificationSource.Notify(NotificationMessagePack)`、テストの `CapturingVanillaApiEvent`（`S/Client.Tests/Inventory/`）、Task 1 の中身・引数・書き口
- Produces: `static class NotificationContentFactory { static NotificationContent FromMessage(NotificationMessagePack message); }`、`sealed class NotificationFeedRelay : IInitializable, IDisposable { NotificationFeedRelay(NotificationFeed feed, INotificationListWriter writer); }`（Task 9 が entry point に登録する）

- [ ] **Step 1: 中継のテストを書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/NotificationFeedRelayTest.cs`）

```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Notification;
using Client.Tests.Inventory;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Notification.Params;
using Core.Master;
using NUnit.Framework;
using Server.Event.Notification;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// Feed の通知が種別の型へ変換されて Model へ積まれ、除去告知が二重に出ないことを確かめる（L0。Web の claimSaveMigrationNotice の 2 件）
    /// Verifies feed notifications become per-kind contents in the model and the prune notice never shows twice (L0; Web claimSaveMigrationNotice x2)
    /// </summary>
    public class NotificationFeedRelayTest
    {
        private ClientLocalNotificationSource _localSource;
        private NotificationFeed _feed;
        private NotificationModel _model;
        private NotificationFeedRelay _relay;

        private IReadOnlyList<NotificationRow> Rows => _model.Current.Value.Rows;

        [SetUp]
        public void CreateFeed()
        {
            _localSource = new ClientLocalNotificationSource();
            _feed = new NotificationFeed(new CapturingVanillaApiEvent(), _localSource);
            _feed.Initialize();
            _model = new NotificationModel();
            _relay = new NotificationFeedRelay(_feed, _model);
        }

        [TearDown]
        public void DisposeRelay()
        {
            _relay.Dispose();
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">snapshotで再着した同じseqの除去告知は1回だけ通す</web-test>
        [Test]
        public void 購読前に来た除去告知は初期データから1回だけ出る()
        {
            _localSource.Notify(NotificationMessagePack.CreateSaveMigrationPruned(1, 2, 3));
            _relay.Initialize();
            _localSource.Notify(NotificationMessagePack.CreateOperationDenied("denied.craftResultFull", new Dictionary<string, string>()));

            Assert.AreEqual(2, Rows.Count);
            Assert.IsInstanceOf<SaveMigrationNotificationContent>(Rows[0].Content);
            Assert.AreEqual("saveMigration.missingMasterPruned", Rows[0].Content.MessageId);
            Assert.IsInstanceOf<OperationDeniedNotificationContent>(Rows[1].Content);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">サーバー再接続で届いた新しいseqの告知は再び通す</web-test>
        [Test]
        public void 購読後に届いた新しい除去告知は再び出る()
        {
            _localSource.Notify(NotificationMessagePack.CreateSaveMigrationPruned(1, 2, 3));
            _relay.Initialize();
            _localSource.Notify(NotificationMessagePack.CreateSaveMigrationPruned(4, 5, 6));

            Assert.AreEqual(2, Rows.Count);
            var researchCount = Rows[1].Content.MessageParams.OfType<TextNotificationParam>().Single(param => param.Name == "researchCount");
            Assert.AreEqual("6", researchCount.Value);
        }

        [Test]
        public void 獲得は数とアイテムを持つ獲得の型へ変換される()
        {
            _relay.Initialize();
            _localSource.Notify(NotificationMessagePack.CreateItemEarned(new ItemId(2), 5));

            Assert.That(Rows[0].Content, Is.TypeOf<EarnedNotificationContent>()
                .With.Property(nameof(EarnedNotificationContent.ItemId)).EqualTo(new ItemId(2))
                .And.Property(nameof(EarnedNotificationContent.Count)).EqualTo(5)
                .And.Property(nameof(NotificationContent.MessageId)).EqualTo("itemEarned.mined"));
        }

        [Test]
        public void 空のアイテムの実績はアイコン無しの型でアイテム付きの実績はアイテムのアイコンを持つ()
        {
            _relay.Initialize();
            _localSource.Notify(NotificationMessagePack.CreateAchievement("achievement.unlockedBlock", new Dictionary<string, string>()));
            _localSource.Notify(NotificationMessagePack.CreateAchievementWithItem("achievement.unlockedItem", new Dictionary<string, string>(), new ItemId(2)));

            Assert.IsInstanceOf<AchievementNotificationContent>(Rows[0].Content);
            Assert.IsInstanceOf<NoNotificationIcon>(Rows[0].Content.Icon);
            Assert.That(Rows[1].Content.Icon, Is.TypeOf<ItemNotificationIcon>().With.Property(nameof(ItemNotificationIcon.ItemId)).EqualTo(new ItemId(2)));
        }
    }
}
```

（`CapturingVanillaApiEvent` の名前空間は実物 `S/Client.Tests/Inventory/CapturingVanillaApiEvent.cs` の `namespace` に合わせる。`ClientLocalNotificationSource` の ctor は引数なし〈実物で確かめる〉）

- [ ] **Step 2: 変換を書く**（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/NotificationContentFactory.cs`）

```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Notification.Params;
using Core.Master;
using Server.Event.Notification;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// サーバーの通知パケットを種別ごとの中身の型へ変換する。空のアイテム ID は「アイコン無し」の型へ閉じる（Web の NotificationTopic と同じ写し方）
    /// Converts a server notification packet into the per-kind content type; an empty item id closes into the "no icon" type (same mapping as the Web NotificationTopic)
    /// </summary>
    public static class NotificationContentFactory
    {
        public static NotificationContent FromMessage(NotificationMessagePack message)
        {
            // params はサーバーが null を送り得る外部データなので空で受ける
            // params are external data the server may send as null, so accept them as empty
            IReadOnlyDictionary<string, string> wireParams = message.MessageParams ?? new Dictionary<string, string>();
            var messageParams = NotificationParams.FromWire(message.MessageId, wireParams);

            // 全カテゴリを列挙し、未知は止める（Feed が未定義値を先に落としているので到達しない）
            // List every category and stop on unknown (the feed already drops undefined values, so it is unreachable)
            return message.Category switch
            {
                NotificationCategory.ItemEarned => new EarnedNotificationContent(message.MessageId, messageParams, message.ItemId, message.Count),
                NotificationCategory.Achievement => new AchievementNotificationContent(message.MessageId, messageParams, IconOf(message.ItemId)),
                NotificationCategory.OperationDenied => new OperationDeniedNotificationContent(message.MessageId, messageParams, IconOf(message.ItemId)),
                NotificationCategory.SaveMigration => new SaveMigrationNotificationContent(message.MessageId, messageParams),
                _ => throw new ArgumentOutOfRangeException(nameof(message), message.Category, null),
            };

            #region Internal

            NotificationIcon IconOf(ItemId itemId)
            {
                return itemId == ItemMaster.EmptyItemId ? NoNotificationIcon.Instance : new ItemNotificationIcon(itemId);
            }

            #endregion
        }
    }
}
```

注: `message.MessageParams ?? …` は外部データ（MessagePack の受信）への null 対応で、AGENTS.md の null 規約の許す側。null で来たことは表示上の差を生まない（params の要る文言は Localize が `{name}` を残して LogError を出す＝開発者が気づく）。

- [ ] **Step 3: 中継を書く**（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/NotificationFeedRelay.cs`）

```csharp
using System;
using Server.Event.Notification;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Notification
{
    /// <summary>
    /// 通知の Feed を購読し、中身へ変換して書き口へ積む。購読前に来た除去告知は初期データから 1 回だけ積む
    /// Subscribes the notification feed and pushes converted contents through the write side; a prune notice that came before subscribing is pushed once from the initial data
    /// </summary>
    public sealed class NotificationFeedRelay : IInitializable, IDisposable
    {
        private readonly NotificationFeed _feed;
        private readonly INotificationListWriter _writer;
        private IDisposable _subscription;

        public NotificationFeedRelay(NotificationFeed feed, INotificationListWriter writer)
        {
            _feed = feed;
            _writer = writer;
        }

        public void Initialize()
        {
            // 初期データと購読を同じ同期区間で取るので、間に来た通知の取りこぼしも二重もない
            // Initial data and the subscription are taken in one synchronous span, so nothing is missed or doubled in between
            var initialNotice = _feed.SaveMigrationNotice;
            if (initialNotice != null) Push(initialNotice.Message);
            _subscription = _feed.OnNotified.Subscribe(notification => Push(notification.Message));
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void Push(NotificationMessagePack message)
        {
            _writer.AddNotification(NotificationContentFactory.FromMessage(message));
        }
    }
}
```

注: `initialNotice != null` は「告知がまだ来ていない」という正常な状態の分岐（初期データの有無）で、欠落の握り潰しではない。中継は寿命中に 1 回だけ購読するので、Web の snapshot 再着（同じ seq の再提示）は UITK に無い（D-3）。

- [ ] **Step 4: 行数を確かめる**

Run: `wc -l moorestech_client/Assets/Scripts/Client.Ui.State/Notification/*.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/*.cs` → 各 200 行未満。`ls moorestech_client/Assets/Scripts/Client.Ui.State/Notification/*.cs | wc -l` → 9

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Notification moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification
git commit -m "feat(uitk): 通知の Feed を種別の型へ変換して書き口へ積む中継を足す（除去告知の 1 回出し。S2-notification L0）"
```

---
### Task 3: 文言の解決（L0・Editor 不要）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Text/NotificationMessageKeys.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Text/INotificationTextLookup.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Text/LocalizeNotificationTextLookup.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Text/NotificationText.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/FakeNotificationTextLookup.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/NotificationMessageKeysTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/NotificationTextTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/NotificationTextLocalizeTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/LocalizeNotificationTextLookupTest.cs`

**Interfaces:**
- Consumes: `Mooresmaster.Localization.Generated.LocalizationKeys.Ui.Notification.<PascalCase>`・`LocalizationKeys.Ui.BlockInventory.InventoryOpenFailed`・`.DetailLoading`・`LocalizationKeys.Ui.Common.ItemFallback`（CSV `ui.common.itemFallback`＝「Item {itemId}」。生成名は Task 5 で実物に合わせる）、`ContentLocalizationKeys.ResearchName(Guid)`・`.ChallengeTitle(Guid)`・`.ItemName(Guid)`（同じ名前空間）、`Client.Localization.LocalizationKey`（`.Key`）・`ContentLocalizationKey`（`.Key`）・`LocalizationTextParam(string name, string value)`・`Localize.GetContent(ContentLocalizationKey)`（全言語に無ければ `[!<key>]` を返し自分ではログを出さない。Web の i18nStore も同じ印）・`Localize.GetFormatted(LocalizationKey, IReadOnlyList<LocalizationTextParam>)`、`Client.Ui.State.Text.UiText(LocalizationKey, IReadOnlyList<LocalizationTextParam>)`・`.Resolve()`、`MasterHolder.ItemMaster.ExistItemId(ItemId)`・`GetItemGuid(ItemId)`、テストの `UiTextAssert.AreEqual(LocalizationKey, string[], UiText)`（名前と値を順に比べる）、`ScreenTestHost`（英語に固定）、Task 1 の中身・引数
- Produces（Task 8・9 が使う）:
  - `static class NotificationMessageKeys { static LocalizationKey Resolve(string messageId); }`
  - `interface INotificationTextLookup { string ContentText(ContentLocalizationKey key); string ItemDisplayName(ItemId itemId); }`
  - `sealed class LocalizeNotificationTextLookup : INotificationTextLookup`（本番。Task 9 が DI に登録。欠けは `[LocalizeNotificationTextLookup] …` の LogError）
  - `static class NotificationText { static UiText Resolve(NotificationContent content, INotificationTextLookup lookup); static IReadOnlyList<LocalizationTextParam> ResolveParams(IReadOnlyList<NotificationParam> messageParams, INotificationTextLookup lookup); static List<LocalizationTextParam> BuildInterpolationValues(string messageId, IReadOnlyList<LocalizationTextParam> resolvedParams); }`
  - テスト用: `FakeNotificationTextLookup { int ContentTextCalls; int ItemNameCalls; void SetItemName(ItemId, string) }`

- [ ] **Step 1: テストの口を書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/FakeNotificationTextLookup.cs`）

```csharp
using System.Collections.Generic;
using Client.Localization;
using Client.Ui.State.Notification.Text;
using Core.Master;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// 内容名は "resolved:<キー>"、アイテム名は登録した名前か "item:<id>" を返し、呼ばれた回数を数える（Web テストの translate と resolveItemDisplayName）
    /// Returns "resolved:<key>" for content names and a registered name or "item:<id>" for items, counting calls (Web test translate and resolveItemDisplayName)
    /// </summary>
    public sealed class FakeNotificationTextLookup : INotificationTextLookup
    {
        private readonly Dictionary<ItemId, string> _itemNames = new();

        public int ContentTextCalls { get; private set; }
        public int ItemNameCalls { get; private set; }

        public void SetItemName(ItemId itemId, string name)
        {
            _itemNames[itemId] = name;
        }

        public string ContentText(ContentLocalizationKey key)
        {
            ContentTextCalls++;
            return $"resolved:{key.Key}";
        }

        public string ItemDisplayName(ItemId itemId)
        {
            ItemNameCalls++;
            return _itemNames.TryGetValue(itemId, out var name) ? name : $"item:{itemId.AsPrimitive()}";
        }
    }
}
```

- [ ] **Step 2: キーの表のテストを書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/NotificationMessageKeysTest.cs`）

```csharp
using Client.Ui.State.Notification.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// messageId から辞書キーへの表を確かめる（L0。Web の notificationMessages.test.ts の resolveNotificationKey）
    /// Verifies the messageId-to-key table (L0; Web notificationMessages.test.ts resolveNotificationKey)
    /// </summary>
    public class NotificationMessageKeysTest
    {
        /// <web-test file="src/features/notification/notificationMessages.test.ts">既知のmessageIdは型付きキーを返す</web-test>
        [Test]
        public void 既知のmessageIdは型付きキーを返す()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Notification.CraftMaterialShortage.Key, NotificationMessageKeys.Resolve("denied.craftMaterialShortage").Key);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">在庫の取得失敗と取得待ちを別の文言へ解決する</web-test>
        [Test]
        public void 在庫の取得失敗と取得待ちを別の文言へ解決する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.InventoryOpenFailed.Key, NotificationMessageKeys.Resolve("denied.inventoryOpenFailed").Key);
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.DetailLoading.Key, NotificationMessageKeys.Resolve("denied.inventoryDetailLoading").Key);
            Assert.AreEqual(LocalizationKeys.Ui.Notification.InventoryMoveSlotRejected.Key, NotificationMessageKeys.Resolve("denied.inventoryMoveSlotRejected").Key);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">未解決在庫の拒否は利用不可の文言へ解決する</web-test>
        [Test]
        public void 未解決在庫の拒否は利用不可の文言へ解決する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.InventoryOpenFailed.Key, NotificationMessageKeys.Resolve("denied.inventoryMoveUnresolved").Key);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">未知のmessageIdは専用キーで可視化する</web-test>
        [Test]
        public void 未知のmessageIdは専用キーで可視化する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Notification.UnknownMessage.Key, NotificationMessageKeys.Resolve("unknown.id").Key);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">獲得通知のmessageIdは表から到達できない</web-test>
        [Test]
        public void 獲得通知のmessageIdは表から到達できない()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Notification.UnknownMessage.Key, NotificationMessageKeys.Resolve("itemEarned.mined").Key);
        }
    }
}
```

- [ ] **Step 3: 文言の解決のテストを書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/NotificationTextTest.cs`）

```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Localization;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.Notification.Params;
using Client.Ui.State.Notification.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// 中身から UiText（キーと補間値）を作る規則を確かめる（L0。Web の notificationMessages.test.ts）
    /// Verifies how a content becomes a UiText key and interpolation values (L0; Web notificationMessages.test.ts)
    /// </summary>
    public class NotificationTextTest
    {
        private const string ContentGuid = "13C3D42F-BBBC-5EB4-8CD0-7B841EF53079";
        private FakeNotificationTextLookup _lookup;

        [SetUp]
        public void CreateLookup()
        {
            _lookup = new FakeNotificationTextLookup();
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">撤去Undoの取りこぼし件数を通知文言へ渡す</web-test>
        [Test]
        public void 撤去Undoの取りこぼし件数を通知文言へ渡す()
        {
            const string id = "denied.undoRestoreSkipped";
            var text = NotificationText.Resolve(Denied(id, Wire(id, new Dictionary<string, string> { ["count"] = "2" }), NoIcon), _lookup);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.Notification.UndoRestoreSkipped, new[] { "count", "2", "messageId", id }, text);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">messageIdとparamsを補間値へ変換する</web-test>
        [Test]
        public void MessageIdとparamsを補間値へ変換する()
        {
            var values = NotificationText.BuildInterpolationValues("known.id", new[] { new LocalizationTextParam("x", "a"), new LocalizationTextParam("y", "b") });
            CollectionAssert.AreEqual(new[] { "x=a", "y=b", "messageId=known.id" }, values.ConvertAll(value => $"{value.Name}={value.Value}"));
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">アイテム名とcountを補間するのは獲得通知だけ</web-test>
        [Test]
        public void アイテム名とcountを補間するのは獲得通知だけ()
        {
            const string deniedId = "denied.craftResultFull";
            UiTextAssert.AreEqual(LocalizationKeys.Ui.Notification.ItemEarned, new[] { "messageId", "itemEarned.mined", "itemName", "item:7", "count", "8" },
                NotificationText.Resolve(Earned(7, 8, "itemEarned.mined"), _lookup));
            UiTextAssert.AreEqual(LocalizationKeys.Ui.Notification.CraftResultFull, new[] { "x", "a", "messageId", deniedId },
                NotificationText.Resolve(Denied(deniedId, Wire(deniedId, new Dictionary<string, string> { ["x"] = "a" }), NoIcon), _lookup));
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">獲得通知はmessageIdに依らず獲得テンプレートを使う</web-test>
        [Test]
        public void 獲得通知はmessageIdに依らず獲得テンプレートを使う()
        {
            UiTextAssert.AreEqual(LocalizationKeys.Ui.Notification.ItemEarned, new[] { "messageId", "itemEarned.drifted", "itemName", "item:7", "count", "3" },
                NotificationText.Resolve(Earned(7, 3, "itemEarned.drifted"), _lookup));
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">獲得messageIdがmessage系categoryで来たら未知通知へ落とす</web-test>
        [Test]
        public void 獲得messageIdがmessage系categoryで来たら未知通知へ落とす()
        {
            var text = NotificationText.Resolve(Achievement("itemEarned.mined", NoParams, NoIcon), _lookup);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.Notification.UnknownMessage, new[] { "messageId", "itemEarned.mined" }, text);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">獲得通知以外はアイテム名を解決しない</web-test>
        [Test]
        public void 獲得通知以外はアイテム名を解決しない()
        {
            NotificationText.Resolve(Achievement("achievement.unlockedItem", NoParams, ItemIcon(7)), _lookup);
            Assert.AreEqual(0, _lookup.ItemNameCalls);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">マスタ欠損の除去通知は3つの件数を補間する</web-test>
        [Test]
        public void マスタ欠損の除去通知は3つの件数を補間する()
        {
            const string id = "saveMigration.missingMasterPruned";
            var wire = new Dictionary<string, string> { ["blockCount"] = "3", ["itemStackCount"] = "4", ["researchCount"] = "5" };
            UiTextAssert.AreEqual(LocalizationKeys.Ui.Notification.SaveMigrationMissingMasterPruned,
                new[] { "blockCount", "3", "itemStackCount", "4", "researchCount", "5", "messageId", id },
                NotificationText.Resolve(SaveMigration(id, Wire(id, wire)), _lookup));
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">Guidパラメータ通知はcontentキーで表示名へ解決する</web-test>
        [Test]
        public void Guidパラメータ通知はcontentキーで表示名へ解決する()
        {
            var lower = ContentGuid.ToLowerInvariant();
            Assert.AreEqual($"resolved:challenge.{lower}.title", ResolvedValue("achievement.challengeCompleted", "challenge", ContentGuid));
            Assert.AreEqual($"resolved:research.{lower}.name", ResolvedValue("achievement.researchCompleted", "research", ContentGuid));
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">Guidパラメータを持たない通知はparamsを素通しする</web-test>
        [Test]
        public void Guidパラメータを持たない通知はparamsを素通しする()
        {
            Assert.AreEqual("raw", ResolvedValue("denied.craftResultFull", "x", "raw"));
            Assert.AreEqual(0, _lookup.ContentTextCalls);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">Guidの名前が欠けた通知は警告してparamsを素通しする</web-test>
        [Test]
        public void Guidの名前が欠けた通知は警告してparamsを素通しする()
        {
            LogAssert.Expect(LogType.Warning, "[NotificationParams] achievement.researchCompleted: missing param research");
            Assert.AreEqual("g", ResolvedValue("achievement.researchCompleted", "wrong", "g"));
            Assert.AreEqual(0, _lookup.ContentTextCalls);
        }

        [Test]
        public void Guidとして読めない値は警告して文字の引数のまま出す()
        {
            const string id = "achievement.researchCompleted";
            LogAssert.Expect(LogType.Warning, $"[NotificationParams] {id}: param research is not a guid: not-a-guid");
            var messageParams = Wire(id, new Dictionary<string, string> { ["research"] = "not-a-guid" });
            Assert.IsInstanceOf<TextNotificationParam>(messageParams.Single());
            Assert.AreEqual("not-a-guid", NotificationText.ResolveParams(messageParams, _lookup).Single().Value);
        }

        // 受信の 1 引数を型付きにしてから解決し、その値を返す
        // Type one wire argument, resolve it and return its value
        private string ResolvedValue(string messageId, string name, string raw)
        {
            var messageParams = Wire(messageId, new Dictionary<string, string> { [name] = raw });
            return NotificationText.ResolveParams(messageParams, _lookup).Single(param => param.Name == name).Value;
        }
    }
}
```

- [ ] **Step 4: 欠けた値が枠のまま見えるテストを書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/NotificationTextLocalizeTest.cs`。辞書の実物で解くので英語に固定する `ScreenTestHost` を土台にする）

```csharp
using System.Collections.Generic;
using Client.Tests.Ui.Screens;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Notification.Text;
using Core.Master;
using NUnit.Framework;
using Server.Event.Notification;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// 補間値が欠けた通知は {name} の枠のまま見え、開発者向けのログが出ることを英語の辞書で確かめる（Web の it.each）
    /// Verifies with the English dictionary that a notification missing a value keeps its {name} token visible and logs for developers (Web it.each)
    /// </summary>
    public class NotificationTextLocalizeTest : ScreenTestHost
    {
        /// <web-test file="src/features/notification/notificationMessages.test.ts">missing values remain visible for %s</web-test>
        [TestCase("saveMigration.missingMasterPruned", NotificationCategory.SaveMigration, "ui.notification.saveMigrationMissingMasterPruned", "researchCount")]
        [TestCase("achievement.researchCompleted", NotificationCategory.Achievement, "ui.notification.researchCompleted", "research")]
        [TestCase("achievement.challengeCompleted", NotificationCategory.Achievement, "ui.notification.challengeCompleted", "challenge")]
        public void 欠けた補間値は枠のまま見える(string messageId, NotificationCategory category, string key, string name)
        {
            // 除去告知はほかの 2 つの件数を渡し、欠けるのを 1 つに絞る（ログの期待を 1 行にするため）
            // Give the prune notice its other two counts so exactly one value is missing (one expected log line)
            var isSaveMigration = category == NotificationCategory.SaveMigration;
            var wire = isSaveMigration
                ? new Dictionary<string, string> { ["blockCount"] = "1", ["itemStackCount"] = "2" }
                : new Dictionary<string, string>();
            if (!isSaveMigration) LogAssert.Expect(LogType.Warning, $"[NotificationParams] {messageId}: missing param {name}");
            LogAssert.Expect(LogType.Error, $"[Localize] {key}: no value for placeholder {{{name}}}");

            NotificationContent content = isSaveMigration ? SaveMigration(messageId, Wire(messageId, wire)) : Achievement(messageId, Wire(messageId, wire), NoIcon);
            var text = NotificationText.Resolve(content, new FakeNotificationTextLookup()).Resolve();
            StringAssert.Contains($"{{{name}}}", text);
        }

        [Test]
        public void 獲得の引数に同名のitemNameとcountが来ても実値で出てエラーにならない()
        {
            // C# の補間は同名の先勝ちで重複を LogError にする。Web は実値が後勝ち（notificationMessages.ts の { ...values, itemName, count }）
            // C# interpolation keeps the first of a duplicate and logs it; Web lets the real values win
            const string id = "itemEarned.mined";
            var lookup = new FakeNotificationTextLookup();
            lookup.SetItemName(new ItemId(2), "Stone");
            var content = new EarnedNotificationContent(id, Wire(id, new Dictionary<string, string> { ["itemName"] = "wrong", ["count"] = "999" }), new ItemId(2), 5);

            var text = NotificationText.Resolve(content, lookup).Resolve();
            StringAssert.Contains("Stone", text);
            StringAssert.Contains("5", text);
            StringAssert.DoesNotContain("wrong", text);
            StringAssert.DoesNotContain("999", text);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
```

- [ ] **Step 5: キーの表を書く**（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Text/NotificationMessageKeys.cs`。Web `W/features/notification/notificationMessages.ts` の `notificationKeys` 70 行を同じ順で写す）

```csharp
using System.Collections.Generic;
using Client.Localization;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Notification.Text
{
    /// <summary>
    /// サーバーの messageId を有限の辞書キーへ閉じる表。未知の id も専用の文言で見せる。獲得はカテゴリでキーが決まるので載せない（Web notificationKeys）
    /// Closes server messageIds into finite dictionary keys and shows unknown ids through a dedicated text; earns take their key from the category and are absent (Web notificationKeys)
    /// </summary>
    public static class NotificationMessageKeys
    {
        private static readonly Dictionary<string, LocalizationKey> Keys = new()
        {
            ["denied.undoRestoreSkipped"] = LocalizationKeys.Ui.Notification.UndoRestoreSkipped,
            ["achievement.researchCompleted"] = LocalizationKeys.Ui.Notification.ResearchCompleted,
            ["achievement.challengeCompleted"] = LocalizationKeys.Ui.Notification.ChallengeCompleted,
            ["achievement.unlockedItem"] = LocalizationKeys.Ui.Notification.UnlockedItem,
            ["achievement.unlockedCraftRecipe"] = LocalizationKeys.Ui.Notification.UnlockedCraftRecipe,
            ["achievement.unlockedMachineRecipe"] = LocalizationKeys.Ui.Notification.UnlockedMachineRecipe,
            ["achievement.unlockedBlock"] = LocalizationKeys.Ui.Notification.UnlockedBlock,
            ["achievement.unlockedTrainCar"] = LocalizationKeys.Ui.Notification.UnlockedTrainCar,
            ["achievement.unlockedConnectTool"] = LocalizationKeys.Ui.Notification.UnlockedConnectTool,
            ["achievement.unlockedBlueprint"] = LocalizationKeys.Ui.Notification.UnlockedBlueprint,
            ["denied.researchNotCompletable"] = LocalizationKeys.Ui.Notification.ResearchNotCompletable,
            ["denied.craftResultFull"] = LocalizationKeys.Ui.Notification.CraftResultFull,
            ["denied.craftMaterialShortage"] = LocalizationKeys.Ui.Notification.CraftMaterialShortage,
            ["denied.deleteUnconfirmed"] = LocalizationKeys.Ui.Notification.DeleteUnconfirmed,
            ["denied.deleteRejected"] = LocalizationKeys.Ui.Notification.DeleteRejected,
            ["denied.removeTrainCarInventoryFull"] = LocalizationKeys.Ui.Notification.RemoveTrainCarInventoryFull,
            ["denied.inventoryMoveUnresolved"] = LocalizationKeys.Ui.BlockInventory.InventoryOpenFailed,
            ["denied.inventoryOpenFailed"] = LocalizationKeys.Ui.BlockInventory.InventoryOpenFailed,
            ["denied.inventoryDetailLoading"] = LocalizationKeys.Ui.BlockInventory.DetailLoading,
            ["denied.inventoryMoveSlotRejected"] = LocalizationKeys.Ui.Notification.InventoryMoveSlotRejected,
            ["denied.inventoryMovePartialSwap"] = LocalizationKeys.Ui.Notification.InventoryMovePartialSwap,
            ["denied.miningInventoryFull"] = LocalizationKeys.Ui.Notification.MiningInventoryFull,
            ["denied.placeBlockNotUnlocked"] = LocalizationKeys.Ui.Notification.PlaceBlockNotUnlocked,
            ["denied.placeBlockCostShortage"] = LocalizationKeys.Ui.Notification.PlaceBlockCostShortage,
            ["denied.placeBlockWireShortage"] = LocalizationKeys.Ui.Notification.PlaceBlockWireShortage,
            ["denied.railEdit.InvalidNode"] = LocalizationKeys.Ui.Notification.RailEditInvalidNode,
            ["denied.railEdit.NodeInUseByTrain"] = LocalizationKeys.Ui.Notification.RailEditNodeInUseByTrain,
            ["denied.railEdit.StationInternalEdge"] = LocalizationKeys.Ui.Notification.RailEditStationInternalEdge,
            ["denied.railEdit.InvalidMode"] = LocalizationKeys.Ui.Notification.RailEditInvalidMode,
            ["denied.railEdit.NotEnoughRailItem"] = LocalizationKeys.Ui.Notification.RailEditNotEnoughRailItem,
            ["denied.railEdit.NotEnoughInventorySpace"] = LocalizationKeys.Ui.Notification.RailEditNotEnoughInventorySpace,
            ["denied.railEdit.RailLengthExceeded"] = LocalizationKeys.Ui.Notification.RailEditRailLengthExceeded,
            ["denied.railEdit.NotUnlocked"] = LocalizationKeys.Ui.Notification.RailEditNotUnlocked,
            ["denied.railEdit.UnknownError"] = LocalizationKeys.Ui.Notification.RailEditUnknownError,
            ["denied.electricWireExtend.OutOfRange"] = LocalizationKeys.Ui.Notification.ElectricWireExtendOutOfRange,
            ["denied.electricWireExtend.AlreadyConnected"] = LocalizationKeys.Ui.Notification.ElectricWireExtendAlreadyConnected,
            ["denied.electricWireExtend.ConnectionLimit"] = LocalizationKeys.Ui.Notification.ElectricWireExtendConnectionLimit,
            ["denied.electricWireExtend.NoWireItem"] = LocalizationKeys.Ui.Notification.ElectricWireExtendNoWireItem,
            ["denied.electricWireExtend.NoPoleItem"] = LocalizationKeys.Ui.Notification.ElectricWireExtendNoPoleItem,
            ["denied.electricWireExtend.InvalidTarget"] = LocalizationKeys.Ui.Notification.ElectricWireExtendInvalidTarget,
            ["denied.electricWireExtend.PositionOccupied"] = LocalizationKeys.Ui.Notification.ElectricWireExtendPositionOccupied,
            ["denied.electricWireExtend.NotUnlocked"] = LocalizationKeys.Ui.Notification.ElectricWireExtendNotUnlocked,
            ["denied.electricWireExtend.InsufficientItems"] = LocalizationKeys.Ui.Notification.ElectricWireExtendInsufficientItems,
            ["denied.electricWireExtend.InvalidMode"] = LocalizationKeys.Ui.Notification.ElectricWireExtendFailed,
            ["denied.electricWireExtend.None"] = LocalizationKeys.Ui.Notification.ElectricWireExtendFailed,
            ["denied.electricWireDisconnect.NotConnected"] = LocalizationKeys.Ui.Notification.ElectricWireDisconnectNotConnected,
            ["denied.electricWireDisconnect.InventoryFull"] = LocalizationKeys.Ui.Notification.ElectricWireDisconnectInventoryFull,
            ["denied.electricWireDisconnect.InvalidTarget"] = LocalizationKeys.Ui.Notification.ElectricWireDisconnectFailed,
            ["denied.gearChainDisconnect.NotConnected"] = LocalizationKeys.Ui.Notification.GearChainDisconnectNotConnected,
            ["denied.gearChainDisconnect.InventoryFull"] = LocalizationKeys.Ui.Notification.GearChainDisconnectInventoryFull,
            ["denied.gearChainDisconnect.InvalidTarget"] = LocalizationKeys.Ui.Notification.GearChainDisconnectFailed,
            ["denied.gearChainConnect.InvalidTarget"] = LocalizationKeys.Ui.Notification.GearChainConnectInvalidTarget,
            ["denied.gearChainConnect.NotUnlocked"] = LocalizationKeys.Ui.Notification.GearChainConnectNotUnlocked,
            ["denied.gearChainConnect.TooFar"] = LocalizationKeys.Ui.Notification.GearChainConnectTooFar,
            ["denied.gearChainConnect.AlreadyConnected"] = LocalizationKeys.Ui.Notification.GearChainConnectAlreadyConnected,
            ["denied.gearChainConnect.ConnectionLimit"] = LocalizationKeys.Ui.Notification.GearChainConnectConnectionLimit,
            ["denied.gearChainConnect.NoItem"] = LocalizationKeys.Ui.Notification.GearChainConnectNoItem,
            ["denied.blueprint.NotUnlocked"] = LocalizationKeys.Ui.Notification.BlueprintNotUnlocked,
            ["denied.blueprint.PasteCostShortage"] = LocalizationKeys.Ui.Notification.BlueprintPasteCostShortage,
            ["denied.blueprint.PasteNotUnlocked"] = LocalizationKeys.Ui.Notification.BlueprintPasteNotUnlocked,
            ["denied.blueprint.PasteLineFailed"] = LocalizationKeys.Ui.Notification.BlueprintPasteLineFailed,
            ["denied.blueprint.NotFound"] = LocalizationKeys.Ui.Notification.BlueprintNotFound,
            ["denied.blueprint.InvalidRequest"] = LocalizationKeys.Ui.Notification.BlueprintPasteInvalidRequest,
            ["denied.blueprint.PastePlacementFailed"] = LocalizationKeys.Ui.Notification.BlueprintPastePlacementFailed,
            ["denied.blueprintCreate.RequestFailed"] = LocalizationKeys.Ui.Notification.BlueprintCreateRequestFailed,
            ["denied.blueprintCreate.InvalidName"] = LocalizationKeys.Ui.Notification.BlueprintCreateInvalidName,
            ["denied.blueprintCreate.EmptyArea"] = LocalizationKeys.Ui.Notification.BlueprintCreateEmptyArea,
            ["denied.blueprintCreate.InvalidRequest"] = LocalizationKeys.Ui.Notification.BlueprintCreateInvalidRequest,
            ["denied.blueprintCreate.Unknown"] = LocalizationKeys.Ui.Notification.BlueprintCreateUnknown,
            ["saveMigration.missingMasterPruned"] = LocalizationKeys.Ui.Notification.SaveMigrationMissingMasterPruned,
        };

        // 未知の id は「不明な通知: <id>」として画面に出る（表の追加漏れはプレイヤーにも開発者にも見え、R4 の網羅テストが落ちる）
        // An unknown id shows as "Unknown notification: <id>" (a missing row is visible to players and developers, and the R4 coverage test fails)
        public static LocalizationKey Resolve(string messageId)
        {
            return Keys.TryGetValue(messageId, out var key) ? key : LocalizationKeys.Ui.Notification.UnknownMessage;
        }
    }
}
```

- [ ] **Step 6: 引く口と本番の口を書く**（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Text/INotificationTextLookup.cs`）

```csharp
using Client.Localization;
using Core.Master;

namespace Client.Ui.State.Notification.Text
{
    /// <summary>
    /// 通知の文で使う内容名（研究名・チャレンジ題）とアイテム名を、いまの言語で引く口
    /// Looks up content names (research names, challenge titles) and item names in the current language for notification text
    /// </summary>
    public interface INotificationTextLookup
    {
        string ContentText(ContentLocalizationKey key);
        string ItemDisplayName(ItemId itemId);
    }
}
```

（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Text/LocalizeNotificationTextLookup.cs`）

```csharp
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Notification.Text
{
    /// <summary>
    /// 本番の口。コンテンツ辞書から引き、アイテム名はマスタの Guid からキーを作る。欠けは理由つきで LogError を出し、Web と同じ見え方で出す
    /// Production lookup; reads the content dictionary and builds item-name keys from the master Guid; gaps log an error with the reason and show the same as Web
    /// </summary>
    public sealed class LocalizeNotificationTextLookup : INotificationTextLookup
    {
        public string ContentText(ContentLocalizationKey key)
        {
            // 全言語に無い内容名は Localize が欠けの印を返す。Web も同じ印を出すのでそのまま出し、理由を開発者に残す
            // Localize returns the missing marker for a content name absent in every language; Web shows the same marker, so keep it and leave the reason for developers
            var text = Localize.GetContent(key);
            if (IsMissingMarker(text, key)) Debug.LogError($"[LocalizeNotificationTextLookup] content text missing in every language: {key.Key}");
            return text;
        }

        public string ItemDisplayName(ItemId itemId)
        {
            // マスタに無いアイテムは Guid を引けない。Web の useItemDisplayName と同じく「Item {itemId}」で出す
            // An item absent from the master has no Guid; show "Item {itemId}" like the Web useItemDisplayName
            if (!MasterHolder.ItemMaster.ExistItemId(itemId))
            {
                Debug.LogError($"[LocalizeNotificationTextLookup] item {itemId.AsPrimitive()} is not in the item master; showing the fallback name");
                return FallbackName(itemId);
            }

            var key = ContentLocalizationKeys.ItemName(MasterHolder.ItemMaster.GetItemGuid(itemId));
            var name = Localize.GetContent(key);
            if (!IsMissingMarker(name, key)) return name;
            Debug.LogError($"[LocalizeNotificationTextLookup] item {itemId.AsPrimitive()} has no name in every language ({key.Key}); showing the fallback name");
            return FallbackName(itemId);
        }

        // 欠けの印は LocalizationTextResolver の「[!<key>]」（Web の i18nStore も同じ形）
        // The missing marker is LocalizationTextResolver's "[!<key>]" (same shape as the Web i18nStore)
        private static bool IsMissingMarker(string text, ContentLocalizationKey key)
        {
            return text == $"[!{key.Key}]";
        }

        private static string FallbackName(ItemId itemId)
        {
            var itemIdText = itemId.AsPrimitive().ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Localize.GetFormatted(LocalizationKeys.Ui.Common.ItemFallback, new[] { new LocalizationTextParam("itemId", itemIdText) });
        }
    }
}
```

（欠けの印を返す公開の口が `Client.Localization` にあれば、`IsMissingMarker` の文字の組み立てをそれに置き換える。アイテム名の口は Task 7 で C3-a の `GameContentPresentation.ItemName(ItemId)` と突き合わせる。`ItemName` が `UiText.FromContent(ContentLocalizationKeys.ItemName(guid))` を返す実物なら、同じキーを引いているので重複ではなく、通知の文が引数に解決済みの文字列を要るための口として残す。散らばりは「本PR外のリファクタ提案」へ。D-4）

（`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/LocalizeNotificationTextLookupTest.cs`）

```csharp
using System;
using Client.Tests.Ui.Screens;
using Client.Ui.State.Notification.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// 本番の口が辞書やマスタの欠けで理由つきの LogError を出し、Web と同じ見え方で返すことを英語の辞書で確かめる（UITK だけ）
    /// Verifies with the English dictionary that the production lookup logs a reasoned error on dictionary or master gaps and returns the Web look (UITK-only)
    /// </summary>
    public class LocalizeNotificationTextLookupTest : ScreenTestHost
    {
        [Test]
        public void 辞書に無い内容名は理由つきのエラーを出し欠けの印のまま見える()
        {
            var key = ContentLocalizationKeys.ResearchName(Guid.Empty);
            LogAssert.Expect(LogType.Error, $"[LocalizeNotificationTextLookup] content text missing in every language: {key.Key}");
            Assert.AreEqual($"[!{key.Key}]", new LocalizeNotificationTextLookup().ContentText(key));
        }

        [Test]
        public void マスタに無いアイテムは理由つきのエラーを出しWebと同じ代わりの名前で見える()
        {
            const int missingItemId = 987654;
            LogAssert.Expect(LogType.Error, $"[LocalizeNotificationTextLookup] item {missingItemId} is not in the item master; showing the fallback name");
            Assert.AreEqual($"Item {missingItemId}", new LocalizeNotificationTextLookup().ItemDisplayName(new ItemId(missingItemId)));
        }
    }
}
```

（2 件目は `MasterHolder.ItemMaster` が読めている前提。Task 5 で `ScreenTestHost` の下で `MasterHolder.ItemMaster` が null なら、client テストで既にマスタを読んでいる土台を `rg -n "MasterHolder.Load" moorestech_client/Assets/Scripts/Client.Tests` で探し、その SetUp を呼ぶ形にする。新しい読み込みの土台は作らない）

- [ ] **Step 7: 文言の解決を書く**（`moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Text/NotificationText.cs`）

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using Client.Localization;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Notification.Params;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Notification.Text
{
    /// <summary>
    /// 中身から表示の UiText を作る。キーと補間値を同じ分岐で決め、アイテム名と数を持つのは獲得だけという約束を 1 か所に閉じる（Web resolveNotificationText）
    /// Builds the display UiText from a content; the key and values come from one branch, closing the "only earns carry an item name and count" contract in one place (Web resolveNotificationText)
    /// </summary>
    public static class NotificationText
    {
        public static UiText Resolve(NotificationContent content, INotificationTextLookup lookup)
        {
            var values = BuildInterpolationValues(content.MessageId, ResolveParams(content.MessageParams, lookup));

            // 獲得は種別でキーを決め、messageId のずれで生のトークンが出るのを防ぐ。ほかは表で引く
            // An earn fixes its key by kind so a drifting messageId cannot leak raw tokens; others use the table
            if (content is not EarnedNotificationContent earned) return new UiText(NotificationMessageKeys.Resolve(content.MessageId), values);

            // Web は { ...values, itemName, count } で実値が後勝ち、C# の補間は先勝ちで重複を LogError にするので、同名の引数を除いてから実値を足す
            // Web lets the real values win via { ...values, itemName, count }; C# interpolation keeps the first and logs duplicates, so drop same-named params before adding the real values
            values.RemoveAll(param => param.Name == "itemName" || param.Name == "count");
            values.Add(new LocalizationTextParam("itemName", lookup.ItemDisplayName(earned.ItemId)));
            values.Add(new LocalizationTextParam("count", earned.Count.ToString(CultureInfo.InvariantCulture)));
            return new UiText(LocalizationKeys.Ui.Notification.ItemEarned, values);
        }

        public static IReadOnlyList<LocalizationTextParam> ResolveParams(IReadOnlyList<NotificationParam> messageParams, INotificationTextLookup lookup)
        {
            var resolved = new List<LocalizationTextParam>(messageParams.Count);
            foreach (var param in messageParams) resolved.Add(new LocalizationTextParam(param.Name, ValueOf(param)));
            return resolved;

            #region Internal

            // 内容名の引数だけ、いまの言語の表示名を引く。サーバーは表示名でなく Guid を送る
            // Only content-name arguments look up the display name in the current language; the server sends Guids, not names
            string ValueOf(NotificationParam param)
            {
                return param switch
                {
                    TextNotificationParam text => text.Value,
                    ContentNameNotificationParam contentName => lookup.ContentText(contentName.Key),
                    _ => throw new ArgumentOutOfRangeException(nameof(param), param.GetType().Name, null),
                };
            }

            #endregion
        }

        // params を並べた後に messageId を足す。params に同名があれば messageId が勝つ（Web の { ...params, messageId }）
        // List the params, then messageId; a same-named param loses to messageId (Web { ...params, messageId })
        public static List<LocalizationTextParam> BuildInterpolationValues(string messageId, IReadOnlyList<LocalizationTextParam> resolvedParams)
        {
            var values = new List<LocalizationTextParam>(resolvedParams.Count + 1);
            foreach (var param in resolvedParams)
            {
                if (param.Name != "messageId") values.Add(param);
            }
            values.Add(new LocalizationTextParam("messageId", messageId));
            return values;
        }
    }
}
```

- [ ] **Step 8: 行数を確かめる**

Run: `wc -l moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Text/*.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/*.cs` → 各 200 行未満。`grep -rn "(EarnedNotificationContent)\|(ItemNotificationIcon)" moorestech_client/Assets/Scripts/Client.Ui.State/Notification moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification` → 0 件（型の判定は型パターンで書く）

- [ ] **Step 9: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Notification/Text moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification
git commit -m "feat(uitk): 通知の messageId→キーの表と UiText への解決を Web の notificationMessages から写す（欠けは LogError。S2-notification L0）"
```

---

### Task 4: サーバー通知 id の表網羅（S 種別の写し・Editor 不要）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/Coverage/InterpolatedNotificationIdEnums.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/Coverage/NotificationServerIdScanner.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/Coverage/NotificationServerIdCoverageTest.cs`

**Interfaces:**
- Consumes: Task 3 の `NotificationMessageKeys.Resolve`、`UnityEngine.Application.dataPath`（`<repo>/moorestech_client/Assets`）
- Produces: なし（テストだけ）

Web の走査（`W/features/notification/notificationServerIdCoverage.test.ts`）を正規表現ごと写す。サーバーは `moorestech_server/Assets/Scripts/` の全 `.cs`（パスのどこかに `Tests` で始まる区切りがあるものは除く）、クライアントは `moorestech_client/Assets/Scripts/` のうち `Client.Tests` を区切りに持たず、`NotificationMessagePack.Create` を含むか表の enum を宣言するファイルだけ。

- [ ] **Step 1: 補間 id の表を書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/Coverage/InterpolatedNotificationIdEnums.cs`）

```csharp
using System.Collections.Generic;

namespace Client.Tests.Ui.State.Notification.Coverage
{
    /// <summary>
    /// 補間で作られる通知 id の接頭辞と、展開する enum・その経路では送られない値（Web の interpolatedIdEnums と同じ表）
    /// Prefixes of interpolated notification ids with the enum to expand and the values that path never sends (same table as Web interpolatedIdEnums)
    /// </summary>
    internal static class InterpolatedNotificationIdEnums
    {
        public static readonly IReadOnlyDictionary<string, InterpolatedIdEnum> ByPrefix = new Dictionary<string, InterpolatedIdEnum>
        {
            ["denied.blueprint."] = new("BlueprintFailureReason", "None", "InvalidName", "EmptyArea", "UnknownOperation"),
            ["denied.blueprintCreate."] = new("BlueprintCreateFailure", "None", "NotUnlocked"),
            ["denied.railEdit."] = new("RailConnectionEditFailureReason", "None"),
            ["denied.electricWireExtend."] = new("ElectricWirePlacementFailureReason", "InventoryFull", "NotConnected"),
            ["denied.electricWireDisconnect."] = new("ElectricWirePlacementFailureReason", "None", "OutOfRange", "AlreadyConnected", "ConnectionLimit", "NoWireItem", "NoPoleItem", "PositionOccupied", "InvalidMode", "NotUnlocked", "InsufficientItems"),
            ["denied.gearChainDisconnect."] = new("GearChainDisconnectFailureReason", "None"),
            ["denied.gearChainConnect."] = new("GearChainPlacementFailureReason", "None", "NoPoleItem", "PositionOccupied", "InsufficientItems"),
        };

        internal sealed class InterpolatedIdEnum
        {
            public readonly string EnumName;
            public readonly IReadOnlyList<string> NotSentMembers;

            public InterpolatedIdEnum(string enumName, params string[] notSentMembers)
            {
                EnumName = enumName;
                NotSentMembers = notSentMembers;
            }
        }
    }
}
```

（Web の表が変わったら同じ PR で両方を直す。Web の `notificationServerIdCoverage.test.ts` は Web が生きている間は残る）

- [ ] **Step 2: 走査を書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/Coverage/NotificationServerIdScanner.cs`）

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Client.Tests.Ui.State.Notification.Coverage
{
    /// <summary>
    /// 通知生成 API の第 1 引数を送信元の .cs から拾い、通知 id へ展開する。分類できない引数は Unclassified へ積む（無音で対象外にしない）
    /// Collects first arguments of the notification factories from sender .cs files and expands them into ids; unclassifiable arguments go to Unclassified (never silently skipped)
    /// </summary>
    internal static class NotificationServerIdScanner
    {
        private static readonly Regex FactoryCallFirstArgument = new(@"NotificationMessagePack\.Create(?:Achievement|AchievementWithItem|OperationDenied)\(\s*(\$?""[^""]*"")");
        private static readonly Regex FactoryInternalLiteral = new(@"""((?:achievement|denied|saveMigration)\.[^""]+)""");
        private static readonly Regex PlainLiteral = new(@"^""([^""{}]+)""$");
        private static readonly Regex FixedMemberInterpolation = new(@"^\$""([^""{}]+)\{(\w+)\.(\w+)\}""$");
        private static readonly Regex ReasonInterpolation = new(@"^\$""([^""{}]+)\{[^""{}]+\}""$");

        public static Dictionary<string, string> ReadSenderSources()
        {
            // repo の根は moorestech_client/Assets の 2 つ上
            // The repo root is two levels above moorestech_client/Assets
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var serverDir = Path.Combine(repoRoot, "moorestech_server", "Assets", "Scripts");
            var clientDir = Path.Combine(repoRoot, "moorestech_client", "Assets", "Scripts");
            var sources = new Dictionary<string, string>();

            foreach (var path in Directory.EnumerateFiles(serverDir, "*.cs", SearchOption.AllDirectories))
            {
                if (SegmentsOf(serverDir, path).Any(segment => segment.StartsWith("Tests"))) continue;
                sources[path] = File.ReadAllText(path);
            }

            // クライアント発の通知 id（Undo の取りこぼし等）も送信元。生成 API を呼ぶか表の enum を持つファイルだけ読む
            // Client-originated ids (e.g. undo restore skips) are senders too; read only files that call the factories or declare a table enum
            var enumNames = InterpolatedNotificationIdEnums.ByPrefix.Values.Select(entry => entry.EnumName).ToArray();
            foreach (var path in Directory.EnumerateFiles(clientDir, "*.cs", SearchOption.AllDirectories))
            {
                if (SegmentsOf(clientDir, path).Contains("Client.Tests")) continue;
                var source = File.ReadAllText(path);
                if (source.Contains("NotificationMessagePack.Create") || enumNames.Any(name => source.Contains($"enum {name}"))) sources[$"client:{path}"] = source;
            }
            return sources;

            #region Internal

            string[] SegmentsOf(string root, string path)
            {
                return Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            #endregion
        }

        public static List<string> FindEnumMembers(Dictionary<string, string> sources, string enumName)
        {
            var enumBody = new Regex($@"enum\s+{enumName}\s*\{{([^}}]*)\}}");
            foreach (var source in sources.Values)
            {
                var match = enumBody.Match(source);
                if (match.Success) return match.Groups[1].Value.Split(',').Select(member => member.Split('=')[0].Trim()).Where(member => member.Length > 0).ToList();
            }
            return null;
        }

        public static ScanResult Collect(Dictionary<string, string> sources)
        {
            var ids = new SortedSet<string>(System.StringComparer.Ordinal);
            var unclassified = new List<string>();
            foreach (var (file, source) in sources.Select(pair => (pair.Key, pair.Value)))
            {
                foreach (Match call in FactoryCallFirstArgument.Matches(source)) Classify(file, call.Groups[1].Value);

                // 生成クラスの中に直書きされた id（除去告知）も送信 id に含める
                // Ids hard-coded inside the factory class (the prune notice) are sent ids too
                if (file.EndsWith("NotificationMessagePack.cs"))
                {
                    foreach (Match literal in FactoryInternalLiteral.Matches(source)) ids.Add(literal.Groups[1].Value);
                }
            }
            return new ScanResult(ids.ToList(), unclassified);

            #region Internal

            void Classify(string file, string argument)
            {
                // 登録済み接頭辞の補間は理由値として enum を展開し、未登録で {Enum.Member} 形なら固定値として扱う
                // A registered prefix is a reason value expanded over its enum; otherwise a {Enum.Member} form is one fixed value
                var literal = PlainLiteral.Match(argument);
                var fixedMember = FixedMemberInterpolation.Match(argument);
                var reason = ReasonInterpolation.Match(argument);
                if (literal.Success) ids.Add(literal.Groups[1].Value);
                else if (reason.Success && InterpolatedNotificationIdEnums.ByPrefix.TryGetValue(reason.Groups[1].Value, out var entry))
                {
                    var members = FindEnumMembers(sources, entry.EnumName);
                    if (members == null) unclassified.Add($"{file}: {argument} (enum {entry.EnumName} not found)");
                    else foreach (var member in members.Where(member => !entry.NotSentMembers.Contains(member))) ids.Add(reason.Groups[1].Value + member);
                }
                else if (fixedMember.Success && (FindEnumMembers(sources, fixedMember.Groups[2].Value)?.Contains(fixedMember.Groups[3].Value) ?? false)) ids.Add(fixedMember.Groups[1].Value + fixedMember.Groups[3].Value);
                else unclassified.Add($"{file}: {argument}");
            }

            #endregion
        }

        internal sealed class ScanResult
        {
            public readonly IReadOnlyList<string> Ids;
            public readonly IReadOnlyList<string> Unclassified;

            public ScanResult(IReadOnlyList<string> ids, IReadOnlyList<string> unclassified)
            {
                Ids = ids;
                Unclassified = unclassified;
            }
        }
    }
}
```

注: Web は enum が見つからないとき例外で落とす（`readEnumMembers`）。ここでは `Unclassified` へ理由付きで積み、「分類できる形」のテストが落ちる＝同じく気づける。`?.Contains(...) ?? false` は「その enum が送信元に無い」形を分類不能へ回すための分岐で、Web の `findEnumMembers(...)?.includes(...)` と同じ。

- [ ] **Step 3: テストを書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/Coverage/NotificationServerIdCoverageTest.cs`）

```csharp
using System.Linq;
using Client.Ui.State.Notification.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Notification.Coverage
{
    /// <summary>
    /// サーバー・クライアントが送る通知 id を走査で全部拾い、C# の表が未知以外へ解決することを確かめる（S。Web の notificationServerIdCoverage.test.ts）
    /// Scans every notification id the server and client send and checks the C# table resolves each to a known key (S; Web notificationServerIdCoverage.test.ts)
    /// </summary>
    public class NotificationServerIdCoverageTest
    {
        private static readonly string[] KnownIdsOfEachForm =
        {
            "denied.miningInventoryFull", "denied.inventoryMoveUnresolved", "achievement.unlockedItem", "saveMigration.missingMasterPruned",
            "denied.blueprint.NotUnlocked", "denied.blueprint.InvalidRequest", "denied.blueprint.NotFound", "denied.blueprint.PasteCostShortage",
            "denied.blueprint.PasteNotUnlocked", "denied.blueprint.PasteLineFailed", "denied.blueprint.PastePlacementFailed", "denied.railEdit.InvalidNode",
            "denied.electricWireDisconnect.InventoryFull", "denied.gearChainDisconnect.InventoryFull", "denied.gearChainConnect.NoItem", "denied.undoRestoreSkipped",
        };

        private System.Collections.Generic.Dictionary<string, string> _sources;
        private NotificationServerIdScanner.ScanResult _scan;

        [OneTimeSetUp]
        public void Scan()
        {
            _sources = NotificationServerIdScanner.ReadSenderSources();
            _scan = NotificationServerIdScanner.Collect(_sources);
        }

        /// <web-test file="src/features/notification/notificationServerIdCoverage.test.ts">走査が送信元の各形を拾えている</web-test>
        [Test]
        public void 走査が送信元の各形を拾えている()
        {
            // 走査が壊れて 0 件になると網羅検査が空振りで緑になるため、各形の既知 id で走査自体を確かめる
            // A broken scan would yield no ids and pass vacuously, so known ids of each form prove the scan works
            foreach (var id in KnownIdsOfEachForm) CollectionAssert.Contains(_scan.Ids, id);
        }

        /// <web-test file="src/features/notification/notificationServerIdCoverage.test.ts">通知生成APIの引数は全て分類できる形である</web-test>
        [Test]
        public void 通知生成APIの引数は全て分類できる形である()
        {
            CollectionAssert.IsEmpty(_scan.Unclassified);
        }

        /// <web-test file="src/features/notification/notificationServerIdCoverage.test.ts">除外指定したenum値は実在する</web-test>
        [Test]
        public void 除外指定したenum値は実在する()
        {
            // enum 値の改名で除外が空振りし、送られる値を黙って対象外にしないようにする
            // Guard against a renamed enum value leaving a stale exclusion that silently skips a sent value
            foreach (var entry in InterpolatedNotificationIdEnums.ByPrefix.Values)
            {
                var members = NotificationServerIdScanner.FindEnumMembers(_sources, entry.EnumName);
                Assert.IsNotNull(members, entry.EnumName);
                foreach (var member in entry.NotSentMembers) CollectionAssert.Contains(members, member, entry.EnumName);
            }
        }

        /// <web-test file="src/features/notification/notificationServerIdCoverage.test.ts">サーバーが送る通知idは全て不明通知以外のキーへ解決する</web-test>
        [Test]
        public void サーバーが送る通知idは全て不明通知以外のキーへ解決する()
        {
            var unknown = LocalizationKeys.Ui.Notification.UnknownMessage.Key;
            CollectionAssert.IsEmpty(_scan.Ids.Where(id => NotificationMessageKeys.Resolve(id).Key == unknown).ToList());
        }

        /// <web-test file="src/features/notification/notificationServerIdCoverage.test.ts">採掘で満杯により失った通知は専用文言を使う</web-test>
        [Test]
        public void 採掘で満杯により失った通知は専用文言を使う()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Notification.MiningInventoryFull.Key, NotificationMessageKeys.Resolve("denied.miningInventoryFull").Key);
        }
    }
}
```

- [ ] **Step 4: 走査の正規表現を Unity 無しで当てる**（C# の `Regex` と JS の正規表現は同じ書き方の範囲で写した。Unity を待たずに、Web の vitest がいま緑であることで表と走査の前提が生きていることを見る）

Run: `cd moorestech_web/webui && npx vitest run src/features/notification/notificationServerIdCoverage.test.ts`
Expected: 5 passed（`node_modules` が無ければ `npm ci` を先に回す。緑でなければ Web の表か送信元が先に壊れている。C# の表を写す前に原因を done.md に書いて指揮へ返す）

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification/Coverage
git commit -m "test(uitk): サーバー通知 id の表網羅を C# の表に対して写す（Web notificationServerIdCoverage の S 種別。S2-notification L0）"
```

---

### Task 5: L0 の末 — Editor 枠を 1 回借りて compile と L0 テスト

**Files:**
- なし（直すものが出たら Task 1〜4 のファイル）

- [ ] **Step 1: Editor 枠を借りて起動する**

Run: `moores-wt status`
Expected: Editor 本数が予算内（実装用 2〜3 本まで）。空いていれば uloop-launch スキルで自分の worktree の `moorestech_client` を起動する（他者の Editor は落とさない。空かなければ空くまで待つ）

- [ ] **Step 2: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0。`LocalizationKeys.Ui.Notification.*` の生成名が表と違えば、生成物（`Mooresmaster.Localization.Generated`）の実名に合わせて表を直す（CSV のキーから機械的に決まる名前なので、直すのは綴りだけ）

- [ ] **Step 3: L0 テストを回す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.Notification\."`
Expected: 41 件 PASS（Model 10・Relay 4・Keys 5・Text 11・Localize 4・Lookup 2・Coverage 5）。Lookup の 2 件目が `MasterHolder.ItemMaster` 未読込で落ちたら Task 3 Step 6 の注に従う

- [ ] **Step 4: 既存の通知の回帰を回す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(WireContractNotification|NotificationStateProbe|Client\.Tests\.Ui\.State\.)"`
Expected: 全件 PASS（既存の `NotificationFeed` を変えていないことの確認）

- [ ] **Step 5: 直したものがあればコミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Notification moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Notification
git commit -m "fix(uitk): 通知の L0 を Editor の compile とテストに合わせる（S2-notification L0 の末）"
```

ここで L0 は完結する（`feature/uitk` へ先に出す必要は無い。Task 6 以降と同じ PR に載せる）。Editor は C3-a の取り込みを待つ間は閉じてよい（`moores-wt status` で自分の Editor を確かめ、uloop で止める）。

---
### Task 6: C3-a と全部隠しの確認、CSS の曲線と横移動（Foundation/Motion。E-19）、行の生存の 2 本の tween

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/Tween/UiEasing.cs`（E-19）
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/Steps/TranslateXTweenStep.cs`（E-19）
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Motion/UiEasingCssCurveTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Motion/UiTweenStepTest.cs`（TranslateX の 1 メソッドを足す）
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/Parts/NotificationRowTiming.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/Parts/NotificationRowFadeSlideStep.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/Parts/NotificationRowLifetime.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/Parts/NotificationRowLifetimeHandle.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationRowLifetimeTest.cs`

**Interfaces:**
- Consumes: `Client.Ui.Foundation.Motion.Tween.UiEasing`・`UiEasingFunctions.Evaluate(UiEasing, float)`・`UiMotionSpec(float durationMilliseconds, float delayMilliseconds, UiEasing easing, UiMotionLoop loop)`・`UiMotionLoop.Once`・`UiTweenAnimation(UiMotionSpec, IUiTweenStep)`（`Begin()`・`bool Advance(float elapsedMilliseconds)`。遅延中は何も書かず、Once は尺で止めて true）・`IUiTweenStep.Apply(float easedProgress)`、`Client.Ui.Foundation.Motion.Steps.OpacityTweenStep(VisualElement, float from, float to)`・`TranslateYTweenStep`（写しの元）、`Client.Ui.Foundation.Motion.Playback.UiMotionPlayer.Play(VisualElement, IUiAnimation) → UiMotionHandle`（`IObservable<Unit> Completed`・`Cancel()`）、テストの `UiTestHost`
- Produces（Task 8 が使う）: `UiEasing.CssEaseOut`・`UiEasing.CssEaseIn`、`sealed class TranslateXTweenStep : IUiTweenStep { TranslateXTweenStep(VisualElement target, float fromPixels, float toPixels); }`、`sealed class NotificationRowTiming { NotificationRowTiming(float enterMilliseconds, float exitMilliseconds, float lifetimeMilliseconds, float shiftPixels); }`、`static class NotificationRowLifetime { static UiTweenAnimation CreateEnter(VisualElement row, NotificationRowTiming timing); static UiTweenAnimation CreateExit(VisualElement row, NotificationRowTiming timing); static NotificationRowLifetimeHandle Play(VisualElement row, NotificationRowTiming timing); }`、`sealed class NotificationRowLifetimeHandle { IObservable<Unit> ExitCompleted { get; } void Cancel(); }`

- [ ] **Step 1: C3-a と C3-b が取り込まれたことを確かめる**（Task 6 以降の前提）

Run:
```bash
git fetch origin && git merge --no-edit origin/feature/uitk
git ls-tree -r --name-only HEAD moorestech_client/Assets/Scripts/Client.Ui | grep -E "GameContentPresentation\.cs|/UiIcon\.cs|/GameIcon\.cs|GameIconFallback\.cs|StageScaleSource\.cs"
grep -n "Core.Master" moorestech_client/Assets/Scripts/Client.Ui/Client.Ui.asmdef
grep -rn "GameContentPresentation" moorestech_client/Assets/Scripts/Client.Starter/Registration --include='*.cs'
```
Expected: C3-a の 4 ファイルと C3-b の `StageScaleSource.cs` があり、`Client.Ui.asmdef` に `Core.Master`、`GameContentPresentation` が GameUi の系で DI 登録されている。どれかが無ければ止めて done.md に「C3-a 待ち」「C3-b 待ち」と書き指揮へ返す。あれば `UiIcon`・`GameIcon`・`GameIconFallback`・`GameContentPresentation.Item(ItemId)` の実物の名前空間・ctor・メソッド名を読み、Task 7・9 のコード片（下では設計書 §2.1・§2.3 の形で書いた）と違うところを done.md の「実物との差」に書いてから実物に合わせて書く。`GameIcon` の大きさの決め方も読む: 大きさのつまみ（USS のカスタムプロパティ）があれば `Notification.uss` の `.notification-host` でそのつまみに 22px を入れて受け、無ければ `NotificationRowView` が `_icon` に付ける画面所有のクラス `notification-row__icon-content` の規則（Task 8 Step 1）で 100% に揃える（部品の内部クラス名は指さない）。本体に大きさが当たっていることは Task 7 の L1「アイコンの無い行は…」に `Assert.AreEqual(22f, icon.resolvedStyle.width, 0.5f)` を 1 行足して確かめる
- `StageScaleSource` を C3-b の実物と照合する（C3-b マージ後に実物と照合）。設計書 [A-41] の形で書いた: Foundation/Stage の `StageScaleSource`（`IReadOnlyReactiveProperty<float> Scale`。`panel.scaledPixelsPerPoint` から読み、解像度の変更で値が変わる。C3-b が DI 登録する）。L1 の土台は `new StageScaleSource(IReadOnlyReactiveProperty<float>)` で外から倍率を与える形を仮定した。実物の名前・ctor・登録に合わせて Task 8 の土台・Task 9 の Presenter と撮影シナリオを書き、差を done.md の「実物との差」に書く。**次のどれかなら止めて指揮へ報告する**: (a) `StageScaleSource` が無い、(b) `Scale` が 16:9 どうしの解像度変更〈パネルの寸法が変わらない〉で変わらない（`GeometryChangedEvent` だけで計り直す作り）、(c) テストから倍率を与える口が無い（具象クラスで ctor に倍率の元を渡せず、interface でもない）
- I-fix1 は `feature/uitk` に取り込み済み（`e26010aa0c`＝PR #1606 のマージ）。`git merge-base --is-ancestor e26010aa0c HEAD && echo in` で入っていることだけ確かめ、done.md に書く

- [ ] **Step 2: 全部隠し（Ctrl+U・カットシーン）が背面の層をどう扱うかを確かめる**（「## 死活表」の最後の行。レーンの開始時に 1 回）

Run:
```bash
git log --oneline origin/feature/uitk | grep -i "s2-skit" | head -3
rg -n "HideAll|hide-all|全部隠し|SetWholeUiVisible|SetAllLayersVisible|WholeUi" moorestech_client/Assets/Scripts/Client.Ui moorestech_client/Assets/Scripts/Client.Ui.State moorestech_client/Assets/Scripts/Client.Starter --glob '*.cs'
```
Expected と分岐（どれか 1 つ。結果を done.md の「死活表の実測」に 1 行で書く）:
- S2-skit がマージ済みで、全部隠しが層の根ごと隠す実物（S2-skit 計画の形: `GameUiRoot.SetWholeUiVisible(bool)` の中身 `UiLayerStack.SetAllLayersVisible(bool)` が、全層の親であるルート 1 つの `style.display` を None にする。Ctrl+U・カットシーンの 2 源を合わせて呼ぶのは `Screens/WholeUiVisibility/WholeUiVisibilityPresenter`）→ 通知のホストは `UiLayer.BehindStage` の層の下にあり、その層はルートの子なので、ルートが隠れれば通知も隠れる。何も足さずに Task 9 の `NotificationLayeringTest` に L1 を 1 件足す（テスト名「全部隠しの間は通知のホストが見えない」、UITK だけ）。判定は通知のホストの `display` ではなく、ルート（全層の親）の `display` で見る: `UiLayerStack.Build(rootVisualElement)` で層を組んでホストを `BehindStage` に載せ、`SetAllLayersVisible(false)` の後に `rootVisualElement.resolvedStyle.display == DisplayStyle.None` かつ `rootVisualElement.Contains(notification-host)`、`SetAllLayersVisible(true)` の後にルートの `display` が Flex で行が残っていることを確かめる（`GameUiRoot` は UIDocument を持つ MonoBehaviour で L1 では組めないので中身を呼ぶ。S2-skit の `WholeUiRootHidingTest` と同じ）。`[TearDown]` で `SetAllLayersVisible(true)` を呼んで共有ルートを戻す（途中の Assert が落ちても後続を巻き込まない。S2-skit の `WholeUiRootHidingTest` と同じ）。`notification-host` 自身の `resolvedStyle.display` は親が隠れても Flex のままなので、それで判定しない（必ず赤になる）。Task 9 の括弧書き〈`resolvedStyle` が隠れの状態〉より、この判定を優先する。この形では「全部隠しの対象へ通知の層を 1 行足す」分岐は要らない（ルートごと隠すので、後から足される層も漏れない）。隠し方が要素を外す（`RemoveFromHierarchy`）実物なら、行の動きが止まり除去が来ないので止めて指揮へ返す（D-2 案 a の前提が崩れるため）
- S2-skit がマージ済みで、隠しがルートでなく層の一覧を持つ実物（S2-skit 計画と違う形でマージされた場合だけ）→ S2-skit のファイルは書かずに止め、done.md の「S2-ladder の確認事項」と指揮（integrator）へ「全部隠しが `UiLayer.BehindStage` を隠さない」を返す（実行計画 E-19: 通知の層を全部隠しの対象へ足す行は S2-notification の仕事ではない）
- S2-skit が未マージ → 何も足さず、done.md の「S2-ladder の確認事項」に「全部隠しが `UiLayer.BehindStage` の `notification-host` を隠すこと（Ctrl+U・カットシーン）を S2-skit 取り込み後に確かめる」と 1 行書く（裁定事項ではない）

- [ ] **Step 3: CSS の曲線のテストを書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/Motion/UiEasingCssCurveTest.cs`）

```csharp
using Client.Ui.Foundation.Motion.Tween;
using NUnit.Framework;

namespace Client.Tests.Ui.Motion
{
    /// <summary>
    /// UiEasing の CSS の ease-out・ease-in（cubic-bezier）の値がブラウザの定義と同じことを確かめる（L0。UITK だけ）
    /// Verifies the CSS ease-out and ease-in (cubic-bezier) of UiEasing match the browser definitions (L0; UITK-only)
    /// </summary>
    public class UiEasingCssCurveTest
    {
        private const float Tolerance = 0.001f;

        [TestCase(0.25f, 0.3781f)]
        [TestCase(0.5f, 0.6846f)]
        [TestCase(0.75f, 0.9065f)]
        public void CssEaseOutはcubic_bezier_0_0_058_1の値を返す(float progress, float expected)
        {
            Assert.AreEqual(expected, UiEasingFunctions.Evaluate(UiEasing.CssEaseOut, progress), Tolerance);
        }

        [TestCase(0.25f, 0.0935f)]
        [TestCase(0.5f, 0.3154f)]
        [TestCase(0.75f, 0.6219f)]
        public void CssEaseInはcubic_bezier_042_0_1_1の値を返す(float progress, float expected)
        {
            Assert.AreEqual(expected, UiEasingFunctions.Evaluate(UiEasing.CssEaseIn, progress), Tolerance);
        }

        [Test]
        public void 端は0と1に留まる()
        {
            Assert.AreEqual(0f, UiEasingFunctions.Evaluate(UiEasing.CssEaseOut, 0f));
            Assert.AreEqual(0f, UiEasingFunctions.Evaluate(UiEasing.CssEaseIn, 0f));
            Assert.AreEqual(1f, UiEasingFunctions.Evaluate(UiEasing.CssEaseOut, 1f));
            Assert.AreEqual(1f, UiEasingFunctions.Evaluate(UiEasing.CssEaseIn, 1f));
        }
    }
}
```

（期待値は Python で同じ二分法を 40 回回して出した値。ブラウザの `cubic-bezier` の定義式 `B(t) = 3(1-t)²t·P1 + 3(1-t)t²·P2 + t³`。`UiTweenAnimation` は Once で 0〜1 に収めて渡すので、範囲外の入力の値は約束しない）

- [ ] **Step 4: `UiEasing` に CSS の曲線を足す**（`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/Tween/UiEasing.cs`。E-19。既存の 3 値と分岐は変えない）

enum の末尾に足す:

```csharp
        // Web の CSS 既定の ease-out・ease-in（cubic-bezier(0,0,.58,1)・(.42,0,1,1)）。EaseOutCubic とは値が違う
        // Web CSS default ease-out and ease-in (cubic-bezier(0,0,.58,1) and (.42,0,1,1)); values differ from EaseOutCubic
        CssEaseOut,
        CssEaseIn,
```

`UiEasingFunctions.Evaluate` の switch の `default` の前に足す:

```csharp
                case UiEasing.CssEaseOut:
                    return CssCubicBezier(0f, 0f, 0.58f, 1f, t);
                case UiEasing.CssEaseIn:
                    return CssCubicBezier(0.42f, 0f, 1f, 1f, t);
```

`UiEasingFunctions` の末尾に足す:

```csharp
        private const int CssBezierBisectionSteps = 40;

        // x(s)=t を二分法で解いて y(s) を返す（x は s について単調）。端は丸めずに 0 と 1 を返す
        // Solve x(s)=t by bisection and return y(s) (x is monotonic in s); the ends return exactly 0 and 1
        private static float CssCubicBezier(float x1, float y1, float x2, float y2, float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            var low = 0f;
            var high = 1f;
            for (var i = 0; i < CssBezierBisectionSteps; i++)
            {
                var middle = (low + high) * 0.5f;
                if (Bezier(middle, x1, x2) < t) low = middle;
                else high = middle;
            }
            return Bezier((low + high) * 0.5f, y1, y2);
        }

        private static float Bezier(float s, float p1, float p2)
        {
            var u = 1f - s;
            return 3f * u * u * s * p1 + 3f * u * s * s * p2 + s * s * s;
        }
```

- [ ] **Step 5: 横移動の Step のテストと Step を書く**（E-19）

`moorestech_client/Assets/Scripts/Client.Tests/Ui/Motion/UiTweenStepTest.cs` の `TranslateYは縦だけを補間し横は0に置く` の直後に足す:

```csharp
        [TestCase(0f, -12f)]
        [TestCase(0.5f, -6f)]
        [TestCase(1f, 0f)]
        [TestCase(1.25f, 3f)]
        public void TranslateXは横だけを補間し縦は0に置く(float progress, float expectedX)
        {
            var target = new VisualElement();
            new TranslateXTweenStep(target, -12f, 0f).Apply(progress);

            var translate = target.style.translate.value;
            Assert.AreEqual(0f, translate.y.value);
            Assert.AreEqual(LengthUnit.Pixel, translate.x.unit);
            Assert.AreEqual(expectedX, translate.x.value, 0.001f);
        }
```

（`moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/Steps/TranslateXTweenStep.cs`）

```csharp
using Client.Ui.Foundation.Motion.Tween;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Motion.Steps
{
    /// <summary>
    /// 要素の横の translate を px で補間する（縦は 0 に置く）
    /// Interpolates the element's horizontal translate in pixels (vertical is set to 0)
    /// </summary>
    public sealed class TranslateXTweenStep : IUiTweenStep
    {
        private readonly VisualElement _target;
        private readonly float _fromPixels;
        private readonly float _toPixels;

        public TranslateXTweenStep(VisualElement target, float fromPixels, float toPixels)
        {
            _target = target;
            _fromPixels = fromPixels;
            _toPixels = toPixels;
        }

        public void Apply(float easedProgress)
        {
            _target.style.translate = new Translate(Mathf.LerpUnclamped(_fromPixels, _toPixels, easedProgress), 0);
        }
    }
}
```

- [ ] **Step 6: 行の生存の 2 本のテストを書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationRowLifetimeTest.cs`）

```csharp
using Client.Ui.Foundation.Motion.Tween;
using Client.Ui.Screens.Notification.Parts;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 行の入場と、生存尺の終端から逆算した遅延で始まる退場の 2 本の tween を経過時間で確かめる（Web の notificationAnimationDesign.test.ts の 2 件）
    /// Verifies the two tweens of a row, the enter and the exit delayed back from the lifetime end, by elapsed time (2 tests of Web notificationAnimationDesign.test.ts)
    /// </summary>
    public class NotificationRowLifetimeTest
    {
        private const float Tolerance = 0.001f;
        private static readonly NotificationRowTiming WebTiming = new(160f, 200f, 7000f, 12f);
        private VisualElement _row;

        [SetUp]
        public void CreateRow()
        {
            _row = new VisualElement();
        }

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">入場と退場の2本を持ち、退場は生存尺から逆算した遅延で始まる</web-test>
        [Test]
        public void 入場と退場の2本を持ち退場は生存尺から逆算した遅延で始まる()
        {
            var enter = NotificationRowLifetime.CreateEnter(_row, WebTiming);
            var exit = NotificationRowLifetime.CreateExit(_row, WebTiming);

            // 入場の半ば（CSS ease-out で 0.6846）。退場は遅延中で何も書かない
            // Midway through the enter (0.6846 on CSS ease-out); the exit is still delayed and writes nothing
            Assert.IsFalse(AdvanceBoth(enter, exit, 80f));
            Assert.AreEqual(0.6846f, Opacity(), Tolerance);
            Assert.AreEqual(-12f * (1f - 0.6846f), TranslateX(), 0.01f);

            // 退場は 7000-200=6800ms から CSS ease-in で始まり、生存尺の終端で終わる
            // The exit starts at 7000-200=6800ms on CSS ease-in and ends at the lifetime end
            Assert.IsFalse(AdvanceBoth(enter, exit, 6799f));
            Assert.AreEqual(1f, Opacity(), Tolerance);
            Assert.IsFalse(AdvanceBoth(enter, exit, 6900f));
            Assert.AreEqual(1f - 0.3154f, Opacity(), Tolerance);
            Assert.IsTrue(AdvanceBoth(enter, exit, 7000f));
            Assert.AreEqual(0f, Opacity(), Tolerance);
            Assert.AreEqual(-12f, TranslateX(), Tolerance);
        }

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">退場のfill-modeはforwardsで入場を巻き戻さない</web-test>
        [Test]
        public void 保持の間は入場の終わりの姿のまま退場を先取りしない()
        {
            var enter = NotificationRowLifetime.CreateEnter(_row, WebTiming);
            var exit = NotificationRowLifetime.CreateExit(_row, WebTiming);
            foreach (var elapsed in new[] { 160f, 3000f, 6800f })
            {
                Assert.IsFalse(AdvanceBoth(enter, exit, elapsed));
                Assert.AreEqual(1f, Opacity(), Tolerance, $"opacity at {elapsed}");
                Assert.AreEqual(0f, TranslateX(), Tolerance, $"translate at {elapsed}");
            }
        }

        [Test]
        public void 尺が0なら即座に表示し生存尺の終端で消える()
        {
            var zero = new NotificationRowTiming(0f, 0f, 7000f, 0f);
            var enter = NotificationRowLifetime.CreateEnter(_row, zero);
            var exit = NotificationRowLifetime.CreateExit(_row, zero);
            Assert.IsFalse(AdvanceBoth(enter, exit, 0f));
            Assert.AreEqual(1f, Opacity(), Tolerance);
            Assert.IsFalse(AdvanceBoth(enter, exit, 6999f));
            Assert.AreEqual(1f, Opacity(), Tolerance);
            Assert.IsTrue(AdvanceBoth(enter, exit, 7000f));
            Assert.AreEqual(0f, Opacity(), Tolerance);
        }

        // 本番と同じく入場→退場の順に同じ経過時間で進め、退場の終わりを返す
        // Advance enter then exit with the same elapsed time as in production and return the exit's end
        private static bool AdvanceBoth(UiTweenAnimation enter, UiTweenAnimation exit, float elapsedMilliseconds)
        {
            enter.Advance(elapsedMilliseconds);
            return exit.Advance(elapsedMilliseconds);
        }

        private float Opacity()
        {
            return _row.style.opacity.value;
        }

        private float TranslateX()
        {
            return _row.style.translate.value.x.value;
        }
    }
}
```

- [ ] **Step 7: 尺の値・束ねの Step・2 本の組み立て・取り消しの口を書く**（`moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/Parts/NotificationRowTiming.cs`）

```csharp
namespace Client.Ui.Screens.Notification.Parts
{
    /// <summary>
    /// 行の入場・退場の尺と生存尺とずれ。入退場とずれはトークン、生存尺は NotificationModel の定数から来る
    /// Enter and exit durations, the lifetime and the shift of a row; enter, exit and shift come from tokens, the lifetime from the NotificationModel constant
    /// </summary>
    public sealed class NotificationRowTiming
    {
        public readonly float EnterMilliseconds;
        public readonly float ExitMilliseconds;
        public readonly float LifetimeMilliseconds;
        public readonly float ShiftPixels;

        public NotificationRowTiming(float enterMilliseconds, float exitMilliseconds, float lifetimeMilliseconds, float shiftPixels)
        {
            EnterMilliseconds = enterMilliseconds;
            ExitMilliseconds = exitMilliseconds;
            LifetimeMilliseconds = lifetimeMilliseconds;
            ShiftPixels = shiftPixels;
        }
    }
}
```

（`moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/Parts/NotificationRowFadeSlideStep.cs`）

```csharp
using Client.Ui.Foundation.Motion.Steps;
using Client.Ui.Foundation.Motion.Tween;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Notification.Parts
{
    /// <summary>
    /// Foundation の不透明度と横移動の Step を同じ進みで書く束ね（Web の opacity と translateX を 1 つの keyframes で動かすのと同じ）。自分ではスタイルを書かない
    /// Drives the Foundation opacity and horizontal-translate steps with one progress (same as the Web keyframes moving opacity and translateX together); it writes no style itself
    /// </summary>
    public sealed class NotificationRowFadeSlideStep : IUiTweenStep
    {
        private readonly OpacityTweenStep _opacity;
        private readonly TranslateXTweenStep _translateX;

        public NotificationRowFadeSlideStep(VisualElement row, float fromOpacity, float toOpacity, float fromPixels, float toPixels)
        {
            _opacity = new OpacityTweenStep(row, fromOpacity, toOpacity);
            _translateX = new TranslateXTweenStep(row, fromPixels, toPixels);
        }

        public void Apply(float easedProgress)
        {
            _opacity.Apply(easedProgress);
            _translateX.Apply(easedProgress);
        }
    }
}
```

（`moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/Parts/NotificationRowLifetime.cs`）

```csharp
using Client.Ui.Foundation.Motion.Playback;
using Client.Ui.Foundation.Motion.Tween;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Notification.Parts
{
    /// <summary>
    /// 行の生存を 2 本の tween で組む: 入場（左のずれと透明から）と、生存尺の終端に終わるよう遅延を逆算した退場。トーストの前例と同じく UiTweenAnimation と Step だけで書く
    /// Builds a row's lifetime from two tweens: the enter (from the left shift and transparent) and the exit delayed so it ends at the lifetime end; written only with UiTweenAnimation and steps like the toast precedent
    /// </summary>
    public static class NotificationRowLifetime
    {
        public static UiTweenAnimation CreateEnter(VisualElement row, NotificationRowTiming timing)
        {
            var spec = new UiMotionSpec(timing.EnterMilliseconds, 0f, UiEasing.CssEaseOut, UiMotionLoop.Once);
            return new UiTweenAnimation(spec, EnterStep(row, timing));
        }

        public static UiTweenAnimation CreateExit(VisualElement row, NotificationRowTiming timing)
        {
            // 退場の頭は生存尺から退場の尺を引いた時刻。2 本は同じフレームで始まるので遅延で順を作る
            // The exit starts at the lifetime minus the exit duration; both start on the same frame, so the delay makes the order
            var spec = new UiMotionSpec(timing.ExitMilliseconds, timing.LifetimeMilliseconds - timing.ExitMilliseconds, UiEasing.CssEaseIn, UiMotionLoop.Once);
            return new UiTweenAnimation(spec, new NotificationRowFadeSlideStep(row, 1f, 0f, 0f, -timing.ShiftPixels));
        }

        public static NotificationRowLifetimeHandle Play(VisualElement row, NotificationRowTiming timing)
        {
            // 最初のスケジューラ呼び出しまでの 1 コマに表示の姿が出ないよう、入場の頭の姿を先に書く
            // Write the enter's head pose first so the shown pose never flashes before the first scheduler callback
            EnterStep(row, timing).Apply(0f);
            var enter = UiMotionPlayer.Play(row, CreateEnter(row, timing));
            var exit = UiMotionPlayer.Play(row, CreateExit(row, timing));
            return new NotificationRowLifetimeHandle(enter, exit);
        }

        private static NotificationRowFadeSlideStep EnterStep(VisualElement row, NotificationRowTiming timing)
        {
            return new NotificationRowFadeSlideStep(row, 0f, 1f, -timing.ShiftPixels, 0f);
        }
    }
}
```

（`moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/Parts/NotificationRowLifetimeHandle.cs`）

```csharp
using System;
using Client.Ui.Foundation.Motion.Playback;
using UniRx;

namespace Client.Ui.Screens.Notification.Parts
{
    /// <summary>
    /// 行の生存の 2 本をまとめて取り消し、退場の終わりを知らせる
    /// Cancels both tweens of a row's lifetime together and reports the exit's end
    /// </summary>
    public sealed class NotificationRowLifetimeHandle
    {
        private readonly UiMotionHandle _enter;
        private readonly UiMotionHandle _exit;

        public IObservable<Unit> ExitCompleted => _exit.Completed;

        public NotificationRowLifetimeHandle(UiMotionHandle enter, UiMotionHandle exit)
        {
            _enter = enter;
            _exit = exit;
        }

        public void Cancel()
        {
            _enter.Cancel();
            _exit.Cancel();
        }
    }
}
```

注: 2 本は同じ要素に別々のスケジューラの項目として載り、入場は 160ms で終わって止まり、退場は 6800ms まで何も書かないので、同じスタイルを同時に書き合う時間は無い。終わった入場の `Cancel()` が 2 度目の停止で壊れないこと（`UiMotionHandle.Stop` の `UnregisterCallback`・`Pause` が 2 回呼ばれて害が無いこと）は `UiMotionPlayerTest` の実物で確かめ、害があれば `NotificationRowLifetimeHandle` の側で入場の `Completed` を見て済みを覚える形にする。

- [ ] **Step 8: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Motion\.|Screens\.Notification\.NotificationRowLifetimeTest\.)"`
Expected: ErrorCount 0 / 全件 PASS（このレーンの分は `UiEasingCssCurveTest` 7〈TestCase 6 行＋1〉・`UiTweenStepTest` の TranslateX 4・`NotificationRowLifetimeTest` 3。Motion の既存テストも緑のまま＝`UiEasing` の既存 3 値を壊していない）

- [ ] **Step 9: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/Tween/UiEasing.cs moorestech_client/Assets/Scripts/Client.Ui/Foundation/Motion/Steps/TranslateXTweenStep.cs moorestech_client/Assets/Scripts/Client.Tests/Ui/Motion moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification
git commit -m "feat(uitk): UiEasing に CSS の ease-in・ease-out、横移動の TranslateXTweenStep を足し、通知の行の生存を入場と遅延つき退場の 2 本の tween で組む（E-19。S2-notification）"
```

---

### Task 7: 通知の 1 行（アイコンと文）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/INotificationRowIcons.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/GameContentNotificationRowIcons.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/NotificationRowView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/FakeNotificationRowIcons.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationRowViewTest.cs`

**Interfaces:**
- Consumes（C3-a。Task 6 Step 1 で実物と照合した名前で書く）: `UiIcon(Texture2D texture, string idText, UiText label)`、`GameIcon(GameIconFallback fallback)`・`SetIcon(UiIcon icon)`、`GameIconFallback.IdText`、`GameContentPresentation.Item(ItemId) → UiIcon`。F1/段階 1: `LocalizedLabel()`・`SetText(UiText)`、`UiTypographyClass.TextMedium`（`"text--medium"`）。Task 1 の `NotificationRow`・中身の型 `OperationDeniedNotificationContent`・アイコンの型 `ItemNotificationIcon`・`NoNotificationIcon`。テストの `ScreenTestHost`（`Mount`・`IsShown`）・`ThemeTokenFile.ResolveColor/ResolvePixels`・`ThemeTokenFile.AssertSameColor(Color expected, Color actual, string what)`
- Produces（Task 8・9 が使う）: `interface INotificationRowIcons { UiIcon ItemIcon(ItemId itemId); }`、`sealed class GameContentNotificationRowIcons : INotificationRowIcons { GameContentNotificationRowIcons(GameContentPresentation content); }`、`sealed class NotificationRowView : VisualElement { NotificationRowView(); NotificationRow Row { get; private set; } void Show(NotificationRow row, UiText text, INotificationRowIcons icons); void SetText(UiText text); }`（name `notification-row`、クラス `notification-row`・`notification-row--operation-denied`・`notification-row__icon`・`notification-row__text`）

- [ ] **Step 1: アイコンの口と本番の口を書く**（`moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/INotificationRowIcons.cs`）

```csharp
using Client.Ui.Foundation.Icons;
using Core.Master;

namespace Client.Ui.Screens.Notification
{
    /// <summary>
    /// 通知の行の頭に出すアイテムのアイコンを引く口（テストでは代わりの文字だけのアイコンを返す）
    /// Looks up the item icon at the head of a notification row (tests return fallback-text-only icons)
    /// </summary>
    public interface INotificationRowIcons
    {
        UiIcon ItemIcon(ItemId itemId);
    }
}
```

（`moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/GameContentNotificationRowIcons.cs`）

```csharp
using Client.Ui.Content;
using Client.Ui.Foundation.Icons;
using Core.Master;

namespace Client.Ui.Screens.Notification
{
    /// <summary>
    /// 本番の口。ID からアイコンへの解決は Content 層（GameContentPresentation）の 1 か所に任せる
    /// Production lookup; resolving an id into an icon is left to the single place in the Content layer (GameContentPresentation)
    /// </summary>
    public sealed class GameContentNotificationRowIcons : INotificationRowIcons
    {
        private readonly GameContentPresentation _content;

        public GameContentNotificationRowIcons(GameContentPresentation content)
        {
            _content = content;
        }

        public UiIcon ItemIcon(ItemId itemId)
        {
            return _content.Item(itemId);
        }
    }
}
```

（`using` の名前空間 `Client.Ui.Content`・`Client.Ui.Foundation.Icons` は設計書 §1.1・§2.1 の置き場からの推定。Task 6 Step 1 の実物に合わせる）

- [ ] **Step 2: テストの口を書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/FakeNotificationRowIcons.cs`）

```csharp
using Client.Ui.Foundation.Icons;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Text;
using Core.Master;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// テクスチャを持たず代わりの文字 "#<id>" だけのアイコンを返す（Web の e2e で ItemIcon が "#2" を出すのと同じ見え方）
    /// Returns texture-less icons with only the "#<id>" fallback text (same look as the Web e2e ItemIcon showing "#2")
    /// </summary>
    public sealed class FakeNotificationRowIcons : INotificationRowIcons
    {
        public UiIcon ItemIcon(ItemId itemId)
        {
            var idText = $"#{itemId.AsPrimitive()}";
            return new UiIcon(null, idText, UiText.FromVerbatim(idText));
        }
    }
}
```

- [ ] **Step 3: 行のテストを書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationRowViewTest.cs`）

```csharp
using System.Collections.Generic;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.State.Notification;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using Client.Ui.State.Text;
using Core.Master;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 通知の 1 行の色・アイコンの枠・寸法を確かめる（L1。UITK だけのテスト）
    /// Verifies the color, icon slot and size of one notification row (L1; UITK-only tests)
    /// </summary>
    public class NotificationRowViewTest : ScreenTestHost
    {
        private readonly FakeNotificationRowIcons _icons = new();

        [Test]
        public void 拒否の行は警告色で出る()
        {
            var row = Mount(new NotificationRowView());
            row.Show(new NotificationRow(1, 0, Denied("denied.craftResultFull", NoParams, NoIcon)), UiText.FromVerbatim("full"), _icons);
            simulate.FrameUpdate();
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--text-insufficient"), row.Q<Label>(className: "notification-row__text").resolvedStyle.color, "denied text");

            row.Show(new NotificationRow(2, 0, Achievement("achievement.unlockedBlock", NoParams, NoIcon)), UiText.FromVerbatim("ok"), _icons);
            simulate.FrameUpdate();
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--text-high-contrast"), row.Q<Label>(className: "notification-row__text").resolvedStyle.color, "normal text");
        }

        [Test]
        public void アイコンの無い行はアイコンの枠を出さずある行は代わりの文字を出す()
        {
            var row = Mount(new NotificationRowView());
            var iconSlot = row.Q(className: "notification-row__icon");
            row.Show(new NotificationRow(1, 0, Achievement("achievement.unlockedBlock", NoParams, NoIcon)), UiText.FromVerbatim("no icon"), _icons);
            Assert.IsFalse(IsShown(iconSlot));

            row.Show(new NotificationRow(2, 0, Earned(2, 5, "itemEarned.mined")), UiText.FromVerbatim("Stone +5"), _icons);
            Assert.IsTrue(IsShown(iconSlot));
            StringAssert.Contains("#2", ConcatText(iconSlot));
        }

        [Test]
        public void 文字だけの行は幅がトークンで高さがWebと同じ30px()
        {
            var row = Mount(new NotificationRowView());
            row.Show(new NotificationRow(1, 0, Achievement("achievement.unlockedBlock", NoParams, NoIcon)), UiText.FromVerbatim("Block unlocked"), _icons);
            simulate.FrameUpdate();

            // Web: 15px×1.2=18px の行＋上下 5px の padding＋上下 1px の枠
            // Web: an 18px line (15px x 1.2) + 5px vertical padding + 1px vertical border
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--notification-width"), row.resolvedStyle.width, 1f);
            Assert.AreEqual(30f, row.layout.height, 1f);
        }

        private static string ConcatText(VisualElement root)
        {
            var text = string.Empty;
            root.Query<TextElement>().ForEach(element => text += element.text);
            return text;
        }
    }
}
```

（`ThemeTokenFile` の名前空間は実物 `S/Client.Tests/Ui/Components/ThemeTokenFile.cs` に合わせる。`--notification-width` 等は `Tokens.uss` で直に読める）

- [ ] **Step 4: 行の View を書く**（`moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/NotificationRowView.cs`）

```csharp
using System;
using Client.Ui.Components.Icons;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Notification
{
    /// <summary>
    /// 通知の 1 行。アイテムのアイコン（任意）と文を横に並べ、拒否は警告色にする。入力は奪わない
    /// One notification row: an optional item icon beside the text, with denied rows in the warning color; never takes input
    /// </summary>
    public sealed class NotificationRowView : VisualElement
    {
        private const string DeniedClassName = "notification-row--operation-denied";

        private readonly VisualElement _iconSlot;
        private readonly GameIcon _icon;
        private readonly LocalizedLabel _text;

        public NotificationRow Row { get; private set; }

        public NotificationRowView()
        {
            name = "notification-row";
            AddToClassList("notification-row");
            pickingMode = PickingMode.Ignore;

            // アイコンは 22px の枠に入れ、テクスチャが無ければ代わりの文字 #<id> を出す（Web の ItemIcon と同じ）
            // The icon sits in a 22px slot and shows the #<id> fallback without a texture (same as Web ItemIcon)
            _iconSlot = new VisualElement { pickingMode = PickingMode.Ignore };
            _iconSlot.AddToClassList("notification-row__icon");
            _icon = new GameIcon(GameIconFallback.IdText) { pickingMode = PickingMode.Ignore };
            _icon.AddToClassList("notification-row__icon-content");
            _iconSlot.Add(_icon);
            Add(_iconSlot);

            _text = new LocalizedLabel { pickingMode = PickingMode.Ignore };
            _text.AddToClassList("notification-row__text");
            _text.AddToClassList(UiTypographyClass.TextMedium);
            Add(_text);
        }

        public void Show(NotificationRow row, UiText text, INotificationRowIcons icons)
        {
            Row = row;
            EnableInClassList(DeniedClassName, row.Content is OperationDeniedNotificationContent);

            // アイコンの型で枠の有無を決める（アイテムだけ枠を出す）
            // The icon type decides the slot (only items show it)
            switch (row.Content.Icon)
            {
                case ItemNotificationIcon itemIcon:
                    _iconSlot.style.display = DisplayStyle.Flex;
                    _icon.SetIcon(icons.ItemIcon(itemIcon.ItemId));
                    break;
                case NoNotificationIcon:
                    _iconSlot.style.display = DisplayStyle.None;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(row), row.Content.Icon.GetType().Name, null);
            }
            _text.SetText(text);

            // GameIcon の子要素（C3-a が作る）も含め、行の部分木を全部入力の対象から外す（通知は背面の層で入力を奪わない）
            // Exclude the whole row subtree from input, including GameIcon children created by C3-a (notifications never take input)
            this.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
        }

        public void SetText(UiText text)
        {
            _text.SetText(text);
        }
    }
}
```

- [ ] **Step 5: コンパイルとテスト**（USS は Task 8 で足すので、ここでは色・寸法の 2 件は落ちてよい。Task 8 Step 6 で緑にする）

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.Notification\.NotificationRowViewTest\."`
Expected: ErrorCount 0 / 「アイコンの無い行は…」が PASS（残り 2 件は USS 待ちで FAIL）

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification
git commit -m "feat(uitk): 通知の 1 行（アイコンの枠と文・拒否の警告色）とアイコンの口を足す（S2-notification）"
```

---
### Task 8: 通知のホスト（差分描画・生存・除去・言語）と USS

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/NotificationHostView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/Notification.uss`
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（Screens 区画の末尾に `@import` 1 行）
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationHostViewTestFixture.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationHostViewTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationHostViewLayoutTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationViewportScalingTest.cs`

**Interfaces:**
- Consumes: Task 6 の `NotificationRowLifetime.Play(VisualElement, NotificationRowTiming) → NotificationRowLifetimeHandle`（`ExitCompleted`・`Cancel()`。パネルのスケジューラで進み、外れると止まる）、`UssCustomStyleReader.TryReadMilliseconds/TryReadPixels(ICustomStyle, string, out float)`（読めなければ自分で LogError）、`Localize.OnLanguageChanged`（`IObservable<Unit>`）・`Localize.TrySetChosenLanguage(string)`、Task 1 の書き口 `INotificationListWriter`、Task 3 の `NotificationText`・`INotificationTextLookup`、Task 7 の行
- つまみの上書き: Web の局所上書き変数は行ごとの inline style `--notification-lifetime`（`NotificationHost.tsx` の `lifetimeStyle`）だけ。UITK では C# の `NotificationRowTiming` の生存尺が受け、USS のつまみの上書きは無い（ホストの受け直し 4 つは画面 USS の中で閉じる）
- Produces（Task 9 が使う）: `sealed class NotificationHostView : VisualElement { NotificationHostView(INotificationListWriter writer, INotificationTextLookup textLookup, INotificationRowIcons icons); void Render(NotificationListViewModel viewModel); }`（name `notification-host`。DSL のロケータと S2-skit の全部隠しが引く名前）、USS の受け直し名 `--notification-host-enter-duration`・`--notification-host-exit-duration`・`--notification-host-shift`・`--notification-host-screen-left`、`void SetStageScale(float stageScale)`（C3-b の `StageScaleSource.Scale` の値を受けて `style.left = screenLeft ÷ stageScale`）

- [ ] **Step 1: USS を書く**（`moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/Notification.uss`）

```css
/* 通知のホストと行。Web W/features/notification/style.module.css の写し。ホストは実画面の左端・縦の中央線から下へ積み、舞台と同率で拡縮する（層の族 Viewport ＋ PanelSettings の倍率） */
/* Notification host and rows, copied from Web W/features/notification/style.module.css; the host stacks down from the vertical center at the left edge of the real screen and scales with the stage (Viewport family plus PanelSettings scaling) */

.notification-host {
    position: absolute;
    top: 50%;
    /* 左端は実画面の 16px（Web .host の left: 1rem は --ui-scale の外）。C# が StageScaleSource の倍率で割って style.left に当てる */
    /* The left edge is 16 real-screen px (Web .host left: 1rem is outside --ui-scale); C# divides it by the StageScaleSource scale into style.left */
    --notification-host-screen-left: 16px;
    flex-direction: column;
    --notification-host-enter-duration: var(--notification-enter-duration);
    --notification-host-exit-duration: var(--notification-exit-duration);
    --notification-host-shift: var(--notification-shift);
}

/* 研究ノードカード同族の半透明ネイビー面と 1px の寒色枠（直角・影なし）。行間 10px は Web の gap の代わりに下の余白で取る */
/* Navy translucent face with a 1px cool border (square, shadowless), same family as research node cards; the 10px row gap replaces the Web gap with a bottom margin */
.notification-row {
    flex-direction: row;
    align-items: center;
    flex-shrink: 0;
    width: var(--notification-width);
    margin-bottom: 10px;
    padding: 5px 12px;
    border-width: 1px;
    border-color: var(--notification-border);
    background-color: var(--notification-face);
}

/* アイテムのアイコンは 1 行の文字高に収め、文との間は Web の gap 8px */
/* The item icon fits within one line of text, with the Web 8px gap before the text */
.notification-row__icon {
    width: 22px;
    height: 22px;
    flex-shrink: 0;
    margin-right: 8px;
}

/* 枠の中の GameIcon 本体も 22px に揃える。部品のクラスは指さず、画面所有のクラスを View が付ける（前例 PauseMenuSubPageView の pause-menu__back） */
/* Size the GameIcon body inside the slot to 22px too; target a screen-owned class the View adds, not the component's class (precedent: pause-menu__back in PauseMenuSubPageView) */
.notification-row__icon-content {
    width: 100%;
    height: 100%;
}

/* 固定幅に収まらない文は折り返す（省略記号だとアイテム名が読めない）。文字影は世界が透ける面の上の読みやすさの補助 */
/* Text that overflows the fixed width wraps (an ellipsis would hide item names); the text shadow aids legibility over the see-through face */
.notification-row__text {
    flex-grow: 1;
    flex-shrink: 1;
    min-width: 0;
    margin: 0;
    padding: 0;
    white-space: normal;
    -unity-text-align: middle-left;
    font-size: 15px;
    /* 0.055em×15px=0.825px。UITK の letter-spacing の px は「文字の大きさ/100×値」で効くので 0.825÷0.15 */
    /* 0.055em x 15px = 0.825px; a UITK letter-spacing px acts as font-size/100 x value, so 0.825 / 0.15 */
    letter-spacing: 5.5px;
    /* 行の高さは Web の 15px×1.2＝18px。1 行目は min-height、2 行目以降の行送りは LocalizedLabel のつまみ（TooltipFace.uss の前例） */
    /* Line height is the Web 15px x 1.2 = 18px: min-height for the first line, LocalizedLabel's knob for the pitch after it (TooltipFace.uss precedent) */
    min-height: 18px;
    --localized-label-line-height: 18px;
    color: var(--text-high-contrast);
    /* Web style.module.css:38 の直書きと同値の借用。key-hint 側が変わると通知も変わる。--notification-text-shadow を integrator に依頼済み（D-9） */
    /* Borrowed because it equals the Web style.module.css:38 literal; a key-hint change also changes notifications; --notification-text-shadow is requested from the integrator (D-9) */
    text-shadow: var(--key-hint-text-shadow);
}

/* 操作拒否は共通の警告色 */
/* Denied operations use the shared warning color */
.notification-row--operation-denied .notification-row__text {
    color: var(--text-insufficient);
}
```

- [ ] **Step 2: tss に足す**（`moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` の `/* == section: Screens == */` の末尾の行の次に 1 行。テキストの追記で、Unity の YAML 資産ではない）

```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/Notification/Notification.uss");
```

- [ ] **Step 3: 左端の置き方を確かめる**（ファイルは足さない）

ホストは Viewport 族の背面の層に置いたままにし、中身（行の幅・文字）は PanelSettings の倍率で舞台と同率に拡縮させる（Web の中身の `scale(var(--ui-scale))`）。倍率の外にある左端だけ（Web `.host` の `left: 1rem`）を、C3-b の `StageScaleSource.Scale`（設計書 [A-41]。解像度の変更で値が変わる）を Task 9 の Presenter が購読して `SetStageScale(float)` へ流し、ホストが `style.left = 16 ÷ 倍率` を当てて実画面 16px に保つ。16 は画面 USS の `--notification-host-screen-left` を他の受け直しと同じ `CustomStyleResolvedEvent` で読む。ホストを `UiScreenPixelScope` に入れて中身に倍率を掛け直す形と、`GeometryChangedEvent` で `panel.scaledPixelsPerPoint` を読み直す形は取らない（D-7）

- [ ] **Step 4: ホストを書く**（`moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/NotificationHostView.cs`）

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Localization;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.Notification.Parts;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Text;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Notification
{
    /// <summary>
    /// 通知の行を Id で差分描画し、各行の生存を入場と退場の 2 本の tween で回して、退場が終わった行を書き口から除く。入力は奪わない
    /// Diff-renders notification rows by Id, runs each row's lifetime as an enter and an exit tween and removes rows through the write side when the exit ends; never takes input
    /// </summary>
    public sealed class NotificationHostView : VisualElement
    {
        private readonly INotificationListWriter _writer;
        private readonly INotificationTextLookup _textLookup;
        private readonly INotificationRowIcons _icons;
        private readonly Dictionary<long, ShownRow> _shownRows = new();
        private NotificationListViewModel _waitingForStyle;
        private NotificationRowTiming _timing;
        private IDisposable _languageSubscription;
        private float _stageScale = 1f;
        private float? _screenLeftPixels;

        public NotificationHostView(INotificationListWriter writer, INotificationTextLookup textLookup, INotificationRowIcons icons)
        {
            _writer = writer;
            _textLookup = textLookup;
            _icons = icons;
            name = "notification-host";
            AddToClassList("notification-host");
            pickingMode = PickingMode.Ignore;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            RegisterCallback<GeometryChangedEvent>(_ => OnGeometryChanged());
            RegisterCallback<AttachToPanelEvent>(attachEvent => _languageSubscription = Localize.OnLanguageChanged.Subscribe(_ => ReapplyTexts()));
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void Render(NotificationListViewModel viewModel)
        {
            // 尺はトークンから読むので、スタイル解決前の描画は解決後まで最新の 1 つを預かる
            // Durations come from tokens, so a render before style resolution keeps the latest one until it resolves
            // silent-ok: パネルに付いてスタイルが解決するまでの一時的な預かり / 解決が来ないまま配置まで進んだら（tss の @import 漏れ等）OnGeometryChanged の ReportUnresolvedStyle が LogError を出す
            if (_timing == null)
            {
                _waitingForStyle = viewModel;
                return;
            }

            RemoveVanishedRows(viewModel);
            foreach (var row in viewModel.Rows) ShowRow(row);
        }

        private void ShowRow(NotificationRow row)
        {
            if (_shownRows.TryGetValue(row.Id, out var shown))
            {
                // silent-ok: Render は毎回全行を渡すので、生存尺が回り直していない行は触らない / 合流の再生は L1「合流で生存尺が回り直し…」が確かめる
                if (shown.LifetimeEpoch == row.LifetimeEpoch) return;

                // 合流で生存尺が回り直したら、文を差し替えて 2 本を頭から再生する（行の要素と位置は保つ）
                // When a merge restarts the lifetime, swap the text and replay both tweens from the head (keeping the element and position)
                shown.Motion.Cancel();
                shown.View.Show(row, TextOf(row), _icons);
                shown.Restart(row.LifetimeEpoch, PlayLifetime(shown.View, row.Id));
                return;
            }

            // 新着は最上段へ（Web の column-reverse と同じ見え方）
            // The newest goes on top (same look as the Web column-reverse)
            var view = new NotificationRowView();
            view.Show(row, TextOf(row), _icons);
            Insert(0, view);
            _shownRows.Add(row.Id, new ShownRow(view, row.LifetimeEpoch, PlayLifetime(view, row.Id)));
        }

        private NotificationRowLifetimeHandle PlayLifetime(NotificationRowView view, long id)
        {
            // 退場の終わりで行を書き口から除く。除去と退場が同じパネルの時計に乗る（除去の経路はこれだけ。D-2）
            // Remove the row through the write side at the exit end, so removal and exit share the panel clock (the only removal path, D-2)
            var handle = NotificationRowLifetime.Play(view, _timing);
            handle.ExitCompleted.Subscribe(_ => _writer.RemoveNotification(id));
            return handle;
        }

        private void RemoveVanishedRows(NotificationListViewModel viewModel)
        {
            var alive = new HashSet<long>(viewModel.Rows.Select(row => row.Id));
            foreach (var id in _shownRows.Keys.Where(id => !alive.Contains(id)).ToList())
            {
                var shown = _shownRows[id];
                shown.Motion.Cancel();
                shown.View.RemoveFromHierarchy();
                _shownRows.Remove(id);
            }
        }

        private void ReapplyTexts()
        {
            // 内容名・アイテム名は解決済みの文字列で UiText に入るので、言語が変わったら行ごとに解き直す
            // Content and item names enter the UiText as resolved strings, so re-resolve every row when the language changes
            foreach (var shown in _shownRows.Values) shown.View.SetText(TextOf(shown.View.Row));
        }

        // 行の文の解決はこの 1 か所（新着・合流・言語の 3 経路が呼ぶ）
        // The single place that resolves a row's text (called by the new-row, merge and language paths)
        private UiText TextOf(NotificationRow row)
        {
            return NotificationText.Resolve(row.Content, _textLookup);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent detachEvent)
        {
            _languageSubscription?.Dispose();

            // 外れると行の動きが止まり退場の終わりが来ない＝行が Model に残る。常駐のホストが外れるのは Dispose だけのはずなので、それ以外で起きたら開発者に知らせる
            // Detaching stops the row motions, so no exit end comes and rows stay in the model; a resident host should only detach on Dispose, so tell developers otherwise
            if (_shownRows.Count > 0) Debug.LogWarning($"[NotificationHostView] detached with {_shownRows.Count} rows; their exit motions stop and they will not be removed");
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            // 読めないトークンは Reader が LogError を出す。通知を届けるのを優先し、出入り無し（尺 0・ずれ 0）で出す
            // The reader logs unreadable tokens; delivering the notification comes first, so show it without enter/exit (0 durations, 0 shift)
            var hasEnter = UssCustomStyleReader.TryReadMilliseconds(customStyle, "--notification-host-enter-duration", out var enter);
            var hasExit = UssCustomStyleReader.TryReadMilliseconds(customStyle, "--notification-host-exit-duration", out var exit);
            var hasShift = UssCustomStyleReader.TryReadPixels(customStyle, "--notification-host-shift", out var shift);
            if (UssCustomStyleReader.TryReadPixels(customStyle, "--notification-host-screen-left", out var screenLeft)) _screenLeftPixels = screenLeft;
            ApplyScreenLeft();
            _timing = hasEnter && hasExit && hasShift
                ? new NotificationRowTiming(enter, exit, NotificationModel.DisplayMilliseconds, shift)
                : new NotificationRowTiming(0f, 0f, NotificationModel.DisplayMilliseconds, 0f);

            var waiting = _waitingForStyle;
            _waitingForStyle = null;
            if (waiting != null) Render(waiting);
        }

        private void OnGeometryChanged()
        {
            ReportUnresolvedStyle();
        }

        private void ReportUnresolvedStyle()
        {
            // 配置まで進んでも画面 USS の受け直しが来ていなければ、預かった通知は出ない。無音にしないため開発者に知らせる
            // If layout ran but the screen USS custom properties never resolved, held notifications never show; tell developers instead of staying silent
            if (_timing == null && _waitingForStyle != null) Debug.LogError("[NotificationHostView] custom style not resolved after layout; is Notification.uss imported by GameUiTheme.tss and the notification-host class present?");
        }

        // 舞台の倍率を受け、左端を実画面の px に保つ（Web .host の left: 1rem は --ui-scale の外。中身は PanelSettings の倍率で舞台と同率）
        // Take the stage scale and keep the left edge in real-screen px (Web .host left: 1rem is outside --ui-scale; contents scale with the stage via PanelSettings)
        public void SetStageScale(float stageScale)
        {
            _stageScale = stageScale;
            ApplyScreenLeft();
        }

        private void ApplyScreenLeft()
        {
            // silent-ok: 画面 USS の解決前は 16px が未知なので置かない / 読めなければ UssCustomStyleReader が LogError、解決しないまま行が来れば ReportUnresolvedStyle が LogError
            if (_screenLeftPixels == null) return;
            style.left = _screenLeftPixels.Value / _stageScale;
        }

        private sealed class ShownRow
        {
            public readonly NotificationRowView View;
            public int LifetimeEpoch { get; private set; }
            public NotificationRowLifetimeHandle Motion { get; private set; }

            public ShownRow(NotificationRowView view, int lifetimeEpoch, NotificationRowLifetimeHandle motion)
            {
                View = view;
                LifetimeEpoch = lifetimeEpoch;
                Motion = motion;
            }

            public void Restart(int lifetimeEpoch, NotificationRowLifetimeHandle motion)
            {
                LifetimeEpoch = lifetimeEpoch;
                Motion = motion;
            }
        }
    }
}
```

注 1: `waiting != null` は「預かった描画が無い」という正常な状態の分岐。`ReportUnresolvedStyle` の前提（クラスの USS 規則が当たらないとき `CustomStyleResolvedEvent` が来ず配置だけが進む）は推測で、Step 6 の UITK だけの 1 件で実測する。実物で `CustomStyleResolvedEvent` が来るなら `UssCustomStyleReader` の LogError が先に出るので、そのテストの期待をその行に替える（どちらでも無音にはならないことを確かめる）。注 2: 取り消した動き（`Cancel()`）は `Completed` を出さない前提（`UiMotionPlayerTest.DetachStopsWithoutCompletion` 等で実物を確かめる。出す実物なら、合流と除去の取り消しで古い退場の終わりが新しい生存の行を消すので、`ShownRow` に epoch を持たせて `ExitCompleted` の購読で照合する形にする）。注 3: 保険タイマーは無い（D-2 案 a）。ホストは常駐で、外れるのは Presenter の Dispose だけなので、退場の終わりは必ず来る。外れたまま行が残る経路は `OnDetachFromPanel` の LogWarning が開発者に知らせる（全部隠しが要素を外す実物でないことは Task 6 Step 2 で確かめる）。ファイルは 200 行未満に収まる見込み（超えたら `ShownRow` を `Parts/NotificationShownRow.cs` へ出す）。

- [ ] **Step 5: テストの土台を書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationHostViewTestFixture.cs`）

```csharp
using System;
using Client.Tests.Ui.State.Notification;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 通知のホストを載せ、本番の Presenter と同じく読み口の変化を Render へ流し、書き口を View に渡す L1 の土台
    /// L1 base that mounts the notification host, pipes read-side changes into Render and hands the write side to the View like the production presenter
    /// </summary>
    public abstract class NotificationHostViewTestFixture : ScreenTestHost
    {
        private IDisposable _subscription;

        protected NotificationModel Model { get; private set; }
        protected FakeNotificationTextLookup TextLookup { get; private set; }
        protected NotificationHostView Host { get; private set; }
        protected ReactiveProperty<float> StageScaleForTest { get; private set; }
        private IDisposable _scaleSubscription;

        [SetUp]
        public void MountNotificationHost()
        {
            Model = new NotificationModel();
            TextLookup = new FakeNotificationTextLookup();
            // 本番の Presenter と同じく StageScaleSource の倍率をホストへ流す。倍率はテストが外から与える（C3-b マージ後に実物の ctor と照合）
            // Feed the StageScaleSource scale into the host like the production presenter; the test supplies the scale (check the real ctor after C3-b merges)
            StageScaleForTest = new ReactiveProperty<float>(1f);
            var stageScaleSource = new StageScaleSource(StageScaleForTest);
            Host = Mount(new NotificationHostView(Model, TextLookup, new FakeNotificationRowIcons()));
            _scaleSubscription = stageScaleSource.Scale.Subscribe(Host.SetStageScale);
            _subscription = Model.Current.Subscribe(Host.Render);
        }

        [TearDown]
        public void DisposeHostSubscription()
        {
            _subscription.Dispose();
            _scaleSubscription.Dispose();
        }

        // パネルの時計を進める（UiMotion はパネルのスケジューラで進む。1 回が 1 フレーム）
        // Advance the panel clock (UiMotion runs on the panel scheduler; one call is one frame)
        protected void AdvancePanel(int milliseconds)
        {
            simulate.FrameUpdateMs(milliseconds);
        }

        protected NotificationRowView RowAt(int index)
        {
            return (NotificationRowView)Host[index];
        }

        protected static string TextOf(VisualElement root)
        {
            var text = string.Empty;
            root.Query<TextElement>().ForEach(element => text += element.text);
            return text;
        }
    }
}
```

- [ ] **Step 6: 時間のテストを書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationHostViewTest.cs`）

```csharp
using System.IO;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Components;
using Client.Ui.State.Notification;
using NUnit.Framework;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 通知の行の出入りと除去の時間を確かめる（L1。Web の notificationAnimationDesign.test.ts 4 件と e2e animation.spec.ts）
    /// Verifies the timing of row enter, exit and removal (L1; 4 tests of Web notificationAnimationDesign.test.ts and e2e animation.spec.ts)
    /// </summary>
    public class NotificationHostViewTest : NotificationHostViewTestFixture
    {
        private const string UssPath = "Assets/Scripts/Client.Ui/Screens/Notification/Notification.uss";

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">生存尺はstoreが唯一の正で、CSSへ尺を直書きしない</web-test>
        [Test]
        public void 生存尺はModelの定数だけが持ちUSSへ尺を書かない()
        {
            Assert.IsFalse(Regex.IsMatch(File.ReadAllText(UssPath), @"7000ms|\b7s\b"));
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(NotificationModel.DisplayMilliseconds - 100);
            Assert.AreEqual(1, Host.childCount);
            AdvancePanel(200);
            AdvancePanel(0);
            Assert.AreEqual(0, Host.childCount);
        }

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">移動量と尺はトークンで管理する</web-test>
        [Test]
        public void 移動量と尺はトークンで管理する()
        {
            Assert.AreEqual("160ms", ThemeTokenFile.Resolve("--notification-enter-duration"));
            Assert.AreEqual("200ms", ThemeTokenFile.Resolve("--notification-exit-duration"));
            Assert.AreEqual("12px", ThemeTokenFile.Resolve("--notification-shift"));
            var uss = File.ReadAllText(UssPath);
            StringAssert.Contains("var(--notification-enter-duration)", uss);
            StringAssert.Contains("var(--notification-exit-duration)", uss);
            StringAssert.Contains("var(--notification-shift)", uss);
        }

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">除去は退場アニメの終了が主で、タイマーは尺より後ろの保険に留める</web-test>
        [Test]
        public void 除去は退場の動きの終わりだけが起こす()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(NotificationModel.DisplayMilliseconds - 50);
            Assert.AreEqual(1, Model.Current.Value.Rows.Count);
            AdvancePanel(100);
            AdvancePanel(0);

            // 保険タイマーは無い（D-2）ので、消したのは退場の動きの終わり
            // There is no fallback timer (D-2), so the exit motion's end did the removal
            Assert.AreEqual(0, Model.Current.Value.Rows.Count);
            Assert.AreEqual(0, Host.childCount);
        }

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">生存尺はホストでなく各行へ渡し、面と同じ要素の上で解決させる</web-test>
        [Test]
        public void 生存は行ごとに回り面と同じ要素が薄れる()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(1000);
            Model.AddNotification(Earned(9, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(6100);
            AdvancePanel(0);

            // 先の行だけが消え、後の行は残る。ホスト自身は薄れない
            // Only the earlier row is gone; the later row stays and the host itself never fades
            Assert.AreEqual(1, Host.childCount);
            Assert.AreEqual(1f, Host.resolvedStyle.opacity, 0.0001f);
            AdvancePanel(1000);
            AdvancePanel(0);
            Assert.AreEqual(0, Host.childCount);
        }

        /// <web-test file="e2e/tests/notification/animation.spec.ts">通知は左からのスライドとフェードで入場し生存尺の終端で退場する</web-test>
        [Test]
        public void 通知は左からのスライドとフェードで入場し生存尺の終端で退場する()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(80);
            var row = RowAt(0);
            Assert.That(row.resolvedStyle.opacity, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(row.resolvedStyle.translate.x, Is.LessThan(0f).And.GreaterThan(-ThemeTokenFile.ResolvePixels("--notification-shift")));

            AdvancePanel(3000);
            Assert.AreEqual(1f, row.resolvedStyle.opacity, 0.0001f);
            Assert.AreEqual(0f, row.resolvedStyle.translate.x, 0.01f);

            AdvancePanel(NotificationModel.DisplayMilliseconds - 3080 - 100);
            Assert.That(row.resolvedStyle.opacity, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(row.resolvedStyle.translate.x, Is.LessThan(0f));
            AdvancePanel(200);
            AdvancePanel(0);
            Assert.AreEqual(0, Host.childCount);
        }

        [Test]
        public void 画面USSが当たらないまま描かれると開発者にエラーを出す()
        {
            // 規則の当たらないホスト（tss の @import 漏れと同じ状態）を載せ、預かった描画が無音で消えないことを確かめる
            // Mount a host no rule matches (same as a missing tss @import) and make sure the held render never vanishes silently
            var bare = new NotificationHostView(new NotificationModel(), new FakeNotificationTextLookup(), new FakeNotificationRowIcons());
            bare.RemoveFromClassList("notification-host");
            bare.Render(NotificationListViewModel.Empty);
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(@"^\[NotificationHostView\] custom style not resolved"));
            Mount(bare);
            AdvancePanel(0);
            AdvancePanel(0);
        }

        [Test]
        public void 合流で生存尺が回り直し動きを頭から再生する()
        {
            Model.AddNotification(Earned(7, 5, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(5000);
            Model.AddNotification(Earned(7, 3, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(80);
            Assert.AreEqual(1, Host.childCount);
            Assert.That(RowAt(0).resolvedStyle.opacity, Is.LessThan(1f));

            // 最初の生存尺（7000ms）を越えても、回り直した行は残る
            // Past the first lifetime (7000ms), the restarted row stays
            AdvancePanel(3000);
            Assert.AreEqual(1, Host.childCount);
            AdvancePanel(4000);
            AdvancePanel(0);
            Assert.AreEqual(0, Host.childCount);
        }
    }
}
```

（`simulate.FrameUpdateMs` は 1 回で 1 フレーム。動きの開始が「Play を呼んだ次のフレーム」になる実物なら、各テストの境目の前後 100ms の余裕の中に収まる。収まらなければ余裕を広げず、開始の時刻を done.md に書いて余裕を実測から決め直す）

- [ ] **Step 7: 配置と文のテストを書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationHostViewLayoutTest.cs`）

```csharp
using System;
using Client.Localization;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.State.Notification;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Content;
using Core.Master;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 通知のホストの並び・寸法・文・入力・言語を確かめる（L1。Web の e2e fixedWidth・itemEarned・layering の 1 件と UITK だけのテスト）
    /// Verifies the host's order, size, text, input and language (L1; Web e2e fixedWidth, itemEarned, one layering test and UITK-only tests)
    /// </summary>
    public class NotificationHostViewLayoutTest : NotificationHostViewTestFixture
    {
        /// <web-test file="e2e/tests/notification/fixedWidth.spec.ts">長さの違う通知が同時に出ても各行の横幅は固定値のまま</web-test>
        [Test]
        public void 長さの違う通知が同時に出ても各行の横幅は固定値のまま()
        {
            TextLookup.SetItemName(new ItemId(7), "Ore");
            TextLookup.SetItemName(new ItemId(9), "An extremely long item name that has to wrap across several lines of the row");
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            Model.AddNotification(Earned(9, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(300);
            var width = ThemeTokenFile.ResolvePixels("--notification-width");
            Assert.AreEqual(width, RowAt(0).layout.width, 1f);
            Assert.AreEqual(width, RowAt(1).layout.width, 1f);
            Assert.Greater(RowAt(0).layout.height, RowAt(1).layout.height);
        }

        /// <web-test file="e2e/tests/notification/itemEarned.spec.ts">アイテム獲得通知はアイコンとアイテム名と獲得数を出し連続獲得で数値が伸びる</web-test>
        [Test]
        public void アイテム獲得通知はアイコンとアイテム名と獲得数を出し連続獲得で数値が伸びる()
        {
            TextLookup.SetItemName(new ItemId(2), "Stone");
            Model.AddNotification(Earned(2, 5, "itemEarned.mined"));
            AdvancePanel(0);
            StringAssert.Contains("#2", TextOf(RowAt(0).Q(className: "notification-row__icon")));
            StringAssert.EndsWith("Stone +5", RowAt(0).Q<Label>(className: "notification-row__text").text);

            Model.AddNotification(Earned(2, 3, "itemEarned.mined"));
            AdvancePanel(0);
            Assert.AreEqual(1, Host.childCount);
            StringAssert.EndsWith("Stone +8", RowAt(0).Q<Label>(className: "notification-row__text").text);
        }

        /// <web-test file="e2e/tests/notification/layering.spec.ts">GameScreenでも通知はマウントされ続け、矩形を持つ</web-test>
        [Test]
        public void ホストは常駐し行があれば矩形を持つ()
        {
            Model.AddNotification(Earned(2, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(300);
            Assert.Greater(Host.worldBound.width, 0f);
            Assert.Greater(Host.worldBound.height, 0f);
            AdvancePanel(NotificationModel.DisplayMilliseconds);
            AdvancePanel(0);
            Assert.IsNotNull(Host.panel);
        }

        [Test]
        public void 新着は最上段に積まれる()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            Model.AddNotification(Earned(9, 1, "itemEarned.mined"));
            AdvancePanel(0);
            Assert.That(RowAt(0).Row.Content, Is.TypeOf<EarnedNotificationContent>().With.Property(nameof(EarnedNotificationContent.ItemId)).EqualTo(new ItemId(9)));
            Assert.Less(RowAt(0).worldBound.y, RowAt(1).worldBound.y);
        }

        [Test]
        public void 倍率が変わると左端は実画面の16pxに当たる値へ置き直され縦の中央線から下へ積む()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            foreach (var stageScale in new[] { 1f, 1.5f, 1f })
            {
                // 16:9 どうしの解像度変更（パネルの寸法は変わらず倍率だけ変わる）を StageScaleSource の値の変化で起こす
                // Drive a 16:9-to-16:9 resolution change (panel size unchanged, only the scale changes) through the StageScaleSource value
                StageScaleForTest.Value = stageScale;
                AdvancePanel(0);
                Assert.AreEqual(16f / stageScale, Host.resolvedStyle.left, 0.01f, $"left at scale {stageScale}");
                Assert.AreEqual(Host.parent.worldBound.height * 0.5f, Host.worldBound.y - Host.parent.worldBound.y, 0.5f, $"center line at scale {stageScale}");
            }
        }

        [Test]
        public void 行の角と中の要素は入力を奪わない()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(300);
            var row = RowAt(0);
            var corners = new[] { new UnityEngine.Vector2(row.worldBound.xMin + 1f, row.worldBound.yMin + 1f), new UnityEngine.Vector2(row.worldBound.xMax - 1f, row.worldBound.yMax - 1f), row.Q(className: "notification-row__icon").worldBound.center };
            foreach (var point in corners)
            {
                var picked = Host.panel.Pick(point);
                Assert.IsFalse(picked != null && (picked == Host || Host.Contains(picked)), $"picked {picked?.name} at {point}");
            }
            row.Query<VisualElement>().ForEach(element => Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.name));
        }

        [Test]
        public void 言語を変えると表示中の行を解き直す()
        {
            Model.AddNotification(Achievement("achievement.researchCompleted", Wire("achievement.researchCompleted", new Dictionary<string, string> { ["research"] = Guid.Empty.ToString() }), NoIcon));
            AdvancePanel(0);
            StringAssert.Contains("Research completed", RowAt(0).Q<Label>(className: "notification-row__text").text);
            var callsBefore = TextLookup.ContentTextCalls;

            Localize.TrySetChosenLanguage("japanese");
            AdvancePanel(0);
            StringAssert.Contains("研究完了", RowAt(0).Q<Label>(className: "notification-row__text").text);
            Assert.Greater(TextLookup.ContentTextCalls, callsBefore);
        }

        [Test]
        public void ホストと行は入力を奪わない()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(300);
            var picked = Host.panel.Pick(RowAt(0).worldBound.center);
            Assert.IsFalse(picked != null && (picked == Host || Host.Contains(picked)), $"picked {picked?.name}");
        }

        [Test]
        public void スタイル解決前の描画は解決後に出る()
        {
            var model = new NotificationModel();
            var early = new NotificationHostView(model, new FakeNotificationTextLookup(), new FakeNotificationRowIcons());
            model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            early.Render(model.Current.Value);
            Assert.AreEqual(0, early.childCount);
            Mount(early);
            AdvancePanel(0);
            Assert.AreEqual(1, early.childCount);
        }
    }
}
```

- [ ] **Step 7b: portalLayerScaling の通知の 1 件を 2 解像度の L1 に写す**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationViewportScalingTest.cs`）

前例 `S/Client.Tests/Ui/Screens/PauseMenu/PauseMenuSettingsLayoutTest.cs` と S2-tutorial の `TutorialViewportScalingTest` の形（`[TestFixture(1280f, 720f)]`・`[TestFixture(1706.7f, 720f)]`。2560×1080 は Expand〈参照 1280×720〉で論理 1706.7×720。`OneTimeSetUp` で `panelSize = _logicalSize`）。Web は 1280×720 と 2560×1440 で「行の幅が `--ui-scale` 倍のまま・画面幅の 2 割」を確かめる。UITK の L1 はパネル単位＝舞台 px なので、「論理の幅が広がっても行の幅は `--notification-width` のままで、画面幅の 2 割を超えない」が同じ意図になる（1280 で 256÷1280＝0.2 ちょうど、1706.7 で 0.15）

```csharp
using Client.Tests.Ui.Components;
using NUnit.Framework;
using UnityEngine;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// Web の portalLayerScaling.spec.ts の通知の 1 件を 2 つの論理解像度で写す。2560x1080 は Expand（参照 1280x720）で 1706.7x720 になる
    /// Copies the notification test of Web portalLayerScaling.spec.ts at two logical sizes; 2560x1080 becomes 1706.7x720 under Expand (reference 1280x720)
    /// </summary>
    [TestFixture(1280f, 720f)]
    [TestFixture(1706.7f, 720f)]
    public class NotificationViewportScalingTest : NotificationHostViewTestFixture
    {
        private readonly Vector2 _logicalSize;

        public NotificationViewportScalingTest(float width, float height)
        {
            _logicalSize = new Vector2(width, height);
        }

        [OneTimeSetUp]
        public void UseLogicalSize()
        {
            // 基底の OneTimeSetUp（本番の 1280x720）の後に走り、パネルの寸法だけを差し替える
            // Runs after the base OneTimeSetUp (production 1280x720) and only swaps the panel size
            panelSize = _logicalSize;
        }

        /// <web-test file="e2e/tests/system/portalLayerScaling.spec.ts">通知の幅はstage比のまま解像度に依らず、高解像度でも画面幅の2割に収まる</web-test>
        [Test]
        public void 通知の幅は解像度に依らず舞台のままで画面幅の2割に収まる()
        {
            Model.AddNotification(Earned(2, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(300);

            // 行の幅は舞台 px の固定値のまま、画面の論理幅に対しては 2 割以内
            // Row width stays the fixed stage px value and within 20% of the logical screen width
            var rowWidth = RowAt(0).worldBound.width;
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--notification-width"), rowWidth, 1f);
            Assert.LessOrEqual(rowWidth / rootVisualElement.worldBound.width, 0.2f + 0.002f);
        }
    }
}
```

（`Earned` は `NotificationTestContents`、`RowAt` は土台 `NotificationHostViewTestFixture` の口。`ThemeTokenFile` の名前空間は Task 7 Step 3 と同じく実物に合わせる。L1 のパネルは PanelSettings の倍率を掛けないので、倍率で左端を置き直す側は Step 7 の「倍率が変わると左端は…」が持つ）

- [ ] **Step 8: コンパイルと Screens のテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Screens\.Notification\."`
Expected: ErrorCount 0 / 24 件 PASS（Lifetime 3・Row 3・Host 7・Layout 9・Scaling 1×2 fixture。Task 7 で落ちていた 2 件も緑）。`get-logs --log-type Error` に `[UssCustomStyleReader]` が無い

- [ ] **Step 9: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git commit -m "feat(uitk): 通知のホスト（行の差分描画・入場と退場の 2 本・退場の終わりで書き口から除去・言語の解き直し）と USS を足す（S2-notification）"
```

---

### Task 9: 背面の層への常駐と DI 登録

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/NotificationLayerPresenter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/NotificationGameUiRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画に 1 行）
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationLayeringTest.cs`

**Interfaces:**
- Consumes: `GameUiRoot.GetLayer(UiLayer, UiFamily)`、C3-b の `StageScaleSource.Scale`（`IReadOnlyReactiveProperty<float>`。C3-b が DI 登録する。Task 6 Step 1 の照合どおり）、`UiLayer.BehindStage`（`--z-viewport-behind-stage`）・`UiFamily.Viewport`、`UiLayerStack.Build(VisualElement) → UiLayerStack`・`.GetLayer(UiLayer, UiFamily)`（層の入れ物の名前は `layer-{layer}`、子は `{入れ物}-viewport`・`-stage`）、VContainer の `RegisterEntryPoint<T>()`・`Register<T>(Lifetime).As<I>()`、Task 1 の読み口・書き口、Task 2 の中継、Task 3 の本番の口、Task 7 の本番の口、Task 8 のホスト、C3-a が登録する `GameContentPresentation`
- Produces: `sealed class NotificationLayerPresenter : IInitializable, IDisposable { internal const UiLayer HostLayer = UiLayer.BehindStage; internal const UiFamily HostFamily = UiFamily.Viewport; }`、`internal static class NotificationGameUiRegistration { static void Register(ContainerBuilder builder); }`

- [ ] **Step 1: 層のテストを書く**（`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification/NotificationLayeringTest.cs`）

```csharp
using Client.Localization;
using Client.Tests.Ui.State.Notification;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Layers;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 通知が背面の層（舞台より前に描かれ、舞台と画面に隠れる層）の実画面の族に立つことを確かめる（Web の notificationLayering.test.ts 3 件と e2e layering.spec.ts 2 件）
    /// Verifies notifications stand in the viewport family of the behind-stage layer, drawn before and covered by the stage and screens (3 tests of Web notificationLayering.test.ts and 2 of e2e layering.spec.ts)
    /// </summary>
    public class NotificationLayeringTest : UiTestHost
    {
        private UiLayerStack _layers;

        [SetUp]
        public void BuildLayers()
        {
            // 文言の辞書を読んでおく（既存の部品テスト GamePanelTest・PanelActionButtonTest の SetUp と同じ）
            // Load the dictionaries first (same as the SetUp of the existing component tests GamePanelTest and PanelActionButtonTest)
            Localize.Initialize();
            _layers = UiLayerStack.Build(rootVisualElement);
        }

        /// <web-test file="src/features/notification/notificationLayering.test.ts">通知ホストは背面層のトークンを使い、最前面のトースト層を使わない</web-test>
        [Test]
        public void 通知ホストは背面の層を使いトーストの層を使わない()
        {
            Assert.AreEqual(UiLayer.BehindStage, NotificationLayerPresenter.HostLayer);
            Assert.AreNotEqual(UiLayer.Toast, NotificationLayerPresenter.HostLayer);
        }

        /// <web-test file="src/features/notification/notificationLayering.test.ts">通知はPortalの外、stageより前のDOM位置に描かれる</web-test>
        [Test]
        public void 通知の層はPortalの層と舞台の層より前に描かれる()
        {
            var notificationIndex = IndexOfLayer(NotificationLayerPresenter.HostLayer);
            foreach (var later in new[] { UiLayer.ViewportStage, UiLayer.Screen, UiLayer.WorldPin, UiLayer.Modal, UiLayer.Toast, UiLayer.Tutorial })
            {
                Assert.Less(notificationIndex, IndexOfLayer(later), later.ToString());
            }
        }

        /// <web-test file="src/features/notification/notificationLayering.test.ts">通知は実画面へ固定され、stage拡縮に追従しない</web-test>
        [Test]
        public void 通知は実画面の族へ置かれ縦の中央線に固定される()
        {
            Assert.AreEqual(UiFamily.Viewport, NotificationLayerPresenter.HostFamily);
            var host = MountHostWithRow();
            Assert.AreEqual(Position.Absolute, host.resolvedStyle.position);
            Assert.AreEqual(rootVisualElement.layout.height * 0.5f, host.worldBound.y, 0.5f);
        }

        /// <web-test file="e2e/tests/notification/layering.spec.ts">インベントリを開いている間、通知はパネルの描画を一切変えない</web-test>
        [Test]
        public void 画面の層のパネルは通知の上に描かれ通知はその入力も奪わない()
        {
            var host = MountHostWithRow();
            var panelProbe = new VisualElement { name = "screen-panel-probe" };
            panelProbe.style.position = Position.Absolute;
            panelProbe.style.left = 0;
            panelProbe.style.top = 0;
            panelProbe.style.right = 0;
            panelProbe.style.bottom = 0;
            _layers.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(panelProbe);
            simulate.FrameUpdate();

            // 画面の層は後に描かれ、通知の行の上で拾われるのは画面のパネル
            // The screen layer draws later, and the screen panel is what gets picked over the notification row
            Assert.Less(IndexOfLayer(UiLayer.BehindStage), IndexOfLayer(UiLayer.Screen));
            Assert.AreSame(panelProbe, rootVisualElement.panel.Pick(host[0].worldBound.center));
        }

        /// <web-test file="e2e/tests/notification/layering.spec.ts">通知ホストはstageより後ろの層に立つ</web-test>
        [Test]
        public void 通知ホストは舞台より後ろの層に立つ()
        {
            var host = MountHostWithRow();
            var hostLayerContainer = _layers.GetLayer(UiLayer.BehindStage, UiFamily.Viewport).parent;
            Assert.IsTrue(hostLayerContainer.Contains(host));
            Assert.Less(IndexOfLayer(UiLayer.BehindStage), IndexOfLayer(UiLayer.ViewportStage));
        }

        private NotificationHostView MountHostWithRow()
        {
            var model = new NotificationModel();
            var host = new NotificationHostView(model, new FakeNotificationTextLookup(), new FakeNotificationRowIcons());
            _layers.GetLayer(NotificationLayerPresenter.HostLayer, NotificationLayerPresenter.HostFamily).Add(host);
            simulate.FrameUpdate();
            model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            host.Render(model.Current.Value);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(300);
            return host;
        }

        private int IndexOfLayer(UiLayer layer)
        {
            return rootVisualElement.IndexOf(_layers.GetLayer(layer, UiFamily.Viewport).parent);
        }
    }
}
```

（Task 6 Step 2 で全部隠しの L1 を足すと決めた場合は、このファイルに `全部隠しの間は通知のホストが見えない` を 1 件足す〈`MountHostWithRow` で行を出し、S2-skit の実物の隠しの口を呼んで `notification-host` の `resolvedStyle` が隠れの状態であること、解くと戻ることを確かめる。UITK だけでタグ無し〉。`UiLayerStack` が層の入れ物を root の直下に並べる実物であることは `UiLayerStackTest` で確かめる。入れ物の親が root でなければ `IndexOfLayer` を入れ物の共通の親で数える形にする）

- [ ] **Step 2: Presenter を書く**（`moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification/NotificationLayerPresenter.cs`）

```csharp
using System;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Text;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Notification
{
    /// <summary>
    /// 背面の層（舞台の裏）の実画面の族へ通知のホストを常駐させ、読み口の行の一覧を描かせ、書き口をホストへ渡す（Web App.tsx の stage より前の NotificationHost）
    /// Keeps the notification host resident in the viewport family of the behind-stage layer, renders the read side's rows and hands the write side to the host (Web App.tsx NotificationHost before the stage)
    /// </summary>
    public sealed class NotificationLayerPresenter : IInitializable, IDisposable
    {
        internal const UiLayer HostLayer = UiLayer.BehindStage;
        internal const UiFamily HostFamily = UiFamily.Viewport;

        private readonly GameUiRoot _gameUiRoot;
        private readonly INotificationListSource _source;
        private readonly INotificationListWriter _writer;
        private readonly INotificationTextLookup _textLookup;
        private readonly INotificationRowIcons _icons;
        private readonly StageScaleSource _stageScaleSource;
        private NotificationHostView _host;
        private IDisposable _subscription;
        private IDisposable _scaleSubscription;

        public NotificationLayerPresenter(GameUiRoot gameUiRoot, INotificationListSource source, INotificationListWriter writer, INotificationTextLookup textLookup, INotificationRowIcons icons, StageScaleSource stageScaleSource)
        {
            _gameUiRoot = gameUiRoot;
            _source = source;
            _writer = writer;
            _textLookup = textLookup;
            _icons = icons;
            _stageScaleSource = stageScaleSource;
        }

        public void Initialize()
        {
            // 中身は PanelSettings の倍率で舞台と同率、倍率の外の左端だけ StageScaleSource を購読して実画面 16px に保つ（Web の left: 1rem＋中身の scale(var(--ui-scale))）
            // Contents scale with the stage via PanelSettings; only the unscaled left edge follows StageScaleSource to stay 16 real px (Web left: 1rem + contents scale(var(--ui-scale)))
            _host = new NotificationHostView(_writer, _textLookup, _icons);
            _gameUiRoot.GetLayer(HostLayer, HostFamily).Add(_host);
            _scaleSubscription = _stageScaleSource.Scale.Subscribe(_host.SetStageScale);
            _subscription = _source.Current.Subscribe(_host.Render);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _scaleSubscription?.Dispose();
            _host?.RemoveFromHierarchy();
        }
    }
}
```

- [ ] **Step 3: DI 登録を書く**（`moorestech_client/Assets/Scripts/Client.Starter/Registration/GameUi/NotificationGameUiRegistration.cs`）

```csharp
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Text;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // 通知（GameUi）の登録。Feed は両方の系で MainGameUiStateRegistration が登録済み
    // Registers GameUi notifications; the feed is already registered for both systems by MainGameUiStateRegistration
    internal static class NotificationGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            // 1 つの Model を読み口と書き口の 2 つの型で配る（読むのは Presenter、書くのは中継とホスト）
            // Hand out one model under the read and write interfaces (the presenter reads; the relay and host write)
            builder.Register<NotificationModel>(Lifetime.Singleton).As<INotificationListSource>().As<INotificationListWriter>();
            builder.RegisterEntryPoint<NotificationFeedRelay>();
            builder.Register<LocalizeNotificationTextLookup>(Lifetime.Singleton).As<INotificationTextLookup>();
            builder.Register<GameContentNotificationRowIcons>(Lifetime.Singleton).As<INotificationRowIcons>();
            builder.RegisterEntryPoint<NotificationLayerPresenter>();
        }
    }
}
```

（`NotificationModel` 自体を引く利用者は無い。`.As<…>()` を並べると具象型で引けなくなるのは VContainer の仕様どおりで、それで足りる）

- [ ] **Step 4: 画面登録区画に 1 行足す**（`moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs` の `// 画面登録区画` の並びの末尾）

```csharp
            NotificationGameUiRegistration.Register(builder);
```

- [ ] **Step 5: コンパイルと UI 全体の回帰**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\." --timeout-seconds 1500`
Expected: ErrorCount 0 / 全件 PASS（通知の 81 件〈L0 41・Motion 11・Screens 24・Layering 5〉を含む。全部隠しの L1 を足したら 82）

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification moorestech_client/Assets/Scripts/Client.Starter/Registration moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Notification
git commit -m "feat(uitk): 通知のホストを背面の層へ常駐させ GameUi の系に登録する（S2-notification）"
```

---
### Task 10: 参照との並べ比べ（720p・2560×1080）と処遇

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-notification-compare.cs`
- Modify: `docs/uitk/disposition.md`（「## 処遇」の末尾に 1 行）
- Modify: `docs/uitk/TODO.md`（差を例外にするときだけ「## 見た目の例外」の末尾に `LX-39`〜`LX-41`。実測を書くときだけ「## 実測の結論」の末尾）
- 直すものが出たら: `Notification.uss`・`NotificationRowView.cs`・`NotificationHostView.cs`

**Interfaces:**
- Consumes: 参照 `../moorestech_logs/uitk/reference-captures/web-a9d5123537/2-notifications.png`（repo の兄弟の `moorestech_logs`。1280×720・日本語・GameScreen。行は獲得〈item 2 ×5〉・研究完了・アイテム解放〈item 2〉・拒否〈研究を完了できません〉）と動画 `2-notification-enter-aggregate-exit.webm`（12 秒）、Web の撮影 `moorestech_web/webui/e2e/uitk-reference/capture.ts`（`UITK_REFERENCE_OUT` に空のディレクトリ。解像度は `shotRunner.ts` の `referenceViewport` 固定）、プレイテスト DSL（`SetupDebugEnvironment`・`SkipOpeningSkitIfPlaying`・`ExitToGameScreen`・`Screenshot`・`WaitSeconds`・`Assert`）、`ClientDIContext.ClientLocalNotificationSource.Notify(NotificationMessagePack)`、`WebUiHost.Hub`（`Client.WebUiHost.Boot`）
- Produces: done.md の「並べ比べ」表と画像の組、disposition の 1 行

- [ ] **Step 1: 全体スイッチが UITK であることを確かめる**（L3・録画の前に毎回。UI のテストの TearDown が既定の Web へ戻すため）

Run: `uloop control-play-mode --project-path ./moorestech_client --action Stop` → `uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'`
Expected: 戻り値 `GameUi`

- [ ] **Step 2: 並べ比べの撮影シナリオを書く**（`.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-notification-compare.cs`）

```csharp
// 通知の並べ比べ撮影: GameUi（UITK）で参照 2-notifications と同じ 4 行（獲得・研究完了・アイテム解放・拒否）を日本語で出して 1280×720 の画面を撮り、PanelSettings の複製に描いた通知の層を 1280×720 と 2560×1080 で撮る
// Notification comparison shots: in GameUi (UITK) show the same four rows as reference 2-notifications in Japanese, shoot the 1280x720 screen, then shoot the notification layer drawn on a PanelSettings copy at 1280x720 and 2560x1080
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Localization;
using Client.Playtest;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Notification.Params;
using Client.Ui.State.Notification.Text;
using Client.WebUiHost.Boot;
using Core.Master;
using Cysharp.Threading.Tasks;
using Server.Event.Notification;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("uitk-notification-compare", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    p.Assert(WebUiHost.Hub == null, "Webホストは起動していない（GameUi）");

    // 参照の元データ（W/e2e/mock-host/topics/topicControls.ts の notificationItemEarned・notificationAchievement・notificationItemUnlocked・notificationDenied）と同じ順・同じ中身。研究完了はアイテム 1 のアイコン付き
    // Same order and contents as the reference source data (topicControls.ts notification controls); research completed carries the item 1 icon
    Localize.TrySetChosenLanguage("japanese");
    var research = MasterHolder.ResearchMaster.GetAllResearches().First();
    var earned = NotificationMessagePack.CreateItemEarned(new ItemId(2), 5);
    var researchCompleted = NotificationMessagePack.CreateAchievementWithItem("achievement.researchCompleted", new Dictionary<string, string> { ["research"] = research.ResearchNodeGuid.ToString() }, new ItemId(1));
    var unlocked = NotificationMessagePack.CreateAchievementWithItem("achievement.unlockedItem", new Dictionary<string, string>(), new ItemId(2));
    var denied = NotificationMessagePack.CreateOperationDenied("denied.researchNotCompletable", new Dictionary<string, string>());

    // 実画面（世界ごと）の 720p。色はこの撮影で比べる（研究名は実マスタの最初の研究なので、文の長さだけ参照と違い得る）
    // The real 720p screen including the world; colors are compared here (the research name is the real master's first research, so only the text length may differ)
    foreach (var message in new[] { earned, researchCompleted, unlocked, denied }) ClientDIContext.ClientLocalNotificationSource.Notify(message);
    await p.WaitSeconds(0.6f);
    await p.Screenshot("uitk-2-notifications-1280x720");

    // 位置と寸法は PanelSettings の複製に通知の層だけを描いて 2 解像度で撮る（本番の資産は書き換えない。S1-theme の撮影の前例）
    // Positions and sizes come from the notification layer alone drawn on a PanelSettings copy at two resolutions (the production asset is never rewritten; S1-theme capture precedent)
    p.Note($"1280x720 の複製パネル: {await CaptureOnPanelCopy(1280, 720, "uitk-2-notifications-panel-1280x720.png")}");
    p.Note($"2560x1080 の複製パネル: {await CaptureOnPanelCopy(2560, 1080, "uitk-2-notifications-panel-2560x1080.png")}");
    Localize.TrySetChosenLanguage("english");

    // 複製の PanelSettings に RenderTexture を付けた UIDocument を作り、層の並びの背面の層へ通知のホストを載せて 4 件を積み、入場の後に ReadPixels する
    // Make a UIDocument on a PanelSettings copy with a RenderTexture, mount the notification host in the behind-stage layer, push the four rows and ReadPixels after the enter
    async UniTask<string> CaptureOnPanelCopy(int width, int height, string fileName)
    {
        var panelCopy = Object.Instantiate(GameUiRoot.Instance.GetComponent<UIDocument>().panelSettings);
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        panelCopy.targetTexture = target;
        var documentObject = new GameObject($"notification-compare-{width}x{height}");
        var document = documentObject.AddComponent<UIDocument>();
        document.panelSettings = panelCopy;
        await UniTask.DelayFrame(1);

        // 本番のルートと同じロケールのクラスを当てる（GameUiLocaleClassBinder が GameUiRoot に当てるもの。日本語は CJK の字形と行送り。Typography.uss）
        // Apply the same locale class the production root gets (what GameUiLocaleClassBinder applies to GameUiRoot; Japanese uses CJK glyphs and leading; Typography.uss)
        document.rootVisualElement.EnableInClassList(GameUiLocaleClass.CjkClassName, GameUiLocaleClass.IsCjk(Localize.GetCurrentLanguageCode()));

        var model = new NotificationModel();
        var host = new NotificationHostView(model, new LocalizeNotificationTextLookup(), CompareRowIcons());
        UiLayerStack.Build(document.rootVisualElement).GetLayer(UiLayer.BehindStage, UiFamily.Viewport).Add(host);
        // 複製のパネルは解像度が固定なので、その倍率を 1 回だけ渡して左端を実画面 16px に置く
        // The copy panel has a fixed resolution, so pass its scale once to put the left edge at 16 real px
        host.SetStageScale(document.rootVisualElement.panel.scaledPixelsPerPoint);
        model.Current.Subscribe(host.Render);
        // 研究完了の行は参照のモックの研究名（W/e2e/mock-host/fixtures/contentLocalizationFixtures.ts の「最初の研究」）を文字の引数で渡し、参照と同じ文にする
        // The research row passes the reference mock's research name ("最初の研究" in contentLocalizationFixtures.ts) as a text argument so the text matches the reference
        model.AddNotification(NotificationContentFactory.FromMessage(earned));
        model.AddNotification(new AchievementNotificationContent("achievement.researchCompleted", new NotificationParam[] { new TextNotificationParam("research", "最初の研究") }, new ItemNotificationIcon(new ItemId(1))));
        model.AddNotification(NotificationContentFactory.FromMessage(unlocked));
        model.AddNotification(NotificationContentFactory.FromMessage(denied));
        await UniTask.Delay(600);

        var previousActive = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();
        RenderTexture.active = previousActive;
        Object.Destroy(documentObject);
        Object.Destroy(panelCopy);
        target.Release();

        var outputDirectory = Path.Combine(Application.dataPath, "..", "..", ".superpowers", "uitk-notification-compare");
        Directory.CreateDirectory(outputDirectory);
        var path = Path.GetFullPath(Path.Combine(outputDirectory, fileName));
        File.WriteAllBytes(path, texture.EncodeToPNG());
        return path;
    }

    // 本番と同じ素材の口で C3-a のアイコンを引く（C3-a 計画 Task の Produces。C3-a マージ後に実物と照合）
    // Resolve icons through the same texture source as production (C3-a plan Produces; verify against the real code after C3-a merges)
    INotificationRowIcons CompareRowIcons()
    {
        return new GameContentNotificationRowIcons(new GameContentPresentation(new ClientContextContentTextureSource()));
    }
});
```

（`.superpowers/` は gitignore 下。本番の `GameUiPanelSettings.asset` には触らず、`Object.Instantiate` した複製に `targetTexture` を付けて描く。S1-theme の撮影〈`../moorestech_logs/harness/sdd/feature-uitk-s1-theme/workspace/task-5-brief.md` の Step 7・`task-5-report.md` の「撮影」〉が同じ方式で 720p と 2560×1080 を実際に撮っている。複製のパネルは GameUi の木を持たないので、通知のホストをその場で層の並びの背面の層に載せて複製のパネルの倍率を `SetStageScale` へ 1 回渡し〈本番の Presenter と同じ層・族。本番は `StageScaleSource` を購読〉、ルートに本番と同じロケールのクラス〈`GameUiLocaleClass.CjkClassName`。`S/Client.Ui/Foundation/Text/GameUiLocaleClassBinder.cs`・`Typography.uss` の CJK の規則〉を当てる。3 計画で同じ形。`CompareRowIcons` は C3-a 計画の Produces（`GameContentPresentation(IGameContentTextureSource)`・引数なしの `ClientContextContentTextureSource`）を写した形で、C3-a マージ後に実物と照合する。複製の撮影は世界が写らないので位置と寸法〈左端・幅・縦の中央線〉を比べ、色は実画面の 720p で比べる。撮影の後に `git status` で `GameUiPanelSettings.asset` に変更が無いことを見る。D-8）

- [ ] **Step 3: UITK を撮る**

Run: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-notification-compare.cs`
Expected: `result.json` が成功、1280×720 の実画面のスクショと `.superpowers/uitk-notification-compare/uitk-2-notifications-panel-1280x720.png`・`uitk-2-notifications-panel-2560x1080.png`。`git status` に `GameUiPanelSettings.asset` の変更が無い。1 回目に "already playing" で落ちたら 1 回だけ再実行する

- [ ] **Step 4: Web の 2560×1080 を撮る**（参照は 1280×720 しか無い）

Run（このレーンの worktree の Web で撮る。`shotRunner.ts` の書き換えはコミットしない）:
```bash
cd moorestech_web/webui
sed -i '' 's/referenceViewport = { width: 1280, height: 720 }/referenceViewport = { width: 2560, height: 1080 }/' e2e/uitk-reference/shotRunner.ts
OUT="$(git rev-parse --show-toplevel)/.superpowers/web-reference-2560x1080" && mkdir -p "$OUT"
UITK_REFERENCE_OUT="$OUT" npx tsx e2e/uitk-reference/capture.ts
git checkout -- e2e/uitk-reference/shotRunner.ts
```
Expected: `$OUT/2-notifications.png`（2560×1080）。`git status` に `shotRunner.ts` の変更が残っていない。`node_modules` が無ければ `npm ci` を先に回す（`docs/development/worktree-and-review-pitfalls.md`）

- [ ] **Step 5: 並べて差を読む**

参照 720p と UITK 720p、Web 2560 と UITK 2560 を並べ、次を測って done.md の「並べ比べ」表に 1 行ずつ書く（列: 項目／Web／UITK／差／判定〈同じ・直した・LX-n〉）。合格の目安は 720p で 2px 以内（設計書 §3.3）。

| 項目 | Web の値（720p） | 2560×1080 での Web の値 |
|---|---|---|
| ホストの左端 | x=16 | x=16（倍率を掛けない） |
| 最上段の行の上端 | y=360（縦の中央線） | y=540 |
| 行の幅 | 256 | 384（×1.5） |
| 文字だけの行の高さ | 30 | 45 |
| 行間 | 10 | 15 |
| アイコン | 22×22、文の左 8px | 33×33 |
| 面の色 | `--notification-face` | — |
| 枠 | 1px `rgb(104,106,120)` | 1.5px 相当 |
| 文字の色 | 白・拒否は `#ff7878` | — |
| 字間 | 0.825px | — |

色・配置・装飾・文字の大きさの差は例外にせず直す（設計書 §3.4）。直したら Task 8 Step 8 のテストを流し直し、撮り直す。例外にしてよい差（同じにする対応が大きく、差が一目で分からないもの。候補: 文字影の 0.35px のにじみ、`overflow-wrap: anywhere` の語の途中の折り返し〈UITK は語の境で折る〉）は台帳「## 見た目の例外」の末尾に `LX-39`〜`LX-41` で書き、USS の該当行に `/* look-exception: LX-n */` を付ける

- [ ] **Step 6: 動画と見比べる**（出入りの目視）

参照 `2-notification-enter-aggregate-exit.webm` の入場（左からのスライドとフェード）・合流（数が伸びて動きが頭から）・全退出と、Task 11 の UITK の録画を見比べ、done.md に所見を 3 行以内で書く

- [ ] **Step 7: 写さない 1 件を処遇へ書く**（`docs/uitk/disposition.md` の「## 処遇」の末尾）

```markdown
| 2 | `src/features/notification/notificationStore.test.ts` | 退場アニメが発火しなくても保険タイマーで消える | 捨てる | UITK の通知ホストは背面の層に常駐し、外れるのは Dispose だけなので退場の終わりが必ず来る。保険タイマーを持たない（Web の保険は React の unmount で onAnimationEnd が来ない場合のもの）。外れた場合は NotificationHostView の LogWarning が知らせる | — |
```

（PR 番号は Task 14 で埋める）

- [ ] **Step 8: 撮影を share に置き、コミットする**

Run: 参照 720p と Web 2560 を `.superpowers/uitk-notification-compare/` へ写してから `share-put .superpowers/uitk-notification-compare -n uitk-s2-notification-compare` → 出た URL を done.md と disposition の行に書く

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage2/uitk-notification-compare.cs docs/uitk/disposition.md docs/uitk/TODO.md moorestech_client/Assets/Scripts/Client.Ui/Screens/Notification
git commit -m "test(uitk): 通知を参照と 720p・2560×1080 で並べ比べ、保険タイマーの 1 件を捨てるに記帳する（S2-notification）"
```

---

### Task 11: 録画 `server-notification-smoke` を直して UITK と Web で通す（bd moorestech-wwxy）

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/server-notification-smoke.cs`

**Interfaces:**
- Consumes: `MasterHolder.ResearchMaster.GetAllResearches()`（`ResearchNodeMasterElement`: `ResearchNodeGuid`・`PrevResearchNodeGuids`・`ConsumeItems`〈`ItemGuid`・`ItemCount`〉）、`MasterHolder.ItemMaster.GetItemMaster(Guid).Name`、`p.GiveItemDirect(string, int)`、`PlaytestUiElementOps.FindUi(string)`・`WaitUiTextContains(string testid, string expected, float timeoutSeconds)`、`Localize.GetContent(ContentLocalizationKeys.ResearchName(Guid))`、`GameUiRoot.Instance.GetLayer(UiLayer, UiFamily)`、`WebUiHost.Hub`
- Produces: 現行マスタで動く録画シナリオ（⚠ 見出しを外す）

現状: 削除済みの研究 GUID `837e9697…` を送るのでサーバーで null 参照になり止まる（台帳「## 壊れた録画の割り振り」の行。割り振りは S2-ladder だが「S2-notification が先に直す」）。直し方: 研究を固定 GUID でなくマスタから選ぶ（前提の研究が無く、消費アイテムを持つ最初のノード。ピン先 master〈`pin-e0c91303`・server_v8〉では `858bcb10-b8ba-478e-9bc5-473ca61281a2` 原始研究1）。素材も名前の直書き（木の板・木の棒）をやめ、そのノードの `ConsumeItems` から付与する。GameUi のときは、通知が背面の層に出たことと研究名の入った文言を確かめる。

- [ ] **Step 1: シナリオを書き直す**（⚠ の 2 行を消して見出しを次に置き換え、研究の選び方と付与を差し替え、GameUi の確認を足す。クールダウンの検証と観測用購読はそのまま残す）

見出し:

```csharp
// サーバー発通知基盤スモーク（GameUi〈UITK〉と Web の両方で通す）
// 1) 素材ゼロで研究完了を試みる→操作拒否（operationDenied）通知が出る
// 2) 同一失敗を 3 秒以内に連打→クールダウンで 2 回目はワイヤに乗らない（通知は増えない）
// 3) 素材を付与して研究を完了→実績（achievement）通知「研究完了: <研究名>」が出る
// 研究はマスタから選ぶ（前提の研究が無く消費アイテムを持つ最初のノード）。GameUi のときは通知が背面の層に出たことと文言も確かめる
// Server-notification smoke (run on both GameUi (UITK) and Web):
// (1) complete-research with no materials -> an operation-denied notification
// (2) burst the same failure within 3s -> the cooldown suppresses the 2nd delivery
// (3) grant the materials and complete -> an achievement notification "Research completed: <name>"
// The research is picked from the master (the first node with no prerequisites and some consumed items); on GameUi it also checks the behind-stage layer and the text
```

研究の選び方（固定 GUID の行を置き換える）:

```csharp
    // 前提の研究が無く、消費アイテムを持つ最初のノードを選ぶ（GUID を直書きするとマスタの改訂で壊れる）
    // Pick the first node with no prerequisites and some consumed items (a hard-coded GUID breaks on master revisions)
    var firstResearch = MasterHolder.ResearchMaster.GetAllResearches()
        .First(node => node.PrevResearchNodeGuids.Length == 0 && node.ConsumeItems.Length > 0);
    var firstResearchGuid = firstResearch.ResearchNodeGuid;
    p.Note($"研究ノード: {firstResearchGuid}");
```

素材の付与（「木の板」「木の棒」の 2 行を置き換える）:

```csharp
    p.Note("研究に必要な素材をマスタの ConsumeItems どおりに付与する");
    foreach (var consume in firstResearch.ConsumeItems) await p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(consume.ItemGuid).Name, consume.ItemCount);
    await p.WaitSeconds(0.5f);
```

（`PrevResearchNodeGuids`・`ConsumeItems` の型〈配列か List か〉と optional かどうかは `Mooresmaster.Model.ResearchModule` の生成物を読んで合わせる。optional なら null を「前提無し／消費無し」として扱う形にする）

実績通知の撮影の直前（2 回目の `await UntilHudVisible("notification-host", 10f);` の次の行）に、GameUi のときだけの確認を足す:

```csharp
    // GameUi では通知が背面の層の実画面の族に居て、研究名の入った文言が出ていることを確かめる
    // On GameUi, check the host sits in the viewport family of the behind-stage layer and shows the text with the research name
    if (WebUiHost.Hub == null)
    {
        var behindStage = GameUiRoot.Instance.GetLayer(UiLayer.BehindStage, UiFamily.Viewport);
        p.Assert(behindStage.Q("notification-host") != null, "通知ホストは背面の層（BehindStage・Viewport）に居る");
        var researchName = Localize.GetContent(ContentLocalizationKeys.ResearchName(firstResearchGuid));
        await Client.Playtest.Operations.PlaytestUiElementOps.WaitUiTextContains("notification-host", researchName, 10f);
    }
```

`using` に `Client.Localization`・`Client.Ui.Foundation.Layers`・`Client.Ui.Foundation.Root`・`Client.Ui.Foundation.Stage`・`Client.WebUiHost.Boot`・`Core.Master`・`Mooresmaster.Localization.Generated`・`UnityEngine.UIElements` を足す。

- [ ] **Step 2: GameUi（UITK）で通す**

Task 10 Step 1 の手順で全体スイッチが `GameUi` であることを確かめてから:
Run: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/server-notification-smoke.cs`
Expected: `result.json` が成功（拒否・クールダウン・実績・背面の層・文言の Assert が全部通る）、録画 mp4 がある。1 回目に "already playing" で落ちたら 1 回だけ再実行する

- [ ] **Step 3: Web で通す**

Run: `uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.Web); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'`（戻り値 `Web`）→ 同じ run-scenario.sh
Expected: `result.json` が成功（GameUi だけの確認は飛ばされ、Web の `notification-host` の矩形で通る）

- [ ] **Step 4: 証跡を残してコミットする**

GameUi 回の録画を `share-put` し、URL を done.md に書く（報告ページにも載せる）。録画・`result.json` は worktree の gitignore 下に置き、`moorestech_logs` へ入れない。done.md に「台帳『## 壊れた録画の割り振り』の server-notification-smoke 行は直した（S2-ladder は通すだけでよい）」と書く（行の書き換えは S2-ladder か integrator。このレーンは台帳の行を書かない）

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/server-notification-smoke.cs
git commit -m "fix(playtest): server-notification-smoke の研究をマスタから選び、GameUi では背面の層と文言も確かめる（bd moorestech-wwxy。S2-notification）"
```

---

### Task 12: テスト名の突き合わせ・回帰・done.md

**Files:**
- Modify: `docs/uitk/TODO.md`（「## 追従記録」の末尾。束 2 が暗転中で、G1 が通知の Web パスの未記帳を出したときだけ）
- 書く（このブランチにコミットしない）: `../moorestech_logs/harness/uitk/S2-notification/done.md`

- [ ] **Step 1: テスト名を突き合わせる**

Run: `node tools/uitk/test-name-parity.mjs --bundle 2 --emit-missing | grep -E "notification|portalLayerScaling"` と `node tools/uitk/test-name-parity.mjs --bundle 5 --emit-missing | grep -E "portalLayerScaling.*通知"`
Expected: 通知の単体 5 ファイル（`src/features/notification/*.test.ts`。`notificationLayering.test.ts` を含む）・e2e 4 ファイル・`portalLayerScaling.spec.ts` の通知の 1 件が 1 行も出ない（ほかのレーンの持ち分の未対応は数えない）。出たらタグの綴り（`file=` のパス・テスト名の全角半角）を直す

- [ ] **Step 2: 全 UI テストと既存の通知の回帰**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\." --timeout-seconds 1500` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(WireContractNotification|NotificationStateProbe|LookExceptionLedger)"`
Expected: ErrorCount 0 / 全件 PASS。本計画のテストは 81 件（L0 41・Motion 11・Screens 24・Layering 5。全部隠しの L1 を足したら 82）

- [ ] **Step 3: G1 の未記帳を見る**

Run: `python3 .github/scripts/check-uitk-web-follow.py --base origin/feature/uitk --head HEAD`（引数は台帳「## 読み方と運用」に従う）
Expected: 束 2 の未記帳 0。束 2 が暗転中で通知の Web パスを触ったコミットが取り込まれていれば、「## 追従記録」の末尾に `写した`（写し先）か `捨てた`（理由）で 1 行ずつ書いてコミットする

- [ ] **Step 4: done.md を書く**（`../moorestech_logs/harness/uitk/S2-notification/done.md`。1 行目は `状態: 完了`）

中身: 完了条件（逐語「通知のストアを C# のモデルへ（`NOTIFICATION_DISPLAY_MS` 参照、入場 160ms・退場 200ms）、背面の層。写す: notification 47（P6・L26・S15）。録画 `server-notification-smoke`（wwxy を直して）」）ごとの証拠（テストの件数と緑のログの行・並べ比べの表と URL・録画の URL・`result.json` のパス）、写したテストの内訳（本計画「写すテストの対応」の表。台帳 47・`it` の行 48・展開 50・うち捨てる 1・UITK だけ 23。実行計画の「実数 48」との差も書く）、C3-a の実物との差（Task 6 Step 1）、死活表の実測（Task 6 Step 2。S2-skit 未マージなら「S2-ladder の確認事項」）、未完了・要裁定、**integrator への依頼**:
- ref-uitk-design SKILL.md §9 の「通知の生存 7000ms をトークンに」を直す（生存尺は Model の定数 `NotificationModel.DisplayMilliseconds` が唯一の正で、USS・トークンに書かない。実行計画の完了条件「`NOTIFICATION_DISPLAY_MS` 参照」と Web の `notificationAnimationDesign.test.ts` に合わせる。D-14）
- E-19 で足した `UiEasing.CssEaseOut`・`CssEaseIn` と `TranslateXTweenStep` を設計書 §7.1 A-69 と部品カタログ（ref-uitk-design）に載せる
- 通知の文字影のトークン `--notification-text-shadow: 0.35px 0.35px 0 rgba(0, 0, 0, 0.8)`（今は同値の `--key-hint-text-shadow` を借りている）
- 台帳「## 行」の notification 行と「背面の通知層」行の実測・参照の列に、このレーンの並べ比べの URL を入れる

- [ ] **Step 5: 残りをコミットする**（Step 3 の追従記録があれば）

```bash
git add docs/uitk/TODO.md
git commit -m "docs(uitk): 束 2 の通知の追従を記帳する（S2-notification）"
```

---

### Task 13: moores-code-review で全ブランチレビュー

**必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。**

- [ ] **Step 1: レビューを回す**（対象: `origin/feature/uitk...HEAD`）
- [ ] **Step 2: 自動適用の後、compile と本計画のテストを流し直す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(State\.Notification|Screens\.Notification|Motion)\." --timeout-seconds 1500`
Expected: ErrorCount 0 / 全件 PASS（本計画の 81 件〈全部隠しの L1 を足したら 82〉と Motion の既存テスト）。見た目に効く修正が入ったら Task 10 Step 3〜5 を撮り直し、done.md の表を更新する

- [ ] **Step 3: 反映をコミットする**（レビュー記録は `../moorestech_logs/harness/` へ。feature ブランチに記録ファイルをコミットしない）

---

### Task 14: feature/uitk へ取り込む（agent が実施）

セッション終了可能状態にすること。master への PR は作らない。

- [ ] **Step 1: pr-create スキルで `base=feature/uitk` の PR を作る**。PR 本文の先頭行は報告ページ `# https://review.moores.tech/pr/<PR番号>/report/`（環境ローカル規約）。報告ページには並べ比べの組（`2-notifications` の Web 720p と UITK 720p、Web 2560×1080 と UITK 2560×1080。Web を先に、caption は `Web <撮影 id>`・`UITK 2-notifications <解像度>`）、done.md の差の表、`server-notification-smoke` の GameUi の録画を載せる。公開スクリプトが失敗したら完了報告しない。disposition の行の PR 列に番号を入れてコミットする
- [ ] **Step 2: `origin/feature/uitk` をこのブランチへマージする**。共有区画（tss の Screens 区画・`MainGameUiRegistration` の画面登録区画・台帳と disposition の末尾）の衝突は両方の行を採る。`uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(State\.Notification|Screens\.Notification|Motion)\." --timeout-seconds 1500` が ErrorCount 0 / 全件 PASS。push する
- [ ] **Step 3: G1 が緑で未記帳 0 を確かめ、`gh pr merge <PR番号> --merge` でマージコミットにする**（Squash 禁止）。CI の `BugReportSubmitKindTest.空白本文でも種別を先に解く` は既知の落ち（bd albof.22）なので、マージの条件は G1 の緑
- [ ] **Step 4: 証拠で完了を確かめる**: `git fetch origin && git log --oneline -3 origin/feature/uitk` にこの PR のマージコミット、done.md に完了条件ごとの証拠・未完了・要裁定
- [ ] **Step 5: `moores-wt rm uitk-lane/s2-notification`（必要なら `--prune-branch`）で worktree と Editor を畳み、bd のこのレーンのタスクを閉じる**。テストやシナリオで `Saves/` にワールドを作っていれば `list-work-saves.sh` で探して消す

---

## Self-Review

1. **Requirements coverage**: R1→Task 1、R2→Task 2、R3→Task 1（引数の型）・3、R4→Task 4、R5→Task 7・8・10、R6→Task 6・8、R7→Task 6 Step 2・9・11、R8→Task 8（言語の L1）、R9→各タスクのタグ・Task 8 Step 7b・Task 10 Step 7・Task 12 Step 1、R10→Task 10、R11→Task 11、R12→Task 5・13・14。漏れなし
2. **Placeholder scan**: 「TBD」「後で」は無い。`<PR番号>`・`<Step 8 で置いた…URL>` は実行時に決まる値。C3-a の API（`UiIcon`・`GameIcon`・`GameIconFallback`・`GameContentPresentation`）の名前空間は設計書からの推定で書き、Task 6 Step 1 で実物と照合してから書く手順にした（C3-a が未マージで実物が無いため）。全部隠しの L1 は S2-skit の実物の口に合わせて Task 6 Step 2 で書く（未マージなら書かない）
3. **Type consistency**: `NotificationModel()`・`DisplayMilliseconds`・`INotificationListSource.Current`・`INotificationListWriter.AddNotification`・`RemoveNotification(long)`、`NotificationRow(long, int, NotificationContent)`、`EarnedNotificationContent(string, IReadOnlyList<NotificationParam>, ItemId, int)`・`AchievementNotificationContent`・`OperationDeniedNotificationContent(string, IReadOnlyList<NotificationParam>, NotificationIcon)`・`SaveMigrationNotificationContent(string, IReadOnlyList<NotificationParam>)`、`NotificationParams.FromWire(string, IReadOnlyDictionary<string,string>)`、`NotificationText.Resolve(NotificationContent, INotificationTextLookup)`・`ResolveParams(IReadOnlyList<NotificationParam>, INotificationTextLookup)`・`BuildInterpolationValues(string, IReadOnlyList<LocalizationTextParam>)`、`NotificationFeedRelay(NotificationFeed, INotificationListWriter)`、`NotificationHostView(INotificationListWriter, INotificationTextLookup, INotificationRowIcons)`・`Render(NotificationListViewModel)`、`NotificationLayerPresenter(GameUiRoot, INotificationListSource, INotificationListWriter, INotificationTextLookup, INotificationRowIcons, StageScaleSource)`、`NotificationRowView.Show(NotificationRow, UiText, INotificationRowIcons)`、`NotificationRowTiming(float, float, float, float)`、`NotificationRowLifetime.CreateEnter/CreateExit/Play`、`UiEasing.CssEaseOut/CssEaseIn`、`TranslateXTweenStep(VisualElement, float, float)` を全タスクで同じ綴りで使っている。USS の受け直し名 4 つは USS・C#・テストで一致。C3-b の `StageScaleSource.Scale` と、L1 で倍率を外から与える ctor の形は Task 6 Step 1 で実物の名前に合わせる。ホスト側は `NotificationHostView.SetStageScale(float)`
4. **保留・縮退経路**: スタイル解決前の描画は最新の 1 つを預かり解決後に描く（一時的）。トークンが読めないときは `UssCustomStyleReader` が LogError を出し、出入り無しで通知を出す（届けるのを優先。トーストの前例と同じ）。ホストが外れて行の動きが止まると除去が来ない → `NotificationHostView` が行数つきの LogWarning を出す（常駐なので Dispose 以外では起きない。全部隠しの実物は Task 6 Step 2 で確かめる）。未知の messageId は「不明な通知」として画面に出て、網羅テストが落ちる。Guid の欠け・読めない値は `NotificationParams` が LogWarning を出して文字の引数のまま通し、Localize が `{name}` を残して LogError を出す。内容名が全言語に無ければ `LocalizeNotificationTextLookup` が LogError を出して欠けの印のまま、アイテムがマスタに無いか名前が無ければ LogError を出して「Item {itemId}」で出す（Web と同じ見え方）。中継の初期データ無しは正常（告知がまだ来ていない）。`RemoveNotification` の空振り・同じ epoch の再描画・左端の同値は `silent-ok` を書いた。撮影は PanelSettings の複製に描くので本番の資産は変わらない（Task 10）
5. **「決定的にする」選択規則**: 合流先は「同じアイテム・同じ messageId の表示中の行」の最初の 1 つ（Web の `find` と同じ。同じ条件の行は合流で 1 つに保たれるので 2 つ並ばない）。並べ比べの研究は `GetAllResearches().First()`、録画の研究は「前提無し・消費あり」の最初のノード（マスタの並び順で決まる）
6. **呼び出し側への責務漏出**: 文言の解決（キー・補間値・内容名）は `NotificationText` の 1 か所（View からは `TextOf` の 1 か所で呼ぶ）、Guid の引数の型付けは `NotificationParams.FromWire` の 1 か所、アイコンの解決は Content（`GameContentPresentation`）の 1 か所、生存尺は Model の定数 1 か所、出入りの尺とずれはトークン 1 か所、曲線は `UiEasing` の 1 か所。View は判定を持たず、中身の型（`OperationDeniedNotificationContent`）とアイコンの型（`ItemNotificationIcon`・`NoNotificationIcon`）を型パターンで読むだけ。書き口の利用者は中継と View の 2 つだけ

## 判断記録（ADR）

設計の出所: 裁定 `origin/master:.decisions/2026-11-06-UITK*.md`・`2026-10-26-UITKが安定するまでWeb-UIの変更を続け現行機能をすべてUITKへ移す.md`、設計書 `docs/uitk-migration/execution/component-architecture.md` §1.1・§2.1・§2.7・§3・§4・§7.1（A-67 時間の型・A-68 アイコンと文言・A-69 CSS の曲線と横移動の Step）、実行計画 §1.4 のレーン S2-notification 行・§6.2・E-19。出所の書き方: 「agent 既定」＝この計画の起草で決めたもの、「設計検査（fresh-context）→ agent 既定」＝設計検査の指摘を受けて直したもの、「指揮の割り当て（実行計画 E-19）」＝指揮が決めた割り当て。どれもユーザー裁定ではない。

| ID | 判断 | 理由・棄却した案 |
|---|---|---|
| D-1 | 行の中身は種別ごとの sealed な型（`EarnedNotificationContent`・`AchievementNotificationContent`・`OperationDeniedNotificationContent`・`SaveMigrationNotificationContent`）で表し、アイコンも型（`ItemNotificationIcon`・`NoNotificationIcon`）、引数も型（`TextNotificationParam`・`ContentNameNotificationParam`）で表す。Model は読み口 `INotificationListSource` と書き口 `INotificationListWriter` を実装し、View には書き口だけ、Presenter には読み口を渡す。State の型は実物（`ItemId`・`UiText`・`ContentLocalizationKey`）を使うので、L0 は Task 5 で Editor 枠を 1 回借りて回す。出所: 設計検査（fresh-context）→ agent 既定（Phase 2.6 の強 1・2） | Web の判別 union（数を持つのは獲得だけ）を型で写し、「獲得を獲得以外の型へ渡すと例外」「アイコン無しを null で表す」口を無くす。1 つの「獲得以外」の型にカテゴリを持たせる案（前版）: `ItemEarned` を渡すと例外になる口が残り、View がカテゴリから警告色を判定する。Model 1 つの型をそのまま View に渡す案: View が `AddNotification` まで呼べ、書く側が型から読めない。S12 で回すため State に独自の enum と id を作る案: `NotificationFeed` の実物と二重の型になる |
| D-2 | 時間（設計書 A-67）: 各行の生存（入場→保持→退場）は View が `UiTweenAnimation` 2 本（入場・遅延つき退場）をパネルの時計で回し、退場が終わったら書き口の `RemoveNotification(id)` を呼ぶ。除去の経路はこれだけで、**保険タイマーは持たない（案 a）**。出入りの尺とずれはトークンを画面 USS が受け直して C# が読み、生存尺は `NotificationModel.DisplayMilliseconds` だけが持つ。`Update()` は使わない。Web の「退場アニメが発火しなくても保険タイマーで消える」は `捨てる`。出所: 設計検査（fresh-context）→ agent 既定（配置検査の違反 1） | 案 a（保険無し）: ホストは背面の層に常駐し、外れるのは Presenter の Dispose だけなので退場の終わりは必ず来る。Web の保険は React の unmount（画面遷移・カットシーンで `NotificationHost` が外れる）で `onAnimationEnd` が来ない場合のためのもので、UITK に同じ外れ方は無い（D-3 と同じ理由で、起きない事態への備えを写さない）。外れた場合は View の LogWarning が知らせる。案 b（ホストの `schedule.Execute(...).StartingIn` に保険を載せる。トーストの前例）: ホストが外れるとスケジューラも止まるので、保険が要る唯一の場面（外れ）で保険も動かず、守るものが無い。前版の注入 `IScheduler` 案: Model に時間を持ち込み、UI の時計と別の時計で行を消す。Model がタイマーで 7 秒で消す案: 削除と退場が別の時計になり、Web のテストが戒める「行が消えた後も面が残る」が起きる |
| D-3 | 除去告知の 1 回出し（Web `claimSaveMigrationNotice`）は UITK では中継の「初期データ＋購読を同じ同期区間で 1 回だけ取る」で満たし、seq の既読表を作らない。Web の 2 件は中継のテストへ写す。出所: agent 既定 | Web は WebSocket の再購読（カットシーン明け・再接続）で snapshot が同じ seq を再提示するので既読表が要る。UITK の中継は DI の寿命中に 1 回だけ購読し、再提示が無い。既読表を写す案: 起きない再着に備えた状態が残る |
| D-4 | 文言（設計書 A-68）は `UiText` の辞書キー（`new UiText(LocalizationKey, params)`）で表し、研究名・チャレンジ題・アイテム名は解決済みの文字列として引数に入れる。言語が変わったら View が行ごとに解き直す（`TextOf` の 1 か所）。内容名とアイテム名は State の口 `INotificationTextLookup`（本番 `LocalizeNotificationTextLookup`）で引き、辞書・マスタの欠けは理由つきの LogError を出して Web と同じ見え方（内容名は欠けの印 `[!<key>]`、アイテムは `ui.common.itemFallback`「Item {itemId}」）で出す。出所: agent 既定（欠けの扱いは 設計検査（fresh-context）→ agent 既定。Phase 2.6 の強 3） | 通知の文は「辞書の型＋内容名の補間」で、`UiText` の `FromContent`・`FromVerbatim` では表せない。アイテム名を `GameContentPresentation.ItemName`（`UiText`）から取る案: `UiText` を引数の文字列へ入れる口が無く、State から呼べない。欠けを黙って既定値で埋める案: 気づく経路が無い。欠けで例外にする案: 通知 1 件の欠けで画面が止まる（Web も表示を続ける） |
| D-5 | アイコン（設計書 A-68）は中身の `NotificationIcon` の型で表し、C3-a の Content 層 `GameContentPresentation.Item(ItemId)` を Screens の口 `INotificationRowIcons`（本番 `GameContentNotificationRowIcons`）越しに使い、行は `GameIcon(GameIconFallback.IdText)` で描く。このため Task 6 以降は C3-a の取り込み後に始める（Task 1〜5 は依存しない）。出所: agent 既定（C3-a の依存は指揮が実行計画のレーン行へ足した） | C3-a 待ちの間に Screens で `ClientContext.ItemImageContainer` を直に引く仮の口を作る案: 置き換える負債が残り、設計書 A-4「ID からの解決は Content に 1 か所」に反する。アイコン無しで先に出す案: Web と見た目が違う |
| D-6 | CSS の `ease-out`・`ease-in`（cubic-bezier）は Foundation の `UiEasing` に `CssEaseOut`・`CssEaseIn` として足し（`UiEasingFunctions.Evaluate` に二分法の分岐）、横移動は新規 `Foundation/Motion/Steps/TranslateXTweenStep.cs` に置く（設計書 A-69）。行の出入りはトーストの前例どおり `UiTweenAnimation`＋Step で書き、Screens は Foundation の Step を束ねる `NotificationRowFadeSlideStep` と 2 本を組む `NotificationRowLifetime` だけを持つ。出所: 指揮の割り当て（実行計画 E-19） | `UiEasing` の既存 3 値は Web の CSS 既定曲線と値が違う（ease-out の x=0.5 で 0.6846 に対し EaseOutCubic は 0.875）。前版の `Screens/Notification/Parts/CssTimingCurve.cs`＋スタイルを書く独自の `IUiAnimation`: Web の CSS アニメを写す次の画面で同じ曲線が二重に生まれ、Foundation の tween の前例から外れる |
| D-7 | ホストは `UiLayer.BehindStage` の `UiFamily.Viewport` に置き、`top: 50%`。中身（行の幅・文字）は PanelSettings の倍率で舞台と同率に拡縮する（Web の中身の `scale(var(--ui-scale))`）。倍率の外にある左端（Web `.host` の `left: 1rem`）だけは、Presenter が C3-b の `StageScaleSource.Scale`（設計書 A-41。解像度の変更で値が変わる）を購読してホストの `SetStageScale` へ流し、`style.left = 16 ÷ 倍率`（16 は画面 USS の `--notification-host-screen-left`）で実画面 16px に保つ。倍率 1→1.5 で `left` が 16/1.5 になることを L1 で確かめる。行間は行の `margin-bottom: 10px`、新着は `Insert(0)` で最上段。出所: 指揮の決定（設計書 A-41 の改訂 690f6a5215） | Web は `position: fixed; left: 1rem; transform: scale(var(--ui-scale)); transform-origin: left top`。USS に `gap` が無く、`column-reverse` の代わりが要る。ホストを `UiScreenPixelScope` に入れて中身に倍率を掛け直す案: 打ち消して掛け直す二重の変換になり、A-41 の「通知は打ち消さない側」に反する。`GeometryChangedEvent` で `panel.scaledPixelsPerPoint` を読み直して再計算する案: Expand で 16:9 どうしの解像度変更ではパネルの論理寸法が変わらず事象が来ないので、古い倍率のまま残る（1280×720→1920×1080 で左端が 24 実 px、1920×1080→1280×720 で約 10.7 実 px）。左を 16 パネル px のままにする案: 2560×1080（倍率 1.5）で 24 実画素になり 2px の合格目安を外す。C3-b に `StageScaleSource` が無い・解像度の変更で値が変わらないと分かったら Task 6 Step 1 で止めて指揮へ返す |
| D-8 | 並べ比べの UITK の撮影は、色を実画面の 720p（PlayMode の画面・世界ごと）で、位置と寸法を本番の `GameUiPanelSettings.asset` を `Object.Instantiate` した複製に `targetTexture`（1280×720・2560×1080 の RenderTexture）を付けた UIDocument で撮る。複製のパネルには背面の層へ通知のホストをその場で載せ、参照の元データ（`topicControls.ts` の通知の 4 件）と同じ中身を積んで ReadPixels する。本番の資産は書き換えない。Web の 2560×1080 はこのレーンの worktree で `shotRunner.ts` の `referenceViewport` を一時的に書き換えて撮り、書き換えはコミットしない。3 計画（S2-mode-tooltip・S2-tutorial・S2-notification）で同じ方式。出所: 設計検査（fresh-context）→ agent 既定（配置検査の違反 4。第 2 回で user-simulator の指摘を受けて方式を替えた） | S1-theme の撮影（`../moorestech_logs/harness/sdd/feature-uitk-s1-theme/workspace/task-5-brief.md` の Step 7・`task-5-report.md` の「撮影」）が同じ複製の方式で 720p と 2560×1080 を実際に撮っており、前版の「複製は GameUi の木が乗らず写らない」は木をその場で載せれば足りるので理由にならない。本番の資産の描き先を替えて戻す案（前版）: 途中で止まると資産が書き換わったまま残り得て、戻しの手順を 2 か所に要する。参照は 1280×720 しか無く、GameView の解像度を変える既存の口も無い |
| D-9 | 文字影は Web `style.module.css:38` の直書きと同値の既存トークン `--key-hint-text-shadow` を借り、通知専用のトークン `--notification-text-shadow` は integrator に依頼する。帰結: 負債が残る（integrator のトークンが入るまで、key-hint 側の値が変わると通知の文字影も変わる。USS のコメントにも書く）。字間は `letter-spacing: 5.5px`（0.825px÷0.15）、行送りは `min-height: 18px`＋`--localized-label-line-height: 18px`（TooltipFace.uss の前例）。出所: agent 既定（帰結の明記は設計検査 第 2 回） | Tokens はこのレーンの所有外（E-19 の例外は Foundation/Motion だけ）。値を USS に直書きする案: 色を直書きしない規則（設計書 §3.2）に反する |
| D-10 | View は書き口 `INotificationListWriter.RemoveNotification` を直に呼び、コマンド（`IXxxCommands`・`UiCommandResult`）を通さない。出所: agent 既定（書き口の分割は D-1） | 除去は UI の内側の出来事で、サーバーへ送らず拒否の経路も無い（Web の `onRemove` も store を直に呼ぶ）。コマンド層を挟む案: 結果が常に成功の口が増えるだけ |
| D-11 | `server-notification-smoke` はこのレーンで直し、GameUi と Web の両方で通す。研究と素材はマスタから選ぶ。台帳「## 壊れた録画の割り振り」の行の書き換えは S2-ladder か integrator に任せ、done.md に「直した」と書く。出所: agent 既定 | 実行計画のレーン行が「wwxy を直して」を完了条件に持ち、台帳が「S2-notification が先に直す」と割り振っている。固定 GUID を新しい値へ差し替える案: 次のマスタ改訂で同じ壊れ方をする |
| D-12 | `e2e/tests/system/portalLayerScaling.spec.ts` の通知の 1 件（台帳の行は uiScale・束 2 5）はこのレーンが写す。S2-tutorial と同じく `[TestFixture(1280f, 720f)]`・`[TestFixture(1706.7f, 720f)]` の L1 `NotificationViewportScalingTest` で、行の幅が `--notification-width` のままで画面の論理幅の 2 割以内であることを確かめる。同じファイルのほかの 3 件は S2-tutorial の持ち分。出所: 指揮の決定（設計検査 第 5 回・評価役の指摘） | 前版の `目視`（L1 のテストパネルは PanelSettings の倍率を掛けないので主張できないとした）: Expand では論理の幅が広がるだけで、行の幅が舞台 px のまま比で収まることは論理 1706.7×720 のパネルで L1 として言える（S2-tutorial の前例）。実画面 px の倍率の側は `NotificationHostViewLayoutTest` の左端の L1 と Task 10 の 2560×1080 の撮影が持つ |
| D-13 | 全部隠し（Ctrl+U・カットシーン）の仕組みは S2-skit の持ち物で、このレーンは「## 死活表」の行として確かめるだけにする: レーンの開始時（Task 6 Step 2）に S2-skit の実物が背面の層を隠すなら L1 を 1 件、隠さないなら S2-skit のファイルは書かずに止めて integrator へ返す、未マージなら done.md に S2-ladder の確認事項として書く（裁定事項にしない）。S2-skit の計画の実装はルート（全層の親）1 つの `display` を None にする全部隠し（`GameUiRoot.SetWholeUiVisible`・`UiLayerStack.SetAllLayersVisible`）なので、通知の層もその下で隠れ、「層の並びへ 1 行」の分岐は要らない。rg の語に S2-skit の実際の名前（`SetWholeUiVisible`・`SetAllLayersVisible`・`WholeUi`）を足し、L1 の判定は `notification-host` の `resolvedStyle.display`（親が隠れても Flex のまま）でなく、ルートの `display` が None でホストがその子孫であることで見る（出所: 独立評価（opus／codex）→本体の agent 既定。S2-skit 計画 D-51）。ホストの名前は `notification-host` に固定する。出所: 設計検査（fresh-context）→ agent 既定（配置検査の違反 2） | 保険タイマーを持たない（D-2）ので、隠しが要素を外す実物だと行が残り続ける。前版の「配線しない・保険タイマーが消す」: 保険を外したので成り立たない。このレーンで隠しの仕組みを作る案: S2-skit と二重になる |
| D-14 | 通知の生存尺 7000ms は Model の定数のまま置き、トークンにしない。ref-uitk-design SKILL.md §9 の「通知の生存 7000ms をトークンに」は誤りとして integrator に直しを依頼する。帰結: 負債が残る（integrator の訂正まで SKILL の行とこの計画が食い違い、SKILL を読んだ別のレーンが生存尺をトークンにし得る）。出所: 設計検査（fresh-context）→ agent 既定（配置検査の違反 3） | 実行計画のレーン行の完了条件が「`NOTIFICATION_DISPLAY_MS` 参照」で、Web の `notificationAnimationDesign.test.ts` も「生存尺は store が唯一の正で、CSS へ尺を直書きしない」と戒める。トークンにする案: USS と Model の 2 か所が生存尺を持ち、Web のテストの写しが落ちる |
| D-15 | 獲得の文の引数は、受信の params に同名の `itemName`・`count` があっても除いてから実値（アイテム名・数）を足す。L0 を 1 件（`NotificationTextLocalizeTest`）。出所: 設計検査（独立評価・再現済み）→ agent 既定 | Web は `{ ...values, itemName, count }` で実値が後勝ち（`notificationMessages.ts:131`）、C# の `LocalizationTextInterpolator` は先勝ちで重複を LogError にする（`LocalizationTextInterpolator.cs:30-53`）。末尾に足すだけの案（前版）: サーバーが同名の引数を送ると誤った名前と数が出てエラーも出る |
| D-16 | 画面 USS が当たらず（tss の `@import` 漏れ等）スタイルの受け直しが来ないまま配置まで進んだら、ホストが LogError を出す（預かった通知が無音で出ない経路を作らない）。出所: 設計検査（独立評価）→ agent 既定 | `silent-ok` だけにする案: `CustomStyleResolvedEvent` は当たるカスタムプロパティが無いと来ない（推測。L1 で実測）ので、`@import` 漏れで全通知が出ないまま誰も気づかない |

## 本PR外のリファクタ提案

- WebUiHost の `NotificationTopic.ToDto` と新しい `NotificationContentFactory.FromMessage` が、同じ `NotificationMessagePack` の種別分けと空アイテム ID の扱いを二重に持つ。Web UI を畳む時点で `ToDto` を消すか、両方が同じ種別分けを使う形に寄せる
- アイテム名の解決が、既存の手書きの 2 か所（Guid からキーを組んで `Localize.GetContent` する箇所）と、このレーンの `LocalizeNotificationTextLookup.ItemDisplayName`、C3-a の `GameContentPresentation.ItemName` に散らばる。欠けの扱い（LogError と「Item {itemId}」）ごと Content 層の 1 か所へ寄せる
- 通知専用の文字影トークン `--notification-text-shadow` の追加（D-9）
- 倍率の外にある端の余白（通知の `left: 1rem`）を `StageScaleSource` の購読で実画面 px に保つ形が他の画面にも出たら、「実画面 px の余白」を Foundation/Stage の部品に寄せる（D-7）

レーンの起動は指揮が行う（実行計画 §4.1）
