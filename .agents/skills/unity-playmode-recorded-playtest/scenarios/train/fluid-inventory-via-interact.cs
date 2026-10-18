using System;
using System.Collections.Generic;
using System.Linq;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.WebUi;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.TrainRail;
using Game.Block.Blocks.TrainRail.ContainerComponents;
using Game.Fluid;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Diagram;
using Game.Train.Event;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

// 録画開始前に注入デバイスと単一キーを確認する
// Verify injection devices and a single key before recording starts
InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
SemanticInput.EnsureDevices();
InputSystem.EnableDevice(Keyboard.current);
InputSystem.EnableDevice(Mouse.current);
await UniTask.DelayFrame(3);
Debug.Log($"Fluid input probe focus={Application.isFocused} background={InputSystem.settings.backgroundBehavior} keyboard={Keyboard.current.enabled} mouse={Mouse.current.enabled}");
if (!Keyboard.current.enabled || !Mouse.current.enabled) throw new InvalidOperationException("Input devices disabled");
SemanticInput.KeyDown(Key.LeftAlt);
await UniTask.DelayFrame(3);
if (!Keyboard.current.leftAltKey.isPressed) throw new InvalidOperationException("Alt input probe failed");
SemanticInput.KeyUp(Key.LeftAlt);
await UniTask.DelayFrame(3);
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("fluid-inventory-via-interact", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.WaitUiState(UIStateEnum.GameScreen,60);
    var water = MasterHolder.FluidMaster.GetFluidId(Guid.Parse("9eae6979-d56a-4991-9107-b8161acec430"));
    var tankPosition = new Vector3Int(2,32,2);
    var platformPosition = new Vector3Int(15,32,2);

    // タンクを通常の照準とF操作で開く
    // Open the tank through normal aiming and F interaction
    p.Note("タンクに水40を準備し、照準→Fで開く");
    p.PlaceBlockDirect("液体タンク",tankPosition,BlockDirection.North);
    p.GetBlock(tankPosition).GetComponent<FluidPipeComponent>().AddLiquid(new FluidStack(40,water),default);
    var tankView = await p.WaitBlockGameObject(tankPosition);
    await Open(tankView.GetComponentsInChildren<Collider>().First(c => c.enabled));
    await CheckFluid("fluid-pipe-fluid-slots","40 / 5,000","タンク");
    await p.Screenshot("01-tank-water40");
    await p.WaitSeconds(1f);
    await Close();

    // PF表示と切替を確認してから液体を退避する
    // Verify platform display and controls before reserving its fluid
    p.Note("液体PFに水80を準備し、Fで開いて表示を確認する");
    p.PlaceBlockDirect("液体プラットフォーム",platformPosition,BlockDirection.North);
    var platform = p.GetBlock(platformPosition);
    var pf = platform.GetComponent<TrainPlatformFluidContainerComponent>();
    pf.AddLiquid(new FluidStack(80,water),default);
    var platformView = await p.WaitBlockGameObject(platformPosition);
    await Open(platformView.GetComponentsInChildren<Collider>().First(c => c.enabled));
    await CheckFluid("train-platform-fluid-slots","80 / 100","液体PF");
    var platformDom = await PlaytestDomQuery.Query("train-platform-mode",5);
    p.Assert(platformDom.Found && platformDom.Text.Contains("積込") && platformDom.Text.Contains("卸し"),"PF積込/卸し切替を表示");
    p.Note("PF画面="+platformDom.Text);
    await p.Screenshot("02-platform-water80");
    await p.WaitSeconds(1f);
    await Close();
    var reserve = new FluidContainer(100);
    p.Assert(TrainPlatformFluidTransfer.Transfer(pf.Container.Container,reserve)==80,"準備用にPF水80を退避");

    // 貨車を空のPFへドッキングし初期在庫を開く
    // Dock the car at the empty platform and open its initial inventory
    p.Note("水10の液体貨車をドッキングし、Fで貨車画面を開く");
    var rails = platform.GetComponents<RailComponent>();
    var entry = rails.First(r => r.FrontNode.StationRef.NodeRole==StationNodeRole.Entry).FrontNode;
    var exit = rails.First(r => r.FrontNode.StationRef.NodeRole==StationNodeRole.Exit).FrontNode;
    var car = new TrainCar(MasterHolder.TrainUnitMaster.GetTrainCarMaster(Guid.Parse("f998f54b-7619-491c-a100-ee88955f9058")),true);
    var fluid = new FluidContainer(1000);
    fluid.AddLiquid(new FluidStack(10,water));
    car.SetContainer(new FluidTrainCarContainer(fluid));
    var position = new RailPosition(new List<IRailNode>{exit,entry},car.Length,0);
    var train = new TrainUnit(position,new List<TrainCar>{car},p.ServerService<TrainRailPositionManager>(),p.ServerService<TrainDiagramManager>());
    p.ServerService<ITrainUnitMutationDatastore>().RegisterTrain(train);
    p.ServerService<ITrainUnitSnapshotNotifyEvent>().NotifySnapshot(train);
    train.trainDiagram.AddEntry(position.GetNodeApproaching(),TrainDiagram.DepartureConditionType.WaitForTicks,10000);
    train.TurnOnAutoRun();
    p.Assert(train.IsAutoRun,"停車ダイヤの自動運転でドッキングを維持");
    train.trainUnitStationDocking.TryDockWhenStopped();
    await p.Until(()=>car.IsDocked,15,"貨車ドッキング");
    var registry = UnityEngine.Object.FindFirstObjectByType<TrainCarObjectDatastore>();
    await p.Until(()=>registry.TryGetEntity(car.TrainCarInstanceId,out var e)&&e.Interactable!=null,30,"貨車View出現");
    registry.TryGetEntity(car.TrainCarInstanceId,out var entity);
    await Open(entity.GetComponentsInChildren<Collider>().First(c=>c.enabled));
    await CheckFluid("train-inventory-fluids","10 / 1,000","貨車初期");
    await p.Screenshot("03-car-water10");
    await p.WaitSeconds(1f);

    // 通常tick搬送後のDOM更新を画面を開いたまま確認する
    // Verify DOM refresh after normal tick transfer while keeping the panel open
    p.Note("PFへ水80を戻し、開いた貨車画面が90へ更新されるまで待つ");
    pf.AddLiquid(new FluidStack(reserve.Amount,reserve.FluidId),default);
    await p.Until(()=>Math.Abs(fluid.Amount-90)<0.001,20,"通常tickで貨車へ80搬送");
    p.Assert(pf.Container.Container.Amount==0,"PF残量0");
    p.Assert(p.CurrentUiState==UIStateEnum.SubInventory,"貨車画面を開いたまま搬送");
    await CheckFluid("train-inventory-fluids","90 / 1,000","貨車更新");
    await p.Screenshot("04-car-water90");
    await p.WaitSeconds(1f);
    await Close();
    await p.Screenshot("05-gameplay-closed");
    await p.WaitSeconds(1f);

    #region Internal
    async UniTask Open(Collider collider)
    {
        var bounds = collider.bounds;
        p.WarpPlayer(new Vector3(bounds.center.x,33.5f,bounds.min.z-1));
        await p.WaitSeconds(0.5f);
        SemanticInput.KeyDown(Key.LeftAlt);
        await UniTask.DelayFrame(3);
        await p.AimAt(bounds.center);
        await p.PressInteract();
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.WaitUiState(UIStateEnum.SubInventory,15);
    }
    async UniTask CheckFluid(string testid,string expected,string label)
    {
        var dom = await PlaytestDomQuery.Query(testid,10);
        var started = Time.realtimeSinceStartup;
        while ((!dom.Found || !dom.Text.Contains(expected)) && Time.realtimeSinceStartup-started<10)
        {
            await UniTask.Yield();
            dom = await PlaytestDomQuery.Query(testid,2);
        }
        p.Note(label+" DOM="+dom.Text);
        p.Assert(dom.Found&&dom.Visible,label+"の液体スロットが可視");
        p.Assert(dom.Text.Contains("水"),label+"の液体名が水");
        p.Assert(dom.Text.Contains(expected),label+"の量容量="+expected);
    }
    async UniTask Close()
    {
        await p.ClickWebUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen,15);
    }
    #endregion
});
