// 束 3 の並べ比べ（組 2: 崩れの確認）: GameUi で参照 3-hotbar-equipment-empty・3-build-menu・3-modal と同じ状態を実画面で作り、1280×720 と 2560×1080 で撮る（S3-ladder）
// Bundle 3 comparison (set 2, breakage check): in GameUi build the same states as references 3-hotbar-equipment-empty, 3-build-menu and 3-modal on the real screen and shoot at 1280x720 and 2560x1080 (S3-ladder)
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

// 撮るのは静止画だけなので録画しない（操作オーバーレイは撮る前に消す）
// Only stills are needed, so no recording (the operation overlay is hidden before each shot)
var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 300f };
return PlaytestRunner.Run("uitk-stage3-compare", options, async p =>
{
    // 1. 系の主張と入力の準備（前例 uitk-build-bp）
    // 1. Assert the UI system and prepare input (precedent uitk-build-bp)
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();

    // 2. ホットバーは枠 0 に木のチェストだけ（参照の fixture の枠 0）。BP の範囲に入れるチェストを 1 個置く
    // 2. The hotbar holds only the wooden chest in slot 0 (reference fixture slot 0); place one chest for the blueprint range
    await p.Hotbar.AssignHotbar(0, "木のチェスト");
    await p.Hotbar.UnlockBlueprint();
    await p.PlaceBlockDirect("木のチェスト", new Vector3Int(2, 32, 2), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 2));
    // BP 解放の通知（参照に無い行）が消えるまで待つ
    // Wait until the blueprint-unlocked notification (absent from the reference) leaves
    await p.WaitSeconds(6f);

    // 3. 解像度ごとにホットバーとビルドメニュー（検索欄は空・最初のカテゴリ・ホバー無し）を撮る
    // 3. Per resolution shoot the hotbar and the build menu (empty search, first category, no hover)
    foreach (var (width, height) in new[] { (1280, 720), (2560, 1080) })
    {
        await SetResolutionAsync(width, height);
        p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, $"ホットバーの撮影はゲーム画面 実際:{p.CurrentUiState}");
        await ShootAsync("3-hotbar-equipment-empty", width, height);
        await p.PressKey(Key.B);
        await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
        await MoveCursorOffPanelsAsync();
        await ShootAsync("3-build-menu", width, height);
        await p.PressKey(Key.B);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    // 4. ビルドメニューから BP コピーを選び範囲を打って名前のモーダルを出す（空欄・確定ボタン無効）。開いたまま 2 解像度で撮る
    // 4. Pick blueprint copy from the build menu and mark the range to open the name modal (empty, confirm disabled); shoot it open at both resolutions
    p.WarpPlayer(new Vector3(6f, 33.5f, -5f));
    var checks = new BlueprintCopyPasteScenarioChecks(p, resolver.Resolve<MouseCursorTooltipState>());
    await checks.SelectEntry("build-menu-category-d1000000-0000-4000-8000-000000000009", "build-menu-entry-blueprintCopy-88dd687d-aceb-4aeb-94e8-44be1e1f5a0d");
    await p.AimAt(new Vector3(0.5f, 32f, 0.5f));
    await p.ClickPlace();
    await p.AimAt(new Vector3(4.5f, 32f, 4.5f));
    await p.ClickPlace();
    await p.UntilUiElement("modal-input", 10f);
    // 無効は SetEnabled の擬似状態なので要素から読む（前例 uitk-hotbar-and-bp-modal）
    // Disabled is SetEnabled's pseudo state, so read the element (precedent uitk-hotbar-and-bp-modal)
    var uiTree = GameUiRoot.Instance.GetRootElement();
    p.Assert(uiTree.Q<TextField>("modal-input").value == "", "名前の入力欄は空");
    p.Assert(!uiTree.Q<VisualElement>("modal-button").enabledSelf, "空の名前では確定ボタンが無効");
    await MoveCursorOffPanelsAsync();
    await ShootAsync("3-modal", 2560, 1080);
    await SetResolutionAsync(1280, 720);
    await MoveCursorOffPanelsAsync();
    await ShootAsync("3-modal", 1280, 720);
    await p.PressKey(Key.Escape);
    await p.WaitSeconds(0.5f);
    p.Assert(!(await PlaytestUiElementOps.FindUi("modal")).IsFound, "ESC でモーダルが閉じる（後片付け）");

    #region Internal

    async UniTask SetResolutionAsync(int width, int height)
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
        await p.WaitSeconds(1.5f);
        p.Assert(Screen.width == width && Screen.height == height, $"Screen が {width}x{height} (実際 {Screen.width}x{Screen.height})");
    }

    // カーソルを画面の左端の中ほど（どのパネルにも掛からない）へ置き、ホバーの無い状態にする
    // Put the cursor mid-left of the screen (over no panel) so nothing is hovered
    async UniTask MoveCursorOffPanelsAsync()
    {
        SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.03f, Screen.height * 0.5f));
        await p.WaitSeconds(0.5f);
    }

    // 操作オーバーレイを消してから撮る（参照に無いものを写さない）
    // Hide the operation overlay before shooting (keep things the reference lacks out of the shot)
    async UniTask ShootAsync(string shotId, int width, int height)
    {
        foreach (var overlay in Object.FindObjectsByType<PlaytestOverlayView>(FindObjectsSortMode.None)) overlay.gameObject.SetActive(false);
        await p.WaitSeconds(0.3f);
        await p.Screenshot($"uitk-{shotId}-{width}x{height}");
    }

    #endregion
});
