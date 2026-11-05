# UITK 段階1 骨組み（integrator）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** UI Toolkit の全画面が乗る骨組み（UI ルート・層・画面ルータ・所有表・Web の全面オフ・Editor 限定の上書き・`UiTestHost`・トークン USS 初版）を、配布画面の見た目を変えずに（所有者は全部 Web のまま＝暗転）`feature/uitk-stage1` に入れる。

**Architecture:** 画面状態の判断（UIState→画面、画面×面→所有者、Web を止めるか）は純ロジックとして `Client.Ui.State/Routing` に置き、Web ホスト（`UiStateTopic`）と描画側（`Client.Ui`）の両方がそれを読む。描画側は新設アセンブリ `Client.Ui` で、`GameUiRoot`（UIDocument 1 枚）が層 enum の順に層コンテナを並べ、`WebUiSuspension` が所有表の判断に従って CEF の RawImage を隠し Unity 側の Web 入力値を無視する。Web は `ui_state.current` の `webUiSuspended` を見て全部を unmount する。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（UIDocument・PanelSettings・USS）、UI Test Framework 1.0.0（`UITestFixture`）、VContainer、UniRx、NUnit、React+zod（Web 側の契約 1 フィールド）、Node（トークン変換スクリプト）

## Requirements

設計の正本は `docs/uitk-migration/plan.md` §2.2・§3.1〜3.5・§3.12・§3.13・§4.3 と `.decisions/` の UITK 裁定。grill は不要（段階計画とユーザー裁定で設計済み）。

- R-1 `Client.Ui` asmdef を新設する（参照の向き `Client.Game` ← `Client.Ui.State` ← {`Client.Ui`・`Client.WebUiHost`}。`Client.Ui` は `Client.Ui.State`・`Client.Game`・`Client.Input`・`UniRx`・`VContainer`・`Unity.ugui`（RawImage）を参照）。受け入れ: compile が通り、`Client.Game` と `Client.Ui.State` が `Client.Ui` を参照していない
- R-2 層 enum `UiLayer` を固定順で定義し、`GameUiRoot` がルート直下に層ごとの兄弟コンテナを enum 順に並べる。コンテナは全面・`PickingMode.Ignore`。受け入れ: L1 テストで順序・Ignore・空の層の上でワールドへクリックが抜けること（Pick が null）を固定。L0 テストで enum の順が Web の `--z-*` の重なり順と一致
- R-3 `GameUiRoot`（UIDocument 1 枚）を `MainGameUI.prefab` に事前配置し、Awake で `_instance` を設定する。PanelSettings は Scale With Screen Size・基準 1280×720・Expand（`screenMatchMode = Expand`）・scale 1・sortingOrder 1（CEF より前。S1）。prefab・PanelSettings は `uloop execute-dynamic-code` で作る。受け入れ: EditModeInPlayingTest で PlayMode 起動後に `GameUiRoot.Instance` が層コンテナを全部持つ
- R-4 画面ルータ: Web の `uiScreenRouting.ts`（`UiScreen`・`SCREEN_TRAITS`・`screenForUiState`）の C# 版を `Client.Ui.State/Routing` に置く。受け入れ: L0 テストで全 `UIStateEnum`×`NestedPauseSubStateEnum` の写像と全画面の traits が Web の表と一致（テスト名は Web の挙動文を写す）
- R-5 所有表（R2）: (画面, 面) → 所有者（`Web` か `GameUi`）の静的なコード表。組の欠け・重複は起動時（DI 構築時）に例外で止める。初版は全部 Web。受け入れ: L0 テストで全組がちょうど 1 つの所有者を持ち、全部 Web
- R-6 Web の全面オフ（R1）: 現在の画面の主パネル（`UiSurface.MainPanel`）の所有者が `GameUi` のあいだ、(a) CEF の RawImage を非表示、(b) `WebUiInputExclusivity` が Web の pointerOverUi・textInputFocused を読まない（戻るときに古い値を捨てる）、(c) `ui_state.current` に `webUiSuspended: true` を載せ、Web の App が全部を unmount する。受け入れ: L0（入力値の無視と破棄）、C# の topic テスト（`webUiSuspended` が載る）、vitest（suspended で何も描かない）、EditModeInPlayingTest（上書きでポーズを GameUi にすると RawImage が隠れる）
- R-7 Editor 限定の上書き（R4）: `#if UNITY_EDITOR` の中でファイル末尾に置いた API で、(画面, 面) の所有者を上書きできる。配布物には入らない。受け入れ: L0 テストで上書き→解除、プレイヤービルドの定義で API が存在しない（`#if` の外から参照していない）
- R-8 `UiTestHost`: 本番の PanelSettings 相当（1280×720・本番テーマ `UnityDefaultRuntimeTheme.tss`・トークン USS）を当てた L1 テストの土台。受け入れ: 最初の L1 テスト（層スタック）がこれで通る
- R-9 `Tokens.uss` 初版: `tokens.css`・`skitTokens.css`・`slotTokens.css` を変換スクリプトで機械的に USS へ移す（rem→px、定数だけの `calc()` は計算済みリテラル＋導出式コメント、`rgb(from …)`・`color-mix` は事前計算）。ビューポート依存などで移せないトークンは除外表に理由付きで書く。受け入れ: L2 テストで「Web のトークン名の集合 = USS の名前 ∪ 除外表」、USS に `calc(`・`rem`・`color-mix`・`rgb(from` が無い
- R-10 拡張点の明記: 層 enum・所有表の行・面 enum・Tokens・composition root の登録口の各ファイル冒頭に「後続レーンが足す場所」をコメントで書く
- R-11 配布画面の見た目は変えない（所有表は全部 Web。UITK の層は空）
- やらないこと: テーマの実機比較 S7・`Theme.uss`・フォント S10・UiMotion・入力（`UiPointerHitTest` の Pick 論理和・ESC ラッチ・アクション資産・フォーカス）・DSL の UITK ロケータ・L2 規約テスト一式・`AnchorIds`／登録簿・ポーズ／設定／バグ報告／トースト・CEF 停止の記録・`UiAssetCatalog`・moores-code-review・pr-create

