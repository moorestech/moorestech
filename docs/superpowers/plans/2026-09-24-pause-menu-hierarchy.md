# ポーズメニュー階層化 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ポーズメニューを「トップ（セーブ／セーブして終了／設定→／バグ報告→）」と子画面「設定画面（言語選択）」「バグ報告画面（種別・説明文・送信）」の2階層にする。

**Architecture:** 今どの画面にいるか（`PauseMenuPage`）は Unity 側の `PauseMenuStateService` が `ReactiveProperty` で持つ。Escape キーの判定はすでにこのサービス1か所に集まっているので、「子画面なら1段戻る」もここで判定する。画面の値は既存の `pause_menu.current` トピックへ `page` として載せて Web へ配る。Web は `pause_menu.show_page` アクションで画面遷移を要求し、`page` に応じて同じパネルの中身を差し替える。バグ報告の書きかけは `PauseMenuPanel` が持ち、ポーズが閉じてパネルが外れると一緒に消える。

**Tech Stack:** Unity C#（UniRx・VContainer・NUnit）、moorestech_web/webui（React 18・Mantine・zod・vitest・Playwright）

## Requirements

設計ADR: `docs/adr/0069-pause-menu-hierarchy-top-settings-bug-report.md`。依頼原文「ポーズ画面 > 設定画面,バグ報告 みたいな感じで、今ポーズ画面に集約されてるものを全部階層化したい」。

1. R1 トップ構成: ポーズを開くと、タイトル・切断表示（切断時のみ）・「ゲームをセーブする」「セーブして終了」「設定」「バグ報告」の4ボタンが出る。言語選択とバグ報告フォームはトップに出ない。受入: vitest（PauseMenuPanel）と e2e（pauseMenu.spec）でトップに4ボタンがあり、`language-select`・`bug-report-description` が無い。
2. R2 設定画面: トップの「設定」で設定画面へ進み、言語選択（既存 `LanguageSelect`）と「戻る」ボタンが出る。中身は言語選択だけ（新しい設定項目は足さない）。受入: e2e で「設定」→ `language-select` が見え、言語を選ぶと `localization.setLocale` が送られる。
3. R3 バグ報告画面: トップの「バグ報告」でバグ報告画面へ進み、既存の報告フォーム（種別・説明文・送信・状態行）と「戻る」ボタンが出る。受入: e2e で「バグ報告」→ `bug-report-description` が見える。
4. R4 画面遷移の持ち主: 画面は C# の `PauseMenuStateService.CurrentPage` が持ち、`pause_menu.current` の `page`（`"top"|"settings"|"bugReport"`）で Web へ届く。Web のボタンは `pause_menu.show_page { page }` を送るだけで、Web 側で画面状態を持たない。受入: C# unit テストで `pause_menu.show_page` が `CurrentPage` を変え、不正値は `invalid_page` で拒否してログを出す。WireContract の fixture に `page` が載る。
5. R5 Escape: 子画面で Escape → トップへ戻る（ポーズは開いたまま）。トップで Escape → ポーズを閉じる。通常ポーズ・列車HUD・スキットの3か所で同じ挙動。受入: C# unit テスト（`HandleCloseKey` 相当の判定）で、子画面では false を返してトップへ移り、トップでは true を返す。
6. R6 戻るボタン: 子画面の「戻る」ボタンは `pause_menu.show_page { page: "top" }` を送る。受入: vitest。
7. R7 開くたびにトップ: ポーズを開くたびに（`OnEnter`）画面はトップになる。受入: C# unit テストで、子画面にいた状態から `OnEnter` するとトップになる。
8. R8 送信成功後: バグ報告の送信が成功したら、入力（説明文・種別）を空に戻し、画面はトップへ戻る。ポーズは閉じない。トーストは今どおり。送信が失敗したら画面も入力もそのまま。受入: EditModeInPlayingTest `BugReportBundleWriteTest` で送信後に `UIStateEnum.PauseMenu` のままかつ `CurrentPage == Top`。vitest で成功時に入力が空になる。
9. R9 書きかけ: 同じポーズの中でトップと子画面を行き来する間はバグ報告の書きかけ（説明文・種別）を残す。ポーズを閉じたら捨てる。受入: vitest（PauseMenuPanel）で、バグ報告画面で入力→`page` を top→bugReport に変えても入力が残り、パネルを unmount→再 mount すると空。
10. R10 記録確保のタイミングは不変: Escape 時点の記録確保（`OnPauseMenuOpened`）はポーズを開いた瞬間のまま。子画面の行き来では発火しない。受入: C# unit テストで `ShowPage` と `HandleCloseKey` の1段戻りでは `OnPauseMenuOpened` が発火しない。
11. R11 チュートリアルアンカー: `pause.menu`（パネル）・`pause.save`・`pause.back` はトップで今どおり付く。新たに `pause.settings`・`pause.bug-report`・`pause.back-to-top` を足す。受入: `anchorIds.test.ts` と `TutorialAnchorContractTest` が通る。
12. R12 文言: 「設定」「バグ報告」「戻る」を localization.csv に英・日・独で足す。

やらないこと:
- 新しい設定項目（音量・感度など）の追加
- 画面遷移のアニメーション・パンくず・タブ
- ポーズを閉じても書きかけを残す仕組み
- セーブ／セーブして終了の置き場所の変更（トップに残す）

## Global Constraints

