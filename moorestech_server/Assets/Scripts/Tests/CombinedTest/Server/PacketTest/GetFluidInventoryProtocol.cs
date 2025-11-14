using System.Linq;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.GetFluidInventoryProtocol;
using MachineFluidIOTest = Tests.CombinedTest.Core.MachineFluidIOTest;
using FuelGearGeneratorTest = Tests.CombinedTest.Core.FuelGearGeneratorTest;
using FluidTest = Tests.CombinedTest.Core.FluidTest;
using System;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetFluidInventoryProtocol
    {
        [Test]
        public void GetFluidMachineTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // 機械を設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidMachineBlock);
            
            // 機械のFluidInventoryコンポーネントを取得
            var fluidInventory = fluidMachineBlock.GetComponent<VanillaMachineFluidInventoryComponent>();
            
            // 機械に直接複数の液体を追加
            var fluidStack1 = new FluidStack(50, MachineFluidIOTest.FluidId1);
            var fluidStack2 = new FluidStack(30, MachineFluidIOTest.FluidId2);
            var fluidStack3 = new FluidStack(20, MachineFluidIOTest.FluidId3);
            
            // 空のFluidContainerを作成（ソース指定なし）
            var emptyContainer = FluidContainer.Empty;
            
            // 各液体を追加
            var remaining1 = fluidInventory.AddLiquid(fluidStack1, emptyContainer);
            var remaining2 = fluidInventory.AddLiquid(fluidStack2, emptyContainer);
            var remaining3 = fluidInventory.AddLiquid(fluidStack3, emptyContainer);
            
            // 液体が全て追加されたことを確認
            Assert.AreEqual(0, remaining1.Amount);
            Assert.AreEqual(0, remaining2.Amount);
            Assert.AreEqual(0, remaining3.Amount);
            
            // プロトコル経由で液体を取得
            var request = MessagePackSerializer.Serialize(new GetFluidInventoryRequestMessagePack(Vector3Int.zero)).ToList();
            var response = packet.GetPacketResponse(request)[0].ToArray();
            var data = MessagePackSerializer.Deserialize<GetFluidInventoryResponseMessagePack>(response);
            
            // 機械の液体とプロトコルで取得した液体を比較
            Assert.AreEqual(3, data.Fluids.Length); // 3種類の液体が入っている
            
            // 各液体を確認（順序は保証されないので、IDで検索）
            var fluid1 = data.Fluids.FirstOrDefault(f => f.FluidId == MachineFluidIOTest.FluidId1.AsPrimitive());
            var fluid2 = data.Fluids.FirstOrDefault(f => f.FluidId == MachineFluidIOTest.FluidId2.AsPrimitive());
            var fluid3 = data.Fluids.FirstOrDefault(f => f.FluidId == MachineFluidIOTest.FluidId3.AsPrimitive());
            
            Assert.IsNotNull(fluid1);
            Assert.AreEqual(50, fluid1.Amount);
            Assert.IsNotNull(fluid2);
            Assert.AreEqual(30, fluid2.Amount);
            Assert.IsNotNull(fluid3);
            Assert.AreEqual(20, fluid3.Amount);
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
            var remaining = steamFluidComponent.AddLiquid(steamStack, FluidContainer.Empty);
            
            // 蒸気が追加されたことを確認
            Assert.AreEqual(0, remaining.Amount);
            
            // プロトコル経由で液体を取得
            var request = MessagePackSerializer.Serialize(new GetFluidInventoryRequestMessagePack(Vector3Int.zero)).ToList();
            var response = packet.GetPacketResponse(request)[0].ToArray();
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
            var remainAmount = fluidPipe.AddLiquid(addingStack, FluidContainer.Empty);
            
            // 液体が全て追加されたことを確認
            Assert.AreEqual(0, remainAmount.Amount);
            
            // プロトコル経由で液体を取得
            var request = MessagePackSerializer.Serialize(new GetFluidInventoryRequestMessagePack(Vector3Int.zero)).ToList();
            var response = packet.GetPacketResponse(request)[0].ToArray();
            var data = MessagePackSerializer.Deserialize<GetFluidInventoryResponseMessagePack>(response);
            
            // パイプの液体を確認
            Assert.AreEqual(1, data.Fluids.Length); // パイプに液体が1つ
            Assert.AreEqual(FluidTest.FluidId.AsPrimitive(), data.Fluids[0].FluidId);
            Assert.AreEqual(addingAmount, data.Fluids[0].Amount); // 追加した量と同じ
        }
        
    }
}
