# UITK 移行調査: 入力・UI 状態・非 Web UI

調査日 2026-10-22。対象は master `e8e611f1c0`（moorestech リポジトリ）。読むだけで、ファイル・git・bd には何も書いていない。
パスは repo からの相対パス。`C#` は `moorestech_client/Assets/Scripts/`、`web` は `moorestech_web/webui/src/` を指す。
cef-unity のソースは repo に無いため、`moorestech_client/Library/PackageCache/jp.juha.cefunity@8043d36a5f6d/Runtime/CefUnityBrowserSample.cs` で読んだ（以下 `CefUnityBrowserSample.cs`）。master のピン（hash 012281d）とは版が違う可能性がある。
「推測」と書いたものは実測していない。

---

## 0. 結論

1. **今は Web とゲームが入力を「両方とも受け取り」、あとから排他している。** cef-unity は毎フレーム、レガシー `UnityEngine.Input` のマウス・キー・ホイール・IME をすべて CEF へ送る。ゲームは InputSystem で同じ入力を読む。排他は Web が WebSocket で送ってくる `pointerOverUi` / `textInputFocused` の2値だけで行っている。この2値は**非同期で1フレーム以上遅れる**。プレイテスト DSL にはこの遅れの待ち合わせが入っている（`C# Client.Playtest/Operations/Ui/PlaytestUiOps.cs:134-138`）。ESC の二重配送を防げているのも、この遅れのおかげである（`docs/webui/design/input-focus-exclusivity.md`）。
2. **UITK 化すると「同じプロセス・同じフレーム・同期判定」になり、入力の取り合いの大半が消える。** WS の `input_state`、CEF への入力転送、IME の常時 ON、Windows の Raw Input 奪取、プレイテスト用の `CefInputForwarder` / `CefScreenMapper`、DOM クエリはすべて不要になる。
3. **代わりに新しく作るものは6つある。**
   - (a) `UiPointerHitTest` を `panel.Pick` による同期判定へ置き換え、透明要素を `PickingMode.Ignore` にする規約を決める。
   - (b) MainGame の `InputSystemUIInputModule` は既定の `DefaultInputActions` を使っており、Navigate に **WASD・矢印が入っている**。メニュー中も WASD で歩く仕様とぶつかるため、UI 用のアクションを差し替え、フォーカスの方針を決める。
   - (c) テキストのフォーカスを同期で追跡し、ESC を同じフレームで消費する。
   - (d) Web 側にあった画面ルーティング表・層・ホイールの装備切替・キー処理を C# へ移す。
   - (e) 名前付きプレースホルダ（`{count}` 等）の補間と、数値書式のカルチャ固定。
   - (f) TextCore の FontAsset と、フォールバックの連鎖。
4. **ローカライズは「C# へ戻す」というより「C# 側の既存の正本へ一本化する」話である。** C# の `Localize` は、辞書の合成、Web と同じ解決順（対象言語→english→source→`[!key]`）、型付きキー、`ContentLocalizationKeys.ItemName(guid)` 等の生成を既に持っている。足りないのは名前付き補間・言語切替時の再バインドの型・数値書式の3つだけである。ADR 0006 の決定5「Web 側解決」は「ビュー側解決」へ読み替える改訂が要る。
5. **非 Web の UI は uGUI 4系統（タイトル/ロード、HP バー、デバッグ、`UiPointerHitTest` の EventSystem 判定）と、死んだ UITK（SkitUI）である。** CEF が消えると、ADR 0052 が uGUI を残した例外1（CEF の描画面）が無くなる。例外2（タイトル/ロード）を残した理由「CEF はゲーム内でしか起動できない」も消える。ただし Unity 6 では TMP が `com.unity.ugui` に同梱されており、デバッグ系の外部パッケージ（UnityDebugSheet・Graphy・IngameDebugConsole）も uGUI 製である。このため「ugui パッケージを外す」かどうかは別の判断として裁定が要る。
6. **移行中に共存させるときの制約がある。** cef-unity は RawImage 全面でマウスをすべて CEF へ送るため、領域単位で入力を止められない（推測。API が見当たらない）。画面単位の strangler は成り立つが、**D&D や掴みで画面をまたぐ組は同時に移す必要がある**。インベントリ・ブロックインベントリ・装備 HUD・GrabOverlay の組と、ビルドメニュー・ホットバーの組がそれにあたる。

---

## 1. 現在の入力と UI 状態の流れ

### 1.1 全体図

```
               ┌────────────────────────── Unity メインプロセス ───────────────────────────┐
 OS 入力 ──┬─► InputSystem ─► InputManager(InputKey) / HybridInput ─► UIStateControl.Update
           │                       │  ▲ textInputFocused で keyboard 系をゼロ化          │ 現 state の GetNextUpdate()
           │                       │  │                                                 ▼
           │                       │  WebUiInputExclusivity（static, Client.Input）   IUIState 12種（GameScreen 等）
           │                       │        ▲ SetState(pointerOverUi, textFocused)       │ OnEnter/OnExit で
           │                       │        │（WS 受信スレッド）                          │ カーソル/カメラポリシー
           │                       ▼        │                                           ▼
           │        ワールド操作 23ファイル 28箇所 ─► UiPointerHitTest.IsPointerOverAnyUi()
           │                    = EventSystem.IsPointerOverGameObject() || IsPointerOverWebUi
           │
           └─► legacy Input ─► CefUnityBrowserSample.OnEarlyUpdateLast（毎フレーム）
                  mouse move/click/wheel(native) / IME / key ─► CEF（別プロセス Chromium）
                                                              │ DOM で判定
                                       op:"input_state"  ◄────┤ pointermove / focusin / focusout
                                       action:"ui_state.request" 等 44種 ◄─┤ クリック・キー処理
                                       topic:"ui_state.current" 等 28種 ──►│ 画面ルーティング（uiScreenRouting.ts）
               └──────────────── Kestrel + WebSocket（Client.WebUiHost） ─────────────────┘
```

### 1.2 UI ステート機械（C# が主権を持つ）

