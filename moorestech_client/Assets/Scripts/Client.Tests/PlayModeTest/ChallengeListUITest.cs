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
    public class ChallengeListUITest
    {
        [UnityTest]
        public IEnumerator CategoryElementTest()
        {
            yield return new EnterPlayMode(expectDomainReload: true);
            
            yield return Test().ToCoroutine();
            
            yield return new ExitPlayMode();
            
            #region Internal
            
            async UniTask Test()
            {
                await PlayModeTestUtil.LoadMainGame();
                
                var challengeListView = Object.FindFirstObjectByType<ChallengeListView>(FindObjectsInactive.Include);
                var categoryParent = challengeListView.DebugCategoryListParent;
                
                // 子要素（カテゴリの要素）が1個であることを確認
                // Confirm that there is one child element (category element)
                Assert.AreEqual(1, GetCategoryCount(categoryParent));
                
                // アイテムを付与してチャレンジ1を完了
                // Grant an item and complete Challenge 1
                await PlayModeTestUtil.GiveItem("小石", 1);
                
                // カテゴリが増えていないことを確認
                // Confirm that the category has not increased
                Assert.AreEqual(1, GetCategoryCount(categoryParent));
                
                await PlayModeTestUtil.GiveItem("石器", 1);
                
                // 上記のチャレンジクリアによってカテゴリが増えることを確認
                // Confirm that the category increases due to the completion of the above challenge
                Assert.AreEqual(2, GetCategoryCount(categoryParent));
            }
            
            int GetCategoryCount(Transform categoryParent)
            {
                return categoryParent.GetComponentsInChildren<ChallengeListViewCategoryElement>().Length;
            }
            
            #endregion
        }
    }
}