- 1ファイル200行未満。1ディレクトリの新規ファイルは10個まで（超える場合はサブディレクトリ）。partial 禁止。`Func<>` 禁止。イベントは UniRx（C# の `event`/`Action` を使わない）。
- 単純な getter/setter プロパティ禁止。値の変更は `public void SetHoge` 形式、または `{ get; private set; }`。本planでは読み出しを `IReadOnlyReactiveProperty<PauseMenuPage> CurrentPage` で公開し、変更は `ShowPage(PauseMenuPage)` で行う。
- デフォルト引数禁止。引数を足したら呼び出し側をすべて直す。
- コメントは日本語1行＋英語1行の2行セットを主要処理ごとに。
- fail-closed で拒否する経路（不正な page）は必ず `Debug.LogWarning` で理由を出す。
- `.meta` は手で作らない（Unity が生成したものはコミットしてよい）。
- `.cs` を変えたら必ず `uloop compile --project-path ./moorestech_client` を通す。localization.csv を変えた後に触っていないキーで CS0117 が出たら、`uloop compile --project-path ./moorestech_client --force-recompile true --wait-for-domain-reload true` を1回挟む。
- webui の i18n キーは `pnpm gen:i18n`（`moorestech_web/webui` で実行）で `src/shared/i18n/generated/localizationKeys.ts` を再生成する。手で編集しない。
- 「unityプレイ録画テスト」の呼称を使う（「E2E」と呼ばない）。webui の Playwright は e2e のままでよい。
- 作業場所: worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/pause-menu-hierarchy`（branch `feature/pause-menu-hierarchy`）。Unity Editor は `uloop launch /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/pause-menu-hierarchy/moorestech_client` で起動する。

---

## File Structure

| ファイル | 変更 | 責務 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PauseMenu/PauseMenuPage.cs` | 新規 | 画面の列挙 `Top / Settings / BugReport` |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PauseMenu/PauseMenuStateService.cs` | 変更 | 今の画面を持つ。`ShowPage`・`HandleCloseKey`・`OnEnter` でトップへ |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PauseMenuState.cs` | 変更 | `IsClosePause()` → `HandleCloseKey()` |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/NestedPause/PauseMenuNestedSubState.cs` | 変更 | 同上 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/PauseMenuTopic.cs` | 変更 | DTO に `Page`。`CurrentPage` 変化で再配信。page の契約文字列変換 `PauseMenuPageContract` |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/PauseMenuActions.cs` | 変更 | `PauseMenuShowPageActionHandler`（`pause_menu.show_page`）を追加 |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/BugReportSubmitActionHandler.cs` | 変更 | 送信成功でポーズを閉じる代わりにトップへ戻す |
| `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs` | 変更 | 上の2つの生成に `PauseMenuStateService` を渡す・新アクション登録 |
| `moorestech_client/Assets/Scripts/Client.Tests/UIState/PauseMenu/PauseMenuPageNavigationTest.cs` | 新規 | R5・R7・R10 |
| `moorestech_client/Assets/Scripts/Client.Tests/UIState/PauseMenu/PauseMenuShowPageActionTest.cs` | 新規 | R4 |
| `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs` | 変更 | fixture に page・エラーコード `invalid_page` |
| `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/pause_menu.json` / `action_names.json` / `error_codes.json` / `tutorial_anchor_ids.json` | 変更 | 共有契約 |
| `moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/BugReportSubmitKindTest.cs` | 変更 | ハンドラの引数変更 |
| `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Util/BugReportSubmitUtil.cs` | 変更 | ハンドラの引数変更 |
| `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/BugReport/BugReportBundleWriteTest.cs` | 変更 | 送信後「トップに戻り、ポーズは開いたまま」を確認 |
| `Localization/localization.csv` | 変更 | 3キー追加 |
| `moorestech_web/webui/src/bridge/transport/actionContract.ts` | 変更 | `PauseMenuPageNames`・`pause_menu.show_page` |
| `moorestech_web/webui/src/bridge/index.ts` | 変更 | `PauseMenuPageNames` と型を再輸出 |
| `moorestech_web/webui/src/bridge/transport/protocol.ts` | 変更 | 再輸出（既存 `PauseMenuReportKinds` と同じ経路） |
| `moorestech_web/webui/src/bridge/contract/schemas/ui.ts` | 変更 | `PauseMenuDataSchema` に `page` |
| `moorestech_web/webui/src/bridge/contract/wireContract.test.ts` | 変更 | fixture の page を確認 |
| `moorestech_web/webui/e2e/mock-host/topics/topicFixtures.ts` / `topicControls.ts` | 変更 | mock に page・page 切替 |
| `moorestech_web/webui/src/shared/tutorialAnchor/anchorIds.ts` | 変更 | 3アンカー追加 |
| `moorestech_web/webui/src/features/pauseMenu/PauseMenuPanel.tsx` | 変更 | page で中身を差し替え・書きかけを保持 |
| `moorestech_web/webui/src/features/pauseMenu/PauseMenuTopPage.tsx` | 新規 | トップの4ボタン |
| `moorestech_web/webui/src/features/pauseMenu/PauseMenuSubPage.tsx` | 新規 | 子画面の枠（見出し＋戻るボタン） |
| `moorestech_web/webui/src/features/pauseMenu/useBugReportDraft.ts` | 新規 | 書きかけ（説明文・種別）の保持 |
| `moorestech_web/webui/src/features/pauseMenu/BugReportForm.tsx` | 変更 | 書きかけを props で受ける |
| `moorestech_web/webui/src/features/pauseMenu/BugReportForm.test.ts` | 変更 | 描画ヘルパーを書きかけ込みに |
| `moorestech_web/webui/src/features/pauseMenu/PauseMenuPanel.test.ts` | 新規 | R1・R6・R9 |
| `moorestech_web/webui/e2e/tests/system/pauseMenu.spec.ts` | 変更 | R1〜R3 |

`features/pauseMenu/` は変更後 9 ファイル（10以下）。`Client.Tests/UIState/` 直下は既に10ファイルなので、新規テストはサブディレクトリ `UIState/PauseMenu/` に置く。

### データの流れ（書き手と読み手）

```
（Escape キー・Web の show_page・送信成功・ポーズを開く）
  → PauseMenuStateService.ShowPage / HandleCloseKey / OnEnter（書き手）
  → [CurrentPage: ReactiveProperty<PauseMenuPage>]
  → PauseMenuTopic が購読して pause_menu.current を再配信（読み手）
  → Web の useTopic(Topics.pauseMenu).page → PauseMenuPanel が中身を選ぶ（読み手）
```

画面の値を保持するのは `PauseMenuStateService.CurrentPage` だけで、Web は配信値を描くだけ。書き換え4経路はどれも `CurrentPage` を通り、`PauseMenuTopic` の購読（Task 2 Step 3）で再配信されるので、Web 側で取り直す処理は要らない。バグ報告の確保状態（`bugReport`）は今どおり `BugReportCaptureSession.Status` の購読で配られる。

### 配置と前例

- 画面の持ち主を `PauseMenuStateService` に置く: 通常ポーズ（`PauseMenuState`）と入れ子ポーズ（`PauseMenuNestedSubState`）がどちらも Escape 判定をこのサービスへ委ねている（前例: 現 `IsClosePause()`）。サービス1か所を変えれば3か所が揃う。
- 変化の通知は UniRx の `ReactiveProperty`（前例: `BugReportCaptureSession.Status` を `PauseMenuTopic` が `Skip(1).Subscribe` で購読する既存形）。
- Web からの遷移要求はアクションハンドラ（前例: 同ファイルの `PauseMenuSaveActionHandler`）。
- 契約文字列の変換を C# 側の1か所に閉じる（前例: `PlaytestReportKindText.TryParseSubmittableFromPauseMenu`）。

### 今ある操作が残るか

| 操作 | 階層化後 | 根拠 |
|---|---|---|
| セーブ | 残る（トップ） | `pause_menu.save` はそのまま |
| セーブして終了 | 残る（トップ） | `pause_menu.save_and_quit` はそのまま |
| 言語の切り替え | 残る（設定画面） | `LanguageSelect` を移すだけ |
| バグ報告・感想の送信 | 残る（バグ報告画面） | `BugReportForm` を移す。1クリック増えるのはユーザー裁定済み |
| 切断表示 | 残る（トップ） | ADR 0069 |
| Escape でゲームへ戻る | 残る（トップで1回。子画面からは2回） | ユーザー裁定済み |
| 列車HUD・スキット中のポーズ | 残る（同じ階層） | 共有サービスを変えるため |
| Web からの閉じ要求（`ui_state.request GameScreen`） | 残る（どの画面でもポーズを閉じる） | `UiStateActions` と `RequestClosePauseMenu` は変えない |
| チュートリアルの `pause.save`・`pause.back` | 残る（トップ） | アンカーは同じボタンに付けたまま |

---

### Task 1: C# 画面状態と Escape の1段戻り

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PauseMenu/PauseMenuPage.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PauseMenu/PauseMenuStateService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PauseMenuState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/NestedPause/PauseMenuNestedSubState.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/PauseMenu/PauseMenuPageNavigationTest.cs`

**Interfaces:**
- Produces:
  - `enum PauseMenuPage { Top, Settings, BugReport }`（namespace `Client.Game.InGame.UI.UIState.State.PauseMenu`）
  - `PauseMenuStateService.CurrentPage : IReadOnlyReactiveProperty<PauseMenuPage>`
  - `void PauseMenuStateService.ShowPage(PauseMenuPage page)`
  - `bool PauseMenuStateService.StepBackOnCloseKey()` — 子画面ならトップへ移って false、トップなら true（ポーズを閉じてよい）を返す。キー入力は読まない
  - `bool PauseMenuStateService.HandleCloseKey()` — Escape が押されたフレームだけ `StepBackOnCloseKey()` を呼び、その結果を返す。押されていなければ false
  - `IsClosePause()` は削除

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/UIState/PauseMenu/PauseMenuPageNavigationTest.cs`:

