using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Tests.Investigation
{
    public class StartupWorldBlockLoadBreakdownTest
    {
        [Test]
        public void ProfileCurrentSaveWorldBlockAddAndPostLoad()
        {
            var serverDataDirectory = ServerDirectory.GetDirectory();
            var saveFilePath = MoorestechServerDIContainerOptions.DefaultSaveJsonFilePath;
            Assert.That(Directory.Exists(serverDataDirectory), Is.True, serverDataDirectory);
            Assert.That(File.Exists(saveFilePath), Is.True, saveFilePath);

            // 実セーブのブロック復元を段階ごとに再現する
            // Reproduce real save block restoration by phase.
            var options = new MoorestechServerDIContainerOptions(serverDataDirectory)
            {
                saveJsonFilePath = new SaveJsonFilePath(saveFilePath)
            };
            var saveJson = File.ReadAllText(saveFilePath);
            new MoorestechServerDIContainerGenerator().Create(options);
            var saveData = JsonConvert.DeserializeObject<WorldSaveAllInfoV1>(saveJson);

            var blocks = LoadBlocks(saveData);
            AddBlocksToDatastore(blocks);
            RunPostBlockLoad();

            GameUpdater.Dispose();
        }

        private static List<IBlock> LoadBlocks(WorldSaveAllInfoV1 saveData)
        {
            var blocks = new List<IBlock>(saveData.World.Count);
            var blockFactory = ServerContext.BlockFactory;
            var stopwatch = Stopwatch.StartNew();

            // Factory復元は登録前に全件まとめて測る
            // Measure factory restoration before registration.
            foreach (var blockSave in saveData.World)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(blockSave.BlockGuid);
                var direction = (BlockDirection)blockSave.Direction;
                var size = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
                var positionInfo = new BlockPositionInfo(blockSave.Pos, direction, size);
                blocks.Add(blockFactory.Load(blockSave.BlockGuid, new BlockInstanceId(blockSave.InstanceId), blockSave.ComponentStates, positionInfo));
            }

            stopwatch.Stop();
            Debug.Log($"[StartupProfile] BreakdownBlockFactoryLoad elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3} blocks={blocks.Count}");
            return blocks;
        }

        private static void AddBlocksToDatastore(List<IBlock> blocks)
        {
            var datastore = ServerContext.WorldBlockDatastore;
            var method = datastore.GetType().GetMethod("TryAddBlock", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var stopwatch = Stopwatch.StartNew();
            var added = 0;

            // 実装と同じprivate登録処理を反射で呼ぶ
            // Invoke the same private registration method by reflection.
            foreach (var block in blocks)
            {
                if ((bool)method.Invoke(datastore, new object[] { block })) added++;
            }

            stopwatch.Stop();
            Debug.Log($"[StartupProfile] BreakdownTryAddBlock elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3} blocks={blocks.Count} added={added}");
        }

        private static void RunPostBlockLoad()
        {
            var durationsByType = new Dictionary<string, double>();
            var countsByType = new Dictionary<string, int>();
            var componentCount = 0;
            var stopwatch = Stopwatch.StartNew();

            // 全ブロック登録後のpost-loadを型別に測る
            // Measure post-load after all blocks are registered.
            foreach (var blockData in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
            {
                foreach (var component in blockData.Block.ComponentManager.GetComponents<IPostBlockLoad>())
                {
                    var componentStopwatch = Stopwatch.StartNew();
                    component.OnPostBlockLoad();
                    componentStopwatch.Stop();
                    componentCount++;
                    AddTypeDuration(component.GetType().Name, componentStopwatch.Elapsed.TotalMilliseconds, durationsByType, countsByType);
                }
            }

            stopwatch.Stop();
            Debug.Log($"[StartupProfile] BreakdownPostBlockLoad elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3} components={componentCount}");
            LogTypeDurations(durationsByType, countsByType);
        }

        private static void AddTypeDuration(string typeName, double elapsedMs, Dictionary<string, double> durationsByType, Dictionary<string, int> countsByType)
        {
            if (!durationsByType.ContainsKey(typeName))
            {
                durationsByType.Add(typeName, 0);
                countsByType.Add(typeName, 0);
            }
            durationsByType[typeName] += elapsedMs;
            countsByType[typeName]++;
        }

        private static void LogTypeDurations(Dictionary<string, double> durationsByType, Dictionary<string, int> countsByType)
        {
            foreach (var pair in durationsByType)
            {
                var count = countsByType[pair.Key];
                Debug.Log($"[StartupProfile] PostBlockLoadType type={pair.Key} count={count} elapsedMs={pair.Value:F3} avgMs={pair.Value / count:F3}");
            }
        }
    }
}
