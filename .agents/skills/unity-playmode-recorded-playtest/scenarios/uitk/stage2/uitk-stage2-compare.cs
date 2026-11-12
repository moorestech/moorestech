// 束 2 の並べ比べ（組 2）: 同じ実ゲームの状態を GameUi と Web の両系で作り参照と同じ撮影 id で 1280×720 と 2560×1080 に撮る。winunity play <branch> <this> --ui-system GameUi|Web で流し、UITK の木の照会は GameUi の回だけ。2-train-hud は乗車で分岐 1 しか出ないので撮らず、通知とスキットは uitk-notification-compare・uitk-skit-compare が受け持つ
// Bundle-2 comparison (group 2): build the same real game states on GameUi and Web and shoot them under the reference ids at 1280x720 and 2560x1080; run with winunity play <branch> <this> --ui-system GameUi|Web, UITK tree queries only on the GameUi pass. 2-train-hud is skipped (riding shows only one branch); notifications and skits belong to uitk-notification-compare and uitk-skit-compare
using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
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
using UnityEngine.UIElements;
using VContainer;
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
    // 参照は日本語で撮る。他のシナリオ（uitk-notification-compare）が Editor の言語を英語で残すので、撮る前に固定する
    // References are in Japanese; another scenario (uitk-notification-compare) leaves the Editor in English, so pin it before shooting
    Localize.TrySetChosenLanguage("japanese");
    p.Assert(Localize.GetCurrentLanguageCode() == "japanese", "言語が日本語");
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
    // 足場の近くの木が読み込まれるまで待ち、両解像度で同じ木を使う（早いと 260m 先の木を引き、足場の周りも未描画で系ごとに写りが変わる）
    // Wait for a tree near the scaffold to load and reuse it at both resolutions (too early picks one 260m away and the scaffold area is unbuilt, varying per run)
    p.WarpPlayer(scaffold);
    var datastore = Object.FindFirstObjectByType<MapObjectGameObjectDatastore>();
    await p.Until(() => datastore.SearchNearestMapObject(treeGuids, scaffold) is { } near && Vector3.Distance(near.transform.position, scaffold) < 20f, 60f, "足場の近くの木が読み込まれた");
    var tree = datastore.SearchNearestMapObject(treeGuids, scaffold);
    var treeCenter = tree.GetComponentInChildren<Collider>(true).bounds.center;
    var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
    if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
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
        foreach (var name in new[] { "challenge-hud", "key-hints" }) NoteRect(res, name);
        await p.Screenshot($"{res}-2-game-hud");
        var session = Guid.NewGuid();
        TutorialPresentationStateStore.Instance.BeginSession(session);
        TutorialPresentationStateStore.Instance.AddKeyControlHint(first.Tutorials[0].TutorialGuid.ToString(), "Tab", "GameScreen");
        await UntilShown("key-control-hint-hud", "Tab");
        await p.Screenshot($"{res}-2-key-control-hint");
        TutorialPresentationStateStore.Instance.EndSession(session);
        // 木の 5m 手前でピンを画面内に、木を越えた先へ飛んで（木が背後）画面外の矢印にする
        // From 5m short of the tree, put the pin on screen, then warp past it (tree behind) for the edge arrow
        p.WarpPlayer(tree.transform.position - forward * 5f + Vector3.up * 0.5f);
        await p.WaitSeconds(0.5f);
        await p.AimAt(treeCenter);
        await p.Until(() => WorldPinStateStore.Instance.GetCurrent().Pins.Any(pin => pin.OnScreen), 10f, "ピンが画面内");
        await p.WaitSeconds(0.5f);
        await UntilShown($"world-pin-{PinId()}", "");
        await p.Screenshot($"{res}-2-world-pin-onscreen");
        p.WarpPlayer(tree.transform.position + forward * 6f + Vector3.up * 0.5f);
        await p.Until(() => WorldPinStateStore.Instance.GetCurrent().Pins.Any(pin => !pin.OnScreen), 10f, "ピンが画面外");
        // store の判定が先に変わり Web は橋と CEF を通って遅れて描くので、落ち着かせてから両系の矢印の要素を確かめて撮る
        // The store flips first and Web draws later via the bridge and CEF, so let it settle and confirm the arrow element on both systems
        await p.WaitSeconds(0.5f);
        await UntilShown($"world-pin-arrow-{PinId()}", "");
        await p.Screenshot($"{res}-2-world-pin-offscreen");
        // 木へ歩いて寄り F で採掘中のゲージを出し、満ちる前に離す（原木を取らずチャレンジを進めない）
        // Walk to the tree and hold F for the gauge, releasing before it fills (no logs, the challenge stays)
        p.WarpPlayer(tree.transform.position - forward * 5f + Vector3.up * 0.5f);
        await p.AimAt(treeCenter);
        SemanticInput.KeyDown(Key.W);
        await p.Until(() => Vector2.Distance(new Vector2(p.PlayerPosition.x, p.PlayerPosition.z), new Vector2(tree.transform.position.x, tree.transform.position.z)) < 1.6f, 10f, "歩いて木に寄った");
        SemanticInput.KeyUp(Key.W);
        await p.AimAt(treeCenter + Vector3.down * 1.5f);
        SemanticInput.KeyDown(Key.F);
        await UntilShown("progress-bar", "");
        await p.WaitSeconds(0.3f);
        NoteRect(res, "progress-bar");
        await p.Screenshot($"{res}-2-mining");
        SemanticInput.KeyUp(Key.F);
        // 足場で木のチェストの設置モード（2-placement）と、届かない遠くの地面へ照準して置けない所（2-placement-unavailable）
        // On the scaffold, wooden-chest placement (2-placement) and aiming at far ground out of reach (2-placement-unavailable)
        p.WarpPlayer(scaffold);
        await p.Hotbar.AssignHotbar(0, "木のチェスト");
        await p.PressKey(Key.Digit1);
        await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
        await p.AimAtPlaceOrigin("木のチェスト", new Vector3Int(2, 32, 2));
        await UntilShown("placement-mode-hud", "木のチェスト");
        NoteRect(res, "placement-mode-hud");
        await p.Screenshot($"{res}-2-placement");
        await p.AimAt(scaffold + forward * 40f + Vector3.down * 2f);
        await UntilShown("cursor-tooltip", Localize.Get(LocalizationKeys.Ui.Tooltip.PlaceTooFar));
        await p.Screenshot($"{res}-2-placement-unavailable");
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        // 枠 2 に電線を割り当てて電線ツールの設置モード
        // Assign the wire to slot 2 for the wire-tool placement mode
        await p.Hotbar.AssignHotbar(1, "電線");
        await p.PressKey(Key.Digit2);
        await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
        await UntilShown("placement-mode-hud", "電線");
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
        await UntilShown("cursor-tooltip", Localize.Get(LocalizationKeys.Ui.Tooltip.WorldTarget));
        // Web は文言が先に届きポインタの位置が遅れて CEF へ着くので、ツールチップが左上のポインタの所へ来るまで待つ
        // On Web the text lands before the pointer position reaches CEF, so wait until the tooltip sits at the top-left pointer
        var tooltipDeadline = Time.realtimeSinceStartup + 10f;
        for (var tip = await PlaytestUiElementOps.FindUi("cursor-tooltip"); Screen.width / 4f < tip.ScreenRect.center.x && Time.realtimeSinceStartup < tooltipDeadline; tip = await PlaytestUiElementOps.FindUi("cursor-tooltip")) await UniTask.DelayFrame(1);
        p.Assert((await PlaytestUiElementOps.FindUi("cursor-tooltip")).ScreenRect.center.x < Screen.width / 4f, "ツールチップが左上のポインタの所");
        NoteRect(res, "cursor-tooltip");
        await p.Screenshot($"{res}-2-tooltip");
        tooltip.Show(owner, new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBlockedByTerrain), new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceTooFar) });
        await UntilShown("cursor-tooltip", Localize.Get(LocalizationKeys.Ui.Tooltip.PlaceTooFar));
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
    // 両系の locator で見えて文言を含むまで待つ。出なければ例外で落ち、状態の出来ていない画像を撮らない
    // Wait until visible with the text on both locators; a timeout throws so no shot is taken of an unbuilt state
    async UniTask UntilShown(string testid, string text)
    {
        await PlaytestUiElementOps.WaitUiTextContains(testid, text, 10f);
        p.Note($"表示を確認 {testid} 「{text}」");
    }
    string PinId() => WorldPinStateStore.Instance.GetCurrent().Pins.Single().PinId;
    #endregion
});
