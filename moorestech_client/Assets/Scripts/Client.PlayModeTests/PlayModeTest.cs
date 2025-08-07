using System.Threading.Tasks;
using Client.Common;
using Client.Starter;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Client.PlayModeTests
{
    public class PlayModeTest : InputTestFixture
    {
        [Test]
        public async Task  NewTestScriptSimplePasses()
        {
            SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
            
            // GameInitializerSceneLoaderが表示されるか15秒のタイムアウトを待つ
            // Wait for GameInitializerSceneLoader to appear or 15 seconds timeout
            var timeout = UniTask.Delay(15000);
            var waitForLoader = UniTask.WaitUntil(() => Object.FindObjectOfType<GameInitializerSceneLoader>() != null);
            await UniTask.WhenAny(waitForLoader, timeout);
            
            var loader = Object.FindObjectOfType<GameInitializerSceneLoader>();
            Assert.IsNotNull(loader, "GameInitializerSceneLoader was not found within 15 seconds");
        }
    }
}