## Global Constraints

- AGENTS.md: 1 ファイル 200 行以下・1 ディレクトリ 10 ファイル以下、partial 禁止、`Func<>` 禁止、try-catch 原則禁止、イベントは UniRx、日英 2 行コメント（各 1 行）、`[SerializeField]` は `_` 無し小文字キャメル、単純 getter/setter 禁止（`{ get; private set; }` は可）、デフォルト引数禁止、初期化は `Initialize`、エディタ専用コードは `#if UNITY_EDITOR` でファイル末尾、`.meta` を手で作らない、prefab・シーン・アセットは `uloop execute-dynamic-code` 経由でだけ変える
- 名前に技術名（Uitk）を入れない（`.decisions/2026-10-29-UIの画面状態はClient.Gameに置かず専用アセンブリClient.Ui.Stateに置く.md`・plan §3.1）。所有者の値は `Web` と `GameUi`
- fail-closed で拒否・無視する経路は理由をログへ出す。必須のアセット参照が無ければ Awake で例外（起動の段階で止める）
- テストは `--filter-type regex` か `class` で絞る。`Client.Tests.Playtest` 系を巻き込む広い正規表現は禁止
- `.cs` を変えたら `uloop compile --project-path ./moorestech_client`
- Web は `cd moorestech_web/webui && npx vitest run <path>` と `npx tsc -b`（または `npm run typecheck` が有れば）で確かめる
- Web パスを触るコミットは `docs/uitk/TODO.md` の「## 追従記録」に `写した` で記帳する（G1。束 1 は暗転中）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 本体の inline 検査（下の判断記録 D-9）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱1・第3バケツ0 / bool の webUiSuspended は 2 状態で閉じる。弱=除外表の理由が自由文（D-10）

---

## File Structure

| ファイル | 責務 |
|---|---|
| `moorestech_client/Assets/Scripts/Client.Ui.State/Routing/UiScreen.cs` | Web の `UiScreen` の C# 版 enum |
| `.../Client.Ui.State/Routing/UiScreenTraits.cs` | `SCREEN_TRAITS` の C# 版（不変 sealed class と静的表） |
| `.../Client.Ui.State/Routing/UiScreenRouting.cs` | `UIStateEnum`＋入れ子サブ状態 → `UiScreen` |
| `.../Client.Ui.State/Routing/UiSurface.cs` | 面 enum（初版 `MainPanel` のみ）と所有者 enum `UiSurfaceOwner` |
| `.../Client.Ui.State/Routing/UiOwnershipTable.cs` | 所有表（静的な行・検証・照会。Editor 上書きは末尾 `#if`） |
| `.../Client.Ui.State/Routing/UiRoutingState.cs` | 現在の画面と `WebUiSuspended` を ReactiveProperty で持つ DI サービス |
| `moorestech_client/Assets/Scripts/Client.Ui/Client.Ui.asmdef` | 描画アセンブリ |
| `.../Client.Ui/Foundation/Layers/UiLayer.cs` | 層 enum（固定順） |
| `.../Client.Ui/Foundation/Layers/UiLayerStack.cs` | ルートに層コンテナを並べ、`GetLayer` で返す（MonoBehaviour 非依存） |
| `.../Client.Ui/Foundation/Root/GameUiRoot.cs` | UIDocument を持つシングルトン。トークン USS を当て、層スタックを組む |
| `.../Client.Ui/Foundation/Routing/WebUiSuspension.cs` | `UiRoutingState` を購読し、RawImage と入力排他を切り替える |
| `.../Client.Ui/Foundation/Theme/Tokens.uss`・`SkitTokens.uss`・`SlotTokens.uss`・`TokenConversionExclusions.txt` | トークン初版と除外表 |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset` | PanelSettings（uloop で作る） |
| `tools/uitk/convert-web-tokens.mjs` | CSS→USS 変換スクリプト |
| `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs` | `Client.Ui` の composition root（登録口） |
| `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs` | `[SerializeField] gameUiRoot` と登録呼び出し |
| `moorestech_client/Assets/Scripts/Client.Input/WebUiInputExclusivity.cs` | `SetWebUiSuspended(bool)` を足す |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/UiStateTopic.cs`・`WebUiGameBinder.cs` | `WebUiSuspended` を載せる |
| `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`・`src/app/App.tsx`・mock-host の ui_state 送出 | `webUiSuspended` の受信と全 unmount |
| `moorestech_client/Assets/Scripts/Client.Tests/Ui/...` | `UiTestHost`・L0・L1・L2・EditModeInPlayingTest |

