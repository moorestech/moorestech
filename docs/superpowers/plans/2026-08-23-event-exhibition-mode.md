# Event Exhibition Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** gamescom出展用に「終了＝リセット」で回る展示モードを実装する（ADR: `docs/adr/0030-event-exhibition-mode.md`）

**Architecture:** 環境変数 `MOORESTECH_EVENT_MODE=1` で有効化されるイベントモードを `Client.Starter/EventMode/` に集約する。有効時は起動直後に既定ワールド（world_1）を削除→言語を英語へ強制→メインメニューを経由せず自動でローカルゲーム開始。無操作180秒で `Application.Quit` し、同梱の再起動ループスクリプト（.command）が新規ワールドで再起動する。あわせてポーズメニューの「セーブしてメインメニューへ戻る」を「セーブして終了」へ恒久差し替えする（イベントモード非依存の永続変更）。

**Tech Stack:** Unity C#（Client.Starter / Client.Game / Client.WebUiHost）、React+TypeScript（moorestech_web/webui）、ローカライズCSV（`Localization/localization.csv`）、bash（macOS .command）

## Requirements

- R1: 環境変数 `MOORESTECH_EVENT_MODE=1` でイベントモードが有効になる（それ以外の値・未設定は通常動作。設定ファイルは使わない — cache/のDebugParameters残置事故の回避）
- R2: イベントモード有効時、起動直後に既定ワールドディレクトリを削除し、必ず新規ワールドで開始する（PlayerPrefsは消さない — reset all dataとは異なる）
- R3: イベントモード有効時、言語を毎起動で英語（`Localize.DefaultLanguageCode`）へ強制リセットする
- R4: イベントモード有効時、メインメニューを経由せず自動でローカルゲーム開始まで進む
- R5: イベントモード有効時、一定時間（既定180秒、環境変数 `MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS` で上書き可）キーボード・マウス入力が無ければ `Application.Quit` する
- R6: ポーズメニューの「セーブしてメインメニューへ戻る」を「セーブして終了」（セーブflush→アプリ終了）へ差し替える。**イベントモードに関係ない永続変更**。ローカライズキーは `ui.game.saveAndQuit`（英: "Save and Quit"、日: "セーブして終了"）
- R7: macOS用の再起動ループスクリプト（.command）をリポジトリに置く。ダブルクリックで起動し、ゲーム終了のたびに再起動する。イベントモード環境変数はこのスクリプトが立てる
- R8: 通常モード（環境変数なし）の挙動はR6以外一切変えない
- やらないこと: アトラクトモード／終了画面・QR／計測ログ／高速起動／アクセシビリティ／コントローラー対応／スタッフ用チート新設／ゲーム内イベントモード設定UI（すべてADRで棄却済み）

## Global Constraints

