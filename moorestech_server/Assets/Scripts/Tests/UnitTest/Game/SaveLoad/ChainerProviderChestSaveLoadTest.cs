using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.CraftChainer.BlockComponent.ProviderChest;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class ChainerProviderChestSaveLoadTest
    {
        [Test]
        public void SaveLoadTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            var blockFactory = ServerContext.BlockFactory;
            var posInfo = new BlockPositionInfo(new Vector3Int(0, 0, 0), BlockDirection.North, Vector3Int.one);

            // CraftChainerProviderChestブロックを作成
            // Create a CraftChainerProviderChest block
            var providerChestBlock = blockFactory.Create(ForUnitTestModBlockId.CraftChainerProviderChest, new BlockInstanceId(1), posInfo);

            // コンポーネントの取得
            // Get the component
            var originalProviderChestComponent = providerChestBlock.GetComponent<CraftChainerProviderChestComponent>();

            // セーブデータの取得
            // Retrieve the save data
            var saveState = providerChestBlock.GetSaveState();

            // ブロックのロード
            // Load the block
            var loadedBlock = blockFactory.Load(providerChestBlock.BlockGuid, new BlockInstanceId(2), saveState, posInfo);
            var loadedProviderChestComponent = loadedBlock.GetComponent<CraftChainerProviderChestComponent>();

            // NodeIdが正しく保存・ロードされているか確認
            // Check if NodeId is correctly saved and loaded
            Assert.AreEqual(originalProviderChestComponent.NodeId, loadedProviderChestComponent.NodeId);
        }
    }
}