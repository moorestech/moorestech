using System.Collections;
using Client.Game.InGame.UI.Challenge;
using Client.Tests.PlayModeTest.Util;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Client.Tests.PlayModeTest
{
    public class BeltConveyorItemObjectTest
    {
        [UnityTest]
        public IEnumerator BeltConveyorItem()
        {
            yield return new EnterPlayMode(expectDomainReload: true);
            
            yield return Test().ToCoroutine();
            
            yield return new ExitPlayMode();
            
            #region Internal
            
            async UniTask Test()
            {
                await PlayModeTestUtil.LoadMainGame();
            }
            
            #endregion
        }
    }
}