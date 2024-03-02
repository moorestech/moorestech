using System.Reflection;
using Game.Block.Interface;
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
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlockDatastore = saveServiceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = saveServiceProvider.GetService<IBlockFactory>();


            //リフレクションでテスト用のファイル名を変更
            ChangeFilePath(saveServiceProvider.GetService<SaveJsonFileName>(), "SaveJsonAndLoadTest.json");
            Debug.Log(saveServiceProvider.GetService<SaveJsonFileName>().FullSaveFilePath);


            //ブロックの追加
            worldBlockDatastore.AddBlock(blockFactory.Create(1, 10), 0, 0, BlockDirection.North);
            worldBlockDatastore.AddBlock(blockFactory.Create(2, 5), 0, 1, BlockDirection.East);
            worldBlockDatastore.AddBlock(blockFactory.Create(3, 1000), 30, -10, BlockDirection.West);

            saveServiceProvider.GetService<IWorldSaveDataSaver>().Save();


            var (_, loadServiceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);


            //テスト用にファイル名を変更
            //リフレクションでテスト用のファイル名を変更
            ChangeFilePath(loadServiceProvider.GetService<SaveJsonFileName>(), "SaveJsonAndLoadTest.json");
            Debug.Log(loadServiceProvider.GetService<SaveJsonFileName>().FullSaveFilePath);

            loadServiceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();
            var loadWorldBlockDatastore = loadServiceProvider.GetService<IWorldBlockDatastore>();


            //追加したブロックのチェック
            var block = loadWorldBlockDatastore.GetBlock(0, 0);
            Assert.AreEqual(1, block.BlockId);
            Assert.AreEqual(10, block.EntityId);
            Assert.AreEqual(BlockDirection.North, loadWorldBlockDatastore.GetBlockDirection(0, 0));

            block = loadWorldBlockDatastore.GetBlock(0, 1);
            Assert.AreEqual(2, block.BlockId);
            Assert.AreEqual(5, block.EntityId);
            Assert.AreEqual(BlockDirection.East, loadWorldBlockDatastore.GetBlockDirection(0, 1));

            block = loadWorldBlockDatastore.GetBlock(30, -10);
            Assert.AreEqual(3, block.BlockId);
            Assert.AreEqual(1000, block.EntityId);
            Assert.AreEqual(BlockDirection.West, loadWorldBlockDatastore.GetBlockDirection(30, -10));
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