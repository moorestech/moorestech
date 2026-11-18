# 段階1 as-built（feature/uitk の現状図）

- 対象: `origin/feature/uitk` = `54c8a4c8c8`（この worktree の HEAD と同じ）。`origin/master...origin/feature/uitk` は 102 コミット・116 ファイル（Client.Ui/Client.Ui.State/Client.Tests と .meta を除く）
- 取り込まれたレーン: skeleton（first-parent 直置き）→ theme `cb035c2dd3` → input `f244b537bc` → screens `2a9b642c79` → moores-code-review 反映 `5421c7f9f0`〜`0d75fa453c`（D-1〜D-10）→ web-look `6357c8aba7` → `54c8a4c8c8`
- 測り方: ファイルの実読・`wc -l`・`grep`。Unity は起動していない（コンパイル・テストの成否は CI run 42109885552 の報告〈`moorestech_logs/harness/sdd/feature-uitk-stage1/lane-screens-done.md`〉の転記で、web-look 以降の HEAD では未確認）
- パスは `moorestech_client/Assets/Scripts/` からの相対で書く（他は repo ルートから）。「推測」と書いた所以外は実測

---

## 1. 全体図

### 1.1 アセンブリと参照の向き

```
Client.Input ──(IGameUiPointerProbe・UiInputExclusivity: static 窓口)
   ▲
Client.Game ──(UIStateControl・PauseMenuStateService・AnchorIds・UiPointerHitTest)
   ▲
Client.Ui.State  (Routing / Commands / PauseMenu / Settings / Text / Toast ＋段階0の BlockDetail 等)
   ▲                         ▲
Client.Ui (描画)          Client.WebUiHost (UiStateTopic・Action アダプタ・WebUiSilenceWatcher・Health)
   ▲                         ▲
Client.Playtest (DSL: GameUi ロケータ＋両系合成)   Client.Starter (composition root)
```

- `Client.Ui.asmdef` の参照: Client.Input・Client.Game・Client.Localization・Client.PlaytestReceiver・Client.Ui.State・UniRx・UniTask・VContainer（`Client.Ui/Client.Ui.asmdef`）
- `Client.Ui.State.asmdef` は master と同じ参照（Client.Game・Client.Network・Core.Master・Game.*・Server.* 等）
- `Client.Game`・`Client.Input` は `Client.Ui*` を参照しない（UITK 側の値は push か interface 登録で渡る。input plan D-4/D-5）

### 1.2 DI 登録（composition root）

- `Client.Starter/Registration/MainGameUiStateRegistration.cs`: `UiOwnershipTable`（Singleton）・`UiRoutingState`（EntryPoint AsSelf）を追加
- `Client.Starter/Registration/MainGameUiRegistration.cs:21-43`（45 行）: `GameUiRoot`（RegisterComponent）・`WebUiSuspension`・`WebUiSilenceWatcher`（※ Client.WebUiHost の型）・`GameUiLocaleClassBinder`・`GameUiInputInstaller`・`ImeCompositionModeApplier`・`PauseMenuCommands as IPauseMenuCommands`・`SettingsCommands as ISettingsCommands`・`PauseMenuModel`・`SettingsModel`・`GameUiToastFeed`・`ToastLayerPresenter`・`BugReportDraft`・`PauseMenuScreenPresenter`
- `Client.Starter/Registration/PlaytestRecordRegistration.cs`: `IWebUiHealthProbe→WebUiHealthProbe`・`IBugReportOutboxRoot→GameSystemBugReportOutboxRoot`
- Unity 資産: `Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset`（1280×720・Expand・sortingOrder 1）、`GameUiTheme.tss`（既定ランタイムテーマ＋Tokens/Skit/Slot/Control/Theme/Typography の 6 USS を束ねる）、`GameUiAssetCatalog.asset`、`Fonts/GameUi{Latin,Japanese,Korean} SDF.asset`、`MainGameUI.prefab` に子 `GameUiRoot`、`MainGame.unity` の入力モジュール資産差し替え（`Assets/Asset/Common/MoorestechUiInputActions.inputactions`）

### 1.3 実行時の流れ（ポーズを例に）

1. `UIStateControl.OnStateChanged`／入れ子画面の `OnPresentationChanged`／Editor 上書きの変化 → `UiRoutingState.Refresh()` → `Current`（`UiRoutingSnapshot{Screen, WebUiSuspended, MainPanelOwner}`）（`Client.Ui.State/Routing/UiRoutingState.cs:32-66`）
2. `WebUiSuspension` が `WebUiSuspended` を購読し `GameUiRoot.SetCefVisible(!s)` と `UiInputExclusivity.SetWebUiSuspended(s)`（`Client.Ui/Foundation/Routing/WebUiSuspension.cs:41-47`）
3. `UiStateTopic` が `webUiSuspended` を ui_state に載せ（`Client.WebUiHost/Game/Topics/UiStateTopic.cs:48,105,128`）、Web の App が全 unmount（`moorestech_web/webui/src/app/App.tsx:74-92`、zod は必須 `schemas/ui.ts:32`）。休止中の `ui_state.request` は dispatcher が `web_ui_suspended` で拒否（`Client.WebUiHost/Boot/WebSocketMessageDispatcher.cs:126-128`）
4. `PauseMenuScreenPresenter` が起動時に 1 回 `PauseMenuView` を `UiLayer.PauseMenu` に載せ、`traits.PauseMenu && MainPanelOwner==GameUi` で表示（`Client.Ui/Screens/PauseMenu/PauseMenuScreenPresenter.cs:43-67`）
5. 所有表の現在値: PauseMenu・TrainPause・SkitPause の MainPanel が `GameUi`、他 7 画面は `Web`（`Client.Ui.State/Routing/UiOwnershipTable.cs:14-26`）