- AGENTS.md 全規約に従う（partial禁止・`Func<>`禁止・try-catch原則禁止・1ファイル200行以下・[SerializeField]は_無し小文字キャメル・デフォルト引数禁止・イベントはUniRx）
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client` を実行する
- .metaファイルは手動作成しない（新規.csはUnity起動時に自動生成されたものをコミット）。シーン・Prefabの直接テキスト編集は禁止
- コメントは日本語・英語2行セット（各1行）
- webui配下を触るタスクは実装前に `webui-design` スキルを読む（ホワイトリスト方式）
- ドメインリロードエラー時は45秒待ってリトライ
- コミットは各タスク末尾で必ず行う（git worktree運用のため作業消失防止）

## 配置と前例

| 項目 | 配置先 | 前例 |
|---|---|---|
| `EventExhibitionMode`（環境変数読み） | `Client.Starter/EventMode/` | `Client.WebUiHost/Common/WebUiHostMode.cs`（環境変数によるモード切替） |
| `LocalGameLauncher`（開始フロー静的化） | `Client.Starter/` | `InitializeProprieties`（同asmdefのstatic factory）。`StartLocal.cs`から移設 |
| `EventModeAutoStart` / `EventIdleQuitWatcher` | `Client.Starter/EventMode/` | `Localize.Initialize`（`RuntimeInitializeOnLoadMethod`前例）。**ランタイムGameObject生成は新規パターン**（シーン事前配置が原則だが、イベントモード限定オブジェクトをシーンに常駐させない方を優先。Instanceプロパティ動的生成禁止則の対象外） |
| ワールド削除 | `EventModeAutoStart`内 | `ResetAllDataConfirmPopup.ResetAllData()`（`new StartServerSettings().WorldDirectory`削除と同経路） |
| セーブして終了 | `Client.Game/InGame/Presenter/PauseMenu/`（`BackToMainMenu.cs`を改名） | 既存`BackToMainMenu.Disconnect()`のセーブ→切断→`GameShutdownEvent`発火順を維持 |
| webuiアクション | `pause_menu.save_and_quit` | 既存`pause_menu.back_to_main_menu`の改名（actionContract.ts＋PauseMenuActions.cs） |

### 機能パリティ死活表（ポーズメニュー）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| セーブ | 生きる | 変更なし |
| セーブしてメインメニューへ戻る | **消える→「セーブして終了」に置換** | ユーザー裁定 2026-08-23（メニューへ戻る道を作らない設計への整合） |
| 言語切り替え | 生きる | 変更なし |
| メインメニューの各操作（通常モード時） | 生きる | イベントモード無効時はメニューを一切触らない |

---

### Task 1: EventExhibitionMode（環境変数設定クラス）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventExhibitionMode.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventExhibitionModeTest.cs`

**Interfaces:**
- Produces: `Client.Starter.EventMode.EventExhibitionMode` — `static bool IsEnabled`、`static int IdleTimeoutSeconds`、`static bool IsEnabledValue(string rawValue)`、`static int ParseIdleTimeoutSeconds(string rawValue)`（Task 3, 4, 6が使用）

- [x] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventExhibitionModeTest.cs`（Client.Tests（`Tests.asmdef`）は`Client.Starter`参照済み）:

```csharp
using Client.Starter.EventMode;
using NUnit.Framework;

namespace Client.Tests.EventMode
{
    public class EventExhibitionModeTest
    {
        [Test]
        public void IsEnabledValue_AcceptsOnlyOne()
        {
            Assert.IsTrue(EventExhibitionMode.IsEnabledValue("1"));
            Assert.IsFalse(EventExhibitionMode.IsEnabledValue(null));
            Assert.IsFalse(EventExhibitionMode.IsEnabledValue(""));
            Assert.IsFalse(EventExhibitionMode.IsEnabledValue("true"));
        }