| 要素 | 実体 | 根拠 |
|---|---|---|
| 状態機械 | `UIStateControl`（MonoBehaviour）。`Update` で「Web からの遷移要求 → 現 state の `GetNextUpdate()`」の順に1遷移する | `C# Client.Game/InGame/UI/UIState/UIStateControl.cs:44,54` |
| 状態 | `UIStateEnum` 12種（GameScreen / PlayerInventory / SubInventory / PauseMenu / DeleteBar / Story / PlaceBlock / ChallengeList / ResearchTree / Debug / TrainHUDScreen / BuildMenu） | `UIStateEnum.cs` |
| state の契約 | `OnEnter` / `GetNextUpdate`（ポーリングで遷移を返す）/ `OnExit` / `LocksPlayerMovement` / `AllowsSprintToggle` / `GetKeyHints`（ADR 0032） | `State/IUIState.cs` |
| 入れ子ポーズ | Story と TrainHUD は `INestedPauseScreenState` でポーズを入れ子に持つ（ADR 0035） | `State/NestedPause/*`, `State/SkitState.cs` |
| Web からの遷移 | `ui_state.request` は GameScreen / PlayerInventory だけを受け付け、許可表 `IsAllowed` で検査してから `RequestTransition` を呼ぶ | `Client.WebUiHost/Game/Actions/UiStateActions.cs` |
| Web への配信 | `ui_state.current` はフレーム末に state 名・subState・keyHints を JSON で送る。Web 側は `screenForUiState` + `SCREEN_TRAITS` 表（grab / pauseMenu / skitInput / backdrop / alwaysOnHud / trainHud）で描く画面を決める | `Client.WebUiHost/Game/Topics/UiStateTopic.cs`, `web shared/uiState/uiScreenRouting.ts` |
| 状態変化の通知 | `public event Action<UIStateEnum> OnStateChanged`。AGENTS.md の「イベントに Action を使わず UniRx を使う」に反する既存形で、UITK 側から購読するときに直す対象 | `UIStateControl.cs:16` |
| UI 全体の非表示 | Ctrl+U で `UIRoot._isActive` を反転し、`ui.visibility` topic で Web 全体を unmount する | `UIState/UIRoot.cs`, `web app/App.tsx:86` |

### 1.3 カーソル・カメラポリシー

- `UiStateCameraPolicyService` は4ゾーンを持つ: Gameplay（常時回転。TPS の左 Alt ホールド中だけ自由カーソル）、Menu、Build（視点で分岐）、Neutral。照準ソース（ScreenCenter / Cursor）もここが AimPointProvider へプッシュする（`State/CameraPolicy/UiStateCameraPolicyService.cs`）。Alt 自由カーソルは `.decisions/2026-08-05-Alt自由カーソル*` の一連の裁定に従う。
- ロックする遷移の直前にはカーソルを画面中央へワープする（ADR 0020、`Client.Input/InputManager.cs` `MouseCursorVisible`）。
- **ゾーン方式を通らない state も残っている。** PlayerInventory / SubInventory / ChallengeList / ResearchTree / PauseMenu / Debug / Skit / TrainHUD のサブステートは、`InputManager.MouseCursorVisible(bool)` を直接呼んでいる（grep: `MouseCursorVisible(` 13箇所。`GameStateController.cs:55-69` も呼ぶ）。UITK とは直接関係しないが、カーソルの所有者は2系統ある。
- これらは純 C# で、UITK 化しても変わらない。変わるのはカーソル追従 UI（ツールチップ・掴み）の座標の出どころだけである（§3.5）。

### 1.4 入力の取り合い（どこで Web とゲームが同じ入力を受けているか）

| 入力 | ゲーム側の受け口 | Web 側の受け口 | 今の排他 | 根拠 |
|---|---|---|---|---|
| マウス位置・クリック | InputSystem（`InputManager.Playable.ScreenLeftClick` 等、`HybridInput.GetMouseButton*`） | CEF が legacy `Input.mousePosition` と `GetMouseButtonDown` を毎フレーム EarlyUpdate で送る | ワールド操作側が `UiPointerHitTest.IsPointerOverAnyUi()` に問い合わせる（23ファイル28箇所）。Web 側は `data-web-ui-transparent` 以外の要素の上を `pointerOverUi=true` として WS で送る | `CefUnityBrowserSample.cs:553-587, 975-1010`, `C# Client.Game/InGame/Control/UiPointerHitTest.cs`, `web shared/uiState/activeLayer.ts`, `useWebInputExclusivity.ts` |
| 右短押しで UI を閉じる | `RightShortPressInputService` が「パネル外」で成立させる（ADR 0046） | Web のスロットは右クリックで半分・1個置き | 上と同じ `isPointerOverUi`（WS 由来なので遅れる） | `State/CancelInput/RightShortPressInputService.cs` |
| キーボード（ゲームキー） | `InputKey`（`InputSuppressionScope.Keyboard` 付き19個）と `HybridInput.GetKey*`（呼び出し35箇所） | CEF が `Input.inputString` と特殊キー表を送る | `textInputFocused` の間だけ C# 側でゼロ値にする（GetKeyUp は通す）。Web 側は Tab と、入力欄以外の Space の既定動作を封じる（`.decisions/2026-10-01-Web UI全体で文字入力欄以外のSpace既定動作を封じる.md`） | `Client.Input/InputManager.cs`, `HybridInput.cs:94-99`, `web shared/uiState/activeLayer.ts:39-43` |
| ESC | C# の各 state が `InputManager.UI.CloseUI` を見る。PauseMenu は1段戻る（`PauseMenuStateService.HandleCloseKey`） | Web も ESC を処理する: 入力欄の blur（`useWebInputExclusivity.ts`）、ブロックインベントリを閉じる `ui_state.request`（`features/blockInventory/BlockInventoryKeyHandler.tsx`）、レシピ選択の解除（`features/recipe/selection/RecipeSelectionKeyHandler.tsx`） | **二重に処理している。** 入力欄で ESC を押したフレームに C# の CloseUI が発火しないのは、WS の遅れで旧フォーカス状態が残るからにすぎない（`docs/webui/design/input-focus-exclusivity.md`「Escを押したフレームでは旧フォーカス状態が…」）。SubInventory の ESC は C# と Web の両方が閉じに行く（同じ state への要求なので害は無い） | `State/SubInventoryState.cs:83-86` |
| 矢印・Enter | — | ベルト流量の窓切替（←→）、クラフト（Enter）、スキット送り（Enter） | Web の中で閉じている | `features/blockInventory/details/BeltThroughputSection.tsx`, `features/recipe/views/CraftRecipeEntry.tsx:73`, `features/skit/SkitPresentation.tsx:68` |
| ホイール | `ElectricWirePoleSelection`（Mouse.scroll。UI 上なら0） | CEF のネイティブ取得（macOS は NSEvent ローカルモニタ、Windows は Raw Input）を Web へ送る。**装備の循環は Web 側が決めて** `inventory.select_equipment` を送る（`data-wheel-passthrough` の HUD 上は通す） | ゲーム側は `IsPointerOverAnyUi`、Web 側は `readActiveLayer()` と `wheelOwnedByBuildTool` | `CefUnityBrowserSample.cs:256-270`, `web features/inventory/EquipmentPanel/index.tsx:55`, `.decisions/2026-09-13-CEF raw input応急処置…md` |
| 数字キーとホットバーのクリック | `HotbarTapInputService` が数字キーと Web の `hotbar.select` を1つの入口で消費する | ホットバーのクリック・D&D（`hotbar.assign/swap/clear`） | 入口が1つなので取り合いは無い | `State/Hotbar/HotbarTapInputService.cs` |
| IME | — | CEF が起動時に `Input.imeCompositionMode = On` を**常時**設定し、composition を CEF へ転送する | ゲーム中も IME は ON のまま（推測: 日本語 IME が有効だと WASD 中に変換窓が出る可能性がある。未実測） | `CefUnityBrowserSample.cs:802-805, 857-912` |
| テキスト入力中の移動停止 | `TextInputMovementLockApplier` が `TextInputFocused` を購読して移動を止める | 入力欄は4つ（駅名、バグ報告、ビルドメニュー検索、モーダル入力〈BP名〉） | WS 由来の ReactiveProperty。受信スレッドから来るので `ObserveOnMainThread` で受ける | `C# Client.Game/InGame/Player/PlayerSystemContainer.cs:37`, `web features/*`（grep `<input|TextInput`） |
| プレイテストの入力注入 | `InputSystem.QueueStateEvent` | **CEF に届かない**（CEF は legacy Input を読むため）。そこで Editor 専用の `CefInputForwarder` が InputSystem の値を CEF へ再送する。要素は `PlaytestDomQuery` で DOM の testid を引く | — | `C# Client.Playtest/WebUi/CefInputForwarder.cs`, `CefScreenMapper.cs`, `Operations/PlaytestWebUiOps.cs`。testid 系の操作を使うシナリオは 24 / 90（`.agents/skills/unity-playmode-recorded-playtest/scenarios`） |