---

## 2. 型ごとの現状（責務・公開 API・依存・行数）

### 2.1 Client.Ui.State/Routing（段階1 新設・6 ファイル）

| 型（ファイル:行数） | 責務 | 公開 API | 依存 |
|---|---|---|---|
| `UiScreen`（UiScreen.cs:20） | Web の UiScreen と同じ 10 画面の enum | None…SkitPause | — |
| `UiScreenRouting`（UiScreenRouting.cs:33） | UIStateEnum＋入れ子サブ状態→画面（Web `screenForUiState` の写し） | `static Resolve(UIStateEnum, NestedPauseSubStateEnum?)` | Client.Game |
| `UiScreenTraits`/`UiScreenTraitsTable`（UiScreenTraits.cs:72） | 画面ごとの Grab/PauseMenu/SkitInput/Backdrop/AlwaysOnHud/TrainHud（Web SCREEN_TRAITS の写し）。欠け行は静的初期化で例外 | `static Get(UiScreen)` | — |
| `UiSurface`/`UiSurfaceOwner`（UiSurface.cs:21） | 描画面 enum（`MainPanel` のみ）と所有者 `Web`/`GameUi` | — | — |
| `UiOwnershipTable`（UiOwnershipTable.cs:89 の前半） | (画面,面)→所有者の静的表。欠け・重複は ctor で例外 | `GetOwner(UiScreen, UiSurface)` | Editor 上書き |
| `UiOwnershipEditorOverride`（同ファイル 55-88、`#if UNITY_EDITOR`） | Editor 限定の行上書き（R4）。変化を UniRx で通知 | `static Set/Clear`、`OnOverridesChanged`、`internal TryGet` | UniRx・Debug |
| `UiRoutingState`（UiRoutingState.cs:101 の前半） | 現在画面と Web 休止要否を ReactiveProperty で保持 | `Current`、`Resolve()`、`Initialize`、`Dispose` | UIStateControl・UiOwnershipTable・INestedPauseScreenState 群 |
| `UiRoutingSnapshot`（同ファイル 69-100） | 不変スナップショット（値の等価） | `Screen`・`WebUiSuspended`・`MainPanelOwner` | — |

### 2.2 Client.Ui.State の段階1 追加（Commands・PauseMenu・Settings・Text・Toast＝15 ファイル）

| 型（ファイル:行数） | 責務 | 公開 API |
|---|---|---|
| `UiCommandResult`/`UiCommandAccepted`/`UiCommandRejected`（Commands/UiCommandResult.cs:41） | 型付きコマンドの判別型結果 | `Reason`・`Detail`・`ToWireCode()` |
| `UiCommandRejectReason`/`…Traits`（Commands/UiCommandRejectReason.cs:74） | 拒否理由 enum、良性判定・Web 失敗コード・ローカライズキーへの写し | `IsBenign`・`FromSubmitFailureCode`・`ToLocalizationKey`・`ToWireCode` |
| `IPauseMenuCommands`/`PauseMenuCommands`（21/95） | セーブ・セーブ終了・ページ遷移・バグ報告送信/再送/書き出し。Web アダプタと GameUi View が同じ口を呼ぶ | `Save`・`SaveAndQuit`・`ShowPage`・`UniTask<UiCommandResult> SubmitBugReport`・`ResendFailedReports`・`ExportUnsentReports` |
| `PauseMenuModel`/`PauseMenuViewModel`（64/29） | 源 5 本→ViewModel 1 つ | `Current`・`OnPauseMenuOpened` |
| `BugReportDraft`（37） | 下書き（本文・種別・世代）。View 外で保持 | `SetDescription`・`SetKind`・`Reset`・`Generation` |
| `BugReportFormText`（165） | バグ報告の状態・配送・書き出し文の純関数（Web BugReportForm と同じ分岐） | `DescribeStatus`(UiText)・`DescribeDelivery`(string)・`ShowResend` 等 |
| `ISettingsCommands`/`SettingsCommands`（18/37） | 画質・ダッシュ・言語の保存 | `ApplyGraphicsPreset`・`SetGraphicsQuality`・`SetSprintInputMode`・`UiCommandResult SetLanguage` |
| `SettingsModel`/`SettingsViewModel`（68/39） | 設定の変化→ViewModel | `Current` |
| `UiText`（Text/UiText.cs:32） | キー＋名前付き引数の文。表示直前に `Resolve()` | `Key`・`Params`・`Resolve()` |
| `GameUiToast`/`GameUiToastFeed`（24/34） | トースト 1 件（解決済み文字列＋Info/Error）とその流れ | `OnToast`・`Show`・`ShowRejected` |

Client.Ui.State の他のディレクトリ（BlockDetail・Blueprint・Challenge・EventMode・Modal・Notification・Research・Train）は master にも同数あり、段階0 で移したもの（段階1 では未変更）。

### 2.3 Client.Ui/Foundation