        [Test]
        public void ParseIdleTimeoutSeconds_AcceptsOnlyPositiveInt_DefaultsTo180()
        {
            Assert.AreEqual(180, EventExhibitionMode.ParseIdleTimeoutSeconds(null));
            Assert.AreEqual(60, EventExhibitionMode.ParseIdleTimeoutSeconds("60"));
            Assert.AreEqual(180, EventExhibitionMode.ParseIdleTimeoutSeconds("0"));
            Assert.AreEqual(180, EventExhibitionMode.ParseIdleTimeoutSeconds("-5"));
            Assert.AreEqual(180, EventExhibitionMode.ParseIdleTimeoutSeconds("abc"));
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `EventExhibitionMode` が存在しないためコンパイルエラー

- [x] **Step 3: 実装を書く**

`moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventExhibitionMode.cs`:

```csharp
using System;

namespace Client.Starter.EventMode
{
    // イベント出展モードの有効判定と設定値（起動スクリプトが環境変数で注入する）
    // Event exhibition mode flags and settings, injected via env vars by the launch script
    public static class EventExhibitionMode
    {
        public const string EnableEnvKey = "MOORESTECH_EVENT_MODE";
        public const string IdleTimeoutEnvKey = "MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS";
        public const int DefaultIdleTimeoutSeconds = 180;

        public static bool IsEnabled => IsEnabledValue(Environment.GetEnvironmentVariable(EnableEnvKey));
        public static int IdleTimeoutSeconds => ParseIdleTimeoutSeconds(Environment.GetEnvironmentVariable(IdleTimeoutEnvKey));

        public static bool IsEnabledValue(string rawValue)
        {
            return rawValue == "1";
        }

        public static int ParseIdleTimeoutSeconds(string rawValue)
        {
            // 正の整数のみ受理し、それ以外は既定値へ戻す / Accept only positive integers, else fall back to the default
            return int.TryParse(rawValue, out var seconds) && seconds > 0 ? seconds : DefaultIdleTimeoutSeconds;
        }
    }
}
```

- [x] **Step 4: コンパイル＋テストが通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EventExhibitionModeTest"`
Expected: 2 tests PASS

- [x] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/EventMode moorestech_client/Assets/Scripts/Client.Tests/EventMode
git commit -m "feat: イベント出展モードの環境変数設定クラスを追加"
```

（Unity起動で生成された.metaも同時にコミットする。以降のタスクも同様）

---

### Task 2: LocalGameLauncher（ローカルゲーム開始フローの静的化）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/LocalGameLauncher.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/StartLocal.cs`

**Interfaces:**
- Produces: `Client.Starter.LocalGameLauncher.StartLocalGame()`（static void。Task 3が使用）

- [x] **Step 1: LocalGameLauncherを作る**

`moorestech_client/Assets/Scripts/Client.Starter/LocalGameLauncher.cs`:

```csharp
using Client.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter
{
    // ローカルゲーム開始フロー（メニューのボタンとイベントモード自動開始の共通経路）
    // Local game start flow shared by the menu button and event-mode auto start
    public static class LocalGameLauncher
    {
        public static void StartLocalGame()
        {
            SceneManager.sceneLoaded += OnGameInitializerSceneLoaded;
            SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
        }

        private static void OnGameInitializerSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnGameInitializerSceneLoaded;
            var starter = Object.FindObjectOfType<InitializeScenePipeline>();
            starter.SetProperty(InitializeProprieties.CreateLocalServer(PlayerPrefs.GetInt(PlayerPrefsKeys.PlayerIdKey)));
        }
    }
}
```

- [x] **Step 2: StartLocal.csを呼び出しに書き換える**

`moorestech_client/Assets/Scripts/Client.MainMenu/StartLocal.cs` の全体を以下に置き換える:

```csharp
using Client.Starter;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu
{
    public class StartLocal : MonoBehaviour
    {
        [SerializeField] private Button startLocalButton;

        private void Start()
        {
            startLocalButton.onClick.AddListener(LocalGameLauncher.StartLocalGame);
        }
    }
}
```

（`Client.MainMenu.asmdef`は`Client.Starter`参照済み — 既存StartLocalが`InitializeScenePipeline`を使っていたことから確認済み）

- [x] **Step 3: コンパイルを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [x] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/LocalGameLauncher.cs* moorestech_client/Assets/Scripts/Client.MainMenu/StartLocal.cs
git commit -m "refactor: ローカルゲーム開始フローをLocalGameLauncherへ抽出"
```

---

### Task 3: EventModeAutoStart（ワールド削除＋英語強制＋自動開始）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventModeAutoStart.cs`

**Interfaces:**
- Consumes: `EventExhibitionMode.IsEnabled`（Task 1）、`LocalGameLauncher.StartLocalGame()`（Task 2）、`Localize.TrySetLanguage` / `Localize.DefaultLanguageCode`（`Client.Localization`、`Client.Starter.asmdef`参照済み）、`new StartServerSettings().WorldDirectory`（`Server.Boot`、参照済み）

- [x] **Step 1: 実装を書く**

`moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventModeAutoStart.cs`:

```csharp
using System.IO;
using Client.Common;
using Client.Localization;
using Server.Boot;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter.EventMode
{
    // イベント出展モード: 起動時にワールドを削除し英語へ戻して自動でローカルゲームを開始する
    // Event exhibition mode: on boot, delete the world, reset to English, and auto-start the local game
    public static class EventModeAutoStart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void AutoStartIfEventMode()
        {
            if (!EventExhibitionMode.IsEnabled) return;
            // メインメニュー以外からの起動（テスト・Editor別シーン再生）では何もしない
            // Do nothing when booted outside the main menu (tests, editor playback of other scenes)
            if (SceneManager.GetActiveScene().name != SceneConstant.MainMenuSceneName) return;

            DeleteDefaultWorldDirectory();
            Localize.TrySetLanguage(Localize.DefaultLanguageCode);
            LocalGameLauncher.StartLocalGame();
        }

        private static void DeleteDefaultWorldDirectory()
        {
            // 既定ワールドを削除し毎回新規生成にする（ResetAllDataConfirmPopupと同経路。PlayerPrefsは残す）
            // Delete the default world so every boot generates fresh (same path as ResetAllDataConfirmPopup, PlayerPrefs kept)
            var worldDirectory = new StartServerSettings().WorldDirectory;
            if (Directory.Exists(worldDirectory)) Directory.Delete(worldDirectory, true);
        }
    }
}
```

`SceneConstant.MainMenuSceneName` は既存（`BackToMainMenu.cs`が使用中）。

- [x] **Step 2: コンパイルを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [x] **Step 3: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/EventMode
git commit -m "feat: イベントモードの起動時ワールド削除・英語強制・自動開始"
```

---

### Task 4: 「セーブして終了」への差し替え（C#側）

**Files:**
- Rename+Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs` → `SaveAndQuitPresenter.cs`（**必ず`git mv`でファイル改名し.metaも`git mv`する** — GUID維持でシーン参照を保つ）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:144`（SerializeFieldの型・名前変更）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/PauseMenuActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:194`

**Interfaces:**
- Produces: `Client.Game.InGame.Presenter.PauseMenu.SaveAndQuitPresenter` — `public void SaveAndQuit()`。webuiアクション名 `"pause_menu.save_and_quit"`（Task 5が使用）

- [x] **Step 1: ファイルを改名する**

```bash
git mv moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/SaveAndQuitPresenter.cs
git mv moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs.meta moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/SaveAndQuitPresenter.cs.meta
```

- [x] **Step 2: SaveAndQuitPresenterを実装する**

`SaveAndQuitPresenter.cs` の全体を以下に置き換える（旧uGUIボタン配線は削除。セーブ→切断→`GameShutdownEvent`発火の順序は旧`Disconnect()`を維持）:

```csharp
using System;
using System.Threading;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Presenter.PauseMenu
{
    // ゲーム内の正規終了経路。メインメニューへは戻らずセーブ後にアプリを終了する（AGENTS.md既知の制約に整合）
    // Canonical in-game exit path: saves and quits the app without returning to the main menu
    public class SaveAndQuitPresenter : MonoBehaviour
    {
        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        public void SaveAndQuit()
        {
            SaveAndQuitAsync().Forget();
        }

        private async UniTask SaveAndQuitAsync()
        {
            Disconnect();
            // サーバー側ShutdownAsyncのセーブflush完了を待ってからプロセスを終える
            // Wait for the server-side ShutdownAsync save flush before ending the process
            await UniTask.Delay(TimeSpan.FromSeconds(2), true);
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void Disconnect()
        {
            ClientContext.VanillaApi.SendOnly.Save();
            Thread.Sleep(50);
            ClientContext.VanillaApi.Disconnect();
            // Web UI と内蔵サーバーへゲーム終了を通知する。内蔵サーバーは保存を消化してから自壊する
            // Notify the Web UI and the embedded server; the server folds itself after flushing pending saves
            GameShutdownEvent.FireGameShutdown();
        }
    }
}
```

- [x] **Step 3: 参照側を更新する**

`MainGameStarter.cs:144` 付近:

```csharp
using UnityEngine.Serialization; // ファイル先頭のusingに追加

[FormerlySerializedAs("backToMainMenu")]
[SerializeField] private SaveAndQuitPresenter saveAndQuitPresenter;
```

同ファイル内で `backToMainMenu` を使っている箇所（VContainer登録等）を `grep -n backToMainMenu` で探し、すべて `saveAndQuitPresenter` へ置換する。

`PauseMenuActions.cs` の `PauseMenuBackToMainMenuActionHandler` を以下に置き換える:

```csharp
    public class PauseMenuSaveAndQuitActionHandler : IActionHandler
    {
        private readonly SaveAndQuitPresenter _saveAndQuitPresenter;
        public string ActionType => "pause_menu.save_and_quit";

        public PauseMenuSaveAndQuitActionHandler(SaveAndQuitPresenter saveAndQuitPresenter)
        {
            _saveAndQuitPresenter = saveAndQuitPresenter;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            _saveAndQuitPresenter.SaveAndQuit();
            return UniTask.FromResult(ActionResult.Success());
        }
    }
```

`WebUiGameBinder.cs:194`:

```csharp
            hub.RegisterAction(new PauseMenuSaveAndQuitActionHandler(resolver.Resolve<SaveAndQuitPresenter>()));
```

`resolver.Resolve<BackToMainMenu>()` の登録元（MainGameStarterのVContainer登録）も型を追従させる。残存参照は `grep -rn "BackToMainMenu" moorestech_client/Assets/Scripts --include="*.cs"` で洗い出し、`NetworkDisconnectPresenter.cs`のコメント中の言及も「SaveAndQuitPresenter」へ直す。

- [x] **Step 4: コンパイルを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [x] **Step 5: シーン参照の生存を確認する**

`uloop execute-dynamic-code` で確認（MainGameシーンを開いてMainGameStarterの`saveAndQuitPresenter`がnullでないことを検証）。FormerlySerializedAs＋GUID維持なら参照は生きているはず。nullならこのステップで再配線する（テキスト編集ではなくdynamic-code経由）。

- [x] **Step 6: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "feat: ポーズメニューの「メインメニューへ戻る」を「セーブして終了」へ差し替え"
```

---

### Task 5: webui側の差し替え（ローカライズ・アクション・E2E）

**注意: 実装前に `webui-design` スキルを読むこと（ホワイトリスト方式）。**

**Files:**
- Modify: `Localization/localization.csv:6`
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts:48,93`
- Modify: `moorestech_web/webui/src/features/pauseMenu/PauseMenuPanel.tsx`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/action_names.json:22`（C#/TS共有のアクション名フィクスチャ。Task 4レビューで発見しTask 5へ移送）
- Modify: `moorestech_web/webui/e2e/tests/system/pauseMenu.spec.ts:19`
- Regenerate: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（手編集禁止・スクリプト生成）

**Interfaces:**
- Consumes: C#側アクション名 `"pause_menu.save_and_quit"`（Task 4）

- [x] **Step 1: ローカライズCSVのキーと文言を差し替える**

`Localization/localization.csv` 6行目:

```
ui.game.saveAndBackToMainMenu,Save and Back to MainMenu,Save and Back to MainMenu,セーブしてメインメニューに戻る
```

を以下へ置き換える（列構成: key, source, english, japanese）:

```
ui.game.saveAndQuit,Save and Quit,Save and Quit,セーブして終了
```

- [x] **Step 2: 生成物を更新する**

Run: `cd moorestech_web/webui && pnpm gen:i18n`
Expected: `localizationKeys.ts` から `saveAndBackToMainMenu` が消え `saveAndQuit` が入る

C#側の生成テーブル（`Mooresmaster.Localization.Generated`）はSourceGeneratorが再生成する。`_CompileRequester.cs`のdirtyがあるのはこのためなので**revertしない**。旧キーのC#残存参照を確認: `grep -rn "saveAndBackToMainMenu" moorestech_client/Assets/Scripts --include="*.cs"`（生成物以外でヒットしたら追従修正）。

- [x] **Step 3: actionContract.tsを更新する**

48行目と93行目の `"pause_menu.back_to_main_menu"` を `"pause_menu.save_and_quit"` に置換する。

- [x] **Step 4: PauseMenuPanel.tsxを更新する**

```tsx
  const quitLabel = t(L.ui.game.saveAndQuit);
  const quit = () => void dispatchAction("pause_menu.save_and_quit", {});
```

（旧 `backLabel` / `back` を置換。ボタンのJSXも `onClick={quit}` / `{quitLabel}` へ。`tutorialAnchor(TutorialAnchorIds.pauseBack)` はそのまま残す — anchor id改名はチュートリアルマスタへ波及するため据え置き）

- [x] **Step 5: E2Eテストを更新する**

`e2e/tests/system/pauseMenu.spec.ts:19` の `"pause_menu.back_to_main_menu"` を `"pause_menu.save_and_quit"` へ。spec内の他の旧名参照も `grep -n back_to_main_menu` で確認して置換する。

- [x] **Step 6: 検証する**

Run: `cd moorestech_web/webui && pnpm build && pnpm test`
Expected: 型チェック・ユニットテスト通過
Run: `cd moorestech_web/webui && pnpm test:e2e -- --grep "pause"`（grepオプションが効かない場合は全件実行）
Expected: pauseMenu系E2E通過
Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（ローカライズ再生成の取り込み）

- [x] **Step 7: コミットする**

```bash
git add Localization moorestech_web/webui moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git commit -m "feat: webuiポーズメニューをセーブして終了へ差し替え"
```

---

### Task 6: EventIdleQuitWatcher（無操作自動終了）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventIdleQuitWatcher.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Client.Starter.asmdef`（references配列に `"Unity.InputSystem"` を追加 — 現状未参照であることを確認済み。asmdefはJSONなのでEdit可）

**Interfaces:**
- Consumes: `EventExhibitionMode.IsEnabled` / `IdleTimeoutSeconds`（Task 1）

- [x] **Step 1: asmdefにUnity.InputSystemを追加する**

`Client.Starter.asmdef` の `references` 配列末尾に `"Unity.InputSystem"` を追加する（参照名は`Client.Game.asmdef`と同じ表記）。

- [x] **Step 2: 実装を書く**

`moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventIdleQuitWatcher.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Starter.EventMode
{
    // イベント出展モード: 一定時間無入力ならアプリを終了する（ループスクリプトが新規ワールドで再起動する）
    // Event exhibition mode: quit after sustained input silence (the loop script relaunches with a fresh world)
    public class EventIdleQuitWatcher : MonoBehaviour
    {
        private float _idleSeconds;
        private Vector2 _lastMousePosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void CreateIfEventMode()
        {
            // イベントモード限定の常駐監視オブジェクトを起動時に生成する（シーンには置かない）
            // Spawn the event-mode-only resident watcher at boot instead of placing it in scenes
            if (!EventExhibitionMode.IsEnabled) return;
            var watcherObject = new GameObject(nameof(EventIdleQuitWatcher));
            DontDestroyOnLoad(watcherObject);
            watcherObject.AddComponent<EventIdleQuitWatcher>();
        }

        private void Update()
        {
            if (HasAnyInput())
            {
                _idleSeconds = 0f;
                return;
            }

            _idleSeconds += Time.unscaledDeltaTime;
            if (_idleSeconds < EventExhibitionMode.IdleTimeoutSeconds) return;

            // ワールドは次回起動時に削除されるためセーブ完了を待たず即終了してよい
            // The world is deleted on next boot, so quitting without waiting for a save flush is fine
            Application.Quit();
        }

        private bool HasAnyInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.isPressed) return true;

            var mouse = Mouse.current;
            if (mouse == null) return false;
            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed) return true;

            // deltaはフレーム跨ぎで欠落しうるため位置比較で移動を検知する
            // Detect motion by comparing positions since delta can be missed across frames
            var position = mouse.position.ReadValue();
            var moved = (position - _lastMousePosition).sqrMagnitude > 0.01f;
            _lastMousePosition = position;
            return moved;
        }
    }
}
```

- [x] **Step 3: コンパイルを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [x] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter
git commit -m "feat: イベントモードの無操作自動終了ウォッチャーを追加"
```

---

### Task 7: 再起動ループスクリプト（macOS）

**Files:**
- Create: `scripts/event/start-gamescom-loop.command`

- [x] **Step 1: スクリプトを書く**

```bash
#!/bin/bash
# gamescom出展用: イベントモードでゲームを無限ループ起動する（終了＝リセット）
# For the gamescom booth: run the game in event mode in an endless loop (quit = reset)
set -u
cd "$(dirname "$0")"

export MOORESTECH_EVENT_MODE=1
# 無操作タイムアウト秒を変えたい場合はコメントを外す / Uncomment to override the idle timeout seconds
# export MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS=180

# 引数で.appを指定可能。省略時は同ディレクトリのmoorestech.app
# The .app path can be given as an argument; defaults to moorestech.app beside this script
APP_PATH="${1:-./moorestech.app}"
BINARY="$(find "$APP_PATH/Contents/MacOS" -maxdepth 1 -type f 2>/dev/null | head -n 1)"
if [ -z "$BINARY" ]; then
  echo "app not found: $APP_PATH"
  exit 1
fi

while true; do
  "$BINARY"
  echo "=== game exited, relaunching... (close this window to stop the loop) ==="
  sleep 1
done
```

- [x] **Step 2: 実行権限を付けて動作確認する**

```bash
chmod +x scripts/event/start-gamescom-loop.command
scripts/event/start-gamescom-loop.command /path/to/nonexistent.app
```
Expected: `app not found: /path/to/nonexistent.app` と表示して exit 1（実.appでの通し確認はビルド後にユーザーが行う）

- [x] **Step 3: コミットする**

```bash
git add scripts/event/start-gamescom-loop.command
git commit -m "feat: gamescom展示用の再起動ループスクリプトを追加"
```

---

### Task 8: 最終ブランチレビュー（省略不可）

- [ ] **Step 1:** 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（moores-code-review スキル。自動実行・ゴール文言による省略不可）
- [ ] **Step 2:** レビュー指摘の機械的修正を適用し、コンパイル・テスト再確認後にコミットする

---

## 手動検証（実装完了後・ユーザーと協働）

1. Editorでの通常モード確認: 環境変数なしでPlayMode起動→ポーズメニューに「Save and Quit」表示、押下でセーブ後にPlayMode停止（Editorでは`isPlaying=false`）
2. macビルド＋スクリプト通し確認（ユーザー実施）: `.app`をビルドし`scripts/event/start-gamescom-loop.command`で起動→新規ワールド自動開始→Cmd+Q→自動再起動→再び新規ワールド、を2周確認。放置180秒で自動終了→再起動も確認
3. 英語ローカライズの品質確認（ユーザー実施・ADRに記載済みの別タスク）

## 判断記録（ADR）

- 設計裁定の正本: `docs/adr/0030-event-exhibition-mode.md`（全ユーザー裁定の出所つき）
- planning中の追加判断（すべてagent前提）:
  - イベントモードコードの置き場は`Client.Starter/EventMode/`に集約（`WebUiHostMode`の環境変数前例・`LocalGameLauncher`との循環参照回避）
  - `BackToMainMenu`は`git mv`によるGUID維持改名＋`FormerlySerializedAs`でシーン参照を保つ
  - ローカライズはキーごと`ui.game.saveAndQuit`へ改名（名前は実処理と一致させる規約）。`TutorialAnchorIds.pauseBack`はチュートリアルマスタへの波及を避け据え置き
  - `SaveAndQuitAsync`のflush待ちは固定2秒（サーバー`ShutdownAsync`の完了通知が既存契約に無いため。イベントモードでは削除されるので影響なし、通常モードの厳密化は将来課題）
  - 無操作タイムアウト既定180秒（ADRのConsequencesに記載済み）
  - ランタイムGameObject生成（EventIdleQuitWatcher）は新規パターンとして採用（イベントモード限定オブジェクトを全シーンに事前配置しないため。Instanceプロパティ動的生成禁止則の対象外）
