using Client.Common;
using Client.Starter;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.PlayModeTests
{
    public class PlayModeTestUtil
    {
        public static async UniTask LoadMainGame()
        {
            SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
                
            // セーブデータだけ別にする
            var starter = GameObject.FindObjectOfType<InitializeScenePipeline>();
            var defaultProperties = InitializeProprieties.CreateDefault();
            starter.SetProperty(defaultProperties);
            
            // GameInitializerSceneLoaderが表示されるか15秒のタイムアウトを待つ
            // Wait for GameInitializerSceneLoader to appear or 15 seconds timeout
            var timeout = UniTask.Delay(15000);
            var waitForLoader = UniTask.WaitUntil(() => Object.FindObjectOfType<GameInitializerSceneLoader>() != null);
            await UniTask.WhenAny(waitForLoader, timeout);
            
            // タイムアウトしてるかどうかを判定
            // Check if the timeout occurred
            var loader = Object.FindObjectOfType<GameInitializerSceneLoader>();
            Assert.IsNotNull(loader, "GameInitializerSceneLoader was not found within 15 seconds");
        }
    }
}