| 型（ファイル:行数） | 責務 | 公開 API | 依存 |
|---|---|---|---|
| `GameUiRoot`（Root/GameUiRoot.cs:66） | MainGameUI.prefab 上のシングルトン MonoBehaviour。UIDocument 1 枚、層スタック構築、CEF RawImage の表示切替、ルートのクラス付け外し。必須参照欠けは Awake で例外（:34） | `Instance`・`AssetCatalog`・`GetRootElement()`・`GetLayer(UiLayer)`・`SetCefVisible(bool)`・`SetRootClassEnabled(string,bool)` | UIDocument・RawImage（uGUI）・UiAssetCatalog |
| `UiLayer`（Layers/UiLayer.cs:21） | 13 層の固定順（Backdrop→FullScreenGate）。Web の `--z-*` 対応 | — | — |
| `UiLayerStack`（Layers/UiLayerStack.cs:65） | ルート直下に層コンテナを enum 順に並べ全面・Ignore | `static Build(VisualElement)`・`GetLayer` | — |
| `WebUiSuspension`（Routing/WebUiSuspension.cs:49） | ルータの休止判断→CEF の絵と入力排他 | EntryPoint | UiRoutingState・GameUiRoot・UiInputExclusivity |
| `UiAssetCatalog`（Assets/UiAssetCatalog.cs:60） | 部品・画面 USS 7 枚の ScriptableObject。欠けは起動で例外 | 7 プロパティ・`ValidateAllAssigned()` | — |
| `GameUiInputInstaller`（Input:37） | ルートへ入力規則を付け、Pick probe を登録 | EntryPoint | GameUiRoot・UiInputExclusivity |
| `GameUiInputRules`（Input:112） | ルート TrickleDown: フォーカス同期・入力欄 ESC（ラッチ＋既定 ESC 抑止＋Blur）・Navigation 打ち消し・非入力欄フォーカスの拒否（LogWarning） | `static Attach(VisualElement)` | UiInputExclusivity |
| `GameUiPointerProbe`（Input:36） | `IGameUiPointerProbe` 実装。スクリーン→パネル座標で `panel.Pick` | `IsPointerOverGameUi(Vector2)` | — |
| `ImeCompositionModeApplier`（Input:42） | IME モードを「どれかの入力欄にフォーカス=On、無ければ Off」に一元化 | EntryPoint | UiInputExclusivity.TextInputFocused |
| `Tokens.uss`(178)/`SkitTokens.uss`(39)/`SlotTokens.uss`(12) | `tools/uitk/convert-web-tokens.mjs` の生成物（手編集禁止）。α補正 14 件にコメント | `:root` 変数 | Web tokens.css 等 |
| `ControlTokens.uss`(49) | Web に同値トークンが無い部品色・前計算色（PoC 由来の `--control-*`、`--pause-menu-face`、`--mantine-dark-*` 等） | `:root` 変数 | — |
| `Theme.uss`(146)/`Typography.uss`(11) | 既定ランタイム部品（Button・BaseField・Slider 等）の暗色化／フォントとロケールクラス | — | ControlTokens |
| `TokenConversionExclusions.txt` | 変換できないトークンと行き先（層 enum・実行時寸法・UiMotion・フォント・分割） | — | — |
| `UssCustomStyleReader`(40)/`UssTokenValue`(37) | 部品 USS で受け直したトークンを C# で px・ms・色として読む | `TryReadPixels/Milliseconds/Color`・`TryParse*` | — |
| Motion（Core 3・Tween 5・Inertia 2・Playback 2・Steps 3＝15 ファイル、最大 67 行） | 時計注入の走行器 `UiMotionTimeline`、tween（曲線・遅延・Repeat/PingPong）と慣性、パネルのスケジューラで回す `UiMotionPlayer.Play`→`UiMotionHandle`（Cancel・Completed・Detach 停止）、Step 3 種（Opacity・TranslateY・ScrollOffset） | `UiMotionPlayer.Play(VisualElement, IUiAnimation)`・`UiTweenAnimation(UiMotionSpec, IUiTweenStep)`・`UiInertiaAnimation(...)` | UniRx |
| Anchor（TutorialAnchor:17・Registry:38・Resolution:33） | クラス名でアンカー印付け、木の照会で Missing/Unique/Duplicate を返す（状態なし） | `TutorialAnchor.Mark(el, ids)`・`TutorialAnchorRegistry.Resolve(root, id)` | AnchorIds（Client.Game） |
| Text（LocalizedLabel:64・UiNumberFormat:25・GameUiLocaleClass:18・…Binder:39） | キー＋引数で言語変更に追従するラベル、InvariantCulture 数値、CJK ロケールクラス | `LocalizedLabel.SetKey(key[, params])`・`UiNumberFormat.Fixed/Integer`・`GameUiLocaleClass.IsCjk` | Localize |

### 2.4 Client.Ui/Components（5 部品）

| 部品（ファイル:行数） | Web の対応 | 公開 API | 備考 |
|---|---|---|---|
| `GamePanel`(55)＋`GamePanelFace`(119)・`GamePanelDecoLine`(71)・`GamePanelBottomDeco`(79)・`GamePanel.uss`(93) | `shared/ui/GamePanel` の default variant | `GamePanel(StyleSheet)`・`SetTitle(Label)`・`contentContainer`=本文 | 面は頂点カラー格子、罫線・三角は Painter2D。box-shadow なし（theme D-6）。**本番の画面からは未使用**（web-look でポーズ枠から外れた） |
| `ModeSwitch<TValue>`(91)＋`ModeSwitchOption`(36)・`.uss`(82) | `shared/ui/ModeSwitch` | `ModeSwitch(testid, orientation, options, sheet)`・`OnSelected`・`SetSelected`・`SetSelectedNone`・`SetEnabledAll` | ring は border で近似 |
| `PanelActionButton`(29)・`.uss`(46) | `shared/ui/PanelActionButton` | `PanelActionButton(testid, LocalizationKey, sheet)`（`Button` 派生） | text-shadow は outline で近似 |
| `GameTextField`(91)＋`ImeCompositionUnderline`(85)・`ImeCaretScreenPosition`(33) | （Web は Mantine Textarea＋`textAreaField.module.css`） | `GameTextField(testid, multiline)`・`Field`・`OnValueChanged`・`SetValueWithoutNotify` | **USS を持たない**。見た目は `BugReport.uss:40-75` が持つ |
| `ToastItem`(33)・`Toast.uss`(66) | Mantine Notification（dark・withBorder） | `ToastItem(GameUiToast, sheet)` | — |

### 2.5 Client.Ui/Screens（4 画面・17 .cs）

