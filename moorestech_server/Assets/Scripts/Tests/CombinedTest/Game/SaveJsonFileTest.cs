using System.Reflection;
using Game.Block.Interface;
using Game.Block.Interface;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    /// <summary>
    ///     実際にファイルに保存、ロードをして正しく動作するかテストする
    /// </summary>
    public class SaveJsonFileTest
    {
        [Test]
        public void SaveJsonAndLoadTest()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;


            //リフレクションでテスト用のファイル名を変更
            ChangeFilePath(saveServiceProvider.GetService<SaveJsonFileName>(), "SaveJsonAndLoadTest.json");
            Debug.Log(saveServiceProvider.GetService<SaveJsonFileName>().FullSaveFilePath);


            //ブロックの追加
            worldBlockDatastore.AddBlock(blockFactory.Create(1, 10, new BlockPositionInfo(new Vector3Int(0 ,0), BlockDirection.North, Vector3Int.one)));
            worldBlockDatastore.AddBlock(blockFactory.Create(2, 5, new BlockPositionInfo(new Vector3Int(0 ,1), BlockDirection.East, Vector3Int.one)));
            worldBlockDatastore.AddBlock(blockFactory.Create(3, 1000, new BlockPositionInfo(new Vector3Int(30 ,-10), BlockDirection.West, Vector3Int.one)));

            saveServiceProvider.GetService<IWorldSaveDataSaver>().Save();


            var (_, loadServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);


            //テスト用にファイル名を変更
            //リフレクションでテスト用のファイル名を変更
            ChangeFilePath(loadServiceProvider.GetService<SaveJsonFileName>(), "SaveJsonAndLoadTest.json");
            Debug.Log(loadServiceProvider.GetService<SaveJsonFileName>().FullSaveFilePath);

            loadServiceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();
            var loadWorldBlockDatastore = ServerContext.WorldBlockDatastore;


            //追加したブロックのチェック
            var block = loadWorldBlockDatastore.GetBlock(new Vector3Int(0,  0));
            Assert.AreEqual(1, block.BlockId);
            Assert.AreEqual(10, block.EntityId);
            Assert.AreEqual(BlockDirection.North, loadWorldBlockDatastore.GetBlockDirection(new Vector3Int(0, 0)));

            block = loadWorldBlockDatastore.GetBlock(new Vector3Int(0,  1));
            Assert.AreEqual(2, block.BlockId);
            Assert.AreEqual(5, block.EntityId);
            Assert.AreEqual(BlockDirection.East, loadWorldBlockDatastore.GetBlockDirection(new Vector3Int(0, 1)));

            block = loadWorldBlockDatastore.GetBlock(new Vector3Int(30,  -10));
            Assert.AreEqual(3, block.BlockId);
            Assert.AreEqual(1000, block.EntityId);
            Assert.AreEqual(BlockDirection.West, loadWorldBlockDatastore.GetBlockDirection(new Vector3Int(30, -10)));
        }

        private void ChangeFilePath(SaveJsonFileName instance, string fileName)
        {
            // バッキングフィールドを取得する
            var fieldInfo = typeof(SaveJsonFileName).GetField("<FullSaveFilePath>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

            // バッキングフィールドの値を更新する
            fieldInfo.SetValue(instance, fileName);
        }
    }
}