using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.GearChainPole;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Core.Master;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class GearChainPoleSaveLoadTest
    {
        [Test]
        public void SaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var blockFactory = ServerContext.BlockFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearChainPole).BlockGuid;
            
            // 複数のチェーンポールブロックを作成してWorldBlockDatastoreに登録する
            // Create multiple chain pole blocks and register them in WorldBlockDatastore
            var pos1 = new Vector3Int(0, 0, 0);
            var pos2 = new Vector3Int(10, 0, 0);
            var pos3 = new Vector3Int(20, 0, 0);
            
            var block1Created = worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, pos1, BlockDirection.North, out var block1);
            var block2Created = worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, pos2, BlockDirection.North, out var block2);
            var block3Created = worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, pos3, BlockDirection.North, out var block3);
            
            Assert.IsTrue(block1Created);
            Assert.IsTrue(block2Created);
            Assert.IsTrue(block3Created);
            
            var chainPole1 = block1.GetComponent<IGearChainPole>();
            var chainPole2 = block2.GetComponent<IGearChainPole>();
            var chainPole3 = block3.GetComponent<IGearChainPole>();
            
            // チェーン接続を追加する
            // Add chain connections
            var connection1to2 = chainPole1.TryAddChainConnection(chainPole2.BlockInstanceId, new GearChainConnectionCost(ItemMaster.EmptyItemId, 0, 0, true));
            var connection1to3 = chainPole1.TryAddChainConnection(chainPole3.BlockInstanceId, new GearChainConnectionCost(ItemMaster.EmptyItemId, 0, 0, true));
            
            Assert.IsTrue(connection1to2);
            Assert.IsTrue(connection1to3);
            
            // 接続が正しく追加されたことを確認する
            // Verify connections are correctly added
            Assert.IsTrue(chainPole1.ContainsChainConnection(chainPole2.BlockInstanceId));
            Assert.IsTrue(chainPole1.ContainsChainConnection(chainPole3.BlockInstanceId));
            
            // セーブデータを取得する
            // Get save data
            var chainPole1Component = block1.GetComponent<GearChainPoleComponent>();
            var save = chainPole1Component.GetSaveState();
            var states = new Dictionary<string, string>() { { chainPole1Component.SaveKey, save } };
            var blockInstanceId1 = block1.BlockInstanceId;
            Debug.Log(save);
            
            // 元のブロックを削除する
            // Remove original block
            var removed = worldBlockDatastore.RemoveBlock(blockInstanceId1, BlockRemoveReason.ManualRemove);
            Assert.IsTrue(removed);
            
            // ブロックをロードする
            // Load block
            var block1Loaded = worldBlockDatastore.TryAddLoadedBlock(blockGuid, blockInstanceId1, states, pos1, BlockDirection.North, out var block1Reloaded);
            Assert.IsTrue(block1Loaded);
            
            var chainPole1Reloaded = block1Reloaded.GetComponent<IGearChainPole>();
            
            // 全てのブロックがロードされた後に、OnPostBlockLoad()を呼び出す
            // Call OnPostBlockLoad() after all blocks are loaded
            var postBlockLoadComponents = block1Reloaded.ComponentManager.GetComponents<IPostBlockLoad>();
            foreach (var component in postBlockLoadComponents)
            {
                component.OnPostBlockLoad();
            }
            
            // 接続が正しく復元されたことを確認する
            // Verify connections are correctly restored
            Assert.IsTrue(chainPole1Reloaded.ContainsChainConnection(chainPole2.BlockInstanceId));
            Assert.IsTrue(chainPole1Reloaded.ContainsChainConnection(chainPole3.BlockInstanceId));
        }
    }
}