| 画面 | 型（行数） | 要点 |
|---|---|---|
| PauseMenu | `PauseMenuScreenPresenter`(70)・`PauseMenuView`(166)・`PauseMenuTopPageView`(67)・`PauseMenuSubPageView`(39)・`PauseMenuSettingsPage`(45)・`PauseMenuBugReportPage`(37)・`IPauseMenuPage`(17)・`PauseMenuStack`(15)・`PauseMenu.uss`(119) | 起動時 1 回マウント＋`SetShown`。表示中だけモデル購読。ページ切替で View を作り直す。枠は Web `.panel` の写し（420px・角丸 12px・padding 32px・題 34px 行） |
| Settings | `LanguageSelectView`(59)・`SprintModeSelectView`(46)・`GraphicsQualitySettingsView`(79)・`SettingsSectionParts`(43)・`GraphicsQualityRows/*`（4 ファイル・最大 77）・`Settings.uss`(33) | 画質行は `GraphicsQualityRow<TValue>` の型付き行抽象（review D-4） |
| BugReport | `BugReportFormView`(196)・`BugReportRecoveryControlsView`(64)・`BugReport.uss`(123) | Mantine Button 相当・textAreaField 相当の見た目を画面 USS に直書き |
| Toast | `ToastLayerPresenter`(40)・`ToastHostView`(97) | 右下縦積み、3000ms で UiMotion の Opacity でフェード |

### 2.6 入力の窓口（Client.Input / Client.Game）

| 型（ファイル:行数） | 責務 | 公開 API |
|---|---|---|
| `UiInputExclusivity`（Client.Input/UiInputExclusivity.cs:193） | Web 側と GameUi 側の入力占有を論理和で持つ static 窓口。Web 休止・Web 無応答（15s）・ESC ラッチ・Pick probe 登録・キー抑止 | `SetState`・`SetGameUiTextInputFocused`・`SetGameUiPointerProbe`・`IsPointerOverGameUi`・`IsPointerOverWebUi`・`ConsumeEscapeThisFrame`・`IsEscapeConsumedThisFrame`・`SetWebUiSuspended`・`IsWebUiSuspended`・`RecordWebMessageReceived`・`EvaluateWebSilence`・`IsSuppressed`・`ProbeSuppressed`・`TextInputFocused` |
| `IGameUiPointerProbe`（13） | GameUi の Pick を Client.Input から引く逆転口 | `IsPointerOverGameUi(Vector2)` |
| `UiPointerHitTest`（Client.Game/InGame/Control/UiPointerHitTest.cs:26） | ロック中 false、他は GameUi Pick ∨ Web pointerOverUi（休止・無応答でない時）∨ uGUI | `IsPointerOverAnyUi()`（呼び出し 23 ファイル以上は不変） |
| `InputKey`(77)・`HybridInput`(140)・`InputSuppressionFilter`(41) | ESC ラッチ中の押下抑止（解放は通す） | — |

### 2.7 DSL（Client.Playtest/UiElements、Editor 限定 asmdef）

| 型（ファイル:行数） | 責務 |
|---|---|
| `GameUiElementLocator`（GameUi/:44, internal） | `IUiElementLocator` の UITK 版。パネルは `GameUiRoot.Instance` |
| `GameUiElementQuery`（GameUi/:107, internal static） | `Query(name)` 全件・2 件以上 Ambiguous・祖先の可視判定・`panel.Pick` で被覆・SetText（ChangeEvent を流す）。非表示は NotFound |
| `GameUiScreenMapper`/`IGameUiScreenMapper`（36/13） | パネル→Unity スクリーン座標（左下原点） |
| `CompositeUiElementLocator`（:75, internal） | UITK を先に探し、両方 Found なら Ambiguous。GameUi で可視に見つかった後の Web 重複照会は 0.5 秒で打ち切り（review D-6） |
| `PlaytestUiLocatorSession`（:51） | 合成ロケータへ差し替え |

### 2.8 テスト（Client.Tests/Ui 67 ファイル・EditModeInPlayingTest/Ui 8 ファイル、計 6,473 行）

- L0/L1/L2 の区分（`Client.Tests/Ui/`）: Anchor 2・Assets 1・Components 5・Conventions 3（USS/UXML 規約の機械検査）・Dsl 4・Input 7・Layers 2（層順と tokens.css の `--z-*` 照合）・Motion 5・Routing 2・Screens 14（`ScreenTestHost` 基底＋Fake/Recording 二重）・State 7・TestDoubles 6（`FakeModel<T>`・`CommandRecorder<T>`・Counter 見本）・TestHost 1（`UiTestHost : UITestFixture`、本番 PanelSettings のテーマ・1280×720）・Text 4・Theme 5（Web トークン名パリティ・α補正と 13px 下限）
- L3（EditModeInPlayingTest/Ui）: `GameUiSkeletonTest`(176)・`GameUiInputTest`(186)・`PauseMenuGameUiTest`(184) と補助 5 本。`UiOwnershipEditorOverride` を使うのは `GameUiSkeletonTest`・`PauseMenuGameUiTest`・`UiOwnershipTableTest`
- 録画シナリオ: `.agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/` に `uitk-lifeline`・`uitk-nested-pause-skit`・`uitk-nested-pause-train`・`uitk-pause-web-action-count`（最後は外した一時計測行 `[R14-MEASURE]` に依存し単体では数えられない＝lane-screens-done.md）

---

## 3. 全体スイッチへの作り直し（albof.17）で消す・残す・直す

