// 油井の出力口からパイプへ原油が出ること、石窯へ電柱を置いても電線がつながらないことを実プレイで確かめる
// Confirm in play that crude oil leaves the oil well into a pipe and that placing a pole near the stone kiln wires nothing
using System;
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Playtest;
using Client.Playtest.Operations.Ui;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.EnergySystem;
using Game.Fluid;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("master-quick-fixes-oilwell-kiln", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == Client.Game.InGame.UI.UIState.UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen に戻った");
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);

    // 油井を北向きに置き、出力口 offset [1,1,8] の外側 (1,33,9) にパイプをつなぐ
    // Place the oil well facing north and attach a pipe just outside the outlet offset [1,1,8] at (1,33,9)
    p.Note("油井を置き、出力口の外側にパイプを置いて給電する");
    var oilWell = await p.PlaceBlockDirect("油井", new Vector3Int(0, 32, 0), BlockDirection.North);
    var pipe = await p.PlaceBlockDirect("鉄のパイプ", new Vector3Int(1, 33, 9), BlockDirection.North);
    var generator = await p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-14, 32, 0), BlockDirection.North);
    var widePole = (await p.PlaceBlockDirect("広範囲電柱", new Vector3Int(-4, 32, 0), BlockDirection.North)).GetComponent<IElectricWireConnector>();
    var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
    var wired = (false, false);
    await p.RunOnServer("発電機に給油して油井へ配線する", () =>
    {
        generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(2000, MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"))), default);
        wired = (ElectricWireSystemUtil.TryConnectBothSides(generator.GetComponent<IElectricWireConnector>(), widePole, free), ElectricWireSystemUtil.TryConnectBothSides(widePole, oilWell.GetComponent<IElectricWireConnector>(), free));
    });
    p.Assert(wired.Item1 && wired.Item2, "発電機→電柱→油井の配線");
    var crudeOilId = MasterHolder.FluidMaster.GetFluidId(new Guid("41407950-bb67-4d7a-a718-a7c5c04a5358"));
    // 足場には原油の鉱脈が無いので、汲み上げの代わりに出力タンクへ原油を直接入れて出力口の接続だけを確かめる
    // The scaffold has no crude-oil vein, so put crude oil straight into the output tank and check only the outlet connection
    var outletTargets = -1;
    await p.RunOnServer("油井の出力タンクへ原油を入れる", () =>
    {
        outletTargets = oilWell.GetComponent<Game.Block.Component.BlockConnectorComponent<Game.Block.Blocks.Fluid.IFluidInventory, Game.Block.Interface.Component.ConnectJudge.DefaultContext<Game.Block.Blocks.Fluid.IFluidInventory>>>().ConnectedTargets.Count;
        oilWell.GetComponent<Game.Block.Blocks.Pump.PumpFluidOutputComponent>().EnqueueGeneratedFluid(new FluidStack(200, crudeOilId));
    });
    p.Assert(outletTargets == 1, $"油井の出力口がパイプとつながっている（接続数 {outletTargets}）");
    await p.Until(() => pipe.GetComponent<Game.Block.Blocks.Fluid.IFluidInventory>().GetFluidInventory().Any(f => f.FluidId == crudeOilId && f.Amount > 0), 60f, "油井の出力口からパイプへ原油が出る");
    // カメラは +z 向きなので、南向きの油井をもう1基置いて出口側（-z）から見た目を撮る。南向きの出口は回転で (1,1,0) の -z 側
    // The camera faces +z, so place a south-facing oil well and shoot from the outlet side (-z); rotated, its outlet is (1,1,0) facing -z
    p.Note("南向きの油井を置き、出口の見た目とパイプの位置を撮る");
    var southWell = await p.PlaceBlockDirect("油井", new Vector3Int(10, 32, -12), BlockDirection.South);
    await p.PlaceBlockDirect("鉄のパイプ", new Vector3Int(11, 33, -13), BlockDirection.North);
    var southTargets = -1;
    await p.RunOnServer("南向き油井の出力口の接続数を読む", () => southTargets = southWell.GetComponent<Game.Block.Component.BlockConnectorComponent<Game.Block.Blocks.Fluid.IFluidInventory, Game.Block.Interface.Component.ConnectJudge.DefaultContext<Game.Block.Blocks.Fluid.IFluidInventory>>>().ConnectedTargets.Count);
    p.Assert(southTargets == 1, $"南向き油井の出力口もパイプとつながる（接続数 {southTargets}）");
    await p.WaitBlockGameObject(new Vector3Int(11, 33, -13));
    p.WarpPlayer(new Vector3(14f, 33.5f, -17f));
    await p.WaitSeconds(1.5f);
    await p.Screenshot("01-oilwell-outlet");

    // 石窯の隣に電柱をUI経路（自動接続あり）で置き、どちらにも電線がつかないことを見る
    // Place a pole next to the stone kiln via the UI path (auto-connect on) and see that neither side gets a wire
    p.Note("石窯の隣に電柱をホットバーから置く（石窯は自動接続の対象にならないこと）");
    var kilnPos = new Vector3Int(20, 32, 0);
    var kiln = await p.PlaceBlockDirect("石窯", kilnPos, BlockDirection.North);
    await p.WaitBlockGameObject(kilnPos);
    // 対照として電気炉も置き、電柱の自動接続が働いていること（電気炉にはつながる）を同時に見る
    // As a control, place an electric furnace too and confirm auto-connect is live (the furnace does get wired)
    var furnacePos = new Vector3Int(20, 32, 6);
    var furnace = await p.PlaceBlockDirect("電気炉", furnacePos, BlockDirection.North);
    p.WarpPlayer(new Vector3(22f, 33.5f, -8f));
    await p.Hotbar.UnlockConnectTool("電線");
    await p.GiveItem("銅のワイヤー", 50);
    await p.Hotbar.AssignHotbar(0, "電柱");
    await p.Hotbar.EnterBuildMode(0);
    var polePos = new Vector3Int(23, 32, 0);
    await p.AimAtPlaceOrigin("電柱", polePos);
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(polePos) != null, 15f, "電柱が置けた");
    await p.WaitBlockGameObject(polePos);
    await p.WaitSeconds(1f);
    var pole = p.GetBlock(polePos);
    p.Assert(furnace.GetComponent<IElectricWireConnector>().ContainsWireConnection(pole.BlockInstanceId), "対照: 電気炉には電柱から自動で電線がつく");
    p.Assert(kiln.GetComponent<IElectricWireConnector>().WireConnections.Count == 0, "石窯に電線がつかない");
    p.Assert(!pole.GetComponent<IElectricWireConnector>().ContainsWireConnection(kiln.BlockInstanceId), $"電柱から石窯へ電線が伸びない（電柱の接続数 {pole.GetComponent<IElectricWireConnector>().WireConnections.Count}）");
    var manual = true;
    await p.RunOnServer("電柱から石窯へ手動で張ってみる", () => manual = ElectricWireSystemUtil.TryConnectBothSides(pole.GetComponent<IElectricWireConnector>(), kiln.GetComponent<IElectricWireConnector>(), free));
    p.Assert(!manual, "手動でも石窯へ張れない");
    await p.Hotbar.ExitBuildMode(0);
    await p.Screenshot("02-kiln-pole");

    // マスタの数値: 鉄・電気採掘機の青銅/粘土、ふいご付き精錬炉の青銅レシピ
    // Master values: bronze/clay on the iron and electric miners, the bellows furnace bronze recipe
    var bronze = new Guid("4563eb30-ed99-4280-9726-ab6a144b559a");
    var clay = new Guid("428a93fc-594b-4f00-a966-b9b8bd50859f");
    foreach (var minerName in new[] { "鉄の採掘機", "電気採掘機" })
    {
        var param = (Mooresmaster.Model.BlocksModule.IMinerParam)MasterHolder.BlockMaster.Blocks.Data.First(b => b.Name == minerName).BlockParam;
        var guids = param.MineSettings.items.Select(m => m.ItemGuid).ToList();
        p.Assert(guids.Contains(bronze) && guids.Contains(clay), $"{minerName} の mineSettings に青銅の鉱石と粘土がある（{guids.Count}品目・出力枠{param.OutputItemSlotCount}）");
    }
    var bronzeRecipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.MachineRecipeGuid == new Guid("019dce42-5712-779d-8441-72657ff3b710"));
    p.Assert(bronzeRecipe.Time == 3 && bronzeRecipe.OutputItems[0].Count == 2, $"ふいご付き精錬炉の青銅は2個/3秒（time={bronzeRecipe.Time} out={bronzeRecipe.OutputItems[0].Count}）");
});
