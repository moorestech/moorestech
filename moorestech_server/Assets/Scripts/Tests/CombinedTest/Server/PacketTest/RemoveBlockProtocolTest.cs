using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.RemoveBlockProtocol;
using System;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RemoveBlockProtocolTest
    {
        private const int MachineBlockId = 1;
        private const int PlayerId = 0;
        
        [Test]
        public void RemoveTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlock = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;

            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);

            // 削除するためのブロックの生成
            // Create block to be removed
            worldBlock.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var blockInventory = block.GetComponent<IBlockInventory>();
            blockInventory.InsertItem(itemStackFactory.Create(new ItemId(10), 7), InsertItemContext.Empty);

            // プロトコルを使ってブロックを削除
            // Remove block using protocol
            var response = GetRemoveBlockResponse(packet.GetPacketResponse(RemoveBlock(new Vector3Int(0, 0), PlayerId), new PacketResponseContext(null)));
            Assert.True(response.Success);
            Assert.AreEqual(RemoveBlockFailureReason.None, response.FailureReason);

            // 削除したブロックがワールドに存在しないことを確認
            // Verify removed block no longer exists in world
            Assert.False(worldBlock.Exists(new Vector3Int(0, 0)));

            // requiredItems未定義ブロックは本体の返却なし。ブロックインベントリ内のアイテムのみ返る
            // Blocks without requiredItems refund nothing for the body; only block-inventory items return
            Assert.AreEqual(10, playerInventoryData.MainOpenableInventory.GetItem(0).Id.AsPrimitive());
            Assert.AreEqual(7, playerInventoryData.MainOpenableInventory.GetItem(0).Count);

            // スロット1は空のまま
            // Slot 1 stays empty
            Assert.AreEqual(ItemMaster.EmptyItemId, playerInventoryData.MainOpenableInventory.GetItem(1).Id);
        }
        
        
        // インベントリに全て入りきらない場合はブロックが削除されないことのテスト
        // Test that block is not removed when inventory cannot fit all items
        [Test]
        public void InventoryFullToRemoveBlockSomeItemRemainTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlock = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;

            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;

            // インベントリの2つのスロットを残してインベントリを満杯にする
            // Fill inventory except for 2 slots
            for (var i = 2; i < mainInventory.GetSlotSize(); i++)
                mainInventory.SetItem(i, itemStackFactory.Create(new ItemId(10), 1));

            // 一つ目のスロットにはID3の最大スタック数から1個少ないアイテムを入れる
            // First slot has ID3 items at max stack - 1
            var id3MaxStack = ItemStackLevelDataStore.Instance.GetMaxStack(new ItemId(3));
            mainInventory.SetItem(0, itemStackFactory.Create(new ItemId(3), id3MaxStack - 1));
            // 二つめのスロットにはID4のアイテムを1つ入れておく
            // Second slot has 1 ID4 item
            mainInventory.SetItem(1, itemStackFactory.Create(new ItemId(4), 1));

            // 削除するためのブロックを設置
            // Place block to be removed
            worldBlock.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var blockInventory = block.GetComponent<IBlockInventory>();
            // ブロックにはID3のアイテムを2個と、ID4のアイテムを5個入れる
            // Block contains 2 ID3 items and 5 ID4 items
            blockInventory.SetItem(0, itemStackFactory.Create(new ItemId(3), 2));
            blockInventory.SetItem(1, itemStackFactory.Create(new ItemId(4), 5));

            // プロトコルを使ってブロックを削除
            // Try to remove block using protocol
            var response = GetRemoveBlockResponse(packet.GetPacketResponse(RemoveBlock(new Vector3Int(0, 0), PlayerId), new PacketResponseContext(null)));
            Assert.False(response.Success);
            Assert.AreEqual(RemoveBlockFailureReason.Unknown, response.FailureReason);

            // 新しい仕様：全てのアイテムが入らない場合はブロックは削除されない
            // New spec: Block is not removed if not all items can fit
            Assert.True(worldBlock.Exists(new Vector3Int(0, 0)));

            // プレイヤーのインベントリは変更されていないことを確認
            // Verify player inventory is unchanged
            Assert.AreEqual(itemStackFactory.Create(new ItemId(3), id3MaxStack - 1), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(4), 1), mainInventory.GetItem(1));

            // ブロックのインベントリも変更されていないことを確認
            // Verify block inventory is unchanged
            Assert.AreEqual(itemStackFactory.Create(new ItemId(3), 2), blockInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(4), 5), blockInventory.GetItem(1));
        }
        
        //ブロックの中にアイテムはないけど、プレイヤーのインベントリが満杯でブロックを破壊できない時のテスト
        [Test]
        public void InventoryFullToCantRemoveBlockTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlock = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var mainInventory =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId)
                    .MainOpenableInventory;
            
            //インベントリを満杯にする
            for (var i = 0; i < mainInventory.GetSlotSize(); i++)
                mainInventory.SetItem(i, itemStackFactory.Create(new ItemId(10), 1));

            //ブロックを設置し、返却対象となるアイテムを1つ入れておく（本体返却なし仕様のため）
            //Place a block and put one item inside so there is something to refund (no body refund anymore)
            worldBlock.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            block.GetComponent<IBlockInventory>().InsertItem(itemStackFactory.Create(new ItemId(3), 1), InsertItemContext.Empty);
            
            
            //プロトコルを使ってブロックを削除
            var response = GetRemoveBlockResponse(packet.GetPacketResponse(RemoveBlock(new Vector3Int(0, 0), PlayerId), new PacketResponseContext(null)));
            Assert.False(response.Success);
            Assert.AreEqual(RemoveBlockFailureReason.Unknown, response.FailureReason);
            
            
            //ブロックが削除できていないことを検証
            Assert.True(worldBlock.Exists(new Vector3Int(0, 0)));
        }

        [Test]
        public void TrainRailBlockInUseByTrainCannotBeRemovedTest()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var worldBlock = environment.WorldBlockDatastore;
            var railPos = new Vector3Int(0, 0, 0);

            // 列車が保持するノードを含む橋脚を準備する
            // Prepare a pier whose node is held by a train position.
            var railA = TrainTestHelper.PlaceRail(environment, railPos, BlockDirection.East, out _);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(1, 0, 0), BlockDirection.East);
            ConnectBidirectional(railA, railB, 100);

            // RailPositionを登録して手動削除ガードの監視対象にする
            // Register a RailPosition so the manual removal guard can observe it.
            CreateTrainOnNode(environment, railA.FrontNode);
            var response = GetRemoveBlockResponse(environment.PacketResponseCreator.GetPacketResponse(RemoveBlock(railPos, PlayerId), new PacketResponseContext(null)));
            Assert.False(response.Success);
            Assert.AreEqual(RemoveBlockFailureReason.NodeInUseByTrain, response.FailureReason);

            // ブロックとレール接続が残り、橋脚削除で列車位置が壊れないことを確認する
            // Verify the block and rail connection remain so train position is preserved.
            Assert.True(worldBlock.Exists(railPos));
            Assert.AreNotEqual(-1, railA.FrontNode.GetDistanceToNode(railB.FrontNode));
        }

        [Test]
        public void TrainRailBlockWithoutTrainCanBeRemovedTest()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var worldBlock = environment.WorldBlockDatastore;
            var railPos = new Vector3Int(0, 0, 0);

            // 列車に使われていない橋脚は通常どおり削除できる
            // A pier unused by trains can still be removed normally.
            TrainTestHelper.PlaceRail(environment, railPos, BlockDirection.East);
            var response = GetRemoveBlockResponse(environment.PacketResponseCreator.GetPacketResponse(RemoveBlock(railPos, PlayerId), new PacketResponseContext(null)));
            Assert.True(response.Success);
            Assert.AreEqual(RemoveBlockFailureReason.None, response.FailureReason);

            Assert.False(worldBlock.Exists(railPos));
        }
        
        
        private byte[] RemoveBlock(Vector3Int pos, int playerId)
        {
            return MessagePackSerializer.Serialize(new RemoveBlockProtocolMessagePack(playerId, pos));
        }

        private static RemoveBlockResponseMessagePack GetRemoveBlockResponse(List<byte[]> responsePackets)
        {
            Assert.AreEqual(1, responsePackets.Count);
            return MessagePackSerializer.Deserialize<RemoveBlockResponseMessagePack>(responsePackets[0]);
        }

        private static void ConnectBidirectional(RailComponent from, RailComponent to, int distance)
        {
            // 表裏の方向ペアを接続し、通常のレール接続と同じ形にする
            // Connect both directional pairs to mirror normal rail connection shape.
            from.FrontNode.ConnectNode(to.FrontNode, distance);
            to.FrontNode.ConnectNode(from.FrontNode, distance);
            to.BackNode.ConnectNode(from.BackNode, distance);
            from.BackNode.ConnectNode(to.BackNode, distance);
        }

        private static TrainUnit CreateTrainOnNode(TrainTestEnvironment environment, IRailNode node)
        {
            var (trainCar, _) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 0, 1, 0, true);
            var railPosition = new RailPosition(new List<IRailNode> { node }, trainCar.Length, 0);

            // TrainUnit生成時にTrainRailPositionManagerへRailPositionが登録される
            // TrainUnit construction registers the RailPosition with TrainRailPositionManager.
            return new TrainUnit(
                railPosition,
                new List<TrainCar> { trainCar },
                environment.GetTrainRailPositionManager(),
                environment.GetTrainDiagramManager());
        }
    }
}
