using System;
using System.IO;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
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
            var savePath = Path.Combine(Environment.CurrentDirectory, "../", "moorestech_server", "SaveJsonAndLoadTest.json");

            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
                {
                    worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
                });
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;


            Debug.Log(saveServiceProvider.GetService<WorldDataDirectory>().SaveJsonFilePath);


            //ブロックの追加
            worldBlockDatastore.TryAddBlock((BlockId)1, new Vector3Int(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block0);
            worldBlockDatastore.TryAddBlock((BlockId)2, new Vector3Int(0, 1), BlockDirection.East, Array.Empty<BlockCreateParam>(), out var block1);
            worldBlockDatastore.TryAddBlock((BlockId)3, new Vector3Int(30, -10), BlockDirection.West, Array.Empty<BlockCreateParam>(), out var block2);

            saveServiceProvider.GetRequiredService<IWorldSaveRequest>().RequestSave();
            GameUpdater.UpdateOneTick();


            var (_, loadServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
                {
                    worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
                });


            Debug.Log(loadServiceProvider.GetService<WorldDataDirectory>().SaveJsonFilePath);

            loadServiceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();
            var loadWorldBlockDatastore = ServerContext.WorldBlockDatastore;

            // ファイルを削除
            File.Delete(savePath);

            //追加したブロックのチェック
            var block = loadWorldBlockDatastore.GetBlock(new Vector3Int(0, 0));
            Assert.AreEqual(1, block.BlockId.AsPrimitive());
            Assert.AreEqual(block0.BlockInstanceId, block.BlockInstanceId.AsPrimitive());
            Assert.AreEqual(BlockDirection.North, loadWorldBlockDatastore.GetBlockDirection(new Vector3Int(0, 0)));

            block = loadWorldBlockDatastore.GetBlock(new Vector3Int(0, 1));
            Assert.AreEqual(2, block.BlockId.AsPrimitive());
            Assert.AreEqual(block1.BlockInstanceId, block.BlockInstanceId.AsPrimitive());
            Assert.AreEqual(BlockDirection.East, loadWorldBlockDatastore.GetBlockDirection(new Vector3Int(0, 1)));

            block = loadWorldBlockDatastore.GetBlock(new Vector3Int(30, -10));
            Assert.AreEqual(3, block.BlockId.AsPrimitive());
            Assert.AreEqual(block2.BlockInstanceId, block.BlockInstanceId.AsPrimitive());
            Assert.AreEqual(BlockDirection.West, loadWorldBlockDatastore.GetBlockDirection(new Vector3Int(30, -10)));

        }
    }
}