裁定 `2026-11-06-UITK長命ブランチの中は全体スイッチ1つで切り替え追従はブランチ側で記帳する.md`（「画面ごとに振り分ける所有表は使わない。全部 Web／全部 UITK の 1 つのデバッグスイッチ。UITK の間は未実装画面は使えない」）と `2026-11-06-UITK一気切替の日にCEFとWebを同時に撤去する.md`（切替後に Web へ戻す切替は残さない）に照らした分類。分類は本調査の提案で、裁定ではない。

### 3.1 消す

| 対象 | 場所 | 理由 |
|---|---|---|
| `UiOwnershipTable`（行表・検証・`GetOwner`） | `Client.Ui.State/Routing/UiOwnershipTable.cs:10-53` | 画面別の振り分けそのもの |
| `UiOwnershipEditorOverride`（Set/Clear/OnOverridesChanged） | 同 `:55-88` | 画面別の Editor 上書き（R4）。全体スイッチに吸収 |
| `UiSurface` enum（`MainPanel`） | `UiSurface.cs:7-10` | 面ごとの所有の概念が不要 |
| `UiRoutingSnapshot.MainPanelOwner` と `WebUiSuspended` の画面依存計算 | `UiRoutingState.cs:61-65,73-99` | 休止は画面でなくスイッチで決まる |
| `UiRoutingState` の Editor 上書き購読 | `UiRoutingState.cs:38-49` | 同上 |
| `UiOwnershipTableTest` | `Client.Tests/Ui/Routing/UiOwnershipTableTest.cs` | 対象消滅 |
| DI 登録 `UiOwnershipTable` | `MainGameUiStateRegistration.cs` | 同上 |
| 台帳の R4 手順・所有者列・束の `切替済み` | `docs/uitk/TODO.md` の「読み方と運用」末尾・「## 束」「## 行」 | 束1 を暗転へ戻す作業は albof.20 |
| （スイッチの実装次第で）Web 側の休止配線一式: `UiStateDto.WebUiSuspended`・`UiStateTopic` のルータ購読・dispatcher の `web_ui_suspended` 拒否・zod `webUiSuspended`・`App.tsx` の休止 unmount・`isWebUiSuspended`・`useWebInputExclusivity(webUiSuspended)` と `watchPointerHitOnDomChange`・mock-host の `webUiSuspended: false` | `UiStateTopic.cs:27-48,105,128`／`WebSocketMessageDispatcher.cs:126-128`／`moorestech_web/webui/src/app/App.tsx:74-92`・`schemas/ui.ts:32`・`shared/uiState/uiScreenRouting.ts`・`shared/uiState/inputExclusivity/*`・`e2e/mock-host/*` | GameUi モードで Web ホストを起動しない形にすれば、「Web が動いたまま一部だけ休止する」状態が存在しなくなる。長命ブランチ上の moorestech_web の差分（約 12 ファイル）は master の Web 変更と衝突する面でもある（推測: 取り込みのたびの衝突源） |

### 3.2 残す（そのまま再利用）

- `UiScreen`・`UiScreenRouting`・`UiScreenTraitsTable`（UITK 側が「どの画面を出すか」を決める正。Web の写しなので Web 撤去後も意味が残る）
- `UiRoutingState`（`Screen` だけを持つ形に縮める）
- Foundation 全般（GameUiRoot・層・テーマ・Motion・Text・Anchor・Input 規則・Probe・IME）、Components、Screens の View、Client.Ui.State のコマンド・モデル
- `UiInputExclusivity` の GameUi 側（文字入力フォーカス・ESC ラッチ・Pick probe）。Web 側（`SetState`・休止・無応答）は Web モードのあいだ要る。切替 PR で Web 側を消す
- DSL の両系合成（Web モードでも UITK モードでも同じシナリオを流すため）

### 3.3 直す

| 対象 | 直し方（案） |
|---|---|
| `WebUiSuspension` | 画面ごとの購読をやめ、スイッチが GameUi なら起動時から CEF の RawImage を隠し `SetWebUiSuspended(true)` 固定、Web なら何もしない。既存の Editor 限定の印 `WebUiHostEditorSkip`（`Client.Starter/Initialization/Boot/WebUiStartup.cs:17,50-`）で Web ホストを起動しない経路が既にあり、スイッチの GameUi 側はこれに寄せられる（推測: 再利用可能。RawImage を隠す処理も起動しなければ不要になる可能性） |
| `PauseMenuScreenPresenter.IsShownOn` | `traits.PauseMenu && MainPanelOwner==GameUi`（`:63-67`）→ `traits.PauseMenu && スイッチ==GameUi`。Web モードでは UITK の層を一切描かない形が素直（推測） |
| `GameUiSkeletonTest`・`PauseMenuGameUiTest` | 上書き API の代わりにスイッチで UITK にして起動する形へ |
| `UiPointerHitTest`・`UiInputExclusivity.IsPointerOverWebUi` | 変更不要（休止フラグが立てば Web 側は読まれない）。ただしスイッチで Web ホストを起動しないなら休止フラグ自体を立てる必要があるか要確認 |
| `docs/uitk/TODO.md` | 束・行の所有者列の意味を「移植済み/未移植」へ（追従記帳は長命ブランチ側へ。albof.20） |

**スイッチの置き場と既定値はまだ決まっていない**（裁定は「1 つのデバッグスイッチ」まで）。決める点: (a) Editor 限定か（切替前は配らないので Editor 限定で足りる。推測）、(b) 長命ブランチ上の既定値を Web と UITK のどちらにするか（UITK 既定なら未移植画面が使えず、Web 既定なら UITK が普段の起動で通らない）、(c) PlayMode 中に切り替えられるか（起動時だけなら CEF の起動要否と揃う）。

---

## 4. 段階2以降が再利用できる基盤 API

