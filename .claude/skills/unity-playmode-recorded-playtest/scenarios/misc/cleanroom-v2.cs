using System;
using Client.Playtest;
using Client.Playtest.Operations;
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
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("cleanroom-v2", options, async p =>
{
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(3f, 33.5f, -5f));

    // 部屋の外殻を建設: 床y=32・天井y=35の全面 + 側壁y=33,34の周回。ドアとハッチを1枚ずつ壁に埋め込む
    // Build the room shell: full floor(y=32)/ceiling(y=35) plus perimeter side walls(y=33,34) with one door and one item hatch
    var doorPos = new Vector3Int(3, 33, 0);
    var hatchPos = new Vector3Int(5, 34, 6);
    for (var x = 0; x <= 6; x++)
    for (var z = 0; z <= 6; z++)
    {
        p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, 32, z), BlockDirection.North);
        p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, 35, z), BlockDirection.North);
        var isPerimeter = x == 0 || x == 6 || z == 0 || z == 6;
        if (!isPerimeter) continue;
        for (var y = 33; y <= 34; y++)
        {
            var pos = new Vector3Int(x, y, z);
            if (pos == doorPos || pos == hatchPos) continue;
            p.PlaceBlockDirect("クリーンルームブロック", pos, BlockDirection.North);
        }
    }
    p.PlaceBlockDirect("クリーンルームドア", doorPos, BlockDirection.North);
    p.PlaceBlockDirect("クリーンルームアイテムハッチ", hatchPos, BlockDirection.North);
    await p.Screenshot("01-room-shell");

    // 清浄機と機械を部屋の内部に設置し、フィルターを装填する
    // Place the air filter and EUV machine inside, then load filter items
    var filterBlock = p.PlaceBlockDirect("クリーンルーム空気清浄機", new Vector3Int(1, 33, 1), BlockDirection.North);
    var machineBlock = p.PlaceBlockDirect("EUV露光式半導体製造装置", new Vector3Int(2, 33, 2), BlockDirection.North);
    var filterItemId = PlaytestItemOps.ResolveItemId("クリーンルームフィルター");
    Core.Inventory.IOpenableInventory filterInventory = filterBlock.GetComponent<IOpenableBlockInventoryComponent>();
    filterInventory.SetItem(0, filterItemId, 5);

    // 発電機を部屋の外に設置してガソリンを直接注入し、電線で清浄機と機械へ給電する
    // Place a gasoline generator outside, fuel it directly, and wire power to the filter and machine
    var generatorBlock = p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-15, 32, -15), BlockDirection.North);
    var gasolineId = MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"));
    generatorBlock.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(400, gasolineId), FluidContainer.Empty);
    var genConn = generatorBlock.GetComponent<IElectricWireConnector>();
    var filterConn = filterBlock.GetComponent<IElectricWireConnector>();
    var machineConn = machineBlock.GetComponent<IElectricWireConnector>();
    var freeCost = new ElectricWireConnectionCost(ItemMaster.EmptyItemId, 0);
    p.Assert(ElectricWireSystemUtil.TryConnectBothSides(genConn, filterConn, freeCost), "wire gen->filter");
    p.Assert(ElectricWireSystemUtil.TryConnectBothSides(genConn, machineConn, freeCost), "wire gen->machine");
    p.ServerService<IElectricWireNetworkDatastore>().RebuildAround(genConn, filterConn, machineConn);

    // 密閉部屋が検出され、給電された清浄機によりクラスAが成立するのを待つ
    // Wait for the sealed room to be detected and promoted to class A by the powered filter
    var datastore = p.ServerService<CleanRoomDatastore>();
    var outIndex = MasterHolder.CleanRoomMaster.OutThresholdIndex;
    await p.Until(() => datastore.TryGetCleanRoom(machineBlock, out var r) && r.ThresholdIndex == 0, 20f, "class A established");
    await p.Screenshot("02-class-a");

    // ウェハを投入して加工開始とチップ産出を確認する（EUV失敗20%を見込み複数投入）
    // Insert wafers and confirm processing starts and a chip is produced (extra wafers cover EUV failures)
    var waferId = PlaytestItemOps.ResolveItemId("完成パターンウェハ");
    Core.Inventory.IOpenableInventory machineInventory = machineBlock.GetComponent<IOpenableBlockInventoryComponent>();
    machineInventory.SetItem(0, waferId, 8);
    var processor = machineBlock.GetComponent<Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent>();
    await p.Until(() => processor.CurrentState == ProcessState.Processing, 15f, "machine processing");
    var chipIds = new[]
    {
        PlaytestItemOps.ResolveItemId("ICチップLv1"), PlaytestItemOps.ResolveItemId("ICチップLv2"),
        PlaytestItemOps.ResolveItemId("ICチップLv3"), PlaytestItemOps.ResolveItemId("ICチップLv4"),
    };
    bool HasChipOutput()
    {
        for (var slot = 2; slot <= 3; slot++)
        {
            var stack = machineInventory.GetItem(slot);
            if (stack.Count > 0 && Array.IndexOf(chipIds, stack.Id) >= 0) return true;
        }
        return false;
    }
    await p.Until(HasChipOutput, 60f, "chip produced");
    await p.Screenshot("03-chip-output");

    // 壁を1枚破壊して密閉を崩し、機械がHaltedへ遷移することを確認する
    // Break one wall to breach the seal and confirm the machine transitions to Halted
    var breachPos = new Vector3Int(6, 33, 3);
    p.RemoveBlock(breachPos);
    await p.Until(() => processor.CurrentState == ProcessState.Halted, 10f, "machine halted after breach");
    p.Assert(!datastore.TryGetCleanRoom(machineBlock, out _), "room dissolved after breach");
    await p.Screenshot("04-halted");

    // 壁を戻して再密閉し、部屋がOutから浄化されてクラスAへ復帰、機械が加工再開するのを確認する
    // Reseal the wall and confirm the room re-purifies from Out back to class A and the machine resumes
    p.PlaceBlockDirect("クリーンルームブロック", breachPos, BlockDirection.North);
    await p.Until(() => datastore.TryGetCleanRoom(machineBlock, out var r) && r.ThresholdIndex == 0, 20f, "room reclassified A after reseal");
    await p.Until(() => processor.CurrentState == ProcessState.Processing || processor.CurrentState == ProcessState.Idle, 20f, "machine recovered from halted");
    p.Assert(processor.CurrentState != ProcessState.Halted, "machine not halted after recovery");
    await p.Screenshot("05-recovered");
});
