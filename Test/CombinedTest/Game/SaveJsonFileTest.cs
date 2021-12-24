using Core.Block.BlockFactory;
using Game.Save.Interface;
using Game.Save.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;

namespace Test.CombinedTest.Game
{
    /// <summary>
    /// 実際にファイルに保存、ロードをして正しく動作するかテストする
    /// </summary>
    public class SaveJsonFileTest
    {
        [Test]
        public void SaveJsonAndLoadTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<BlockFactory>();
            //テスト用にファイル名を変更
            saveServiceProvider.GetService<SaveJsonFileName>().ChangeFileName("SaveJsonAndLoadTest.json");
            
            worldBlockDatastore.AddBlock(blockFactory.Create(1, 10), 0, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(2, 5), 0, 1, BlockDirection.East);
            worldBlockDatastore.AddBlock(blockFactory.Create(3, 1000), 30, -10, BlockDirection.West);
            
            saveServiceProvider.GetService<ISaveRepository>().Save();
            
            
            
            var (_, loadServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            //テスト用にファイル名を変更
            loadServiceProvider.GetService<SaveJsonFileName>().ChangeFileName("SaveJsonAndLoadTest.json");
            
            loadServiceProvider.GetService<ILoadRepository>().Load();
            var loadWorldBlockDatastore = loadServiceProvider.GetService<IWorldBlockDatastore>();
            
            var block = loadWorldBlockDatastore.GetBlock(0, 0);
            Assert.AreEqual(1,block.GetBlockId());
            Assert.AreEqual(10,block.GetIntId());
            Assert.AreEqual(BlockDirection.North,loadWorldBlockDatastore.GetBlockDirection(0, 0));
            
            block = loadWorldBlockDatastore.GetBlock(0, 1);
            Assert.AreEqual(2,block.GetBlockId());
            Assert.AreEqual(5,block.GetIntId());
            Assert.AreEqual(BlockDirection.East,loadWorldBlockDatastore.GetBlockDirection(0, 1));
            
            block = loadWorldBlockDatastore.GetBlock(30, -10);
            Assert.AreEqual(3,block.GetBlockId());
            Assert.AreEqual(1000,block.GetIntId());
            Assert.AreEqual(BlockDirection.West,loadWorldBlockDatastore.GetBlockDirection(30, -10));
        }
    }
}