| 用途 | API | 状態 |
|---|---|---|
| 画面を載せる | `GameUiRoot.GetLayer(UiLayer)`、`UiLayerStack.Build`（L1 用） | 13 層。Skit 遷移層は無い（§6.3） |
| どの画面か | `UiRoutingState.Current`・`UiScreenRouting.Resolve`・`UiScreenTraitsTable.Get` | 主パネル級 10 画面のみ。HUD 類は対象外 |
| ポーズ型の載せ降ろし | `PauseMenuScreenPresenter` の形（起動時 1 回マウント＋`SetShown`＋表示中だけ購読） | 前例 1 件 |
| USS の配布 | `UiAssetCatalog`（1 USS＝フィールド＋プロパティ＋検査表 1 組）、部品は ctor で `StyleSheet` を受ける | 拡張点は `UiAssetCatalog.cs:12-15` |
| トークン | `GameUiTheme.tss` 経由の `:root` 変数、部品 USS で `--部品-名: var(--token)` と受け直し、C# は `UssCustomStyleReader` | 生成は `node tools/uitk/convert-web-tokens.mjs` |
| 描画 | Painter2D／頂点カラーの前例: `GamePanelFace`（格子フェード）・`GamePanelDecoLine`（横グラデ）・`GamePanelBottomDeco`（三角） | — |
| 動き | `UiMotionPlayer.Play(el, new UiTweenAnimation(new UiMotionSpec(ms, delay, easing, loop), step))`、`UiInertiaAnimation`、Step: Opacity/TranslateY/ScrollOffset、`UiMotionHandle.Cancel/Completed` | 慣性・TranslateY・ScrollOffset は本番未使用 |
| 文言・数値 | `LocalizedLabel.SetKey`・`UiText`・`UiNumberFormat` | `UiNumberFormat` は本番未使用 |
| アンカー | `TutorialAnchor.Mark`・`TutorialAnchorRegistry.Resolve`・`AnchorIds`（Client.Game、Web anchorIds.ts と L0 照合） | 枠の描画側（消費者）は未実装 |
| 入力 | `GameUiInputRules.Attach`（全体キー規則の追加口）、`GameTextField`、`UiInputExclusivity.ConsumeEscapeThisFrame`、ボタン類は `focusable=false` | 画面固有キーは各 Presenter の持ち物（`GameUiInputRules.cs:11-14`） |
| 部品 | `GamePanel`（default）・`ModeSwitch<T>`・`PanelActionButton`・`GameTextField`・`ToastItem` | §5 の不足を参照 |
| 状態・コマンド | `UiCommandResult`（判別型）・`UiCommandRejectReasonTraits`、`IXxxCommands` を Web アダプタと View が共有する形、`ActionResult.FromCommandResult`（`Client.WebUiHost/Game/Actions/IActionHandler.cs`） | 前例 2 画面 |
| テスト | `UiTestHost`・`ScreenTestHost`・`FakeModel<T>`・`CommandRecorder<T>`・`UiStyleConventionRules`（L2 規約）・`GameUiElementQuery`（DSL） | `ScreenTestHost` は英語固定と言語の保存値復元つき |
| DSL | testid=`name`、`CompositeUiElementLocator`（UITK 優先） | Web と同じ綴りなら既存シナリオがそのまま動く |

---

## 5. Web 版の見た目と現状の差

### 5.1 段階1 の 4 画面（web-look 後）

- 枠: ポーズ・設定・バグ報告は Web `features/pauseMenu/style.module.css` の `.panel` を写した（`Client.Ui/Screens/PauseMenu/PauseMenu.uss` の `.pause-menu__panel`: 420px・padding 32px・角丸 12px・1px 線）。題は Web 34px に対しフォント幅差で 32px（同ファイルのコメント）
- 実物の比較（Web 左・UITK 右、720p）: https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-06-uitk-s1-web-look-evidence/index.html 。トップは位置・寸法・色ともほぼ一致。設定では (a) UITK の行の縦の間隔が数 px 大きい、(b) スクロールバーの形と位置が Web（細い・枠の右端）と違う（UITK は既定テーマの Scroller を `Theme.uss` の PoC 由来トークンで塗ったもの）を目視で確認（cmp-settings.png）
- **UITK でポーズを出すと Web の HUD（ホットバー・チャレンジ・キーヒント）がポーズの背後から消える**（Web 版は背後に残る。cmp-pause-top.png）。原因は R1 の Web 全面 unmount。全体スイッチ後は HUD を UITK で作るまで UITK モードでは HUD 無し（裁定の「未実装画面は使えない」の範囲）
- トースト: Mantine Notification の写し（`Toast.uss`）

### 5.2 部品レベル（段階2以降に効く）

- `GamePanel` は Web の default variant を構造・面・罫線・三角まで写している（`Client.Ui/Components/GamePanel/*`）。差: box-shadow（`0 0 12px 7px rgb(9 11 20 / 45%)`、`moorestech_web/webui/src/shared/ui/GamePanel/style.module.css:33`）を描かない（theme D-6）／`titleAction`（タイトル行右端の副次アクション）と `gridArea` が無い（Web `index.tsx:19-24` に対し UITK は `SetTitle(Label)` だけ）／craft・skit・hud variant が無い
- Web `shared/ui` の 27 項目のうち UITK にあるのは GamePanel・ModeSwitch・PanelActionButton の 3 つと、textAreaField 相当（`GameTextField`、ただし見た目は画面 USS 側）。無いもの: ItemSlot・SlotFrame・SlotGrid・BlockSlot・FluidSlot・FluidSlotRow・FluidAmountSlot・ItemIcon・BlockIcon・FluidIcon・GameIcon・IconButton・GaugeBar・BarSeries・ProgressArrowBar・ProgressArrowGlyph・HoverTooltip・FadeRule・RecipeListScrollArea・panelBodyScrollArea・slotContent・PlacementTargetFace・ConnectingPlaceholder
- Mantine 相当の部品が無い。Web features が使う Mantine は Text 42・Stack 33・Group 10・Title 9・ScrollArea 8・Button 8・Box 4・Paper 3・Portal 3・Overlay 2・Tooltip/TextInput/Notification/Modal/Loader 各 1（import の集計）。UITK では Mantine Button 相当が `BugReport.uss:80-121`、Title 相当が `PauseMenu.uss` と `SettingsSectionParts`、ScrollArea 相当が PauseMenuSubPageView に、それぞれ画面の中で直書きされている
- 既定コントロールの塗り: `Theme.uss`＋`ControlTokens.uss` の `--control-*` は PoC 由来（例 `--control-accent: #f2a640`）で Web/Mantine の値ではない。素の Button・Slider・Scroller・Dropdown を使うと Web と違う色になる