前提の設定:
- `ProjectSettings.asset:1025 activeInputHandler: 2`（Both）。CEF が legacy Input を読むために必要である。
- MainGame の EventSystem は `InputSystemUIInputModule` で、`m_ActionsAsset` は InputSystem 同梱の `DefaultInputActions`（guid `ca9f5fa95ffab41fb9a615ab714db018`）を参照している（`Assets/Scenes/Game/MainGame.unity:8435`）。MainMenu と GameInitialaizer は `StandaloneInputModule` である。

### 1.5 今の方式から出ている実害・負債

| 事象 | 根拠 |
|---|---|
| `pointerOverUi` が非同期で届くため、UI をクリックした直後の設置クリックが弾かれる競合がある（DSL に待ちを入れて回避している） | `Client.Playtest/Operations/Ui/PlaytestUiOps.cs:134-138` |
| カーソルをロックすると DOM に pointermove が届かず、`pointerOverUi` が古い値のまま残る。統一クエリ側で「ロック中は false」として無害化している | `UiPointerHitTest.cs:13`, `input-focus-exclusivity.md` |
| Windows の CEF Raw Input による入力奪取が未解決（実害が確認されたら根治する、という裁定） | `.decisions/2026-09-13-CEF raw input応急処置は入れず…md` |
| 入力の二重配送・IME・フォーカス往復後の入力復活の実機検証が未完了 | `docs/webui/TODO.md:25-26`（A2） |
| CEF は座標が「変化したときだけ」mousemove を送るため、ワープしても Web 側のカーソル追従 UI が更新されない経路がある | ADR 0020「実装後の実測」、ADR 0074 |
| ワールドピンを毎フレーム C# で射影し、JSON にして WS で送っている（描画は少なくとも1フレーム遅れる。推測） | `Client.Game/InGame/Tutorial/Presentation/WorldPinStateStore.cs`, `Client.WebUiHost/Game/Topics/Tutorial/WorldPinTopic.cs` |

---

## 2. UI Toolkit 化で単純になる点

| 今あるもの | UITK 化後 | 消えるコード・機構 |
|---|---|---|
| WS の `input_state` で届く `pointerOverUi`（非同期・残留） | 同じフレームで `panel.Pick` で判定する（§3.3） | `WsClientMessage.PointerOverUi/TextInputFocused`、`WebSocketMessageDispatcher.cs:51`、`WebSocketHub.cs:159` の切断時リセット、`web useWebInputExclusivity.ts` / `activeLayer.ts` の大半、DSL の待ち合わせ |
| CEF への入力転送（legacy Input・EarlyUpdate フック・キーリピートの自前実装・ネイティブホイール・Raw Input） | UITK は InputSystemUIInputModule 経由で同じ InputSystem を読む | cef-unity の入力経路まるごと。Raw Input 奪取の懸案（2026-09-13 裁定）も消える |
| IME の常時 ON | TextField がフォーカスを持っている間だけ IME を扱う（推測。§3.4 で実機検証が要る） | `SetupImeProxy` の副作用 |
| プレイテストの CEF 橋渡し | InputSystem の注入が EventSystem 経由でそのまま UITK に届く | `CefInputForwarder` / `CefScreenMapper` / `PlaytestDomQuery`（約440行）。`PlaytestWebUiOps` は testid を VisualElement の name で引く形に差し替えれば、シナリオは無改変で済む |
| Web → C# の操作（action 44種）は JSON・requestId・メインスレッド移送・結果の往復を伴う | ビューから C# のサービスを直接呼ぶ（同期） | `IActionHandler` の JObject パース層、`ui_state.request` の許可表は C# の `UIStateControl` 側へ移す |
| C# → Web の状態配信（topic 28種）は revision・snapshot・デバウンスを伴う | ビューが Model を UniRx で購読する（AGENTS.md「イベントパケット＋初期データ＋購読」のクライアント内部版） | `UiStateTopic` 等の JSON DTO 層 |
| ESC・矢印・Enter・ホイールが C# と Web に散っている | キー処理はすべて C#（UIState か画面の presenter）の1箇所に集める | 二重の ESC ハンドラ |
| テキスト選択を禁止する CSS（ADR 0021） | UITK の Label は既定で選択できない | ADR 0021 の対応がほぼ不要になる |
| 辞書が届く前の文言表、辞書取得の失敗・再試行（`preDictionaryText.ts`、`DictionaryIndependentText`、`provider/dictionaryRetry`） | `Localize` は `BeforeSceneLoad` で同期に初期化されるため「辞書が無い」状態が存在しない | i18n の取得ライフサイクル全体 |
| 接続断オーバーレイ、再接続時の再送 | プロセス内なので「切断」が無い | `ConnectingPlaceholder`、再接続まわり |
| ワールドピン・カーソル追従を WS で送っている | 同じフレームで style を更新する | 遅れ |

---

## 3. 新たに作る必要がある点

### 3.1 パネル構成と層

