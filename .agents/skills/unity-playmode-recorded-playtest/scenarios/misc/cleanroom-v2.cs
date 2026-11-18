using Game.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Inventory;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.EnergySystem;
using Game.Fluid;
using Game.Research;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
var semiconductorResearch = Guid.Parse("019e3ad9-46e2-73fd-9555-8c1d0245d26d");
return PlaytestRunner.Run("cleanroom-v2", options, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    p.WarpPlayer(new Vector3(3f, 33.5f, -5f));
    await p.RunOnServer("半導体製造の研究を前提ごと完了扱いにする", () => ServerContext.GetService<IResearchDataStore>().LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(semiconductorResearch).Select(g => g.ToString()).ToList() }));

    // 部屋の外殻を建設: 床y=32・天井y=38の全面 + 側壁y=33..37。ドアとハッチは 5x5x1 の壁サイズなので南と北の壁の x1..5 にそれぞれ1枚ずつはめる
    // Build the room shell: full floor(y=32)/ceiling(y=38) plus side walls(y=33..37); the door and item hatch are 5x5x1 wall pieces filling x1..5 of the south and north walls
    var doorPos = new Vector3Int(1, 33, 0);
    var hatchPos = new Vector3Int(1, 33, 6);
    for (var x = 0; x <= 6; x++)
    for (var z = 0; z <= 6; z++)
    {
        await p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, 32, z), BlockDirection.North);
        await p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, 38, z), BlockDirection.North);
        if (x != 0 && x != 6) continue;
        for (var y = 33; y <= 37; y++) await p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, y, z), BlockDirection.North);
    }
    await p.PlaceBlockDirect("クリーンルームドア", doorPos, BlockDirection.North);
    await p.PlaceBlockDirect("クリーンルームアイテムハッチ", hatchPos, BlockDirection.North);
    await p.Screenshot("01-room-shell");

    // 清浄機2台と EUV を部屋の内部に置き、フィルターを装填する。クラスAは換気率0.058以上が要り、内部の空き約103セルでは清浄機2台（毎秒10）で届く
    // Place two air filters and the EUV inside and load filters; class A needs an air change rate of 0.058, which two filters (10/s) reach over about 103 free cells
    var filterBlocks = new[] { await p.PlaceBlockDirect("クリーンルーム空気清浄機", new Vector3Int(1, 33, 1), BlockDirection.North), await p.PlaceBlockDirect("クリーンルーム空気清浄機", new Vector3Int(5, 33, 5), BlockDirection.North) };
    var machineBlock = await p.PlaceBlockDirect("EUV露光式半導体製造装置", new Vector3Int(2, 33, 2), BlockDirection.North);
    var filterItemId = PlaytestItemOps.ResolveItemId("クリーンルームフィルター");
    foreach (var filterBlock in filterBlocks)
    {
        Core.Inventory.IOpenableInventory filterInventory = filterBlock.GetComponent<IOpenableBlockInventoryComponent>();
        await p.RunOnServer("清浄機にフィルター5個を装填する", () => filterInventory.SetItem(0, filterItemId, 5));
    }

    // 発電機を部屋の外に設置してガソリンを直接注入し、電線で清浄機と機械へ給電する
    // Place a gasoline generator outside, fuel it directly, and wire power to the filters and machine
    var generatorBlock = await p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-15, 32, -15), BlockDirection.North);
    var gasolineId = MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"));
    var genConn = generatorBlock.GetComponent<IElectricWireConnector>();
    var freeCost = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
    var wired = new List<bool>();
    await p.RunOnServer("発電機に給油して清浄機と機械へ配線する", () =>
    {
        generatorBlock.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(400, gasolineId), default);
        foreach (var consumer in filterBlocks.Append(machineBlock)) wired.Add(ElectricWireSystemUtil.TryConnectBothSides(genConn, consumer.GetComponent<IElectricWireConnector>(), freeCost));
    });
    p.Assert(wired.Count == 3 && wired.All(w => w), "wire gen->filters and machine");

    // 密閉部屋が検出され、給電された清浄機によりクラスAが成立するのを待つ
    // Wait for the sealed room to be detected and promoted to class A by the powered filters
    await p.WaitForCleanRoomClass(machineBlock, 0, 20f, "class A established");
    await p.Screenshot("02-class-a");

    // EUV のトランジスタ設置のレシピを選び、ウェハと IPA を入れて加工開始と産出を確かめる
    // Select the EUV transistor-placement recipe, insert wafers and IPA, and confirm processing starts and output appears
    var output = MasterHolder.ItemMaster.GetItemMaster(PlaytestItemOps.ResolveItemId("トランジスタ設置済みウェハ")).ItemGuid;
    var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == machineBlock.BlockMasterElement.BlockGuid && r.OutputItems.Any() && r.OutputItems.First().ItemGuid == output);
    machineBlock.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
    var selected = MachineRecipeSelectionResult.Success;
    await p.RunOnServer("EUV のレシピを選ぶ", () => selected = selector.SetSelectedRecipe(recipe, new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10)));
    p.Assert(selected == MachineRecipeSelectionResult.Success, "select EUV transistor recipe");
    var waferId = PlaytestItemOps.ResolveItemId("シリコンウェハ");
    var ipaId = MasterHolder.FluidMaster.GetFluidId(MasterHolder.FluidMaster.Fluids.Data.First(f => f.Name == "イソプロピルアルコール").FluidGuid);
    Core.Inventory.IOpenableInventory machineInventory = machineBlock.GetComponent<IOpenableBlockInventoryComponent>();
    await p.RunOnServer("機械にウェハ8枚と IPA を入れる", () =>
    {
        machineInventory.SetItem(0, waferId, 8);
        machineBlock.GetComponent<VanillaMachineFluidInventoryComponent>().AddLiquid(new FluidStack(100000, ipaId), default);
    });
    await p.WaitForMachineState(machineBlock, new[] { ProcessState.Processing }, 15f, "machine processing");
    await p.WaitForItemPrefix(machineBlock.GetComponent<IOpenableBlockInventoryComponent>(), "トランジスタ設置済みウェハ", 1, 60f, "transistor wafer produced");
    await p.Screenshot("03-wafer-output");

    // 壁を1枚破壊して密閉を崩し、機械がHaltedへ遷移することを確認する
    // Break one wall to breach the seal and confirm the machine transitions to Halted
    var breachPos = new Vector3Int(6, 33, 3);
    await p.RemoveBlock(breachPos);
    await p.WaitForMachineState(machineBlock, new[] { ProcessState.Halted }, 10f, "machine halted after breach");
    var roomGone = false;
    await p.RunOnServer("密閉解除後の部屋の有無を読む", () => roomGone = !ServerContext.GetService<CleanRoomDatastore>().TryGetCleanRoom(machineBlock, out _));
    p.Assert(roomGone, "room dissolved after breach");
    await p.Screenshot("04-halted");

    // 壁を戻して再密閉し、部屋がOutから浄化されてクラスAへ復帰、機械が加工再開するのを確認する
    // Reseal the wall and confirm the room re-purifies from Out back to class A and the machine resumes
    await p.PlaceBlockDirect("クリーンルームブロック", breachPos, BlockDirection.North);
    await p.WaitForCleanRoomClass(machineBlock, 0, 20f, "room reclassified A after reseal");
    await p.WaitForMachineState(machineBlock, new[] { ProcessState.Processing, ProcessState.Idle }, 20f, "machine recovered from halted");
    p.Assert((await p.ReadMachineState(machineBlock)) != ProcessState.Halted, "machine not halted after recovery");
    await p.Screenshot("05-recovered");

    #region Internal
    IEnumerable<Guid> WithPrerequisites(Guid target)
    {
        // 前提を辿って閉包を作る（CompleteResearch は素材と前提が揃わないと拒むため使わない）
        // Walk prerequisites into a closure (CompleteResearch refuses without items and prerequisites)
        var done = new HashSet<Guid>();
        var pending = new Stack<Guid>(new[] { target });
        while (pending.TryPop(out var guid))
            if (done.Add(guid)) foreach (var prev in MasterHolder.ResearchMaster.GetResearch(guid).PrevResearchNodeGuids) pending.Push(prev);
        return done;
    }
    #endregion
});
