// post-visual の並べ比べ（モード HUD・チュートリアルの印・採掘・通知・ツールチップ・BP 名のモーダル）: 前例 uitk-stage2-compare の手順を両側で通る API に直して 1280×720 で撮る
// post-visual comparison (mode HUD, tutorial cues, mining, notifications, tooltip, blueprint-name modal): the uitk-stage2-compare steps rewritten with APIs present on both sides, at 1280x720
// 地形の木を使うので地形は隠さない。master と feature/uitk の両方でコンパイルできる API だけを使う
// Uses the terrain trees, so terrain stays visible; only APIs that compile on both master and feature/uitk
using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using System.Reflection;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Overlay;
using Client.Playtest.Operations.Ui;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.ChallengesModule;
using VContainer;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f };
return PlaytestRunner.Run("pv-mode", options, async p =>
{
    // 1. 入力・言語・平地を両側で同じにする
    // 1. Align input, language and flat ground on both sides
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);
    p.Assert(Localize.TrySetChosenLanguage("japanese"), "言語 japanese を当てられる");
    await SetResolution720();
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    var backgroundSkit = Object.FindFirstObjectByType<BackgroundSkitManager>(FindObjectsInactive.Include);
    await p.Until(() => !backgroundSkit.IsPlayingSkit, 120f, "背景スキットが終わる");
    await p.WaitSeconds(2f);
    await p.Hotbar.UnlockConnectTool("電線");
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;

    // 2. 先頭チャレンジのピン先の木を足場の近くで探す（前例 uitk-stage2-compare）
    // 2. Find the tree pinned by the first challenge near the scaffold (precedent uitk-stage2-compare)
    var first = MasterHolder.ChallengeMaster.GetCategoryInitialChallenges(MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements.Single(c => c.InitialUnlocked).CategoryGuid).Single();
    var pinTutorial = first.Tutorials.Single(t => t.TutorialType == TutorialsElement.TutorialTypeConst.mapObjectPin);
    var treeGuids = MasterHolder.ChallengeMaster.ResolvePinTargets((MapObjectPinTutorialParam)pinTutorial.TutorialParam);
    var scaffold = new Vector3(6f, 33.5f, -5f);
    p.WarpPlayer(scaffold);
    var datastore = Object.FindFirstObjectByType<MapObjectGameObjectDatastore>();
    await p.Until(() => datastore.SearchNearestMapObject(treeGuids, scaffold) is { } near && Vector3.Distance(near.transform.position, scaffold) < 20f, 60f, "足場の近くの木が読み込まれた");
    var tree = datastore.SearchNearestMapObject(treeGuids, scaffold);
    var treeCenter = tree.GetComponentInChildren<Collider>(true).bounds.center;
    var forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
    if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;

    // 3. store へ差したキー操作ヒント
    // 3. Key-control hint injected into the store
    await p.WaitSeconds(1.5f);
    var session = Guid.NewGuid();
    TutorialPresentationStateStore.Instance.BeginSession(session);
    TutorialPresentationStateStore.Instance.AddKeyControlHint(first.Tutorials[0].TutorialGuid.ToString(), "Tab", "GameScreen");
    await p.WaitSeconds(1.5f);
    await Shoot("2-key-control-hint", true);
    TutorialPresentationStateStore.Instance.EndSession(session);

    // 4. 木の 5m 手前でピンを画面内に、木を越えて画面外の矢印に
    // 4. Pin on screen from 5m short of the tree, then warp past it for the edge arrow
    p.WarpPlayer(tree.transform.position - forward * 5f + Vector3.up * 0.5f);
    await p.WaitSeconds(0.5f);
    await p.AimAt(treeCenter);
    await p.Until(() => WorldPinStateStore.Instance.GetCurrent().Pins.Any(pin => pin.OnScreen), 10f, "ピンが画面内");
    await p.WaitSeconds(1f);
    await Shoot("2-world-pin-onscreen", false);
    p.WarpPlayer(tree.transform.position + forward * 6f + Vector3.up * 0.5f);
    await p.Until(() => WorldPinStateStore.Instance.GetCurrent().Pins.Any(pin => !pin.OnScreen), 10f, "ピンが画面外");
    await p.WaitSeconds(1f);
    await Shoot("2-world-pin-offscreen", false);

    // 5. 木へ歩いて寄り F を押し続けて採掘のゲージ（満ちる前に離す）
    // 5. Walk to the tree and hold F for the mining gauge (release before it fills)
    p.WarpPlayer(tree.transform.position - forward * 5f + Vector3.up * 0.5f);
    await p.AimAt(treeCenter);
    SemanticInput.KeyDown(Key.W);
    await p.Until(() => Vector2.Distance(new Vector2(p.PlayerPosition.x, p.PlayerPosition.z), new Vector2(tree.transform.position.x, tree.transform.position.z)) < 1.6f, 10f, "歩いて木に寄った");
    SemanticInput.KeyUp(Key.W);
    await p.AimAt(treeCenter + Vector3.down * 1.5f);
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(0.8f);
    await Shoot("2-mining", false);
    SemanticInput.KeyUp(Key.F);

    // 6. 足場で木のチェストを持ち、届かない遠くを狙った置けない状態。枠 2 の電線で接続ツールの配置 HUD
    // 6. On the scaffold hold the chest and aim far out of reach; then the wire in slot 2 for the connect-tool HUD
    p.WarpPlayer(scaffold);
    await p.Hotbar.AssignHotbar(0, "木のチェスト");
    await p.PressKey(Key.Digit1);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
    await p.AimAt(scaffold + forward * 40f + Vector3.down * 2f);
    await p.WaitSeconds(1f);
    await Shoot("2-placement-unavailable", false);
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.Hotbar.AssignHotbar(1, "電線");
    await p.PressKey(Key.Digit2);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 10f);
    await p.WaitSeconds(1f);
    await Shoot("2-placement-connect-tool", true);
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 7. 持ち物を開き、左上のポインタに 2 行のツールチップ
    // 7. Open the inventory and show a two-line tooltip at the top-left pointer
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);
    var (tooltip, owner) = (resolver.Resolve<IMouseCursorTooltip>(), new TooltipOwner());
    SemanticInput.MouseMoveTo(new Vector2(2f, Screen.height - 2f));
    tooltip.Show(owner, new[] { PlacementFeedback.BlockedByTerrainLine(), PlacementFeedback.TooFarLine() });
    await p.WaitSeconds(1.5f);
    await Shoot("2-tooltip-multiline", false);
    tooltip.Hide(owner);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);

    // 8. give のコマンド経路で 3 種を受け取り、1 秒後の獲得通知
    // 8. Receive three kinds via the give command and shoot the pickup notifications 1s later
    foreach (var (name, count) in new[] { ("原木", 5), ("石", 3), ("木の板", 2) }) await p.GiveItem(name, count);
    await p.WaitSeconds(1f);
    await Shoot("2-notifications", true);

    // 9. BP コピーで範囲を打ち、名前のモーダル（前例 uitk-stage3-compare）
    // 9. Mark a range with blueprint copy for the name modal (precedent uitk-stage3-compare)
    await p.Hotbar.UnlockBlueprint();
    await p.PlaceBlockDirect("木のチェスト", new Vector3Int(2, 32, 2), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 2));
    await p.WaitSeconds(6f);
    p.WarpPlayer(new Vector3(6f, 33.5f, -5f));
    var checks = new BlueprintCopyPasteScenarioChecks(p, resolver.Resolve<MouseCursorTooltipState>());
    await checks.SelectEntry("build-menu-category-d1000000-0000-4000-8000-000000000009", "build-menu-entry-blueprintCopy-88dd687d-aceb-4aeb-94e8-44be1e1f5a0d");
    await p.AimAt(new Vector3(0.5f, 32f, 0.5f));
    await p.ClickPlace();
    await p.AimAt(new Vector3(4.5f, 32f, 4.5f));
    await p.ClickPlace();
    await p.UntilUiElement("modal-input", 10f);
    await p.WaitSeconds(1f);
    await Shoot("3-modal", true);
    await p.PressKey(Key.Escape);

    #region Internal

    // 操作オーバーレイを消し、カーソルを外して撮る（moveCursor=false は照準中なので動かさない）
    // Hide the operation overlay, move the cursor away and shoot (moveCursor=false keeps the aim)
    async UniTask Shoot(string shotId, bool moveCursor)
    {
        foreach (var overlay in Object.FindObjectsByType<PlaytestOverlayView>(FindObjectsSortMode.None)) overlay.gameObject.SetActive(false);
        if (moveCursor) SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.03f, Screen.height * 0.5f));
        await p.WaitSeconds(0.8f);
        p.Assert(Screen.width == 1280 && Screen.height == 720, $"{shotId}: Screen が 1280x720 (実際 {Screen.width}x{Screen.height})");
        await p.Screenshot(shotId);
    }

    // Game View を 1280×720 の固定解像度にする（feature/uitk の GameViewResolution と同じ反射。master に無いので写す）
    // Set the Game View to fixed 1280x720 (same reflection as feature/uitk GameViewResolution, copied since master lacks it)
    async UniTask SetResolution720()
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var asm = typeof(UnityEditor.Editor).Assembly;
        var (sizesType, sizeType) = (asm.GetType("UnityEditor.GameViewSizes"), asm.GetType("UnityEditor.GameViewSize"));
        var (kindType, gameViewType) = (asm.GetType("UnityEditor.GameViewSizeType"), asm.GetType("UnityEditor.GameView"));
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy).GetValue(null);
        var group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new object[] { (int)sizesType.GetProperty("currentGroupType", flags).GetValue(sizes) });
        var total = (int)group.GetType().GetMethod("GetTotalCount", flags).Invoke(group, null);
        var index = -1;
        for (var i = 0; i < total && index < 0; i++)
        {
            var size = group.GetType().GetMethod("GetGameViewSize", flags).Invoke(group, new object[] { i });
            if ((int)sizeType.GetProperty("width", flags).GetValue(size) == 1280 && (int)sizeType.GetProperty("height", flags).GetValue(size) == 720 && sizeType.GetProperty("sizeType", flags).GetValue(size).ToString() == "FixedResolution") index = i;
        }
        if (index < 0)
        {
            var newSize = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) }).Invoke(new object[] { Enum.Parse(kindType, "FixedResolution"), 1280, 720, "1280x720" });
            group.GetType().GetMethod("AddCustomSize", flags).Invoke(group, new[] { newSize });
            index = total;
        }
        var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
        gameViewType.GetMethod("SizeSelectionCallback", flags).Invoke(gameView, new object[] { index, null });
        await p.Until(() => Screen.width == 1280 && Screen.height == 720, 10f, "Screen が 1280x720 になる");
        await p.WaitSeconds(1f);
    }

    #endregion
});