| 論点 | 現状（Web） | UITK で要るもの | 推奨 |
|---|---|---|---|
| 拡縮 | 1280×720 の `.stage` を `Math.min(w/1280, h/720)` で一様に拡縮する（`web app/App.tsx:39`）。stage 族と viewport 族の二分は ADR 0013 | PanelSettings を Scale With Screen Size、基準 1280×720、`screenMatchMode = Expand` にする（scale = min になり、root は基準以上の大きさになる）。root 直下に「中央 1280×720 の stage コンテナ」と「root の辺に付く viewport 族」を置く | 既存の `SkitPanelSettings.asset`（1920×1080・MatchWidthOrHeight）は流用せず、新しく作る |
| 層の順序 | z-index のトークン（ADR 0013/0017）: backdrop → 通知（stage の背面）→ stage → viewportOverlay → Modal → GrabOverlay → Toast・チュートリアル・ワールドピン | z-index が無い（PoC）。兄弟順で固定した層コンテナを root に並べる | パネルは1枚にし、層は兄弟コンテナで表す（Pick とフォーカスが1パネルで閉じるため。§3.3） |
| 透明な層 | `data-web-ui-transparent` と CSS の `pointer-events:none`（`.viewport` / `.stage` / `.viewportOverlay` / `.backdrop`） | 層コンテナと backdrop を `PickingMode.Ignore` にする規約。**忘れると「パネル外の右短押し」が成立せず、ワールドクリックも死ぬ** | 層コンテナ生成時の既定にし、テストで固定する |
| ホイールを通す印 | `data-wheel-passthrough`（ホットバー・装備 HUD） | クラス名（例: `wheel-passthrough`）を付け、Pick の結果の祖先を見る | §3.3 の判定に含める |

### 3.2 PanelSettings と InputSystem の連携

| 論点 | 事実 | 対応案 |
|---|---|---|
| 入力経路 | MainGame には EventSystem + `InputSystemUIInputModule` がある。UITK は EventSystem があればそれを経由して入力を受ける（推測。一般仕様として。要実測） | EventSystem を残す（デバッグの uGUI も使う）。InputSystem への注入がそのまま UITK に届く |
| **Navigate に WASD が入っている** | `DefaultInputActions` の UI/Navigate には `<Keyboard>/w,a,s,d` と矢印が束縛されている（パッケージキャッシュの `DefaultInputActions.inputactions` で確認）。メニュー中も WASD で移動できる仕様（`.decisions/2026-10-01-メニュー中の視点は回さず移動だけ解放する.md`）とぶつかる。ボタンにフォーカスがあると、WASD でフォーカスが動いて歩きもする（推測: UITK の NavigationMoveEvent の挙動から） | moorestech 専用の UI アクション資産を作る。Point / Click / RightClick / MiddleClick / ScrollWheel は残し、Navigate は空か矢印だけにする。さらに入力欄以外のコントロールを `focusable=false` にする |
| Submit / Cancel | Submit は `*/{Submit}`（キーボードでは Enter）、Cancel は `*/{Cancel}`（Escape） | Cancel は使わない（ESC は C# の UIState が持つ）。Space は Submit に含まれないはず（推測）。2026-10-01 の Space 裁定を守るため実測が要る |
| Tab | Tab はインベントリの開閉キー。Web は Tab のフォーカス移動を preventDefault している | UITK の Tab によるフォーカス移動（NavigationMoveEvent Next）を止める（推測: focusable=false の徹底で足りる。要実測） |
| カーソルロック中 | `m_CursorLockBehavior: 0`（OutsideScreen）のため、ロック中の UITK ポインタは画面外として扱われる（`MainGame.unity:8448`） | 今の「ロック中は UI に乗らない」と同じ意味になる。そのまま使える |
| イベント処理の順序 | EventSystem の Update（UITK のイベント処理）と `UIStateControl.Update` の先後は保証されていない（推測） | 「UI のクリック → C# 遷移要求」は既存の `RequestTransition`（次の Update で最優先に消費）に乗せれば順序に依存しない。ESC の消費だけは §3.4 のラッチで同じフレーム内の先後を固定する |
| ゲームパッド | **正式対応外**（`docs/adr/0082-sprint-toggle-mode-default.md:27`）。入力資産でもパッドの束縛は Player マップ（Move / Look / Jump / Sprint）だけで、UI マップには無い（`Asset/Common/moorestechInputSettings.inputactions`） | UI のパッド操作は作らない。UITK のナビゲーションは上の理由で切る。将来対応するときは focusable の設計からやり直すことになり、今の判断でその道が塞がるわけではない |

### 3.3 ゲーム入力とのブロック判定

`UiPointerHitTest.IsPointerOverAnyUi()` は全ワールド操作（23ファイル28箇所）が通る唯一の関門である。中身だけ差し替えれば、呼び出し側は無改変で済む。

| 案 | 中身 | 長所 | 短所 |
|---|---|---|---|
| A. パネルを Pick する（推奨） | ロックしていなければ `RuntimePanelUtils.ScreenToPanel` で座標を変換し（y を反転）、`panel.Pick(pos)` が null でなく、かつ層コンテナでもなければ true。uGUI（デバッグ）の分は `EventSystem.IsPointerOverGameObject()` との論理和で残す | 同じフレームで同期に決まる。透明・ホイールを通す印の判定も同じ場所で書ける | パネルが複数あるとパネルごとに Pick が要る（→ 1パネル構成を推す理由） |
| B. EventSystem に任せる | `EventSystem.current.IsPointerOverGameObject()` だけにする（UITK は PanelRaycaster 経由で当たる。推測） | 1行で済む | InputSystemUIInputModule は前フレームの raycast 結果を返し、イベント処理中に呼ぶと警告を出す（推測: 既知の仕様）。1フレーム遅れが残る |

ホイール: Web の `useGameLayerWheel` にある装備循環（`EquipmentPanel/index.tsx:55-80`）を C# へ移す。条件は「UI 上なら止める（印のある HUD の上は通す）、建築ツールがホイールを占有していたら止める、1枠以下なら何もしない」で、これを C# の1箇所へ写す。Web で見られる `pendingSelections` の楽観更新は、同期呼び出しになれば単純化できる見込みである（推測）。

### 3.4 テキスト入力・IME・ESC