---

### Task 1: 画面ルータと所有表（`Client.Ui.State/Routing`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Routing/UiScreen.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Routing/UiScreenTraits.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Routing/UiScreenRouting.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Routing/UiSurface.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Routing/UiOwnershipTable.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui.State/Routing/UiRoutingState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiStateRegistration.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Routing/UiScreenRoutingTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Routing/UiOwnershipTableTest.cs`

**Interfaces:**
- Produces:
  - `enum UiScreen { None, PlayerInventory, SubInventory, ResearchTree, BuildMenu, ChallengeList, PauseMenu, TrainHud, TrainPause, SkitPause }`
  - `sealed class UiScreenTraits { bool Grab; bool PauseMenu; bool SkitInput; bool Backdrop; bool AlwaysOnHud; bool TrainHud }`（`{ get; }` の読み取り専用＋ctor）と `static class UiScreenTraitsTable { static UiScreenTraits Get(UiScreen screen) }`
  - `static class UiScreenRouting { static UiScreen Resolve(UIStateEnum state, NestedPauseSubStateEnum? subState) }`
  - `enum UiSurface { MainPanel }`、`enum UiSurfaceOwner { Web, GameUi }`
  - `class UiOwnershipTable { UiOwnershipTable(); UiSurfaceOwner GetOwner(UiScreen screen, UiSurface surface); }`（ctor で全組の欠け・重複を検証し、欠ければ `InvalidOperationException`）。Editor 限定: `#if UNITY_EDITOR` 内の `static class UiOwnershipEditorOverride { static void Set(UiScreen, UiSurface, UiSurfaceOwner); static void Clear(); }`
  - `class UiRoutingState : IInitializable { IReadOnlyReactiveProperty<UiRoutingSnapshot> Current; UiRoutingSnapshot Resolve(); }`、`sealed class UiRoutingSnapshot { UiScreen Screen; bool WebUiSuspended; }`

- [ ] **Step 1: enum と traits 表を書く**

`UiScreenTraitsTable` は Web の `SCREEN_TRAITS` を行ごとに写す（`skitPause` だけ `SkitInput=false`、`researchTree` だけ `AlwaysOnHud=false`、`trainHud`/`trainPause` だけ `TrainHud=true`、`none`/`trainHud` は `Backdrop=false`、`playerInventory`/`subInventory`/`researchTree` が `Grab=true`、`pauseMenu`/`trainPause`/`skitPause` が `PauseMenu=true`）。辞書の初期化子で全 `UiScreen` を列挙し、欠けたら静的初期化で例外。

- [ ] **Step 2: ルータを書く**

```csharp
public static UiScreen Resolve(UIStateEnum state, NestedPauseSubStateEnum? subState)
{
    // Web の screenForUiState と同じ写像。未対応 state はパネル無し
    // Mirrors the web screenForUiState; unsupported states are panel-less
    var nestedPause = subState == NestedPauseSubStateEnum.PauseMenuScreen;
    switch (state)
    {
        case UIStateEnum.PlayerInventory: return UiScreen.PlayerInventory;
        case UIStateEnum.SubInventory: return UiScreen.SubInventory;
        case UIStateEnum.ResearchTree: return UiScreen.ResearchTree;
        case UIStateEnum.BuildMenu: return UiScreen.BuildMenu;
        case UIStateEnum.ChallengeList: return UiScreen.ChallengeList;
        case UIStateEnum.PauseMenu: return UiScreen.PauseMenu;
        case UIStateEnum.TrainHUDScreen: return nestedPause ? UiScreen.TrainPause : UiScreen.TrainHud;
        case UIStateEnum.Story: return nestedPause ? UiScreen.SkitPause : UiScreen.None;
        default: return UiScreen.None;
    }
}
```

- [ ] **Step 3: 所有表を書く**

