using Core.Master;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class ChestSaveLoadTest
    {
        [Test]
        public void SaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var blockFactory = ServerContext.BlockFactory;
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid;
            
            var chestPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var chestBlock = blockFactory.Create(ForUnitTestModBlockId.ChestId, new BlockInstanceId(1), chestPosInfo);
            var chest = chestBlock.GetComponent<VanillaChestComponent>();
            
            
            chest.SetItem(0, new ItemId(1), 7);
            chest.SetItem(2, new ItemId(2), 45);
            chest.SetItem(4, new ItemId(3), 3);
            
            var save = chest.GetSaveState();
            Debug.Log(save);
            
            var chestBlock2 = blockFactory.Load(blockGuid, new BlockInstanceId(1), save, chestPosInfo);
            var chest2 = chestBlock2.GetComponent<VanillaChestComponent>();
            
            Assert.AreEqual(chest.GetItem(0), chest2.GetItem(0));
            Assert.AreEqual(chest.GetItem(2), chest2.GetItem(2));
            Assert.AreEqual(chest.GetItem(4), chest2.GetItem(4));
        }
    }
}