| 論点 | 対応 |
|---|---|
| フォーカスの追跡 | root で `FocusInEvent` / `FocusOutEvent` を TrickleDown で受け、`focusController.focusedElement` が TextField かを `WebUiInputExclusivity`（`UiInputExclusivity` へ改名）の ReactiveProperty へ同期で書く。受信スレッドから来なくなるので lock と `ObserveOnMainThread` は不要になる |
| **ESC の同フレーム問題** | 今は WS の遅れで偶然防げている。UITK では TextField 上の ESC で blur するとそのフレームの抑止が外れ、`CloseUI` まで発火して画面が閉じる恐れがある（推測: EventSystem の処理が UIStateControl より先に走る場合）。「このフレームの ESC は UI が消費した」ラッチを `InputKey` の抑止判定に足す |
| IME | PoC で未検証（PoC レポート「未検証」）。Both 設定のままなら UITK TextField は legacy の `Input.compositionString` を使うはず（推測）。日本語 IME の変換・確定・取消を4つの入力欄で手動確認する必要がある。CEF が設定していた `imeCompositionMode=On` の常時化は消えるので、ゲーム中の IME 挙動は変わる（良い方向のはず。推測） |
| Ctrl+C/V 等 | CEF は Copy / Paste / SelectAll を自前で中継していた（`CefUnityBrowserSample.cs:1185-1189`）。UITK の TextField は標準で持つ（推測） |
| 入力欄の棚卸し | 駅名（`features/blockInventory/details/station/TrainStationNameSection.tsx`）、バグ報告の本文（`features/pauseMenu/BugReportForm.tsx`）、ビルドメニュー検索（`features/buildMenu/views/BuildMenuSearchInput.tsx`）、モーダル入力〈BP 名〉（`features/modal/ModalHost.tsx`）の4つ |

### 3.5 カーソル追従 UI（ツールチップ・掴み）

- 今は CEF の mousemove で Web が位置を決めている（ADR 0019 で書式は Web 側、ADR 0074 で要素付与時に位置を再計算）。UITK では `Mouse.current.position` か root の `PointerMoveEvent` から、同じフレームで置く。
- ロック中（Gameplay）は UITK のポインタが画面外になる（§3.2）。クロスヘア基準のツールチップ（採掘中の「左クリックで取得」等）は、「ロック中は画面中央」という規則を C# 側で明示する。ADR 0020 のワープ前提は不要になる。
- ツールチップの中身は既に C# の Model（`Client.Game/InGame/UI/Tooltip/MouseCursorTooltipState.cs`、所有者トークンの排他は 2026-08-22 裁定）にある。表示層だけ作ればよい。PoC の罠「ドラッグ中も下の要素に PointerEnter が届く」の抑止を最初から入れる。

### 3.6 Web 側にしか無い UI 状態の C# 移設

| Web 側の状態・規則 | 移す先（案） | 根拠 |
|---|---|---|
| `screenForUiState` + `SCREEN_TRAITS`（画面ごとの grab / pause / skitInput / backdrop / alwaysOnHud / trainHud） | C# の画面ルータ。UIStateControl の状態変化を UniRx で購読し、表を C# に写す | `web shared/uiState/uiScreenRouting.ts`（vitest 9ケース） |
| 最前面レイヤーの導出 `deriveActiveLayer`（modal > blockInventory > research > buildMenu > game） | 不要になる見込み（キー処理を C# の UIState に寄せれば、層での抑止が要らない）。残すなら C# の1関数 | `activeLayer.ts`（17ケース） |
| ビルドメニューのセッション状態（タブ・検索・スクロール位置。2026-08-05 裁定）、詳細の最後のホバー | 画面の Model（MonoBehaviour 非依存） | `.decisions/2026-08-05-ビルドメニュー*` |
| スキットのタイプライタ表示（`visibleCount`）、reveal と advance の判定 | `SkitPresentationStateStore` の側か、ビュー | `web features/skit/SkitPresentation.tsx:63-72` |
| チュートリアルのアンカー登録（DOM の `data-tutorial-anchor`）、祖先のクリップ（ADR 0024）、`tutorial.anchor_ack` | VisualElement の登録簿（anchorId → 要素）。**語彙は変えない**（マスタに `research.node-<guid>` / `recipe.craft-button` / `challenge.current-hud` が直書きされているため。2026-08-19 裁定） | `web shared/tutorialAnchor/anchorIds.ts`、`../moorestech_master/server_v8/mods`（grep で9件） |
| 通知の集約（2026-08-19 裁定で Web 側に置いた）、トースト | C# の通知 Model | `web features/notification/notificationStore.ts` |
| Web の操作音 `sound.play` | ビューから直接鳴らす | `.decisions/2026-08-26-Web操作音はC#へsound.playを送って鳴らす.md` |

### 3.7 移行中の共存（strangler）で入力について守ること

- cef-unity は RawImage 全面でマウスを CEF へ送り続ける。moorestech 側から領域単位で止める API は無い（推測。`CefUnityBrowserSample` は READ 権限のみの外部パッケージで、読んだ範囲に無効化 API は見当たらない）。UITK のボタンの真下に Web の実要素があると、両方がクリックを受ける。
- そのため、同じ画面状態で同じ領域を2系統が描かないように、画面単位で UIState からどちらか一方へ振り分ける必要がある。共存中の判定は `IsPointerOverAnyUi = Pick(UITK) || IsPointerOverWebUi || EventSystem`、テキストフォーカスは `UITK || Web` の論理和にする。
- **D&D・掴みで画面をまたぐ組は同時に移す。** ドラッグの途中で描画系統をまたげないからである。
  - インベントリ・ブロックインベントリ・装備 HUD・GrabOverlay・カーソルツールチップ。掴みは `screenAllowsGrab` を共有している（`web shared/uiState/useGrabInteractive.ts`）。
  - ビルドメニューとホットバー。ホットバーへの割当はビルドメニューからの D&D だけ（`web features/hotbar/hotbarDnd.ts`）。
  - 議論記録の「第一弾はインベントリかホットバー」は、この組単位で考えることになる。
- UITK パネルと uGUI オーバーレイ Canvas（CEF）の描画順は、PanelSettings と Canvas の sortingOrder で決まる（推測。要実測）。

### 3.8 テストとプレイテスト DSL

