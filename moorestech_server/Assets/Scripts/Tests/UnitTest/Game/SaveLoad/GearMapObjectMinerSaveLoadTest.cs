using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Miner;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class GearMapObjectMinerSaveLoadTest
    {
        [Test]
        public void SaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var blockFactory = ServerContext.BlockFactory;
            
            // 採掘機の設定
            var minerId = ForUnitTestModBlockId.GearMinerId;
            var minerGuid = MasterHolder.BlockMaster.GetBlockMaster(minerId).BlockGuid;
            var minerPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var minerBlock = blockFactory.Create(minerId, new BlockInstanceId(1), minerPosInfo);
            var minerComponent = minerBlock.GetComponent<VanillaGearMinerComponent>();
            
            // チェストの設定
            var chestId = ForUnitTestModBlockId.ChestId;
            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(chestId).BlockGuid;
            var chestPosInfo = new BlockPositionInfo(new Vector3Int(1, 0), BlockDirection.North, Vector3Int.one);
            var chestBlock = blockFactory.Create(chestId, new BlockInstanceId(2), chestPosInfo);
            var chestComponent = chestBlock.GetComponent<VanillaChestComponent>();
            
            // 状態設定
            minerComponent.SetRemainingMiningTime(5.5f);
            chestComponent.SetItem(0, new ItemId(1), 10);
            chestComponent.SetItem(2, new ItemId(3), 5);
            
            // セーブデータ取得
            var saveStates = new Dictionary<string, string>
            {
                { minerComponent.SaveKey, minerComponent.GetSaveState() },
                { chestComponent.SaveKey, chestComponent.GetSaveState() }
            };
            
            // ブロック再生成
            var loadedMiner = blockFactory.Load(minerGuid, new BlockInstanceId(1), saveStates, minerPosInfo);
            var loadedChest = blockFactory.Load(chestGuid, new BlockInstanceId(2), saveStates, chestPosInfo);
            
            // 検証
            var loadedMinerComponent = loadedMiner.GetComponent<VanillaGearMinerComponent>();
            var loadedChestComponent = loadedChest.GetComponent<VanillaChestComponent>();
            
            Assert.AreEqual(5.5f, loadedMinerComponent.RemainingMiningTime);
            Assert.AreEqual(chestComponent.GetItem(0), loadedChestComponent.GetItem(0));
            Assert.AreEqual(chestComponent.GetItem(2), loadedChestComponent.GetItem(2));
        }
    }
}