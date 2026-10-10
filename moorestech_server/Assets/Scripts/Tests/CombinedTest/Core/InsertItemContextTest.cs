using System;
using System.Collections.Generic;
using System.Linq;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor.Connection;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Blocks.Chest;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    /// InsertItemContextが正しく設定されるかテスト
    /// Test that InsertItemContext is correctly set
    /// </summary>
    public class InsertItemContextTest
    {
        /// <summary>
        /// ベルトコンベアからターゲットにアイテムが転送される際、InsertItemContextが正しく設定されるかテスト
        /// Test that InsertItemContext is correctly set when items are transferred from belt conveyor to target
        /// </summary>
        [Test]
        public void BeltConveyorToTargetInsertContextTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 搬入チェスト→ベルト1マス→搬出チェストを組み、ベルトの搬出接続を取り出す
            // Build source chest -> one belt cell -> target chest and take the belt's output connection
            Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North);
            var belt = Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North);
            var target = Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 1), BlockDirection.North);
            ServerContext.GetService<BeltTransportDatastore>().RebuildIfDirty();

            // 搬出は送り元ベルトのblock・ベルトの出力コネクター・チェストの入力コネクターを文脈として渡す
            // The handoff carries the emitting belt block, the belt's output connector and the chest's input connector as its context
            var dummyTarget = HandOverThroughOutputLinkTo(target, Vector3Int.zero);
            Assert.AreEqual(1, dummyTarget.InsertedContexts.Count);
            var context = dummyTarget.InsertedContexts[0];
            Assert.AreEqual(belt.BlockInstanceId, context.SourceBlockInstanceId);
            Assert.AreEqual(BeltOutputConnectorGuid(belt), context.SourceConnector.ConnectorGuid);
            Assert.AreEqual(ChestInputConnectorGuid(target), context.TargetConnector.ConnectorGuid);
        }

        /// <summary>
        /// チェストからターゲットにアイテムが転送される際、InsertItemContextが正しく設定されるかテスト
        /// Test that InsertItemContext is correctly set when items are transferred from chest to target
        /// </summary>
        [Test]
        public void ChestToTargetInsertContextTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;

            // チェストを作成（WorldBlockDatastoreに登録）
            // Create chest (registered in WorldBlockDatastore)
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest);
            var chestBlockInstanceId = chest.BlockInstanceId;
            var chestComponent = chest.GetComponent<VanillaChestComponent>();

            // ターゲットとしてDummyBlockInventoryを使用（InsertItemContextを記録）
            // Use DummyBlockInventory as target (records InsertItemContext)
            var dummyTarget = new DummyBlockInventory();

            // チェスト→ターゲットの接続を設定
            // Set up chest → target connection
            // 接続一覧はベルト構成の再構築でも走査されるため、相手blockには離れた位置の実チェストを代役に置く
            // The connection list is also walked by the belt layout rebuild, so a real chest placed far away stands in as the partner block
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(10, 0, 10), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var standInTargetBlock);
            var selfConnector = CreateInventoryConnector(0);
            var targetConnector = CreateInventoryConnector(1);
            var connectedInfo = new ConnectedInfo(selfConnector, targetConnector, standInTargetBlock);

            var chestConnectorComponent = chest.GetComponent<BlockConnectorComponent<IBlockInventory, BeltInventoryConnectionContext>>();
            var connectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)chestConnectorComponent.ConnectedTargets;
            connectInventory.Clear();
            connectInventory.Add(dummyTarget, connectedInfo);

            // チェストにアイテムを設定
            // Set item to chest
            var item = itemStackFactory.Create(new ItemId(1), 1);
            chestComponent.SetItem(0, item);

            // アイテムがターゲットに転送されるまで待つ
            // Wait until item is transferred to target
            while (dummyTarget.InsertedContexts.Count == 0) GameUpdater.UpdateOneTick();

            // InsertItemContextが正しく設定されていることを確認
            // Verify InsertItemContext is correctly set
            Assert.AreEqual(1, dummyTarget.InsertedContexts.Count);
            var context = dummyTarget.InsertedContexts[0];

            // SourceBlockInstanceIdがチェストのBlockInstanceIdと一致すること
            // SourceBlockInstanceId matches chest's BlockInstanceId
            Assert.AreEqual(chestBlockInstanceId, context.SourceBlockInstanceId);

            // SourceConnectorが正しく設定されていること
            // SourceConnector is correctly set
            Assert.IsNotNull(context.SourceConnector);
            Assert.AreEqual(selfConnector.ConnectorGuid, context.SourceConnector.ConnectorGuid);

            // TargetConnectorが正しく設定されていること
            // TargetConnector is correctly set
            Assert.IsNotNull(context.TargetConnector);
            Assert.AreEqual(targetConnector.ConnectorGuid, context.TargetConnector.ConnectorGuid);
        }

        /// <summary>
        /// ベルトコンベアは接続された機械からの押し込みだけを受け付けるテスト
        /// Test that a belt conveyor accepts pushes only from a connected machine
        /// </summary>
        [Test]
        public void BeltConveyorAcceptsOnlyConnectedMachinePushTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            // ベルトの背面に接続チェスト、離れた場所に未接続チェストを置く
            // Put a connected chest behind the belt and an unconnected chest far away
            var connected = Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North);
            var belt = Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North);
            var unconnected = Place(ForUnitTestModBlockId.ChestId, new Vector3Int(10, 0, 10), BlockDirection.North);
            ServerContext.GetService<BeltTransportDatastore>().RebuildIfDirty();
            var beltInventory = Inlet(belt);

            // 文脈なし・未接続の送り元は面の受け口が無いので、そのまま差し戻される
            // No context or an unconnected source has no port for that face, so the stack comes back unchanged
            var item = itemStackFactory.Create(new ItemId(1), 2);
            Assert.AreEqual(2, beltInventory.InsertItem(item, InsertItemContext.Empty).Count);
            Assert.AreEqual(2, beltInventory.InsertItem(item, new InsertItemContext(unconnected.BlockInstanceId, null, null)).Count);
            Assert.AreEqual(0, ItemsOnSegmentAt(Vector3Int.zero).Length);

            // 接続された機械からの押し込みは1個だけ入る
            // A push from the connected machine enters exactly one item
            Assert.AreEqual(1, beltInventory.InsertItem(item, new InsertItemContext(connected.BlockInstanceId, null, null)).Count);
            Assert.AreEqual(1, ItemsOnSegmentAt(Vector3Int.zero).Length);
        }

        /// <summary>
        /// チェスト→複数マスのベルトコンベア→ターゲットの経路で、搬出の送り元が末尾マスのベルトになるかテスト
        /// Test that on chest -> multi-cell belt -> target, the handoff source is the belt of the last cell
        /// </summary>
        [Test]
        public void ChestToBeltConveyorToTargetInsertContextTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 入力チェスト→ベルト2マス→出力チェスト
            // Input chest -> two belt cells -> output chest
            var inputChest = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North);
            var lastBelt = Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            var target = Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 2), BlockDirection.North);
            inputChest.SetItem(0, ServerContext.ItemStackFactory.Create(new ItemId(1), 1));

            // 2マス・速度6: 進入距離1で出口まで511。85tick後に残り1、86tick目に出力チェストへ届く
            // Two cells at speed 6: 511 to the exit; 1 away after 85 ticks, reaching the output chest on tick 86
            GameUpdater.RunFrames(85);
            Assert.AreEqual(0, CountOf(Inventory(target), new ItemId(1)));
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, CountOf(Inventory(target), new ItemId(1)));

            // 2マスは1つのsegmentになり、搬出の送り元は先頭でなく末尾マスのベルトになる
            // The two cells form one segment, and the handoff source is the last cell's belt, not the head
            var context = HandOverThroughOutputLinkTo(target, Vector3Int.zero).InsertedContexts[0];
            Assert.AreEqual(lastBelt.BlockInstanceId, context.SourceBlockInstanceId);
            Assert.AreEqual(BeltOutputConnectorGuid(lastBelt), context.SourceConnector.ConnectorGuid);
            Assert.AreEqual(ChestInputConnectorGuid(target), context.TargetConnector.ConnectorGuid);
        }

        // 指定マスのsegmentから対象への搬出接続を、受け手だけDummyに差し替えた受け口へ1個渡す
        // Hand one item through the segment's output connection to the target, with only the receiver swapped for a dummy
        private static DummyBlockInventory HandOverThroughOutputLinkTo(IBlock target, Vector3Int segmentCell)
        {
            var layout = Assembly().Layouts.Single(l => l.Cells.Any(cell => cell.Position == segmentCell));
            var link = layout.Outputs.Single(o => o.IsMachine && ReferenceEquals(o.Connection.PartnerBlock, target));
            var c = link.Connection;
            var dummy = new DummyBlockInventory();
            var connection = new BeltTopologyConnection(c.Direction, c.EntryDirection, c.PartnerKind, c.PartnerBlock, c.PartnerCell, c.SourceConnector, c.TargetConnector, dummy);
            var receiver = new BeltMachineReceiver(layout.Cells[layout.Cells.Length - 1].BlockInstanceId, connection);
            Assert.IsTrue(receiver.TryReceive(link.Direction, 1, new BeltItem(new ItemId(1), ItemInstanceId.Create(), link.EntryDirection)));
            return dummy;
        }

        private static Guid BeltOutputConnectorGuid(IBlock belt)
        {
            return ((BeltConveyorBlockParam)belt.BlockMasterElement.BlockParam).InventoryConnectors.OutputConnects[0].ConnectorGuid;
        }

        private static Guid ChestInputConnectorGuid(IBlock chest)
        {
            return ((ChestBlockParam)chest.BlockMasterElement.BlockParam).InventoryConnectors.InputConnects[0].ConnectorGuid;
        }

        private static IBlockConnector CreateInventoryConnector(int index)
        {
            return new OutputConnectsElement(index, Guid.NewGuid(), null, Vector3Int.zero, Array.Empty<Vector3Int>());
        }
    }
}
