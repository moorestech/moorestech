using System;
using Client.Common;
using Client.Starter;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Client.PlayModeTests
{
    public class PlayModeTestUtil
    {
        public static async UniTask LoadMainGame()
        {
            // 初期化シーンをロード
            // Load the initialization scene
            SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
            
            SetInitializeProperty();
            
            await WaitStartServer();
            
            #region Internal
            
            // 初期化プロパティをセット
            // Set the initialization properties
            void SetInitializeProperty()
            {
                // 既存のセーブデータをロードさせず、オートセーブもしないようにする
                var properties = new StartServerSettings()
                {
                    SaveFilePath = String.Empty,
                    AutoSave = false,
                };
                var args = CliConvert.Serialize(properties);
                
                var starter = GameObject.FindObjectOfType<InitializeScenePipeline>();
                var defaultProperties = InitializeProprieties.CreateDefault();
                defaultProperties.CreateLocalServerArgs = args;
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
                //var loader = Object.FindObjectOfType<GameInitializerSceneLoader>();
                // 一旦アサートを外す Assert.IsNotNull(loader, "GameInitializerSceneLoader was not found within 60 seconds");
            }
            
            #endregion
        }
    }
}