```csharp
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.UIState.PauseMenu
{
    public class PauseMenuPageNavigationTest
    {
        [Test]
        public void 開くたびにトップから始まる()
        {
            var service = new PauseMenuStateService();
            service.ShowPage(PauseMenuPage.BugReport);

            service.OnEnter();

            Assert.AreEqual(PauseMenuPage.Top, service.CurrentPage.Value);
        }

        [Test]
        public void 子画面での閉じキーはトップへ1段戻りポーズを閉じない()
        {
            var service = new PauseMenuStateService();
            service.OnEnter();
            service.ShowPage(PauseMenuPage.Settings);

            var shouldClose = service.StepBackOnCloseKey();

            Assert.IsFalse(shouldClose);
            Assert.AreEqual(PauseMenuPage.Top, service.CurrentPage.Value);
        }

        [Test]
        public void トップでの閉じキーはポーズを閉じる()
        {
            var service = new PauseMenuStateService();
            service.OnEnter();

            Assert.IsTrue(service.StepBackOnCloseKey());
            Assert.AreEqual(PauseMenuPage.Top, service.CurrentPage.Value);
        }

        // Escape時点の記録確保はポーズを開いた瞬間だけ。子画面の行き来で確保し直すと報告が別の瞬間を指す
        // The Escape-moment capture fires only when the pause opens; re-capturing on page moves would point the report elsewhere
        [Test]
        public void 子画面の行き来では開いた通知を出さない()
        {
            var service = new PauseMenuStateService();
            var opened = 0;
            service.OnPauseMenuOpened.Subscribe(_ => opened++);
            service.OnEnter();

            service.ShowPage(PauseMenuPage.BugReport);
            service.StepBackOnCloseKey();
            service.ShowPage(PauseMenuPage.Settings);

            Assert.AreEqual(1, opened);
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PauseMenuPage`・`ShowPage`・`CurrentPage`・`StepBackOnCloseKey` が未定義で CS0103/CS1061 のエラー

- [ ] **Step 3: 実装する**

`PauseMenuPage.cs`:

```csharp
namespace Client.Game.InGame.UI.UIState.State.PauseMenu
{
    // ポーズメニュー内の画面。トップと、トップから進む子画面（ADR 0069）
    // Pages inside the pause menu: the top and the sub-pages reached from it (ADR 0069)
    public enum PauseMenuPage
    {
        Top,
        Settings,
        BugReport,
    }
}
```

`PauseMenuStateService.cs` 全体:

```csharp
using System;
using Client.Input;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.PauseMenu
{
    public class PauseMenuStateService
    {
        private readonly Subject<Unit> _onPauseMenuOpened = new();
        private readonly ReactiveProperty<PauseMenuPage> _currentPage = new(PauseMenuPage.Top);

        // 開いたことだけを知らせる。何を確保するかはバグ報告側の関心で、共有UIサービスは知らない
        // Announces only that the menu opened; what gets captured is the bug report's concern, not this shared UI service's
        public IObservable<Unit> OnPauseMenuOpened => _onPauseMenuOpened;

        // 今どの画面にいるか。Escapeを判定するのがここなので、画面の持ち主もここに置く
        // The page currently shown; Escape is judged here, so the page is owned here too
        public IReadOnlyReactiveProperty<PauseMenuPage> CurrentPage => _currentPage;

        public void ShowPage(PauseMenuPage page)
        {
            _currentPage.Value = page;
        }

        // Escapeが押されたフレームだけ1段戻りを判定し、ポーズを閉じてよいかを返す
        // Judges the one-step back only on the frame Escape is pressed and returns whether the pause may close
        public bool HandleCloseKey()
        {
            return InputManager.UI.CloseUI.GetKeyDown && StepBackOnCloseKey();
        }

        // 子画面ならトップへ戻して閉じない。トップなら閉じてよい
        // On a sub-page go back to the top and stay open; on the top the pause may close
        public bool StepBackOnCloseKey()
        {
            if (_currentPage.Value == PauseMenuPage.Top) return true;

            _currentPage.Value = PauseMenuPage.Top;
            return false;
        }

        public void OnEnter()
        {
            // 開くたびにトップから始め、前回の子画面を持ち越さない
            // Every open starts from the top and never carries over the previous sub-page
            _currentPage.Value = PauseMenuPage.Top;
            InputManager.MouseCursorVisible(true);
            _onPauseMenuOpened.OnNext(Unit.Default);
        }
    }
}
```

`PauseMenuState.cs` の `GetNextUpdate`:

```csharp
        public UITransitContext GetNextUpdate()
        {
            return _pauseMenuStateService.HandleCloseKey() ? new UITransitContext(UIStateEnum.GameScreen) : null;
        }
```

`PauseMenuNestedSubState.cs` の `GetNextUpdate`:

```csharp
        public NestedPauseSubStateEnum? GetNextUpdate()
        {
            return _pauseMenuStateService.HandleCloseKey() ? NestedPauseSubStateEnum.GameScreen : null;
        }
```

`IsClosePause` の呼び出しが他に無いことを確認する: `grep -rn "IsClosePause" moorestech_client/Assets/Scripts` が0件。

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.UIState.PauseMenu.PauseMenuPageNavigationTest"`
Expected: 4件 PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State moorestech_client/Assets/Scripts/Client.Tests/UIState/PauseMenu
git commit -m "feat: ポーズメニューの今の画面をPauseMenuStateServiceが持ちEscapeで1段戻る"
```
（Unity が生成した `.meta` も含めてコミットする）

---

### Task 2: C# の配信・遷移アクション・送信後のトップ遷移

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/PauseMenuTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/PauseMenuActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/BugReportSubmitActionHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:93,190-192`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs:130-167`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/pause_menu.json`, `action_names.json`, `error_codes.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/BugReportSubmitKindTest.cs:37-41`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Util/BugReportSubmitUtil.cs:47`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/BugReport/BugReportBundleWriteTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/PauseMenu/PauseMenuShowPageActionTest.cs`

