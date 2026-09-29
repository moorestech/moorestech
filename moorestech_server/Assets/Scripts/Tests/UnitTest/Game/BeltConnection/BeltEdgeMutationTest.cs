using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.WorldMutation;
using NUnit.Framework;

namespace Tests.UnitTest.Game.BeltConnection
{
    public class BeltEdgeMutationTest
    {
        [Test]
        public void EveryPlacementOrderConvergesAndRemovalRestoresThirdPartySource()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var codes = new[] { 1, 1, 2, 3 };
            foreach (var order in Permutations(new[] { 0, 1, 2, 3 }))
            {
                foreach (var i in order) world.Place(BeltEdgeTestWorld.Slots[i], codes[i]);
                world.AssertEdges(new[] { "UL>UR" }, string.Join(",", order));
                // 上側撤去で変更対象外の下側sourceが復帰する
                // Removing the upper belt restores the unchanged lower source
                world.Remove("UL");
                world.AssertEdges(new[] { "LL>UR" }, "upper source removed");
                world.Remove("UR");
                world.AssertEdges(new string[0], "peak");
                world.Place("UL", 1);
                world.AssertEdges(new[] { "UL>LR" }, "replaced source");
                world.Clear();
            }
        }

        [Test]
        public void InvalidUpperPairSuppressesLowerConnectionUntilRemoved()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var lower = world.Place("LL", 2);
            world.Place("UR", 2);
            world.AssertEdges(new[] { "LL>UR" }, "before");
            world.Place("UL", 3);
            world.AssertEdges(new string[0], "valley blocks fallback");
            Assert.AreEqual(0, BeltEdgeTestWorld.Connector(lower).ConnectedTargets.Count);
            world.Remove("UL");
            world.AssertEdges(new[] { "LL>UR" }, "restored");
            world.Clear();
        }

        [Test]
        public void UnchangedConnectionKeepsDictionaryAndEntry()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var source = world.Place("UL", 1);
            world.Place("UR", 1);
            var connector = BeltEdgeTestWorld.Connector(source);
            var dictionary = connector.ConnectedTargets;
            var enumerator = dictionary.GetEnumerator();
            Assert.IsTrue(enumerator.MoveNext());
            // 同一接続は辞書versionを進めず、その場で維持する
            // Identical connections preserve the dictionary version in place
            connector.CaptureWorldMutation().ApplyAfterMutation();
            world.Place("LL", 2);
            world.Remove("LL");
            Assert.AreSame(dictionary, connector.ConnectedTargets);
            Assert.DoesNotThrow(() => enumerator.MoveNext());
            world.Clear();
        }

        [Test]
        public void WorldsAreIsolatedAndLoadOrderConverges()
        {
            var first = new BeltEdgeTestWorld(false, BlockDirection.North);
            first.Place("UL", 1);
            first.Place("UR", 1);
            first.Place("LL", 2);
            first.Place("LR", 3);
            var save = first.World.GetSaveJsonObject();
            var snapshot = BeltEdgeTestWorld.Connector(first.World.GetBlock(first.Position("UL"))).CaptureWorldMutation();
            var second = new BeltEdgeTestWorld(false, BlockDirection.North);
            second.Place("LL", 2);
            second.Place("UR", 1);
            // 旧worldのsnapshotを新world作成後に適用しても新worldへ触れない
            // Applying an old-world snapshot after creating another world cannot touch the new world
            snapshot.ApplyAfterMutation();
            second.AssertEdges(new[] { "LL>UR" }, "isolated world");
            second.Clear();
            save.Reverse();
            second.World.LoadBlockDataList(save);
            var loadedSource = second.World.GetBlock(second.Position("UL"));
            var loadedTarget = second.World.GetBlock(second.Position("UR"));
            Assert.AreSame(loadedTarget, BeltEdgeTestWorld.Connector(loadedSource).ConnectedTargets.Single().Value.TargetBlock);
            Assert.AreEqual(0, BeltEdgeTestWorld.Connector(second.World.GetBlock(second.Position("LL"))).ConnectedTargets.Count);
        }

        private static IEnumerable<int[]> Permutations(int[] values)
        {
            if (values.Length == 0)
            {
                yield return values;
                yield break;
            }
            foreach (var value in values)
                foreach (var rest in Permutations(values.Where(v => v != value).ToArray()))
                    yield return new[] { value }.Concat(rest).ToArray();
        }
    }
}