---

## 6. 設計上の負債

### 6.1 200 行・10 ファイル

- 200 行超: `Client.Tests/Ui/Screens/BugReport/BugReportFormViewTest.cs` 201 行（違反）
- 上限間近: `Client.Ui/Screens/BugReport/BugReportFormView.cs` 196、`Client.Input/UiInputExclusivity.cs` 193、`Client.Tests/Ui/Screens/PauseMenu/PauseMenuViewTest.cs` 194、`Client.Ui.State/Train/TrainTimetableFetcher.cs` 198（段階0）
- `Client.Input/` は .cs が 10 で上限ちょうど
- partial・`Func<>`・try-catch は Client.Ui/Client.Ui.State に無い（grep 0 件）

### 6.2 責務の漏出・置き場

- `MainGameUiRegistration`（Client.Ui の登録口）に `WebUiSilenceWatcher`（Client.WebUiHost の型）と、Client.Ui.State のコマンド・モデル（Web アダプタも使う）を登録している（`:25,32-35`）。Web ホスト側の登録や `MainGameUiStateRegistration` との境界が曖昧。CEF 停止の記録を master へ切り出すとき、master に `MainGameUiRegistration` は無いので登録先を作り直す必要がある
- `UiInputExclusivity` が 1 つの static に Web 側の値・GameUi 側の値・休止・無応答・ESC ラッチ・Pick probe・キー抑止を抱える（screens D-24 で分割を据え置き）。切替 PR で Web 側を抜く前提なら許容（推測）
- GameUi 側の見た目が画面 USS に閉じ込められている: `GameTextField` に USS が無く（input plan R-10 では `GameTextField.uss` を作る予定だった）、textAreaField 相当は `BugReport.uss:40-75`。Mantine Button 相当も同様。段階3 の BP 名入力・ビルドメニュー検索で同じ見た目が要ると重複する
- 部品が ctor で `StyleSheet` を受ける形（GamePanel・ModeSwitch・PanelActionButton・ToastItem）は、呼び出し側が毎回 `catalog.Xxx` を渡す。`UiAssetCatalog` は USS 1 枚ごとにフィールド＋プロパティ＋検査表 1 行を足す設計で、部品と画面が増えると 200 行を超える（推測: 段階2〜4 で 30 枚超になる見込み）。部品 USS を `GameUiTheme.tss` に束ねる選択肢もある（トークンは既にそうしている。theme D-7）

### 6.3 層 enum の不足と不一致