- `PlaytestWebUiOps` は testid の文字列 API である（`ClickWebUi` / `HoverWebUi` / `WaitWebUiElement` / `WaitWebUiTextContains` / `ScrollDownUntilWebUiElementIsClickable`）。testid を VisualElement の `name` として持てば、シナリオ24本の差し替えは実装側だけで済む。
- 録画は `GameViewInputSettings`（`Client.Playtest/Recording/PlaytestRecorder.cs:33`）、バグ報告の動画は `ScreenCapture.CaptureScreenshotIntoRenderTexture`（`Client.Game/InGame/BugReport/Recording/ScreenFrameReader.cs:24`）で撮っている。PoC は `ScreenCapture.CaptureScreenshot` に UITK が写ることを確認した。Recorder に写るかは要実測（推測では写る）。uloop の screenshot の rendering モードには写らない（PoC）。
- 仕様の正本として移すべき入力・状態系の vitest（ケース数）は次のとおり。`features/tutorial/tutorialAttentionDesign.test.ts`（21）、`shared/uiState/activeLayer.test.ts`（17）、`features/tutorial/overlay/TutorialOverlay.test.ts`（17）、`shared/i18n/useI18n.test.ts`（16）、`shared/uiState/uiScreenRouting.test.ts`（9）、`shared/tooltip/CursorTooltip.test.ts`（6）、`features/skit/interaction.test.ts`（5）。Playwright は `e2e/tests/regression/inputLayer.spec.ts`、`system/i18n.spec.ts`、`system/pauseMenu.spec.ts`、`system/tutorial*.spec.ts`、`skit.spec.ts`。C# 側では `Client.Tests/Player/TextInputMovementLockTest.cs`、`Input/PlayableInteractInputTest.cs` が `WebUiInputExclusivity.SetState` を直接叩いており、改名に追従させる。

---

## 4. ローカライズ

### 4.1 現状

| 層 | C#（`Client.Localization`） | Web（`shared/i18n`） |
|---|---|---|
| 辞書 | `Localize` が `BeforeSceneLoad` でバニラを入れ、マスタのロード後に `MergeGameDictionaries` で mod CSV とマスタ原文を合成し、凍結したスナップショットを公開する | `/api/i18n` で対象言語・english・source の3辞書を取得する（`LocalizationDictionaryEndpoint` / `LocalizationTopic` / `LocalizationLanguagesEndpoint`） |
| 解決順 | 対象言語 → english → source → `[!key]`（`LocalizationTextResolver.cs`） | 同じ（`i18nStore.ts` `createTranslationResolver`） |
| 型付きキー | 生成済み `LocalizationKeys.Ui.*` と `ContentLocalizationKeys.ItemName(Guid)` 等15種（`mooresmaster.Generator/Localization/ContentKeyCodeEmitter.cs`、宣言表は `Localization/content_keys.csv`） | 同じ CSV から `generated/localizationKeys.ts` / `contentKeys.ts` を生成 |
| 補間 | **`{p0}` の位置引数だけ**（`LocalizationTextInterpolator.cs`） | 名前付き `{name}`（任意の名前） |
| 言語切替 | `Localize.OnLanguageChanged`（UniRx） | Web の再描画だけで完結（ADR 0006 決定5） |
| 使用箇所 | 製品コードで `Localize.Get*` を呼ぶのは14ファイル（設置ターゲット名・採掘・BP モーダル文言・ローディング・タイトル・タイトルゲート・TextMeshProLocalize 等） | `t(` の呼び出しが 208 箇所・76 ファイル |
| スキット | C# が解決し、表示文字列を push する（ADR 0006 決定11） | `t()` を通さない（`SkitPresentation.tsx:112`） |

CSV（`Localization/localization.csv`、422キー、列は Source / english / japanese / german / korean）のプレースホルダは2種類ある。`{p0}`〜`{p2}` は C# の書式。名前付きは Web の書式で、`{count}` `{requiredCount}` `{ownedCount}` `{itemName}` `{current}` `{seconds}` `{required}` `{rate}` `{name}` `{value}` `{power}` `{max}` `{index}` `{generated}` `{x,y,z}` 等がある（`{count}` は5列合計で65出現）。

### 4.2 C# へ戻す必要と範囲

| 項目 | 必要性 | 範囲 |
|---|---|---|
| キー解決 | 既にある。`Localize.Get` / `GetContent` をそのまま使う | 追加は無い |
| **名前付き補間** | 必要。Web 書式のテンプレートを C# で埋められない | `Localize.GetFormatted(key, IReadOnlyDictionary<string,string>)` 相当を足すか、CSV を位置引数へ一括で書き換えるか（AGENTS.md「変更の波及を恐れない」に従えば後者もあり得る。**裁定事項**） |
| 言語切替時の再バインド | 必要。今の C# 呼び出し元は「呼んだ時点の文字列」を保持する作りが多い（例 `BlockPlacementTarget.DisplayName` はプロパティなので読むたびに再解決） | UITK の Label 用に「キー + 引数 → OnLanguageChanged で再解決」する小さな部品を作る（`TextMeshProLocalize` の UITK 版）。DTO は Guid とキーだけを運び、ビューが解決するという ADR 0006 の原則は維持する |
| **数値書式** | 必要。Web の `toFixed`（12箇所）は常に `.` を使う。C# の `ToString()` は CurrentCulture に依存し、ドイツ語 OS では `,` になる。グローバルなカルチャ設定は無い（grep で `DefaultThreadCurrentCulture` が0件） | 表示用の数値書式ヘルパーを作り、InvariantCulture を明示する。de 列があるので実害が出る |
| 辞書が無い・取得失敗の状態 | 不要（同期初期化） | `preDictionaryText`、`DictionaryIndependentText`（出展モードの言語ゲート文言を含む）は、辞書を通さない固定文言として C# に移す |
| 静的検査 | Web には `noJsxVisibleLiteral.test.ts`（JSX の生文字を禁止）と `allScreensI18n.test.ts` がある | UXML の `text=` 属性の生文字を禁止する検査に置き換える（推測: UXML はテキストなので grep 型のテストで足りる） |
| 撤去するもの | — | `LocalizationDictionaryEndpoint` / `LocalizationLanguagesEndpoint` / `LocalizationTopic` / `localization.setLocale` action、TS のキー生成（`npm run gen:i18n`）、`I18nProvider` |
| ADR 0006 | 決定5は「Web 側解決」の明記で、帰結にも Web が出てくる | 「ビュー側（UITK の presenter）で解決し、言語切替は再バインドで済ませ、再 push はしない」へ改訂する ADR が要る |

### 4.3 フォント

| 論点 | 事実 | 対応 |
|---|---|---|
| アセットの種類 | 既存の `Dependencies/Font/NotoSansJP-Medium SDF 1/2.asset` 等は **TMP_FontAsset**（スクリプト guid `71c1514a…`、AtlasPopulationMode 0）で、uGUI/TMP 用。UITK の TextCore `FontAsset` とは別の型である | UITK 用の TextCore FontAsset を Editor で作ってコミットする（PoC は実行時に `FontAsset.CreateFontAsset` で SDF を作った。`FromFont` はビットマップで、拡大すると崩れる） |
| 字形の構成 | Web は英数を NotoSans-Regular（400）、CJK ロケールでは NotoSansJP-Medium（500）を優先する（`web app/index.css:8-34`、`data-locale` で切替）。合成太字は禁止 | 言語ごとに root の `unityFontDefinition` を切り替えるか、フォールバック表で順序を作る |
| 韓国語 | Web では korean も「MooresUI JP」優先だが、NotoSansJP にハングルは無い。CEF ではシステムフォントへ落ちて表示されている（推測） | UITK はシステムフォントへ自動では落ちない（推測）。repo にある `NotoSansKR-Medium.otf` をフォールバックへ明示的に入れる |
| 小さい文字 | 11〜12px は Chrome より細く甘い（PoC 実測） | 最小サイズのトークンを見直す |
| 文中アイコン | 実行時の SpriteAsset が内部 API 依存（PoC） | 構築と描画のテストを置く（PoC の結論のとおり） |

