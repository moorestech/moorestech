using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.CraftChainer.BlockComponent.Computer;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class ChainerMainComputerSaveLoadTest
    {
        [Test]
        public void SaveLoadTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var blockFactory = ServerContext.BlockFactory;
            var posInfo = new BlockPositionInfo(new Vector3Int(0, 0, 0), BlockDirection.North, Vector3Int.one);
            
            // ChainerMainComputerブロックを作成
            // Create a ChainerMainComputer block
            var mainComputerBlock = blockFactory.Create(ForUnitTestModBlockId.CraftChainerMainComputer, new BlockInstanceId(1), posInfo);
            var originalMainComputerComponent = mainComputerBlock.GetComponent<CraftChainerMainComputerComponent>();
            
            
            // セーブデータを取得
            // Get the save data
            var saveState = mainComputerBlock.GetSaveState();
            
            // ブロックをロード
            // Load the block
            var loadedBlock = blockFactory.Load(mainComputerBlock.BlockGuid, new BlockInstanceId(2), saveState, posInfo);
            var loadedMainComputerComponent = loadedBlock.GetComponent<CraftChainerMainComputerComponent>();
            
            // NodeIdが正しく保存・ロードされているか確認
            // Check if NodeId is correctly saved and loaded
            Assert.AreEqual(originalMainComputerComponent.NodeId, loadedMainComputerComponent.NodeId);
        }
    }
}