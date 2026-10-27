// シナリオ: パネルを床・壁・天井に使いドアで部屋を建て、ドアを歩いて通り、清浄機のUIでフィルターを入れ、部屋欄とツールチップと漏れ表示を確かめる
// Scenario: build a room using panels as floor, walls and ceiling plus a door, walk through it, load filters via the purifier UI, then check the room section, tooltip and leak display
using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Tooltip;
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
using Game.CleanRoom;
using Game.EnergySystem;
using Game.Fluid;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
const string PanelName = "クリーンルームパネル";
var outsidePosition = new Vector3(3.5f, 33.5f, -3.5f); // ドア正面の室外（床の上）
var doorAimPoint = new Vector3(3.5f, 34f, 0.5f); // 1×2×1 ドアの中心
var breachCell = new Vector3Int(6, 33, 3); // 後で外す東の壁
var uiPanelCell = new Vector3Int(6, 33, 4); // Shift+R で縦にして UI で置く東の壁
var hatchCell = new Vector3Int(4, 34, 0); // 南の壁上段のアイテムハッチ（案内と要約の併記を見る）
return PlaytestRunner.Run("cleanroom-room-usability", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 15f, "GameScreenに到達");
    await p.SetupFlatGround();
    // 地形(メサ・岩・木)は足場を突き抜けて立ち位置・照準・画面を狂わせ、原点の遠い大物ほど遅れて生成されるので、スキットと同じ経路で地形の表示と生成を止める
    // Terrain (mesas, rocks, trees) pokes through the scaffold and skews footing, aim and view, with large far-origin pieces spawning late, so stop terrain display and spawning via the skit path
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    // 開始位置(0,33.5,0)は壁の角になるので、建てる前に室外へ退く。床は室外まで北向き(水平)のパネルで敷く
    // The spawn (0,33.5,0) becomes a wall corner, so step outside first; the floor is north-facing (flat) panels reaching outdoors
    p.WarpPlayer(outsidePosition);
    for (var x = -2; x <= 8; x++) for (var z = -6; z <= 8; z++) await p.PlaceBlockDirect(PanelName, new Vector3Int(x, 32, z), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(6, 32, 4));
    await p.WaitSeconds(1f);
    #region パネルを Shift+R で縦にして UI で置く / Stand a panel upright with Shift+R and place it via the UI
    p.WarpPlayer(new Vector3(4.5f, 33.5f, 1.5f));
    await p.PrepareBlockForUiPlacement(PanelName, 1);
    await p.OpenBuildMenuAndSelectBlock(PanelName);
    SemanticInput.KeyDown(Key.LeftShift);
    await p.WaitSeconds(0.5f);
    await p.PressKey(Key.R);
    SemanticInput.KeyUp(Key.LeftShift);
    await p.AimAt(new Vector3(6.5f, 33f, 4.5f));
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(uiPanelCell) != null, 15f, "UIで置いたパネルがサーバーに載った");
    p.Assert(Mathf.Abs((p.GetBlock(uiPanelCell).BlockPositionInfo.BlockDirection.GetRotation() * Vector3.up).y) < 0.5f, $"Shift+Rでパネルが縦向き(Up*/Down*)になった(向き={p.GetBlock(uiPanelCell).BlockPositionInfo.BlockDirection})");
    await p.ExitToGameScreen();
    #endregion
    // 東西南の壁は板が室内側に来る縦向きのパネル、北の壁は壁ブロック、天井は北向きのパネルにする
    // East/west/south walls are upright panels with the plate facing inside, the north wall is wall blocks, the ceiling is north-facing panels
    p.WarpPlayer(outsidePosition);
    for (var x = 0; x <= 6; x++)
    for (var z = 0; z <= 6; z++)
    {
        await p.PlaceBlockDirect(PanelName, new Vector3Int(x, 35, z), BlockDirection.North);
        if (x != 0 && x != 6 && z != 0 && z != 6) continue;
        foreach (var cell in new[] { new Vector3Int(x, 33, z), new Vector3Int(x, 34, z) }.Where(cell => !(x == 3 && z == 0) && cell != uiPanelCell && cell != hatchCell))
            if (x == 0) await p.PlaceBlockDirect(PanelName, cell, BlockDirection.UpWest);
            else if (x == 6) await p.PlaceBlockDirect(PanelName, cell, BlockDirection.DownEast);
            else if (z == 0) await p.PlaceBlockDirect(PanelName, cell, BlockDirection.UpSouth);
            else await p.PlaceBlockDirect("クリーンルームブロック", cell, BlockDirection.North);
    }
    await p.PlaceBlockDirect("クリーンルームドア", new Vector3Int(3, 33, 0), BlockDirection.North);
    await p.PlaceBlockDirect("クリーンルームアイテムハッチ", hatchCell, BlockDirection.North);
    var filterBlock = await p.PlaceBlockDirect("クリーンルーム空気清浄機", new Vector3Int(1, 33, 5), BlockDirection.North);
    var machineBlock = await p.PlaceBlockDirect("EUV露光式半導体製造装置", new Vector3Int(3, 33, 3), BlockDirection.North);
    await WirePower(filterBlock, machineBlock);
    await p.WaitBlockGameObject(new Vector3Int(3, 33, 0));
    // 床(水平)・西(UpWest)・南(UpSouth)・UIで置いた東(縦)の板が、それぞれ自分のセルの中に描かれる
    // The floor (flat), west (UpWest), south (UpSouth) and UI-placed east (upright) plates each render inside their own cell
    foreach (var (plateCell, upright) in new[] { (new Vector3Int(2, 32, 2), false), (new Vector3Int(0, 33, 2), true), (new Vector3Int(1, 33, 0), true), (uiPanelCell, true) })
        await AssertPanelPlateInCell(plateCell, upright);
    // 密閉された部屋が検出されるのを待つ（クラスは問わない）
    // Wait for the sealed room to be detected, whatever its class
    await p.Until(() => p.ServerService<CleanRoomDatastore>().TryGetCleanRoom(machineBlock, out _), 15f, "部屋が検出された");
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
    await p.UntilWebUiElement("clean-room-filter-grid-slot-0", 10f);
    await p.ClickWebUi($"main-slot-{FindMainSlot(localInventory, filterItemId)}");
    await p.ClickWebUi("clean-room-filter-grid-slot-0");
    Core.Inventory.IOpenableInventory filterInventory = filterBlock.GetComponent<IOpenableBlockInventoryComponent>();
    await p.Until(() => filterInventory.GetItem(0).Id == filterItemId && filterInventory.GetItem(0).Count == 5, 10f, "フィルター5個がスロットに入った");
    await p.UntilWebUiElement("clean-room-section", 10f);
    await p.Screenshot("03-filter-loaded");
    await CloseBlockUi();
    #endregion
    #region EUV の部屋欄 / Room section of the EUV
    await OpenBlockUi(new Vector3(4.5f, 33.5f, 2f), new Vector3(4.5f, 34f, 4.5f));
    await p.UntilWebUiElement("clean-room-section", 10f);
    await p.UntilWebUiElement("clean-room-headline", 10f);
    await p.Screenshot("04-euv-room-section");
    await CloseBlockUi();
    #endregion
    #region ハッチとドアのツールチップと漏れ表示 / Hatch and door tooltips and leak display
    p.WarpPlayer(outsidePosition);
    await p.WaitSeconds(1f);
    SemanticInput.KeyDown(Key.LeftAlt);
    await p.AimAt(hatchCell + Vector3.one * 0.5f);
    // アイテムハッチはマスタで openable=false のため操作案内を持たず、部屋の要約(クラス・濃度・清浄機)が出る
    // The item hatch is openable=false in the master, so it has no interact hint and shows the room summary (class, air, purifiers)
    await p.Until(() => (TooltipKeys().Contains(LocalizationKeys.Ui.Tooltip.CleanRoomClass.Key) || TooltipKeys().Contains(LocalizationKeys.Ui.Tooltip.CleanRoomClassOut.Key)) && TooltipKeys().Contains(LocalizationKeys.Ui.Tooltip.CleanRoomAir.Key) && TooltipKeys().Contains(LocalizationKeys.Ui.Tooltip.CleanRoomFilters.Key), 5f, "ハッチのツールチップに部屋の要約(クラス・濃度・清浄機)が出た");
    p.Note($"ハッチのツールチップ行={string.Join(",", TooltipKeys())}");
    await PlaytestWebUiOps.WaitWebUiTextContains("cursor-tooltip", Client.Localization.Localize.GetFormatted(LocalizationKeys.Ui.Tooltip.CleanRoomFilters, new[] { "1" }), 5f);
    await p.Screenshot("05a-hatch-tooltip");
    await p.AimAt(doorAimPoint);
    await p.Until(() => FirstTooltipKey() == LocalizationKeys.Ui.Tooltip.CleanRoomClass.Key || FirstTooltipKey() == LocalizationKeys.Ui.Tooltip.CleanRoomClassOut.Key, 5f, "ドアのツールチップ先頭行が部屋クラス");
    await p.Screenshot("05-door-tooltip-sealed");
    // 直接撤去後に旧部屋が残った。tick競合は仮説。本番撤去と描画待機で安定を確認した
    // The old room remained after direct removal; a tick race is a hypothesis. Production removal and render waiting were observed to be stable
    var removeResponse = await ClientContext.VanillaApi.Response.Block.BlockRemove(breachCell, System.Threading.CancellationToken.None);
    p.Assert(removeResponse.Success, $"撤去プロトコルで東の壁を外した(失敗理由={removeResponse.FailureReason})");
    await p.Until(() => FirstTooltipKey() == LocalizationKeys.Ui.Tooltip.CleanRoomNotSealed.Key, 10f, "壁を外すとドアのツールチップが未密閉になる");
    await p.Until(() => ActiveMarkers().Any(marker => Vector3.Distance(marker.position, breachCell + Vector3.one * 0.5f) < 0.01f), 5f, "外した壁の位置に漏れマーカーが出た");
    p.Assert(ActiveMarkers().Length == 1, "漏れマーカーは外した1セルだけ");
    // 再照会(1秒毎)を2回以上またいでも未密閉から戻らないかを毎フレーム見て、Web UI の描画が追いついてから撮る
    // Watch every frame across 2+ re-polls (1 s each) that it never flips back from not-sealed, then capture once the Web UI render catches up
    var observedKeys = new System.Collections.Generic.HashSet<string>();
    for (var watchEnd = Time.realtimeSinceStartup + 2.5f; Time.realtimeSinceStartup < watchEnd; await UniTask.Yield()) observedKeys.Add(FirstTooltipKey());
    p.Assert(observedKeys.SetEquals(new[] { LocalizationKeys.Ui.Tooltip.CleanRoomNotSealed.Key }) && ActiveMarkers().Length == 1, $"2.5秒間ツールチップ先頭行が未密閉のまま・漏れマーカー1個(観測キー={string.Join(",", observedKeys)})");
    await PlaytestWebUiOps.WaitWebUiTextContains("cursor-tooltip", Client.Localization.Localize.GetFormatted(LocalizationKeys.Ui.Tooltip.CleanRoomNotSealed, new[] { "1" }), 5f);
    await p.Screenshot("06-door-tooltip-leak");
    // 外した東の壁はドアの正面からは見えないので、南東の室外から東の壁を狙い漏れマーカーを録画に残す（Altは最後に離す）
    // The removed east wall is hidden from the door front, so aim at the east wall from the south-east and keep the marker on the recording (Alt is released last)
    p.WarpPlayer(new Vector3(8f, 33.5f, -2f));
    await p.AimAt(new Vector3(6.1f, 34.5f, 1.5f));
    await p.WaitSeconds(2f);
    SemanticInput.KeyUp(Key.LeftAlt);
    #endregion
    async UniTask AssertPanelPlateInCell(Vector3Int cell, bool expectUpright)
    {
        // 板の描画範囲がセルの中に収まり、縦なら高さ1・厚さ0.1、横なら高さ0.1になる
        // The plate's rendered bounds stay inside the cell: height 1 and thickness 0.1 when upright, height 0.1 when flat
        var bounds = (await p.WaitBlockGameObject(cell)).GetComponentsInChildren<MeshRenderer>(true).First(renderer => renderer.gameObject.name == "PanelPlate").bounds;
        var cellBounds = new Bounds(cell + Vector3.one * 0.5f, Vector3.one * 1.02f);
        var isUpright = 0.9f < bounds.size.y && Mathf.Min(bounds.size.x, bounds.size.z) < 0.15f;
        p.Assert(cellBounds.Contains(bounds.min) && cellBounds.Contains(bounds.max) && isUpright == expectUpright, $"{cell} のパネルの板がセル内に{(expectUpright ? "縦" : "横")}で描かれる(bounds={bounds})");
    }
    async UniTask OpenBlockUi(Vector3 standPosition, Vector3 aimPoint)
    {
        // Fは三人称だと画面中央を狙い射程2m。電線の上を避けて床に着地しカメラが天井の下に来てから、Altのカーソル照準で開く
        // In third person F aims at screen center with 2 m reach; land on the floor off the wires (camera below the ceiling), then open via the Alt cursor
        p.WarpPlayer(standPosition);
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 33f) < 0.05f, 5f, "床パネルの上(y=33.0)に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(aimPoint);
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
    }
    async UniTask CloseBlockUi() { await p.PressKey(Key.Tab); await p.WaitUiState(UIStateEnum.GameScreen, 10f); }
    int FindMainSlot(ILocalPlayerInventory inventory, ItemId itemId) => Enumerable.Range(0, inventory.MainSlotCount).Where(i => inventory[i].Id == itemId && 0 < inventory[i].Count).DefaultIfEmpty(-1).First();
    string[] TooltipKeys() => ClientDIContext.DIContainer.DIContainerResolver.Resolve<IMouseCursorTooltip>().GetPresentation() is { Visible: true } shown ? shown.Lines.Select(line => line.Key.Key).ToArray() : Array.Empty<string>();
    string FirstTooltipKey() => TooltipKeys().FirstOrDefault() ?? string.Empty;
    Transform[] ActiveMarkers() => UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(renderer => renderer.gameObject.activeInHierarchy && renderer.gameObject.name == "CleanRoomLeakMarker").Select(renderer => renderer.transform).ToArray();
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
