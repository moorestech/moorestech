using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;
namespace Tests.CombinedTest.Core
{
    public class InsertItemContextTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ResolvedSenderTargetContextTest(bool beltSender)
        {
            Initialize();
            var sender = Place(beltSender ? ForUnitTestModBlockId.BeltConveyorId : ForUnitTestModBlockId.ChestId, 0);
            var target = Place(ForUnitTestModBlockId.ChestId, 1);
            var recorder = new DummyBlockInventory();
            var edge = ReplaceReceiver(sender, target, recorder);
            sender.GetComponent<IBlockInventory>().SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));
            WaitAndAssert(recorder, sender.BlockInstanceId, edge);
        }
        [Test]
        public void BeltReceivesResolvedEntryDirectionFromContextTest()
        {
            var transport = Initialize();
            var source = Place(ForUnitTestModBlockId.ChestId, -1);
            var belt = Place(ForUnitTestModBlockId.BeltConveyorId, 0);
            var edge = source.GetComponent<IBlockConnectorComponent<IBlockInventory>>().ConnectedTargets.Single().Value;
            transport.Initialize();
            var context = new InsertItemContext(source.BlockInstanceId, edge.SelfConnector, edge.TargetConnector);
            var facade = belt.GetComponent<VanillaBeltConveyorComponent>();
            Assert.AreEqual(0, facade.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), context).Count);
            GameUpdater.UpdateOneTick();
            var state = transport.CaptureCommittedSnapshot().Snapshot.Items.Single();
            Assert.AreEqual(global::Core.BeltTransport.BeltDirection.Back, state.EntryDirection);
            Assert.AreEqual(0, state.EntryHeight);
        }
        [Test]
        public void ChestThroughBeltUsesLastSenderContextTest()
        {
            Initialize();
            var source = Place(ForUnitTestModBlockId.ChestId, 0);
            var belt = Place(ForUnitTestModBlockId.BeltConveyorId, 1);
            var target = Place(ForUnitTestModBlockId.ChestId, 2);
            var recorder = new DummyBlockInventory();
            var edge = ReplaceReceiver(belt, target, recorder);
            source.GetComponent<IBlockInventory>().SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));
            WaitAndAssert(recorder, belt.BlockInstanceId, edge);
            Assert.AreEqual(0, source.GetComponent<IBlockInventory>().GetItem(0).Count);
        }
        private static BeltWorldTransport Initialize()
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return services.GetRequiredService<BeltWorldTransport>();
        }
        private static IBlock Place(BlockId id, int z)
        {
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(id, new Vector3Int(0, 0, z), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block));
            return block;
        }
        private static ConnectedInfo ReplaceReceiver(IBlock source, IBlock target, DummyBlockInventory recorder)
        {
            // 接続先の在庫だけを記録器へ置換し、resolverの座標・GUID・ブロックは保持する。
            // Replace only the receiving inventory and retain resolved geometry, GUIDs and block identity.
            var connections = (Dictionary<IBlockInventory, ConnectedInfo>)source.GetComponent<IBlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            var pair = connections.Single(p => p.Value.TargetBlock == target);
            connections.Remove(pair.Key);
            connections.Add(recorder, pair.Value);
            return pair.Value;
        }
        private static void WaitAndAssert(DummyBlockInventory recorder, BlockInstanceId sender, ConnectedInfo edge)
        {
            for (int tick = 0; tick < 200 && recorder.InsertedContexts.Count == 0; tick++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(1, recorder.InsertedContexts.Count);
            var context = recorder.InsertedContexts[0];
            Assert.AreEqual(sender, context.SourceBlockInstanceId);
            Assert.AreEqual(edge.SelfConnector.ConnectorGuid, context.SourceConnector.ConnectorGuid);
            Assert.AreEqual(edge.TargetConnector.ConnectorGuid, context.TargetConnector.ConnectorGuid);
        }
    }
}
