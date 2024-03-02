using Core.EnergySystem;
using Core.EnergySystem.Electric;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

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
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 1), 3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 2), 6, 0, BlockDirection.North);

            //発電機と機械の設定
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 3), 0, 1, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 4), 0, -1, BlockDirection.North);

            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 5), 3, 1, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 6), 6, 1, BlockDirection.North);

            var worldElectricSegment = saveServiceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();
            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            //右端の電柱を削除
            worldBlockDatastore.RemoveBlock(6, 0);
            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());
            //電柱を再設置
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 2), 6, 0, BlockDirection.North);
            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());


            //真ん中の電柱を削除
            worldBlockDatastore.RemoveBlock(3, 0);
            //セグメントが増えていることを確認する
            Assert.AreEqual(2, worldElectricSegment.GetEnergySegmentListCount());

            //真ん中の発電機が2つのセグメントにないことを確認する
            Assert.AreEqual(false, worldElectricSegment.GetEnergySegment(0).Generators.ContainsKey(5));
            Assert.AreEqual(false, worldElectricSegment.GetEnergySegment(1).Generators.ContainsKey(5));

            //両端の電柱が別のセグメントであることを確認する
            var segment1 = worldElectricSegment.GetEnergySegment(worldBlockDatastore.GetBlock(0, 0) as IElectricPole);
            var segment2 = worldElectricSegment.GetEnergySegment(worldBlockDatastore.GetBlock(6, 0) as IElectricPole);

            Assert.AreNotEqual(segment1.GetHashCode(), segment2.GetHashCode());

            //右端の電柱を削除する
            worldBlockDatastore.RemoveBlock(6, 0);
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
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 0), 0, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 1), 3, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 2), 6, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 3), 0, 3, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 4), 3, 3, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(ElectricPoleId, 5), 6, 3, BlockDirection.North);

            //発電機と機械の設定
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 103), 0, 1, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 104), 0, -1, BlockDirection.North);

            worldBlockDatastore.AddBlock(blockFactory.Create(GenerateId, 105), 3, -1, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(MachineId, 106), 6, 1, BlockDirection.North);


            //セグメントの数を確認
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            //真ん中の電柱を削除
            worldBlockDatastore.RemoveBlock(3, 0);
            //セグメント数が変わってないかチェック
            Assert.AreEqual(1, worldElectricSegment.GetEnergySegmentListCount());

            //真ん中の発電機がセグメントにないことを確認する
            Assert.AreEqual(false, worldElectricSegment.GetEnergySegment(0).Generators.ContainsKey(105));
        }
    }
}