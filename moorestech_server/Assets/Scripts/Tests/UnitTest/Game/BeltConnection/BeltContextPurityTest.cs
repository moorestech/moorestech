using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.BeltConveyor.Connection;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using NUnit.Framework;

namespace Tests.UnitTest.Game.BeltConnection
{
    public class BeltContextPurityTest
    {
        [Test]
        public void GetOverrideReturnsCompleteSelfResultWithoutChangingAnyConnector()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var lower = world.Place("LL", 2);
            var upper = world.Place("UL", 1);
            var target = world.Place("UR", 1);
            var lowerConnector = BeltEdgeTestWorld.Connector(lower);
            var current = (Dictionary<IBlockInventory, ConnectedInfo>)lowerConnector.ConnectedTargets;
            var upperCurrent = BeltEdgeTestWorld.Connector(upper).ConnectedTargets;
            var upperEnumerator = upperCurrent.GetEnumerator();
            Assert.IsTrue(upperEnumerator.MoveNext());
            var previous = upperEnumerator.Current;
            var targetCurrent = BeltEdgeTestWorld.Connector(target).ConnectedTargets;
            var targetEnumerator = targetCurrent.GetEnumerator();
            var ordinary = new Dictionary<IBlockInventory, ConnectedInfo>();
            var context = new BeltInventoryConnectionContext();

            // 撤去を仮定した計算はworldも自分・他者の辞書も変更しない
            // Calculating a hypothetical removal changes neither the world nor self or other dictionaries
            var desired = context.GetOverride(current, upper, lowerConnector.Data, world.World, upper, ordinary);
            Assert.AreSame(target, desired.Single().Value.TargetBlock);
            Assert.AreNotSame(current, desired);
            Assert.AreNotSame(ordinary, desired);
            Assert.IsEmpty(current);
            Assert.IsEmpty(ordinary);
            Assert.AreSame(upper, world.World.GetBlock(world.Position("UL")));
            Assert.AreSame(previous.Value.SelfConnector, upperCurrent[previous.Key].SelfConnector);
            Assert.DoesNotThrow(() => upperEnumerator.MoveNext());
            Assert.DoesNotThrow(() => targetEnumerator.MoveNext());
            world.AssertEdges(new[] { "UL>UR" }, "pure calculation");

            // 同じ共有Contextへ別sourceを渡しても前回の入力を保持しない
            // The same shared context retains no previous input when called for another source
            var upperConnector = BeltEdgeTestWorld.Connector(upper);
            var upperDesired = context.GetOverride((Dictionary<IBlockInventory, ConnectedInfo>)upperCurrent, lower,
                upperConnector.Data, world.World, null, ordinary);
            Assert.AreSame(target, upperDesired.Single().Value.TargetBlock);
            desired.Clear();
            Assert.AreEqual(1, upperCurrent.Count);
            Assert.AreEqual(1, upperDesired.Count);
            Assert.IsEmpty(context.GetOverride(current, target, lowerConnector.Data, world.World, target, ordinary));
            world.Clear();
        }
    }
}
