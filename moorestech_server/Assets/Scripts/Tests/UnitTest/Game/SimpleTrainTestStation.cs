using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Blocks.TrainRail;
using Game.Context;
using Game.Train.RailGraph;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Game.Block.Interface.Component;
using Core.Master;

namespace Tests.UnitTest.Game
{
    public class SimpleTrainTestStation
    {
        /// <summary>
        /// 駅の向きテスト
        /// 駅を設置したときにRailComponentが真ん中を通るようにならないといけない
        /// </summary>
        [Test]
        public void StationDirectionSimple()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            // 1) 駅をつくってrailcomponentの座標を確認
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(0, 0, 0), BlockDirection.North, out var stationBlockA);

            // RailComponent を取得
            var railcompos = stationBlockA.GetComponent<RailSaverComponent>();
            //2つあるかassert
            Assert.AreEqual(2, railcompos.RailComponents.Length);
            var railComponentA = railcompos.RailComponents[0];
            var railComponentB = railcompos.RailComponents[1];
            Debug.Log("railComponentAの座標" + railComponentA.Position);
            Debug.Log("railComponentBの座標" + railComponentB.Position);
        }

        [Test]
        public void StationDirectionMain()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            //以下のを4方向でloopで確認する

            for (int i = 0; i < 4; i++)
            {
                var direction = (BlockDirection)4 + i;
                // 1) 駅をつくってrailcomponentの座標を確認
                worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(0, 5 * i, 0), direction, out var stationBlockA);
                // RailComponent を取得
                var railcompos = stationBlockA.GetComponent<RailSaverComponent>();
                //2つあるかassert
                Assert.AreEqual(2, railcompos.RailComponents.Length);
                var railComponentA = railcompos.RailComponents[0];
                var railComponentB = railcompos.RailComponents[1];
                Debug.Log("railComponentAの座標" + railComponentA.Position);
                Debug.Log("railComponentBの座標" + railComponentB.Position);
            }
        }

        [Test]
        public void StationConnectionSimple()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(0, 0, 0), BlockDirection.North, out var stationBlockA);
            var railcompos = stationBlockA.GetComponent<RailSaverComponent>();
            var railNodeA = railcompos.RailComponents[0].FrontNode;
            var railNodeB = railcompos.RailComponents[1].FrontNode;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestTrainStation, new Vector3Int(22, 0, 0), BlockDirection.North, out var stationBlockB);
            railcompos = stationBlockB.GetComponent<RailSaverComponent>();
            var railNodeC = railcompos.RailComponents[0].FrontNode;
            var railNodeD = railcompos.RailComponents[1].FrontNode;

            Debug.Log(
                RailGraphDatastore.GetDistanceBetweenNodes(railNodeA, railNodeB)
            );
            var length0 = RailGraphDatastore.GetDistanceBetweenNodes(railNodeB, railNodeC);
            Debug.Log(
                length0
            );
            Assert.AreEqual(0, length0);
            Debug.Log(
                RailGraphDatastore.GetDistanceBetweenNodes(railNodeC, railNodeD)
            );
        }


        [Test]
        public void TrainStationItemInputOutputTest()
        {
            // テスト環境の初期化  
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            // 駅ブロックを設置  
            var stationPosition = new Vector3Int(0, 0, 0);
            worldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.TestTrainStation,
                stationPosition,
                BlockDirection.North,
                out var stationBlock
            );

            Assert.IsNotNull(stationBlock, "駅ブロックの設置に失敗");

            // StationComponentの取得  
            var stationComponent = stationBlock.GetComponent<StationComponent>();
            Assert.IsNotNull(stationComponent, "StationComponentが見つからない");

            // インベントリスロット数の確認  
            Assert.AreEqual(1, stationComponent.InventorySlotCount, "インベントリスロット数が正しくない");

            // アイテム搬入テスト用のチェストを設置  
            var inputChestPosition = new Vector3Int(4, 0, 0); // inputConnectのオフセット位置  
            worldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.ChestId,
                inputChestPosition,
                BlockDirection.North
                , out var inputChest
            );
            Assert.IsNotNull(inputChest, "搬入用チェストの設置に失敗");

            // アイテム搬出テスト用のチェストを設置  
            var outputChestPosition = new Vector3Int(6, 0, 0); // outputConnectのオフセット位置  
            worldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.ChestId,
                outputChestPosition,
                BlockDirection.North,
                out var outputChest
            );
            Assert.IsNotNull(outputChest, "搬出用チェストの設置に失敗");

            var inputInventory = inputChest.GetComponent<IBlockInventory>();
            // ForUnitTestModItemId.Stoneの代わりに、実際に存在するアイテムIDを使用  
            var testItem = ServerContext.ItemStackFactory.Create(new ItemId(1), 10); // Test1アイテムを使用  


            inputInventory.InsertItem(testItem);

            // アイテム搬入の確認  
            var insertedItem = inputInventory.GetItem(0);
            Assert.AreEqual(new ItemId(1), insertedItem.Id, "テストアイテムが正しく挿入されていない");
            Assert.AreEqual(10, insertedItem.Count, "アイテム数が正しくない");

            // 駅のインベントリ接続の確認  
            var stationInventory = stationBlock.GetComponent<IBlockInventory>();
        }

    }
}