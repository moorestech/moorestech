// 束 2 の並べ比べ（組 2）: 同じ実ゲームの状態を GameUi と Web の両系で作り、参照キャプチャと同じ撮影 id で 1280×720 と 2560×1080 に撮る
// Bundle-2 comparison (group 2): build the same real game states on both GameUi and Web and shoot them under the reference capture ids at 1280x720 and 2560x1080
// 実行は winunity play <branch> <this> --ui-system GameUi|Web。系の照会（UITK の木）は GameUi の回だけ行う
// Run with winunity play <branch> <this> --ui-system GameUi|Web; UITK tree queries run only on the GameUi pass
// 2-train-hud は乗車で分岐 1 しか出ないので撮らない。通知とスキットは uitk-notification-compare・uitk-skit-compare が受け持つ
// 2-train-hud is skipped (riding shows only one branch); notifications and skits belong to uitk-notification-compare and uitk-skit-compare
using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Client.Ui.Foundation.Root;
using Client.WebUiHost.Boot;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f };
return PlaytestRunner.Run("uitk-stage2-compare", options, async p =>
{
    // 系の主張: 起動した系とホストの有無が噛み合う（両系で同じ手順を流すので系そのものは固定しない）
    // Assert the system matches the host presence (both systems run the same steps, so the system itself is not pinned)
    var isGameUi = UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi;
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    // 裏タブの Game View は描画されずスクショが空になるので表へ出す
    // A Game View behind another tab never renders and screenshots come out empty, so bring it to front
    PlaytestGameViewOps.BringToFront();
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    p.Assert(isGameUi == (WebUiHost.Hub == null), isGameUi ? "GameUi の回は Web ホストが無い" : "Web の回は Web ホストがある");

    // 開幕スキットと背景スキットを抜け、参照に無い操作オーバーレイを切る（前例 uitk-skit-compare）
    // Leave the opening and background skits and turn off the operation overlay absent from the references (precedent uitk-skit-compare)
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    var backgroundSkit = Object.FindFirstObjectByType<Client.Game.InGame.BackgroundSkit.BackgroundSkitManager>(FindObjectsInactive.Include);
    await p.Until(() => !backgroundSkit.IsPlayingSkit, 120f, "背景スキットが終わる");
    Object.FindFirstObjectByType<PlaytestOverlayView>().enabled = false;
    await p.Hotbar.UnlockConnectTool("電線");
    await p.Until(() => Camera.main != null, 10f, "MainCameraの起動");

    // 先頭チャレンジのピン先の木（前例 uitk-opening-hud）
    // The tree the first challenge pins (precedent uitk-opening-hud)
    var category = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements.Single(c => c.InitialUnlocked);
    var first = MasterHolder.ChallengeMaster.GetCategoryInitialChallenges(category.CategoryGuid).Single();
    var pinTutorial = first.Tutorials.Single(t => t.TutorialType == TutorialsElement.TutorialTypeConst.mapObjectPin);
    var treeGuids = MasterHolder.ChallengeMaster.ResolvePinTargets((MapObjectPinTutorialParam)pinTutorial.TutorialParam);
    var scaffold = new Vector3(6f, 33.5f, -5f);

    foreach (var (width, height) in new[] { (1280, 720), (2560, 1080) })
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, width, height);
        await ShootAll($"{width}x{height}");
    }
    await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);

    #region Internal

    async UniTask ShootAll(string res)
    {
        // ゲーム画面と、store へ差したキー操作ヒント（前例 uitk-tutorial-cues の 03）
        // Game screen, then a key-control hint injected into the store (precedent uitk-tutorial-cues 03)
        p.WarpPlayer(scaffold);
        await p.WaitSeconds(1.5f);
        NoteRect(res, "challenge-hud");
        NoteRect(res, "key-hints");
        await p.Screenshot($"{res}-2-game-hud");
        var session = Guid.NewGuid();
        TutorialPresentationStateStore.Instance.BeginSession(session);
        TutorialPresentationStateStore.Instance.AddKeyControlHint(first.Tutorials[0].TutorialGuid.ToString(), "Tab", "GameScreen");
        await p.WaitSeconds(1f);
        await p.Screenshot($"{res}-2-key-control-hint");
        TutorialPresentationStateStore.Instance.EndSession(session);

        // 木の 5m 手前でピンを画面内に、真後ろを向いて画面外の矢印にする
        // From 5m short of the tree, put the pin on screen, then face away for the edge arrow
        var tree = Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SearchNearestMapObject(treeGuids, scaffold);
        var treeCenter = tree.GetComponentInChildren<Collider>(true).bounds.center;
        var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
        p.WarpPlayer(tree.transform.position - forward * 5f + Vector3.up * 0.5f);
        await p.WaitSeconds(0.5f);
        await p.AimAt(treeCenter);
        await p.Until(() => WorldPinStateStore.Instance.GetCurrent().Pins.Any(pin => pin.OnScreen), 10f, "ピンが画面内");
        await p.WaitSeconds(0.5f);
        await p.Screenshot($"{res}-2-world-pin-onscreen");
        await p.AimAt(p.PlayerPosition - (treeCenter - p.PlayerPosition));
        await p.Until(() => WorldPinStateStore.Instance.GetCurrent().Pins.Any(pin => !pin.OnScreen), 10f, "ピンが画面外");
        await p.WaitSeconds(0.5f);
        await p.Screenshot($"{res}-2-world-pin-offscreen");

        // 木へ歩いて寄り F で採掘中のゲージを出し、満ちる前に離す（原木を取らずチャレンジを進めない）
        // Walk to the tree and hold F for the gauge, releasing before it fills (no logs, the challenge stays)
        await p.AimAt(treeCenter);
        SemanticInput.KeyDown(Key.W);
        await p.Until(() => Vector3.Distance(Flat(p.PlayerPosition), Flat(tree.transform.position)) < 1.6f, 10f, "歩いて木に寄った");
        SemanticInput.KeyUp(Key.W);
        await p.AimAt(treeCenter);
        SemanticInput.KeyDown(Key.F);
        await UntilFound("progress-bar", 10f);
        await p.WaitSeconds(0.3f);
        NoteRect(res, "progress-bar");
        await p.Screenshot($"{res}-2-mining");
        SemanticInput.KeyUp(Key.F);

        // 足場で木のチェストの設置モード（2-placement）と、空へ照準して置けない所（2-placement-unavailable）
        // On the scaffold, wooden-chest placement (2-placement) and aiming at the sky where nothing places (2-placement-unavailable)
        p.WarpPlayer(scaffold);
        await p.Hotbar.AssignHotbar(0, "木のチェスト");
        await p.PressKey(Key.Digit1);
        await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
        await p.AimAtPlaceOrigin("木のチェスト", new Vector3Int(2, 32, 2));
        await p.WaitSeconds(0.5f);
        NoteRect(res, "placement-mode-hud");
        await p.Screenshot($"{res}-2-placement");
        await p.AimAt(p.PlayerPosition + Vector3.up * 40f + forward);
        await p.WaitSeconds(0.5f);
        await p.Screenshot($"{res}-2-placement-unavailable");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);

        // 枠 2 に電線を割り当てて電線ツールの設置モード
        // Assign the wire to slot 2 for the wire-tool placement mode
        await p.Hotbar.AssignHotbar(1, "電線");
        await p.PressKey(Key.Digit2);
        await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
        await p.WaitSeconds(0.5f);
        await p.Screenshot($"{res}-2-placement-connect-tool");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);

        // G で削除モード（帯と箱の高さ）
        // G enters delete mode (bands and box height)
        await p.PressKey(Key.G);
        await p.WaitUiState(UIStateEnum.DeleteBar, 10f);
        await p.WaitSeconds(0.5f);
        NoteRect(res, "delete-box-height-hud");
        await p.Screenshot($"{res}-2-delete-bar");
        await p.PressKey(Key.G);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);

        // インベントリでロックを外し、ポインタを左上へ置いて 1 行と 2 行のツールチップ（前例 uitk-mode-hud-and-tooltip の 04）
        // Unlock via the inventory, put the pointer at the top-left and show one- and two-line tooltips (precedent uitk-mode-hud-and-tooltip 04)
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);
        var tooltip = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IMouseCursorTooltip>();
        var owner = new TooltipOwner();
        SemanticInput.MouseMoveTo(new Vector2(2f, Screen.height - 2f));
        tooltip.Show(owner, LocalizationKeys.Ui.Tooltip.WorldTarget);
        await p.WaitSeconds(1f);
        NoteRect(res, "cursor-tooltip");
        await p.Screenshot($"{res}-2-tooltip");
        tooltip.Show(owner, new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain), new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceTooFar) });
        await p.WaitSeconds(1f);
        await p.Screenshot($"{res}-2-tooltip-multiline");
        tooltip.Hide(owner);
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.GameScreen, 15f);
    }

    // UITK の木の矩形（パネル座標）を記録する。Web の回は UITK の木が無いので測らない
    // Record a UITK tree rect in panel coordinates; the Web pass has no UITK tree, so skip it
    void NoteRect(string res, string name)
    {
        if (!isGameUi) return;
        var element = GameUiRoot.Instance.GetRootElement().Q(name);
        p.Note($"矩形 {res} {name} {(element == null ? "none" : element.worldBound.ToString())}");
    }

    // FindUi を await して見つかるまで回す（両系の locator が引く）
    // Await FindUi until found (the locator serves both systems)
    async UniTask UntilFound(string testid, float timeoutSeconds)
    {
        var deadline = Time.realtimeSinceStartup + timeoutSeconds;
        var found = (await PlaytestUiElementOps.FindUi(testid)).IsFound;
        for (; !found && Time.realtimeSinceStartup < deadline; found = (await PlaytestUiElementOps.FindUi(testid)).IsFound) await UniTask.DelayFrame(1);
        p.Assert(found, $"{testid} が出た");
    }

    Vector3 Flat(Vector3 position) => new(position.x, 0f, position.z);

    #endregion
});