**Interfaces:**
- Consumes: Task 1 の `PauseMenuPage`・`PauseMenuStateService.CurrentPage`・`ShowPage`
- Produces:
  - `pause_menu.current` の JSON に `"page": "top" | "settings" | "bugReport"`
  - アクション `pause_menu.show_page`、payload `{ "page": "top" | "settings" | "bugReport" }`。不正値は `ActionResult.Fail("invalid_page")`
  - `static class PauseMenuPageContract { string ToContractText(PauseMenuPage); bool TryParse(string, out PauseMenuPage) }`（`PauseMenuTopic.cs` 内、namespace `Client.WebUiHost.Game.Topics`）
  - `PauseMenuTopic(WebSocketHub, NetworkDisconnectState, BugReportCaptureSession, PauseMenuStateService)`
  - `PauseMenuShowPageActionHandler(PauseMenuStateService)`
  - `BugReportSubmitActionHandler(BugReportSubmitter, PauseMenuStateService)`（`UIStateControl` は受け取らない）

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/UIState/PauseMenu/PauseMenuShowPageActionTest.cs`:

```csharp
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UIState.PauseMenu
{
    public class PauseMenuShowPageActionTest
    {
        [TestCase("settings", PauseMenuPage.Settings)]
        [TestCase("bugReport", PauseMenuPage.BugReport)]
        [TestCase("top", PauseMenuPage.Top)]
        public void 契約文字列の画面へ移る(string page, PauseMenuPage expected)
        {
            var service = new PauseMenuStateService();
            var handler = new PauseMenuShowPageActionHandler(service);

            var result = handler.ExecuteAsync(new JObject { ["page"] = page }).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok, result.Error);
            Assert.AreEqual(expected, service.CurrentPage.Value);
        }

        [Test]
        public void 不正な画面名は理由をログに出して拒否する()
        {
            var service = new PauseMenuStateService();
            service.ShowPage(PauseMenuPage.Settings);
            var handler = new PauseMenuShowPageActionHandler(service);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("ポーズメニューの画面名が不正"));
            var result = handler.ExecuteAsync(new JObject { ["page"] = "Settings" }).GetAwaiter().GetResult();

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("invalid_page", result.Error);
            Assert.AreEqual(PauseMenuPage.Settings, service.CurrentPage.Value);
        }

        [Test]
        public void 契約文字列は往復で同じ画面に戻る()
        {
            foreach (PauseMenuPage page in System.Enum.GetValues(typeof(PauseMenuPage)))
            {
                Assert.IsTrue(PauseMenuPageContract.TryParse(PauseMenuPageContract.ToContractText(page), out var parsed));
                Assert.AreEqual(page, parsed);
            }
        }
    }
}
```

`WireFixtures/pause_menu.json` を次に置き換える（1行）:

```json
{"disconnected":true,"bugReport":{"kind":"capturing","missing":["video"]},"page":"bugReport"}
```

`WireContractTest.PauseMenuMatchesFixture`（using に `Client.Game.InGame.UI.UIState.State.PauseMenu` を足す）の DTO に `Page = PauseMenuPageContract.ToContractText(PauseMenuPage.BugReport),` を足し、コメントを次へ直す:

```csharp
        // ポーズメニューは切断表示・報告の確保状態・今の画面を配信する
        // The pause menu sends the disconnect state, the report capture status and the current page
```

`WireContractTest` のエラーコード期待集合（`"empty_description", "invalid_kind", ...` の行）に `"invalid_page"` を足す。`WireFixtures/error_codes.json` の `codes` にも `"invalid_page"` を足す。`WireFixtures/action_names.json` の `actions` で `"pause_menu.save_and_quit",` の直後に `"pause_menu.show_page",` を足す。

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PauseMenuShowPageActionHandler`・`PauseMenuPageContract`・`PauseMenuDto.Page` が未定義のエラー

- [ ] **Step 3: 実装する**

`PauseMenuTopic.cs` の変更点:

```csharp
// using に追加
using Client.Game.InGame.UI.UIState.State.PauseMenu;

        private readonly PauseMenuStateService _pauseMenuStateService;

        public PauseMenuTopic(WebSocketHub hub, NetworkDisconnectState state, BugReportCaptureSession bugReportCaptureSession, PauseMenuStateService pauseMenuStateService)
        {
            _hub = hub;
            _state = state;
            _bugReportCaptureSession = bugReportCaptureSession;
            _pauseMenuStateService = pauseMenuStateService;

            // 切断状態・確保状態・今の画面の変化だけを配信し、再接続時はsnapshotから復元する
            // Publish only disconnect, capture-status and page changes; restore from the snapshot after reconnect
            state.OnDisconnectedChanged.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
            bugReportCaptureSession.Status.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
            pauseMenuStateService.CurrentPage.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
        }
```

`BuildJson` の DTO に `Page = PauseMenuPageContract.ToContractText(_pauseMenuStateService.CurrentPage.Value),` を足す。`PauseMenuDto` に `public string Page;` を足す。ファイル末尾に:

```csharp
    // 画面名の契約文字列。Webとの変換はここ1か所で行う（前例 PlaytestReportKindText）
    // Contract text for page names; conversion to and from the Web happens only here (precedent: PlaytestReportKindText)
    public static class PauseMenuPageContract
    {
        public static string ToContractText(PauseMenuPage page)
        {
            return page switch
            {
                PauseMenuPage.Top => "top",
                PauseMenuPage.Settings => "settings",
                PauseMenuPage.BugReport => "bugReport",
                _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
            };
        }

        public static bool TryParse(string text, out PauseMenuPage page)
        {
            switch (text)
            {
                case "top": page = PauseMenuPage.Top; return true;
                case "settings": page = PauseMenuPage.Settings; return true;
                case "bugReport": page = PauseMenuPage.BugReport; return true;
                default: page = PauseMenuPage.Top; return false;
            }
        }
    }
```

`PauseMenuActions.cs` 末尾（namespace 内）に追加し、using に `Client.Game.InGame.UI.UIState.State.PauseMenu`・`Client.WebUiHost.Game.Topics`・`UnityEngine` を足す:

```csharp
    // Webのボタンからの画面遷移要求。画面の持ち主はPauseMenuStateService
    // Page-move requests from the Web buttons; PauseMenuStateService owns the page
    public class PauseMenuShowPageActionHandler : IActionHandler
    {
        private readonly PauseMenuStateService _pauseMenuStateService;
        public string ActionType => "pause_menu.show_page";

        public PauseMenuShowPageActionHandler(PauseMenuStateService pauseMenuStateService)
        {
            _pauseMenuStateService = pauseMenuStateService;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            // 画面名はwebuiの定数が必ず載せる。範囲外は壊れた要求として拒否する
            // The webui constants always send a page name; an out-of-range value is a broken request
            var pageText = payload?["page"]?.ToString() ?? "";
            if (!PauseMenuPageContract.TryParse(pageText, out var page))
            {
                Debug.LogWarning($"ポーズメニューの画面名が不正なため遷移しません page:{pageText}");
                return UniTask.FromResult(ActionResult.Fail("invalid_page"));
            }

            _pauseMenuStateService.ShowPage(page);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
```

`BugReportSubmitActionHandler.cs`: フィールド `UIStateControl _uiStateControl` を `PauseMenuStateService _pauseMenuStateService` に替え、コンストラクタを `(BugReportSubmitter submitter, PauseMenuStateService pauseMenuStateService)` にする。`using Client.Game.InGame.UI.UIState;` を `using Client.Game.InGame.UI.UIState.State.PauseMenu;` に替える。クラス冒頭コメントと送信成功後を次にする:

```csharp
    // 説明文を受け取り、確保済みの記録と一緒に outbox へ書き、ポーズメニューのトップへ戻す（ADR 0069）
    // Takes the description, writes it with the secured records into the outbox, then returns to the pause-menu top (ADR 0069)
```

```csharp
            var submitted = await _submitter.SubmitAsync(description, kind);
            if (!submitted.Submitted) return ActionResult.Fail(submitted.FailureCode);

            // 送れたらポーズは開いたままトップへ戻す。失敗時は画面を動かさず書きかけを残す
            // After a send the pause stays open and returns to the top; on failure the page stays so the draft survives
            _pauseMenuStateService.ShowPage(PauseMenuPage.Top);
            return ActionResult.Success();
```

`WebUiGameBinder.cs`:
- 93行: `new PauseMenuTopic(hub, networkDisconnectState, resolver.Resolve<BugReportCaptureSession>(), resolver.Resolve<PauseMenuStateService>())`
- 191行の後に `hub.RegisterAction(new PauseMenuShowPageActionHandler(resolver.Resolve<PauseMenuStateService>()));`
- 192行: `hub.RegisterAction(new BugReportSubmitActionHandler(resolver.Resolve<BugReportSubmitter>(), resolver.Resolve<PauseMenuStateService>()));`

