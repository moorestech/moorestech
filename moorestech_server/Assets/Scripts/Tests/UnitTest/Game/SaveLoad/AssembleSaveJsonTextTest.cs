using Game.Block.Interface;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class AssembleSaveJsonTextTest
    {
        //ブロックを追加した時のテスト
        [Test]
        public void SimpleBlockPlacedTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;
            
            worldBlockDatastore.TryAddBlock(1, Vector3Int.zero, BlockDirection.North, out _);
            worldBlockDatastore.TryAddBlock(2, new Vector3Int(10, -15), BlockDirection.North, out _);
            
            var json = assembleSaveJsonText.AssembleSaveJson();
            
            Debug.Log(json);
            
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(json);
            
            var worldLoadBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var block1 = worldLoadBlockDatastore.GetBlock(new Vector3Int(0, 0));
            Assert.AreEqual(1, block1.BlockId);
            Assert.AreEqual(10, block1.BlockInstanceId.AsPrimitive());
            
            var block2 = worldLoadBlockDatastore.GetBlock(new Vector3Int(10, -15));
            Assert.AreEqual(2, block2.BlockId);
            Assert.AreEqual(100, block2.BlockInstanceId.AsPrimitive());
        }
    }
}