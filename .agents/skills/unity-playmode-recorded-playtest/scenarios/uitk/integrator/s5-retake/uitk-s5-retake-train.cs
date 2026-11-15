// S5-crosscut T19 の撮り直し（列車族）: 貨物プラットフォーム・機関車の持ち物タブ（空と物あり）と、乗車中の列車 HUD に背景の会話の字幕を出したままを日本語で 1280×720 と 2560×1080 に撮る（S5-ladder）
// S5-crosscut T19 retake (train family): shoot the cargo platform, the locomotive inventory tab (empty and filled) and the riding train HUD with a background-skit subtitle in Japanese at 1280x720 and 2560x1080 (S5-ladder)
// 実行は stage5/driver/winunity-scenario-run.sh <branch> <GameUi|Web> normal <この .cs> <name> から。Web の系は R4 の列車 HUD＋字幕だけを撮る（ブロック画面の段を飛ばす）
// Run via stage5/driver/winunity-scenario-run.sh <branch> <GameUi|Web> normal <this .cs> <name>; on Web only the R4 train HUD with subtitle is shot (block-screen steps are skipped)
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Context;
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
using Client.Skit.UI;
using Client.Ui.Foundation.Root;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Game.UnlockState;
using MessagePack;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using TrainCarEntityObject = Client.Game.InGame.Train.View.Object.Core.TrainCarEntityObject;

