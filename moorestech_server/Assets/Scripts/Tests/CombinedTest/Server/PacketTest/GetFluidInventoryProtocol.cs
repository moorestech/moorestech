using System.Linq;
using Core.Master;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Game.UnlockState;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.GetFluidInventoryProtocol;
using MachineFluidIOTest = Tests.CombinedTest.Core.MachineFluidIOTest;
using FuelGearGeneratorTest = Tests.CombinedTest.Core.FuelGearGeneratorTest;
using FluidTest = Tests.CombinedTest.Core.FluidTest;
using System;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetFluidInventoryProtocol
    {
        [Test]
        public void GetFluidMachineTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // 機械を設置し、入力液体2種(タンク0・1)を持つレシピを選択する（束縛前提。ADR 0042）
            // Place the machine and select the recipe with 2 input fluids (tank 0, 1); binding requires this (ADR 0042)
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidMachineBlock);
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(ForUnitTestMachineRecipeId.LockedMachineRecipe);
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.LockedMachineRecipe);
            MachineRecipeSelectTestUtil.SelectRecipe(fluidMachineBlock, recipe);

            // 機械のFluidInventoryコンポーネントを取得
            var fluidInventory = fluidMachineBlock.GetComponent<VanillaMachineFluidInventoryComponent>();

            // 機械に直接複数の液体を追加（fluid3は束縛外タンク2向けのため拒否される）
            // Add several fluids directly (fluid3 targets the unbound tank 2 and is refused)
            var fluidStack1 = new FluidStack(50, MachineFluidIOTest.FluidId1);
            var fluidStack2 = new FluidStack(30, MachineFluidIOTest.FluidId2);
            var fluidStack3 = new FluidStack(20, MachineFluidIOTest.FluidId3);

            // 各液体を追加
            var remaining1 = fluidInventory.AddLiquid(fluidStack1, default);
            var remaining2 = fluidInventory.AddLiquid(fluidStack2, default);
            var remaining3 = fluidInventory.AddLiquid(fluidStack3, default);

            // 束縛済みの2種は全量入り、束縛外のfluid3は拒否されて全量残ることを確認
            // The two bound fluids fully land while the unbound fluid3 is fully refused
            Assert.AreEqual(0, remaining1.Amount);
            Assert.AreEqual(0, remaining2.Amount);
            Assert.AreEqual(20, remaining3.Amount);

            // プロトコル経由で液体を取得
            var request = MessagePackSerializer.Serialize(new GetFluidInventoryRequestMessagePack(Vector3Int.zero));
            var response = packet.GetPacketResponse(request, new PacketResponseContext(null))[0];
            var data = MessagePackSerializer.Deserialize<GetFluidInventoryResponseMessagePack>(response);

            // 機械の液体とプロトコルで取得した液体を比較（拒否されたfluid3は含まれない）
            Assert.AreEqual(2, data.Fluids.Length); // 束縛された2種類の液体が入っている

            // 各液体を確認（順序は保証されないので、IDで検索）
            var fluid1 = data.Fluids.FirstOrDefault(f => f.FluidId == MachineFluidIOTest.FluidId1.AsPrimitive());
            var fluid2 = data.Fluids.FirstOrDefault(f => f.FluidId == MachineFluidIOTest.FluidId2.AsPrimitive());

            Assert.IsNotNull(fluid1);
            Assert.AreEqual(50, fluid1.Amount);
            Assert.IsNotNull(fluid2);
            Assert.AreEqual(30, fluid2.Amount);
        }
        
        [Test]
        public void GetSteamEngineTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // Steam Gear Generatorを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FuelGearGeneratorId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var steamGeneratorBlock);
            
            // Steam Gear GeneratorのFluidComponentを取得
            var steamFluidComponent = steamGeneratorBlock.GetComponent<FuelGearGeneratorFluidComponent>();
            
            // 蒸気を直接追加
            var steamStack = new FluidStack(100, FuelGearGeneratorTest.SteamFluidId);
            var remaining = steamFluidComponent.AddLiquid(steamStack, default);
            
            // 蒸気が追加されたことを確認
            Assert.AreEqual(0, remaining.Amount);
            
            // プロトコル経由で液体を取得
            var request = MessagePackSerializer.Serialize(new GetFluidInventoryRequestMessagePack(Vector3Int.zero));
            var response = packet.GetPacketResponse(request, new PacketResponseContext(null))[0];
            var data = MessagePackSerializer.Deserialize<GetFluidInventoryResponseMessagePack>(response);
            
            // 蒸気タンクの内容を確認
            Assert.AreEqual(1, data.Fluids.Length); // 蒸気タンクに液体が1つ
            Assert.AreEqual(FuelGearGeneratorTest.SteamFluidId.AsPrimitive(), data.Fluids[0].FluidId);
            Assert.AreEqual(100, data.Fluids[0].Amount); // 追加した量と同じ
        }
        
        [Test]
        public void GetFluidPipeTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // パイプを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock);
            
            // パイプに液体を追加
            var fluidPipe = fluidPipeBlock.GetComponent<FluidPipeComponent>();
            const double addingAmount = 50;
            var addingStack = new FluidStack(addingAmount, FluidTest.FluidId);
            var remainAmount = fluidPipe.AddLiquid(addingStack, default);
            
            // 液体が全て追加されたことを確認
            Assert.AreEqual(0, remainAmount.Amount);
            
            // プロトコル経由で液体を取得
            var request = MessagePackSerializer.Serialize(new GetFluidInventoryRequestMessagePack(Vector3Int.zero));
            var response = packet.GetPacketResponse(request, new PacketResponseContext(null))[0];
            var data = MessagePackSerializer.Deserialize<GetFluidInventoryResponseMessagePack>(response);
            
            // パイプの液体を確認
            Assert.AreEqual(1, data.Fluids.Length); // パイプに液体が1つ
            Assert.AreEqual(FluidTest.FluidId.AsPrimitive(), data.Fluids[0].FluidId);
            Assert.AreEqual(addingAmount, data.Fluids[0].Amount); // 追加した量と同じ
        }
        
    }
}
