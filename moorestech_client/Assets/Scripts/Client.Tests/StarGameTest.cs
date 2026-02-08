using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using static Client.Tests.PlayModeTest.Util.PlayModeTestUtil;

namespace Client.Tests
{
    public class StarGameTest
    {
        [UnityTest]
        public IEnumerator BeltConveyorItemEntityPositionTest()
        {
            yield return new EnterPlayMode(expectDomainReload: true);
            
            yield return SetUp().ToCoroutine();
            
            yield return new ExitPlayMode();
            
            #region Internal
            
            async UniTask SetUp()
            {
                var loadTask = LoadMainGame();
                var timeOuter = UniTask.Delay(TimeSpan.FromSeconds(30));
                
                var result = await UniTask.WhenAny(loadTask, timeOuter);
                if (result == 1)
                {
                   Assert.Fail("LoadMainGame timed out.");
                }
            }
            
            #endregion
        }
    }
}