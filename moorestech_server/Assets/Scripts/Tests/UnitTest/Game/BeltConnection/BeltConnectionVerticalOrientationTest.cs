using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Connection;
using Game.Block.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.BeltConnection.Fixtures;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.BeltConnection
{
    public class BeltConnectionVerticalOrientationTest
    {
        public enum BeltKind { NormalFlat, NormalUp, NormalDown, GearFlat, GearUp, GearDown, Splitter }

        private static IEnumerable<TestCaseData> VerticalCases()
        {
            var directions = new[] { BlockDirection.UpNorth, BlockDirection.UpEast, BlockDirection.UpSouth, BlockDirection.UpWest,
                BlockDirection.DownNorth, BlockDirection.DownEast, BlockDirection.DownSouth, BlockDirection.DownWest };
            foreach (var direction in directions)
                foreach (BeltKind kind in Enum.GetValues(typeof(BeltKind)))
                    yield return new TestCaseData(direction, kind);
        }

        [TestCaseSource(nameof(VerticalCases))]
        public void VerticalBeltsPreserveTheTouchingLowerConnection(BlockDirection direction, BeltKind kind)
        {
            foreach (var upperSlot in new[] { "UL", "UR" })
            {
                var world = new BeltEdgeTestWorld(false, BlockDirection.North);
                string[] expected;
                // 送出側と受入側の両方で上側候補から除外する
                // Exclude vertical upper candidates on both the source and receiving sides
                if (upperSlot == "UL")
                {
                    world.Place("LL", 2);
                    world.Place("UR", 1);
                    expected = new[] { "LL>UR" };
                }
                else
                {
                    world.Place("UL", 1);
                    world.Place("LR", 3);
                    expected = new[] { "UL>LR" };
                }
                world.AssertEdges(expected, "before vertical placement");
                var position = world.Position(upperSlot);
                ExpectOrientationLog(direction);
                Assert.IsTrue(world.World.TryAddBlock(GetBlockId(kind), position, direction, Array.Empty<BlockCreateParam>(), out var vertical));
                Assert.IsTrue(BeltInventoryConnectionData.TryGet(vertical, out var context));
                Assert.AreEqual(0, context.Edges.Count);
                Assert.AreEqual(0, BeltEdgeTestWorld.Connector(vertical).ConnectedTargets.Count, "Vertical belt must not connect to itself or another belt.");
                // 空edgeで絞り込む前に全sourceの実辞書を検査する
                // Inspect every source dictionary before filtering by physical edges
                foreach (var slot in BeltEdgeTestWorld.Slots)
                {
                    var block = world.World.GetBlock(world.Position(slot));
                    if (block == null) continue;
                    Assert.IsFalse(BeltEdgeTestWorld.Connector(block).ConnectedTargets.Values.Any(info => ReferenceEquals(info.TargetBlock, vertical)),
                        $"{slot} must not have an incoming connection to the vertical belt.");
                }
                world.AssertEdges(expected, "vertical upper candidate excluded");

                // 除外した上側の撤去でも既存接続は維持する
                // Removing the excluded upper block also preserves the existing connection
                Assert.IsTrue(world.World.RemoveBlock(position, BlockRemoveReason.ManualRemove));
                world.AssertEdges(expected, "vertical upper candidate removed");
                world.Clear();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void VerticalBeltHasNoSharedEdgeWithChest(bool chestFirst)
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            if (chestFirst) PlaceChest();
            ExpectOrientationLog(BlockDirection.UpNorth);
            Assert.IsTrue(world.World.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.UpNorth,
                Array.Empty<BlockCreateParam>(), out var belt));
            if (!chestFirst) PlaceChest();
            var chest = world.World.GetBlock(Vector3Int.up);
            Assert.IsEmpty(BeltEdgeTestWorld.Connector(belt).ConnectedTargets);
            Assert.IsEmpty(BeltEdgeTestWorld.Connector(chest).ConnectedTargets);
            world.World.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);
            world.World.RemoveBlock(Vector3Int.up, BlockRemoveReason.ManualRemove);

            #region Internal
            void PlaceChest() => Assert.IsTrue(world.World.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.up,
                BlockDirection.UpNorth, Array.Empty<BlockCreateParam>(), out _));
            #endregion
        }

        private static void ExpectOrientationLog(BlockDirection direction) => LogAssert.Expect(LogType.Log,
            new Regex("Belt inventory edges require a horizontal block orientation: " + direction));

        private static BlockId GetBlockId(BeltKind kind) => kind switch
        {
            BeltKind.NormalFlat => ForUnitTestModBlockId.BeltConveyorId,
            BeltKind.NormalUp => BeltTestMaster.Up,
            BeltKind.NormalDown => BeltTestMaster.Down,
            BeltKind.GearFlat => ForUnitTestModBlockId.GearBeltConveyor,
            BeltKind.GearUp => ForUnitTestModBlockId.TestGearBeltConveyorUp,
            BeltKind.GearDown => ForUnitTestModBlockId.TestGearBeltConveyorDown,
            BeltKind.Splitter => ForUnitTestModBlockId.FilterSplitter,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown test belt kind.")
        };
    }
}