呼び出し側の引数を直す:
- `BugReportSubmitKindTest.cs`: `new BugReportSubmitActionHandler(new BugReportSubmitter(...), null)` → 第2引数を `new PauseMenuStateService()` に。直前のコメント2行を「kind 検証は送信より前段なので、トップへの遷移は起きない / The kind check runs before submitting, so no move to the top happens」に直す。using に `Client.Game.InGame.UI.UIState.State.PauseMenu` を足す。
- `BugReportSubmitUtil.cs:47`: `new BugReportSubmitActionHandler(submitter, resolver.Resolve<PauseMenuStateService>())`。using を足す。`Client.Game.InGame.UI.UIState` の using は `OpenPauseMenuAndWaitCapture` が `UIStateControl` を使うので残す。

`BugReportBundleWriteTest.cs`（using に `Client.Game.InGame.UI.UIState.State.PauseMenu` を足す）: 2つのテストで `SubmitAndTakeNewBundle` の直前に、バグ報告画面にいる状態を作る1行を足す:

```csharp
                // 実際の送信はバグ報告画面から行われるので、その画面にいる状態から送る
                // Real sends happen from the bug-report page, so send while standing on it
                resolver.Resolve<PauseMenuStateService>().ShowPage(PauseMenuPage.BugReport);
```

`AssertReturnedToGameScreen` を次に置き換え、2つの呼び出しも `AssertReturnedToPauseMenuTop(resolver)` に直す:

```csharp
        // 送信後はポーズを閉じずにトップへ戻る（ADR 0069）
        // After a send the pause stays open and returns to its top (ADR 0069)
        private static async UniTask AssertReturnedToPauseMenuTop(IObjectResolver resolver)
        {
            var uiState = resolver.Resolve<UIStateControl>();
            var pauseMenu = resolver.Resolve<PauseMenuStateService>();
            for (var i = 0; i < 40 && pauseMenu.CurrentPage.Value != PauseMenuPage.Top; i++) await UniTask.Delay(50);
            Assert.AreEqual(PauseMenuPage.Top, pauseMenu.CurrentPage.Value, "送信後にポーズメニューのトップへ戻っていない");
            Assert.AreEqual(UIStateEnum.PauseMenu, uiState.CurrentState, "送信後にポーズメニューが閉じている");
        }
```

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PauseMenuShowPageActionTest|PauseMenuPageNavigationTest|WireContract|BugReportSubmitKindTest|TutorialAnchorContractTest"`
Expected: 全件 PASS
Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.EditModeInPlayingTest.BugReport.BugReportBundleWriteTest" --timeout-seconds 1500`（EditModeInPlayingTest は EditMode テストとして走り途中で PlayMode へ移る。uloop v3 の既定 EditMode のままでよい）
Expected: PASS。「Unity is reloading」エラーが出たら45秒待って再試行。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat: ポーズメニューの画面を配信しshow_pageで遷移させ送信成功後はトップへ戻す"
```

---

### Task 3: Web 契約（schema・action・mock）

**Files:**
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts`
- Modify: `moorestech_web/webui/src/bridge/transport/protocol.ts`
- Modify: `moorestech_web/webui/src/bridge/index.ts:15-16`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts:58`
- Modify: `moorestech_web/webui/src/bridge/contract/wireContract.test.ts:105-109`
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicFixtures.ts:48`
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicControls.ts`

**Interfaces:**
- Consumes: Task 2 の JSON 形（`page` と `pause_menu.show_page`）
- Produces:
  - `PauseMenuPageNames = { top: "top", settings: "settings", bugReport: "bugReport" } as const`
  - `type PauseMenuPageName`
  - `ActionPayloads["pause_menu.show_page"] = { page: PauseMenuPageName }`
  - `PauseMenuData.page: "top" | "settings" | "bugReport"`
  - mock host が action `pause_menu.show_page` を受けたら `pause_menu.current` の `page` を書き換えて再配信する

- [ ] **Step 1: 失敗するテストを書く**

`wireContract.test.ts` の既存テストへ1行足す:

```ts
  it("pause_menu が切断状態と今の画面を受理する", () => {
    const data = loadFixture("pause_menu.json");
    expect(parseTopicPayload(Topics.pauseMenu, data).valid).toBe(true);
    expect((data as PauseMenuData).disconnected).toBe(true);
    expect((data as PauseMenuData).page).toBe("bugReport");
  });
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/bridge/contract`
Expected: FAIL（`page` が strict でない schema に無く型エラー、または `actionNames.test.ts` が `pause_menu.show_page` 不足で失敗）

- [ ] **Step 3: 実装する**

`actionContract.ts` の `PauseMenuReportKinds` の下に:

```ts
// C#のPauseMenuPageContractと1対1で対応する画面名（ADR 0069）
// Page names mirroring C#'s PauseMenuPageContract one-to-one (ADR 0069)
export const PauseMenuPageNames = {
  top: "top",
  settings: "settings",
  bugReport: "bugReport",
} as const;

export type PauseMenuPageName = (typeof PauseMenuPageNames)[keyof typeof PauseMenuPageNames];
```

`ActionPayloads` の `"pause_menu.save_and_quit"` の次に `"pause_menu.show_page": { page: PauseMenuPageName };`、`ACTION_TYPES` の `"pause_menu.save_and_quit",` の次に `"pause_menu.show_page",` を足す。

`protocol.ts` で `PauseMenuReportKinds` を再輸出している行に `PauseMenuPageNames` を、型の再輸出に `PauseMenuPageName` を足す。`bridge/index.ts` 15・16行も同様に足す。

`schemas/ui.ts:58`:

```ts
export const PauseMenuDataSchema = z.object({
  disconnected: z.boolean(),
  bugReport: BugReportStatusSchema,
  page: z.enum(["top", "settings", "bugReport"]),
});
```

`topicFixtures.ts:48`:

```ts
  [Topics.pauseMenu]: () => ({ disconnected: false, bugReport: { kind: "ready", missing: [] }, page: state.pauseMenuPage }),
```

`topicControls.ts`: 既存の mock state に `pauseMenuPage: "top"` を足し、既存の action 受信処理（`pause_menu.save` 等を記録している箇所）で `pause_menu.show_page` を受けたら `state.pauseMenuPage = payload.page` にして `Topics.pauseMenu` を再配信する。`setUiState(page, "PauseMenu")` 系でポーズに入るときは `state.pauseMenuPage = "top"` に戻す（C# の `OnEnter` と同じ）。既存の `pauseDisconnected` シナリオの fixture にも `page: "top"` を足す。書き方は同ファイル内の既存の state 更新と再配信の形に合わせる（実装者は `grep -n "publish\|state\." e2e/mock-host/topics/topicControls.ts` で既存形を確認してから書く）。

- [ ] **Step 4: テスト**

Run: `cd moorestech_web/webui && pnpm vitest run src/bridge && pnpm tsc -b`
Expected: PASS・型エラー0

- [ ] **Step 5: コミット**

```bash
git add moorestech_web/webui/src/bridge moorestech_web/webui/e2e/mock-host
git commit -m "feat: webui契約にポーズメニューの画面名とshow_pageを足す"
```

---

### Task 4: Web のパネル階層化・文言・アンカー

**Files:**
- Modify: `Localization/localization.csv`
- Modify（生成）: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`
- Modify: `moorestech_web/webui/src/shared/tutorialAnchor/anchorIds.ts`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/tutorial_anchor_ids.json`
- Create: `moorestech_web/webui/src/features/pauseMenu/useBugReportDraft.ts`
- Create: `moorestech_web/webui/src/features/pauseMenu/PauseMenuTopPage.tsx`
- Create: `moorestech_web/webui/src/features/pauseMenu/PauseMenuSubPage.tsx`
- Modify: `moorestech_web/webui/src/features/pauseMenu/PauseMenuPanel.tsx`
- Modify: `moorestech_web/webui/src/features/pauseMenu/BugReportForm.tsx`
- Modify: `moorestech_web/webui/src/features/pauseMenu/BugReportForm.test.ts`
- Create: `moorestech_web/webui/src/features/pauseMenu/PauseMenuPanel.test.ts`
- Modify: `moorestech_web/webui/e2e/tests/system/pauseMenu.spec.ts`

**Interfaces:**
- Consumes: Task 3 の `PauseMenuPageNames`・`ActionPayloads["pause_menu.show_page"]`・`PauseMenuData.page`
- Produces:
  - `type BugReportDraft = { description: string; kind: PauseMenuReportKind; setDescription(v: string): void; setKind(v: PauseMenuReportKind): void; reset(): void }`
  - `function useBugReportDraft(): BugReportDraft`
  - `BugReportForm({ status, draft }: { status: PauseMenuData["bugReport"]; draft: BugReportDraft })`
  - `PauseMenuTopPage()`、`PauseMenuSubPage({ title, children })`

- [ ] **Step 1: 文言とアンカーを足す**

`Localization/localization.csv` の `ui.pauseMenu.disconnected` の行の直後に:

```
ui.pauseMenu.settings,Settings,Settings,設定,Einstellungen
ui.pauseMenu.bugReport,Bug report,Bug report,バグ報告,Fehlerbericht
ui.pauseMenu.back,Back,Back,戻る,Zurück
```

Run: `cd moorestech_web/webui && pnpm gen:i18n`（`localizationKeys.ts` に `pauseMenu.settings/bugReport/back` が出ることを確認）

`anchorIds.ts` の `pauseBack` の次に:

```ts
  pauseSettings: "pause.settings",
  pauseBugReport: "pause.bug-report",
  pauseBackToTop: "pause.back-to-top",
