// 段階 5 の並べ比べ（組 2）: 出展モードで起動し、言語ゲートとチャレンジ一覧を同じ実ゲームの状態で 1280×720 と 2560×1080 に撮る。全体スイッチの両系で同じ手順が通る（S5-ladder）
// Stage 5 comparison (set 2): boot in exhibition mode and shoot the language gate and challenge list in the same real game state at 1280x720 and 2560x1080; the same steps pass on both UI systems (S5-ladder)
// 実行は stage5/driver/winunity-scenario-run.sh <branch> <GameUi|Web> exhibition <この .cs> <name> から。参照（web-8a585f742a）の画面の字は日本語なので、撮る間は日本語にしてゲートでも日本語を選ぶ
// Run via stage5/driver/winunity-scenario-run.sh <branch> <GameUi|Web> exhibition <this .cs> <name>; the reference (web-8a585f742a) shows Japanese, so shoot in Japanese and choose Japanese at the gate
using System.IO;
using System.Linq;
using Client.Game.Common;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Core.Master;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

// 撮るのは静止画だけなので録画しない（操作オーバーレイは撮る前に消す）
// Only stills are needed, so no recording (the operation overlay is hidden before each shot)
var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 500f };
return PlaytestRunner.Run("uitk-stage5-compare", options, async p =>
{
    // 1. 系を記録し、撮影名の頭を系で分ける。入力の準備
    // 1. Record the system and prefix shot names by it; prepare input
    var system = UiInputExclusivity.ActiveUiSystem;
    var shotPrefix = system == UiSystem.GameUi ? "uitk" : "web-live";
    p.Note($"系={system}");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    var initialized = false;
    var initializedSubscription = GameInitializedEvent.OnGameInitialized.Subscribe(_ => initialized = true);

    // 2. DI より前のゲート。選ぶ前の言語を控え、撮る間だけ参照と同じ日本語を当てる（前例 uitk-event-language-gate）
    // 2. The pre-DI gate; note the pre-choice language and apply the reference's Japanese only while shooting (precedent uitk-event-language-gate)
    await p.UntilUiElement("event-language-gate", 90f);
    p.Assert(!initialized, "言語を選ぶまで初期化が完了しない");
    var languageBefore = Localize.GetCurrentLanguageCode();
    var languageBeforePath = Path.Combine(Application.dataPath, "..", "PlaytestResults", "scratch", "event-language-gate", "language-before.txt");
    Directory.CreateDirectory(Path.GetDirectoryName(languageBeforePath));
    File.WriteAllText(languageBeforePath, languageBefore);
    p.Assert(Localize.TrySetChosenLanguage("japanese"), "撮影の言語 japanese を当てられる");
    await MoveCursorOffPanelsAsync();
    await ShootBothResolutionsAsync("5-event-language-gate");

    // 日本語を選んで初期化を待つ
    // Choose Japanese and wait for initialization
    await p.ClickUi("event-language-gate-option-japanese");
    await p.Until(() => initialized, 180f, "選択の後に初期化が完了する");
    await p.WaitSeconds(1f);
    p.Assert(!(await PlaytestUiElementOps.FindUi("event-language-gate")).IsFound, "選択の後はゲートが無い");
    p.Assert(Localize.GetCurrentLanguageCode() == "japanese", "日本語で始まる");

    // 3. ゲーム画面へ進み、参照に無い背景スキットの字幕とチュートリアルの枠を消す（前例 uitk-challenge-list）
    // 3. Reach the game screen and drop the background-skit subtitle and tutorial outlines absent from the reference (precedent uitk-challenge-list)
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    // Game View を表へ出し、Story のままなら飛ばし直して Story を抜ける（回数は Driver が Note に残す）
    // Bring the Game View front and re-skip while in Story until it leaves (the Driver notes the rounds)
    await p.SkipOpeningSkitUntilLeftStory();
    await p.ExitToGameScreen();
    var backgroundSkit = Object.FindFirstObjectByType<Client.Game.InGame.BackgroundSkit.BackgroundSkitManager>(FindObjectsInactive.Include);
    await p.Until(() => !backgroundSkit.IsPlayingSkit, 120f, "背景スキットが終わる");
    foreach (var challenge in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements.SelectMany(c => c.Challenges)) TutorialPresentationStateStore.Instance.EndSession(challenge.ChallengeGuid);

    // 4. T でチャレンジ一覧を開き、中央寄せの動きが止まってから撮る
    // 4. Open the challenge list with T and shoot once centering settles
    await p.PressKey(Key.T);
    await p.WaitUiState(UIStateEnum.ChallengeList, 10f);
    await p.UntilUiElement("challenge-panel", 10f);
    await p.WaitSeconds(1f);
    await MoveCursorOffPanelsAsync();
    await ShootBothResolutionsAsync("5-challenge-list");

    // 5. 閉じて言語を戻す。戻せたときだけ控えを消し、戻せなければ控えを残して運転役に戻させる
    // 5. Close and restore the language; delete the note only on success, else keep it for the driver to retry
    await p.PressKey(Key.T);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    var languageRestored = Localize.TrySetChosenLanguage(languageBefore);
    p.Note($"選ぶ前の言語={languageBefore}（戻せた={languageRestored}）");
    p.Assert(languageRestored, $"選ぶ前の言語 {languageBefore} へ戻せる");
    if (languageRestored) File.Delete(languageBeforePath);
    initializedSubscription.Dispose();

    #region Internal

    async UniTask ShootBothResolutionsAsync(string shotId)
    {
        // 参照と同じ 2 つの解像度で撮り、1280×720 へ戻す
        // Shoot at the reference's two resolutions and return to 1280x720
        await ShootAsync(shotId, 1280, 720);
        await ShootAsync(shotId, 2560, 1080);
        await SetResolutionAsync(1280, 720);
    }

    async UniTask ShootAsync(string shotId, int width, int height)
    {
        // 操作オーバーレイを消してから撮る（参照に無いものを写さない）
        // Hide the operation overlay before shooting (keep things the reference lacks out of the shot)
        await SetResolutionAsync(width, height);
        foreach (var overlay in Object.FindObjectsByType<PlaytestOverlayView>(FindObjectsSortMode.None)) overlay.gameObject.SetActive(false);
        await p.WaitSeconds(0.5f);
        await p.Screenshot($"{shotPrefix}-{shotId}-{width}x{height}");
    }

    async UniTask SetResolutionAsync(int width, int height)
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
        await UniTask.DelayFrame(3);
        await p.WaitSeconds(1.5f);
        p.Assert(Screen.width == width && Screen.height == height, $"Screen が {width}x{height} (実際 {Screen.width}x{Screen.height})");
    }

    async UniTask MoveCursorOffPanelsAsync()
    {
        // カーソルを画面の左端の中ほど（どのボタンにも掛からない）へ置き、ホバーの無い状態にする
        // Put the cursor mid-left of the screen (over no button) so nothing is hovered
        SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.03f, Screen.height * 0.5f));
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
