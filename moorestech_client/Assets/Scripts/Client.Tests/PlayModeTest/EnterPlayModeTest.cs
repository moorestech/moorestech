using System.Collections;
using Client.Tests.PlayModeTest.Util;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.PlayModeTest
{
    /// <summary>
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// This test is executed in EditMode, but it switches to PlayMode during execution.
    /// </summary>
    public class EnterPlayModeTest
    {
        [UnityTest]
        public IEnumerator EnterTest()
        {
            yield return new EnterPlayMode(expectDomainReload: true);
            
            yield return LoadMainGame().ToCoroutine();
            
            yield return new ExitPlayMode();
            
            #region Internal
            
            async UniTask LoadMainGame()
            {
                await PlayModeTestUtil.LoadMainGame();
            }
            
            #endregion
        }
        
    }
}