---

## 5. Web 以外の UI 系統の扱い

### 5.1 棚卸し

| 系統 | 技術 | 実体 | 現在の裁定 |
|---|---|---|---|
| CEF の描画面 | uGUI | `MainGameUI.prefab` の Canvas と RawImage | ADR 0052 例外1。**CEF の撤去で消える** |
| タイトル（MainMenu シーン） | uGUI + TMP | `Client.MainMenu`（430行: ConnectServer / StartLocal / LanguageSetting / ResetAllData / 参加同意 / 前回の異常終了）。`StandaloneInputModule` | ADR 0052 例外2（`.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md`）。「作り変え」は別タスク（`2026-09-13-配布ビルドのメインメニューは作り変え…md`）。同意ゲートは uGUI で追加済み（`2026-09-20-参加同意…md`） |
| ローディング | uGUI + TMP | `Client.Starter/InitializeScenePipeline.cs:37` の `TMP_Text loadingLog`、`Initialization/Progress/LoadingProgressLog.cs`、`TextMeshProLocalize` | 同上 |
| mapObject の HP バー | uGUI（ワールド空間 Canvas）+ TMP | `Client.Game/InGame/Map/MapObject/MapObjectHpBarView.cs`（Slider + TMP_Text） | ADR 0052 例外3 |
| デバッグ UI | uGUI 製の外部パッケージ | UnityDebugSheet（`com.harumak.unitydebugsheet`）、Graphy、IngameDebugConsole、Runtime Hierarchy Inspector、自前の `TrainUnitDebugOverlayPresenter` | ADR 0052 例外4 |
| ポインタ判定の uGUI 分 | EventSystems | `UiPointerHitTest.cs:17` | ADR 0052 の agent 前提1（デバッグ UI があるので残す） |
| スキットの UITK 旧経路 | UITK | `Client.Skit/UI/SkitUI.cs`、`SkitUITools.cs`、`Asset/UI/NewUI/Skit/SkitUI.uxml/.uss/SkitPanelSettings.asset`、`Asset/Skit/SkitUI.prefab`。`SkitPresentationMode(true)` 固定で、非 Web の分岐は死んでいる | bd `moorestech-lnsf.8`（撤去には「会話 UI のフォールバックを永久に持たない」確認の裁定が要る） |
| Editor の拡張 | UITK / IMGUI | ツールバーの要素、`Client.Playtest/Overlay/PlaytestOverlayView.cs`（IMGUI・Editor 専用） | 移行対象外 |
| 使われていないパッケージ | — | `com.singtaa.onejs`（OneJS 不採用の裁定）、`com.annulusgames.debug-ui`（コード参照0件） | 掃除の候補 |

### 5.2 選択肢

**CEF 撤去後の uGUI パッケージ（`com.unity.ugui`）**

| 案 | 帰結 |
|---|---|
| A. 残す（例外2〜4は現状維持） | 追加の作業は無い。UI は UITK と uGUI の2系統のまま。TMP も残る。2026-09-05 のユーザー意図「uGUI 関連をすべて消したい」は満たされない |
| B. 外す | 例外2〜4をすべて置き換える必要がある。Unity 6 では TMP が ugui に同梱されているため（ADR 0052 の記述）、HP バーをワールド空間の TextMeshPro（非 UI）にする逃げ道も無くなる。デバッグ系の外部パッケージ3つ（Graphy・IngameDebugConsole・UnityDebugSheet）を代替するか捨てる必要がある |
| C. 製品の画面だけ UITK へ寄せ、デバッグだけ uGUI を残す | ugui パッケージは残る。製品経路から uGUI は消える。「UI を壊す環境差」の議論（裁定の理由）に照らすと、配布物の UI が全部 UITK になれば目的は達する |

**系統ごとの選択肢**

| 系統 | 選択肢 | 判断材料 |
|---|---|---|
| タイトル・ローディング | ① uGUI 現状維持 ② UITK 化（タイトルの「作り変え」タスクと合流させる） | 例外にした理由は「CEF はゲーム内でしか起動できない」（ADR 0052 例外2の原文）だったが、UITK は初フレームから描けるのでこの理由は消える。同意ゲートを uGUI で作ったばかりという作業損はある。**裁定事項** |
| HP バー | ① uGUI 現状維持 ② UITK のワールド空間パネル（推測: Unity 6.2 以降の機能。6000.3.8f1 で使えるか要実測） ③ 画面投影オーバーレイ（ワールドピン型。2026-09-05 に棄却済み） | ①が最小。B 案を取るなら②が要る |
| デバッグ UI | ① uGUI 現状維持 ② UITK で自作し、外部パッケージを捨てる | 配布ビルドに入るかは要確認（`DebugObjectsBootstrap`）。開発者専用なら①で十分 |
| 旧 SkitUI（UITK） | ① 撤去し、`SkitPresentationStateStore` を購読する新しい UITK ビューを作る ② 旧 SkitUI.uxml を土台に拡張する | 旧 UXML（26行）は今の Web 版（ツールバー・選択肢・タイプライタ・背景モード・ポーズ中の入力停止）より古い。C# の Store とインテント待ち（`SkitPresentationStateStore` / `SkitIntentWaitController`）はそのまま使える。①を推す。lnsf.8 の「フォールバックを持たない確認」は、UITK が唯一の正規経路になることで実質的に解消する |

### 5.3 推奨（裁定の材料として）

- 製品の画面は UITK に一本化する。HP バーとデバッグは現状維持。タイトルとローディングは「メインメニュー作り変え」タスクで UITK 化する方向にする（案 C）。これで配布物の画面 UI は UITK と HP バーだけになる。ugui パッケージを外すかどうかは CEF の撤去完了後に改めて裁定する。
- `UiPointerHitTest` の EventSystem 判定は、デバッグの uGUI が残る限り残す。

---

## 6. 計画への示唆

