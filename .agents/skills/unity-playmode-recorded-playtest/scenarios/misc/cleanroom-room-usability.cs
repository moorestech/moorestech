// シナリオ: パネルを床・壁・天井に使いドアで部屋を建て、ドアを歩いて通り、清浄機のUIでフィルターを入れ、部屋欄を確かめる
// Scenario: build a room using panels as floor, walls and ceiling plus a door, walk through it, load filters via the purifier UI, then check the room section
using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.EnergySystem;
using Game.Fluid;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
const string PanelName = "クリーンルームパネル";
const string WallPanelName = "クリーンルーム壁パネル";
var outsidePosition = new Vector3(3.5f, 33.5f, -3.5f); // ドア正面の室外（床の上）
var uiPanelOrigin = new Vector3Int(6, 33, 1); // 壁パネル(5x5x1)を R で東向きに回して UI で置く東の壁。x=6・y33..37・z1..5 を占める
var hatchCell = new Vector3Int(4, 34, 0); // 南の壁上段のアイテムハッチ
return PlaytestRunner.Run("cleanroom-room-usability", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 15f, "GameScreenに到達");
    await p.SetupFlatGround();
    // 地形(メサ・岩・木)は足場を突き抜けて立ち位置・照準・画面を狂わせ、原点の遠い大物ほど遅れて生成されるので、スキットと同じ経路で地形の表示と生成を止める
    // Terrain (mesas, rocks, trees) pokes through the scaffold and skews footing, aim and view, with large far-origin pieces spawning late, so stop terrain display and spawning via the skit path
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    // 開始位置(0,33.5,0)は壁の角になるので、建てる前に室外へ退く。床は室外まで北向き(水平)の 5x1x5 のパネルを x-4..10・z-6..8 に 3x3 枚敷く
    // The spawn (0,33.5,0) becomes a wall corner, so step outside first; the floor is 3x3 north-facing (flat) 5x1x5 panels over x-4..10 / z-6..8 reaching outdoors
    p.WarpPlayer(outsidePosition);
    for (var x = -4; x <= 6; x += 5) for (var z = -6; z <= 4; z += 5) await p.PlaceBlockDirect(PanelName, new Vector3Int(x, 32, z), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(6, 32, 4));
    await p.WaitSeconds(1f);
    #region 壁パネルを R で回して UI で置く / Rotate a wall panel with R and place it via the UI
    // 縦回転は廃止したので、壁は縦の板を持つ壁パネルを水平に回して立てる
    // Vertical rotation is gone, so walls use the wall panel (an upright plate) turned horizontally
    p.WarpPlayer(new Vector3(4.5f, 33.5f, 1.5f));
    await p.PrepareBlockForUiPlacement(WallPanelName, 1);
    await p.OpenBuildMenuAndSelectBlock(WallPanelName);
    await p.PressKey(Key.R);
    await p.AimAt(new Vector3(6.5f, 33f, 3.5f));
    // 照準直後はプレビューが追従していないので待ってから置く
    // The preview lags the aim, so wait before placing
    await p.WaitSeconds(0.5f);
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(uiPanelOrigin) != null, 15f, "UIで置いた壁パネルがサーバーに載った");
    var uiPanelInfo = p.GetBlock(uiPanelOrigin).BlockPositionInfo;
    p.Assert(uiPanelInfo.OriginalPos == uiPanelOrigin && uiPanelInfo.BlockDirection == BlockDirection.East, $"R1回で壁パネルが東向きになり x=6・z1..5 を占めた(原点={uiPanelInfo.OriginalPos} 向き={uiPanelInfo.BlockDirection})");
    await p.ExitToGameScreen();
    #endregion
    // 内寸 x1..5・z1..5・y33..37。東西北の壁は板が室内側に来る向きの壁パネル 1 枚ずつ、ドアとハッチの穴が要る南の壁と四隅の柱はブロック、天井は北向きのパネル 1 枚にする
    // Interior x1..5 / z1..5 / y33..37; east/west/north walls are one upright panel each with the plate inside, the south wall (door and hatch holes) and corner posts are blocks, the ceiling is one north-facing panel
    p.WarpPlayer(outsidePosition);
    await p.PlaceBlockDirect(WallPanelName, new Vector3Int(0, 33, 1), BlockDirection.West);
    await p.PlaceBlockDirect(WallPanelName, new Vector3Int(1, 33, 6), BlockDirection.North);
    await p.PlaceBlockDirect(PanelName, new Vector3Int(1, 38, 1), BlockDirection.North);
    for (var x = 0; x <= 6; x++)
    for (var y = 33; y <= 37; y++)
    {
        if (!(x == 3 && y <= 34) && new Vector3Int(x, y, 0) != hatchCell) await p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, y, 0), BlockDirection.North);
        if (x == 0 || x == 6) await p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, y, 6), BlockDirection.North);
    }
    await p.PlaceBlockDirect("クリーンルームドア", new Vector3Int(3, 33, 0), BlockDirection.North);
    await p.PlaceBlockDirect("クリーンルームアイテムハッチ", hatchCell, BlockDirection.North);
    var filterBlock = await p.PlaceBlockDirect("クリーンルーム空気清浄機", new Vector3Int(1, 33, 5), BlockDirection.North);
    var machineBlock = await p.PlaceBlockDirect("EUV露光式半導体製造装置", new Vector3Int(3, 33, 3), BlockDirection.North);
    await WirePower(filterBlock, machineBlock);
    await p.WaitBlockGameObject(new Vector3Int(3, 33, 0));
    // 床(水平)・西・北・UIで置いた東の壁パネルの板が、それぞれ自分の占める範囲の中に描かれる
    // The floor (flat), west, north and UI-placed east wall-panel plates each render inside their own footprint
    foreach (var (origin, size, upright) in new[] { (new Vector3Int(1, 32, -1), new Vector3Int(5, 1, 5), false), (new Vector3Int(0, 33, 1), new Vector3Int(1, 5, 5), true), (new Vector3Int(1, 33, 6), new Vector3Int(5, 5, 1), true), (uiPanelOrigin, new Vector3Int(1, 5, 5), true) })
        await AssertPanelPlateInFootprint(origin, size, upright);
    // 密閉された部屋が検出されるのを待つ（クラスは問わない）
    // Wait for the sealed room to be detected, whatever its class
    await p.WaitForCleanRoom(machineBlock, false, 15f, "部屋が検出された");
    await p.Screenshot("01-room-built");
    #region ドアを歩いて通る / Walk through the door
    p.WarpPlayer(outsidePosition);
    await p.WaitSeconds(1f);
    p.Assert(0.7f < Camera.main.transform.forward.z, "カメラが北(+z)＝ドアを向いている");
    SemanticInput.KeyDown(Key.W);
    await p.Until(() => 1.2f < p.PlayerPosition.z, 10f, "Wで歩いてドアを抜け室内(z>1.2)に入った");
    SemanticInput.KeyUp(Key.W);
    p.Note($"室内の位置={p.PlayerPosition}");
    await p.Screenshot("02-inside-room");
    #endregion
    #region 清浄機のUIでフィルターを入れる / Load filters through the purifier UI
    await p.GiveItemDirect("クリーンルームフィルター", 5);
    var filterItemId = PlaytestItemOps.ResolveItemId("クリーンルームフィルター");
    var localInventory = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ILocalPlayerInventory>();
    await p.Until(() => FindMainSlot(localInventory, filterItemId) >= 0, 10f, "フィルターがクライアントのインベントリに届いた");
    await OpenBlockUi(new Vector3(1.5f, 33.5f, 3.5f), new Vector3(1.5f, 34f, 5.5f));
    await p.UntilUiElement("clean-room-filter-grid-slot-0", 10f);
    await p.ClickUi($"main-slot-{FindMainSlot(localInventory, filterItemId)}");
    await p.ClickUi("clean-room-filter-grid-slot-0");
    Core.Inventory.IOpenableInventory filterInventory = filterBlock.GetComponent<IOpenableBlockInventoryComponent>();
    await p.Until(() => filterInventory.GetItem(0).Id == filterItemId && filterInventory.GetItem(0).Count == 5, 10f, "フィルター5個がスロットに入った");
    await p.UntilUiElement("clean-room-section", 10f);
    await p.Screenshot("03-filter-loaded");
    await CloseBlockUi();
    #endregion
    #region EUV の部屋欄 / Room section of the EUV
    await OpenBlockUi(new Vector3(4.5f, 33.5f, 2f), new Vector3(4.5f, 34f, 4.5f));
    await p.UntilUiElement("clean-room-section", 10f);
    await p.UntilUiElement("clean-room-headline", 10f);
    await p.Screenshot("04-euv-room-section");
    await CloseBlockUi();
    #endregion
    async UniTask AssertPanelPlateInFootprint(Vector3Int origin, Vector3Int size, bool expectUpright)
    {
        // 板の描画範囲がパネルの占める範囲に収まり、縦なら高さ5で厚さは1マス未満、横なら5x5で高さ1マス未満になる（板は厚さ0.7で層の中央）
        // The plate's rendered bounds stay inside the panel footprint: 5 tall and under one cell thick when upright, 5x5 and under one cell tall when flat (the plate is 0.7 thick, centred in its layer)
        var bounds = (await p.WaitBlockGameObject(origin)).GetComponentsInChildren<MeshRenderer>(true).First(renderer => renderer.gameObject.name == "PanelPlate").bounds;
        var footprint = new Bounds(origin + (Vector3)size * 0.5f, (Vector3)size + Vector3.one * 0.02f);
        var isUpright = 4.5f < bounds.size.y && Mathf.Min(bounds.size.x, bounds.size.z) < 0.9f;
        var isFlat = bounds.size.y < 0.9f && 4.5f < Mathf.Min(bounds.size.x, bounds.size.z);
        p.Assert(footprint.Contains(bounds.min) && footprint.Contains(bounds.max) && (expectUpright ? isUpright : isFlat), $"{origin} のパネルの板が占める範囲内に{(expectUpright ? "縦" : "横")}で描かれる(bounds={bounds})");
    }
    async UniTask OpenBlockUi(Vector3 standPosition, Vector3 aimPoint)
    {
        // Fは三人称だと画面中央を狙い射程2m。電線の上を避けて床に着地しカメラが天井の下に来てから、Altのカーソル照準で開く
        // In third person F aims at screen center with 2 m reach; land on the floor off the wires (camera below the ceiling), then open via the Alt cursor
        p.WarpPlayer(standPosition);
        await p.Until(() => 32.5f < p.PlayerPosition.y && p.PlayerPosition.y < 33.2f, 5f, "床パネルの板の上(y≈32.85)に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(aimPoint);
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
    }
    async UniTask CloseBlockUi() { await p.PressKey(Key.Tab); await p.WaitUiState(UIStateEnum.GameScreen, 10f); }
    int FindMainSlot(ILocalPlayerInventory inventory, ItemId itemId) => Enumerable.Range(0, inventory.MainSlotCount).Where(i => inventory[i].Id == itemId && 0 < inventory[i].Count).DefaultIfEmpty(-1).First();
    async UniTask WirePower(IBlock filter, IBlock machine)
    {
        // 室外の発電機へ燃料を直接入れ、電線で清浄機と機械へ給電する（接続処理が電力網を dirty にする）
        // Fuel an outdoor generator directly and wire it to the purifier and machine; connecting marks the network dirty
        var generatorBlock = await p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-15, 32, -15), BlockDirection.North);
        var (generatorConnector, filterConnector, machineConnector) = (generatorBlock.GetComponent<IElectricWireConnector>(), filter.GetComponent<IElectricWireConnector>(), machine.GetComponent<IElectricWireConnector>());
        var freeCost = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
        var (wiredFilter, wiredMachine) = (false, false);
        await p.RunOnServer("発電機に給油して清浄機と機械へ配線する", () =>
        {
            generatorBlock.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(400, MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"))), default);
            (wiredFilter, wiredMachine) = (ElectricWireSystemUtil.TryConnectBothSides(generatorConnector, filterConnector, freeCost), ElectricWireSystemUtil.TryConnectBothSides(generatorConnector, machineConnector, freeCost));
        });
        p.Assert(wiredFilter, "電線 発電機→清浄機");
        p.Assert(wiredMachine, "電線 発電機→EUV");
    }
});