```csharp
// 所有表の行。後続レーンは面を足したらここへ全画面ぶんの行を足し、切替 PR で Web→GameUi に書き換える
// Ownership rows; later lanes add a row per screen for each new surface and flip Web→GameUi in the cutover PR
private static readonly (UiScreen screen, UiSurface surface, UiSurfaceOwner owner)[] Rows =
{
    (UiScreen.None, UiSurface.MainPanel, UiSurfaceOwner.Web),
    // …全 UiScreen について MainPanel=Web を 1 行ずつ（10 行）
};

public UiOwnershipTable()
{
    // 全組がちょうど 1 行。欠け・重複は起動時に止める
    // Exactly one row per pair; missing or duplicated pairs stop at startup
    foreach (var row in Rows)
    {
        if (!_owners.TryAdd((row.screen, row.surface), row.owner)) throw new InvalidOperationException($"[UiOwnershipTable] duplicated row {row.screen}/{row.surface}");
    }
    foreach (UiScreen screen in Enum.GetValues(typeof(UiScreen)))
    foreach (UiSurface surface in Enum.GetValues(typeof(UiSurface)))
    {
        if (!_owners.ContainsKey((screen, surface))) throw new InvalidOperationException($"[UiOwnershipTable] missing row {screen}/{surface}");
    }
}

public UiSurfaceOwner GetOwner(UiScreen screen, UiSurface surface)
{
    var owner = _owners[(screen, surface)];
#if UNITY_EDITOR
    if (UiOwnershipEditorOverride.TryGet(screen, surface, out var overridden)) owner = overridden;
#endif
    return owner;
}
```

ファイル末尾に `#if UNITY_EDITOR` で `UiOwnershipEditorOverride`（静的辞書・`Set`・`Clear`・`internal TryGet`。上書きを設定・解除したら `Debug.Log` で 1 行）。

- [ ] **Step 4: `UiRoutingState` を書く**

ctor で `UIStateControl`・`UiOwnershipTable`・`IReadOnlyList<INestedPauseScreenState>` を受け取り、`Initialize()` で `UIStateControl.OnStateChanged` と各入れ子画面の `OnPresentationChanged` を購読して `Current` を更新する（同値は ReactiveProperty が落とす。`UiRoutingSnapshot` は値の等価を `Equals` で実装）。`Resolve()` は現在の `UIStateControl.CurrentState` と `GetCurrentNestedPauseScreen()?.SubState` から `UiScreenRouting.Resolve` し、`WebUiSuspended = table.GetOwner(screen, UiSurface.MainPanel) == UiSurfaceOwner.GameUi`。Editor 上書きを変えたあと即時に反映できるよう `public void Refresh()` も持つ（`Current.Value = Resolve()`）。モーダル（段階 3）は「モーダルを GameUi が出している」を `WebUiSuspended` の論理和に足す拡張点としてコメントで書く。

- [ ] **Step 5: DI 登録**

`MainGameUiStateRegistration.Register` に追記:

```csharp
// 画面ルータと所有表。Web ホストと描画側の両方が読む
// Screen router and ownership table, read by both the web host and the renderer
builder.Register<UiOwnershipTable>(Lifetime.Singleton);
builder.RegisterEntryPoint<UiRoutingState>().AsSelf();
```

- [ ] **Step 6: L0 テストを書く**

`UiScreenRoutingTest`: Web の `uiScreenRouting.test.ts` のテスト名（日本語の挙動文）を写し、全 `UIStateEnum` を `Enum.GetValues` で回して期待表と照合する。traits の 10 行を照合する。`UiOwnershipTableTest`: 全組が Web、`GetOwner` が例外を出さない、Editor 上書きで `PauseMenu/MainPanel` が `GameUi` になり `Clear()` で戻る（`[TearDown]` で必ず `Clear()`）。

- [ ] **Step 7: compile とテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.Routing\."`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 8: コミット・push**

`git commit -m "feat(uitk): 画面ルータと所有表を Client.Ui.State に置く"` → `git push origin feature/uitk-stage1`

### Task 2: Web の全面オフ（R1）を契約と入力排他に通す

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Input/WebUiInputExclusivity.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/UiStateTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`
- Modify: `moorestech_web/webui/src/app/App.tsx`
- Modify: mock-host と既存テストの ui_state 送出（`grep -rn "keyHints" moorestech_web/webui/e2e moorestech_web/webui/src --include=*.ts*` で ui_state を組んでいる所を全部）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Routing/WebUiInputExclusivitySuspensionTest.cs`、既存 `Client.Tests/WebUi/UiStateSubStatePublishTest.cs` に 1 件、`moorestech_web/webui/src/bridge/contract/schemas/__tests__/ui.test.ts` に 1 件、App の suspended 描画のテスト（既存の App テストの流儀に合わせる）
- Modify: `docs/uitk/TODO.md`（追従記録）

