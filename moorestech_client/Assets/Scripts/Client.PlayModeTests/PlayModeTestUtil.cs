using System;
using System.IO;
using Client.Common;
using Client.Game.InGame.Context;
using Client.Starter;
using Core.Master;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;
using Server.Protocol.PacketResponse;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Client.PlayModeTests
{
    public class PlayModeTestUtil
    {
        public static string PlayModeTestServerDirectoryPath => Path.Combine(Environment.CurrentDirectory, "../", "moorestech_client", "Assets/Scripts/Client.PlayModeTests/ServerData");
        
        public static async UniTask LoadMainGame(string serverDirectory = null)
        {
            serverDirectory ??= PlayModeTestServerDirectoryPath;
            
            // 初期化シーンをロード
            // Load the initialization scene
            SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
            
            // シーンのオブジェクトが初期化されるまで1フレーム待機
            // Wait 1 frame for scene objects to initialize
            await UniTask.Yield();
            
            SetInitializeProperty();
            
            await WaitStartServer();
            
            #region Internal
            
            // 初期化プロパティをセット
            // Set the initialization properties
            void SetInitializeProperty()
            {
                // 既存のセーブデータをロードさせず、オートセーブもしないようにする
                var defaultProperties = InitializeProprieties.CreateDefault();
                var properties = new StartServerSettings
                {
                    SaveFilePath = String.Empty,
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
    }
}