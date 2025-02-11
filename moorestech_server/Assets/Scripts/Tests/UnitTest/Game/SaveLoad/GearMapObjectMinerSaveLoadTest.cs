using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Block.Blocks.MapObjectMiner;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    /// <summary>
    /// GearMapObjectMiner（ギア採掘機）のセーブ・ロードにおいて、
    /// 採掘対象（map object）の残り採掘時間とチェストの内容が正しく保存・復元されるかを検証するテスト。
    /// </summary>
    public class GearMapObjectMinerSaveLoadTest
    {
        [Test]
        public void SaveLoadTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            // GearMapObjectMinerブロックの生成
            // GearMapObjectMiner block generation
            var posInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var minerBlock = blockFactory.Create(ForUnitTestModBlockId.GearMapObjectMiner, new BlockInstanceId(1), posInfo);
            var processor = minerBlock.GetComponent<VanillaGearMapObjectMinerProcessorComponent>();
            
            // 非公開フィールド _miningTargetInfos をリフレクションで取得する
            // Get the private field _miningTargetInfos through reflection
            var mtField = typeof(VanillaGearMapObjectMinerProcessorComponent).GetField("_miningTargetInfos", BindingFlags.NonPublic | BindingFlags.Instance);
            var miningTargetInfos = mtField.GetValue(processor) as Dictionary<Guid, MiningTargetInfo>;
            
            // 採掘対象の残り採掘時間を設定する
            // Set the remaining mining time of the mining target
            var firstKey = miningTargetInfos.Keys.GetEnumerator().Current;
            miningTargetInfos.TryGetValue(firstKey, out var miningTargetInfo);
            const float testRemainingTime = 0.5f;
            miningTargetInfo.RemainingMiningTime = testRemainingTime;
            
            
            
            // 保存状態を取得
            // Get the save state
            var save = processor.GetSaveState();
            var states = new Dictionary<string, string>() { { processor.SaveKey, save } };
            
            
            // --- セーブ状態から新規ロードする ---
            var loadedBlock = blockFactory.Load(minerBlock.BlockGuid, new BlockInstanceId(1), states, posInfo);
            var loadedProcessor = loadedBlock.GetComponent<VanillaGearMapObjectMinerProcessorComponent>();
            
            
            // 非公開フィールド _miningTargetInfos をリフレクションで取得する
            // Get the private field _miningTargetInfos through reflection
            var loadedMtField = typeof(VanillaGearMapObjectMinerProcessorComponent).GetField("_miningTargetInfos", BindingFlags.NonPublic | BindingFlags.Instance);
            var loadedMiningTargetInfos = loadedMtField.GetValue(loadedProcessor) as Dictionary<Guid, MiningTargetInfo>;
            
            // 採掘対象の残り採掘時間が一致するかチェック
            // Check if the remaining mining time of the mining target matches
            loadedMiningTargetInfos.TryGetValue(firstKey, out var loadedMiningTargetInfo);
            
            Assert.AreEqual(testRemainingTime, loadedMiningTargetInfo.RemainingMiningTime);
        }
    }
}