- `TokenConversionExclusions.txt` は `--z-viewport-stage`・`--z-stage-overlay-panel-chrome`・`--z-stage-skit`・`--z-portal-skit-transition` を「層 enum: UiLayer」としているが、`UiLayer`（`Client.Ui/Foundation/Layers/UiLayer.cs:5-20`）に対応する層は無い。overlay-panel(30) と chrome(31) は `Hud` 1 層に潰れており（研究パネルの上にチャレンジ HUD・キーヒント・採掘バーを出す Web の順を層で表せない）、skit-transition(210、Modal 200 と Toast 300 の間）は置き場が無い。`UiLayerOrderTest` の対応表（`Client.Tests/Ui/Layers/UiLayerOrderTest.cs:36-48`）にも無い

### 6.4 重複

- `UiText`（Key＋Params）と `LocalizedLabel.SetKey(key, params)` が同じ組を別々に持つ（LocalizedLabel は UiText を受けない）。`BugReportFormText.DescribeDelivery` は string、`DescribeStatus` は UiText と、文の型が混在
- `GameUiToast` は解決済み文字列を持つので、表示中の言語変更に追従しない（3 秒で消えるので実害は小さい。推測）
- albof.16.2（screens D-25）: `EventLanguageGate.TrySelectLanguage` と `SetLanguage` の結果型、`GraphicsQualityWire.PresetToWire` と `SettingsModel` のプリセット導出の重複、設定の 3 topic を `SettingsModel` の購読へ寄せる——未着手
- テストの土台が `UiTestHost`→`ScreenTestHost` の 2 段で、`ScreenTestHost` が言語固定・カタログ読み込み・トースト収集を持つ。段階2の画面テストもこれを継承する前提

### 6.5 本番未使用の基盤

`TutorialAnchorRegistry`・`UiInertiaAnimation`・`TranslateYTweenStep`・`ScrollOffsetTweenStep`・`UiNumberFormat`・`GamePanel` は本番コードから参照されていない（テストのみ。grep 実測）。段階2以降の先行基盤として残す判断は review D-5（`fd898b6344`）で記録済み

---

## 7. 計画（docs/superpowers/plans/2026-11-05-uitk-stage1-*.md）と実装のずれ

| 計画の記述 | 実装 | 経緯 |
|---|---|---|
| skeleton: 入力排他は `WebUiInputExclusivity` | `UiInputExclusivity` に改名・汎用化 | input plan R-1 で予定どおり |
| skeleton R-5: 所有表の初版は全部 Web（R-11 見た目不変） | ポーズ系 3 行が GameUi | screens R-7 で切替。今は全体スイッチの裁定で再び作り直し対象 |
| skeleton Task 3: `GameUiRoot` は `StyleSheet[] tokenStyleSheets` を持ちルートへ当てる | トークンは `GameUiTheme.tss` から当たり、`GameUiRoot` は `UiAssetCatalog` を持つ | theme D-7・screens D-6 |
| skeleton Task 1: `UiRoutingSnapshot{Screen, WebUiSuspended}`、`UiRoutingState : IInitializable` | `MainPanelOwner` を追加、`IDisposable` を追加し Editor 上書きを購読 | screens D-20、review D-10（`b595f00f48`） |
| skeleton R-6: Web 休止は RawImage・入力値・App unmount | 加えて休止中の `ui_state.request` を拒否 | review D-1（`bdaa84c383`）。計画に無い |
| theme R10: 登録簿は「Attach で登録・Detach で同期解除」 | 状態を持たず木を照会する static `Resolve` | theme D-8 |
| theme R7: GamePanel は default のみ | 同じ（ただし本番画面から未使用になった） | web-look |
| input R-10 / File Structure: `GameTextField.uss` を作る | 存在しない。見た目は `BugReport.uss` | 記録された判断は見つからなかった（未確認） |
| screens R-3・D-27・D-28: ポーズは GamePanel の様式、testid は GamePanel に付ける | Web `.panel` の写し、testid `pause-menu` は `pause-menu__panel` に付く（`PauseMenuView.cs:52-54`） | 裁定 `2026-11-06-UITK段階1の画面はWeb版と同じ見た目にする.md`（web-look `75b0142a79`）。plan 本文は未更新 |
| screens R-10・D-17: トーストは GamePanel 配色の小窓＋左帯 | Mantine Notification の写し | 同上 |
| screens R-6: トーストの文は `<コマンド名> failed: <コード>` | 拒否理由ごとのローカライズ文言（失敗コードはログだけ） | review D-8（`3e946e4bd3`） |
| screens R-4: 画質行は testid の文字列で組む | `GraphicsQualityRow<TValue>` の型付き行抽象 | review D-4（`95586cf85b`） |
| screens R-8: `CefProcessState.Running` | `ObservedAfterBrowserStart` に改名、起動失敗時は観測を始めない | review D-3（`d1bfc4afbd`） |
| screens R-15: IME を Mac/Windows 両方で確認 | 一部合格（Windows の Web 欄・Mac 配布ビルド全画面・Retina は未測） | lane-screens-done.md「未完了」 |
| 各 plan の D-1: レーンは moores-code-review を行わず統合後に指揮が行う | 統合後に moores-code-review を実施し D-1〜D-10 を反映（`5421c7f9f0`〜`0d75fa453c`） | — |
| skeleton の前提: 束ごとに暗転→所有表で切替 | 裁定で長命ブランチ＋全体スイッチへ転換（2026-11-06） | albof.17 |

---

## 8. 関連: CEF 停止の記録（albof.18）の切り出し範囲

master へ切り出す対象として feature/uitk 上で特定できたもの（`19b34304aa`・`271cfcec4b`・`d1bfc4afbd`・`19512764e2` の差分から）:

- Client.Game: `InGame/BugReport/WebUiHealth/{IWebUiHealthProbe,WebUiHealthReading}.cs`、`BugReportManifest.cs`（schemaVersion 6）、`BugReportBundleWriter.cs`、`LastSession/CrashBundleWriter.cs`
- Client.WebUiHost: `Health/{WebUiHealthProbe,WebUiHealthRecorder}.cs`、`Boot/WebUiSilenceWatcher.cs`、`Boot/WebSocketMessageDispatcher.cs:33`（受信時刻の記録）、`Cef/WebUiCefNavigator.cs`
- Client.Input: `UiInputExclusivity` の無応答部分（`:21-23,115-146` と `RecomputeTextInputFocused` の `_webSilent` 条件）。master は改名前の `WebUiInputExclusivity` なので、その上へ移植する形になる
- Client.Starter: `WebUiStartup.cs`（起動失敗の LogError 化・`RecordHostStartup`）、`PlaytestRecordRegistration.cs`。`WebUiSilenceWatcher` の登録は feature/uitk では `MainGameUiRegistration` にあり、master には同ファイルが無い
- テスト: `UiInputExclusivityWebSilenceTest`・`WebSilenceThresholdWebParityTest`・`Bundle/WebUiHealthManifestTest`・`LastSession/CrashBundleWebUiHealthTest`・`FixedWebUiHealthProbe` と、manifest の版を見ている既存テスト数本
- 注意: `IBugReportOutboxRoot` の注入（review D-2 `4a80048bf4`）が同じファイル群に絡む。切り出しに含めるかは未判断

---

## 9. 未確認・調べきれなかったこと

- web-look 以降の HEAD（`54c8a4c8c8`）でのコンパイル・テスト成否（Unity 未起動。`BugReportDescriptionHeightTest` の修正後の通過は前任も未確認＝handoff-bigbang-switch.md）
- `Client.Ui.asmdef` が uGUI（RawImage）を参照名に持たずにコンパイルが通っている理由（CI は通っている。asmdef の参照解決の仕組みは未調査）
- `GameTextField.uss` を作らなかった判断の記録の有無
- 2560×1080 のグリフ欠け（screens 要裁定 4）・Mac Editor 縮小時の候補窓ずれ（同 5）の現状
- Web 版との見た目の差は 4 画面の 720p 比較画像でのみ確認。2560×1080・英語・韓国語での比較は見ていない
- ref-uitk-design スキルは repo の `.agents/skills/` に無く、git 履歴にも見つからなかった（albof.19 の対象が実在しない）
