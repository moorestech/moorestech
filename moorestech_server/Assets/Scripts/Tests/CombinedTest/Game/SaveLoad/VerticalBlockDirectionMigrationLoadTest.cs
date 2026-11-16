using System;
using System.IO;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.UnitTest.Game;
using UnityEngine;

namespace Tests.CombinedTest.Game.SaveLoad
{
    // 版9の縦置きが実ロードで同方位の水平へ寝ることを検証
    // Verify a version-9 vertical block lies flat to the same heading on real load
    public class VerticalBlockDirectionMigrationLoadTest
    {
        private static readonly Vector3Int TallPosition = new(3, 0, 5);
        private static readonly Vector3Int CubePosition = new(10, 0, 10);
        // UpEast(1) は新しい East と同じ番号で移行が素通りしても通るため、番号の違う DownEast(9) を使う
        // UpEast(1) shares its number with the new East and would pass even without migration, so use DownEast(9) whose number differs
        private const int LegacyDownEast = 9;
        private const int LegacyFlatOffset = 4;

        private string _archiveRoot;

        [SetUp]
        public void SetUp()
        {
            _archiveRoot = SaveLoadPreparerTestFixture.ArchiveRootForThisRun();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_archiveRoot)) Directory.Delete(_archiveRoot, true);
        }

        [Test]
        public void 版9の縦置きは同じ方位の水平へ寝かされてロードされる()
        {
            var original = BuildVersion9Save().ToString();

            // 一時ファイルをセーブパスにしてロード
            // Load with a temporary file as the save path
            Directory.CreateDirectory(_archiveRoot);
            var sourcePath = WorldDataDirectory.FromWorldRoot(_archiveRoot).SaveJsonFilePath;
            File.WriteAllText(sourcePath, original);
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, sourcePath),
            };
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(options);
            var configuredDirectory = loadProvider.GetRequiredService<WorldDataDirectory>();
            Assert.AreEqual(sourcePath, configuredDirectory.SaveJsonFilePath);
            loadProvider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();

            // 原点保持でEastに寝て最大座標が一致
            // Lies flat to East at the same origin; max cell matches
            var tall = ServerContext.WorldBlockDatastore.GetBlock(TallPosition);
            Assert.IsNotNull(tall, "移行後に縦置きだったブロックが消えています");
            Assert.AreEqual(MultiSizeBlockTest.Block_1x2x3_Id, tall.BlockId);
            Assert.AreEqual(TallPosition, tall.BlockPositionInfo.OriginalPos);
            Assert.AreEqual(BlockDirection.East, tall.BlockPositionInfo.BlockDirection);
            Assert.AreEqual(BlockPositionInfo.CalcBlockMaxPos(TallPosition, BlockDirection.East, new Vector3Int(1, 2, 3)), tall.BlockPositionInfo.MaxPos);
            Assert.AreEqual(BlockDirection.North, ServerContext.WorldBlockDatastore.GetBlock(CubePosition).BlockPositionInfo.BlockDirection);

            // 原本は版9として退避され、再保存は現在版
            // The original is archived as version 9 and the re-save is at the current version
            Assert.AreEqual(original, File.ReadAllText(configuredDirectory.BackupSaveJsonPath(9)));
            var resaved = JObject.Parse(loadProvider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, resaved["worldVersion"].Value<int>());
        }

        // 現行セーブを版9番号へ戻し1×2×3だけDownEastにする
        // Revert a current save to version-9 numbers; only the 1x2x3 becomes DownEast
        private static JObject BuildVersion9Save()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.AreEqual(new Vector3Int(1, 2, 3), MasterHolder.BlockMaster.GetBlockMaster(MultiSizeBlockTest.Block_1x2x3_Id).BlockSize, "テストの前提のブロックサイズが違います");
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(MultiSizeBlockTest.Block_1x2x3_Id, TallPosition, BlockDirection.East, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BlockId, CubePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));

            var save = JObject.Parse(provider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
            foreach (var block in (JArray)save["world"])
            {
                var isTall = block["X"].Value<int>() == TallPosition.x && block["Y"].Value<int>() == TallPosition.y && block["Z"].Value<int>() == TallPosition.z;
                block["direction"] = isTall ? LegacyDownEast : block["direction"].Value<int>() + LegacyFlatOffset;
            }

            save["worldVersion"] = 9;
            return save;
        }
    }
}
