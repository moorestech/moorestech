using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
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
using SteamGearGeneratorTest = Tests.CombinedTest.Core.SteamGearGeneratorTest;
using FluidTest = Tests.CombinedTest.Core.FluidTest;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetFluidInventoryProtocol
    {
        [Test]
        public void GetFluidMachineTest()
        {
            // 機械を設置 参考: moorestech/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineFluidIOTest.cs
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // 機械を設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.zero, BlockDirection.North, out var fluidMachineBlock);
            
            // パイプを設置して機械に接続
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1), BlockDirection.North, out var fluidPipeBlock);
            
            // パイプに液体を追加
            指摘：パイプ経由で転送するのではなく、直接液体を入れてください。他のテストも同様です。
            var fluidPipe = fluidPipeBlock.GetComponent<FluidPipeComponent>();
            var addingStack = new FluidStack(50, MachineFluidIOTest.FluidId1);
            fluidPipe.AddLiquid(addingStack, FluidContainer.Empty);
            
            // Updateを複数回実行して液体を転送
            for (int i = 0; i < 10; i++)
            {
                GameUpdater.UpdateWithWait();
            }
            
            // プロトコル経由で液体を取得 参考：このディレクトリの他のテスト
            var request = MessagePackSerializer.Serialize(new GetFluidInventoryRequestMessagePack(Vector3Int.zero)).ToList();
            var response = packet.GetPacketResponse(request)[0].ToArray();
            var data = MessagePackSerializer.Deserialize<GetFluidInventoryResponseMessagePack>(response);
            
            指摘：複数の液体を入れてテストしてください。
            
            // 機械の液体とプロトコルで取得した液体を比較
            Assert.AreEqual(1, data.Fluids.Length); // 入力タンクに液体が1つ
            Assert.AreEqual(MachineFluidIOTest.FluidId1.AsPrimitive(), data.Fluids[0].FluidId);
            Assert.Greater(data.Fluids[0].Amount, 0); // 液体が転送されている
        }
        
        [Test]
        public void GetSteamEngineTest()
        {
            // 上の機械がSteamEngineになったバージョン 参考： moorestech/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/SteamGearGeneratorTest.cs
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // Steam Gear Generatorを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SteamGearGeneratorId, Vector3Int.zero, BlockDirection.North, out var steamGeneratorBlock);
            
            // パイプを設置して接続
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 1), BlockDirection.North, out var fluidPipeBlock);
            
            // パイプに蒸気を追加
            var fluidPipe = fluidPipeBlock.GetComponent<FluidPipeComponent>();
            var steamStack = new FluidStack(30, SteamGearGeneratorTest.SteamFluidId);
            fluidPipe.AddLiquid(steamStack, FluidContainer.Empty);
            
            // Updateを複数回実行して蒸気を転送
            for (int i = 0; i < 10; i++)
            {
                GameUpdater.UpdateWithWait();
            }
            
            // プロトコル経由で液体を取得
            var request = MessagePackSerializer.Serialize(new GetFluidInventoryRequestMessagePack(Vector3Int.zero)).ToList();
            var response = packet.GetPacketResponse(request)[0].ToArray();
            var data = MessagePackSerializer.Deserialize<GetFluidInventoryResponseMessagePack>(response);
            
            // 蒸気タンクの内容を確認
            Assert.AreEqual(1, data.Fluids.Length); // 蒸気タンクに液体が1つ
            Assert.AreEqual(SteamGearGeneratorTest.SteamFluidId.AsPrimitive(), data.Fluids[0].FluidId);
            Assert.Greater(data.Fluids[0].Amount, 0); // 蒸気が転送されている
        }
        
        [Test]
        public void GetFluidPipeTest()
        {
            // 上の機械がパイプになったバージョン 参考: moorestech/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/FluidTest.cs
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // パイプを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.zero, BlockDirection.North, out var fluidPipeBlock);
            
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