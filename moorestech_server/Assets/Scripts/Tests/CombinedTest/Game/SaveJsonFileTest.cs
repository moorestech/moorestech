using System;
using System.IO;
using System.Reflection;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
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
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;
            
            
            //リフレクションでテスト用のファイル名を変更
            ChangeFilePath(saveServiceProvider.GetService<SaveJsonFilePath>(), "SaveJsonAndLoadTest.json");
            Debug.Log(saveServiceProvider.GetService<SaveJsonFilePath>().Path);
            
            
            //ブロックの追加
            worldBlockDatastore.TryAddBlock((BlockId)1, new Vector3Int(0, 0), BlockDirection.North, out var block0, System.Array.Empty<BlockCreateParam>());
            worldBlockDatastore.TryAddBlock((BlockId)2, new Vector3Int(0, 1), BlockDirection.East, out var block1, System.Array.Empty<BlockCreateParam>());
            worldBlockDatastore.TryAddBlock((BlockId)3, new Vector3Int(30, -10), BlockDirection.West, out var block2, System.Array.Empty<BlockCreateParam>());
            
            saveServiceProvider.GetService<IWorldSaveDataSaver>().Save();
            
            
            var (_, loadServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            
            //テスト用にファイル名を変更
            //リフレクションでテスト用のファイル名を変更
            ChangeFilePath(loadServiceProvider.GetService<SaveJsonFilePath>(), "SaveJsonAndLoadTest.json");
            Debug.Log(loadServiceProvider.GetService<SaveJsonFilePath>().Path);
            
            loadServiceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();
            var loadWorldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // ファイルを削除
            File.Delete(saveServiceProvider.GetService<SaveJsonFilePath>().Path);
            
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
        
        private void ChangeFilePath(SaveJsonFilePath instance, string fileName)
        {
            // Pathプロパティのバッキングフィールドを取得する
            var fieldInfo = typeof(SaveJsonFilePath).GetField("<Path>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            
            // バッキングフィールドの値を更新する
            var path = Path.Combine(Environment.CurrentDirectory, "../", "moorestech_server", fileName);
            fieldInfo.SetValue(instance, path);
        }
    }
}