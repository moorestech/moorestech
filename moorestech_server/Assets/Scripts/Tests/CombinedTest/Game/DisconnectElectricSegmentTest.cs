using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Module.TestMod.ForUnitTestModBlockId;

namespace Tests.CombinedTest.Game
{
    //電柱が無くなったときにセグメントが切断されるテスト
    public class DisconnectElectricSegmentTest
    {
        [Test]
        public void RemoveElectricPoleToDisconnectSegment()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            /*設置する電柱、機械、発電機の場所
             * M □  □ G □  □ M
             * P □  □ P □  □ P
             * G
             */
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            //電柱の設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(0, 0), BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(3, 0), BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(6, 0), BlockDirection.North, out _);
            
            //発電機と機械の設定
            worldBlockDatastore.TryAddBlock(MachineId, Pos(0, 1), BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(GeneratorId, Pos(0, -1), BlockDirection.North, out _);
            
            worldBlockDatastore.TryAddBlock(GeneratorId, Pos(3, 1), BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(MachineId, Pos(6, 1), BlockDirection.North, out _);
            
            IWorldEnergySegmentDatastore<EnergySegment> worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());
            
            //右端の電柱を削除
            worldBlockDatastore.RemoveBlock(Pos(6, 0));
            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());
            //電柱を再設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(6, 0), BlockDirection.North, out _);
            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());
            
            
            //真ん中の電柱を削除
            worldBlockDatastore.RemoveBlock(Pos(3, 0));
            //セグメントが増えていることを確認する
            Assert.AreEqual(2, worldElectricSegment.GetEnergySegmentListCount());
            
            //真ん中の発電機が2つのセグメントにないことを確認する
            Assert.AreEqual(false, worldElectricSegment.GetEnergySegment(0).Generators.ContainsKey(new BlockInstanceId(5)));
            Assert.AreEqual(false, worldElectricSegment.GetEnergySegment(1).Generators.ContainsKey(new BlockInstanceId(5)));
            
            //両端の電柱が別のセグメントであることを確認する
            var segment1Block = worldBlockDatastore.GetBlock(Pos(0, 0));
            var segment2Block = worldBlockDatastore.GetBlock(Pos(6, 0));
            var electricityTransformer1 = segment1Block.GetComponent<IElectricTransformer>();
            var electricityTransformer2 = segment2Block.GetComponent<IElectricTransformer>();
            var segment1 = worldElectricSegment.GetEnergySegment(electricityTransformer1);
            var segment2 = worldElectricSegment.GetEnergySegment(electricityTransformer2);
            
            Assert.AreNotEqual(segment1.GetHashCode(), segment2.GetHashCode());
            
            //右端の電柱を削除する
            worldBlockDatastore.RemoveBlock(Pos(6, 0));
            //セグメントが減っていることを確認する
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());
        }
        
        //最後の電柱を削除した場合に、発電機と機械が正しく削除されるテスト
        [Test]
        public void RemoveLastElectricPoleWithGeneratorAndMachine()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            /*設置する電柱、機械、発電機の場所
             * M □ P □ G
             */

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            //電柱の設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(2, 0), BlockDirection.North, out _);

            //発電機と機械の設定
            worldBlockDatastore.TryAddBlock(MachineId, Pos(0, 0), BlockDirection.North, out var machineBlock);
            worldBlockDatastore.TryAddBlock(GeneratorId, Pos(4, 0), BlockDirection.North, out var generatorBlock);

            var machineInstanceId = machineBlock.BlockInstanceId;
            var generatorInstanceId = generatorBlock.BlockInstanceId;

            IWorldEnergySegmentDatastore<EnergySegment> worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();

            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            //セグメントに発電機と機械が登録されていることを確認
            var segment = worldElectricSegment.GetEnergySegment(0);
            Assert.AreEqual(1, segment.Generators.Count);
            Assert.AreEqual(1, segment.Consumers.Count);
            Assert.IsTrue(segment.Generators.ContainsKey(generatorInstanceId));
            Assert.IsTrue(segment.Consumers.ContainsKey(machineInstanceId));

            //電柱を削除
            worldBlockDatastore.RemoveBlock(Pos(2, 0));

            //セグメントが削除されていることを確認
            Assert.AreEqual(0, worldElectricSegment.GetEnergySegmentListCount());
            
            // アップデートを呼び出してもエラーが起きないことを確認
            Assert.DoesNotThrow(() => GameUpdater.UpdateWithWait());
        }

        //電柱を消してもループによって1つのセグメントになっている時のテスト
        [Test]
        public void LoopedElectricSegmentRemoveElectricPoleTest()
        {
            /*設置する電柱、機械、発電機の場所
             * P □ □ P □ □ P
             * G □ □ □ □ □ □
             * M □ □ □ □ □ M
             * P □ □ P □ □ P
             * G □ □ G
             */
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            IWorldEnergySegmentDatastore<EnergySegment> worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            
            //電柱の設置
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(0, 0), BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(3, 0), BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(6, 0), BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(0, 3), BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(3, 3), BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(6, 3), BlockDirection.North, out _);
            
            //発電機と機械の設定
            worldBlockDatastore.TryAddBlock(MachineId, Pos(0, 1), BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(GeneratorId, Pos(0, -1), BlockDirection.North, out _);
            
            worldBlockDatastore.TryAddBlock(GeneratorId, Pos(3, -1), BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(MachineId, Pos(6, 1), BlockDirection.North, out _);
            
            
            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());
            
            //真ん中の電柱を削除
            worldBlockDatastore.RemoveBlock(Pos(3, 0));
            //セグメント数が変わってないかチェック
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());
            
            //真ん中の発電機がセグメントにないことを確認する
            Assert.AreEqual(false, worldElectricSegment.GetEnergySegment(0).Generators.ContainsKey(new BlockInstanceId(105)));
        }
        
        private static Vector3Int Pos(int x, int z)
        {
            return new Vector3Int(x, 0, z);
        }
    }
}
