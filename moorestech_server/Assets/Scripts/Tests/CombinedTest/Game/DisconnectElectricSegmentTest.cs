using Server.Core.EnergySystem;
using Server.Core.EnergySystem.Electric;
using Game.Block.Interface;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    //電柱が無くなったときにセグメントが切断されるテスト
    public class DisconnectElectricSegmentTest
    {
        private const int ElectricPoleId = UnitTestModBlockId.ElectricPoleId;
        private const int MachineId = UnitTestModBlockId.MachineId;
        private const int GenerateId = UnitTestModBlockId.GeneratorId;

        [Test]
        public void RemoveElectricPoleToDisconnectSegment()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            /*設置する電柱、機械、発電機の場所
             * M □  □ G □  □ M
             * P □  □ P □  □ P
             * G
             */

            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();

            //電柱の設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0, new BlockPositionInfo(new Vector3Int(0 ,0), BlockDirection.North, Vector3Int.one)));
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 1, new BlockPositionInfo(new Vector3Int(3 ,0), BlockDirection.North, Vector3Int.one)));
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 2, new BlockPositionInfo(new Vector3Int(6 ,0), BlockDirection.North, Vector3Int.one)));

            //発電機と機械の設定
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 3, new BlockPositionInfo(new Vector3Int(0 ,1), BlockDirection.North, Vector3Int.one)));
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 4, new BlockPositionInfo(new Vector3Int(0 ,-1), BlockDirection.North, Vector3Int.one)));

            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 5, new BlockPositionInfo(new Vector3Int(3 ,1), BlockDirection.North, Vector3Int.one)));
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 6, new BlockPositionInfo(new Vector3Int(6 ,1), BlockDirection.North, Vector3Int.one)));

            var worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            //右端の電柱を削除
            worldBlockDatastore.RemoveBlock(new Vector3Int(6 , 0));
            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());
            //電柱を再設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 2, new BlockPositionInfo(new Vector3Int(6 ,0), BlockDirection.North, Vector3Int.one)));
            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());


            //真ん中の電柱を削除
            worldBlockDatastore.RemoveBlock(new Vector3Int(3, 0));
            //セグメントが増えていることを確認する
            Assert.AreEqual(2, worldElectricSegment.GetEnergySegmentListCount());

            //真ん中の発電機が2つのセグメントにないことを確認する
            Assert.AreEqual(false, worldElectricSegment.GetEnergySegment(0).Generators.ContainsKey(5));
            Assert.AreEqual(false, worldElectricSegment.GetEnergySegment(1).Generators.ContainsKey(5));

            //両端の電柱が別のセグメントであることを確認する
            var segment1 = worldElectricSegment.GetEnergySegment(worldBlockDatastore.GetBlock(new Vector3Int(0,  0)) as IElectricPole);
            var segment2 = worldElectricSegment.GetEnergySegment(worldBlockDatastore.GetBlock(new Vector3Int(6,  0)) as IElectricPole);

            Assert.AreNotEqual(segment1.GetHashCode(), segment2.GetHashCode());

            //右端の電柱を削除する
            worldBlockDatastore.RemoveBlock(new Vector3Int(6, 0));
            //セグメントが減っていることを確認する
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());
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
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();
            var worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();

            //電柱の設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0, new BlockPositionInfo(new Vector3Int(0 ,0), BlockDirection.North, Vector3Int.one)));
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 1, new BlockPositionInfo(new Vector3Int(3 ,0), BlockDirection.North, Vector3Int.one)));
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 2, new BlockPositionInfo(new Vector3Int(6 ,0), BlockDirection.North, Vector3Int.one)));
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 3, new BlockPositionInfo(new Vector3Int(0 ,3), BlockDirection.North, Vector3Int.one)));
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 4, new BlockPositionInfo(new Vector3Int(3 ,3), BlockDirection.North, Vector3Int.one)));
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 5, new BlockPositionInfo(new Vector3Int(6 ,3), BlockDirection.North, Vector3Int.one)));

            //発電機と機械の設定
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 103, new BlockPositionInfo(new Vector3Int(0 ,1), BlockDirection.North, Vector3Int.one)));
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 104, new BlockPositionInfo(new Vector3Int(0 ,-1), BlockDirection.North, Vector3Int.one)));

            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 105, new BlockPositionInfo(new Vector3Int(3 ,-1), BlockDirection.North, Vector3Int.one)));
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 106, new BlockPositionInfo(new Vector3Int(6 ,1), BlockDirection.North, Vector3Int.one)));


            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            //真ん中の電柱を削除
            worldBlockDatastore.RemoveBlock(new Vector3Int(3, 0));
            //セグメント数が変わってないかチェック
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            //真ん中の発電機がセグメントにないことを確認する
            Assert.AreEqual(false, worldElectricSegment.GetEnergySegment(0).Generators.ContainsKey(105));
        }
    }
}