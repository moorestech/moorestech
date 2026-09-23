using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.CombinedTest.Game.BeltSegmentWorld;
using Tests.Module.TestMod;
using UnityEngine;
namespace Tests.CombinedTest.Core
{
    public class ChestLogicTest
    {
        [Test]
        public void BeltConveyorInsertChestLogicTest()
        {
            var f = new BeltWorldFixture();
            var belt = f.Belt(Vector3Int.zero, BlockDirection.North);
            var chest = f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.forward, BlockDirection.North).GetComponent<VanillaChestComponent>();
            f.Seed(belt,1); f.Tick(17);
            Assert.AreEqual(1,chest.GetItem(0).Count);
            Assert.AreEqual(new ItemId(1),chest.GetItem(0).Id);
        }
        [Test]
        public void BeltConveyorOutputChestLogicTest()
        {
            var f = new BeltWorldFixture();
            var chest = f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.back, BlockDirection.North).GetComponent<VanillaChestComponent>();
            var belt = f.Belt(Vector3Int.zero,BlockDirection.North);
            chest.SetItem(0,new ItemId(1),1); f.Tick(1);
            Assert.AreEqual(0,chest.GetItem(0).Count);
            Assert.AreEqual(1,belt.GetItem(0).Count);
        }
    }
}
