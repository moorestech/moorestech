using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.CraftChainer.BlockComponent;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class ChainerTransporterSaveLoadTest
    {
        [Test]
        public void SaveLoadTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var blockFactory = ServerContext.BlockFactory;
            var posInfo = new BlockPositionInfo(new Vector3Int(0, 0, 0), BlockDirection.North, Vector3Int.one);

            // Transporter blockを作成
            // Create a Transporter block
            var transporterBlock = blockFactory.Create(ForUnitTestModBlockId.CraftChainerTransporter, new BlockInstanceId(1), posInfo);
            var originalTransporter = transporterBlock.GetComponent<CraftChainerTransporterComponent>();

            // セーブデータを取得
            // Get the save data
            var saveState = transporterBlock.GetSaveState();

            // ブロックをロード
            // Load the block
            var loadedBlock = blockFactory.Load(transporterBlock.BlockGuid, new BlockInstanceId(2), saveState, posInfo);
            var loadedTransporterComponent = loadedBlock.GetComponent<CraftChainerTransporterComponent>();

            // ノードIDのチェック
            // Check the node ID
            Assert.AreEqual(originalTransporter.NodeId, loadedTransporterComponent.NodeId);
        }
    }
}
