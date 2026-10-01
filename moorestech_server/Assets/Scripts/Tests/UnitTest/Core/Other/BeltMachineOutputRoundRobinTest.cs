using System.Collections.Generic;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Tests.Module;
using Tests.Module.TestMod;
using Game.Context;
using static Tests.CombinedTest.Core.Transport.Segment.BeltWorldTransportTest;

namespace Tests.UnitTest.Core.Other
{
    public class BeltMachineOutputRoundRobinTest
    {
        [Test]
        public void RoundRobinSelectionAndResolvedContextTest()
        {
            var transport = CreateWorld();
            var block = Place(ForUnitTestModBlockId.GearBeltConveyorSplitter, 0, 0, BlockDirection.North);
            Place(ForUnitTestModBlockId.ChestId, 0, 1, BlockDirection.North);
            Place(ForUnitTestModBlockId.ChestId, -1, 0, BlockDirection.North);
            Place(ForUnitTestModBlockId.ChestId, 1, 0, BlockDirection.North);
            var connector = block.GetComponent<IBlockConnectorComponent<IBlockInventory>>();
            var resolved = (Dictionary<IBlockInventory, ConnectedInfo>)connector.ConnectedTargets;
            var targets = new Dictionary<DummyBlockInventory, ConnectedInfo>();
            foreach (var info in resolved.Values) targets.Add(new DummyBlockInventory(), info);
            resolved.Clear();
            foreach (var pair in targets) resolved.Add(pair.Key, pair.Value);
            GameUpdater.UpdateOneTick();
            var belt = block.GetComponent<VanillaBeltConveyorComponent>();
            belt.SetTicksOfItemEnterToExit(2);

            // 解決済み接続をそのまま使い、成功した方向だけ順序を進める。
            // Use resolved connections unchanged and rotate only successful output directions.
            for (int i = 0; i < 3; i++)
            {
                belt.SetItem(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));
                GameUpdater.UpdateOneTick();
            }
            foreach (var pair in targets)
            {
                Assert.AreEqual(1, pair.Key.InsertedItems.Count);
                Assert.AreEqual(1, pair.Key.InsertedContexts.Count);
                var context = pair.Key.InsertedContexts[0];
                Assert.AreEqual(block.BlockInstanceId, context.SourceBlockInstanceId);
                Assert.AreSame(pair.Value.SelfConnector, context.SourceConnector);
                Assert.AreSame(pair.Value.TargetConnector, context.TargetConnector);
            }
        }
    }
}
