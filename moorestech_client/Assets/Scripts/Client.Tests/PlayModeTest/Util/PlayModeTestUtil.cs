using System;
using System.IO;
using Client.Common;
using Client.Game.InGame.Context;
using Client.Starter;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;
using Server.Protocol.PacketResponse;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Client.Tests.PlayModeTest.Util
{
    public class PlayModeTestUtil
    {
        public static string PlayModeTestServerDirectoryPath => Path.Combine(Environment.CurrentDirectory, "../", "moorestech_client", "Assets/Scripts/Client.Tests/PlayModeTest/ServerData");
        
        public static async UniTask LoadMainGame(string serverDirectory = null, string saveFilePath = null)
        {
            saveFilePath ??= $"dummy_play_mode_test_{Guid.NewGuid()}.json";
            serverDirectory ??= PlayModeTestServerDirectoryPath;
            
            // 初期化シーンをロード
            // Load the initialization scene
            SceneManager.sceneLoaded += SetInitializeProperty;
            SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
            
            // シーンのオブジェクトが初期化されるまで1フレーム待機
            // Wait 1 frame for scene objects to initialize
            await UniTask.Yield();
            
            await WaitStartServer();
            
            await UniTask.Delay(TimeSpan.FromSeconds(1));
            
            #region Internal
            
            // 初期化プロパティをセット
            // Set the initialization properties
            void SetInitializeProperty(Scene scene, LoadSceneMode mode)
            {
                SceneManager.sceneLoaded -= SetInitializeProperty;
                
                // 既存のセーブデータをロードさせず、オートセーブもしないようにする
                var defaultProperties = InitializeProprieties.CreateDefault();
                var properties = new StartServerSettings
                {
                    SaveFilePath = saveFilePath,
                    AutoSave = false,
                    ServerDataDirectory = serverDirectory,
                };
                defaultProperties.CreateLocalServerArgs = CliConvert.Serialize(properties);
                
                var starter = GameObject.FindObjectOfType<InitializeScenePipeline>();
                starter.SetProperty(defaultProperties);
            }
            
            async UniTask WaitStartServer()
            {
                // GameInitializerSceneLoaderが表示されるか60秒のタイムアウトを待つ
                // Wait for GameInitializerSceneLoader to appear or 15 seconds timeout
                var timeout = UniTask.Delay(60000);
                var waitForLoader = UniTask.WaitUntil(() => Object.FindObjectOfType<GameInitializerSceneLoader>() != null);
                await UniTask.WhenAny(waitForLoader, timeout);
                
                
                // タイムアウトしてるかどうかを判定
                // Check if the timeout occurred
                var loader = Object.FindObjectOfType<GameInitializerSceneLoader>();
                Assert.IsNotNull(loader, "GameInitializerSceneLoader was not found within 60 seconds. This usually means the initialization failed.");
            }
            
            #endregion
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
            
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            var command = $"{SendCommandProtocol.GiveCommand} {playerId} {giveItemId} {count}";
            ClientContext.VanillaApi.SendOnly.SendCommand(command);
            
            await UniTask.Delay(1000);
        }
        
        public static IBlock PlaceBlock(string blockName, Vector3Int position, BlockDirection direction)
        {
            var blockId = new BlockId(-1);
            foreach (var id in MasterHolder.BlockMaster.GetBlockAllIds())
            {
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(id);
                if (blockMaster.Name != blockName) continue;
                blockId = id;
            }
            if (blockId.AsPrimitive() == -1)
            {
                throw new ArgumentException($"Block not found: {blockName}");
            }
            
            ServerContext.WorldBlockDatastore.TryAddBlock(
                blockId,
                position,
                direction,
                out var block
            );
            
            return block;
        }
        
        public static bool RemoveBlock(Vector3Int position)
        {
            return ServerContext.WorldBlockDatastore.RemoveBlock(position);
        }
        
        public static IItemStack InsertItemToBlock(IBlock block, ItemId itemId, int count)
        {
            var blockInventory = block.GetComponent<IBlockInventory>();
            var itemStack = ServerContext.ItemStackFactory.Create(itemId, count);
            
            return blockInventory.InsertItem(itemStack);
        }
    }
}