**Interfaces:**
- Consumes: `UiRoutingState.Resolve()`（Task 1）
- Produces: `WebUiInputExclusivity.SetWebUiSuspended(bool suspended)`; `UiStateDto.WebUiSuspended`（JSON `webUiSuspended`）; zod `webUiSuspended: z.boolean()`（必須。欠損は契約破れ）

- [ ] **Step 1: 入力排他**

`WebUiInputExclusivity` に `private static volatile bool _webUiSuspended;` と `SetWebUiSuspended(bool)` を足す。suspended のあいだ `IsPointerOverWebUi` は false、`IsSuppressed`/`ProbeSuppressed` は Web の textInputFocused を見ない。`SetState` は suspended 中も受けるが値を捨てる（`_pointerOverUi=false`、TextInputFocused=false）。`SetWebUiSuspended(false)` でも両値を false にしてから戻す（古い値を捨てる。Web は次の WS 受信で最新値を送る）。切替時に `Debug.Log("[WebUiInput] web UI suspended=…")`。`TextInputFocused`（ReactiveProperty）の購読者にも false が届くこと。

- [ ] **Step 2: topic**

`UiStateDto` に `public bool WebUiSuspended;`、`UiStateTopic` の ctor に `UiRoutingState routingState` を足し `BuildJson` で `WebUiSuspended = _routingState.Resolve().WebUiSuspended`。`routingState.Current` も購読して `SchedulePublish()`（Editor 上書きの変化を配る）。`WebUiGameBinder` は `resolver.Resolve<UiRoutingState>()` を渡す。

- [ ] **Step 3: Web**

`UiStateDataSchema` に `webUiSuspended: z.boolean()`（コメント 2 行: UITK が主パネルを出す間 Web は全部 unmount する。R1）。`App.tsx` で `const webUiSuspended = useTopicSelector(Topics.uiState, (d) => d?.webUiSuspended === true);` を取り、`if (!uiVisible || cutScene || webUiSuspended) return <div className={styles.hidden} data-web-ui-transparent />;`。mock-host とテストの ui_state に `webUiSuspended: false` を足す。

- [ ] **Step 4: テスト**

C#: suspended 中に `SetState(true,true)` しても `IsPointerOverWebUi==false`・`IsTextInputFocused==false`、解除後も古い値が残らない（`[TearDown]` で `SetWebUiSuspended(false)`・`SetState(false,false)`）。topic: 既存 `UiStateSubStatePublishTest` の流儀で JSON に `"webUiSuspended":false` が載る。Web: schema が `webUiSuspended` 欠損を拒否、App が suspended で stage を描かない。

- [ ] **Step 5: 確認**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\.|UiStateSubStatePublishTest"` → `cd moorestech_web/webui && npx vitest run src/bridge/contract src/app src/shared/uiState && npx tsc -b --noEmit`（tsc の実コマンドは `package.json` の scripts に合わせる）
Expected: 全 PASS・型エラー 0

- [ ] **Step 6: 追従記録とコミット**

`docs/uitk/TODO.md` の「## 追従記録」に、このコミットを `| 1 | <hash10> | 写した | R1 の全面オフ（C# UiRoutingState.WebUiSuspended を Web が受けて unmount） |` で記帳する（コミット後に hash を書いて追いコミット）。`python3 .github/scripts/check-uitk-web-follow.py --base origin/master --head HEAD` が通ること。push。

### Task 3: `Client.Ui` アセンブリ・層・`GameUiRoot`・`WebUiSuspension`・composition root

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Client.Ui.asmdef`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Layers/UiLayer.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Layers/UiLayerStack.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Root/GameUiRoot.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Routing/WebUiSuspension.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameUiRegistration.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`、`Client.Starter/Client.Starter.asmdef`（`Client.Ui` 参照）、`Client.Tests/Tests.asmdef`（`Client.Ui` 参照）
- Create (uloop): `moorestech_client/Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset`、`MainGameUI.prefab` に子 `GameUiRoot`（UIDocument＋GameUiRoot）、MainGame シーンの `MainGameStarter.gameUiRoot` の参照

**Interfaces:**
- Consumes: `UiRoutingState.Current`（Task 1）、`WebUiInputExclusivity.SetWebUiSuspended`（Task 2）
- Produces: `enum UiLayer`、`sealed class UiLayerStack { static UiLayerStack Build(VisualElement root); VisualElement GetLayer(UiLayer layer); }`、`class GameUiRoot : MonoBehaviour { static GameUiRoot Instance; VisualElement GetLayer(UiLayer layer); void SetCefVisible(bool visible); }`（`[SerializeField] UIDocument uiDocument; StyleSheet[] tokenStyleSheets; RawImage cefRawImage;`）、`class WebUiSuspension : IInitializable, IDisposable`

- [ ] **Step 1: 層 enum**

