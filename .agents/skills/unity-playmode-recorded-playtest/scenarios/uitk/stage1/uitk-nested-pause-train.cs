// 入れ子ポーズ（列車乗車中）: Esc→UITKのポーズ→設定→戻る→Escで閉じ、乗ったまま列車HUDへ戻ることを録画する（ADR 0035・R-13）
// Nested pause while riding a train: Esc -> UITK pause -> settings -> back -> Esc returns to the train HUD still riding (ADR 0035, R-13)
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Paths;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.UnlockState;
using MessagePack;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using TrainCarEntityObject = Client.Game.InGame.Train.View.Object.Core.TrainCarEntityObject;
using Client.Playtest.Input;
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-nested-pause-train", options, async p =>
{
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;

    // 列車をレールへ置いて乗る
    // Place a train on rails and ride it
    p.Note("列車をレールへ配置し、実プレイヤー操作で乗車する");
    var car = await PrepareAndRideTrain();
    var trainHud = resolver.Resolve<UIStateDictionary>().GetState(UIStateEnum.TrainHUDScreen) as TrainHUDScreenState;
    await p.Until(() => p.CurrentUiState == UIStateEnum.TrainHUDScreen && trainHud.IsRiding, 15f, "列車HUDで乗車済み");
    await p.Until(() => PlaytestUiElementOps.FindUi("train-riding-hud").GetAwaiter().GetResult().IsFound, 10f, "train-riding-hud が出た");
    await p.Screenshot("00-riding");

    // Escで入れ子ポーズ（UITK）を開く
    // Open the nested pause (UITK) with Esc
    p.Note("列車HUD: Escで入れ子ポーズ（UITK）");
    await p.PressKey(Key.Escape);
    await p.Until(() => trainHud.SubState == NestedPauseSubStateEnum.PauseMenuScreen, 15f, "列車HUDの入れ子ポーズ表示");
    await p.UntilUiElement("pause-menu", 10f);
    p.Assert(p.CurrentUiState == UIStateEnum.TrainHUDScreen, "入れ子ポーズ中も外側はTrainHUDScreen");
    await p.Screenshot("01-train-pause-top");

    // 設定へ進み、戻るでトップへ
    // Go to settings and back to the top
    await p.ClickUi("pause-menu-open-settings");
    await p.UntilUiElement("language-select", 10f);
    await p.Screenshot("02-train-pause-settings");
    await p.ClickUi("pause-menu-back");
    await p.UntilUiElement("pause-menu-open-settings", 10f);
    p.Assert(trainHud.SubState == NestedPauseSubStateEnum.PauseMenuScreen, "設定から戻っても入れ子ポーズが開いている");

    // Escで閉じて乗ったまま列車HUDへ
    // Close with Esc and return to the train HUD still riding
    await p.PressKey(Key.Escape);
    await p.Until(() => trainHud.SubState == NestedPauseSubStateEnum.GameScreen, 15f, "入れ子ポーズを閉じる");
    await p.WaitSeconds(0.5f);
    var closed = await PlaytestUiElementOps.FindUi("pause-menu");
    p.Assert(!closed.IsFound || !closed.Visible, "閉じた後は pause-menu が見えない");
    p.Assert(p.CurrentUiState == UIStateEnum.TrainHUDScreen && trainHud.IsRiding, "列車に乗ったまま列車HUDへ戻る");
    await p.Screenshot("03-train-hud-restored");

    #region Internal
    async UniTask<TrainCarEntityObject> PrepareAndRideTrain()
    {
        var a = new Vector3Int(10, 32, 6); var b = new Vector3Int(10, 32, 22);
        await PlacePier(a); await PlacePier(b);
        await p.WaitBlockGameObject(a); await p.WaitBlockGameObject(b);
        var railA = p.GetBlock(a).GetComponent<RailComponent>(); var railB = p.GetBlock(b).GetComponent<RailComponent>();
        var master = MasterHolder.TrainUnitMaster.Train.TrainCars.First(trainCar => trainCar.Length <= 7);
        await p.RunOnServer("レール2本を接続して列車を解放する", () =>
        {
            railA.FrontNode.ConnectNode(railB.FrontNode); railB.BackNode.ConnectNode(railA.BackNode);
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockTrainCar(master.TrainCarGuid);
        });
        foreach (var required in master.RequiredItems) await p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(required.ItemGuid).Name, required.Count);
        var position = new RailPosition(new List<IRailNode> { railA.BackNode, railB.BackNode }, TrainLengthConverter.ToRailUnits(master.Length), 0);
        var response = await ClientContext.VanillaApi.Response.Train.PlaceTrainOnRail(position, master.TrainCarGuid, CancellationToken.None);
        p.Assert(response != null && response.Success, "乗車用の列車を本番プロトコルで配置できる");
        TrainCarEntityObject Spawned() => Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).FirstOrDefault();
        await p.Until(() => Spawned() != null, 15f, "乗車用列車のクライアント表示");
        var spawned = Spawned(); var collider = spawned.GetComponentInChildren<Collider>();
        p.WarpPlayer(collider.bounds.center + new Vector3(0f, 0f, -2f));
        await p.AimAt(collider.bounds.center); await p.PressRide();
        return spawned;
    }
    async UniTask PlacePier(Vector3Int position)
    {
        var blockId = PlaytestBlockOps.ResolveBlockId("レール橋脚");
        var detail = new RailBridgePierComponentStateDetail(RailComponent.ToVector3(BlockDirection.North));
        var parameters = new[] { new BlockCreateParam(RailBridgePierComponentStateDetail.StateDetailKey, MessagePackSerializer.Serialize(detail)) };
        var placed = false;
        await p.RunOnServer($"レール橋脚を配置 {position}", () => placed = ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, BlockDirection.North, parameters, out _));
        p.Assert(placed, $"レール橋脚を配置 {position}");
    }
    #endregion
});
