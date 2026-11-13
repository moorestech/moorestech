// ワールドピンを電気炉のパネルの閉じるボタンと研究の詳細に重ね、ピンが前に出るか（Web と同じ層の前後か）を Game View の実画面で 1280×720・2560×1080 に撮る（I-fix5 R-7）
// Overlays a world pin on the electric furnace panel's close button and the research detail, and shoots whether the pin draws in front (the Web layer order) on the real Game View at 1280x720 and 2560x1080 (I-fix5 R-7)
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Client.Ui.Foundation.Root;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

const string ProbePinId = "ifix5-layer-probe";
var furnace = new Vector3Int(0, 32, 0);
return PlaytestRunner.Run("uitk-worldpin-layer-compare", new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f }, async p =>
{
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen");
    Localize.TrySetChosenLanguage("japanese");
    // 地形（岩など）が照準を奪うので uitk-machine-ui と同じく地形の表示を止める
    // Terrain (rocks etc.) steals the aim, so hide it like uitk-machine-ui
    Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    var root = GameUiRoot.Instance.GetRootElement();
    var tutorialGuid = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements[0].Challenges[0].Tutorials[0].TutorialGuid.ToString();

    // 1. 電気炉を開き、閉じるボタンにピンを重ねる
    // 1. Open the electric furnace and overlay the pin on its close button
    await p.PlaceBlockDirect("電気炉", furnace, BlockDirection.North);
    await p.WaitBlockGameObject(furnace);
    p.WarpPlayer(new Vector3(furnace.x + 1.5f, 32.5f, furnace.z - 0.7f));
    await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "着地");
    SemanticInput.KeyDown(Key.LeftAlt);
    await p.AimAt(new Vector3(furnace.x + 1.5f, 32.5f, furnace.z + 1.5f));
    await p.PressInteract();
    await p.WaitUiState(UIStateEnum.SubInventory, 10f);
    SemanticInput.KeyUp(Key.LeftAlt);
    await p.UntilUiElement("block-inventory-close", 10f);
    await ShootBoth("machine-close", root.Q("block-inventory-close"), 45f);

    // 2. 閉じて研究の詳細を開き、詳細の中央にピンを重ねる
    // 2. Close, open the research detail and overlay the pin on its center
    await p.ClickUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.UntilUiElement("research-tree", 5f);
    var node = MasterHolder.ResearchMaster.GetAllResearches().First(r => r.PrevResearchNodeGuids.Length == 0 && r.ConsumeItems.Length > 0);
    await p.ClickUi($"research-node-{node.ResearchNodeGuid}");
    await p.UntilUiElement("research-detail-pane", 5f);
    await ShootBoth("research-detail", root.Q("research-detail-pane"), 0f);
    WorldPinStateStore.Instance.RemovePin(ProbePinId);

    #region Internal

    // 2 解像度で、対象の中心から tipOffsetY 下へピンの先端を置いて撮る（ラベルが対象に重なる。座標は解像度ごとに置き直す）
    // At both resolutions, put the pin tip tipOffsetY below the target's center so the label overlaps it, and shoot (re-placed per resolution)
    async UniTask ShootBoth(string name, VisualElement target, float tipOffsetY)
    {
        foreach (var (w, h) in new[] { (1280, 720), (2560, 1080) })
        {
            await PlaytestGameViewOps.SetResolution(p.CancellationToken, w, h);
            SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.03f, Screen.height * 0.5f));
            foreach (var overlay in Object.FindObjectsByType<PlaytestOverlayView>(FindObjectsSortMode.None)) overlay.gameObject.SetActive(false);
            await p.WaitSeconds(1f);
            var center = target.worldBound.center;
            WorldPinStateStore.Instance.SetPin(ProbePinId, tutorialGuid, new WorldPinProjection { ScreenX = center.x / root.layout.width, ScreenY = (center.y + tipOffsetY) / root.layout.height, OnScreen = true });
            await p.Until(() => root.Query(className: "world-pin").ToList().Any(e => e.resolvedStyle.display == DisplayStyle.Flex), 5f, "ピンが出た");
            await p.WaitSeconds(1f);
            p.Note($"{name} {w}x{h} target={target.worldBound} pin={string.Join(",", root.Query(className: "world-pin").ToList().Select(e => e.worldBound.ToString()))}");
            await p.Screenshot($"{name}-{w}x{h}");
        }
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    }

    #endregion
});