```csharp
// 層の固定順（先頭が最背面）。順の正本は web/app/tokens.css の --z-*。後続レーンは層を足すときここと UiLayerOrderTest の対応表を同時に直す
// Fixed back-to-front order sourced from --z-* in web/app/tokens.css; later lanes add a layer here and in UiLayerOrderTest together
public enum UiLayer
{
    Backdrop,        // --z-viewport-backdrop
    BehindStage,     // --z-viewport-behind-stage（通知）
    Screen,          // --z-stage-screen
    Hud,             // --z-stage-overlay-panel / -chrome
    PauseMenu,       // --z-stage-pause-menu
    Tooltip,         // --z-stage-tooltip
    Grab,            // --z-viewport-grab
    WorldPin,        // --z-portal-world-pin
    Modal,           // --z-portal-modal
    Toast,           // --z-portal-toast
    Tutorial,        // --z-portal-tutorial
    Reconnect,       // --z-portal-reconnect
    FullScreenGate,  // --z-portal-full-screen-gate
}
```

- [ ] **Step 2: `UiLayerStack`**

`Build(root)` は `Enum.GetValues` の順に `new VisualElement { name = $"layer-{layer}", pickingMode = PickingMode.Ignore }` を作り、`style.position = Absolute`・上下左右 0 で `root.Add`。`root.pickingMode = PickingMode.Ignore` も設定。USS クラス `game-ui-layer` を付ける。

- [ ] **Step 3: `GameUiRoot`**

```csharp
public class GameUiRoot : MonoBehaviour
{
    private static GameUiRoot _instance;
    public static GameUiRoot Instance => _instance;

    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet[] tokenStyleSheets;
    [SerializeField] private RawImage cefRawImage;

    private UiLayerStack _layers;

    private void Awake()
    {
        _instance = this;
        // 必須参照が欠けたら起動の段階で止める
        // Stop at startup when a required reference is missing
        if (uiDocument == null || cefRawImage == null || tokenStyleSheets == null || tokenStyleSheets.Length == 0) throw new InvalidOperationException("[GameUiRoot] required references are not assigned on MainGameUI.prefab");
        var root = uiDocument.rootVisualElement;
        foreach (var sheet in tokenStyleSheets) root.styleSheets.Add(sheet);
        _layers = UiLayerStack.Build(root);
    }

    public VisualElement GetLayer(UiLayer layer) => _layers.GetLayer(layer);

    // R1 の全面オフで CEF の絵を隠す
    // Hide the CEF picture during the R1 full-off
    public void SetCefVisible(bool visible) => cefRawImage.enabled = visible;
}
```


- [ ] **Step 4: `WebUiSuspension`**

ctor で `UiRoutingState`・`GameUiRoot`。`Initialize()` で `routingState.Current.Select(s => s.WebUiSuspended).DistinctUntilChanged().Subscribe(Apply)`。`Apply(bool suspended)`: `gameUiRoot.SetCefVisible(!suspended)` と `WebUiInputExclusivity.SetWebUiSuspended(suspended)`。CEF への入力転送そのものは cef-unity に止める API が無い（`CefUnityBrowserSample.OnEarlyUpdateLast` は RawImage の有効・無効を見ない）ので、Web 側の全 unmount（Task 2）で受け手を消す、とコメントに書く（既知の穴として完了報告に載せる）。

- [ ] **Step 5: composition root**

```csharp
// Client.Ui の登録口。後続レーンは画面の Presenter・Mount をこの下に 1 行ずつ足す
// Registration entry for Client.Ui; later lanes append their screen presenters and mounts below
internal static class MainGameUiRegistration
{
    public static void Register(ContainerBuilder builder, GameUiRoot gameUiRoot)
    {
        builder.RegisterComponent(gameUiRoot);
        builder.RegisterEntryPoint<WebUiSuspension>();
    }
}
```

`MainGameStarter` に `[SerializeField] private GameUiRoot gameUiRoot;` と `MainGameUiRegistration.Register(builder, gameUiRoot);`（`MainGameUiStateRegistration.Register` の直後）。

- [ ] **Step 6: アセットと prefab（uloop）**

`uloop execute-dynamic-code` で: `PanelSettings` を `ScriptableObject.CreateInstance<PanelSettings>()` で作り `scaleMode=ScaleWithScreenSize`・`referenceResolution=(1280,720)`・`screenMatchMode=Expand`・`scale=1`・`sortingOrder=1`・`themeStyleSheet=Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss` で `AssetDatabase.CreateAsset`。`PrefabUtility.LoadPrefabContents("Assets/Asset/UI/Prefab/MainGameUI.prefab")` で子 `GameUiRoot` を作り `UIDocument`（panelSettings 設定）と `GameUiRoot` を付け、`SerializedObject` で `uiDocument`・`tokenStyleSheets`（Task 4 の 3 枚。Task 4 より前なら Task 4 の後でこの Step を行う）・`cefRawImage`（同 prefab の `CefUnity` 子の RawImage）を設定、`SaveAsPrefabAsset`。MainGame シーンを開き `MainGameStarter.gameUiRoot` にシーン内 MainGameUI インスタンスの `GameUiRoot` を設定して保存。