1. **第一弾の前に共通基盤を作る**（PoC の結論に入力系を足したもの）
   - UI 用アクション資産（WASD・矢印の Navigate を外す）、非入力欄を `focusable=false` にする規約
   - 1パネル + 層コンテナ + `PickingMode.Ignore` の規約、`UiPointerHitTest` の Pick 化（共存期間は論理和）
   - テキストフォーカスの追跡と ESC ラッチ、IME の手動確認
   - 画面ルータ（`SCREEN_TRAITS` の C# 版。`UIStateControl.OnStateChanged` の UniRx 化を含む）
   - 名前付き補間、数値書式、TextCore FontAsset とフォールバック
   - カーソル追従層（ツールチップと掴み）
2. **第一弾の画面は D&D・掴みの組単位で選ぶ**（§3.7）。単体で切り出せるのは、ポーズメニュー（入力欄が1つ、IME と ESC の1段戻りを検証できる）、チャレンジ一覧、研究ツリーなどの「掴みを持たない画面」である。インベントリを選ぶなら、ブロックインベントリ・装備 HUD・GrabOverlay・ツールチップまで同時に移すことになる。
3. **完了の定義に入れる入力系の撤去物**: `WebUiInputExclusivity` の WS 経路、`HybridInput` の legacy フォールバック（CEF が消えれば `activeInputHandler` を InputSystem のみへ寄せられる可能性がある。ただし IME の実装次第。推測）、`CefInputForwarder` / `CefScreenMapper` / `PlaytestDomQuery`、Web の i18n 一式、ADR 0006 / 0013 / 0017 / 0019 / 0020 / 0021 の改訂または廃止の記録。
4. **Kestrel 撤去の巻き添え**: RemoteExec（Editor の外からコードを実行する開発ツール）は Kestrel のエンドポイントに相乗りしている（`Client.WebUiHost/Boot/WebUiEndpoints.cs:97-101`、`Client.RemoteExec/RemoteExecActivation.cs`）。Kestrel を消すなら RemoteExec の受け口を別に用意する必要がある。

---

## 7. 未確認・実測が要る点

| 項目 | 確かめ方 |
|---|---|
| EventSystem があるときの UITK 入力経路と、`IsPointerOverGameObject` が UITK を拾うか | PlayMode で UITK ボタン上の判定値をログに出す |
| WASD の Navigate がフォーカス中の UITK 要素を動かすか、Space が Submit になるか、Tab でフォーカスが動くか | ボタンをクリックした後に WASD・Space・Tab を注入する |
| TextField 上で ESC を押したとき、EventSystem と UIStateControl のどちらが先に処理するか | 同じフレームのログ順を見る |
| 日本語 IME（変換・確定・取消）。Windows と macOS | 手動（自動操作では確かめられない。PoC も未検証） |
| Recorder（GameViewInputSettings）とバグ報告の動画に UITK が写るか | 録画テスト1本 |
| uGUI の Overlay Canvas（CEF）と UITK パネルの重なり順 | 共存時の sortingOrder を変えて目視 |
| UITK のワールド空間パネルが 6000.3.8f1 で使えるか | HP バーの試作（HP バーを置き換える場合だけ） |
| cef-unity に領域単位で入力を止める手段が無いか | 版ピン（012281d）のソースを確認 |
| 韓国語の表示が CEF でシステムフォントに落ちているか | korean で Web を表示して確認 |

## 8. 主な根拠ファイル

- 状態機械: `C# Client.Game/InGame/UI/UIState/UIStateControl.cs`, `UIStateEnum.cs`, `State/IUIState.cs`, `State/GameScreenState.cs`, `State/PlayerInventoryState.cs`, `State/BuildMenuState.cs`, `State/SubInventoryState.cs`, `State/PauseMenuState.cs`, `State/PauseMenu/PauseMenuStateService.cs`, `State/SkitState.cs`, `State/CameraPolicy/UiStateCameraPolicyService.cs`, `State/CancelInput/RightShortPressInputService.cs`, `State/Hotbar/HotbarTapInputService.cs`, `UIRoot.cs`
- 入力基盤: `C# Client.Input/InputManager.cs`, `HybridInput.cs`, `WebUiInputExclusivity.cs`, `Client.Game/InGame/Control/UiPointerHitTest.cs`, `Client.Game/InGame/Player/TextInputMovementLockApplier.cs`, `Assets/Asset/Common/moorestechInputSettings.inputactions`, `Assets/Scenes/Game/MainGame.unity:8432-8449`, `ProjectSettings/ProjectSettings.asset:1025`
- Web 橋渡し: `C# Client.WebUiHost/Boot/WebSocketMessageDispatcher.cs`, `WsClientMessage.cs`, `Game/Actions/UiStateActions.cs`, `Game/Topics/UiStateTopic.cs`, `Game/WebUiModalService.cs`, `Game/BlueprintNameInputWebBridge.cs`, `Cef/WebUiCefNavigator.cs`; `web shared/uiState/*`, `app/App.tsx`, `features/inventory/EquipmentPanel/index.tsx`, `features/blockInventory/BlockInventoryKeyHandler.tsx`, `features/skit/SkitPresentation.tsx`, `features/hotbar/hotbarDnd.ts`
- CEF: `CefUnityBrowserSample.cs`（パッケージキャッシュ）、`docs/webui/design/input-focus-exclusivity.md`, `docs/webui/TODO.md`
- ローカライズ: `C# Client.Localization/Localize.cs`, `LocalizationTextResolver.cs`, `LocalizationTextInterpolator.cs`, `TextMeshProLocalize.cs`; `web shared/i18n/i18nStore.ts`, `dictionaryIndependentText.ts`; `Localization/localization.csv`, `content_keys.csv`; `docs/adr/0005-*`, `0006-*`
- フォント: `Assets/Dependencies/Font/*`, `web app/index.css`
- 非 Web UI: `docs/adr/0052-ugui-removal-scope-and-exceptions.md`, `.decisions/2026-09-05-*`（uGUI 撤去の一連）, `Client.MainMenu/*`, `Client.Starter/InitializeScenePipeline.cs`, `Client.Game/InGame/Map/MapObject/MapObjectHpBarView.cs`, `Client.DebugSystem/DebugSheet/DebugSheetController.cs`, `Client.Skit/UI/*`, `Assets/Asset/UI/NewUI/Skit/*`, bd `moorestech-lnsf.8`
- プレイテスト: `C# Client.Playtest/WebUi/*`, `Operations/PlaytestWebUiOps.cs`, `Operations/Ui/PlaytestUiOps.cs`, `Recording/PlaytestRecorder.cs`
- PoC: `docs/uitk-migration/poc/hard-cases/index.html`、ブランチ `poc/uitk-hard-cases` の `moorestech_client/Assets/Scripts/Client.UitkPoc/UitkPocBootstrap.cs`
