using System;
using System.Collections.Generic;
using System.IO;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Tests.Investigation
{
    public class StartupPerformanceInvestigationTest
    {
        [Test]
        public void ProfileCurrentSaveStartupLoad()
        {
            var serverDataDirectory = ServerDirectory.GetDirectory();
            var saveFilePath = MoorestechServerDIContainerOptions.DefaultSaveJsonFilePath;
            Assert.That(Directory.Exists(serverDataDirectory), Is.True, serverDataDirectory);
            Assert.That(File.Exists(saveFilePath), Is.True, saveFilePath);

            // 実セーブと実modで起動経路を再現する
            // Reproduce the startup path with the real save and real mod.
            var readStopwatch = Stopwatch.StartNew();
            var saveJson = File.ReadAllText(saveFilePath);
            readStopwatch.Stop();
            Debug.Log($"[StartupProfile] ReadCurrentSave elapsedMs={readStopwatch.Elapsed.TotalMilliseconds:F3} path={saveFilePath} bytes={new FileInfo(saveFilePath).Length}");

            var createStopwatch = Stopwatch.StartNew();
            var options = new MoorestechServerDIContainerOptions(serverDataDirectory)
            {
                saveJsonFilePath = new SaveJsonFilePath(saveFilePath)
            };
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(options);
            createStopwatch.Stop();
            Debug.Log($"[StartupProfile] CreateContainer elapsedMs={createStopwatch.Elapsed.TotalMilliseconds:F3} serverDataDirectory={serverDataDirectory}");

            // ブロック復元内の重なり判定だけを分離して測る
            // Measure only overlap checks inside block restoration.
            var saveData = JsonConvert.DeserializeObject<WorldSaveAllInfoV1>(saveJson);
            ProbeBlockOverlapOnly(saveData);

            var loadStopwatch = Stopwatch.StartNew();
            serviceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();
            loadStopwatch.Stop();
            Debug.Log($"[StartupProfile] LoadOrInitialize elapsedMs={loadStopwatch.Elapsed.TotalMilliseconds:F3}");

            MeasureTicks(1);
            MeasureTicks(20);

            serviceProvider.Dispose();
            GameUpdater.Dispose();
        }

        [Test]
        public void ProfileCurrentSaveBlockFactoryLoadOnly()
        {
            var serverDataDirectory = ServerDirectory.GetDirectory();
            var saveFilePath = MoorestechServerDIContainerOptions.DefaultSaveJsonFilePath;
            Assert.That(Directory.Exists(serverDataDirectory), Is.True, serverDataDirectory);
            Assert.That(File.Exists(saveFilePath), Is.True, saveFilePath);

            // DIとマスターだけ作り、WorldBlockDatastoreには登録しない
            // Build DI and master data, but do not register blocks to WorldBlockDatastore.
            var options = new MoorestechServerDIContainerOptions(serverDataDirectory)
            {
                saveJsonFilePath = new SaveJsonFilePath(saveFilePath)
            };
            var saveJson = File.ReadAllText(saveFilePath);
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(options);
            var saveData = JsonConvert.DeserializeObject<WorldSaveAllInfoV1>(saveJson);

            ProbeBlockFactoryLoadOnly(saveData);

            serviceProvider.Dispose();
            GameUpdater.Dispose();
        }

        private static void ProbeBlockOverlapOnly(WorldSaveAllInfoV1 saveData)
        {
            var positions = new List<BlockPositionInfo>(saveData.World.Count);
            long comparisons = 0;
            var overlaps = 0;
            var stopwatch = Stopwatch.StartNew();

            // TryAddBlockと同じ順序で重なり判定を再現する
            // Reproduce overlap checks in the same order as TryAddBlock.
            foreach (var blockSave in saveData.World)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(blockSave.BlockGuid);
                var direction = (BlockDirection)blockSave.Direction;
                var size = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
                var positionInfo = new BlockPositionInfo(blockSave.Pos, direction, size);

                // 実装上は既存全ブロックを線形走査する
                // The current implementation linearly scans all existing blocks.
                for (var i = 0; i < positions.Count; i++)
                {
                    comparisons++;
                    if (positions[i].IsOverlap(positionInfo)) overlaps++;
                }
                positions.Add(positionInfo);
            }

            stopwatch.Stop();
            Debug.Log($"[StartupProfile] BlockOverlapOnly elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3} blocks={saveData.World.Count} comparisons={comparisons} overlaps={overlaps}");
        }

        private static void ProbeBlockFactoryLoadOnly(WorldSaveAllInfoV1 saveData)
        {
            var durationsByType = new Dictionary<string, double>();
            var countsByType = new Dictionary<string, int>();
            var blockFactory = ServerContext.BlockFactory;
            var stopwatch = Stopwatch.StartNew();

            // 保存ブロックからテンプレート復元だけを実行する
            // Execute only template restoration from saved blocks.
            foreach (var blockSave in saveData.World)
            {
                var blockElement = MasterHolder.BlockMaster.GetBlockMaster(blockSave.BlockGuid);
                var blockId = MasterHolder.BlockMaster.GetBlockId(blockSave.BlockGuid);
                var direction = (BlockDirection)blockSave.Direction;
                var size = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
                var positionInfo = new BlockPositionInfo(blockSave.Pos, direction, size);
                var blockType = blockElement.BlockType;

                // 型別集計で重いテンプレートを特定する
                // Aggregate by block type to find heavy templates.
                var blockStopwatch = Stopwatch.StartNew();
                var block = blockFactory.Load(blockSave.BlockGuid, new BlockInstanceId(blockSave.InstanceId), blockSave.ComponentStates, positionInfo);
                blockStopwatch.Stop();
                Assert.That(block, Is.Not.Null);
                AddTypeDuration(blockType, blockStopwatch.Elapsed.TotalMilliseconds, durationsByType, countsByType);
            }

            stopwatch.Stop();
            Debug.Log($"[StartupProfile] BlockFactoryLoadOnly elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3} blocks={saveData.World.Count}");
            LogTypeDurations(durationsByType, countsByType);
        }

        private static void AddTypeDuration(string blockType, double elapsedMs, Dictionary<string, double> durationsByType, Dictionary<string, int> countsByType)
        {
            if (!durationsByType.ContainsKey(blockType))
            {
                durationsByType.Add(blockType, 0);
                countsByType.Add(blockType, 0);
            }
            durationsByType[blockType] += elapsedMs;
            countsByType[blockType]++;
        }

        private static void LogTypeDurations(Dictionary<string, double> durationsByType, Dictionary<string, int> countsByType)
        {
            foreach (var pair in durationsByType)
            {
                var count = countsByType[pair.Key];
                Debug.Log($"[StartupProfile] BlockFactoryType blockType={pair.Key} count={count} elapsedMs={pair.Value:F3} avgMs={pair.Value / count:F3}");
            }
        }

        private static void MeasureTicks(uint tickCount)
        {
            var stopwatch = Stopwatch.StartNew();
            for (var i = 0u; i < tickCount; i++) GameUpdater.Update();
            stopwatch.Stop();
            Debug.Log($"[StartupProfile] GameUpdaterTicks ticks={tickCount} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3} avgMs={stopwatch.Elapsed.TotalMilliseconds / tickCount:F3}");
        }
    }
}