- [ ] **Step 7: compile とコミット**

Run: `uloop compile --project-path ./moorestech_client` → Expected: ErrorCount 0。push。

### Task 4: トークン USS 初版

**Files:**
- Create: `tools/uitk/convert-web-tokens.mjs`
- Create: `moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/Tokens.uss`・`SkitTokens.uss`・`SlotTokens.uss`・`TokenConversionExclusions.txt`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Theme/WebTokenParityTest.cs`

**Interfaces:**
- Produces: `:root` の USS 変数（名前は Web と同じ `--x`）。除外表は 1 行 `--name<TAB>理由`

- [ ] **Step 1: 変換スクリプト**

Node（依存なし）。入力 3 ファイルの `:root { … }` から `--name: value;` を順に読む（コメントは捨てる）。値の変換: `Nrem`→`N*16px`、`Nem` は除外（文脈依存）、`var(--x)` だけの値はそのまま、`calc(...)` は参照トークンを再帰的に数値へ解決して四則演算を評価し `Npx` へ（評価できなければ除外。導出式は `/* calc: … */` を同じ行に残す）、`rgb(from var(--c) r g b / A%)` と `color-mix(...)` は参照色を hex に解決して `rgba(r, g, b, a)` へ、ビューポート寸法（`--ui-stage-*`・`--ui-viewport-*` とそれに依存する式）は除外（理由: 実行時の寸法。USS の配置か C# の幾何で置き換える）。出力は 1 ファイル 200 行以下に収まるよう、各ファイルで `:root {` と 1 行 1 変数、先頭に生成元と再生成コマンドのコメント 2 行。`node tools/uitk/convert-web-tokens.mjs` で 4 ファイルを書き出す。

- [ ] **Step 2: 生成して目視**

Run: `node tools/uitk/convert-web-tokens.mjs && grep -cE "calc\(|rem|color-mix|rgb\(from" moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/*.uss`
Expected: 各 0。`wc -l` が各 200 以下

- [ ] **Step 3: L2 パリティテスト**

`WebTokenParityTest`: repo ルートは `Application.dataPath` から `../../` で辿る。3 つの CSS の変数名集合 = 3 つの USS の変数名集合 ∪ 除外表の名前（交わりなし）。USS に `calc(`・`rem`・`color-mix`・`rgb(from` が無い。

- [ ] **Step 4: Unity が USS を取り込めることを確かめる**

`uloop get-logs --project-path ./moorestech_client --log-type Error` で USS のインポートエラーが 0（変数の値が USS で解釈できない形なら変換規則を直す）。

- [ ] **Step 5: コミット**

push。

### Task 5: `UiTestHost` と最初の L1・L0

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/TestHost/UiTestHost.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Layers/UiLayerStackTest.cs`（L1）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Ui/Layers/UiLayerOrderTest.cs`（L0。tokens.css の `--z-*` との対応）

**Interfaces:**
- Consumes: `UiLayerStack.Build`、`GameUiPanelSettings.asset`、トークン USS（Task 3・4）
- Produces: `abstract class UiTestHost : UITestFixture`（`[SetUp]` で `panelSize=1280×720`・本番テーマ・トークン USS を `rootVisualElement` に当てる。後続レーンはこれを継承して View を組む）

- [ ] **Step 1: `UiTestHost`**

UI Test Framework の `UITestFixture`（`Unity.UI.TestFramework.Runtime`）の API をパッケージ（`/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/Resources/PackageManager/BuiltInPackages/com.unity.ui.test-framework/`）の Runtime と Documentation~ で確かめ、パネル寸法・テーマ・スタイルシートの当て方をそれに合わせる。テーマと USS は `AssetDatabase.LoadAssetAtPath` で読み、null なら `Assert.Fail`。

- [ ] **Step 2: L1**

`UiLayerStackTest : UiTestHost`: 層コンテナが enum 順に並び全部 `PickingMode.Ignore`、空の層の上の点で `panel.Pick` が null、`Modal` 層に `pickingMode=Position` の要素を置くとその要素が Pick される。

- [ ] **Step 3: L0**

`UiLayerOrderTest`: 各 `UiLayer` を (群, トークン名) に対応させ（`Screen/Hud/PauseMenu/Tooltip` は群 stage で、viewport の `--z-viewport-stage` の位置に入れ子、`WorldPin` 以降は portal 群で viewport 全体より前）、tokens.css の値で組んだ合成キーが enum 順に単調増加。全 `UiLayer` が対応表にあること。

- [ ] **Step 4: 実行**

Run: `uloop compile …` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.Ui\."`
Expected: 全 PASS。CI の EditMode client shard に `Client.Tests.Ui` が含まれることを `.github/workflows` で確かめ、含まれなければ完了報告に書く（S13 はテスト土台レーンの持ち物）。

- [ ] **Step 5: コミット**

push。

### Task 6: 実ゲームでの結合確認（EditModeInPlayingTest）

**Files:**
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Ui/GameUiSkeletonTest.cs`

- [ ] **Step 1: テスト**

editmode-in-playing-test スキルの雛形で PlayMode に入り、(1) `GameUiRoot.Instance` が全 `UiLayer` のコンテナを持つ、(2) 既定（全部 Web）で CEF の RawImage が有効、`UiRoutingState.Current.Value.WebUiSuspended == false`、(3) `UiOwnershipEditorOverride.Set(PauseMenu, MainPanel, GameUi)` → `UiRoutingState.Refresh()` → ポーズへ遷移（`UIStateControl.RequestTransition(UIStateEnum.PauseMenu)`）で RawImage が無効・`WebUiInputExclusivity.IsPointerOverWebUi == false`、閉じると RawImage が戻る。終わりに `Clear()`。

- [ ] **Step 2: 実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.EditModeInPlayingTest.Ui.GameUiSkeletonTest" --timeout-seconds 900`
Expected: PASS。ドメインリロード中のエラーは 45 秒待って再試行。Error ログ 0（`uloop get-logs --log-type Error`）

- [ ] **Step 3: コミット**

push。

### Task 7: 締め（このレーンの範囲）

- [ ] 全 `.cs` 変更後の最終 `uloop compile`、`Client\.Tests\.Ui\.|UiStateSubStatePublishTest|GameUiSkeletonTest` の再実行、vitest・tsc の再実行
- [ ] `docs/uitk/TODO.md` の追従記録が G1 スクリプトで通る
- [ ] sdd-archive（moores-subagent-driven-development の規定）を実行し、全作業をコミット・push
- [ ] moores-code-review と pr-create は**行わない**（下の D-1）

---

## 判断記録（ADR）

- D-1 このレーンは moores-code-review と pr-create を行わない。段階 0 から続く裁定「各レーンは統合ブランチへ入れ、PR は段階で 1 本」（`.decisions/2026-10-26-UITK段階0のPRは1本に統合する.md` の段階 1 への適用）と、指揮セッションの依頼文による。最終レビューと PR は指揮セッションが統合後に行う。出所: ユーザー（指揮セッション経由の依頼）
- D-2 所有表・ルータ・`UiRoutingState` は `Client.Ui.State` に置く。`UiStateTopic`（WebUiHost）が `webUiSuspended` を読むため、`Client.Ui` に置くと WebUiHost→Client.Ui の参照が要り、R1 で Client.Ui が WebUiHost 側を触る向きと循環する（plan §3.1 の理由と同じ）
- D-3 所有者の値は `Web`/`GameUi`。技術名を名前に入れない裁定（10-29）に合わせる
- D-4 面 enum は初版 `MainPanel` だけ。Web を止めるかの判断に要るのは主パネルだけで、トースト等の面は移す段で足す（YAGNI）。拡張点はコメントで明記
- D-5 Web の全面オフは、`ui_state.current` に `webUiSuspended` を足して App 全体を unmount する形にした（plan R2 の「`screenForUiState` が委譲画面で主パネルを描かない」より強い）。cef-unity に入力転送を止める公開 API が無く（`OnEarlyUpdateLast` は RawImage の有効を見ない）、RawImage を隠しても Web の HUD・document のキー処理へ入力が届くため、受け手ごと消す。新規パターンとして完了報告の要裁定に載せる
- D-6 層の順は plan §3.3 の大筋を tokens.css の `--z-*` の入れ子（viewport・stage・portal）で具体化した（Screen < Hud < PauseMenu < Tooltip < Grab < WorldPin < Modal …）。L0 テストで tokens.css と照合する
- D-7 `Tokens.uss` は 3 ファイルに分けた（各 200 行以下の規約。plan §3.5 の 1 ファイルとの差）。除外表は別ファイル
- D-8 PanelSettings の sortingOrder は 1（S1 の実測: 1 以上で CEF より前）
- D-9 配置検査（inline）: 新規型はすべて新設ディレクトリ内。前例: DI 登録は `MainGameUiStateRegistration`、シングルトンは AGENTS.md の Object シングルトン、topic の DTO 拡張は `UiStateTopic.SubState`、zod の必須化は `keyHints` の前例。違反なし
- D-10 Phase 2.6 弱: 除外表の理由は自由文（機械判定に使わないため可）
- D-11 ランタイム挙動に触れる（prefab に UIDocument、R1 の切替）ので EditModeInPlayingTest（Task 6）を入れる。録画プレイテストは見た目が不変（層は空）なので省く
- D-12 user-simulator と AskUserQuestion は行わない（依頼文「ユーザーへ質問しない。推奨案で進め、要裁定に書く」）
