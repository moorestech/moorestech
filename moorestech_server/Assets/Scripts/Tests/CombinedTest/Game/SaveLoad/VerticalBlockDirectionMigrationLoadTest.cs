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
    // 版9の縦置き（DownEast）が実ロードで同じ方位の水平（East）に寝かされ、水平のブロックは向きを保つことを検証
    // Verify a version-9 vertical block (DownEast) is laid flat to the same heading (East) by a real load, while flat blocks keep their heading
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

            // 一時ファイルを実セーブパスに指定してロードする
            // Point the DI save path at a temporary file and load it
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

            // 縦置きは原点を保ったまま East に寝て、占有の最大座標も East の計算と一致する
            // The vertical block lies flat to East at the same origin, and its max cell matches the East computation
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
            Assert.AreEqual(10, WorldSaveAllInfo.CurrentVersion);
        }

        // 現行の本物のセーブの向きを版9の番号へ戻し、1×2×3 だけを DownEast にして版9と名乗らせる
        // Turn a real current save's directions back into version-9 numbers, make only the 1x2x3 DownEast, and label it version 9
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