```

`tutorial_anchor_ids.json` の `staticIds` の `"pause.back",` の次に `"pause.settings", "pause.bug-report", "pause.back-to-top",` を足す。

- [ ] **Step 2: 失敗するテストを書く**

`BugReportForm.test.ts` の `render` を書きかけ込みにする（他のテストは変えない。既存テストはすべてこのヘルパー経由なので、送信成功で入力が空になる確認もそのまま効く）:

```ts
import { useBugReportDraft } from "./useBugReportDraft";

function FormWithDraft({ status }: { status: Status }) {
  const draft = useBugReportDraft();
  return createElement(BugReportForm, { status, draft });
}

async function render(status: Status): Promise<ReactTestRenderer> {
  let renderer: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(FormWithDraft, { status }));
  });
  return renderer!;
}
```

既存テスト「送信後にポーズメニューを閉じるactionは出さない」のコメント2行と名前を次に直す（中身の assert は変えない。トップへ戻すのも C# の送信ハンドラが持つ）:

```ts
  // 送信後の画面遷移はC#の送信ハンドラ1本が持つ。Web側からも遷移させると同じ判断が2箇所に増える
  // The one C# submit handler owns the page move after a send; moving from the Web too would put the same decision in two places
  it("送信後に画面遷移のactionは出さない", async () => {
```

同テストの assert に `expect(mocks.dispatchAction).not.toHaveBeenCalledWith("pause_menu.show_page", expect.anything());` を足す。

`PauseMenuPanel.test.ts` を新規作成する。モックの張り方は `BugReportForm.test.ts` と同じ（`@/bridge` の `useTopic`・`dispatchAction`・`readTopic` を差し替え、`@mantine/core` の `Button`・`Stack`・`Text`・`Title` を素の要素へ、`@/shared/ui` の `ModeSwitch` をスタブ、`@/features/settings` の `LanguageSelect` を `createElement("mock-language-select", { "data-testid": "language-select" })` へ）:

```ts
import { createElement, type ReactNode } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const mocks = vi.hoisted(() => ({
  dispatchAction: vi.fn(async () => true),
  readTopic: vi.fn(() => null as unknown),
  pauseMenu: { current: null as unknown },
}));

vi.mock("@/bridge", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/bridge")>();
  return {
    ...actual,
    dispatchAction: mocks.dispatchAction,
    readTopic: mocks.readTopic,
    useTopic: (topic: string) => (topic === actual.Topics.pauseMenu ? mocks.pauseMenu.current : null),
  };
});
vi.mock("@/features/toast", () => ({ emitToast: vi.fn() }));
vi.mock("@mantine/core", () => ({
  Button: ({ children, ...rest }: { children: ReactNode }) => createElement("button", rest, children),
  Stack: ({ children, ...rest }: { children: ReactNode }) => createElement("div", rest, children),
  Text: ({ children }: { children: ReactNode }) => createElement("p", null, children),
  Title: ({ children }: { children: ReactNode }) => createElement("h1", null, children),
}));
vi.mock("@/shared/ui", () => ({
  ModeSwitch: ({ value, onChange, testId }: { value: string; onChange: (v: string) => void; testId?: string }) =>
    createElement("mock-mode-switch", { value, onChange, "data-testid": testId }),
}));
vi.mock("@/features/settings", () => ({
  LanguageSelect: () => createElement("mock-language-select", { "data-testid": "language-select" }),
}));

import { PauseMenuPanel } from "./PauseMenuPanel";

type Page = "top" | "settings" | "bugReport";
const ready = { kind: "ready", missing: [] as string[] };

afterEach(() => {
  vi.clearAllMocks();
  mocks.pauseMenu.current = null;
});

describe("PauseMenuPanel", () => {
  it("トップは4ボタンだけを出し、言語選択と報告欄を出さない", async () => {
    const renderer = await render("top");
    for (const id of ["pause-menu-save", "pause-menu-save-and-quit", "pause-menu-open-settings", "pause-menu-open-bug-report"]) {
      expect(byTestId(renderer, id)).toHaveLength(1);
    }
    expect(byTestId(renderer, "language-select")).toHaveLength(0);
    expect(byTestId(renderer, "bug-report-description")).toHaveLength(0);
    act(() => renderer.unmount());
  });

  it("設定ボタンはsettingsへのshow_pageを送る", async () => {
    const renderer = await render("top");
    await act(async () => byTestId(renderer, "pause-menu-open-settings")[0].props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("pause_menu.show_page", { page: "settings" });
    act(() => renderer.unmount());
  });

  it("設定画面は言語選択と戻るボタンを出し、戻るはtopへのshow_pageを送る", async () => {
    const renderer = await render("settings");
    expect(byTestId(renderer, "language-select")).toHaveLength(1);
    await act(async () => byTestId(renderer, "pause-menu-back")[0].props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("pause_menu.show_page", { page: "top" });
    act(() => renderer.unmount());
  });

  // 同じポーズの中で画面を行き来しても書きかけは残る（ADR 0069）
  // The draft survives page moves inside the same pause (ADR 0069)
  it("バグ報告の書きかけはトップへ戻って再び開いても残る", async () => {
    const renderer = await render("bugReport");
    act(() => byTestId(renderer, "bug-report-description")[0].props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    await setPage(renderer, "top");
    await setPage(renderer, "bugReport");
    expect(byTestId(renderer, "bug-report-description")[0].props.value).toBe("ベルトが止まる");
    act(() => renderer.unmount());
  });

  // ポーズを閉じるとパネルごと外れ、書きかけは捨てられる
  // Closing the pause unmounts the panel, and the draft goes with it
  it("パネルを外して付け直すと書きかけは空になる", async () => {
    const first = await render("bugReport");
    act(() => byTestId(first, "bug-report-description")[0].props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    act(() => first.unmount());
    const second = await render("bugReport");
    expect(byTestId(second, "bug-report-description")[0].props.value).toBe("");
    act(() => second.unmount());
  });
});

async function render(page: Page): Promise<ReactTestRenderer> {
  setDictionaries("japanese", {}, {}, {});
  mocks.pauseMenu.current = { disconnected: false, bugReport: ready, page };
  let renderer: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(PauseMenuPanel));
  });
  return renderer!;
}

async function setPage(renderer: ReactTestRenderer, page: Page) {
  mocks.pauseMenu.current = { disconnected: false, bugReport: ready, page };
  await act(async () => renderer.update(createElement(PauseMenuPanel)));
}

function byTestId(renderer: ReactTestRenderer, id: string) {
  return renderer.root.findAll((node) => typeof node.type === "string" && node.props["data-testid"] === id);
}
```

- [ ] **Step 3: 実行して失敗を確認する**

Run: `cd moorestech_web/webui && pnpm vitest run src/features/pauseMenu`
Expected: FAIL（`useBugReportDraft` が無い、`pause-menu-open-settings` が無い）

- [ ] **Step 4: 実装する**

`useBugReportDraft.ts`:

```ts
import { useState } from "react";
import { PauseMenuReportKinds, type PauseMenuReportKind } from "@/bridge";

// バグ報告の書きかけ。ポーズメニューのパネルが持ち、ポーズを閉じてパネルが外れると一緒に消える（ADR 0069）
// The bug-report draft; the pause-menu panel holds it and it vanishes with the panel when the pause closes (ADR 0069)
export type BugReportDraft = {
  description: string;
  kind: PauseMenuReportKind;
  setDescription: (value: string) => void;
  setKind: (value: PauseMenuReportKind) => void;
  reset: () => void;
};

export function useBugReportDraft(): BugReportDraft {
  const [description, setDescription] = useState("");
  const [kind, setKind] = useState<PauseMenuReportKind>(PauseMenuReportKinds.bug);

  // 種別も既定へ戻す。残すと次の1件が前回の種別のまま箱詰めされる（ADR 0058）
  // Reset the kind too: leaving it boxes the next report under the previous kind (ADR 0058)
  const reset = () => {
    setDescription("");
    setKind(PauseMenuReportKinds.bug);
  };

  return { description, kind, setDescription, setKind, reset };
}
```

`BugReportForm.tsx`:
- 冒頭コメントを「バグ報告画面の報告欄。書きかけは親のパネルが持ち、ここは入力と送信だけを担う / The report form on the bug-report page; the parent panel holds the draft, this only handles input and sending」に直す。
- `Props` に `draft: BugReportDraft` を足し、`useState` の `description`・`kind` を削除して `draft.description`・`draft.kind`・`draft.setDescription`・`draft.setKind` を使う。
- 送信成功後の `setDescription(""); setKind(PauseMenuReportKinds.bug);` とその直前のコメント2行を `draft.reset();` に置き換える（理由コメントは `useBugReportDraft.ts` に移った）。
- `sending` の `useState` は残す（送信中の手元状態は画面ごとのもので、書きかけではない）。

`PauseMenuSubPage.tsx`:

```tsx
import { Button, Stack, Title } from "@mantine/core";
import type { ReactNode } from "react";
import { dispatchAction, PauseMenuPageNames } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";

// 子画面の枠。見出しと、トップへ戻るボタンを持つ
// Frame for a sub-page: a heading and a button back to the top
export function PauseMenuSubPage({ title, children }: { title: string; children: ReactNode }) {
  const { t } = useI18n();
  const back = () => void dispatchAction("pause_menu.show_page", { page: PauseMenuPageNames.top });

  return (
    <Stack gap="md">
      <Title order={1}>{title}</Title>
      {children}
      <Button variant="default" onClick={back} data-testid="pause-menu-back" {...tutorialAnchor(TutorialAnchorIds.pauseBackToTop)}>
        {t(L.ui.pauseMenu.back)}
      </Button>
    </Stack>
  );
}
```

`PauseMenuTopPage.tsx`:

```tsx
import { Button, Stack, Text, Title } from "@mantine/core";
import { dispatchAction, PauseMenuPageNames, type PauseMenuPageName } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";

// ポーズメニューのトップ。セーブ系2つと、子画面への入口2つを並べる（ADR 0069）
// The pause-menu top: two save actions plus two entries to the sub-pages (ADR 0069)
export function PauseMenuTopPage({ disconnected }: { disconnected: boolean }) {
  const { t } = useI18n();
  const disconnectColor = "red";
  const save = () => void dispatchAction("pause_menu.save", {});
  const quit = () => void dispatchAction("pause_menu.save_and_quit", {});
  const open = (page: PauseMenuPageName) => void dispatchAction("pause_menu.show_page", { page });

  return (
    <Stack gap="md">
      <Title order={1}>{t(L.ui.pauseMenu.title)}</Title>
      {disconnected && <Text c={disconnectColor}>{t(L.ui.pauseMenu.disconnected)}</Text>}
      <Button onClick={save} data-testid="pause-menu-save" {...tutorialAnchor(TutorialAnchorIds.pauseSave)}>
        {t(L.ui.game.saveGame)}
      </Button>
      <Button onClick={quit} data-testid="pause-menu-save-and-quit" {...tutorialAnchor(TutorialAnchorIds.pauseBack)}>
        {t(L.ui.game.saveAndQuit)}
      </Button>
      <Button onClick={() => open(PauseMenuPageNames.settings)} data-testid="pause-menu-open-settings" {...tutorialAnchor(TutorialAnchorIds.pauseSettings)}>
        {t(L.ui.pauseMenu.settings)}
      </Button>
      <Button onClick={() => open(PauseMenuPageNames.bugReport)} data-testid="pause-menu-open-bug-report" {...tutorialAnchor(TutorialAnchorIds.pauseBugReport)}>
        {t(L.ui.pauseMenu.bugReport)}
      </Button>
    </Stack>
  );
}
```

`PauseMenuPanel.tsx` 全体:

```tsx
import { Topics, useTopic } from "@/bridge";
import { LanguageSelect } from "@/features/settings";
import { L, useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { BugReportForm } from "./BugReportForm";
import { PauseMenuSubPage } from "./PauseMenuSubPage";
import { PauseMenuTopPage } from "./PauseMenuTopPage";
import { useBugReportDraft } from "./useBugReportDraft";
import styles from "./style.module.css";

// どの画面を出すかはC#が配るpageだけで決める。書きかけはここで持ち、画面を行き来しても残す
// Which page shows is decided only by the page C# publishes; the draft lives here so it survives page moves
export function PauseMenuPanel() {
  const data = useTopic(Topics.pauseMenu);
  const { locale, t } = useI18n();
  const draft = useBugReportDraft();

  return (
    <section className={styles.panel} data-testid="pause-menu" {...tutorialAnchor(TutorialAnchorIds.pauseMenu)}>
      <div data-testid={`pause-menu-locale-${locale}`}>{renderPage()}</div>
    </section>
  );

  function renderPage() {
    // 初回配信前はトップの枠だけ出す。報告欄は確保状態が届くまで描かない
    // Before the first delivery only the top renders; the report form waits for the capture status
    if (!data) return <PauseMenuTopPage disconnected={false} />;
    switch (data.page) {
      case "top": return <PauseMenuTopPage disconnected={data.disconnected} />;
      case "settings": return <PauseMenuSubPage title={t(L.ui.pauseMenu.settings)}><LanguageSelect /></PauseMenuSubPage>;
      case "bugReport": return <PauseMenuSubPage title={t(L.ui.pauseMenu.bugReport)}><BugReportForm status={data.bugReport} draft={draft} /></PauseMenuSubPage>;
    }
  }
}
```

`pauseMenu.spec.ts`: 既存1本目（セーブ／セーブして終了）はボタン名で押しているのでそのまま通る。次を足す:

```ts
test("トップから設定画面へ進み言語を選べ、戻るでトップへ戻る", async ({ page }) => {
  await setUiState(page, "PauseMenu");
  await page.goto("/");
  const menu = page.getByTestId("pause-menu");
  await expect(menu.getByTestId("language-select")).toHaveCount(0);

  await menu.getByTestId("pause-menu-open-settings").click();
  await expect.poll(async () => (await payloadsOf(page, "pause_menu.show_page")).at(-1)).toEqual({ page: "settings" });
  await expect(menu.getByTestId("language-select")).toBeVisible();

  await menu.getByTestId("pause-menu-back").click();
  await expect.poll(async () => (await payloadsOf(page, "pause_menu.show_page")).at(-1)).toEqual({ page: "top" });
  await expect(menu.getByTestId("pause-menu-open-bug-report")).toBeVisible();
});

test("トップからバグ報告画面へ進むと報告欄が出る", async ({ page }) => {
  await setUiState(page, "PauseMenu");
  await page.goto("/");
  const menu = page.getByTestId("pause-menu");
  await expect(menu.getByTestId("bug-report-description")).toHaveCount(0);

  await menu.getByTestId("pause-menu-open-bug-report").click();
  await expect(menu.getByTestId("bug-report-description")).toBeVisible();
});
```

`e2e/tests/system/i18n.spec.ts` がポーズメニュー上で言語選択を操作している場合（`language-select-option-*` を押している箇所）は、その前に `page.getByTestId("pause-menu-open-settings").click()` を足す。見出し `Pause Menu` の確認はトップで行われるのでそのまま。

- [ ] **Step 5: テスト**

Run: `cd moorestech_web/webui && pnpm vitest run && pnpm tsc -b && pnpm lint`
Expected: 全件 PASS・エラー0
Run: `cd moorestech_web/webui && pnpm test:e2e -- --grep "PauseMenu|pause_menu|設定画面|バグ報告画面|i18n|入れ子Pause"`
Expected: PASS（失敗 spec が毎回変わるときはポート5273の共有を疑う）
Run: `uloop compile --project-path ./moorestech_client`（localization.csv 変更の反映。CS0117 が出たら force-recompile を1回挟む）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TutorialAnchorContractTest|Localization"`
Expected: PASS

- [ ] **Step 6: コミット**

```bash
git add Localization/localization.csv moorestech_web/webui moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/tutorial_anchor_ids.json
git commit -m "feat: ポーズメニューをトップと設定・バグ報告の子画面に分ける"
```

---

### Task 5: 実機での確認（unityプレイ録画テスト）

**Files:** なし（確認のみ。記録は `../moorestech_logs/` 側）

- [ ] **Step 1: Unity を起動しコンパイルを通す**

Run: `uloop launch /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/pause-menu-hierarchy/moorestech_client` → `uloop compile --project-path ./moorestech_client`（エラー0）

- [ ] **Step 2: unityプレイ録画テストで通しを動かす**

unity-playmode-recorded-playtest スキルに従い、DSL のシナリオで次の順に操作して録画する（入力は InputSystem 注入。OS の simulate 系は使わない）:
1. Escape → ポーズのトップが出る（4ボタン）
2. 「設定」→ 言語選択が出る → Escape → トップへ戻る（ポーズは開いたまま）
3. 「バグ報告」→ 説明文を入力 → 「戻る」→ 再び「バグ報告」→ 書きかけが残っている
4. 「送信」→ トップへ戻り、ポーズは開いたまま、トーストが出る
5. トップで Escape → ゲームへ戻る
6. 列車に乗った状態で 1・2・5 を繰り返す（列車HUDの入れ子ポーズ）

- [ ] **Step 3: 合否をログの警告語で判定する**

合格の条件は、期待どおりの画面が出ることに加えて、シナリオの区間に次の語が **0件** であること。`uloop get-logs --project-path ./moorestech_client --log-type Warning` と `--log-type Error` で引く:
`ポーズメニューの画面名が不正` / `invalid_page` / `transition_not_allowed` / `バグ報告の説明文が空` / `プレイ報告の種別が不正` / `Exception`

1件でもあれば不合格として原因を直し、Step 2 からやり直す。手順4の送信では、outbox に報告の箱が1つ増え、manifest の `kind` が `bug` になるところまで確かめる。

---

### Task 6: 全ブランチレビュー（省略不可）

- [ ] **Step 1:** 必ず最後に moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）。
- [ ] **Step 2:** レビュー指摘を反映して判定経路・条件式・その評価時点（`HandleCloseKey`・`StepBackOnCloseKey`・`ShowPage` の呼び出し箇所、送信ハンドラの遷移）に触れたら、Task 5 の unityプレイ録画テストを反映後のビルドでやり直してから完了とする。
- [ ] **Step 3:** plan・実機記録・進捗台帳に書いた「未検証」「未確認」「残差」は、1件ずつ `bd create` で起票し、記録の結論には issue 番号を並べる（「残差は X のみ」とまとめない）。
- [ ] **Step 4:** Task 5 の合否は、期待する画面が出たことではなく、Step 3 に挙げた警告・拒否の語がシナリオ区間で0件であることで判定したかを確認する。

---

## 判断記録（ADR）

- 設計: `docs/adr/0069-pause-menu-hierarchy-top-settings-bug-report.md`（トップ4ボタン・Esc1段戻り・送信後トップ・設定は言語のみ・画面の持ち主は C#・同じパネルで差し替え・毎回トップから・書きかけはポーズ中だけ残す）
- 裁定の蒸留: `.decisions/2026-09-24-ポーズメニューは4ボタンのトップから設定とバグ報告の子画面へ階層化する.md`、`.decisions/2026-09-24-ポーズメニューの子画面でEscapeは1段戻る.md`、`.decisions/2026-09-24-バグ報告画面の送信成功後はポーズのトップへ戻る.md`、`.decisions/2026-09-24-設定画面は今回言語選択の移設だけにする.md`
- planning 中に新たに決めたこと:
  - 画面名の契約文字列は `top` / `settings` / `bugReport`（小文字始まり。既存の `bugReport.kind` の値の形に合わせる）。出所: agent前提（`BugReportCaptureStatus` の契約値の前例）
  - Web からの遷移要求は新アクション `pause_menu.show_page` 1本。不正値は `invalid_page` で拒否してログを出す。出所: agent前提（`pause_menu.save` と同じアクションハンドラの形・AGENTS.md「無音の縮退は禁止」）
  - `IsClosePause()` を `HandleCloseKey()` に名前を変える。1段戻りの副作用を持つようになり、名前と処理を合わせるため。キー入力に触れない判定部分 `StepBackOnCloseKey()` を分けてテストする。出所: agent前提（AGENTS.md「名前は実処理と一致させる」）
  - Web からの閉じ要求（`ui_state.request GameScreen`）は、どの画面にいてもポーズを閉じる今の挙動のまま変えない。出所: agent前提（Esc の1段戻り裁定はキー操作についてのもので、Web の閉じ要求は対象外）
  - 書きかけは `useBugReportDraft` で `PauseMenuPanel` が持つ。パネルの unmount で消えることを「ポーズを閉じたら捨てる」の実現とする。出所: agent前提（ADR 0069 の書きかけの扱い）
  - Web のタスク分割は契約（Task 3）と画面（Task 4）に分ける。契約だけを先にレビューで通せるため。出所: agent前提
