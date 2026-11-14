using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Context;
using Client.Starter;
using Client.Tests.EditModeInPlayingTest.Util.Boot;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.MapGeneration.Transfer;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;
using Server.Protocol.PacketResponse;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.EditModeInPlayingTest.Util
{
    public class EditModeInPlayingTestUtil
    {
        public static string EditModeInPlayingTestServerDirectoryPath => Path.Combine(Environment.CurrentDirectory, "../", "moorestech_client", "Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData");
        
        public static void EnterPlayModeUtil()
        {
            // テスト中はデバッグオブジェクトの生成を無効化（ドメインリロード後も保持される）
            // Disable debug object creation during test (persists across domain reload).
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", true);

            // 応答者のいないテスト起動なので開始ゲートを出さない。印は起動時にゲートが読んだ時点で消費される
            // A test boot has nobody to answer, so the start gates stay closed; the gate consumes the mark when it reads it at boot
            PlaytestStartGateBypass.Apply();

            AssetBundle.UnloadAllAssetBundles(true);
        }
        
        // 初期化の完了（GameInitializedEvent）まで待ってから戻る
        // Returns only after initialization completes (GameInitializedEvent)
        public static async UniTask LoadMainGame(string serverDirectory = null, string worldDirectory = null)
        {
            await MainGameTestBoot.BootUntilInitialized(serverDirectory, worldDirectory, WorldMapMode.Template);
        }

        // 初期化の前で止まる起動（出展モードのゲートの待機中など）を見るテスト向けに、ローダーが出た時点で戻る入口
        // Entry that returns once the loader appears, for tests observing a boot that halts before initialization (e.g. the exhibition gate)
        public static async UniTask LoadMainGameUntilLoaderShown(string serverDirectory, string worldDirectory)
        {
            await MainGameTestBoot.BootUntilLoaderShown(serverDirectory, worldDirectory, WorldMapMode.Template);
        }

        // 地形生成を伴うワールドで起動したいテスト向けにmapModeを明示する入口。初期化の完了まで待つ
        // Entry point for tests that need to boot a world provisioned with a specific map mode; waits for initialization
        public static async UniTask LoadMainGameWithMapMode(string serverDirectory, string worldDirectory, string mapMode)
        {
            await MainGameTestBoot.BootUntilInitialized(serverDirectory, worldDirectory, mapMode);
        }

        // 既存のセーブデータをロードさせず、オートセーブも常時記録もしないようにする起動設定を作る
        // Build boot settings that skip loading an existing save and disable both auto-save and always-on capture
        public static StartServerSettings CreateServerSettings(string worldDirectory, string serverDirectory, string mapMode)
        {
            return new StartServerSettings
            {
                WorldDirectory = worldDirectory,
                AutoSave = false,
                ServerDataDirectory = serverDirectory,
                MapMode = mapMode,
            };
        }

        public static async UniTask GiveItem(string itemName, int count)
        {
            var giveItemId = new ItemId(-1);
            foreach (var itemId in  MasterHolder.ItemMaster.GetItemAllIds())
            {
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                if (itemMaster.Name != itemName) continue;
                giveItemId = itemId;
            }
            if (giveItemId.AsPrimitive() == -1)
            {
                throw new ArgumentException($"Item not found: {itemName}");
            }
            
            var command = $"{SendCommandProtocol.GiveCommand} {giveItemId} {count}";
            ClientContext.VanillaApi.SendOnly.SendCommand(command);
            
            await UniTask.Delay(1000);
        }
        
        public static BlockId FindBlockIdByName(string blockName)
        {
            foreach (var id in MasterHolder.BlockMaster.GetBlockAllIds())
            {
                if (MasterHolder.BlockMaster.GetBlockMaster(id).Name == blockName) return id;
            }
            throw new ArgumentException($"Block not found: {blockName}");
        }
        
        public static IBlock PlaceBlock(string blockName, Vector3Int position, BlockDirection direction)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(
                FindBlockIdByName(blockName),
                position,
                direction, Array.Empty<BlockCreateParam>(), out var block);
            
            return block;
        }
        
        public static async UniTask<BlockGameObject> WaitBlockGameObjectSpawn(Vector3Int position)
        {
            // クライアント側のBlockGameObjectがスポーンするまで待機する
            // Wait until the client-side BlockGameObject spawns
            BlockGameObject blockGameObject = null;
            for (var i = 0; i < 180 && blockGameObject == null; i++)
            {
                ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(position, out blockGameObject);
                await UniTask.Yield();
            }
            Assert.IsNotNull(blockGameObject, $"client BlockGameObject not spawned at {position}");
            return blockGameObject;
        }

        public static bool RemoveBlock(Vector3Int position)
        {
            return ServerContext.WorldBlockDatastore.RemoveBlock(position, BlockRemoveReason.ManualRemove);
        }
        
        public static IItemStack InsertItemToBlock(IBlock block, ItemId itemId, int count)
        {
            var blockInventory = block.GetComponent<IBlockInventory>();
            var itemStack = ServerContext.ItemStackFactory.Create(itemId, count);

            return blockInventory.InsertItem(itemStack, InsertItemContext.Empty);
        }

        // 意図した終了を待ち、保存と終了参加者が完了したことを確かめる
        // Await an intentional exit and verify saving and shutdown participants completed
        public static async UniTask AssertIntentionalShutdownFlushedAsync(string logLabel)
        {
            var flushResult = await GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit);
            Debug.Log($"[{logLabel}] result:{flushResult}");
            Assert.AreEqual(ShutdownFlushResult.Flushed, flushResult);
            await UniTask.Yield();
        }
    }
}