return PlaytestRunner.Run("uitk-s5-retake-train", new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f }, async p =>
{
    // 1. 系の記録・入力・平地・日本語（Windows の PlayerPrefs は前の録画の言語が残るので撮る前に当てる）
    // 1. Record the system, input, flat ground and Japanese (Windows PlayerPrefs keep the last run's language, so apply it before shooting)
    var system = UiInputExclusivity.ActiveUiSystem;
    var shotPrefix = system == UiSystem.GameUi ? "uitk" : "web-live";
    p.Note($"系={system}");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen");
    Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    var languageBefore = Localize.GetCurrentLanguageCode();
    p.Assert(Localize.TrySetChosenLanguage("japanese"), "撮影の言語 japanese を当てられる");
    var backgroundSkit = Object.FindFirstObjectByType<Client.Game.InGame.BackgroundSkit.BackgroundSkitManager>(FindObjectsInactive.Include);
    await p.Until(() => !backgroundSkit.IsPlayingSkit, 120f, "背景スキットが終わる");
    foreach (var challenge in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements.SelectMany(c => c.Challenges)) TutorialPresentationStateStore.Instance.EndSession(challenge.ChallengeGuid);
    var overlay = Object.FindFirstObjectByType<PlaytestOverlayView>();
    overlay.enabled = false;
    try
    {
        // 2. 橋脚 2 本のレールに機関車を置く（前例 stage1/uitk-nested-pause-train・stage4/uitk-train-inventory）
        // 2. Put a locomotive on a two-pier rail (precedents stage1/uitk-nested-pause-train, stage4/uitk-train-inventory)
        var train = await PlaceLocomotive();

        // 3. GameUi だけ: 貨物プラットフォームと、機関車の持ち物タブ（空 → 物あり）
        // 3. GameUi only: the cargo platform and the locomotive inventory tab (empty, then filled)
        if (system == UiSystem.GameUi)
        {
            var platformPos = new Vector3Int(-20, 32, -20);
            await p.PlaceBlockDirect("貨物プラットフォーム", platformPos, BlockDirection.North);
            var platformObject = await p.WaitBlockGameObject(platformPos);
            await p.Until(() => !platformObject.IsPlayingPlaceAnimation, 30f, "貨物プラットフォームの設置演出");
            p.WarpPlayer(new Vector3(platformPos.x + 3.5f, 32.5f, platformPos.z - 0.7f));
            await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "貨物プラットフォームの前に着地");
            SemanticInput.KeyDown(Key.LeftAlt);
            await p.AimAt(new Vector3(platformPos.x + 3.5f, platformPos.y + 0.4f, platformPos.z + 1.5f));
            await p.PressInteract();
            await p.WaitUiState(UIStateEnum.SubInventory, 10f);
            SemanticInput.KeyUp(Key.LeftAlt);
            await p.Until(() => Shown("train-platform-view"), 15f, "train-platform-view が出る");
            await ShootBoth("4-block-trainPlatform");
            await Close();

            await AimAtCar();
            await p.PressInteract();
            await p.WaitUiState(UIStateEnum.SubInventory, 10f);
            await p.Until(() => Shown("train-inventory-body"), 15f, "train-inventory-body が出る");
            p.Assert(GameUiRoot.Instance.GetRootElement().Q<VisualElement>("train-tab-inventory").ClassListContains("mode-switch__option--selected"), "持ち物タブが選ばれている");
            await ShootBoth("4-block-trainTimetable");
            await p.RunOnServer("機関車の枠 0・1 に物を入れる", () => { var container = (ItemTrainCarContainer)train.Cars[0].Container; container.SetItem(0, PlaytestItemOps.ResolveItemId("原木"), 24); container.SetItem(1, PlaytestItemOps.ResolveItemId("石"), 8); });
            await p.Until(() => GameUiRoot.Instance.GetRootElement().Q<VisualElement>("train-inventory-slots-slot-1")?.Query<Label>().ToList().Any(l => l.text.Contains("8")) == true, 10f, "枠 1 に 8 個が出る");
            await ShootBoth("4-block-train");
            await Close();
        }

        // 4. 乗車して列車 HUD を出し、背景の会話の字幕を出したまま撮る（R4。両系）
        // 4. Ride, show the train HUD and shoot it with a background-skit subtitle up (R4; both systems)
        await AimAtCar();
        await p.PressRide();
        await p.Until(() => p.CurrentUiState == UIStateEnum.TrainHUDScreen, 15f, "列車 HUD の状態");
        if (system == UiSystem.GameUi) await p.Until(() => Shown("train-riding-hud"), 10f, "train-riding-hud が出る");
        else await p.UntilUiElement("train-riding-hud", 30f);
        var store = SkitPresentationStateStore.Instance;
        store.BeginBackground();
        store.SetBackgroundText("ムーア", "列車の旅は順調だ。次の駅で荷物を降ろそう");
        await p.WaitSeconds(1.5f);
        await ShootBoth("2-train-hud");
        store.End();
    }
    finally
    {
        // 撮影の前の言語へ戻す
        // Restore the pre-shoot language
        overlay.enabled = true;
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
        var restored = Localize.TrySetChosenLanguage(languageBefore);
        p.Note($"撮影の前の言語={languageBefore}（戻せた={restored}）");
        p.Assert(restored, $"撮影の前の言語 {languageBefore} へ戻せる");
    }

    #region Internal

    async UniTask<TrainUnit> PlaceLocomotive()
    {
        // 機関車（長さ 20）が載るよう橋脚の間を 44 空ける
        // Space the piers 44 apart so the locomotive (length 20) fits
        var (a, b) = (new Vector3Int(10, 32, 0), new Vector3Int(10, 32, 44));
        foreach (var position in new[] { a, b })
        {
            var detail = new RailBridgePierComponentStateDetail(RailComponent.ToVector3(BlockDirection.North));
            var parameters = new[] { new BlockCreateParam(RailBridgePierComponentStateDetail.StateDetailKey, MessagePackSerializer.Serialize(detail)) };
            var placed = false;
            await p.RunOnServer($"レール橋脚を配置 {position}", () => placed = ServerContext.WorldBlockDatastore.TryAddBlock(PlaytestBlockOps.ResolveBlockId("レール橋脚"), position, BlockDirection.North, parameters, out _));
            p.Assert(placed, $"レール橋脚を配置 {position}");
            await p.WaitBlockGameObject(position);
        }
        var (railA, railB) = (p.GetBlock(a).GetComponent<RailComponent>(), p.GetBlock(b).GetComponent<RailComponent>());
        var master = MasterHolder.TrainUnitMaster.Train.TrainCars.First(car => 0 < car.TractionForce);
        await p.RunOnServer("レール 2 本を接続して機関車を解放する", () =>
        {
            railA.FrontNode.ConnectNode(railB.FrontNode); railB.BackNode.ConnectNode(railA.BackNode);
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockTrainCar(master.TrainCarGuid);
        });
        foreach (var required in master.RequiredItems) await p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(required.ItemGuid).Name, required.Count);
        var railPosition = new RailPosition(new List<IRailNode> { railA.BackNode, railB.BackNode }, TrainLengthConverter.ToRailUnits(master.Length), 0);
        var response = await ClientContext.VanillaApi.Response.Train.PlaceTrainOnRail(railPosition, master.TrainCarGuid, CancellationToken.None);
        p.Assert(response != null && response.Success, "機関車を本番プロトコルで配置できる");
        await p.Until(() => SpawnedCar() != null, 15f, "機関車のクライアント表示");
        TrainUnit train = null;
        await p.RunOnServer("列車を取得する", () => train = ServerContext.GetService<ITrainUnitLookupDatastore>().GetRegisteredTrains().First());
        return train;
    }

    // 後ろの車体の後端を狙う（前例 uitk-train-inventory の AimAtCar）
    // Aim at the rear end of the car body (precedent AimAtCar in uitk-train-inventory)
    async UniTask AimAtCar()
    {
        Collider rear = null;
        foreach (var collider in SpawnedCar().GetComponentsInChildren<Collider>(true)) if (rear == null || collider.bounds.min.z < rear.bounds.min.z) rear = collider;
        var bounds = rear.bounds;
        p.WarpPlayer(new Vector3(bounds.center.x, 33.5f, bounds.min.z - 1.2f));
        await p.WaitSeconds(1f);
        await p.AimAt(new Vector3(bounds.center.x, bounds.max.y - 0.3f, bounds.min.z + 0.3f));
    }

    TrainCarEntityObject SpawnedCar() => Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).FirstOrDefault();

    async UniTask Close()
    {
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    async UniTask ShootBoth(string stateId)
    {
        SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.5f, Screen.height * 0.97f));
        foreach (var (w, h) in new[] { (1280, 720), (2560, 1080) })
        {
            await PlaytestGameViewOps.SetResolution(p.CancellationToken, w, h);
            await UniTask.DelayFrame(3);
            p.Assert(Screen.width == w && Screen.height == h, $"Screen が {w}x{h} (実際 {Screen.width}x{Screen.height})");
            await p.WaitSeconds(system == UiSystem.GameUi ? 0.5f : 2f);
            await p.Screenshot($"{shotPrefix}-{stateId}-{w}x{h}");
        }
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    }

    bool Shown(string testid)
    {
        var element = GameUiRoot.Instance.GetRootElement().Q<VisualElement>(testid);
        if (element == null || element.panel == null) return false;
        for (var e = element; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
        return true;
    }

    #endregion
});
